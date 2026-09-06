COMPLETED:
HOME object highlight visuals replaced with rasterized feathered soft-mist overlays. Existing HOME hit polygons and actions are unchanged. Laptop, Mame, tea, lamp, workbench and door now use blurred milky-white alpha masks with subtle idle/hover/press intensity; WindowHotspot uses a separate edge-faded glass-mist field so it does not trace a rectangular frame.

STATIC CHECKS:
HomeView.cs delimiter/structure check passed; all seven HOME hotspot actions remain present; legacy polygon stroke/mesh-halo rendering is removed from the visual path. Whitespace check performed before commit.

UNITY:
NOT RUN — user will validate manually.

NEXT:
User opens Unity and checks idle + hover look for laptop, tea, lamp, workbench, window, door and Mame.
