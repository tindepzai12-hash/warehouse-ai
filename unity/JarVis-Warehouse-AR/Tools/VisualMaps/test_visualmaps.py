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

from server import State, extract_capture, handler_for
from visualmap_core import FLIP, camera_basis_correction, fit_similarity, metric_pose, quaternion_matrix, validate_manifest


def manifest():
    frames = []
    for index in range(20):
        frames.append({"name": f"{index:05}.jpg", "width": 640, "height": 480, "fx": 500, "fy": 500,
                       "cx": 320, "cy": 240, "time": index + 1, "position": [index * .1, 1.3, .3 * (index % 3)],
                       "rotation": [0, 0, 0, 1], "onRoute": index >= 10, "start": index == 10, "end": index == 19})
    return {"schema": 1, "coordinateSystem": "Unity metres, Y up", "floorY": 0, "frames": frames}


def test_manifest_accepts_capture():
    frames, route = validate_manifest(manifest())
    assert len(frames) == 20 and len(route) == 10


@pytest.mark.parametrize("mutation", [
    lambda data: data.update(schema=99),
    lambda data: data.update(floorY=float("nan")),
    lambda data: data["frames"][0].update(name="../escape.jpg"),
    lambda data: data["frames"][0].update(name="..\\escape.jpg"),
    lambda data: data["frames"][0].update(fx=0),
    lambda data: data["frames"][5].update(fx=650),
    lambda data: data["frames"][5].update(width=800),
    lambda data: data["frames"][0].update(position=[float("inf"), 1.3, 0]),
    lambda data: data["frames"][0].update(rotation=[0, 0, 0, 0]),
    lambda data: data["frames"][2].update(time=1),
    lambda data: data["frames"][-1].update(end=False),
    lambda data: data.update(frames=data["frames"][:19]),
])
def test_invalid_capture_rejected(mutation):
    data = manifest()
    mutation(data)
    with pytest.raises((ValueError, KeyError)):
        validate_manifest(data)


def test_similarity_restores_metric_coordinates():
    random = np.random.default_rng(42)
    source = random.normal(size=(30, 3))
    angle = .71
    rotation = np.array([[np.cos(angle), 0, np.sin(angle)], [0, 1, 0], [-np.sin(angle), 0, np.cos(angle)]])
    target = (2.4 * (rotation @ source.T)).T + [2, -1, 3]
    scale, actual_rotation, translation, rmse = fit_similarity(source, target)
    assert abs(scale - 2.4) < 1e-9 and rmse < 1e-9
    assert np.allclose(actual_rotation, rotation)
    assert np.allclose(translation, [2, -1, 3])


def test_collinear_capture_rejected():
    points = np.array([[index, 0, 0] for index in range(10)], float)
    with pytest.raises(ValueError, match="chuyển động ngang"):
        fit_similarity(points, points)


def test_unity_opencv_pose_conversion():
    import pycolmap
    pose = pycolmap.Rigid3d(pycolmap.Rotation3d(np.eye(3)), np.array([-2, 1.3, -4]))
    position, rotation = metric_pose(pose, (1, np.eye(3), np.zeros(3)))
    assert np.allclose(position, [2, 1.3, 4])
    assert np.allclose(rotation, np.eye(3))
    assert np.linalg.det(rotation) == pytest.approx(1)


def test_portrait_camera_basis_is_calibrated():
    import pycolmap
    from types import SimpleNamespace
    quaternion = [0, 0, np.sin(np.pi / 4), np.cos(np.pi / 4)]
    image = SimpleNamespace(cam_from_world=lambda: pycolmap.Rigid3d())
    correction = camera_basis_correction([{"name": "portrait", "rotation": quaternion}], {"portrait": image}, (1, np.eye(3), np.zeros(3)))
    assert np.allclose(correction, quaternion_matrix(quaternion))
    assert np.linalg.det(correction) == pytest.approx(1)


def test_archive_rejects_extra_path(tmp_path):
    output = io.BytesIO()
    data = manifest()
    with zipfile.ZipFile(output, "w") as archive:
        archive.writestr("manifest.json", json.dumps(data))
        for frame in data["frames"]:
            archive.writestr("images/" + frame["name"], b"image")
        archive.writestr("../escape.txt", b"unexpected")
    output.seek(0)
    with pytest.raises(ValueError, match="thừa"):
        extract_capture(output, tmp_path / "capture")
    assert not (tmp_path / "escape.txt").exists()


def test_http_auth_and_missing_map(tmp_path):
    state = State(tmp_path, "http://127.0.0.1:8765")
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler_for(state))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    url = f"http://127.0.0.1:{server.server_port}"
    try:
        assert urllib.request.urlopen(url).status == 200
        with pytest.raises(urllib.error.HTTPError) as error:
            urllib.request.urlopen(url + "/status")
        assert error.value.code == 401
        headers = {"Authorization": "Bearer " + state.token}
        status = json.loads(urllib.request.urlopen(urllib.request.Request(url + "/status", headers=headers)).read())
        assert status["state"] == "idle" and not status["hasMap"]
        with pytest.raises(urllib.error.HTTPError) as error:
            urllib.request.urlopen(urllib.request.Request(url + "/map", headers=headers))
        assert error.value.code == 404
        with pytest.raises(urllib.error.HTTPError) as error:
            urllib.request.urlopen(urllib.request.Request(url + "/localize", data=b"{}", headers=headers))
        assert error.value.code == 413
        state.job["state"] = "building"
        with pytest.raises(urllib.error.HTTPError) as error:
            urllib.request.urlopen(urllib.request.Request(url + "/stop", data=b"", headers=headers))
        assert error.value.code == 409
        state.job["state"] = "idle"
        stopped = json.loads(urllib.request.urlopen(urllib.request.Request(url + "/stop", data=b"", headers=headers)).read())
        assert stopped["state"] == "stopped"
        thread.join(timeout=3)
        assert not thread.is_alive()
    finally:
        server.shutdown()
        server.server_close()
        state.executor.shutdown()
