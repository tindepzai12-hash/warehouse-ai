import json
import math
import sqlite3
import threading
import time
from pathlib import Path

import cv2
import numpy as np
import pycolmap
from PIL import Image
from map_catalog import clean_name, valid_id
from map_geometry import validate_depth, build_geometry
from object_analysis import ObjectAnalyzer

FLIP = np.diag([1.0, -1.0, 1.0])
MAX_IMAGES = 600


def finite_array(value, shape):
    result = np.asarray(value, dtype=float)
    if result.shape != shape or not np.isfinite(result).all():
        raise ValueError("Dữ liệu tọa độ / camera không hợp lệ.")
    return result


def validate_manifest(data):
    if data.get("schema") not in (1, 2) or data.get("coordinateSystem") != "Unity metres, Y up":
        raise ValueError("Sai phiên bản dữ liệu chụp.")
    if data.get("schema") == 2:
        if data.get("interrupted"):
            raise ValueError("Phiên AR đã bị ngắt; giữ dữ liệu để phục hồi nhưng không công bố map từ pose chưa liên tục.")
        if data.get("captureState", "complete") != "complete":
            raise ValueError("Lượt quét chưa kết thúc; giữ ảnh đã có và lưu lượt trước khi gửi dựng.")
        if type(data.get("floorKnown", True)) is not bool:
            raise ValueError("Trạng thái mặt sàn không hợp lệ.")
        clean_name(data.get("name", ""))
        if data.get("purpose") not in ("environment", "route") or not valid_id(data.get("captureId")):
            raise ValueError("Thiếu mục đích hoặc ID lượt chụp.")
        if data.get("parentId") and not valid_id(data["parentId"]):
            raise ValueError("Map gốc không hợp lệ.")
    frames = data.get("frames", [])
    if not 20 <= len(frames) <= MAX_IMAGES:
        raise ValueError(f"Cần 20–{MAX_IMAGES} ảnh hợp lệ; nên chụp 80–150 ảnh.")
    floor = float(data["floorY"])
    if not math.isfinite(floor):
        raise ValueError("Chưa căn mặt sàn.")
    previous_time = -1.0
    names = set()
    for frame in frames:
        name = frame["name"]
        if not isinstance(name, str) or Path(name).name != name or not name.endswith(".jpg") or name in names:
            raise ValueError("Tên ảnh không hợp lệ hoặc trùng.")
        names.add(name)
        width, height = int(frame["width"]), int(frame["height"])
        intrinsics = finite_array([frame[key] for key in ["fx", "fy", "cx", "cy"]], (4,))
        position = finite_array(frame["position"], (3,))
        rotation = finite_array(frame["rotation"], (4,))
        stamp = float(frame["time"])
        if not (64 <= width <= 2048 and 64 <= height <= 2048 and intrinsics[0] > 0 and intrinsics[1] > 0
                and 0 <= intrinsics[2] <= width and 0 <= intrinsics[3] <= height
                and math.isfinite(stamp) and stamp > previous_time and abs(np.linalg.norm(rotation) - 1) < 0.02
                and (not data.get("floorKnown", True) or 0.4 < position[1] - floor < 3)):
            raise ValueError("Ảnh thiếu thông số camera, thời gian hoặc mặt sàn hợp lệ.")
        previous_time = stamp
        if data.get("schema") == 2:
            validate_depth(frame)
    first = frames[0]
    for frame in frames:
        if (frame["width"], frame["height"]) != (first["width"], first["height"]):
            raise ValueError("Kích thước camera đã đổi trong lượt chụp.")
        if any(abs(frame[key] / first[key] - 1) > 0.03 for key in ("fx", "fy")) or abs(frame["cx"] - first["cx"]) > first["width"] * 0.01 or abs(frame["cy"] - first["cy"]) > first["height"] * 0.01:
            raise ValueError("Thông số camera thay đổi quá nhiều trong lượt chụp; chưa dựng được bản đồ đáng tin.")
    route = [frame for frame in frames if frame.get("onRoute")]
    needs_route = data.get("schema") == 1 or data.get("purpose") == "route" or bool(route)
    if needs_route and not data.get("floorKnown", True):
        raise ValueError("Tuyến cần mặt sàn đã căn. Lượt quét ảnh phòng không bắt buộc căn sàn.")
    if needs_route and (len(route) < 4 or not route[0].get("start") or not route[-1].get("end")):
        raise ValueError("Cần ghi tuyến từ QR A tới QR B trong cùng lượt chụp.")
    return frames, route


