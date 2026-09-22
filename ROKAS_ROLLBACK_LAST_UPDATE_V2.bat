@echo off
setlocal EnableExtensions EnableDelayedExpansion
title ROKAS - ROLLBACK LAST UPDATE V2

set "REPO=D:\Rokas\Rokas"
set "STATE=D:\Rokas\ROKAS_LAST_UPDATE_STATE.txt"

echo.
echo ============================================================
echo   ROKAS - ROLLBACK LAST UPDATE V2
echo ============================================================
echo.

if not exist "%REPO%\.git" (
    echo [ERROR] Git repository not found: %REPO%
    goto :fail
)

if not exist "%STATE%" (
    echo [ERROR] No rollback state file found:
    echo         %STATE%
    goto :fail
)

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git is not available in PATH.
    goto :fail
)

tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
    echo [STOP] Unity Editor is running.
    echo Close Unity Editor and run rollback again.
    goto :fail
)

for /f "usebackq tokens=1,* delims==" %%A in ("%STATE%") do set "%%A=%%B"

if not defined OLD_HEAD (
    echo [ERROR] Rollback state is incomplete: OLD_HEAD missing.
    goto :fail
)

cd /d "%REPO%"
if errorlevel 1 goto :fail

echo Previous branch: !OLD_BRANCH!
echo Previous HEAD  : !OLD_HEAD!
echo Updated HEAD   : !UPDATED_HEAD!
echo Safety branch  : !BACKUP_BRANCH!
echo.

rem Preserve anything changed after the update.
set "ROLLBACK_STASH_HASH="
set "NOW_DIRTY="
for /f "delims=" %%S in ('git status --porcelain --untracked-files=all') do set "NOW_DIRTY=1"

if defined NOW_DIRTY (
    for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss-fff"') do set "NOWSTAMP=%%T"
    echo [1/4] Saving changes made after update...
    git stash push -u -m "ROKAS_AUTO_BACKUP_BEFORE_ROLLBACK_!NOWSTAMP!"
    if errorlevel 1 goto :fail
    for /f %%H in ('git rev-parse refs/stash') do set "ROLLBACK_STASH_HASH=%%H"
    echo       Safety stash: !ROLLBACK_STASH_HASH!
) else (
    echo [1/4] Current working tree is clean.
)
echo.

echo [2/4] Restoring previous branch/HEAD...
if /I "!OLD_BRANCH!"=="__DETACHED__" (
    git switch --detach "!OLD_HEAD!"
    if errorlevel 1 goto :fail
) else (
    git show-ref --verify --quiet "refs/heads/!OLD_BRANCH!"
    if errorlevel 1 (
        git switch -c "!OLD_BRANCH!" "!OLD_HEAD!"
        if errorlevel 1 goto :fail
    ) else (
        git switch "!OLD_BRANCH!"
        if errorlevel 1 goto :fail
        git reset --hard "!OLD_HEAD!"
        if errorlevel 1 goto :fail
    )
)

echo.
echo [3/4] Restoring pre-update local changes...
if defined PRE_STASH_HASH (
    git cat-file -e "!PRE_STASH_HASH!^{commit}" 2>nul
    if errorlevel 1 (
        echo [WARNING] Recorded pre-update stash no longer exists:
        echo           !PRE_STASH_HASH!
        echo Branch/HEAD rollback succeeded, but that stash cannot be auto-applied.
    ) else (
        git stash apply "!PRE_STASH_HASH!"
        if errorlevel 1 (
            echo.
            echo [WARNING] Conflicts occurred while restoring pre-update changes.
            echo The stash was NOT deleted.
            echo Recorded stash: !PRE_STASH_HASH!
            goto :partial
        )
    )
) else (
    echo       No pre-update stash was recorded.
)

echo.
echo [4/4] Final verification...
git status
echo.
git log -1 --oneline
echo.

for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss-fff"') do set "USEDSTAMP=%%T"
move /y "%STATE%" "D:\Rokas\ROKAS_LAST_UPDATE_STATE.used-!USEDSTAMP!.txt" >nul

echo ============================================================
echo   ROLLBACK COMPLETE
echo ============================================================
echo Restored:
echo   !OLD_BRANCH!
echo   !OLD_HEAD!
echo.
if defined ROLLBACK_STASH_HASH (
    echo Changes made AFTER the update were preserved separately in stash:
    echo   !ROLLBACK_STASH_HASH!
    echo They were not auto-applied over the old version.
)
echo ============================================================
echo.
pause
exit /b 0

:partial
echo.
echo ============================================================
echo   HEAD RESTORED - LOCAL CHANGES NEED MANUAL ATTENTION
echo ============================================================
echo Your previous commit is restored.
echo The pre-update stash remains preserved.
echo No stash was dropped.
echo.
pause
exit /b 2

:fail
echo.
echo ============================================================
echo   STOPPED SAFELY
echo ============================================================
echo No force push was used.
echo No stash was deleted.
echo.
pause
exit /b 1
