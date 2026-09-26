# ROKAS Main Room Overlay Workshop

Editor-only authoring workspace for manually matching the supplied Main Room scan-mode reference.

## First use

1. Download and extract `ROKAS_MAIN_ROOM_WORKSHOP_ASSETS.zip`.
2. In Unity choose **ROKAS → Main Room Overlay Workshop → Import Provided Icons...** and select the extracted folder.
3. Choose **ROKAS → Main Room Overlay Workshop → Open or Create**.
4. Unity creates `Assets/Rokas/EditorWorkshops/MainRoomOverlayWorkshop.unity` only if it does not already exist.
5. Under `ReferenceHelpers`, enable `ReferenceOracle` when you want the semi-transparent oracle directly over the room.
6. Save manual edits normally with **Ctrl+S**. Reopening the workshop does **not** regenerate or reset it.

## Editable hierarchy

- `Background` — existing room artwork.
- `ReferenceHelpers/ReferenceOracle` — optional semi-transparent reference image imported from the supplied ZIP.
- `Header` — title and accent line.
- `Hotspots` — eight independent Image objects using the provided icons. Move/scale them with RectTransform; swap Sprite in Inspector if desired.
- `Connectors` — eight independent dotted connector graphics. Move/resize/rotate their RectTransform; tune dot size/spacing/glow in Inspector.
- `Outlines` — eight independent editable path graphics. Select an outline and drag its amber point handles directly in Scene View; core/halo/glow thickness and colors are Inspector fields.

## Handing the approved layout back

When the layout looks correct:

1. Save the scene with **Ctrl+S**.
2. Choose **ROKAS → Main Room Overlay Workshop → Export Layout JSON...**.
3. Save `ROKAS_MAIN_ROOM_OVERLAY_LAYOUT.json`.
4. Send that JSON back for the final runtime integration pass.

The JSON preserves the editable RectTransform layout plus outline/connector serialized settings, icon assignments and basic header styling. Use **Import Layout JSON...** to restore a previously exported layout.

The workshop scene is intentionally not added to Build Settings and does not change the current runtime Home implementation. Final runtime integration happens only after manual visual approval.
