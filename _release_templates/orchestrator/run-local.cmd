@echo off
setlocal

cd /d "%~dp0"

"%~dp0ExportIfc.Orchestrator.exe"

echo.
echo Process finished. Press any key to close, or wait 600 seconds...
timeout /t 600 >nul
