# ROKAS Main Room Overlay Workshop

Editor-only authoring workspace for manually matching the supplied Main Room scan-mode reference.

## First use

1. Download and extract `ROKAS_MAIN_ROOM_WORKSHOP_ASSETS.zip`.
2. In Unity choose **ROKAS → Main Room Overlay Workshop → Import Provided Icons...** and select the extracted folder.
3. Choose **ROKAS → Main Room Overlay Workshop → Open or Create**.
4. Unity creates `Assets/Rokas/EditorWorkshops/MainRoomOverlayWorkshop.unity` only if it does not already exist.
5. Save manual edits normally with **Ctrl+S**. Reopening the workshop does **not** regenerate or reset it.

## Editable hierarchy

- `Background` — existing room artwork.
- `ReferenceHelpers/ReferenceOracle` — optional semi-transparent reference image; enable it in Hierarchy after importing the ZIP.
- `Header` — title and accent line.
- `Hotspots` — eight independent Image objects using the provided icons.
- `Connectors` — eight independent dotted connector graphics. Move/resize their RectTransform; tune dot size/spacing/glow in Inspector.
- `Outlines` — eight independent editable path graphics. Select an outline and drag its amber point handles directly in Scene View; thickness/glow/colors are Inspector fields.

The workshop scene is intentionally not added to Build Settings and does not change the current runtime Home implementation. Final runtime integration should happen only after manual visual approval.
