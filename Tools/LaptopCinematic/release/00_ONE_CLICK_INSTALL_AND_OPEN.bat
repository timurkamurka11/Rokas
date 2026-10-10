@echo off
setlocal
set "BASE=%~dp0"
set "TARGET=D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BASE%INSTALL_SAFE.ps1" -Target "%TARGET%"
if errorlevel 1 (
 echo ROKAS FPS hand: INSTALLATION BLOCKED OR FAILED. Unity not opened.
 pause
 exit /b 1
)
if exist "%ProgramFiles%\Unity Hub\Unity Hub.exe" (
 start "" "%ProgramFiles%\Unity Hub\Unity Hub.exe" -- --projectPath "%TARGET%"
) else (
 explorer.exe "%TARGET%"
)
echo The isolated hand assets were installed. Inspect in your local Unity Editor.
pause
