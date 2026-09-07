# ROKAS Unity GameCI Design

## Goal
Add a real Unity CI lane around the existing ROKAS project so GitHub can compile Unity and run EditMode + PlayMode tests before manual editor verification.

## Current repository facts
- Source of truth branch: `development`.
- Unity project root: repository root (`projectPath: .`).
- Unity version: `6000.3.19f1`.
- Unity Test Framework: `com.unity.test-framework` `1.4.6`.
- Existing Unity test assemblies: EditMode and PlayMode under `Assets/Rokas/Tests/`.
- Existing `.github/workflows/core-tests.yml` is a fast .NET/static validation lane and does not execute Unity.
- No `Packages/packages-lock.json` is committed.
- No Git LFS configuration is present in the current repository tree.

## CI shape
Create a separate `.github/workflows/unity-ci.yml` rather than expanding `core-tests.yml`, because the responsibilities and runtime costs are different.

Initial trigger: `workflow_dispatch` only. Automatic `push` and `pull_request` triggers for `development` are enabled only after a real manual GameCI run has either passed or produced a clearly understood pre-existing production blocker.

The Unity job runs on `ubuntu-latest`, uses the project root, Unity version auto-detection, `testMode: All`, a bounded timeout, concurrency cancellation, a `Library` cache keyed from `Assets/**`, `Packages/**`, and `ProjectSettings/**`, and always uploads test artifacts.

Current stable action majors verified on 2026-09-08:
- `game-ci/unity-test-runner@v4`
- `actions/checkout@v7`
- `actions/cache@v6`
- `actions/upload-artifact@v7`

## Unity 6 coverage constraint
The current GameCI documentation describes `coverageEnabled: false`, but the published stable `unity-test-runner@v4` action manifest does not expose that input. GameCI's August 2026 issue history confirms that v4 previously enabled coverage unconditionally and that the proposed v4 opt-out was not merged; the implementation is being replaced in v5 beta.

Therefore the stable workflow must not include a fake `coverageEnabled` input that v4 would ignore. ROKAS will stay on the stable v4 runner for the first manual CI experiment and will report this as a known GameCI limitation. No code-coverage setup is added to ROKAS itself.

## Targeted ROKAS smoke coverage
Add minimal Unity tests using production loading/reference patterns:

1. Food resources: load `Food/ROKAS_FoodAtlas` plus the five actual detail resources as `Texture2D`: `TravelerOnigiri`, `SpicyMiso`, `HunterTempura`, `MoonMochi`, and `GreenTea`. Ramen has no separate detail resource in production and is represented by the atlas, so no fictional `Food/Details/Ramen` path is introduced.
2. Laptop/lamp audio: load `RokasAssets` and assert the production-bound `laptopMouseClick`, `lampOn`, and `lampOff` `AudioClip` fields are non-null. Do not invent Resources audio paths because production uses serialized GUID bindings.
3. Food build path: reuse the existing PlayMode `LaptopFoodPreservesPurchaseGuardAndReturnsFocus` path, which opens the real Food laptop page and therefore executes `LaptopFoodView.Build()`. Add only direct resource assertions needed for fast diagnosis; do not create a second UI robot architecture.

## Security and licensing
No credentials or license material are committed or requested in chat. The workflow reads GitHub Actions secrets only. For Unity Personal the required secrets are `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD`; for Professional the license secret is `UNITY_SERIAL` plus email/password. License type is not inferable from repository data.

## Non-goals
No gameplay, combat, Food UI, laptop UI, graphics, audio content, save/progression, Unity builds, releases, deployment, branch protection, required checks, or package-dependency repair is part of this task.
