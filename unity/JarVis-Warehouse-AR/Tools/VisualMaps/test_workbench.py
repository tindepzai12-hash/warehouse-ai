import copy
import io
import json
import threading
import urllib.error
import urllib.request
import zipfile
from http.server import ThreadingHTTPServer

import numpy as np
import pytest

from map_catalog import MapCatalog, clean_name, valid_id
from map_geometry import build_geometry, check_corridor, validate_depth
from object_analysis import ObjectAnalyzer
from server import ROOT, State, extract_capture, handler_for
from test_visualmaps import manifest
from visualmap_core import validate_manifest


def room_capture():
    capture = manifest()
    capture.update(schema=2, captureId="a" * 32, name="Phòng ngủ của Tín", purpose="environment")
    for frame in capture["frames"]:
        frame.update(onRoute=False, start=False, end=False)
    return capture


def test_room_capture_has_no_qr_dependency():
    frames, route = validate_manifest(room_capture())
    assert len(frames) == 20 and route == []


def test_floor_optional_for_capture_but_required_for_route():
    data = room_capture()
    data.update(floorKnown=False, floorY=0, captureState="complete")
    for frame in data["frames"]:
        frame["position"][1] = 0
    assert validate_manifest(data)[1] == []
    data["purpose"] = "route"
    with pytest.raises(ValueError, match="mặt sàn"):
        validate_manifest(data)


@pytest.mark.parametrize("state", ["recording", "interrupted", "invalid"])
def test_unfinished_capture_not_published(state):
    data = room_capture()
    data["captureState"] = state
    with pytest.raises(ValueError, match="chưa kết thúc"):
        validate_manifest(data)


def test_empty_depth_metadata_is_not_fake_geometry(tmp_path):
    frame = room_capture()["frames"][0]
    frame["depth"] = {"file": "", "width": 0}
    result = build_geometry(tmp_path, [frame], {frame["name"]: (np.zeros(3), np.eye(3))}, 0, [])
    assert result["state"] == "missing" and not result["navigationAllowed"]


def test_partial_route_not_mislabeled_room():
    data = room_capture()
    data["frames"][-1]["onRoute"] = True
    with pytest.raises(ValueError):
        validate_manifest(data)


@pytest.mark.parametrize("identifier", ["../x", "..\\x", "a/b", "a:b", "", None, "x?", "a" * 97])
def test_invalid_map_id(identifier):
    assert not valid_id(identifier)


def test_multiple_maps_rename_restart_and_lineage(tmp_path):
    catalog = MapCatalog(tmp_path)
    for identifier, name in (("room_a", "Phòng A"), ("room_b", "Kho B")):
        folder = catalog.folder(identifier)
        folder.mkdir()
        (folder / "map.json").write_text(json.dumps({"id": identifier, "name": name, "state": "ready", "points": []}), encoding="utf-8")
    catalog.rename("room_a", "  Phòng   của tôi  ")
    restored = MapCatalog(tmp_path)
    assert len(restored.entries()) == 2
    assert restored.read("room_a")["name"] == "Phòng của tôi"
    assert restored.read("room_b")["name"] == "Kho B"
    assert restored.lineage("room_a") == 2
    assert not restored.read("room_a")["navigationAllowed"]
    with pytest.raises(FileNotFoundError):
        restored.read("missing")


def test_metadata_cannot_change_identity_or_enable_navigation(tmp_path):
    catalog = MapCatalog(tmp_path)
    folder = catalog.folder("room_a")
    folder.mkdir()
    (folder / "map.json").write_text(json.dumps({"id": "room_a", "name": "A", "state": "ready", "navigationAllowed": False}))
    (folder / "metadata.json").write_text(json.dumps({"id": "room_b", "name": "Renamed", "navigationAllowed": True}))
    assert catalog.read("room_a")["id"] == "room_a" and not catalog.read("room_a")["navigationAllowed"]


@pytest.mark.parametrize("name", ["", " " * 5, "x" * 65, None])
def test_name_validation(name):
    with pytest.raises(ValueError):
        clean_name(name)


def test_swept_path_checks_walls_unknown_and_bounds():
    cells = np.ones((50, 50), np.uint8)
    route = [[1, 1], [4, 4]]
    clear = check_corridor(cells, [0, 0], .1, route)
    assert clear["valid"] and clear["reason"] == "candidate"
    assert clear["distanceMetres"] == pytest.approx(3 * np.sqrt(2))
    assert "khoảng trống" in clear["reasonText"]
    cells[25, :] = 2
    blocked = check_corridor(cells, [0, 0], .1, route)
    assert not blocked["valid"] and blocked["blocked"] > 0 and blocked["coverage"] == 1
    assert "vật cản" in blocked["reasonText"]
    cells[:] = 1
    cells[20:25, 20:25] = 0
    unknown = check_corridor(cells, [0, 0], .1, route)
    assert unknown["unknown"] > 0 and "bổ sung" in unknown["reasonText"]
    assert check_corridor(cells, [0, 0], .1, [[0, 0], [2, 2]])["reason"] == "outside"


def test_body_clearance_and_long_segment_not_just_waypoints():
    cells = np.ones((50, 50), np.uint8)
    cells[:, 23] = 2
    assert check_corridor(cells, [0, 0], .1, [[2, 1], [2, 4]])["blocked"] > 0
    assert check_corridor(cells, [0, 0], .1, [[1, 1], [4, 1]])["blocked"] > 0


