import math
from pathlib import Path

import cv2
import numpy as np


def validate_depth(frame):
    depth = frame.get("depth")
    if not depth or not depth.get("file"):
        return []
    width, height = depth.get("width"), depth.get("height")
    if type(width) is not int or type(height) is not int or not 8 <= width <= 640 or not 8 <= height <= 640:
        raise ValueError("Kích thước depth không hợp lệ.")
    if depth.get("format") != "float32-metres" or depth.get("file") != frame["name"] + ".f32" or depth.get("confidence") != frame["name"] + ".u8":
        raise ValueError("Định dạng/tên depth không hợp lệ.")
    calibration = np.asarray([depth.get(key) for key in ("fx", "fy", "cx", "cy", "time")], float)
    if not np.isfinite(calibration).all() or min(calibration[:2]) <= 0 or not 0 <= calibration[2] <= width or not 0 <= calibration[3] <= height:
        raise ValueError("Calibration depth không hợp lệ.")
    if abs(float(frame.get("cameraTime", -100)) - calibration[4]) > .05:
        raise ValueError("Depth không đồng bộ ảnh camera.")
    return ["depth/" + depth["file"], "depth/" + depth["confidence"]]


def check_corridor(cells, origin, resolution, points, radius=.4):
    if not .05 <= resolution <= .5 or not .2 <= radius <= .8 or cells.ndim != 2:
        raise ValueError("Lưới/hành lang không hợp lệ.")
    route = np.asarray(points, float)
    if route.ndim != 2 or route.shape[1] != 2 or not 2 <= len(route) <= 600 or not np.isfinite(route).all():
        return {"valid": False, "reason": "missing-route", "reasonText": "Chưa có tuyến A–B được ghi.", "coverage": 0.0, "unknown": 0, "blocked": 0, "distanceMetres": 0.0}
    touched = set()
    outside = False
    effective = radius + resolution * math.sqrt(2) / 2 + resolution / 8
    for first, second in zip(route, route[1:]):
        distance = np.linalg.norm(second - first)
        if distance > 100:
            raise ValueError("Đoạn tuyến quá dài.")
        for position in np.linspace(first, second, max(2, math.ceil(distance / (resolution / 4)) + 1)):
            center = (position - origin) / resolution - .5
            span = math.ceil(effective / resolution)
            for row in range(math.floor(center[1]) - span, math.floor(center[1]) + span + 2):
                for column in range(math.floor(center[0]) - span, math.floor(center[0]) + span + 2):
                    if np.linalg.norm((np.array([column, row]) - center) * resolution) > effective:
                        continue
                    if not 0 <= row < cells.shape[0] or not 0 <= column < cells.shape[1]:
                        outside = True
                    else:
                        touched.add((row, column))
    blocked = sum(cells[index] == 2 for index in touched)
    unknown = sum(cells[index] == 0 for index in touched)
    count = len(touched)
    valid = bool(count) and not outside and not blocked and not unknown
    reason = "outside" if outside else "blocked" if blocked else "unknown" if unknown else "candidate"
    reason_text = {
        "outside": "Tuyến đi ra ngoài vùng có bằng chứng depth; cần quét rộng thêm khu vực tuyến.",
        "blocked": "Depth ghi nhận bề mặt cản trong hành lang; kiểm tra vật cản và ghi tuyến thay thế.",
        "unknown": "Còn đoạn thiếu bằng chứng depth; quét bổ sung chậm quanh phần tuyến chưa rõ.",
        "candidate": "Có bằng chứng bề mặt dọc tuyến; vẫn cần kiểm tra thực tế khoảng trống và độ cao.",
    }[reason]
    return {"valid": valid, "reason": "outside" if outside else "blocked" if blocked else "unknown" if unknown else "candidate",
            "reasonText": reason_text, "coverage": (count - unknown) / count if count else 0.0,
            "unknown": int(unknown), "blocked": int(blocked), "checkedCells": count,
            "distanceMetres": float(np.linalg.norm(np.diff(route, axis=0), axis=1).sum()),
            "radius": radius, "note": "Surface evidence only; headroom and physical review still required"}


