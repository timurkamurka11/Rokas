# ROKAS VN visual polish design

## Scope

Polish the existing Yarn-driven VN intro presentation without replacing or widening the narrative/save architecture. The canonical story flow remains `bus_stop -> night_sky -> phone -> Home handoff` and the existing one-click-one-beat presenter gate, Skip, Pause, Mute, persistence, debug replay/reset and safe Home handoff remain authoritative.

Primary visual/behavioural source is the user-provided `ROKAS_VN_VISUAL_POLISH_PACKAGE` (`MASTER_PROMPT_SOL.txt`, six target PNGs and three MP4 references).

## UI composition

- Keep the authored scene/background textures.
- Recompose the VN overlay around a wide bottom dialogue panel matching the target PNG proportions.
- Put a large circular/cropped speaker portrait at the lower-left edge of the panel.
- Place speaker/name and dialogue copy inside safe text bounds with dark/light panel contrast preserved.
- Present five aligned controls: Mute, Pause, Skip, Back, Next.
- `Back` is visible but disabled/non-interactive. No Yarn rewind is introduced.
- `Next` calls the same existing `Continue()` callback used by the story click surface, preserving exactly one beat per successful continuation.
- Layout must remain inside safe bounds at 1280x720 and 1024x768.

## Character presentation

The view supports zero, one or two staged characters without changing story progression. Existing character sheets are inspected before defining any UV/state. Only states actually authored in the sheets may be mapped. Unknown expression/state tokens must fall back safely to an authored neutral state rather than inventing UVs.

Home target PNGs are composition/style references for full-body placement only. They do not create a new story beat.

## Presentation animation

Animation is presentation-only and driven by unscaled presentation time:

- natural authored-state blink when a real blink state exists;
- subtle breathing/bob;
- short expression crossfade where authored states permit it;
- active-speaker focus/pulse: active character approximately 1.05 scale/full brightness/front, inactive approximately 0.94 scale/slightly dimmed, with about 0.18 s smoothing.

Pause freezes these VN presentation animations locally and does not change global `Time.timeScale`. No presentation animation may call Continue, Skip, complete Yarn content, or mutate persistence.

## Architecture boundaries

- Keep `VnIntroController` and `VnIntroDialoguePresenter` semantics unchanged unless a failing test proves a narrow change is necessary.
- Keep Yarn as the source of beat/background/speaker/portrait-state information.
- Reuse the existing `PortraitId` visual token rather than adding a parallel narrative engine.
- Do not add autoplay or rewind.
- Do not add a Home Yarn beat.

## Verification

Use TDD for the presentation surface and helper logic. Verify focused VN tests, existing flow regressions, full EditMode/PlayMode policy, a Windows Development Player and real `Rokas.exe` runtime scenarios. Capture and visually inspect runtime screenshots at both 1280x720 and 1024x768 against the supplied PNG/MP4 references. Temporary inspection/test workflows are removed before the final clean verification. No integration is performed automatically.
