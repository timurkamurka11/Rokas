# Combat 3 Phase 0 continuation

Updated: 2026-09-13 01:58 UTC. 0A is verified; full Phase 0 is NOT complete.

## Identity and safety

- Feature: `codex/combat3-phase0`, worktree `D:/Rokas/Rokas/.worktrees/combat3-phase0`.
- Selected baseline: `09994586c3004473706e15a900ebd54ad67c1f2e` (VERIFIED MAIN MENU INTERACTION + AUDIO + HOME TRANSITION CHECKPOINT). Startup fixture correction committed as `aa1fe6d`; 0A feature checkpoint follows. Recover actual HEAD using git log, never reset to the baseline.
- Baseline tree: `a05ec6ff5e515f09407687c55bb948e4c9f0a946`.
- Canonical local/remote integration at recovery: `6881911867302dd2308d01d6eaca1369428ed739`, tree `545e5e6b8a79b0a39a68ecd1acfc40f0d181b7aa`. Later VN work deliberately excluded from the explicitly verified source; preserved in canonical and preview trees.
- No existing Combat 3 branch, worktree, commits, handoff, or verification jobs were found. Created this isolated worktree only after checking refs/worktrees.
- Canonical Unity editor PID 18220 with two import workers uses `D:/Rokas/Rokas`; left running. Canonical status has modified ProjectVersion plus untracked generated project settings, package lock, and two video metadata files; no canonical edits made.
- Do not touch development, merge PR #4, rewrite history, remove worktrees, or integrate/push this feature. Latest user instruction requires fully verified Phase 0 AND user integration choice first.
- `rokas-development-workflow` is absent from installed skill paths (searched both Codex skills/plugins and project); use project AGENTS and available Superpowers instructions, report missing skill honestly.

## Source and scope

- Accepted source: `C:/Users/tim/.codex/visualizations/2026/09/10/01a08ac0-162d-7e12-afb6-f01b5747ef5d/combat3-study/ROKAS-Combat-3-Design-Study.md`.
- Exact Phase 0 request: `C:/Users/tim/.codex/attachments/eb918f30-cf5d-4523-99e7-825d5ac221d0/pasted-text.txt`.
- Latest continuation: `C:/Users/tim/.codex/attachments/bf54d68b-bf73-4873-b420-542bd4ac1d36/pasted-text.txt`. Confirms the chosen baseline, existing work, no repeat baseline RCA and no integration.
- These materials were read. Do not reread the full study/video or create a new design.
- Only 0A heavy/lane/counter → verified runtime gate → 0B low wave/dodge → verified gate → 0C projectile/deflect → verified gate → 0D locked ink/residual → complete verification.
- Shared CombatService/GameSession remains sole HP/result/economy owner. Separate gated Combat 3 review entry; Combat 2 preserved. No production art or second combat framework.

## Progress / evidence

- Recovery and 0A complete. 0B is next; 0C–0D wait for earlier gates. Implementation checklist: `Docs/Superpowers/plans/2026-09-12-combat3-phase0.md`.
- Evidence/tooling directory: `D:/Rokas/combat3-phase0-evidence`.
- Portable test runner adapted from existing `D:/Rokas/combat2-tools/run-tests.mjs`; commands use `node D:/Rokas/combat3-phase0-evidence/run-baseline.mjs core|editmode|playmode`.
- Core baseline: exit 0, all existing domain suites, 23 Combat 2 scenarios and four original requirements pass. `baseline-core-runner.log`.
- EditMode baseline: exit 0, 34 passed / 0 failed. `combat3-baseline-editmode-latest.xml` and `.log`.
- PlayMode headless baseline crashed in native particle rendering (`GfxDevice::DrawSharedGeometryJobs`), no XML. Graphics baseline then completed: 88/91 pass; three preexisting startup test failures, `combat3-baseline-gfx-playmode.xml`.
- Root causes proven from existing runtime: two fixtures assume Linux decoder fallback although Windows plays the preview; one expects Home after one frame despite existing fade. Corrected only the two test fixture files. Focused rerun `node D:/Rokas/combat3-phase0-evidence/run-startup-regression.mjs playmode`: exit 0, 12/12 pass, `combat3-startup-regression-playmode.xml`. No startup production changes.
- Baseline asset validator on tracked source at 09994586: **27** findings (two missing video metas, four unresolved TMP GUIDs, eighteen below-HD images, three mono PCM sources), `tracked-baseline-validator.log`. Earlier handoff count26 was corrected by counting actual lines. This exceeds historical13 and is existing source debt, not new feature findings. 0A working-copy validator has25, no added findings; Unity generated the two missing video metas, left untracked. Crash-generated InitTestScene owned by this run was removed individually after the editor stopped.
- Domain TDD: `0a-red-01.log` expected23 missing-entry assertions; `0a-green-attempt-01.log`23green; `0a-red-02-backlog.log` reproduced aggregate clock backlog; `0a-green-02-backlog.log`24green. Agent finished scoped implementation/report before its usage limit; no agent jobs remain. `0a-domain-report.md` records API, cases and boundaries.
- Presentation TDD: `combat3-0a-presentation-red-playmode.xml`3 expected failures; initial GREEN3/3 plus real captures. `combat3-0a-lifecycle-red-playmode.xml` proved2 failures: stale counter invitation after use, application pause undone by Update. Fixed both, retained the held/new-press requirement.
- **0A gate:** `node D:/Rokas/combat3-phase0-evidence/run-0a-gate.mjs core|editmode|playmode`. Core exit0/all domain suites including24C3 and23C2; EditMode58/58; graphical PlayMode96/96 (five C3 cases plus all existing suites). XML/logs named `combat3-0a-gate-editmode.*`, `combat3-0a-gate-playmode.*`, Core `0a-gate-core.log`.
- Runtime visuals inspected in `visuals-0a` and captured fresh in `visuals-0a-final`: visible five lanes, locked heavy telegraph, lane move avoids hit, staying reduces HP100→92 while window still opens, fresh pointer counter enemyHP180→171/Seal100→75/Resonance0→10. No final art; fixed proxy pool, existing uGUI/MissionView/Bootstrap and shared economy remain.
- Literal `git diff --check`: exit0, empty stdout/stderr after normalizing only owned text files to repository CRLF. `0a-diff-check.log`. Unity .meta GUIDs preserved; only new C3 metadata included in feature commit; generated project settings/package lock/video metadata left untracked.
- Initial Core launch did not execute .NET because copied runner accidentally changed the tools directory. Confirmed missing executable, corrected only that path, rerun passed. No production fix or test change.
- No0B wave/dodge, no0C projectile/deflect, no0D ink/full6–10second composition yet. Do not claim full Phase0 complete.

## Exact next operation

Save the reviewed 0A feature checkpoint, then add 0B RED tests for low wave and RMB dodge/perfect timing using the same Combat3Encounter. Continue sequentially; do not merge newer VN work or integrate this branch.

## Tool recovery

Default PowerShell fails CET on this host. `exec_command` with `shell: cmd.exe`, `login: false`, `tty: true` works. Python 3.14 is `C:/Python314/python.exe`; use subprocess argument arrays and hidden processes for runners. Unity is `D:/BOT/6000.3.19f1/Editor/Unity.exe`; .NET is `D:/Rokas/combat2-tools/dotnet-8.0.424/dotnet.exe`.

Preserve evidence; update this file after each verified gate and before stopping. Record exact commands/results, changed files, current problem, and one next step. No stale success claims.