def fit_similarity(source, target):
    source, target = np.asarray(source, dtype=float), np.asarray(target, dtype=float)
    if source.shape != target.shape or source.ndim != 2 or source.shape[1] != 3 or len(source) < 6:
        raise ValueError("Thiếu vị trí để xác định tỷ lệ bản đồ.")
    if not np.isfinite(source).all() or not np.isfinite(target).all():
        raise ValueError("Tọa độ không hữu hạn.")
    source_mean, target_mean = source.mean(0), target.mean(0)
    source_centered, target_centered = source - source_mean, target - target_mean
    spread = np.linalg.svd(target_centered, compute_uv=False)
    if spread[0] < 0.8 or spread[1] < 0.15:
        raise ValueError("Ảnh chưa có đủ chuyển động ngang: đi thêm một vòng / dịch trái phải, không chỉ xoay máy tại chỗ.")
    left, singular, right = np.linalg.svd(target_centered.T @ source_centered / len(source))
    signs = np.ones(3)
    signs[-1] = np.linalg.det(left @ right)
    rotation = left @ np.diag(signs) @ right
    variance = np.mean(np.sum(source_centered ** 2, axis=1))
    if variance < 1e-10:
        raise ValueError("Bản đồ không có chiều sâu.")
    scale = np.sum(singular * signs) / variance
    translation = target_mean - scale * rotation @ source_mean
    residuals = np.linalg.norm((scale * (rotation @ source.T)).T + translation - target, axis=1)
    if not 0.001 < scale < 1000 or np.sqrt(np.mean(residuals ** 2)) > 0.30 or np.max(residuals) > 0.75:
        raise ValueError("Bản đồ và vị trí AR lệch quá lớn; chưa công bố. Chụp lại chậm hơn, đủ ánh sáng.")
    return scale, rotation, translation, float(np.sqrt(np.mean(residuals ** 2)))


def metric_pose(camera_from_world, alignment):
    scale, rotation, translation = alignment
    world_from_camera = camera_from_world.inverse()
    center = FLIP @ (scale * rotation @ world_from_camera.translation + translation)
    orientation = FLIP @ rotation @ world_from_camera.rotation.matrix() @ FLIP
    return center, orientation


def quaternion_matrix(quaternion):
    x, y, z, w = finite_array(quaternion, (4,))
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def camera_basis_correction(frames, registered, alignment):
    corrections = []
    for frame in frames:
        _, raw_orientation = metric_pose(registered[frame["name"]].cam_from_world(), alignment)
        corrections.append(raw_orientation.T @ quaternion_matrix(frame["rotation"]))
    left, _, right = np.linalg.svd(np.sum(corrections, axis=0))
    signs = np.ones(3)
    signs[-1] = np.linalg.det(left @ right)
    correction = left @ np.diag(signs) @ right
    angles = [math.degrees(math.acos(float(np.clip((np.trace(correction.T @ value) - 1) / 2, -1, 1)))) for value in corrections]
    if np.percentile(angles, 90) > 10:
        raise ValueError("Hướng camera và bản đồ chưa nhất quán; chưa công bố. Giữ điện thoại dọc và không đổi camera / zoom.")
    return correction


def read_features(database_path, reconstruction):
    features = {}
    with sqlite3.connect(database_path) as database:
        for image_id, image in reconstruction.images.items():
            row = database.execute("SELECT rows,cols,data FROM descriptors WHERE image_id=?", (image_id,)).fetchone()
            if row is None:
                continue
            descriptors = np.frombuffer(row[2], dtype=np.uint8).reshape(row[0], row[1]).astype(np.float32)
            descriptors /= np.maximum(np.linalg.norm(descriptors, axis=1, keepdims=True), 1e-8)
            ids = np.array([point.point3D_id if point.has_point3D() else -1 for point in image.points2D], dtype=np.int64)
            usable = ids >= 0
            features[image.name] = (descriptors[usable], ids[usable])
    return features


