@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Generate.ps1" -Format Json
exit /b %ERRORLEVEL%
