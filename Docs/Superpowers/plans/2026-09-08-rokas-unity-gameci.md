# ROKAS Unity GameCI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real Unity GitHub Actions lane that runs ROKAS EditMode + PlayMode tests and targeted resource smoke checks.

**Architecture:** Keep the existing fast `core-tests.yml` unchanged and add a separate manual-first GameCI workflow. Reuse existing Unity test assemblies, add one focused PlayMode resource smoke file, then enable automatic development/PR triggers only after a real manual run or a clearly understood blocker.

**Tech Stack:** Unity 6000.3.19f1, Unity Test Framework 1.4.6, GitHub Actions, GameCI `unity-test-runner@v4`, checkout@v7, cache@v6, upload-artifact@v7.

**Spec:** `Docs/Superpowers/specs/2026-09-08-rokas-unity-gameci-design.md`

## Global Constraints

- Do not modify gameplay, combat, Food UI, laptop UI, graphics, audio content, save, progression, or package dependencies.
- Initial Unity workflow trigger is `workflow_dispatch` only.
- Use `projectPath: .`, `unityVersion: auto`, and `testMode: All`.
- Do not add Unity build/release/deployment/coverage/branch-protection architecture.
- Never commit or print Unity credentials/license material.
- Preserve checkpoint commits after each completed milestone.
- Stable v4 does not currently expose the documented `coverageEnabled` input; do not add an ignored input.

---

### Task 1: Manual-first GameCI workflow

**Files:**
- Create: `.github/workflows/unity-ci.yml`

**Interfaces:**
- Consumes: root Unity project, `ProjectSettings/ProjectVersion.txt`, existing `Assets/Rokas/Tests` assemblies, GitHub Actions secrets.
- Produces: manual `ROKAS Unity CI` workflow and uploaded Unity test artifacts.

- [ ] **Step 1: Create the workflow with manual trigger only**

Use exactly this shape:

```yaml
name: ROKAS Unity CI

on:
  workflow_dispatch:

permissions:
  contents: read
  checks: write

concurrency:
  group: Unity-CI-${{ github.ref }}
  cancel-in-progress: true

jobs:
  unity-tests:
    name: ROKAS Unity Tests
    runs-on: ubuntu-latest
    timeout-minutes: 45
    steps:
      - name: Checkout repository
        uses: actions/checkout@v7

      - name: Cache Unity Library
        uses: actions/cache@v6
        with:
          path: Library
          key: Unity-Library-${{ runner.os }}-${{ hashFiles('Assets/**', 'Packages/**', 'ProjectSettings/**') }}
          restore-keys: |
            Unity-Library-${{ runner.os }}-

      - name: Run EditMode and PlayMode tests
        id: unity-tests
        uses: game-ci/unity-test-runner@v4
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
          UNITY_SERIAL: ${{ secrets.UNITY_SERIAL }}
        with:
          projectPath: .
          unityVersion: auto
          testMode: All
          githubToken: ${{ secrets.GITHUB_TOKEN }}
          checkName: ROKAS Unity Tests

      - name: Upload Unity test artifacts
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: rokas-unity-test-results
          path: ${{ steps.unity-tests.outputs.artifactsPath }}
          if-no-files-found: warn
```

- [ ] **Step 2: Verify configuration statically**

Check YAML structure, stable action majors, manual-only trigger, no LFS option, project root, cache inputs, timeout/concurrency, secret references only, and artifact output usage. Run/confirm `git diff --check` where a git working tree is available; otherwise verify the resulting remote diff contains only `.github/workflows/unity-ci.yml` and no whitespace-error patch.

- [ ] **Step 3: Checkpoint commit**

Commit message:

```text
wip: checkpoint gameci workflow
```

Record the SHA before starting smoke tests.

---

### Task 2: Critical resource smoke tests

**Files:**
- Create: `Assets/Rokas/Tests/PlayMode/CriticalResourceSmokeTests.cs`
- Create: `Assets/Rokas/Tests/PlayMode/CriticalResourceSmokeTests.cs.meta`

**Interfaces:**
- Consumes: `Resources.Load<Texture2D>`, `Resources.Load<RokasAssets>`, existing `Rokas.PlayModeTests` assembly references.
- Produces: direct diagnostic tests for the Food detail assets and serialized laptop/lamp audio bindings.

