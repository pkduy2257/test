@echo off
rem Nhấn đúp tệp này để mở giao diện mẫu LegacyVault.
rem Tệp sẽ chạy máy chủ giao diện (server.mjs) rồi mở trình duyệt ở http://localhost:5173.
rem Backend phải được chạy riêng ở http://localhost:5000.
chcp 65001 >nul
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo Chưa cài Node.js. Tải tại https://nodejs.org rồi chạy lại tệp này.
  pause
  exit /b 1
)

curl.exe -s -o nul http://localhost:5000/health/live
if errorlevel 1 (
  echo Lưu ý: chưa thấy backend ở http://localhost:5000. Giao diện vẫn mở nhưng chưa gọi được API.
  echo Chạy backend: dotnet run --project src/LegacyVault.Api --launch-profile http
  echo.
)

rem Máy chủ chạy trong cửa sổ riêng; đóng cửa sổ đó là tắt giao diện mẫu.
start "LegacyVault - Giao dien mau" node server.mjs
timeout /t 2 /nobreak >nul
start "" http://localhost:5173

echo Đã mở http://localhost:5173. Có thể đóng cửa sổ này.
timeout /t 5 >nul