def depth_frame():
    frame = room_capture()["frames"][0]
    frame["cameraTime"] = 10
    frame["depth"] = {"width": 160, "height": 120, "format": "float32-metres", "file": frame["name"] + ".f32",
                      "confidence": frame["name"] + ".u8", "time": 10.01, "fx": 100, "fy": 100, "cx": 80, "cy": 60}
    return frame


@pytest.mark.parametrize("mutation", [lambda value: value["depth"].update(time=9),
                                      lambda value: value["depth"].update(file="../x"),
                                      lambda value: value["depth"].update(fx=float("nan")),
                                      lambda value: value["depth"].update(width=99999)])
def test_depth_rejects_bad_calibration_path_or_time(mutation):
    frame = depth_frame()
    mutation(frame)
    with pytest.raises(ValueError):
        validate_depth(frame)


def test_depth_optional_and_declared():
    assert len(validate_depth(depth_frame())) == 2
    assert validate_depth(room_capture()["frames"][0]) == []


def test_room_archive_without_route_and_with_depth(tmp_path):
    data = room_capture()
    data["frames"][0] = depth_frame()
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w") as archive:
        archive.writestr("manifest.json", json.dumps(data))
        for frame in data["frames"]:
            archive.writestr("images/" + frame["name"], b"image")
        for filename in validate_depth(data["frames"][0]):
            pixels = data["frames"][0]["depth"]["width"] * data["frames"][0]["depth"]["height"]
            archive.writestr(filename, bytes(pixels * (4 if filename.endswith(".f32") else 1)))
    output.seek(0)
    extract_capture(output, tmp_path / "capture")
    assert (tmp_path / "capture" / "manifest.json").exists()


def test_calibrated_depth_yields_surface_evidence_not_navigation(tmp_path):
    (tmp_path / "depth").mkdir()
    frames, poses = [], {}
    for index in range(2):
        frame = depth_frame()
        frame["name"] = f"{index:05}.jpg"
        frame["depth"].update(width=16, height=16, fx=10, fy=10, cx=8, cy=0,
                              file=frame["name"] + ".f32", confidence=frame["name"] + ".u8")
        depth = np.zeros((16, 16), dtype="<f4")
        for row in range(4, 16):
            depth[row] = 1.3 * 10 / row
        depth[2] = 1
        depth.tofile(tmp_path / "depth" / frame["depth"]["file"])
        np.full((16, 16), 255, np.uint8).tofile(tmp_path / "depth" / frame["depth"]["confidence"])
        frames.append(frame)
        poses[frame["name"]] = np.array([0, 1.3, 0]), np.eye(3)
    report = build_geometry(tmp_path, frames, poses, 0, [])
    assert report["state"] == "surface-preview" and report["depthFrames"] == 2
    assert 1 in report["cells"] and 2 in report["cells"] and 0 in report["cells"]
    assert not report["navigationAllowed"] and (tmp_path / "floor-preview.png").exists()


def test_real_model_positive_fixture():
    import cv2
    fixture = ROOT / "Review" / "objects" / "basketball1.png"
    analyzer = ObjectAnalyzer(ROOT / "vendor" / "nanodet")
    if not fixture.exists() or not analyzer.available():
        pytest.skip("Official OpenCV fixture and model required")
    detected = analyzer.detect(cv2.imread(str(fixture)))
    assert any(record["label"] == "person" for record in detected["detections"])
    assert "not obstacle clearance" in detected["scope"]


def test_real_model_blank_image_and_missing_model(tmp_path):
    with pytest.raises(ValueError, match="model"):
        ObjectAnalyzer(tmp_path).detect(np.zeros((480, 640, 3), np.uint8))
    analyzer = ObjectAnalyzer(ROOT / "vendor" / "nanodet")
    if not analyzer.available():
        pytest.skip("Run SETUP-OBJECTS.ps1 for real inference test")
    assert analyzer.detect(np.full((480, 640, 3), 127, np.uint8))["detections"] == []


def test_library_http_and_bad_id(tmp_path):
    state = State(tmp_path, "http://127.0.0.1:8765")
    for identifier in ("room_a", "room_b"):
        folder = state.catalog.folder(identifier)
        folder.mkdir()
        (folder / "map.json").write_text(json.dumps({"id": identifier, "name": identifier, "state": "ready"}), encoding="utf-8")
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler_for(state))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    base = f"http://127.0.0.1:{server.server_port}"
    headers = {"Authorization": "Bearer " + state.token}
    try:
        with urllib.request.urlopen(urllib.request.Request(base + "/maps", headers=headers)) as response:
            assert len(json.load(response)["maps"]) == 2
        request = urllib.request.Request(base + "/maps/room_a/rename", data=json.dumps({"name": "Phòng 1"}).encode(), headers=headers)
        assert json.load(urllib.request.urlopen(request))["name"] == "Phòng 1"
        assert state.catalog.read("room_b")["name"] == "room_b"
        with pytest.raises(urllib.error.HTTPError) as error:
            urllib.request.urlopen(urllib.request.Request(base + "/maps/../connection.json", headers=headers))
        assert error.value.code == 400
    finally:
        server.shutdown()
        server.server_close()
        state.executor.shutdown()
