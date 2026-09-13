# Combat 3 Phase 0 continuation

Updated: 2026-09-13 15:58 UTC. 0A–0C verified; 0D next; full Phase 0 is NOT complete.

## Identity and safety

- Feature: `codex/combat3-phase0`, worktree `D:/Rokas/Rokas/.worktrees/combat3-phase0`.
- Selected baseline: `09994586c3004473706e15a900ebd54ad67c1f2e` (VERIFIED MAIN MENU INTERACTION + AUDIO + HOME TRANSITION CHECKPOINT). Startup fixture correction `aa1fe6d`; verified 0A checkpoint `db5e4d7fa9e18ec2e21e1c4b16bc0a186e3ec560`. Recover actual HEAD using git log, never reset to the baseline.
- Baseline tree: `a05ec6ff5e515f09407687c55bb948e4c9f0a946`.
- Canonical local/remote integration at recovery: `6881911867302dd2308d01d6eaca1369428ed739`, tree `545e5e6b8a79b0a39a68ecd1acfc40f0d181b7aa`. Later VN work deliberately excluded from the explicitly verified source; preserved in canonical and preview trees.
- No existing Combat 3 branch, worktree, commits, handoff, or verification jobs were found. Created this isolated worktree only after checking refs/worktrees.
- Canonical Unity editor PID 18220 with two import workers uses `D:/Rokas/Rokas`; left running. Canonical status has modified ProjectVersion plus untracked generated project settings, package lock, and two video metadata files; no canonical edits made.
- Do not touch development, merge PR #4, rewrite history, remove worktrees, or integrate/push this feature. Latest user instruction requires fully verified Phase 0 AND user integration choice first.
- `rokas-development-workflow` is absent from installed skill paths (searched both Codex skills/plugins and project); use project AGENTS and available Superpowers instructions, report missing skill honestly.

## Source and scope

- Accepted source: `C:/Users/tim/.codex/visualizations/2026/09/10/01a08ac0-162d-7e12-afb6-f01b5747ef5d/combat3-study/ROKAS-Combat-3-Design-Study.md`.
- Exact Phase 0 request: `C:/Users/tim/.codex/attachments/eb918f30-cf5d-4523-99e7-825d5ac221d0/pasted-text.txt`.
- Latest continuation: `C:/Users/tim/.codex/attachments/bf8e00e7-226f-40eb-ba80-58ae10068594/pasted-text.txt`. Confirms the chosen baseline, existing work, no repeat baseline RCA and no integration.
- These materials were read. Do not reread the full study/video or create a new design.
- Only 0A heavy/lane/counter → verified runtime gate → 0B low wave/dodge → verified gate → 0C projectile/deflect → verified gate → 0D locked ink/residual → complete verification.
- Shared CombatService/GameSession remains sole HP/result/economy owner. Separate gated Combat 3 review entry; Combat 2 preserved. No production art or second combat framework.

## Progress / evidence

- Recovery and 0A–0C implementation complete. Save verified0C checkpoint, then0D. Implementation checklist: `Docs/Superpowers/plans/2026-09-12-combat3-phase0.md`.
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
- 0B domain RED18 then GREEN42 C3, preserving all23 C2 and other Core suites: `0b-red-01.log`, `0b-green-01.log`, `0b-domain-report.md`.
- 0B runtime fixes were sequential, each focused RED then GREEN: RMB route/Perfect HUD (`0b-dash-red2`/`0b-dash-green2`); wave proxy/cue (`0b-wave-red2`/`0b-wave-green2`); arena-only LMB (`0b-pointer-red2`/`0b-pointer-green2`); practice portal (`0b-practice-red2`/`0b-practice-green2`).
- Review found pointer/raw direction order and active wave warning jump. Added actual regression tests: `0b-order-red` then `0b-order-green5`; `0b-wave-position-red` then `0b-wave-position-green`. Initial attempt to resample domain queued direction broke existing DodgeDirectional; reverted. Final fix queues/merges RMB in presentation and flushes after direction sample before domain Tick; domain capture semantics preserved.
- 0B final gate: `run-check.py core 0b-core-final5` exit0/all suites/42 C3+23 C2; `run-check.py editmode 0b-edit-final6` 76 passed; `run-check.py playmode 0b-play-final6 Rokas.Tests capture` 104 passed. Earlier gate76/101 also green before two review regressions were added. `0b-play-final3` selected an invalid filter and ran0 tests: NOT verification evidence.
- Current presentation captures: `visuals-0b-play-final6`. Five-lane wave warning, dodge lift/HP preservation, empty dodge no reward and Perfect Seal92/R8 inspected. Validator25 vs tracked baseline27, no added findings; two Unity-generated video metas explain difference.
- Final review also reproduced a rejected raw RMB replay by the pointer duplicate after cooldown expiry: `0b-duplicate-red` then `0b-duplicate-green`. Raw/pointer frame stamps deduplicate both event orders; cancellation preserves stamps.
- No0C projectile/deflect or0D ink/full6–10second composition yet. Do not claim full Phase0 complete.

