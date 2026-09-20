# ROKAS Visual Novel Intro — Design

## Goal

Implement the first working click-advanced visual-novel intro after the existing Main Menu `Enter World` action without replacing ROKAS' proven menu, audio, transition, Home, or Yarn Spinner architecture.

## Approved architecture

- Yarn Spinner v3.1.2 remains the only narrative engine.
- Add dedicated `VnIntroController`, `VnIntroDialoguePresenter`, and `VnIntroView` components. Do not reuse `MessagesDialoguePresenter` directly because it is coupled to `MessageService`, but follow its existing Yarn async presenter pattern.
- Preserve the existing `EnterWorldCurtain` transition and `EnterGame` audio path.
- Add replaceable temporary progress storage via `IVnIntroProgress` / `PlayerPrefsVnIntroProgress` with exact versioned key `rokas.vn_intro.completed.v1`.
- Wire all VN textures through the existing `RokasAssets` ScriptableObject.
- Import only the 10 unique PNG files from `Import_Recommended/`; never import `Source_All_Submissions/` duplicates.
- `VN_BusStop_PhoneMessage_Mina.png` is a complete full-frame story image and must not be reconstructed from separate phone/background layers.
- Keiko and Mina character sheets remain source sheets. Portrait states use cropped `RawImage.uvRect` regions; the full character sheet is never displayed as a dialogue portrait.

## Required asset set

Exactly these 10 PNG files are required before the asset-import task can pass:

1. `Characters/Keiko_CharacterSheet.png`
2. `Characters/Mina_CharacterSheet.png`
3. `Backgrounds/VN_BusStop_Rain_Night.png`
4. `Backgrounds/VN_NightSky_Rain.png`
5. `Scenes/VN_BusStop_PhoneMessage_Mina.png`
6. `UI/Dialogue/VN_DialoguePanel_Keiko_Dark.png`
7. `UI/Dialogue/VN_DialoguePanel_Mina_Light.png`
8. `UI/Controls/VN_Icon_Mute.png`
9. `UI/Controls/VN_Icon_Skip.png`
10. `UI/Controls/VN_Icon_Pause.png`

The implementation must fail closed if the complete recommended set is unavailable. Do not generate, rename, or substitute missing artwork.

## Story state machine

`VnIntroController` owns only the intro lifecycle. Its state is:

- `Inactive`: no VN is active.
- `Presenting`: Yarn is active and a beat may be waiting for one player advance.
- `Paused`: VN remains rendered but progression input is blocked. No `Time.timeScale` change is allowed.
- `Completing`: normal Yarn completion or Skip has entered the shared safe handoff. Repeated completion requests are ignored.
- `Completed`: existing Home was constructed successfully, the temporary completion flag was committed, and the VN has been disposed.

Mute is an independent boolean, not a state-machine branch.

### Input rules

- `Advance()` is accepted only in `Presenting` and only when `VnIntroDialoguePresenter` has a pending line/beat. One accepted call releases exactly one Yarn line gate.
- `TogglePause()` switches only `Presenting <-> Paused`. Pause never changes `Time.timeScale` and never pauses unrelated game systems globally.
- `ToggleMute()` calls a transient VN mute gate on the existing `RokasAudio`; it never edits `SettingsData.masterVolume`, `musicVolume`, or `sfxVolume`.
- `Skip()` is accepted from `Presenting` or `Paused` once and delegates to the same `CompleteIntroAndGoHome()` path used by natural Yarn completion.

## Yarn beat model

Use a dedicated VN Yarn project and node, separate from the Messages content but using the same Yarn Spinner package and `DialogueRunner` runtime.

The first node is `VnIntro_Start`. Visual changes are explicit Yarn commands handled by `VnIntroController`:

- `<<rokas_vn_background bus_stop>>`
- `<<rokas_vn_background night_sky>>`
- `<<rokas_vn_fullframe phone_mina>>`
- optional speaker/panel commands such as `<<rokas_vn_speaker keiko neutral>>` or `<<rokas_vn_speaker mina neutral>>` when a dialogue panel is shown.

Every authored Yarn line is gated by `VnIntroDialoguePresenter.RunLineAsync()` through a `YarnTaskCompletionSource`. Commands may update visuals, but progression cannot automatically cross the next line gate. This makes each click release one story beat instead of creating an autoplay cinematic.

Initial required flow:

