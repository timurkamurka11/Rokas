# ROKAS Laptop Cinematic V11.2 — main GitHub integration

Status: **Original chibi + four gold PNGs correctly imported, strict art-integrated Unity PlayMode PASS and focused Unity EditMode/PlayMode PASS. Real QHD-room visual acceptance remains user-local.**
Branch: `codex/fidelity3-resume-5779190`.
**Verified runtime + exact PNG importer fix SHA:** `f37c7175ba7e09735596c1d2a5026375948799dc`. (Previous `d00b22f` checks were insufficient for PNG import, as noted below.)
Previous tested V11.1 base: `7ffa90bb76195d6f12f9e2037fd9e285ac7775ac`.

## Original assets uploaded to GitHub, unchanged

A GitHub Actions [exact-asset import SUCCESS](https://github.com/timurkamurka11/Rokas/actions/runs/38094082016) downloaded the user's five original PNGs from conversation and committed exact PNG bytes without retouching, recoloring, copying screenshots or replacing fonts:

- `Assets/Rokas/Resources/LaptopCinematic/UI/rokas_no_signal_chibi.png` (1,948,744 bytes; exact original attached image(20261010-223111).png).
- `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_open.png` (1,094,468 bytes; original gold E/open laptop).
- `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_back.png` (491,507 bytes; original gold ESC/back).
- `Assets/Rokas/Resources/LaptopCinematic/UI/prompt_power.png` (996,335 bytes; original gold E/power).
- `Assets/Rokas/Resources/LaptopCinematic/UI/back_arrow.png` (463,899 bytes; original gold left arrow).

Stable Unity meta GUIDs are committed. `LaptopCinematicTextureImporter.cs` preserves full NPOT PNG size, uncompressed RGBA transparent artwork and preserves alpha on UI, without altering original art.

## Runtime (scoped changes only)

- **No Signal before Power:** Real original small chibi displayed over opaque dark panel inset from the approved physical LCD bounds, with `RectMask2D`; `LaptopNoSignalBounce` moves diagonally in local LCD pixel space and reflects off boundaries. Falls back to text NO SIGNAL ONLY if exact resource is missing.
- **Power / mini YOMI:** `PhysicalMiniYomiVisible` is a direct uGUI RawImage using the existing runtime `LaptopPhysicalDesktopClock.mainTexture`, rendered over a masked dark physical LCD area rather than depending exclusively on an invisible custom quad. Mini YOMI has the exact Tokyo night wallpaper sourced from `RokasAssets.laptopWallpaper`, nine same vector `LaptopIcon` glyphs as fullscreen YOMI plus the same Russian application titles, live OS clock and unread status. In-world app buttons remain passive; fullscreen YOMI opens only via E/click. Session power state remains until app restart, no second boot.
- **PNG prompts:** Original gold `prompt_power`, `prompt_back`, `prompt_open` and `back_arrow` loaded directly from Resources; fully white tint, original transparency + glow, existing hover/pulse/press preserved, increased displayed width and moved to fit within frame. `prompt_open_yomi` resource is supported if a future exact authored PNG exists; otherwise the original user's `prompt_open` is used for the powered action (no flat placeholder).
- Original 49 right-hand frames, Blender source, camera speed, physical Power key timing, audio gains and Combat/VN scripts **NOT modified**.

## Earlier CI evidence (d00b22f): does not establish original PNG import

- [Unity compile, EditMode, baseline PlayMode, five V11 PlayMode cases: **PASS**](https://github.com/timurkamurka11/Rokas/actions/runs/38094323055).
- [Unity PlayMode with pinned real 49 right-hand PNGs, imported original chibi and gold prompts, DVD movement, 9 real mini YOMI icons, live nonblack atlas, correct powering & return: **PASS**](https://github.com/timurkamurka11/Rokas/actions/runs/38094323040).
- [Exact original user PNG import: **PASS**](https://github.com/timurkamurka11/Rokas/actions/runs/38094082016).

In the art-integrated cloud run the approved-size 1672x941 actual-room photograph is a clearly labeled **synthetic test-only** stand-in. Thus CI does NOT prove 100% correct full-size photo color grading, exact physical bezel polygon clipping, actual audio peaks, or actual local Unity Game View quality. The user must check after pulling.

## 2026-10-11 importer recovery and mandatory original-art acceptance

The original five PNGs WERE committed earlier, but all five tracked
`.png.meta` files contained **literal backslash-n characters**, not real
YAML newlines. Unity's editor log stated that the files **did not have valid
GUIDs and would be ignored**. The earlier green art-integrated test silently
accepted the fallback `NO SIGNAL` text and alternate prompt graphics.
That earlier green badge was **not valid visual acceptance of the original PNGs**.

Corrective commits on this SAME main integration branch:

- Five `.png.meta` files rebuilt with full valid `TextureImporter` YAML, stable
  unchanged GUIDs, 4096 max texture dimension, uncompressed RGBA/NPOT
  settings. Original PNG bytes **unchanged**.
- `.github/workflows/laptop-v112-import-approved-ui.yml` preserves these
  existing metas and is idempotent, rather than producing single-line escaped
  YAML again.
- `LaptopV111SeamlessPOVPlayModeTests` now REQUIRES all five exact Unity
  `Resources.Load<Texture2D>` assets, an animated real chibi and actual
  original PNG prompt textures; text fallback cannot pass the test.
  Hover is asserted on the golden child Graphic where it actually lives.

New authoritative checks on `f37c7175ba7e09735596c1d2a5026375948799dc`:

- [**Art-integrated Unity 6000.3 PlayMode PASS 1/1**](https://github.com/timurkamurka11/Rokas/actions/runs/38095453550).
  Downloaded `playmode-results.xml` confirmed passed=1, failed=0,
  skipped=0. The actual Unity `playmode.log` contained **no** `does not
  have a valid GUID` or `chibi sticker resource missing` diagnostics.
  The fixture includes the pinned 49 original hand PNG resources; the
  POV photograph remains clearly labeled synthetic test-only.
- [**Focused cloud Unity EditMode and PlayMode PASS**](https://github.com/timurkamurka11/Rokas/actions/runs/38095453603).
- [**Idempotent exact PNG import workflow PASS**](https://github.com/timurkamurka11/Rokas/actions/runs/38095185724).

No edits to 49 hand frames, POV artwork, Combat, VN or game audio were made in
this hotfix. The original Windows QHD scene/rendering, precise LCD bezel
perspective, scene color grading and SFX playback still require inspection
on the user's local Unity project. **Do not claim final local visual QA PASS.**

## Delivery and next step

1. Close Unity Editor.
2. In the canonical existing Git folder `D:\Rokas\Rokas-FULL-R11-FINISHED-UI COPY`: run `git pull` on its tracked `codex/fidelity3-resume-5779190` branch. If git rejects local edits, back them up/inspect; NEVER force reset/clean the project.
3. Open Unity and run full cycle: before first Power see small bouncing chibi with no text fallback; press physical Power with original 49 hand frames; after Boot see blue Tokyo-wallpaper nine-icon mini YOMI physically inside notebook screen; open fullscreen YOMI by E only; close and see powered mini YOMI with live clock; check full-glow prompt_power, prompt_open, prompt_back, back_arrow PNG + hover. Record original QHD viewport for final visual acceptance.

**No BAT, no new branch, no Combat scope changes. No claim that the entire project's Core CI is green.**
