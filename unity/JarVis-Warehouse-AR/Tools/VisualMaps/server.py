import argparse
import base64
import concurrent.futures
import hmac
import io
import ipaddress
import json
import os
import secrets
import socket
import sys
import threading
import uuid
import zipfile
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import cv2
import numpy as np
import qrcode
from PIL import Image

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "vendor" / "hloc"))
from visualmap_core import Localizer, MAX_IMAGES, reconstruct, validate_manifest
from map_catalog import MapCatalog, atomic_json
from map_geometry import validate_depth
from object_analysis import ObjectAnalyzer
from dashboard import render_dashboard

MAX_UPLOAD = 220 * 1024 * 1024


def extract_capture(archive, destination):
    destination = Path(destination)
    with zipfile.ZipFile(archive) as source:
        entries = source.infolist()
        names = [entry.filename for entry in entries]
        if len(entries) > MAX_IMAGES * 3 + 1 or len(set(names)) != len(names) or sum(entry.file_size for entry in entries) > MAX_UPLOAD:
            raise ValueError("Gói ảnh quá lớn hoặc trùng tên.")
        if "manifest.json" not in names or source.getinfo("manifest.json").file_size > 2 * 1024 * 1024:
            raise ValueError("Thiếu manifest hợp lệ.")
        manifest = json.loads(source.read("manifest.json").decode("utf-8"))
        frames, _ = validate_manifest(manifest)
        expected = {"manifest.json"} | {f"images/{frame['name']}" for frame in frames}
        depth_sizes = {}
        for frame in frames:
            paths = validate_depth(frame)
            expected.update(paths)
            if paths:
                pixels = frame["depth"]["width"] * frame["depth"]["height"]
                depth_sizes[paths[0]], depth_sizes[paths[1]] = pixels * 4, pixels
        if set(names) != expected:
            raise ValueError("Gói ảnh có file thừa / thiếu; không giải nén.")
        for entry in entries:
            if entry.filename in depth_sizes and entry.file_size != depth_sizes[entry.filename]:
                raise ValueError("Dữ liệu depth/confidence sai kích thước; ảnh gốc được giữ.")
            if entry.file_size > 5 * 1024 * 1024 or entry.is_dir() or "\\" in entry.filename or entry.flag_bits & 1 or entry.external_attr >> 16 & 0o170000 == 0o120000:
                raise ValueError("File ảnh không hợp lệ.")
            target = destination / entry.filename
            target.parent.mkdir(parents=True, exist_ok=True)
            with source.open(entry) as incoming, target.open("wb") as outgoing:
                while block := incoming.read(65536):
                    outgoing.write(block)


class State:
    def __init__(self, root, url):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        self.config_path = self.root / "connection.json"
        if self.config_path.exists():
            self.token = json.loads(self.config_path.read_text())["token"]
        else:
            self.token = secrets.token_hex(32)
            self.config_path.write_text(json.dumps({"token": self.token}), encoding="utf-8")
        self.url = url
        self.job = {"state": "idle", "stage": "Chưa có bản đồ phòng", "step": 0}
        self.active = None
        self.guard = threading.Lock()
        self.query_guard = threading.Lock()
        self.catalog = MapCatalog(self.root)
        self.localizers = {}
        self.objects = ObjectAnalyzer(ROOT / "vendor" / "nanodet")
        self.executor = concurrent.futures.ThreadPoolExecutor(max_workers=1)
        pointer = self.root / "active.json"
        if pointer.exists():
            try:
                identifier = json.loads(pointer.read_text())["id"]
                self.active = Localizer(self.catalog.folder(identifier))
                self.localizers[identifier] = self.active
                self.job = {"state": "ready", "stage": "Đã có bản đồ", "step": 7}
            except (OSError, ValueError, KeyError):
                self.job = {"state": "idle", "stage": "Bản chọn trước chưa đọc được; thư viện và dữ liệu gốc vẫn được giữ", "step": 0}

    def localizer(self, identifier):
        with self.guard:
            self.catalog.read(identifier)
            if identifier not in self.localizers:
                candidate = Localizer(self.catalog.folder(identifier))
                if len(self.localizers) >= 2:
                    self.localizers.pop(next(iter(self.localizers)))
                self.localizers[identifier] = candidate
            return self.localizers[identifier]

    def import_file(self, archive):
        with self.guard:
            if self.job["state"] in ("building", "uploading"):
                raise ValueError("PC đang xử lý lượt khác; đợi xong rồi nhập.")
            folder = self.root / uuid.uuid4().hex
            folder.mkdir()
            try:
                extract_capture(archive, folder)
                manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
                self.catalog.lineage(manifest.get("parentId", ""))
            except Exception:
                self.job = {"state": "failed", "stage": "Gói nhập chưa hợp lệ; không thay bản đồ cũ", "step": 0}
                raise
            self.job = {"state": "building", "stage": "Đã nhập ZIP từ máy tính", "step": 0, "id": folder.name}
            self.executor.submit(self.build, folder)
            return folder.name

    def build(self, folder):
        def status(stage, step):
            self.job = {"state": "building", "stage": stage, "step": step, "id": folder.name}
        try:
            data = reconstruct(folder, status)
            data["revision"] = self.catalog.lineage(data.get("parentId", ""))
            atomic_json(folder / "map.json", data)
            candidate = Localizer(folder)
            pending = self.root / "active.pending"
            pending.write_text(json.dumps({"id": folder.name}), encoding="utf-8")
            os.replace(pending, self.root / "active.json")
            with self.guard:
                self.active = candidate
                self.localizers[data["id"]] = candidate
                while len(self.localizers) > 2:
                    self.localizers.pop(next(iter(self.localizers)))
            self.job = {"state": "ready", "stage": "Đã phân tích; xem thư viện, chưa xác nhận tránh vật cản", "step": 7, "id": data["id"],
                        "registered": data["registered"], "total": data["total"], "rmse": data["rmse"]}
        except Exception as error:
            self.job = {"state": "failed", "stage": str(error)[:450], "step": 0, "id": folder.name}
            (folder / "failure.txt").write_text(type(error).__name__ + ": " + str(error), encoding="utf-8")


