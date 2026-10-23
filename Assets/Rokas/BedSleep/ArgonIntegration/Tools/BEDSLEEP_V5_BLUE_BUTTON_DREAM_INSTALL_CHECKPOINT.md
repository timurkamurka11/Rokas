# ROKAS Bed Sleep V5 — checkpoint (2026-10-24)

Continuation of the SAME draft PR #13. **Do not merge**. Keep source hand V16 safe, the user's animated GLB→FBX hand, real original smartwatch with Russian OLED and Hand Studio unchanged.

## User's actual Game View report
Screenshot showed a dark/blue rectangular background behind the gold `НАСТРОИТЬ ВРЕМЯ` button with unnecessary overlaid `НАЖМИ ЕЩЁ РАЗ — ВЕРНУТЬСЯ К РУКЕ`; user also reported the promised Keiko sleep loop, dark-room music fade, curved/blurry waking and exit blackout absent (only buttons appeared).

### Confirmed root cause of the button defect
`RokasBedSleepCinematic.cs` was attaching **both** `RokasBedWatchFocusButton` (legacy feedback component which forcibly writes *opaque cyan/blue* tint to button's Image every Update) AND `RokasBedAuthoredActionButton` (new original gold PNG). It also explicitly calls `SetReturnToHand(watchFocus)`, which creates the unwanted in-game caption. This is a code defect, not a problem with the PNG.

### Fixes committed
- `RokasBedSleepCinematic.cs`: REMOVE legacy focus component from the authored button; no call to `SetReturnToHand`; always keep the original gold PNG and hover effect with **the same ToggleWatchFocus action**, not an extra UI button.
- `RokasBedAuthoredActionButton.cs`: removed obsolete caption code.
- `RokasBedSleepCinematic.cs`: added **fade to black when leaving bed normally OR with ESC**, then a separate overlaid black fade-out to reveal the restored hub after the bed presentation root is deleted (prevents a single-frame scene flash). Existing Sit On Bed foley remains.
- `RokasBedSleepCinematic.cs`: Game Console emits explicit `[ROKAS DREAM V5]` when the actual silent StreamingAssets video is/was prepared, and logs missing/failed media. Existing silent `RokasKeikoDreamLoop`, music fade through `RokasAudio`, blurred curved eyelid animation and 18s dream exposure remain as in V4.
- V4 only-button symptom could mean **V3 installation completed and V4 installation stopped** (V4 previous BAT ran two installers in series); screenshot alone cannot prove the exact failing phase. V5 **does not rerun V3**. Its post-install assertions require an actual video file and code/music wiring, not merely presence of new buttons.

## Delivered full guarded single-BAT install package
`ROKAS_BEDSLEEP_V5_FULL_FIX_D.zip` (5,769,948 bytes), SHA256 **`e61987a78ea992d5915ffa7ccf5e03158e3e32859885623487e981cd55a8400d`**. Includes:
- *Original user* 2 button PNGs, sleep icon and all 3 sleep/watch/sleeve recorded sounds/credits from existing V4 bundle, unchanged.
- Seam-crossfaded muted MP4 video `KeikoSleepLoop.mp4` (SHA256 `2985b9d58922c27e65f2328da9c605dff11b9f37d38cedec39440910d50616c7`) installed at `Assets/StreamingAssets/BedSleep/KeikoSleepLoop.mp4`.
- One BAT and PowerShell script pinned to [commit 23c32628](https://github.com/timurkamurka11/Rokas/commit/23c32628d6e2de1abe15b4021644656e00a4b3ff); verify exact source Git blob checksums for Cinematic/helper/button; make scoped idempotent music/View C# patch; backup+rollback. Never run Blender, replace FBX/watch/V16, edit actual scene files or switch Git branch. If unknown local Cinematic edits exist, attempt only an explicit 3-way `git merge-file` from known V3 ancestor; on conflict halt and preserve user's original.
- Requires Unity.exe closed; target existing `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`; post-install asserts complete dream/video, curved eyes, 0/1 music duck targets, no blue component and no extra caption and exit fade. Presence of original PNG buttons alone is no longer enough.
- ZIP integrity and expected SHA and pinned GH source SHAs verified **statically**. The BAT and Unity C# have not been executed on user's Windows Unity, so **no compilation or Game View PASS**.

## Real acceptance checks
After BAT reports all stages PASS, open original ROKAS via Unity Hub, enter bed and watch: plain amber "Настроить время" PNG hover and watch-focus toggle, no caption or cyan backdrop; confirm sleep, main music fades to silence, video visibly plays/loops for 18s while sleeping without its source audio; wake beep while oval, feathered, slightly blurred eyelids open; Sit On Bed sound and black fade during standing, gentle hub reveal. Pressing ESC while in bed should also dark-fade and restore hub/music. Record Game View & Unity Console if anything fails. **PR stays draft.**