def reconstruct(folder, status):
    folder = Path(folder)
    manifest = json.loads((folder / "manifest.json").read_text(encoding="utf-8"))
    frames, route = validate_manifest(manifest)
    first = frames[0]
    if any((frame["width"], frame["height"]) != (first["width"], first["height"]) for frame in frames):
        raise ValueError("Kích thước camera đã đổi trong lượt chụp.")
    for frame in frames:
        with Image.open(folder / "images" / frame["name"]) as header:
            if header.size != (frame["width"], frame["height"]) or header.format != "JPEG":
                raise ValueError("Kích thước / định dạng ảnh không khớp manifest.")
        image = cv2.imread(str(folder / "images" / frame["name"]), cv2.IMREAD_GRAYSCALE)
        if image is None or image.shape != (frame["height"], frame["width"]):
            raise ValueError("Thiếu ảnh hoặc kích thước ảnh không khớp manifest.")
    database = folder / "colmap.db"
    options = pycolmap.FeatureExtractionOptions()
    options.use_gpu = False
    options.num_threads = 4
    options.sift.max_num_features = 4096
    camera = pycolmap.ImageReaderOptions()
    camera.camera_model = "PINHOLE"
    camera.camera_params = ",".join(str(first[key]) for key in ["fx", "fy", "cx", "cy"])
    status("Trích đặc trưng ảnh", 1)
    pycolmap.extract_features(database, folder / "images", camera_mode=pycolmap.CameraMode.SINGLE,
                             reader_options=camera, extraction_options=options, device=pycolmap.Device.cpu)
    pairs = set()
    for index, frame in enumerate(frames):
        nearby = sorted(range(len(frames)), key=lambda other: np.linalg.norm(
            np.array(frame["position"]) - np.array(frames[other]["position"])))[:18]
        for other in set(nearby) | set(range(index + 1, min(index + 11, len(frames)))):
            if other != index:
                pairs.add(tuple(sorted((index, other))))
    pairs_path = folder / "pairs.txt"
    pairs_path.write_text("\n".join(f"{frames[left]['name']} {frames[right]['name']}" for left, right in sorted(pairs)), encoding="utf-8")
    matching = pycolmap.FeatureMatchingOptions()
    matching.use_gpu = False
    matching.num_threads = 4
    status("Ghép ảnh và kiểm tra hình học", 2)
    pairing = pycolmap.ImportedPairingOptions()
    pairing.match_list_path = str(pairs_path)
    pycolmap.match_image_pairs(database, matching_options=matching, pairing_options=pairing, device=pycolmap.Device.cpu)
    sparse = folder / "sparse"
    sparse.mkdir(exist_ok=True)
    pipeline = pycolmap.IncrementalPipelineOptions()
    pipeline.ba_refine_focal_length = False
    pipeline.ba_refine_principal_point = False
    pipeline.ba_refine_extra_params = False
    pipeline.min_model_size = 10
    pipeline.num_threads = 4
    status("Dựng bản đồ COLMAP", 3)
    models = pycolmap.incremental_mapping(database, folder / "images", sparse, options=pipeline)
    if not models:
        raise ValueError("COLMAP không dựng được bản đồ. Cần ảnh chồng lấn và có dịch chuyển máy.")
    model_id, model = max(models.items(), key=lambda pair: pair[1].num_reg_images())
    registered = {image.name: image for image in model.images.values()}
    if len(registered) < max(15, math.ceil(len(frames) * 0.70)):
        raise ValueError(f"Chỉ ghép được {len(registered)}/{len(frames)} ảnh; chưa đủ để công bố.")
    if route and (route[0]["name"] not in registered or route[-1]["name"] not in registered):
        raise ValueError("Không định vị được ảnh tại A hoặc B; chưa thể tạo đích đúng.")
    registered_route = [frame for frame in route if frame["name"] in registered]
    if len(registered_route) < math.ceil(len(route) * 0.80):
        raise ValueError("Quá nhiều ảnh trên tuyến A–B không ghép được; chụp tuyến lại.")
    status("Căn tỷ lệ mét và kiểm tra tuyến", 4)
    accepted = [frame for frame in frames if frame["name"] in registered]
    source = [registered[frame["name"]].projection_center() for frame in accepted]
    target = [FLIP @ np.array(frame["position"]) for frame in accepted]
    scale, rotation, translation, rmse = fit_similarity(source, target)
    correction = camera_basis_correction(accepted, registered, (scale, rotation, translation))
    points = []
    for frame in registered_route:
        position, _ = metric_pose(registered[frame["name"]].cam_from_world(), (scale, rotation, translation))
        point = {"x": float(position[0]), "y": manifest["floorY"], "z": float(position[2])}
        if not points or frame is registered_route[-1] or np.linalg.norm(np.array([point["x"] - points[-1]["x"], point["z"] - points[-1]["z"]])) > 0.12:
            points.append(point)
    if route and (len(points) < 3 or sum(math.hypot(right["x"] - left["x"], right["z"] - left["z"]) for left, right in zip(points, points[1:])) < 1):
        raise ValueError("Tuyến hoàn chỉnh phải dài ít nhất 1 m.")
    result = {"schema": manifest["schema"], "id": folder.name, "state": "ready", "floorY": manifest["floorY"],
              "points": points, "registered": len(registered), "total": len(frames), "rmse": rmse,
              "scale": scale, "rotation": rotation.tolist(), "translation": translation.tolist(),
              "cameraCorrection": correction.tolist(), "device": manifest.get("device", ""),
              "model": f"sparse/{model_id}", "builtUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
              "name": manifest.get("name", "Phòng " + folder.name[:8]), "parentId": manifest.get("parentId", ""),
              "revision": 1, "geometryState": "missing", "navigationAllowed": False,
              "captureId": manifest.get("captureId", folder.name), "objectCount": 0}
    status("Phân tích bề mặt từ depth; giữ vùng thiếu dữ liệu", 5)
    camera_poses = {frame["name"]: metric_pose(registered[frame["name"]].cam_from_world(), (scale, rotation, translation)) for frame in accepted}
    geometry = build_geometry(folder, accepted, camera_poses, manifest["floorY"], points) if manifest.get("floorKnown", True) else {"state": "missing", "navigationAllowed": False, "depthFrames": 0, "reason": "Đã ghép ảnh phòng; chưa căn mặt sàn nên chưa suy ra sơ đồ đi bộ."}
    result["geometryState"] = geometry["state"]
    (folder / "geometry.json").write_text(json.dumps(geometry, ensure_ascii=False, allow_nan=False), encoding="utf-8")
    status("Nhận diện đồ vật trên ảnh", 6)
    analyzer = ObjectAnalyzer(Path(__file__).parent / "vendor" / "nanodet")
    objects = {"state": "model-missing", "observations": [], "note": "Không phải số vật thể duy nhất trong phòng; nhãn cần kiểm tra lại."}
    if analyzer.available():
        indices = np.unique(np.linspace(0, len(accepted) - 1, min(30, len(accepted))).astype(int))
        try:
            for index in indices:
                frame = accepted[int(index)]
                detected = analyzer.detect(cv2.imread(str(folder / "images" / frame["name"])))
                objects["observations"].append({"image": frame["name"], **detected})
            objects["state"] = "complete"
        except (ValueError, cv2.error) as error:
            objects["state"] = "failed"
            objects["error"] = str(error)[:200]
    result["objectCount"] = sum(len(item["detections"]) for item in objects["observations"])
    result["objectState"] = objects["state"]
    (folder / "objects.json").write_text(json.dumps(objects, ensure_ascii=False, allow_nan=False), encoding="utf-8")
    (folder / "map.json").write_text(json.dumps(result, ensure_ascii=False, allow_nan=False), encoding="utf-8")
    status("Đã phân tích; xem thư viện và báo cáo trước khi sử dụng", 7)
    return result


