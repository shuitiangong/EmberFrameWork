@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Generate.ps1" -Format Bin
exit /b %ERRORLEVEL%