## Current 0C evidence / next operation

- Verified0B: `1a3441ee5ff52d5417f81d516366fe516541fa66`; current0C changes build on it.
- Latest user continuation fully read: `C:/Users/tim/.codex/attachments/b314c378-b3e5-40e7-9f5b-e061cc4d92ae/pasted-text.txt`. Sequential focused RED/GREEN; no restarting0A/B/C, no integration.
- 0C domain complete:61C3+23C2+all Core suites, `0c-domain-final-green-core.log`; detailed focused pairs/API in external `0c-domain-report.md`. Two marked instances, locked adjacent lanes, independent arrival.9/1.3 and.18 contact lifetimes. Deflect.14/sharedcooldown.55, actual colliding ID only, Seal−12/R+12, pure visual return. Edges, sibling damage, second-instance deflect, wrong/empty/early/late, immunity and cleanup tested.
- Runtime sequential RED→GREEN: `0c-space-route-red/green` (Space/settings cancellation), `0c-projectile-view-red/green` (travel/normal lane avoidance), `0c-return-view-red/green` (actual deflect HUD/return/sibling stays), `0c-practice-red/green` (existing portal travel). Initial `0c-runtime-red1` was missing enum, expected before domain family implementation.
- Final focused C3 PlayMode20/20: `combat3-0c-focused-gate-playmode.xml`. Actual current captures `visuals-0c-focused-gate` inspected: marked projectile, unchanged enemy HP, deflect Seal88/R12, surviving sibling and return.
- Full Unity gates: EditMode95/95 `combat3-0c-edit-gate-editmode.xml`; graphical PlayMode111/111 `combat3-0c-play-gate-playmode.xml`. Includes all previous Home/Messages/startup/C2/save/payment suites.
- Domain review and subsequent scoped presentation review by `phase0c_review` clean. No active domain writer.
- Asset validator `0c-gate-final-validator.log`:25 baseline findings, no new findings versus tracked27. Earlier `0c-gate-validator.log` ran while a Home test temporarily renamed LaptopBoot media; those transient extra findings disappeared after test teardown. Do not treat the intermediate scan as final.
- Record verified0C feature checkpoint after literal diff/log audit. Then0D only: delayed ink locks current lane before clear warning and never retargets; explicit residual lifetime; finish approved6–10second pattern tuning and full lifecycle/runtime proof. No later phases, merge, integration, push, or canonical QA handoff.

## Tool recovery

Default PowerShell fails CET on this host. `exec_command` with `shell: cmd.exe`, `login: false`, without `tty` works. Old Python REPL sessions are unusable; create external .py helpers and execute by path. Python 3.14 is `C:/Python314/python.exe`; use subprocess argument arrays and hidden processes for runners. Unity is `D:/BOT/6000.3.19f1/Editor/Unity.exe`; .NET is `D:/Rokas/combat2-tools/dotnet-8.0.424/dotnet.exe`.

Preserve evidence; update this file after each verified gate and before stopping. Record exact commands/results, changed files, current problem, and one next step. No stale success claims.
