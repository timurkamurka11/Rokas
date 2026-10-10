# ROKAS V11 code-only review sync (NOT A FINAL RELEASE)

Five *C# files only* at exact [Unity-tested code commit f7fc077](https://github.com/timurkamurka11/Rokas/commit/f7fc0779bbe16c1b2dd4ffc25dc4ddc2d69d74dc).
The 49 hand PNGs, approved POV, three existing UI PNGs and user audio are **not** included and are never modified by this code-only tool.

This integration is explicitly **REVIEW_ONLY**, not approved production installation. The one-time target Windows Unity review can only be carried out after the project's existing V10 hand frames + original assets are present.

First run READ-ONLY AUDIT from an unpacked build artifact (PowerShell 5.1, no administrator necessary):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\V11_CODE_SYNC.ps1 -Target 'D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY'
```

If and only if **all five** say BASELINE and no local change diverges, close Unity and explicitly apply this **code-only QA staging**:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\V11_CODE_SYNC.ps1 -Target 'D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY' -Mode Apply -ConfirmCodeSync
```

Rollback with the recorded RunId (defaults to last successful install) ONLY if no file has been locally edited since application:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\V11_CODE_SYNC.ps1 -Target 'D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY' -Mode Rollback -ConfirmCodeSync
```

Safeguards: fixed allowlist of exactly five presentation C# files, pinned source/base revisions, SHA256 payload+baseline+installed+backups, zero writes on Audit or divergence, explicit Apply, Unity-closed check, no junctions, backup prior to copy, interruption rollback, post-install edits protected on rollback, non-project target refused. The tool does NOT overwrite source modified independently by the user. When local source hashes differ, do NOT force a patch; compare/merge those source files manually after making a project backup.

The Windows GitHub CI checks all behavior **only in an isolated temporary Unity-shaped fixture**; it does not access the user's D drive. It cannot certify audiovisual QA, scene perspective alignment, exact real-world Power-depth, WAV mix or global Core behavior. The V11 final release remains BLOCKED.