def handler_for(state):
    class Handler(BaseHTTPRequestHandler):
        server_version = "JarVisMaps/0.1"

        def log_message(self, *_):
            return

        def setup(self):
            super().setup()
            self.connection.settimeout(45)

        def reply(self, code, data):
            body = json.dumps(data, ensure_ascii=False, allow_nan=False).encode("utf-8")
            self.send_response(code)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            self.wfile.write(body)

        def authorized(self):
            if not hmac.compare_digest(self.headers.get("Authorization", ""), "Bearer " + state.token):
                self.reply(401, {"error": "Kết nối lại bằng QR trên máy tính."})
                return False
            return True

        def do_GET(self):
            if self.path in ["/", "/setup.png"]:
                if not ipaddress.ip_address(self.client_address[0]).is_loopback:
                    self.reply(403, {"error": "Mở màn hình kết nối trực tiếp trên PC."})
                    return
                if self.path == "/setup.png":
                    output = io.BytesIO()
                    qrcode.make(f"JVM1|{state.url}|{state.token}").save(output, format="PNG")
                    body, content = output.getvalue(), "image/png"
                else:
                    body = render_dashboard(state.token,state.url)
                    content = "text/html; charset=utf-8"
                self.send_response(200)
                self.send_header("Content-Type", content)
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Cache-Control", "no-store")
                self.end_headers()
                self.wfile.write(body)
                return
            if not self.authorized():
                return
            if self.path == "/status":
                self.reply(200, {**state.job, "hasMap": state.active is not None, "objectsAvailable": state.objects.available()})
            elif self.path == "/maps":
                self.reply(200, {"maps": state.catalog.entries()})
            elif self.path.startswith("/maps/"):
                try:
                    parts = self.path.split("/")
                    data = state.catalog.read(parts[2])
                    if len(parts) == 3:
                        self.reply(200, data)
                    elif len(parts) == 4 and parts[3] in ("geometry", "objects"):
                        report = state.catalog.folder(parts[2]) / (parts[3] + ".json")
                        self.reply(200, json.loads(report.read_text(encoding="utf-8")))
                    else:
                        self.reply(404, {"error": "Không có báo cáo này."})
                except FileNotFoundError:
                    self.reply(404, {"error": "Map/báo cáo chưa có; dữ liệu khác vẫn được giữ."})
                except (ValueError, OSError):
                    self.reply(400, {"error": "ID hoặc dữ liệu map không hợp lệ."})
            elif self.path == "/map":
                if state.job["state"] in ["uploading", "building"]:
                    self.reply(409, {"error": "PC đang dựng bản đồ mới. Chờ xong để không dùng nhầm bản đồ trước."})
                else:
                    self.reply(200, state.active.data) if state.active else self.reply(404, {"error": "Chưa có bản đồ đã dựng thành công."})
            else:
                self.reply(404, {"error": "Không có chức năng này."})

        def do_POST(self):
            if not self.authorized():
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if self.path.startswith("/maps/") and self.path.endswith("/rename"):
                    if not 2 <= length <= 2048:
                        raise ValueError("Tên quá dài.")
                    self.reply(200, state.catalog.rename(self.path.split("/")[2], json.loads(self.rfile.read(length))["name"]))
                    return
                if self.path == "/stop":
                    if not ipaddress.ip_address(self.client_address[0]).is_loopback:
                        self.reply(403, {"error": "Chỉ dừng trực tiếp từ PC."})
                    elif state.job["state"] in ["uploading", "building"]:
                        self.reply(409, {"error": "Đang nhận / dựng ảnh; đợi xong rồi dừng để không ngắt dữ liệu."})
                    else:
                        self.reply(200, {"state": "stopped"})
                        threading.Thread(target=self.server.shutdown, daemon=True).start()
                    return
                if self.path == "/capture":
                    if state.job["state"] in ["uploading", "building"]:
                        self.reply(409, {"error": "PC đang xử lý một lượt chụp. Chờ hoàn tất rồi thử lại."})
                        return
                    if not 100 <= length <= MAX_UPLOAD:
                        self.reply(413, {"error": "Gói ảnh vượt giới hạn 220 MB hoặc rỗng."})
                        return
                    if not state.guard.acquire(blocking=False):
                        self.reply(409, {"error": "PC đang nhận ảnh."})
                        return
                    folder = state.root / uuid.uuid4().hex
                    folder.mkdir()
                    state.job = {"state": "uploading", "stage": "Đang nhận ảnh", "step": 0}
                    try:
                        archive = folder / "capture.zip"
                        with archive.open("wb") as outgoing:
                            remaining = length
                            while remaining:
                                block = self.rfile.read(min(65536, remaining))
                                if not block:
                                    raise ValueError("Upload bị ngắt. Ảnh gốc vẫn còn trên điện thoại.")
                                outgoing.write(block)
                                remaining -= len(block)
                        extract_capture(archive, folder)
                        manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
                        state.catalog.lineage(manifest.get("parentId", ""))
                        state.job = {"state": "building", "stage": "Đã nhận ảnh; chuẩn bị dựng", "step": 0, "id": folder.name}
                        state.executor.submit(state.build, folder)
                    except Exception:
                        state.job = {"state": "failed", "stage": "Nhận ảnh thất bại; thử gửi lại.", "step": 0}
                        raise
                    finally:
                        state.guard.release()
                    self.reply(202, {"id": folder.name, "state": "building"})
                elif self.path in ("/localize", "/objects"):
                    if not 10 < length <= 3 * 1024 * 1024:
                        self.reply(413, {"error": "Ảnh truy vấn quá lớn."})
                        return
                    if not state.query_guard.acquire(blocking=False):
                        self.reply(409, {"error": "Đang đối chiếu ảnh trước. Thử lại sau."})
                        return
                    try:
                        data = json.loads(self.rfile.read(length).decode("utf-8"))
                        active = None
                        if self.path == "/localize":
                            try:
                                active = state.localizer(data.get("mapId"))
                            except (ValueError, FileNotFoundError):
                                self.reply(409, {"error": "Map được chọn chưa tồn tại hoặc chưa dựng xong. Chọn lại từ thư viện."})
                                return
                        encoded = base64.b64decode(data["image"], validate=True)
                        with Image.open(io.BytesIO(encoded)) as header:
                            if header.format != "JPEG" or min(header.size) < 64 or max(header.size) > 2048:
                                raise ValueError("Chỉ nhận JPEG camera tối đa 2048 pixel mỗi cạnh.")
                        image = cv2.imdecode(np.frombuffer(encoded, np.uint8), cv2.IMREAD_COLOR if self.path == "/objects" else cv2.IMREAD_GRAYSCALE)
                        if image is None:
                            raise ValueError("Không đọc được ảnh.")
                        if self.path == "/objects":
                            self.reply(200, {"requestId": str(data.get("requestId", ""))[:64], **state.objects.detect(image)})
                        else:
                            self.reply(200, active.locate(image, data, str(data.get("requestId", ""))[:64]))
                    finally:
                        state.query_guard.release()
                else:
                    self.reply(404, {"error": "Không có chức năng này."})
            except FileNotFoundError:
                self.reply(404, {"error": "Không tìm thấy map đã chọn. Làm mới thư viện; các map khác vẫn được giữ."})
            except (ValueError, KeyError, zipfile.BadZipFile, UnicodeError) as error:
                self.reply(400, {"error": str(error)[:350] or "Dữ liệu gửi chưa hợp lệ. Ảnh gốc vẫn được giữ."})
            except (BrokenPipeError, ConnectionError, TimeoutError):
                return
            except Exception:
                self.reply(500, {"error": "PC chưa xử lý được. Kiểm tra cửa sổ server / failure.txt; không có kết quả định vị giả."})
    return Handler


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--bind", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--url")
    parser.add_argument("--data", type=Path, default=ROOT / "data")
    args = parser.parse_args()
    address = "127.0.0.1"
    if args.bind != "127.0.0.1":
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
            probe.connect(("1.1.1.1", 80))
            address = probe.getsockname()[0]
        if not ipaddress.ip_address(address).is_private:
            raise ValueError("Chỉ chạy trên LAN riêng, không mở ra Internet.")
    state = State(args.data, args.url or f"http://{address}:{args.port}")
    print(f"JarVis Maps PC: http://127.0.0.1:{args.port} · Giữ cửa sổ này mở.", flush=True)
    server = ThreadingHTTPServer((args.bind, args.port), handler_for(state))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        state.executor.shutdown(wait=False)


if __name__ == "__main__":
    main()
