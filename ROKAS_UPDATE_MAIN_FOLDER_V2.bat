@echo off
setlocal EnableExtensions EnableDelayedExpansion
title ROKAS - UPDATE MAIN FOLDER V2

set "REPO=D:\Rokas\Rokas"
set "STATE=D:\Rokas\ROKAS_LAST_UPDATE_STATE.txt"
set "REMOTE=origin"
set "TARGET=integration/rokas-unified"

echo.
echo ============================================================
echo   ROKAS - UPDATE MAIN FOLDER V2
echo ============================================================
echo.
echo Safe update of D:\Rokas\Rokas to latest integration.
echo This version disables Git auto-maintenance during fetch and uses
echo collision-resistant flat backup branch names.
echo.

if not exist "%REPO%\.git" (
    echo [ERROR] Git repository not found: %REPO%
    goto :fail
)

where git >nul 2>nul
if errorlevel 1 (
    echo [ERROR] Git is not available in PATH.
    goto :fail
)

rem Unity Editor must be closed while switching project files.
tasklist /FI "IMAGENAME eq Unity.exe" 2>nul | find /I "Unity.exe" >nul
if not errorlevel 1 (
    echo [STOP] Unity Editor is running.
    echo Close Unity Editor and run this BAT again.
    goto :fail
)

for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss-fff"') do set "STAMP=%%T"
set "NONCE=%RANDOM%%RANDOM%"

cd /d "%REPO%"
if errorlevel 1 goto :fail

rem Verify repository before touching anything.
git rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
    echo [ERROR] %REPO% is not a valid Git working tree.
    goto :fail
)

set "OLD_BRANCH="
for /f "delims=" %%B in ('git branch --show-current') do set "OLD_BRANCH=%%B"
if not defined OLD_BRANCH set "OLD_BRANCH=__DETACHED__"

set "OLD_HEAD="
for /f %%H in ('git rev-parse HEAD') do set "OLD_HEAD=%%H"
if not defined OLD_HEAD (
    echo [ERROR] Could not read current HEAD.
    goto :fail
)

set "OLD_INTEGRATION_HEAD="
git show-ref --verify --quiet refs/heads/integration/rokas-unified
if not errorlevel 1 (
    for /f %%H in ('git rev-parse integration/rokas-unified') do set "OLD_INTEGRATION_HEAD=%%H"
)

echo Current branch: !OLD_BRANCH!
echo Current HEAD  : !OLD_HEAD!
if defined OLD_INTEGRATION_HEAD echo Local integration HEAD: !OLD_INTEGRATION_HEAD!
echo.

rem Preserve any previous rollback-state file rather than silently overwriting it.
if exist "%STATE%" (
    set "OLD_STATE=D:\Rokas\ROKAS_LAST_UPDATE_STATE.previous-!STAMP!.txt"
    copy /y "%STATE%" "!OLD_STATE!" >nul
    if errorlevel 1 (
        echo [ERROR] Could not preserve previous rollback state.
        goto :fail
    )
    echo Previous rollback state copied to:
    echo   !OLD_STATE!
    echo.
)

rem Save tracked and untracked local work.
set "PRE_STASH_HASH="
set "DIRTY="
for /f "delims=" %%S in ('git status --porcelain --untracked-files=all') do set "DIRTY=1"

if defined DIRTY (
    echo [1/6] Saving local changes to stash...
    git stash push -u -m "ROKAS_AUTO_BACKUP_BEFORE_UPDATE_!STAMP!"
    if errorlevel 1 (
        echo [ERROR] Could not stash local changes.
        goto :fail
    )
    for /f %%H in ('git rev-parse refs/stash') do set "PRE_STASH_HASH=%%H"
    echo       Saved stash: !PRE_STASH_HASH!
) else (
    echo [1/6] Working tree already clean.
)
echo.

rem Flat names avoid refs/heads/backup namespace conflicts from older scripts.
set "BACKUP_BRANCH=rokas-backup-before-update-!STAMP!-!NONCE!"
echo [2/6] Creating safety branch !BACKUP_BRANCH! ...
git show-ref --verify --quiet "refs/heads/!BACKUP_BRANCH!"
if not errorlevel 1 (
    echo [ERROR] Unexpected backup-name collision.
    goto :fail
)
git branch "!BACKUP_BRANCH!" "!OLD_HEAD!"
if errorlevel 1 (
    echo [ERROR] Could not create safety branch.
    echo Current local branches:
    git branch --list
    goto :fail
)