class Localizer:
    def __init__(self, folder):
        from hloc.localize_sfm import QueryLocalizer
        self.folder = Path(folder)
        self.data = json.loads((self.folder / "map.json").read_text(encoding="utf-8"))
        self.model = pycolmap.Reconstruction(self.folder / self.data["model"])
        self.query = QueryLocalizer(self.model, {"estimation": {"ransac": {"max_error": 4.0}},
                                               "refinement": {"refine_focal_length": False, "refine_extra_params": False}})
        self.features = read_features(self.folder / "colmap.db", self.model)
        descriptors, point_ids = [], []
        for image_descriptors, image_ids in self.features.values():
            descriptors.append(image_descriptors)
            point_ids.extend(image_ids.tolist())
        self.ids = np.asarray(point_ids, dtype=np.int64)
        self.descriptors = np.concatenate(descriptors).astype(np.float32)
        self.matcher = cv2.FlannBasedMatcher({"algorithm": 1, "trees": 4}, {"checks": 64})
        self.matcher.add([self.descriptors])
        self.matcher.train()
        options = pycolmap.FeatureExtractionOptions()
        options.sift.max_num_features = 4096
        self.sift = pycolmap.Sift(options=options, device=pycolmap.Device.cpu)
        self.lock = threading.Lock()

    def locate(self, image, calibration, request_id):
        if self.data.get("device", "") != calibration.get("device", ""):
            raise ValueError("Bản thử này cần dùng cùng điện thoại đã thu ảnh để giữ đúng căn chỉnh camera.")
        height, width = image.shape
        intrinsics = finite_array([calibration[key] for key in ["fx", "fy", "cx", "cy"]], (4,))
        if width > 2048 or height > 2048 or min(width, height) < 64 or min(intrinsics[:2]) <= 0 or not 0 <= intrinsics[2] <= width or not 0 <= intrinsics[3] <= height:
            raise ValueError("Ảnh truy vấn / camera không hợp lệ.")
        with self.lock:
            keypoints, descriptors = self.sift.extract(image.astype(np.float32) / 255)
            if len(keypoints) < 30:
                return {"ok": False, "message": "Ít chi tiết. Hướng máy vào đồ vật / góc phòng đủ sáng.", "inliers": 0}
            descriptors = np.asarray(descriptors, dtype=np.float32)
            descriptors /= np.maximum(np.linalg.norm(descriptors, axis=1, keepdims=True), 1e-8)
            matches = self.matcher.knnMatch(descriptors, k=4)
            correspondences = {}
            for neighbors in matches:
                if len(neighbors) < 2:
                    continue
                best = neighbors[0]
                point_id = self.ids[best.trainIdx]
                alternate = next((neighbor for neighbor in neighbors[1:] if self.ids[neighbor.trainIdx] != point_id), None)
                if alternate is not None and best.distance < 0.80 * alternate.distance:
                    previous = correspondences.get(point_id)
                    if previous is None or best.distance < previous.distance:
                        correspondences[point_id] = best
            if len(correspondences) < 25:
                return {"ok": False, "message": "Chưa khớp bản đồ. Đứng yên, nhìn lại khu vực đã chụp.", "inliers": 0}
            ids = list(correspondences)
            indices = [correspondences[point_id].queryIdx for point_id in ids]
            camera = pycolmap.Camera(model="PINHOLE", width=width, height=height, params=intrinsics)
            result = self.query.localize(np.asarray(keypoints)[:, :2], indices, ids, camera)
            count = int(result["num_inliers"]) if result else 0
            if result is None or count < 25 or count / len(ids) < 0.25:
                return {"ok": False, "message": "Đối chiếu hình học chưa đáng tin. Chưa bật chỉ đường.", "inliers": count}
            pose = result["cam_from_world"]
            inliers = np.asarray(result["inlier_mask"], dtype=bool)
            point_positions = np.asarray([self.model.points3D[point_id].xyz for point_id in ids])
            projected = camera.img_from_cam(pose * point_positions)
            errors = np.linalg.norm(projected - np.asarray(keypoints)[indices, :2], axis=1)
            median = float(np.median(errors[inliers]))
            if not math.isfinite(median) or median > 2.5:
                return {"ok": False, "message": "Sai số ảnh còn lớn. Giữ máy ổn định và thử lại.", "inliers": count}
            position, rotation = metric_pose(pose, (self.data["scale"], np.array(self.data["rotation"]), np.array(self.data["translation"])))
            rotation = rotation @ np.array(self.data.get("cameraCorrection", np.eye(3)))
            if not np.isfinite(position).all() or not np.isfinite(rotation).all():
                raise ValueError("Kết quả định vị không hữu hạn.")
            return {"ok": True, "mapId": self.data["id"], "requestId": request_id, "inliers": count,
                    "medianError": median, "position": position.tolist(), "rotation": rotation.reshape(-1).tolist()}
