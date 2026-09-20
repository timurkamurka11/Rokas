# ROKAS VN Authoring Polish Design

**Authority:** user-supplied continuation contract dated 2026-09-20.

## Baseline

- Branch: `feature/vn-scene-composer`
- Recovered HEAD: `441180a45d701f3c9a1b3198f6f13549103b117b`
- Recovered TREE: `e6a8a7b0df33ad7856de97373403311ef4a18b29`
- Last automated GREEN run: `35530710512` (#350)
- Real authoring project: `3cc2bc9ec7974ea7a5f4d074683bed61`
- Unity: `6000.3.19f1`

Do not restart or reimplement accepted M-TEXT, M-SFX, primary BGM, video reliability, text geometry, plaque geometry, asset migration, or character staging. Final Certification remains blocked until manual PASS.

## A. Speaker color inside one Scene

Extend speaker-name color authoring from two levels to three:

1. `Ко всем сценам`
2. `Только к этой сцене`
3. `Этому говорящему в этой сцене`

Resolution order is:

`Scene + Speaker override -> Scene default -> Global default`.

The Scene+Speaker override is strictly local to one Scene. Prefer the Beat's stable Character ID when present; otherwise use the authored speaker string trimmed at its boundaries. Text-only speakers work with `Персонаж реплики = Без персонажа`. Empty/narrator speaker does not create a local speaker entry.

Only RGB speaker color is speaker-specific. Speaker font, size, alignment, fallback, opacity, text geometry, and plaque geometry remain governed by their existing shared contracts.

The Scene model owns an independent serializable map/list of local speaker-color overrides. Scene duplication deep-copies it. Undo, Save/Reopen, Preview and Play resolve through the same resolver.

## B. Smooth dialogue reveal

Keep the existing `Появление текста` and `Скорость текста` authoring settings. Replace substring-driven rendering with a layout-stable reveal model:

- the full authored dialogue string is retained for layout from the first frame;
- rich-text markup is not counted as visible glyphs;
- visible units are Unicode text elements/grapheme-safe units;
- visibility progresses from elapsed time, not rendered frames;
- newly exposed glyphs receive a short subtle fade window;
- hidden glyphs remain layout participants, preventing rewrap/jitter;
- speaker name remains immediate;
- normal advance while reveal is incomplete completes the current line only;
- the following advance moves to the next Beat;
- stale reveal state is reset on Beat/Scene/session navigation;
- force-complete leaves all glyphs fully opaque.

The editor preview currently renders through IMGUI rather than a live TMP component, so the implementation will expose stable full-text reveal metadata from playback and render a rich-text alpha-masked full string. This preserves the same final layout without allocating a shrinking substring each frame. The reveal sampler remains independent of video, audio, character staging and scene transitions.

## C. Smooth audio transitions

Reuse existing Fade In/Fade Out semantics.

Primary BGM:
- Keep Previous preserves the same live source and playback position.
- Track A -> Track B uses a controlled two-source crossfade.
- Track -> Silence fades the outgoing source before release.
- Silence -> Track uses incoming Fade In.
- Explicit zero fade stays instant.
- no accumulating/ghost BGM sources.

Layered additional audio:
- inherited active cues preserve the same AudioSource and elapsed position;
- non-inherited active cues use their authored Fade Out on Scene exit;
- Beat-stop cues use authored Fade Out;
- new cues use authored Fade In;
- one-shots are not retriggered unnecessarily;
- navigation and replay cannot accumulate sources.

All fades are elapsed-time based and remain independent from video, character staging, dialogue reveal and scene-curtain timing.

## Compatibility and safety

- Existing projects load without visual changes.
- Serialized field additions use zero/default values compatible with historical JSON.
- Existing geometry contracts are untouched.
- Existing migration remains the review handoff authority.
- No worktree/reset/clean/gc/repack/prune in the review BAT.
- The review BAT is derived from the established CLASSIC_FIXED template and preserves project/assets/.meta/GUIDs.

## Acceptance

Automated RED/GREEN coverage must include the speaker-color, dialogue-reveal and audio matrices from the user contract, then the current affected regression chain with actual counts.

After automated GREEN create `ROKAS_VN_POLISH_REVIEW_<SHORT_SHA>.bat` and stop at:

`M-AUTHORING POLISH REVISION = AUTOMATED GREEN / MANUAL OPEN`

Do not run Final Certification before explicit manual PASS for all three polish areas.
