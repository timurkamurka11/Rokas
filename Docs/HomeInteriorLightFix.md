# Home interior Light ON/OFF targeted fix

## BASE

Branch: integration/rokas-unified. Fetched local/remote base: `2c8657e1b4ed9237f89d4995fb2b6dabae2965ad`. Tree: `c0003c877acc637deff0f58dabc94bbb6f0a3214`. Implementation branch: fix/home-unified-light-toggle. Unity: 6000.3.19f1. Original tracked ProjectVersion and package manifest are unchanged.

## RCA

Type: **mixed**. The old constant internal light is baked into `Assets/Rokas/Art/Home/ApartmentNight.png`, referenced by RokasAssets.home and rendered as AuthoredStage/WorldIllustration. Direct asset inspection shows the bright floor lamp, workbench lamp, hallway and warm illumination of furniture, bedding and floor without any runtime overlays.

The button previously controlled only WorldEffects' HomeWarmInteriorGlow, using WarmPracticalMask. Its OFF value remained nonzero, and lightning added another small warm contribution. The base room image and sampled foreground frame artwork were unaffected.

Exact flow: LampHotspot → GameSession.SetLamp → SaveData.lampOn → RokasView.Tick → WorldEffects.Tick/ApplyLighting. The existing button action saves immediately; lampOn already survives profile reload. See [pre-fix RCA](Verification/HomeInteriorLight/rca.md) and the three expected failures in [RED XML](Verification/HomeInteriorLight/red.xml).

## FIX

- WorldEffects selects a separate authored OFF room representation and matching foreground frame slices from the existing lampOn input while at Home. Portal and Combat continue using their own original backgrounds.
- Added `Assets/Rokas/Resources/Home/ApartmentNightLightOff.png` and its unique importer metadata. The original ON artwork is retained byte-for-byte. Both sources are 1672×941; Unity uses the existing 1920×1080 authored stage.
- OFF warm mask alpha is exactly zero. ON retains the established warm practical contribution. Lightning is carried by the existing cold/storm masks, without energizing the practical layer.
- Three regressions in HomeWeather3PlayModeTests cover the real button, artwork, weather independence, Messages/Combat return, object reuse, and save/reload. Optional test-only captures record actual runtime canvases and the rendered hierarchy.

No new light-state boolean, fade system, save format, weather camera/RenderTexture/particle lifecycle, full-stage dark card, or unrelated subsystem change was introduced. The original exterior window source is intentionally retained, including its **outdoor** balcony lantern. Warm indoor lamps and their reflected illumination switch off together.

## TESTS

Fresh verification evidence is indexed in [results.json](Verification/HomeInteriorLight/results.json):

- Original Home/Weather baseline: 10/10 PlayMode PASS.
- TDD RED: 3 expected new failures, 5 existing Weather tests PASS.
- Home/Weather plus three new regressions: 13/13 PlayMode PASS.
- Real first-loop, Messages lifecycle and critical resources: 9/9 PlayMode PASS; includes manual Combat, return, exactly-once payment and reload.
- Unity JSON save/backup boundary: 2/2 EditMode PASS.
- First-loop domain guards, reward idempotence and combat reload: 3/3 EditMode PASS.

The user's task-specific focused-test requirement supersedes the permanent full-matrix milestone default. Core/Combat/Message/save production sources are unchanged; no unrelated historical Combat 23-case matrix was rerun. Existing integrated first-loop and focused domain/save boundaries verify the affected location transition responsibilities.

Validator: **23 existing, 0 new, 0 removed**. The unchanged approved Weather 3 base already adds ten below-HD weather textures to the historic 13 findings. Both actual baseline and final lists are recorded; validation rules were not modified to change the count.

Relevant compile/runtime log scan: no C# compile errors, NullReference, missing-reference/component, shader/RenderTexture creation, unhandled exception or assertion failures in the successful runs. Raw Unity logs remain in `D:/Rokas/home-light-evidence`. Literal git diff --check and staged checks must be exit 0 and empty before checkpoint publication.

## VISUAL PROOF

All five actual Unity 1920×1080 captures were inspected by the implementer and an independent reviewer:

- `D:/Rokas/home-light-evidence/green/light-on-normal.png`
- `D:/Rokas/home-light-evidence/green/light-off-normal.png`
- `D:/Rokas/home-light-evidence/green/light-on-lightning.png`
- `D:/Rokas/home-light-evidence/green/light-off-lightning.png`
- `D:/Rokas/home-light-evidence/green/light-off-idle.png`

Comparison sheets: `D:/Rokas/home-light-evidence/comparisons.html` contains ON | OFF normal rain and OFF normal | OFF lightning, with original PNG links. Runtime screenshots are not generated or retouched. Only the separate OFF source artwork was created through built-in Imagegen.

Old warm internal light while OFF: **NO**. Dark/bright/light-off rectangles, hard glazing border or weather bleed introduced: **NO**. Window/cold exterior, rain and lightning while OFF: **YES**. OFF remains readable and returns to OFF after the transient flash. Source/capture SHA-256 values are recorded in [art.json](Verification/HomeInteriorLight/art.json) and results.json.

## REVIEW

Independent review: **APPROVE**, Critical 0, Important 0, Minor 0. See [review record](Verification/HomeInteriorLight/review.md). The only handoff observation concerned line-ending warnings; edited files were normalized before final empty-output diff checks.

## CHECKPOINT AND CANONICAL HANDOFF

After all gates are green, create `VERIFIED HOME UNIFIED INTERIOR LIGHT ON-OFF FIX CHECKPOINT`, fast-forward the remote integration/rokas-unified only, verify its exact SHA, and safely fast-forward D:/Rokas/Rokas after its Editor is closed and its local changes/collisions are checked. No reset, history rewrite, force push, development update, PR #4 merge, or worktree cleanup is permitted.

Exact fix/checkpoint/tree, remote before/after, canonical update and preservation results are recorded after publication in `D:/Rokas/home-light-evidence/final-handoff.json` and the task handoff. This pre-publication record does not claim those subsequent operations have already happened.

## MANUAL QA

Use only D:/Rokas/Rokas, Unity 6000.3.19f1, Assets/Rokas/Scenes/Rokas.unity, after the final handoff confirms that folder is updated. Open Home and press Свет: ON → OFF → ON; inspect the internal lamps, cool window, rain, and a subsequent lightning event. Enter Messages and return; leave for Combat and return; save/reopen. The previously selected lamp state should remain coherent. Stop after this milestone.
