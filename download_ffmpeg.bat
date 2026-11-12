@echo off
echo [*] Downloading FFmpeg Shared Binaries for ViewiPhone...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0download_ffmpeg.ps1"
pause
