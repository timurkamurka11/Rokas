# ROKAS permanent integration and manual-QA workflow

## Canonical game tree

- The long-lived staging/manual-QA branch is integration/rokas-unified.
- The user's normal Unity project folder is D:/Rokas/Rokas, using Unity 6000.3.19f1.
- Feature branches and other worktrees are internal implementation infrastructure. A verified feature checkpoint alone is not ready for user manual QA.
- Every future milestone must be integrated and verified with the current game before handoff. Do not direct routine user QA to separate Home, Combat, Messages, or feature project folders.

## Required milestone sequence

1. Implement in an isolated feature branch/worktree when useful and verify that feature.
2. Recover actual refs and select the explicitly verified source checkpoint. Stop before incorporating unknown later work when source refs differ from the approved checkpoint.
3. Integrate into integration/rokas-unified, preferring a real Git merge preserving history.
4. Resolve shared runtime/input/save/UI conflicts semantically, preserving all compatible responsibilities. Do not choose a whole side merely to clear a conflict.
5. Run the complete integrated Core/domain, Unity EditMode, and Unity PlayMode suites. Include Home, Messages/Live Messenger, Combat, save/progression, and exactly-once payment regressions. Verify real runtime visuals for affected presentation.
6. Run the asset validator and compare with the recorded baseline (historically 13 findings; never assume new findings are acceptable). Inspect relevant Unity logs. Run literal git diff --check; require exit 0 and empty output.
7. With a green integrated tree, create VERIFIED ROKAS UNIFIED INTEGRATION CHECKPOINT, push only integration/rokas-unified, and verify its exact local/remote SHA match.
8. Safely update D:/Rokas/Rokas to that verified integration checkpoint. Inspect status, unstaged/staged diffs, untracked collisions, and editor state first. Preserve all meaningful local changes; stop before an unsafe switch. Never reset/clean/force-checkout over user work.
9. Hand off the one canonical folder for user manual QA, report exact checkpoint and verification evidence, then stop at the requested milestone.

## Protected work

- Do not update or merge into development unless the user explicitly instructs it later. Do not treat integration approval as development approval.
- Do not force-push or rewrite verified feature branch history.
- Do not merge PR #4 as part of establishing this workflow.
- Preserve existing worktrees and evidence until the user approves cleanup after integrated manual inspection.
- Keep new Home 2.5D/weather, Live Messenger reactions/typing/unread/audio, Combat 2.0 manual input and HUD, and save/payment lifecycle together. Never reintroduce the legacy automatic-attack combat UI.

This project-wide rule supersedes historical feature reports that requested testing from separate worktrees. See Docs/UnifiedIntegration.md for the first integration checkpoint's evidence.
