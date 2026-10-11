# ROKAS Laptop Cinematic V11.7 — all gold prompts 50px lower / left LCD inset

Date: 2026-10-11
Reference: user screenshot `image(20261011-123826).png` and attached Game View capture.
Canonical Windows working copy: `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`.
Git branch: `codex/fidelity3-resume-5779190`.

## Approved tiny scope

- Move ALL original gold prompt graphics **50 stage UI pixels DOWN** as a single group, both powered and unpowered: `R — ВСТАТЬ`, `E — ОТКРЫТЬ НОУТБУК`, `E — ВКЛЮЧИТЬ НОУТБУК`, `ESC — НАЗАД`, original off-state back arrow and no-art text/button fallbacks.
- Keep the existing optical correction between differently padded original PNGs: `E +19`, `ESC +13` relative to R. No resizing or editing original artwork, colors, hover and golden glow.
- Adjust ONLY the LCD left border: original-source `leftTopInset 15→22`, `leftBottomInset 18→26`. Keep right `5`, vertical `5`, mesh/mask and original real YOMI UV mapping untouched.
- Preserve the smoother V11.6 seated/standing return and the unchanged 49 original hand frames.
- Do not modify Codex's combat WIP, VN, Unity backgrounds or other parts of ROKAS.

## Exact implementation

`Assets/Rokas/Scripts/Presentation/LaptopCinematicSequence.cs`:

- Single source of truth `GoldPromptDrop = 50f`, `PreviouslyApprovedStandBottom = 1045f`.
- R original transparent art now has a stage bottom edge of `1095f`; E and ESC resolve their targets from this same R edge with prior optical offsets. Stand art remains original 2048×682 with width 438; its ALPHA bounding box ends at y=607 of 682; this gives predicted final last visible pixel at ~1078.96 of the 1080p stage — within frame, although true Windows Game View confirmation is still mandatory.
- Legacy OFF-state arrow and absent-PNG text/button fallback elements also receive the same +50.
- Left source-picture safeCorners: `topLeftX+22f`, `bottomLeftX+26f` while topRight/bottomRight use unchanged `-5f`. Both power states share one stencil.

`Assets/Rokas/Tests/PlayMode/LaptopV111SeamlessPOVPlayModeTests.cs`:

- Asserts original source images remain and gold R edge is `1045+50`, E is R+19, ESC is R+13 and original back arrow moves y=`942+50`.

## Verification

- 10/10 focused structural checks on current remote branch passed. Geometry check of original user PNG alpha bounds predicts no visible R button cropping at 1080p.
- [Art-integrated Unity PlayMode](https://github.com/timurkamurka11/Rokas/actions/runs/38140634402) **BLOCKED at existing unrelated C# compile error** (as in V11.5/V11.6):
  `ReactiveCombatArena.cs` CS0246 `CombatShotBounds` (lines 76/77), `CombatShotBoundsProjection` (line 1899).
- [Main Unity EditMode/PlayMode workflow](https://github.com/timurkamurka11/Rokas/actions/runs/38140634333) for follow-up status.

**Do not claim Unity PASS for V11.7 or final pixel-perfect Windows visual acceptance until Codex combat compile blocker is resolved and tests re-run.** No destructive local Git operations, new review clones or BAT packages. Cloud branch changed only. If Codex writes are ongoing in canonical local Unity copy, sync only at a safe checkpoint.
