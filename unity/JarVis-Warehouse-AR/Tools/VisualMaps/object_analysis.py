import hashlib
import importlib.util
import threading
from pathlib import Path

import cv2
import numpy as np

MODEL_HASH = "4b82da9944b88577175ee23a459dce2e26e6e4be573def65b1055dc2d9720186"
WRAPPER_HASH = "5ba84922eaefdc48d0066cffdfb721d0d434c5f3b92213563161e00da26cea1b"
LABELS = ("person,bicycle,car,motorcycle,airplane,bus,train,truck,boat,traffic light,fire hydrant,stop sign,"
          "parking meter,bench,bird,cat,dog,horse,sheep,cow,elephant,bear,zebra,giraffe,backpack,umbrella,"
          "handbag,tie,suitcase,frisbee,skis,snowboard,sports ball,kite,baseball bat,baseball glove,skateboard,"
          "surfboard,tennis racket,bottle,wine glass,cup,fork,knife,spoon,bowl,banana,apple,sandwich,orange,"
          "broccoli,carrot,hot dog,pizza,donut,cake,chair,couch,potted plant,bed,dining table,toilet,tv,laptop,"
          "mouse,remote,keyboard,cell phone,microwave,oven,toaster,sink,refrigerator,book,clock,vase,scissors,"
          "teddy bear,hair drier,toothbrush").split(",")


class ObjectAnalyzer:
    def __init__(self, root):
        self.root = Path(root)
        self.model = None
        self.guard = threading.Lock()

    def available(self):
        return (self.root / "model.onnx").is_file() and (self.root / "nanodet.py").is_file()

    def detect(self, image):
        if image is None or image.ndim not in (2, 3) or min(image.shape[:2]) < 64 or max(image.shape[:2]) > 2048:
            raise ValueError("Ảnh nhận diện cần 64–2048 pixel mỗi cạnh.")
        with self.guard:
            if self.model is None:
                for filename, expected in (("model.onnx", MODEL_HASH), ("nanodet.py", WRAPPER_HASH)):
                    path = self.root / filename
                    if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != expected:
                        raise ValueError("Thiếu/sai model nhận diện. Chạy SETUP-OBJECTS.ps1 trên PC.")
                specification = importlib.util.spec_from_file_location("jarvis_nanodet", self.root / "nanodet.py")
                module = importlib.util.module_from_spec(specification)
                specification.loader.exec_module(module)
                self.model = module.NanoDet(str(self.root / "model.onnx"), prob_threshold=.50)
            if image.ndim == 2:
                image = cv2.cvtColor(image, cv2.COLOR_GRAY2BGR)
            height, width = image.shape[:2]
            scale = min(416 / width, 416 / height)
            scaled_width, scaled_height = round(width * scale), round(height * scale)
            left, top = (416 - scaled_width) // 2, (416 - scaled_height) // 2
            canvas = np.zeros((416, 416, 3), np.uint8)
            canvas[top:top + scaled_height, left:left + scaled_width] = cv2.resize(cv2.cvtColor(image, cv2.COLOR_BGR2RGB), (scaled_width, scaled_height))
            self.model.net.setInput(self.model.pre_process(canvas))
            outputs = self.model.net.forward(self.model.net.getUnconnectedOutLayersNames())
            by_shape = {(value.shape[-2], value.shape[-1]): value for value in outputs}
            ordered = []
            for stride in (8, 16, 32):
                count = (416 // stride) ** 2
                for channels in (80, 32):
                    if (count, channels) not in by_shape:
                        raise ValueError("Model có đầu ra không tương thích; không trả nhãn giả.")
                    ordered.append(by_shape[(count, channels)])
            output = self.model.post_process(ordered)
            records = []
            for detection in output[:100]:
                first_x, first_y, last_x, last_y, score, class_id = detection
                box = [np.clip((first_x - left) / scale / width, 0, 1),
                       np.clip((first_y - top) / scale / height, 0, 1),
                       np.clip((last_x - left) / scale / width, 0, 1),
                       np.clip((last_y - top) / scale / height, 0, 1)]
                if box[2] <= box[0] or box[3] <= box[1] or not np.isfinite(detection).all():
                    continue
                records.append({"label": LABELS[int(class_id)], "confidence": float(score), "box": [float(value) for value in box]})
            return {"model": "NanoDet OpenCV Zoo", "width": width, "height": height,
                    "detections": records, "scope": "2D categories, not SKU, not obstacle clearance"}
