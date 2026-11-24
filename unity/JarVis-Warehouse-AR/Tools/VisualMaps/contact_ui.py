from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent
files = sorted((root / "UI").glob("*.png"))
for offset in range(0, len(files), 12):
    sheet = Image.new("RGB", (1000, 1060), "#eaf0fa")
    draw = ImageDraw.Draw(sheet)
    for slot, source in enumerate(files[offset:offset + 12]):
        picture = Image.open(source).convert("RGB")
        picture.thumbnail((235, 315))
        left, top = (slot % 4) * 250, (slot // 4) * 350
        draw.text((left + 6, top + 4), source.stem, fill="#132444")
        sheet.paste(picture, (left + 6, top + 25))
    sheet.save(root / f"UI-contact-{offset // 12 + 1}.png")