- [ ] **Step 1: Establish RED capability without touching production assets**

Use a controlled test-only missing path to prove the assertion helper fails for a missing resource. Do not delete or rename any production resource. The helper remains test-only.

- [ ] **Step 2: Add the production resource assertions**

Create the test file with these tests:

```csharp
using System;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class CriticalResourceSmokeTests
    {
        private static readonly string[] FoodDetails =
        {
            "Food/Details/TravelerOnigiri",
            "Food/Details/SpicyMiso",
            "Food/Details/HunterTempura",
            "Food/Details/MoonMochi",
            "Food/Details/GreenTea"
        };

        [Test]
        public void MissingTextureProbeProvesGuardCanFail()
        {
            Assert.Throws<AssertionException>(() =>
                RequireResource("Food/Details/__ROKAS_CI_MISSING_PROBE__", Resources.Load<Texture2D>));
        }

        [Test]
        public void ProductionFoodResourcesLoadAsTexture2D()
        {
            RequireResource("Food/ROKAS_FoodAtlas", Resources.Load<Texture2D>);
            foreach (string path in FoodDetails)
                RequireResource(path, Resources.Load<Texture2D>);
        }

        [Test]
        public void CriticalLaptopAudioBindingsArePresent()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null, "Missing Resources/RokasAssets asset.");
            Assert.That(assets.laptopMouseClick, Is.Not.Null, "Missing production LaptopMouseClick binding.");
            Assert.That(assets.lampOn, Is.Not.Null, "Missing production LampOn binding.");
            Assert.That(assets.lampOff, Is.Not.Null, "Missing production LampOff binding.");
        }

        private static void RequireResource<T>(string path, Func<string, T> load) where T : UnityEngine.Object
        {
            Assert.That(load(path), Is.Not.Null, "Missing production Unity resource: " + path);
        }
    }
}
```

- [ ] **Step 3: Verify integration scope**

Confirm no production code changed, `Rokas.PlayModeTests.asmdef` already references `Rokas.Presentation`, and the existing `LaptopFoodPreservesPurchaseGuardAndReturnsFocus` PlayMode test still exercises the actual Food UI build path.

- [ ] **Step 4: Checkpoint commit**

Commit message:

```text
wip: checkpoint unity ci smoke tests
```

Record the SHA.

---

### Task 3: License gate and first manual run

**Files:**
- No repository changes unless a real configuration failure requires a narrowly scoped workflow correction.

**Interfaces:**
- Consumes: GitHub Actions secrets and the manual workflow.
- Produces: an actual GameCI run with known checkout/activation/package/compile/EditMode/PlayMode/artifact status.

- [ ] **Step 1: Determine license handling without exposing secrets**

Repository data does not reveal license type. For Personal, user configures `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`. For Pro, user configures `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`. No values are sent through chat.

- [ ] **Step 2: Dispatch only after secrets are configured**

Run `ROKAS Unity CI` through `workflow_dispatch` and inspect jobs/logs/artifacts.

- [ ] **Step 3: Classify any first failure**

Classify as license, GameCI config, package resolution, compile, EditMode, PlayMode, or actual ROKAS regression. Apply `systematic-debugging` only to the real first root cause and do not repair unrelated pre-existing production issues in this task.

---

### Task 4: Automatic CI triggers after the manual gate

**Files:**
- Modify: `.github/workflows/unity-ci.yml`

**Interfaces:**
- Consumes: known result from Task 3.
- Produces: final development push + PR Unity checks while retaining manual dispatch.

- [ ] **Step 1: Update triggers only after the gate condition is met**

Use:

```yaml
on:
  workflow_dispatch:
  push:
    branches:
      - development
  pull_request:
    branches:
      - development
```

- [ ] **Step 2: Verify final workflow diff and triggers**

Confirm no gameplay/package changes, concurrency remains active, artifacts remain `if: always()`, and Unity inputs remain project-root/auto/All.

- [ ] **Step 3: Final checkpoint commit**

Commit message:

```text
ci: enable automatic Unity checks
```

Record SHA and verify the resulting Actions run state.
