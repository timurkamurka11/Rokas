# Permanent ROKAS integration execution plan

Approved specification: user request to establish integration/rokas-unified and one canonical manual-QA project at D:/Rokas/Rokas.

## Constraints

- Base: verified Messages/Home c68763fb482665ea53cb42c48b2638cc5314c3c0, tree f6318724b3dff93857011803ff09679e9c8d69b5.
- Merge complete verified Combat fa4c44d620debae7bf12984a5db9195794c1aa1a, tree a09aa1cee13271efe4147c4f949734da22a68ac6.
- Remote development remains 5e494f188b90bdb6916e8e78e35ac38509bd0bce. Local development and both source branches remain untouched. No PR #4 merge, force push, new feature, or destructive cleanup.
- Temporary worktrees are implementation infrastructure; final QA belongs in D:/Rokas/Rokas.

## Execution

1. Verify actual remote refs/trees, preserve main checkout status, create isolated integration worktree from exact verified base.
2. Real Git merge with both parent histories; inspect shared runtime responsibilities semantically. Preserve existing Home/weather, Messages/Live Messenger, Combat and save/payment behavior.
3. Resolve only proven test/compile/runtime failures. Existing reaction/payment assertions stay intact. Add one-session Home/Messages-to-Combat runtime proof and opt-in external captures.
4. Run full Core, Unity EditMode, Unity PlayMode, validator baseline comparison, relevant log scan, and literal git diff --check. Inspect actual Home and new CombatHud renders.
5. Review merge and compatibility changes; persist permanent AGENTS.md workflow and portable validation report.
6. Create identical-tree VERIFIED ROKAS UNIFIED INTEGRATION CHECKPOINT; push only integration/rokas-unified and verify remote SHA.
7. Recheck normal folder against final target, back up local tracked diff and every untracked file, and confirm no blocking Unity process. Detach internal integration worktree without deleting it; switch D:/Rokas/Rokas to the exact verified integration tip. Verify preserved local file hashes and untouched source/development refs.
8. Stop for USER MANUAL UNIFIED ROKAS INSPECTION.

## Recovery ledger

Detailed current execution state and raw logs/captures live outside Git at D:/Rokas/unified-integration-evidence. Final portable evidence is documented in Docs/UnifiedIntegration.md.
