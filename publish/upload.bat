@echo off
chcp 65001 >nul
cd /d "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0upload.ps1"
echo.
pause
