# ROKAS — Laptop LCD Calibration Workshop V1.0

## Purpose

An **actual Unity EditorWindow**, not a mockup or web-only preview. The user can drag the four ORIGINAL laptop LCD corners onto the source POV image and save a JSON preset for exact later production integration. Changes are **export-only**. Do not edit the live YOMI screen, background, power choreography, Golden buttons, combat/VN, source image or existing calibration.

Open in Unity Editor via **ROKAS → Laptop → Screen Calibration Workshop**. You do not have to run the game or enter Play Mode. Suggested floating window size: 1300×850 or larger.

## Steps (user)

1. Ensure original source texture `LaptopPOV_screen_off_APPROVED_CINEMATIC_DOF` is imported in the Unity project. Window finds it automatically by asset name. If it does not, select the exact texture in the **REFERENCE PHOTO** ObjectField. Do not use a cropped screenshot or a screenshot containing the gold buttons: coordinates must correspond to the original 1672×941 POV pixels.
2. The initial four corners are seeded from the current V11.8 calibration (base corners plus current left/top/right/bottom safe insets). This is only a starting position, not a new production change.
3. Zoom in using mouse wheel or the Zoom slider. Use middle mouse or Alt+left-drag to pan. Move the four colored handles **TL → TR → BR → BL** to the actual **inner** LCD borders, *not* to the outer metal bezel. The image stays still as handles are moved.
4. Use `YomiPreview` (default checker grid with clearly marked outer edges; or choose an actual captured YOMI PNG with **Choose captured YOMI PNG...**), `BlackScreen`, `Mask`, or `Outline`. Toggle grid/outline, adjust preview opacity. Drag until there are **no black gutters or overhangs**, even at corners.
5. For pixel-perfect precision, click one corner and use arrow keys for 1 source-image pixel movements (Shift+arrows = 10 px). Or type X and Y on the right.
6. Click **COPY CORNERS JSON** to copy directly, or **SAVE / EXPORT JSON...** to save `ROKAS_Laptop_LCD_Calibration.json` wherever you prefer. Load via **LOAD JSON...** to resume. Reset to current V11.8 starts over without touching the runtime.
7. Send the JSON or a screenshot with the four corner coordinates back to ChatGPT. The final integration will apply the four **inner final pixels** directly to **all** powered/off LCD layers. **Do not add the old runtime insets again.**

## Technical contract

- Menu: `ROKAS/Laptop/Screen Calibration Workshop`.
- Single editor-only script: `Assets/Rokas/Editor/LaptopScreenCalibrationWorkshop.cs` with .meta GUID `497bcf6e04e34dd99e9e50916876c12a`.
- Export schema is flat and preserves the original runtime field naming for ease of integration: `topLeftX`, `topLeftY`, `topRightX`, `topRightY`, `bottomRightX`, `bottomRightY`, `bottomLeftX`, `bottomLeftY`, `powerX`, `powerY`, plus `formatVersion:1`, `coordinateSpace:"source_image_pixels_top_left"`, `sourceWidth` and `sourceHeight`. These are **final polygon vertices** (after whatever manual inset is needed), unlike legacy runtime *outer* `screen_calibration` fields that acquire another automatic inset.
- Validation prevents self-crossing/non-convex or out-of-image four-corner polygons and very small/degenerated polygons.
- Preview uses a **two-triangle UV texture mesh**, just like runtime `LaptopPerspectiveQuad`. The source art is never re-encoded.
- Save uses `EditorUtility.SaveFilePanel`, copying uses `GUIUtility.systemCopyBuffer`, loading validates coordinate-space and source dimensions. JSON exports to a user-chosen path; **does not overwrite or modify** `Assets/Rokas/Resources/LaptopCinematic/screen_calibration.json` or runtime source code.
- The optional external YOMI PNG is read into a temporary editor-only texture and is never imported into the game or committed.

## CI / setup caveat

Existing Codex Combat WIP in the branch has previously failed Unity compilation with missing `CombatShotBounds` and `CombatShotBoundsProjection` (`ReactiveCombatArena.cs`), independent of the workshop. Unity Editor must compile the scripts to load a newly added EditorWindow; **the window may not appear until that compile blocker is fixed**. The workshop itself makes no modifications to combat or the compiler environment.

The master calibration image may live in the user's **local Unity Resources and not in GitHub**; the workshop intentionally loads the user's original local asset, rather than shipping a blurry screenshot.
