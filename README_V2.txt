ROKAS UPDATE / ROLLBACK V2
==========================

Use these files with Unity Editor CLOSED.

1) ROKAS_UPDATE_MAIN_FOLDER_V2.bat
   - saves the current branch/HEAD
   - stashes tracked + untracked local changes if needed
   - creates unique flat safety branches
   - fetches integration with Git auto-maintenance/gc disabled
     (fixes the Windows pack-*.idx unlink retry seen on this PC)
   - switches D:\Rokas\Rokas to integration/rokas-unified
   - aligns it to origin/integration/rokas-unified
   - records rollback information in D:\Rokas\ROKAS_LAST_UPDATE_STATE.txt

2) ROKAS_ROLLBACK_LAST_UPDATE_V2.bat
   - saves any work done after the update
   - restores the branch/HEAD from before the update
   - reapplies the pre-update stash when possible
   - never drops stashes automatically

Changes from V1
---------------
- Fetch uses: git -c maintenance.auto=false -c gc.auto=0 fetch ...
- Safety branch names no longer use backup/... namespace.
- Backup names include milliseconds + random suffix to avoid collisions.
- Existing rollback-state file is preserved before a new update.
- More explicit diagnostics on backup failures.

No script performs git push or force push.
