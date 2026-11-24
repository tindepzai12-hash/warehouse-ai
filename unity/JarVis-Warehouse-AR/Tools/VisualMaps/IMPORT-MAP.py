import argparse
import json
import time
from pathlib import Path

from server import ROOT, State


def main():
    parser = argparse.ArgumentParser(description="Nhập ZIP từ điện thoại; không cần điện thoại cùng Wi-Fi.")
    parser.add_argument("capture", type=Path)
    parser.add_argument("--data", type=Path, default=ROOT / "data")
    arguments = parser.parse_args()
    if not arguments.capture.is_file():
        parser.error("Không tìm thấy ZIP. Dùng đường dẫn đầy đủ và đặt trong dấu ngoặc kép nếu có khoảng trắng.")
    state = State(arguments.data, "http://127.0.0.1:8765")
    try:
        identifier = state.import_file(arguments.capture)
        last = None
        while state.job["state"] in ("building", "uploading"):
            if state.job != last:
                print(json.dumps(state.job, ensure_ascii=False), flush=True)
                last = dict(state.job)
            time.sleep(.5)
        print(json.dumps(state.job, ensure_ascii=False), flush=True)
        print("Dữ liệu và báo cáo:", state.catalog.folder(identifier))
        if state.job["state"] != "ready":
            raise SystemExit(1)
    finally:
        state.executor.shutdown(wait=True)


if __name__ == "__main__":
    main()
