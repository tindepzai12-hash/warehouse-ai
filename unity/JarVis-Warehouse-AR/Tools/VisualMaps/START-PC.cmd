@echo off
chcp 65001 >nul
set PYTHONUTF8=1
cd /d "%~dp0"
if not exist ".venv\Scripts\python.exe" (
  echo Chưa có môi trường Python. Đọc HUONG-DAN.md, mục cài lại PC.
  pause
  exit /b 1
)
echo JarVis Maps - PC và điện thoại phải cùng Wi-Fi tin cậy.
echo Mở http://127.0.0.1:8765 trong trình duyệt sau khi server sẵn sàng.
echo Giữ cửa sổ này mở. Dừng server bằng Ctrl+C.
".venv\Scripts\python.exe" run_pc.py --bind 0.0.0.0
pause