def build_geometry(folder, frames, camera_poses, floor_y, route):
    folder = Path(folder)
    floor_evidence, obstacles = {}, set()
    resolution = .1
    used = 0
    for frame in frames:
        if not frame.get("depth") or not frame["depth"].get("file") or frame["name"] not in camera_poses:
            continue
        validate_depth(frame)
        metadata = frame["depth"]
        width, height = metadata["width"], metadata["height"]
        depth = np.fromfile(folder / "depth" / metadata["file"], dtype="<f4")
        confidence = np.fromfile(folder / "depth" / metadata["confidence"], dtype="u1")
        if len(depth) != width * height or len(confidence) != width * height or not np.isfinite(depth).all() or np.any(depth < 0):
            raise ValueError("Dữ liệu depth/confidence sai kích thước hoặc không hữu hạn.")
        depth, confidence = depth.reshape(height, width)[::2, ::2], confidence.reshape(height, width)[::2, ::2]
        rows, columns = np.mgrid[0:height:2, 0:width:2]
        usable = (confidence >= 200) & (depth >= .2) & (depth <= 5)
        local = np.column_stack(((columns[usable] - metadata["cx"]) * depth[usable] / metadata["fx"],
                                 -(rows[usable] - metadata["cy"]) * depth[usable] / metadata["fy"], depth[usable]))
        center, rotation = camera_poses[frame["name"]]
        world = local @ rotation.T + center
        if not len(world):
            continue
        used += 1
        for position in world:
            key = (math.floor(position[0] / resolution), math.floor(position[2] / resolution))
            relative_height = position[1] - floor_y
            if abs(relative_height) <= .07:
                floor_evidence.setdefault(key, set()).add(frame["name"])
            elif .1 <= relative_height <= 2.0:
                obstacles.add(key)
    keys = set(floor_evidence) | obstacles
    if not keys:
        return {"state": "missing", "depthFrames": used, "navigationAllowed": False, "reason": "Chưa có hình học đủ tin cậy."}
    minimum = np.min(list(keys), axis=0) - 5
    maximum = np.max(list(keys), axis=0) + 5
    dimensions = maximum - minimum + 1
    if np.max(dimensions) > 512:
        raise ValueError("Map hình học vượt phạm vi 51 m; chia thành các map riêng.")
    cells = np.zeros((dimensions[1], dimensions[0]), np.uint8)
    for key, evidence in floor_evidence.items():
        if len(evidence) >= 2:
            cells[key[1] - minimum[1], key[0] - minimum[0]] = 1
    for column, row in obstacles:
        cells[row - minimum[1], column - minimum[0]] = 2
    origin = minimum * resolution
    candidate = check_corridor(cells, origin, resolution, [[point["x"], point["z"]] for point in route])
    palette = np.array([[68, 57, 47], [244, 234, 213], [70, 80, 230]], np.uint8)
    preview = palette[cells]
    if route:
        polygon = np.round((np.array([[point["x"], point["z"]] for point in route]) - origin) / resolution).astype(np.int32)
        cv2.polylines(preview, [polygon], False, (255, 150, 10), 1)
    preview = cv2.resize(cv2.flip(preview, 0), None, fx=4, fy=4, interpolation=cv2.INTER_NEAREST)
    cv2.imwrite(str(folder / "floor-preview.png"), preview)
    return {"state": "surface-preview", "depthFrames": used, "resolution": resolution, "origin": origin.tolist(),
            "width": int(dimensions[0]), "height": int(dimensions[1]), "cells": cells.flatten().tolist(),
            "route": candidate, "navigationAllowed": False,
            "reason": "Sơ đồ bề mặt thử nghiệm; chưa chứng minh khoảng trống toàn thân hoặc vật cản động."}
