# ROKAS — real bed hotspot icon, transition and user supplied watch/arm sound

Continuation of the same draft **PR #13**; don't merge before Unity real scene visual/audio QA.

## User-provided source assets

- Original supplied transparent 1254×1254 PNG gold neon sleeping-in-bed sign, distributed byte-for-byte as `Assets/Resources/BedSleep/UI/BedSleep_Icon.png`. SHA256 `d01d014bcea700d8c91f12c63f386563a1e3b0ee6aebfe358b22cdb04d76ccb7`. No AI remake, text overlay or generic placeholder.
- `Одиночный звук завода часов.mp3` → `Assets/Resources/BedSleep/Audio/Watch_Wheel_Single.wav`, SHA256 `43529e6a5549de994d191229df242fa8eef5a1b46fb33ed4019f69abb4200434`, user recording MP3 decoded to 44.1 kHz mono PCM. **Only** crown/dial steps.
- `Button-click-sound.zip/Leven watch sound.mp3` → `Assets/Resources/BedSleep/Audio/Watch_Button_Click.wav`, SHA256 `95b0c64dd1f294a9f78687e5519f600c702272a3602f8a71c3fbfa1881962319`. Only physical wristwatch button. Supplied archive's `Read.txt` states Orange Free Sounds, Creative Commons Attribution 4.0 International; attribution must be kept in credits, `Assets/Resources/BedSleep/Audio/README_Attribution.txt`.
- `Sound of up arm.wav` → `Assets/Resources/BedSleep/Audio/Arm_Raise_Cloth.wav` (original rustle with +12 dB gain; no newly generated sound), SHA256 `09f94f828291d08239e3e42464ab7cc17bf7bdc7bdecc61e5f01fdf5a0fdc83d`. Once on arm state transition to Raise, **not** during idle/small finger movement.

All four non-code assets and credits are distributed as part of the **single BAT ZIP** `ROKAS_BEDSLEEP_UI_ICON_AUDIO_FADE_D.zip`. The binary assets are NOT claimed committed to GitHub: PR #13 stores the exact C# references and this integration checklist. Installer places the real bytes into the original Unity working copy under D:, verifying each SHA256 and making per-file backups. Do not run Blender or reinstall GLB.

## Actual code changes, same PR #13

1. `Assets/Rokas/Scripts/Presentation/HomeView.cs`: keeps original `BedInteractable_Hotspot` Button and click gameplay; removes displayed plain `ЛЕЧЬ СПАТЬ` text when original icon exists; attaches the exact transparent PNG as centered, non-raycasting UI Image child and a gentle `HomeBedSleepIconFeedback` hover/pressed/keyboard selected interaction animation (1.065x max hover scale, gold authored pixels never repainted). Text fallback if missing resource, no action loss.
2. `Assets/Rokas/Scripts/Presentation/RokasBedSleepCinematic.cs`: the initial fade to ceiling is 0.96 sec, 0.10 sec black hold, reveal 1.08 sec, quintic eased with zero slope/acceleration at boundaries; no scene or gameplay state changes. The user wheel click is played via existing UI Step and dial handlers, debounced; shared V10 native/3D interaction retains its original gameplay ownership.
3. `Assets/Rokas/BedSleep/V10Release/Scripts/BedSleepSFXController.cs`: user Resources clips take precedence over generated fallback mechanical audio in the existing SFX controller; `PlayCrownClick`, `PlayButtonMechanical` and `PlaySleeveRaise` retain all previous triggers; optional recordings missing => original fallback, no fake silent placeholders. Original V16/FBX, watch case/strap, Russian OLED, Hand Studio, scene, save and PR not merged.

## QA gate

Local Unity run after installer: Hub icon is the authored golden circle, hover breath/scale, keyboard highlight, existing bed press works once, dark 0.96/black .10/reveal 1.08 without flash, soft cloth rustle only upon arm raise, exactly one wheel-detent recording when rotating, mechanical click when pressing physical side button, no duplicated clicks or sound overlap. Hand Studio calibration and the newly attached original 23-joint hand unaffected. No cloud real Unity audio/game capture available, so **visual/audio PASS is not claimed**.
