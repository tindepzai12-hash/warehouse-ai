import json
import sys
import time
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / "vendor" / "hloc"))
from visualmap_core import Localizer, reconstruct


def main():
    folder = ROOT / "tests-output" / ("synthetic-room-" + str(time.time_ns()))
    if (folder / "colmap.db").exists():
        raise RuntimeError("Synthetic output already exists; choose a new test output before rebuilding.")
    (folder / "images").mkdir(parents=True, exist_ok=True)
    random = np.random.default_rng(2026)
    textures = []
    for plane in range(3):
        image = np.full((900, 1200), 230, np.uint8)
        for index in range(1600):
            center = tuple(random.integers([0, 0], [1200, 900]).tolist())
            color = int(random.integers(15, 220))
            cv2.circle(image, center, int(random.integers(2, 14)), color, -1)
        for index in range(50):
            center = tuple(random.integers([10, 30], [1100, 850]).tolist())
            cv2.putText(image, f"{plane}-{index}", center, cv2.FONT_HERSHEY_SIMPLEX, .5, 30, 1)
        textures.append(image)
    planes = [np.array([[-4, -3, 5], [4, -3, 5], [4, 2, 5], [-4, 2, 5]], float),
              np.array([[-2.8, -1.8, 3.5], [-1.4, -1.8, 3.5], [-1.4, .8, 3.5], [-2.8, .8, 3.5]], float),
              np.array([[1.0, -2.1, 4.0], [2.8, -2.1, 4.0], [2.8, 1.0, 4.0], [1.0, 1.0, 4.0]], float)]
    width, height, focal = 900, 675, 750
    camera = np.array([[focal, 0, width / 2], [0, focal, height / 2], [0, 0, 1]])
    corners = np.float32([[0, 0], [1199, 0], [1199, 899], [0, 899]])

    def render(position):
        output = np.full((height, width), 250, np.uint8)
        for texture, plane in zip(textures, planes):
            points = plane - np.array([position[0], -position[1], position[2]])
            projected = (camera @ points.T).T
            projected = (projected[:, :2] / projected[:, 2:]).astype(np.float32)
            homography = cv2.getPerspectiveTransform(corners, projected)
            warped = cv2.warpPerspective(texture, homography, (width, height))
            mask = cv2.warpPerspective(np.full(texture.shape, 255, np.uint8), homography, (width, height))
            output[mask > 250] = warped[mask > 250]
        return output

    frames = []
    for index in range(28):
        angle = 2 * np.pi * index / 28
        position = [.8 * np.cos(angle), 1.3, .6 * np.sin(angle)]
        name = f"{index:05}.jpg"
        cv2.imwrite(str(folder / "images" / name), render(position), [cv2.IMWRITE_JPEG_QUALITY, 95])
        frames.append({"name": name, "position": position, "rotation": [0, 0, 0, 1], "width": width, "height": height,
                       "fx": focal, "fy": focal, "cx": width / 2, "cy": height / 2, "time": index + 1,
                       "onRoute": index >= 14, "start": index == 14, "end": index == 27})
    (folder / "manifest.json").write_text(json.dumps({"schema": 1, "coordinateSystem": "Unity metres, Y up", "floorY": 0, "frames": frames}))
    data = reconstruct(folder, lambda stage, step: print(f"STAGE {step}: {stage}", flush=True))
    localizer = Localizer(folder)
    query_position = np.array([.15, 1.3, .12])
    query_image = render(query_position)
    cv2.imwrite(str(folder / "held-out-query.jpg"), query_image)
    result = localizer.locate(query_image, frames[0], "synthetic-held-out")
    assert result["ok"], result
    error = float(np.linalg.norm(np.array(result["position"]) - query_position))
    orientation_error = float(np.linalg.norm(np.array(result["rotation"]).reshape(3, 3) - np.eye(3)))
    assert error < .12, (error, result)
    assert orientation_error < .08, orientation_error
    blank = localizer.locate(np.full(query_image.shape, 128, np.uint8), frames[0], "blank")
    assert not blank["ok"]
    report = {"evidence": "SYNTHETIC, not a physical room or Android acceptance", "positionErrorMetres": error,
              "orientationMatrixError": orientation_error, "localization": result, "map": data, "blankRejected": True}
    (ROOT / "synthetic-result.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"PASS": True, "registered": data["registered"], "inliers": result["inliers"], "errorMetres": error}), flush=True)


if __name__ == "__main__":
    main()
