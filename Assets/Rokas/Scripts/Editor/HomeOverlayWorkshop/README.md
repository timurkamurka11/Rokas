# ROKAS Main Room Overlay Workshop

Editor-only authoring workspace for the Main Room scan/glow overlay. The current manually edited workshop scene is authoritative; **Open or Create** opens the saved scene when it exists and does not regenerate it.

## Current manual scene

The user-authored scene from the handoff is authoritative. Removed default objects must not be recreated implicitly. The current authored set keeps Door/Laptop/Cat outlines plus the selected Window/Swords/Door/Laptop/FloorLamp/Cat connectors and hotspots.

`ReferenceHelpers/ReferenceOracle` remains optional. The historical `Variant 4 — scan mode reveal` header is not a dependency of the glow tooling and should not be restored if the user removes it.

## Existing icon workflow

1. Import/copy the supplied hotspot PNGs into `Assets/Rokas/Art/Home/OverlayWorkshop/Icons`.
2. Choose **Refresh Imported Assets** if the scene needs its Image references rebound to the imported icon paths.
3. Save with **Ctrl+S**.

## Custom Glow

The custom golden line and haze art used for manual review is installed under:

- `Assets/Rokas/Art/Home/OverlayWorkshop/CustomGlow/Source/`
- reusable cropped pieces under `CustomGlow/Sprites/`
- optional haze pieces under `CustomGlow/Haze/`

Use **ROKAS → Main Room Overlay Workshop → Prepare Custom Glow Layer** once for the active workshop. It creates/preserves:

- `WorkshopCanvas/CustomGlow/GlowLines`
- `WorkshopCanvas/CustomGlow/GlowConnectors`
- `WorkshopCanvas/CustomGlow/GlowFrames`
- `WorkshopCanvas/CustomGlow/GlowFX`

A small starter palette is placed near the bottom of the 1920×1080 authoring canvas. It is intentionally not a final composition. Move, resize, rotate, duplicate, disable or delete pieces with normal RectTransform tools.

For additional pieces use **Glow Palette** / **Open Custom Glow Palette**. No manual sprite slicing is required.

Straight horizontal/vertical pieces are imported with sprite borders so Unity `Image.Type.Sliced` can stretch them while preserving the bright end-cap region. Irregular corners, frames, dots and circuit pieces retain their native proportions by default.

## Pulse / shimmer / haze

Each custom glow piece has `MainRoomOverlayGlowFx` authoring controls:

- Brightness
- Pulse Enabled / Speed / Amount
- Shimmer Enabled / Speed / Amount / Width
- Phase Offset
- Haze Enabled / Opacity (used by haze elements)

Pulse is intentionally subtle. Shimmer uses the reusable editor-workshop shader `ROKAS/Workshop/GlowShimmer`; it animates in edit/review without writing transient phase data into the scene every frame. Haze pieces are optional and the default preview is disabled.

## Layout JSON

`MainRoomOverlayWorkshopLayoutIO` now writes schema v2. v2 stores the custom glow object set, sprite paths, complete RectTransform state, active state, Image styling and FX parameters. Import reconciles only `CustomGlow` objects by stable ID; it does **not** rebuild deleted legacy defaults.

Old schema v1 JSON remains accepted and applies only legacy layout data, so older files such as `glow check.json` remain usable.

Use:

- **Export Layout JSON...** after manual authoring
- **Import Layout JSON...** to restore a saved layout

## Scope

The workshop scene remains outside Build Settings. This tooling does not integrate or modify runtime Home, VN Scene Composer, ReactiveTurns or combat. Runtime integration happens only after manual visual approval.
