import base64
import json
import tempfile
import threading
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer
from pathlib import Path

from server import State, handler_for
from map_catalog import MapCatalog
from visualmap_core import Localizer


def main():
    root = Path(__file__).resolve().parent
    evidence = json.loads((root / "synthetic-result.json").read_text(encoding="utf-8"))
    folder = root / "tests-output" / evidence["map"]["id"]
    with tempfile.TemporaryDirectory(prefix="jarvis-http-") as temporary:
        state = State(temporary, "http://127.0.0.1")
        state.active = Localizer(folder)
        state.catalog = MapCatalog(folder.parent)
        state.localizers[state.active.data["id"]] = state.active
        state.job = {"state": "ready", "stage": "Synthetic test ONLY", "step": 5}
        server = ThreadingHTTPServer(("127.0.0.1", 0), handler_for(state))
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f"http://127.0.0.1:{server.server_port}"
        headers = {"Authorization": "Bearer " + state.token, "Content-Type": "application/json"}
        try:
            model = json.loads(urllib.request.urlopen(urllib.request.Request(base + "/map", headers=headers)).read())
            query = {"mapId": model["id"], "requestId": "http-held-out", "fx": 750, "fy": 750, "cx": 450, "cy": 337.5,
                     "image": base64.b64encode((folder / "held-out-query.jpg").read_bytes()).decode()}
            body = json.dumps(query).encode()
            response = json.loads(urllib.request.urlopen(urllib.request.Request(base + "/localize", data=body, headers=headers), timeout=30).read())
            assert response["ok"] and response["requestId"] == query["requestId"] and response["mapId"] == model["id"]
            query["mapId"] = "wrong-map"
            try:
                urllib.request.urlopen(urllib.request.Request(base + "/localize", data=json.dumps(query).encode(), headers=headers))
                raise AssertionError("Wrong map accepted")
            except urllib.error.HTTPError as error:
                assert error.code == 409
            state.job["state"] = "building"
            continuing = json.loads(urllib.request.urlopen(urllib.request.Request(base + "/localize", data=body, headers=headers), timeout=30).read())
            assert continuing["ok"] and continuing["mapId"] == model["id"]
            try:
                urllib.request.urlopen(urllib.request.Request(base + "/map", headers=headers))
                raise AssertionError("Stale map served during rebuild")
            except urllib.error.HTTPError as error:
                assert error.code == 409
            report = {"PASS": True, "evidence": "Synthetic images over real localhost HTTP, NOT Android camera acceptance",
                      "inliers": response["inliers"], "medianError": response["medianError"], "wrongMapRejected": True, "legacyActiveBlockedDuringBuild": True, "explicitMapStillUsableDuringAnotherBuild": True}
            (root / "http-result.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
            print(json.dumps(report))
        finally:
            server.shutdown()
            server.server_close()
            state.executor.shutdown()


if __name__ == "__main__":
    main()
