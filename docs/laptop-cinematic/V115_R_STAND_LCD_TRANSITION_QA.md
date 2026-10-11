# ROKAS Laptop Cinematic V11.5 — R stand, full-width YOMI LCD, persistent powered transition

Date: 2026-10-11
User references: `bandicam 2026-10-11 12-40-46-022.mp4` (13.2 sec); original transparent `R — ВСТАТЬ` 2048x682 PNG; two physical-LCD screenshots.

## Observed root causes

- The powered controls were at different heights and the stand PNG still read E although E opens YOMI.
- `RefreshExactYomiMirror` selected `Mathf.Max(lcdW / texture.width, lcdH / texture.height)` on a **rectangular RawImage**. Its overscanned texture was clipped by the masked LCD rectangle, losing genuine content at both left and right.
- `BackToRoom` invoked `SetPromptVisibility(false)`, which instantly disabled the ON mini-YOMI viewport while the POV camera was still fading to the room. The approved OFF-state photo then appeared with a black LCD at about 5.85 s. During powered reseating, the viewport was only enabled after the approach animation instead of at the very first seated transition frame.

## Changes scoped ONLY to laptop

1. Import exact user **`R — ВСТАТЬ`** PNG at `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_stand_r.png`, 667984 bytes, original RGBA 2048x682, SHA256 `e56a6ee3e9c16838b07191fca1ff08dcb19ebf5b3a6b31ebf79cd4890b1e3c46`. No redraw or recompression. Preserved previous E PNG separately. Original asset upload workflow passed: https://github.com/timurkamurka11/Rokas/actions/runs/38129621708.
2. `LaptopCinematicSequence` uses `prompt_stand_r` for powered stand button; `RokasView.Tick` handles real keyboard R by calling `BackToRoom()` only while powered+seated; E still opens YOMI, and Esc can also stand.
3. Powered **R Stand, E Open and ESC Back**, and OFF prompt pairs, are placed along a shared **1045 stage-pixel lower baseline**. All original PNG dimensions, colors, hover and glow remain unchanged.
4. A new `PhysicalExactYomiPerspective` `LaptopPerspectiveQuad` directly maps all of the real fullscreen YOMI screenshot UVs [0,1] onto the four already calibrated physical LCD corners. No rectangular cover-scale/crop; the actual original screenshot fills the trapezoid with its full left and right content. `PhysicalMiniYomiVisible` retains a passive texture ref for tests/fallback but is hidden when the real exact screenshot quad is available.
5. A powered LCD remains visible when **standing** (until the POV fades out) and is enabled immediately when **reseating** or returning from YOMI, instead of briefly showing the approved photo's unpowered black LCD.

## Added PlayMode acceptance

- Original `prompt_stand_r` Resource load, correct texture and non-tinted color, hover/glow.
- Same bottom baseline for all three powered gold prompts and both OFF prompts.
- Presence of exact full-UV perspective LCD quad.
- Before stand/after invoking R Stand button, physical LCD remains on while returning to room.
- On immediate repeat seating, the powered LCD is already visible before approach ends.

## CI status / blocker

- **Exact original R PNG import and meta:** PASS — https://github.com/timurkamurka11/Rokas/actions/runs/38129621708
- **Full Unity PlayMode:** blocked at C# compile — https://github.com/timurkamurka11/Rokas/actions/runs/38129745619
- **Unity EditMode + regression:** https://github.com/timurkamurka11/Rokas/actions/runs/38129745601

Blocking compile errors PRECEDE THIS PATCH (reproduced on branch head `e756257`):
`Assets/Rokas/Scripts/Presentation/ReactiveCombatArena.cs` requires missing
`CombatShotBounds` and `CombatShotBoundsProjection`, reported as CS0246 at lines
76,77,1899. These are part of the parallel Codex combat WIP, not the laptop.
Do not invent stubs or alter combat without explicit approval. Therefore do NOT claim V11.5 runtime PASS or local pixel-perfect Game View acceptance until combat compile is restored and tests rerun.

## Safety and local handoff

Only laptop workflow, exact original PNG + importer, `LaptopCinematicSequence.cs`, `RokasView.cs`, targeted PlayMode tests were modified on existing branch `codex/fidelity3-resume-5779190`. The user's Codex WIP combat commits `be7fca2` and `e756257` remain untouched. Do not attempt `reset --hard`, `clean -fd`, `push --force` or branch overwrite.

The user's Windows canonical Unity working copy is `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`. Before pulling, finish pending Codex writes and verify `git status -sb` and local/remote divergence. Avoid re-running the earlier blind pull if the branches have diverged. Once it is safe to sync and the missing combat helper types are committed, check the real QHD screenshot for 4-corner fit/no image cropping, lower alignment and no LCD black flicker at standing/seating.
