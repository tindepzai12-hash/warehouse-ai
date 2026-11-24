from pathlib import Path

import cv2
import qrcode


def main():
    root = Path(__file__).resolve().parent
    for label in ("A", "B"):
        payload = f"JWM:MAIN:{label}"
        code = qrcode.QRCode(error_correction=qrcode.constants.ERROR_CORRECT_Q, box_size=20, border=4)
        code.add_data(payload)
        code.make(fit=True)
        target = root / f"QR-{label}.png"
        code.make_image(fill_color="black", back_color="white").save(target)
        decoded, _, _ = cv2.QRCodeDetector().detectAndDecode(cv2.imread(str(target)))
        if decoded != payload:
            raise RuntimeError(f"QR {label} failed round-trip validation")
    print("PASS QR A and B decode to the application identifiers")


if __name__ == "__main__":
    main()
