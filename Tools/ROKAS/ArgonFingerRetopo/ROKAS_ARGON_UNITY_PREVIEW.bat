@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "CANDIDATE=codex/bedsleep-argon-import-candidate"
set "PROJECT=%~dp0..\..\.."
pushd "%PROJECT%" || goto :fail
where git >nul 2>nul || (
  echo Git for Windows is required.
  goto :failpop
)
git rev-parse --is-inside-work-tree >nul 2>nul || (
  echo This BAT must be run inside your existing ROKAS Unity Git project.
  goto :failpop
)
echo [ROKAS] Fetching the already pushed Argon preview branch...
git fetch origin "%CANDIDATE%" || goto :failpop
for /f "delims=" %%B in ('git branch --show-current') do set "CURRENT=%%B"
if /I not "%CURRENT%"=="%CANDIDATE%" (
  echo.
  echo Current local branch: %CURRENT%
  echo This script NEVER merges the unapproved PR into production V10.
  echo To inspect the latest isolated hand, temporarily switch to:
  echo %CANDIDATE%
  for /f "delims=" %%D in ('git status --porcelain') do set "DIRTY=1"
  if defined DIRTY (
    echo Local Git changes exist. Commit/stash them with your usual workflow first.
    echo Nothing has been switched or overwritten.
    goto :failpop
  )
  choice /C YN /N /M "Switch to isolated preview branch safely? [Y/N] "
  if errorlevel 2 goto :failpop
  git show-ref --verify --quiet "refs/heads/%CANDIDATE%"
  if errorlevel 1 (
    git switch --track -c "%CANDIDATE%" "origin/%CANDIDATE%" || goto :failpop
  ) else (
    git switch "%CANDIDATE%" || goto :failpop
  )
)
echo [ROKAS] Fast-forwarding isolated branch. No merge into main.
git pull --ff-only origin "%CANDIDATE%" || goto :failpop
echo [ROKAS] Downloading the exact cloud Unity-tested model...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Fetch-ArgonUnityPreview.ps1"
if errorlevel 1 goto :failpop
echo.
echo SUCCESS. In Unity choose:
echo ROKAS ^> Bed Sleep ^> Argon ^> Open latest connected hand in Unity
echo The active Bed Sleep V10 was not replaced.
popd
pause
exit /b 0
:failpop
popd
:fail
echo.
echo PREVIEW NOT INSTALLED. Existing production/game work was not merged.
pause
exit /b 1
