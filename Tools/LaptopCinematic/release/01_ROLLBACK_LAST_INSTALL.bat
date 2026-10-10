@echo off
setlocal
set "BASE=%~dp0"
set "TARGET=D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY"
echo Restore only the 49 FPS hand PNGs and one manifest from the last safe backup.
choice /C YN /N /M "Proceed with guarded rollback? [Y/N] "
if errorlevel 2 exit /b 0
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BASE%INSTALL_SAFE.ps1" -Target "%TARGET%" -Rollback
if errorlevel 1 (
 echo Rollback refused for safety or failed. No manual deletion recommended.
 pause
 exit /b 1
)
echo Guarded rollback finished.
pause
