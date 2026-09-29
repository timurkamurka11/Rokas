# VN UI Workshop Design

**Status:** Approved for implementation

**SourceHead:** `04a53a955286bb926365459ee11c6836ae490282`

## Goal

Build an editor-only VN UI Workshop for safely tuning the verified VN presentation without changing production state, story flow, or runtime behavior. The Workshop provides a real-asset mirror preview, local preset overrides, direct manipulation, resolution previews, variants, and portable JSON import/export.

## Hard boundaries

- Create and work only on `feature/vn-ui-workshop`, based exactly on `04a53a955286bb926365459ee11c6836ae490282`.
- Do not modify `feature/vn-visual-polish-refs`, `integration/rokas-unified`, or `development`.
- Keep production `VnIntroView`, Controller, Presenter, Yarn content, and `RokasAssets` unchanged unless a real implementation blocker is proven.
- Put all Workshop implementation in the `Rokas.Editor` editor-only assembly.
- Do not integrate this feature as part of this task.

## Architecture

The Workshop uses an isolated editor-native mirror renderer rather than modifying or driving the runtime `VnIntroView`. The renderer reads the real `RokasAssets` textures/font and `VnCharacterVisualCatalog` UV states, then draws a faithful editor preview from an immutable verified baseline plus a separate override-only preset.

The verified baseline is code-defined and immutable. It contains the exact `SourceHead`, 1920x1080 logical reference space, approved layout constants needed by the Workshop, and no persistence API. A `VnPresentationWorkshopPreset` stores only values the user changed from baseline. Resolving a preview always means `baseline + overrides`; resetting an element removes that element's override rather than copying baseline values into the preset.

No Save, variant operation, or JSON export writes production assets or runtime source. There is intentionally no Apply-to-Production command in version one.

## Components

### `VnPresentationWorkshopBaseline`

Editor-only static model containing:

- `SourceHead = 04a53a955286bb926365459ee11c6836ae490282`;
- logical reference size 1920x1080;
- verified dialogue panel transform values;
- Mina body position/scale defaults;
- speaker-name and dialogue-text rectangles;
- Back/Next hit/visual rectangles;
- Mute/Pause/Skip hit regions;
- two-character spacing and active/inactive focus scale, brightness, and alpha values;
- resolution conversion matching the production `CanvasScaler.ScaleWithScreenSize`, `MatchWidthOrHeight`, match=0.5 model.

The baseline cannot be overwritten or serialized as a mutable preset.

### `VnPresentationWorkshopPreset`

Serializable editor-only override model. Each editable field is optional. An unset override resolves to the immutable baseline. The preset supports element-level reset and reset-all by clearing overrides.

Version one editable groups:

- Dialogue Panel
- Mina Body
- Speaker Name
- Dialogue Text
- Back
- Next
- Mute hit region
- Pause hit region
- Skip hit region
- Two-character focus values

Annotations are explicitly second priority and do not gate the first useful version.

### `VnPresentationWorkshopResolver`

Pure functions that combine baseline and preset overrides into one resolved preview state. It also converts between the 1920x1080 logical canvas and preview resolutions using the same logarithmic CanvasScaler scale-factor calculation used by the verified responsive tests.

### `VnPresentationWorkshopStorage`

Stores user variants outside production assets under `Library/ROKAS/VnUiWorkshop/variants/`. Variant files never enter `Assets/` and are therefore never part of player content. Variant names are sanitized to safe file names.

Portable export writes `ROKAS_VN_WORKSHOP_PRESET.json` through an explicit editor save dialog. Import validates schema/version, numeric finiteness/ranges, and `sourceHead`. A source-head mismatch is allowed only after an explicit warning/confirmation; it is never silent.

### `VnPresentationWorkshopWindow`

Menu path: `ROKAS/VN UI Workshop`.

Layout:

- left: preview state, variant selector/actions, resolution selector;
- center: large real-asset preview canvas with selection outlines and before/after toggle;
- right: friendly numeric inspector for the selected element and focus values.

The preview uses actual assets loaded from `Resources/RokasAssets` and authored UV states from `VnCharacterVisualCatalog`. No mock graphics are permitted.

## Preview semantics

The mirror preview uses 1920x1080 logical coordinates. The 1280x720 and 1024x768 modes are derived from the same production scaling model, never stored as independent layouts.

Supported visual states include the verified backgrounds/panels and Mina authored body UV. Keiko protagonist presentation remains text-only in the ordinary intro state; the two-character preview mode exists only to tune the already-supported focus constants and stage spacing.

Mute/Pause/Skip artwork is baked into the authored dialogue panel. The Workshop therefore labels those controls `Baked into panel` and exposes only their hit regions. It must not draw a fake independently movable mute/pause/skip icon. Back and Next are independent glyph elements and may be positioned/scaled separately.

## Direct manipulation

- Mouse drag changes the selected element position in logical 1920x1080 space.
- Arrow keys nudge by 1 logical pixel.
- Shift+arrow keys nudge by 10 logical pixels.
- Numeric fields edit position, size/scale, and relevant focus values directly.
- `Reset Element` clears only the selected element's overrides.
- `Reset All` clears the entire preset after confirmation.
- `Before/After` switches between immutable baseline and baseline+current overrides without mutating either.

Panel, Mina body, speaker-name rect, dialogue-text rect, Back/Next, and all control hit regions are directly selectable. Dragging rectangle elements moves them; resizing/scaling is done through numeric fields in version one to keep manipulation predictable.

## Variant and JSON rules

- Variant Save writes only the current override preset into `Library/ROKAS/VnUiWorkshop/variants/`.
- Load replaces the current override preset after validation.
- Duplicate creates a new local variant with the same overrides.
- Rename and delete operate only on local variant files.
- Export creates a standalone `ROKAS_VN_WORKSHOP_PRESET.json` containing schema version, source head, optional variant name, and override fields.
- Import never changes production code/assets.
- Baseline data is never written back by Save or Export.

## Validation and error handling

- Reject non-finite floats and invalid rectangle sizes/scales.
- Clamp/validate user-facing values to conservative editor-safe ranges before accepting them.
- Invalid or incompatible JSON is reported in a dialog and leaves the current preset unchanged.
- Source-head mismatch produces an explicit warning naming both heads.
- Missing `RokasAssets` prevents preview rendering and shows an actionable editor message; it does not create mock assets.

## Verification strategy

TDD is required for the model/storage/resolution behavior. Editor tests cover:

- exact immutable SourceHead and reference resolution;
- override-only resolution and reset semantics;
- 1280x720 and 1024x768 scale mapping;
- safe nudge/drag operations;
- focus override resolution;
- JSON round trip;
- rejection of invalid numeric data;
- source-head mismatch reporting;
- local variant paths staying under `Library/ROKAS/VnUiWorkshop/variants/`;
- explicit baked-control metadata for Mute/Pause/Skip.

Editor verification then checks the window can open and resolve the real ROKAS assets/catalog without touching runtime code. Existing permanent VN EditMode/PlayMode regression suites are run at the final feature HEAD. The Windows development-player and real-player runtime proof are also run at that exact HEAD to demonstrate that production behavior remains unchanged.

A final git diff against `04a53a955286bb926365459ee11c6836ae490282` must show no modifications to production `VnIntroView`, Controller, Presenter, Yarn files, or `RokasAssets`.

## Completion boundary

The task stops with:

`READY FOR USER WORKSHOP REVIEW`

`NOT INTEGRATED`

The user will then open the Workshop and tune the UI manually.