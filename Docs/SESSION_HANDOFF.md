COMPLETED:
HOME object highlights restyled from harsh cyan polygon strokes to a soft ambient mist/glow. Existing object-shaped hit masks and all HOME actions are preserved. Idle now keeps a very faint warm-white highlight, hover strengthens the same glow with a subtle pulse, and press adds a short brightness bump. Window/door use much lower halo strength to avoid visible rectangular framing.

STATIC CHECKS:
Reviewed the committed HomeView.cs diff and confirmed the old AddStroke/cyan edge rendering was removed. GitHub reports no commit status checks for this commit; Unity/PlayMode were intentionally not run here.

UNITY:
NOT RUN — user will validate manually.

LAST PUSHED COMMIT:
a0f6b6e6de40dc7cf3f3c2e22a34c5156c7a40ed

NEXT:
User opens Unity and checks idle + hover appearance for laptop, tea, lamp, workbench, door, window and Mame. If the visual result is not acceptable, revert only this highlight-style commit.