set "INTEGRATION_BACKUP_BRANCH="
if defined OLD_INTEGRATION_HEAD (
    if /I not "!OLD_INTEGRATION_HEAD!"=="!OLD_HEAD!" (
        set "INTEGRATION_BACKUP_BRANCH=rokas-integration-backup-!STAMP!-!NONCE!"
        echo       Backing up previous local integration as !INTEGRATION_BACKUP_BRANCH! ...
        git branch "!INTEGRATION_BACKUP_BRANCH!" "!OLD_INTEGRATION_HEAD!"
        if errorlevel 1 (
            echo [ERROR] Could not back up local integration branch.
            echo The primary safety branch above is still preserved.
            echo Current local branches:
            git branch --list
            goto :fail
        )
    )
)
echo.

rem IMPORTANT: Disable auto maintenance/gc for this fetch. This avoids the
rem Windows pack-*.idx unlink retry that was reproduced on this machine.
echo [3/6] Fetching latest %REMOTE%/%TARGET% ...
git -c maintenance.auto=false -c gc.auto=0 fetch "%REMOTE%" "%TARGET%"
if errorlevel 1 (
    echo [ERROR] Fetch failed. Backups/stash are preserved.
    goto :fail
)

set "NEW_HEAD="
for /f %%H in ('git rev-parse "%REMOTE%/%TARGET%"') do set "NEW_HEAD=%%H"
if not defined NEW_HEAD (
    echo [ERROR] Could not resolve %REMOTE%/%TARGET% after fetch.
    goto :fail
)
echo       Remote integration HEAD: !NEW_HEAD!
echo.

echo [4/6] Switching main project folder to integration/rokas-unified ...
git show-ref --verify --quiet refs/heads/integration/rokas-unified
if errorlevel 1 (
    git switch -c integration/rokas-unified --track "%REMOTE%/%TARGET%"
    if errorlevel 1 (
        echo [ERROR] Could not create/switch local integration branch.
        goto :fail
    )
) else (
    git switch integration/rokas-unified
    if errorlevel 1 (
        echo [ERROR] Could not switch to local integration branch.
        goto :fail
    )
)

rem Approved workflow: local main folder mirrors the verified remote integration.
rem It is protected by OLD_HEAD + safety branches + optional stash above.
git reset --hard "%REMOTE%/%TARGET%"
if errorlevel 1 (
    echo [ERROR] Could not align local integration with remote.
    goto :fail
)

set "FINAL_HEAD="
for /f %%H in ('git rev-parse HEAD') do set "FINAL_HEAD=%%H"
if /I not "!FINAL_HEAD!"=="!NEW_HEAD!" (
    echo [ERROR] Local HEAD does not match fetched integration.
    echo Local : !FINAL_HEAD!
    echo Remote: !NEW_HEAD!
    goto :fail
)

echo.
echo [5/6] Saving rollback state...
(
    echo STAMP=!STAMP!
    echo OLD_BRANCH=!OLD_BRANCH!
    echo OLD_HEAD=!OLD_HEAD!
    echo OLD_INTEGRATION_HEAD=!OLD_INTEGRATION_HEAD!
    echo PRE_STASH_HASH=!PRE_STASH_HASH!
    echo BACKUP_BRANCH=!BACKUP_BRANCH!
    echo INTEGRATION_BACKUP_BRANCH=!INTEGRATION_BACKUP_BRANCH!
    echo UPDATED_HEAD=!FINAL_HEAD!
) > "%STATE%"
if errorlevel 1 (
    echo [ERROR] Could not write rollback state file.
    goto :fail
)
echo       State saved: %STATE%
echo.

echo [6/6] Final verification...
git status --short
echo.
git log -1 --oneline
echo.

echo ============================================================
echo   UPDATE COMPLETE
echo ============================================================
echo Project folder:
echo   %REPO%
echo.
echo Current integration commit:
echo   !FINAL_HEAD!
echo.
echo Safety branch:
echo   !BACKUP_BRANCH!
if defined INTEGRATION_BACKUP_BRANCH echo Integration safety branch: !INTEGRATION_BACKUP_BRANCH!
if defined PRE_STASH_HASH echo Pre-update stash: !PRE_STASH_HASH!
echo.
echo If you do not like this version, close Unity and run:
echo   ROKAS_ROLLBACK_LAST_UPDATE_V2.bat
echo ============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================================
echo   STOPPED SAFELY
echo ============================================================
echo No force push was used.
echo Existing backup branches/stashes were not deleted.
echo.
pause
exit /b 1