1. Bus stop background appears; short Keiko establishing text may be shown.
2. One click advances to the night-sky background.
3. One click advances to the complete phone/Mina frame containing the required story intent `Я дома, приходи, нужно поговорить.`
4. One click reaches node completion.
5. Natural node completion calls `CompleteIntroAndGoHome()`.

## Enter World integration

The existing initial half of `RokasBootstrap.EnterWorldRoutine()` remains authoritative:

1. Guard repeated Enter World requests.
2. Play `EnterGame` once through the existing persistent `RokasAudio` owner.
3. Create `EnterWorldCurtain`.
4. Fade Main Menu to fully black.
5. Dispose Main Menu only under full black.
6. If `VnIntroProgress.IsCompleted` is true, build the existing Home and run the current fade-in path.
7. Otherwise create the VN under full black, then fade the same curtain out to the VN.

No Home objects are constructed behind the VN intro.

## Shared safe handoff

Natural Yarn completion and Skip must call the exact same idempotent `CompleteIntroAndGoHome()` method.

The safe order is:

1. Enter `Completing`; reject Advance/Pause/Skip re-entry.
2. Fade the existing curtain back to fully black using unscaled time.
3. Restore transient VN mute to `false` without modifying saved settings.
4. Dispose/stop the VN dialogue and view.
5. Set `startupPending = false` and call the existing `BuildPresentation()` exactly once.
6. Confirm `View != null`. This is the success acknowledgement for the handoff.
7. Only after step 6 call `VnIntroProgress.MarkCompleted()` (`rokas.vn_intro.completed.v1 = 1`).
8. Fade the curtain from black to the existing Home.
9. Destroy the curtain and mark controller lifecycle complete.

The flag is never set by `Skip()` itself and never set before `View != null` confirms Home construction.

## Failure semantics

- Missing Yarn project/node, missing required VN assets, or presenter startup errors must be surfaced as diagnostics; they must not silently mark the intro complete.
- Any failure before successful Home construction leaves `rokas.vn_intro.completed.v1` unset.
- `CompleteIntroAndGoHome()` is guarded so double Yarn completion, double Skip, or completion plus Skip cannot build Home twice.
- Transient VN mute is restored in cleanup/failure paths.
- No `Time.timeScale` writes are permitted.
- The permanent `SaveData` schema is untouched in this iteration.

## VN view ownership

`VnIntroView` creates one 1920x1080 authored stage using the same `UiKit`/Canvas conventions as the rest of ROKAS.

Layers, back to front:

1. background/full-frame `RawImage` fitted to authored stage;
2. optional character portrait region;
3. supplied transparent dialogue panel texture;
4. speaker name and dialogue text;
5. supplied Mute / Skip / Pause icon buttons.

The phone/Mina beat hides portrait/dialogue composition if it would obscure the supplied complete frame; it renders the scene texture itself as the dominant full-frame image.

Keiko uses `VN_DialoguePanel_Keiko_Dark.png`; Mina uses `VN_DialoguePanel_Mina_Light.png`.

## Audio ownership

Add a transient VN mute gate to the existing `RokasAudio` rather than introducing a second audio framework. `SetVnMuted(bool)` temporarily drives the existing ROKAS audio outputs to silence while active and restores output from the unchanged `SettingsData` values when disabled. VN teardown always clears this transient gate.

## Test strategy

TDD is mandatory for code changes.

Focused tests must prove:

- PlayerPrefs key is exactly versioned and remains unset until explicit successful completion.
- VN Yarn project imports and contains `VnIntro_Start`.
- each `Advance()` releases exactly one pending line/beat;
- Pause blocks `Advance()` and does not change `Time.timeScale`;
- Mute leaves saved settings unchanged and is restored on exit;
- Skip and natural completion converge on one guarded Home handoff;
- first-time Enter World reaches bus stop -> night sky -> full phone frame -> Home;
- completed flag bypasses VN and preserves the existing direct full-black-to-Home behavior;
- Home is built exactly once;
- Main Menu is not removed before full black;
- `EnterGame` still plays once and generic click is not stacked;
- all 10 supplied PNG references are present and valid;
- panels/icons retain alpha-capable textures and production layout;
- no unrelated Unity-generated files are committed.

Final proof requires focused VN tests, existing Main Menu/Story regressions, broader Unity CI, runtime/visual evidence for the VN frames, diff review, and a verified checkpoint. `development` must remain untouched and integration must not be advanced automatically.