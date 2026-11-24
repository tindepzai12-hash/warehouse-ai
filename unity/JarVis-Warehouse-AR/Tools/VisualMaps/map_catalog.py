import json
import os
import re
import threading
from pathlib import Path


def valid_id(identifier):
    return isinstance(identifier, str) and re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]{0,95}", identifier) is not None


def clean_name(value):
    if not isinstance(value, str):
        raise ValueError("Tên map phải là chữ.")
    result = " ".join(value.split())
    if not 1 <= len(result) <= 64 or any(ord(character) < 32 for character in result):
        raise ValueError("Tên map cần 1–64 ký tự.")
    return result


def atomic_json(path, data):
    path = Path(path)
    temporary = path.with_suffix(path.suffix + ".pending")
    temporary.write_text(json.dumps(data, ensure_ascii=False, allow_nan=False), encoding="utf-8")
    os.replace(temporary, path)


class MapCatalog:
    def __init__(self, root):
        self.root = Path(root).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.guard = threading.RLock()

    def folder(self, identifier):
        if not valid_id(identifier):
            raise ValueError("Map ID không hợp lệ.")
        path = self.root / identifier
        if path.is_symlink() or path.resolve().parent != self.root:
            raise ValueError("Không truy cập map ngoài thư viện.")
        return path

    def read(self, identifier):
        folder = self.folder(identifier)
        path = folder / "map.json"
        if not path.is_file():
            raise FileNotFoundError("Map chưa dựng xong hoặc không tồn tại.")
        data = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(data, dict):
            raise ValueError("Dữ liệu map không hợp lệ.")
        if data.get("id") != identifier:
            raise ValueError("Map ID không khớp dữ liệu lưu.")
        metadata = folder / "metadata.json"
        if metadata.is_file():
            extra = json.loads(metadata.read_text(encoding="utf-8"))
            if not isinstance(extra, dict):
                raise ValueError("Metadata map không hợp lệ.")
            if "name" in extra:
                data["name"] = clean_name(extra["name"])
        data.setdefault("name", "Phòng " + identifier[:8])
        data.setdefault("revision", 1)
        data.setdefault("navigationAllowed", False)
        data.setdefault("geometryState", "missing")
        if not isinstance(data.get("points", []), list):
            raise ValueError("Tuyến lưu trong map không hợp lệ.")
        return data

    def entries(self):
        result = []
        with self.guard:
            for folder in self.root.iterdir():
                if not folder.is_dir() or not valid_id(folder.name):
                    continue
                try:
                    data = self.read(folder.name)
                except (ValueError, OSError, KeyError):
                    continue
                result.append({key: data.get(key) for key in (
                    "id", "name", "revision", "parentId", "state", "registered", "total", "builtUtc",
                    "geometryState", "navigationAllowed", "objectCount")})
                result[-1]["routePoints"] = len(data.get("points", []))
                if len(result) >= 500:
                    break
        return sorted(result, key=lambda item: item.get("builtUtc") or "", reverse=True)

    def rename(self, identifier, name):
        name = clean_name(name)
        with self.guard:
            self.read(identifier)
            path = self.folder(identifier) / "metadata.json"
            metadata = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
            metadata["name"] = name
            atomic_json(path, metadata)
        return self.read(identifier)

    def lineage(self, parent_id):
        if not parent_id:
            return 1
        return int(self.read(parent_id)["revision"]) + 1
