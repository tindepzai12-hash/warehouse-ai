from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parent.parent
folder = root / 'Docs' / 'ScannerSamples'
code = 'MILK-180ML-CASE24-TEST'
regular = root / 'Assets' / 'Resources' / 'BeVietnamPro-Regular.ttf'
bold = root / 'Assets' / 'Resources' / 'BeVietnamPro-Bold.ttf'

def font(size, heavy=False):
    return ImageFont.truetype(str(bold if heavy else regular), size)

image = Image.new('RGB', (1200, 1500), 'white')
draw = ImageDraw.Draw(image)
draw.rectangle((0, 0, 1200, 18), fill='#1255d5')
draw.text((600, 80), 'THÙNG SỮA', anchor='mt', font=font(64, True), fill='#152640')
draw.text((600, 180), 'Mẫu thử: 24 hộp × 180 ml', anchor='mt', font=font(34), fill='#405671')
draw.rounded_rectangle((264, 265, 936, 331), radius=16, fill='#edf3fd')
draw.text((600, 276), 'QR NỘI BỘ · KHÔNG PHẢI GTIN', anchor='mt', font=font(25, True), fill='#1255d5')
qr = Image.open(folder / 'Milk-Case-QR.png').convert('RGB')
image.paste(qr, ((1200 - qr.width) // 2, 400))
draw.text((600, 1105), code, anchor='mt', font=font(32, True), fill='#152640')
draw.text((600, 1185), 'MÃ THỬ — CHƯA ĐĂNG KÝ TRONG KHO', anchor='mt', font=font(29, True), fill='#8d3c14')
draw.text((600, 1250), 'Quét để kiểm tra đọc mã; không tự cộng tồn kho.', anchor='mt', font=font(27), fill='#405671')
draw.text((600, 1320), 'Muốn tra được hàng thật: dùng SKU / barcode đã có trong OMS.', anchor='mt', font=font(24), fill='#405671')
image.save(folder / 'Milk-Case-Label.png')
print(folder / 'Milk-Case-Label.png')
