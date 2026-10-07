using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum HomeLocation
    {
        MainRoom,
        Hallway
    }

    public sealed class HallwayView
    {
        public const string BackgroundResourcePath = "Home/HallwayReference";

        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;
        private readonly RokasAudio audio;
        private readonly Action<Func<bool>, string> act;
        private readonly Action returnToMainRoom;
        private readonly Action exitApartment;
        private HomeLightStateOverlay lighting;

        public HallwayView(UiKit ui, RokasAssets assets, GameSession session, RokasAudio audio,
            Action<Func<bool>, string> act, Action returnToMainRoom, Action exitApartment)
        {
            this.ui = ui;
            this.assets = assets;
            this.session = session;
            this.audio = audio;
            this.act = act;
            this.returnToMainRoom = returnToMainRoom;
            this.exitApartment = exitApartment;
        }

        public static Texture2D LoadBackground()
        {
            return Resources.Load<Texture2D>(BackgroundResourcePath);
        }

        // The supplied reference is 3:2 while Home is authored at 16:9.
        // Preserve its proportions and center-crop vertically instead of stretching it.
        public static Rect GetBackgroundUvRect(Texture texture)
        {
            if (!texture || texture.width <= 0 || texture.height <= 0)
                return new Rect(0f, 0f, 1f, 1f);

            float sourceAspect = texture.width / (float)texture.height;
            const float targetAspect = 16f / 9f;
            if (sourceAspect < targetAspect)
            {
                float visibleHeight = sourceAspect / targetAspect;
                return new Rect(0f, (1f - visibleHeight) * .5f, 1f, visibleHeight);
            }
            if (sourceAspect > targetAspect)
            {
                float visibleWidth = targetAspect / sourceAspect;
                return new Rect((1f - visibleWidth) * .5f, 0f, visibleWidth, 1f);
            }
            return new Rect(0f, 0f, 1f, 1f);
        }

        public void Build(RectTransform parent)
        {
            lighting = HomeLightStateOverlay.CreateForHallway(ui, parent);

            // Natural left opening: the polygon follows the architectural doorway instead of
            // placing a visible rectangular button over the scene.
            CreatePolygonHotspot(parent, "HallwayReturnHotspot", HomeScanGlyph.Exit,
                new[]
                {
                    new Vector2(260f, 0f), new Vector2(760f, 0f),
                    new Vector2(760f, 860f), new Vector2(665f, 930f),
                    new Vector2(285f, 930f), new Vector2(250f, 735f)
                },
                535f, 505f, 62f, true, returnToMainRoom);

            // Front apartment door: authoritative entry to the existing Portal/Contract route.
            CreatePolygonHotspot(parent, "HallwayFrontDoorHotspot", HomeScanGlyph.Exit,
                new[]
                {
                    new Vector2(1215f, 80f), new Vector2(1710f, 80f),
                    new Vector2(1710f, 875f), new Vector2(1215f, 875f)
                },
                1500f, 470f, 62f, false, exitApartment);

            // Existing wall switch in the supplied reference.
            CreateRectHotspot(parent, "HallwayLightHotspot", HomeScanGlyph.LightBulb,
                745f, 315f, 150f, 155f,
                830f, 395f, 48f, ToggleHallwayLight);

            Refresh();
        }

        private void ToggleHallwayLight()
        {
            bool next = !session.HallwayLightOn;
            act(() =>
            {
                session.SetHallwayLight(next);
                return true;
            }, string.Empty);
            audio.Play(next ? assets.lampOn : assets.lampOff);
        }

        public void Refresh()
        {
            lighting?.SetHallwayStates(session.State.lampOn, session.HallwayLightOn);
        }

        public void Tick(float dt)
        {
            lighting?.Tick(dt);
        }

        public void ClearReferences()
        {
            lighting = null;
        }

        private void CreatePolygonHotspot(RectTransform parent, string name, HomeScanGlyph glyph,
            Vector2[] polygon, float markerX, float markerY, float connectorLength, bool flipGlyph, Action action)
        {
            RectTransform hit = ui.Rect(parent, name, 0f, 0f, 1920f, 1080f);
            HallwayHotspotGraphic target = hit.gameObject.AddComponent<HallwayHotspotGraphic>();
            target.color = new Color(1f, 1f, 1f, .001f);
            target.Configure(polygon);

            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                audio.Click();
                action?.Invoke();
            });

            BuildMarker(parent, button, name + "Marker", glyph,
                markerX, markerY, connectorLength, flipGlyph);
        }

        private void CreateRectHotspot(RectTransform parent, string name, HomeScanGlyph glyph,
            float x, float y, float width, float height,
            float markerX, float markerY, float connectorLength, Action action)
        {
            RectTransform hit = ui.Rect(parent, name, x, y, width, height);
            Image target = hit.gameObject.AddComponent<Image>();
            target.color = new Color(1f, 1f, 1f, .001f);
            target.raycastTarget = true;

            Button button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() =>
            {
                audio.Click();
                action?.Invoke();
            });

            BuildMarker(parent, button, name + "Marker", glyph,
                markerX, markerY, connectorLength, false);
        }

        private void BuildMarker(Transform parent, Button button, string name, HomeScanGlyph glyph,
            float centerX, float centerY, float connectorLength, bool flipGlyph)
        {
            const float markerSize = 72f;
            RectTransform marker = ui.Rect(parent, name,
                centerX - markerSize * .5f, centerY - markerSize * .5f,
                markerSize, markerSize);

            RectTransform glowRoot = ui.Rect(marker, "ScanGlow", -7f, -7f, 86f, 86f);
            CanvasGroup glowGroup = glowRoot.gameObject.AddComponent<CanvasGroup>();
            LaptopSurface glow = Surface(glowRoot, "GlowSurface", 0f, 0f, 86f, 86f, 43f,
                new Color(.92f, .70f, .36f, .13f));

            LaptopSurface border = Surface(marker, "ScanBadge", 0f, 0f,
                markerSize, markerSize, markerSize * .5f,
                new Color(.92f, .70f, .36f, .98f));
            LaptopSurface face = Surface(marker, "ScanFace", 3f, 3f,
                markerSize - 6f, markerSize - 6f, (markerSize - 6f) * .5f,
                new Color(.025f, .018f, .014f, .88f));

            RectTransform glyphRect = ui.Rect(marker, "ScanGlyph", 17f, 17f, 38f, 38f);
            glyphRect.gameObject.AddComponent<CanvasRenderer>();
            HomeScanGlyphGraphic glyphGraphic = glyphRect.gameObject.AddComponent<HomeScanGlyphGraphic>();
            glyphGraphic.Glyph = glyph;
            glyphGraphic.color = new Color(.98f, .80f, .43f, 1f);
            glyphGraphic.raycastTarget = false;
            if (flipGlyph) glyphRect.localEulerAngles = new Vector3(0f, 0f, 180f);

            RectTransform connector = ui.Rect(marker, "ScanConnector", 33f, 75f, 6f, connectorLength);
            CanvasGroup connectorGroup = connector.gameObject.AddComponent<CanvasGroup>();
            connectorGroup.alpha = .96f;
            int dots = Mathf.Max(3, Mathf.FloorToInt((connectorLength - 2f) / 13f) + 1);
            for (int index = 0; index < dots; index++)
            {
                float y = index * 13f;
                if (y + 6f > connectorLength) break;
                Surface(connector, "Dot" + index, 0f, y, 6f, 6f, 3f,
                    new Color(.96f, .72f, .36f, .96f));
            }

            HallwayMarkerFeedback feedback = button.gameObject.AddComponent<HallwayMarkerFeedback>();
            feedback.Initialize(marker, button, glow, glowGroup, border, face,
                glyphGraphic, connectorGroup);
        }

        private LaptopSurface Surface(Transform parent, string name,
            float x, float y, float width, float height, float radius, Color color)
        {
            GameObject go = ui.Rect(parent, name, x, y, width, height).gameObject;
            go.AddComponent<CanvasRenderer>();
            LaptopSurface surface = go.AddComponent<LaptopSurface>();
            surface.Radius = radius;
            surface.color = color;
            surface.raycastTarget = false;
            return surface;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HallwayHotspotGraphic : MaskableGraphic
    {
        private Vector2[] points = Array.Empty<Vector2>();

        public void Configure(Vector2[] value)
        {
            points = value ?? Array.Empty<Vector2>();
            SetVerticesDirty();
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = true;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (points.Length < 3) return;

            int first = mesh.currentVertCount;
            for (int i = 0; i < points.Length; i++)
                mesh.AddVert(Map(points[i]), color, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++)
                mesh.AddTriangle(first, first + i, first + i + 1);
        }

        public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
        {
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform, screenPoint, eventCamera, out local))
                return false;

            Vector2 point = new Vector2(local.x, -local.y);
            bool inside = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[j];
                bool crosses =
                    (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) /
                    ((Mathf.Abs(b.y - a.y) < .0001f) ? .0001f : (b.y - a.y)) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private Vector2 Map(Vector2 topLeft)
        {
            Rect rect = GetPixelAdjustedRect();
            return new Vector2(rect.xMin + topLeft.x, rect.yMax - topLeft.y);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HallwaySoftRectGraphic : MaskableGraphic
    {
        [Range(.05f, .45f)] public float edge = .18f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float e = Mathf.Clamp(edge, .05f, .45f);
            float[] xs = { 0f, e, 1f - e, 1f };
            float[] ys = { 0f, e, 1f - e, 1f };
            int[,] index = new int[4, 4];

            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    float weight = (x == 0 || x == 3 || y == 0 || y == 3) ? 0f : 1f;
                    Color vertex = color;
                    vertex.a *= weight;
                    index[x, y] = mesh.currentVertCount;
                    mesh.AddVert(new Vector3(
                        Mathf.Lerp(rect.xMin, rect.xMax, xs[x]),
                        Mathf.Lerp(rect.yMin, rect.yMax, ys[y])), vertex, Vector2.zero);
                }
            }

            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    int a = index[x, y];
                    int b = index[x, y + 1];
                    int c = index[x + 1, y + 1];
                    int d = index[x + 1, y];
                    mesh.AddTriangle(a, b, c);
                    mesh.AddTriangle(a, c, d);
                }
            }
        }
    }

    public sealed class HomeLightStateOverlay : MonoBehaviour
    {
        private CanvasGroup ownHallwayOff;
        private CanvasGroup mainRoomOff;
        private CanvasGroup hallwayNeighborOff;
        private float ownTarget;
        private float mainTarget;
        private float neighborTarget;
        private bool stateInitialized;

        public static HomeLightStateOverlay CreateForHallway(UiKit ui, RectTransform parent)
        {
            RectTransform root = ui.Rect(parent, "HallwayLightStateOverlay", 0f, 0f, 1920f, 1080f);
            HomeLightStateOverlay overlay = root.gameObject.AddComponent<HomeLightStateOverlay>();

            RectTransform own = ui.Rect(root, "HallwayOwnLightOffMask", 0f, 0f, 1920f, 1080f);
            overlay.ownHallwayOff = own.gameObject.AddComponent<CanvasGroup>();
            Soft(ui, own, "CeilingPracticalDim",
                1030f, 0f, 730f, 520f, new Color(.035f, .075f, .12f, .54f));
            Soft(ui, own, "CabinetPracticalDim",
                650f, 300f, 620f, 690f, new Color(.035f, .065f, .10f, .43f));
            Soft(ui, own, "EntryWarmDim",
                1160f, 280f, 720f, 680f, new Color(.025f, .055f, .095f, .39f));
            Soft(ui, own, "FloorWarmDim",
                520f, 720f, 1320f, 350f, new Color(.04f, .075f, .11f, .22f));

            RectTransform main = ui.Rect(root, "HallwayMainRoomOffMask", 0f, 0f, 1920f, 1080f);
            overlay.mainRoomOff = main.gameObject.AddComponent<CanvasGroup>();
            Soft(ui, main, "VisibleMainRoomDim",
                210f, 0f, 590f, 970f, new Color(.025f, .07f, .12f, .60f));
            Soft(ui, main, "VisibleMainRoomFloorDim",
                250f, 610f, 600f, 390f, new Color(.03f, .075f, .12f, .32f));

            overlay.ownHallwayOff.alpha = 0f;
            overlay.mainRoomOff.alpha = 0f;
            return overlay;
        }

        public static HomeLightStateOverlay CreateForMainRoom(UiKit ui, RectTransform parent)
        {
            RectTransform root = ui.Rect(parent, "MainRoomNeighborLightOverlay", 0f, 0f, 1920f, 1080f);
            HomeLightStateOverlay overlay = root.gameObject.AddComponent<HomeLightStateOverlay>();

            RectTransform neighbor = ui.Rect(root, "MainRoomHallwayOffMask", 0f, 0f, 1920f, 1080f);
            overlay.hallwayNeighborOff = neighbor.gameObject.AddComponent<CanvasGroup>();
            Soft(ui, neighbor, "VisibleHallwayDoorDim",
                1540f, 0f, 380f, 720f, new Color(.025f, .065f, .11f, .54f));
            Soft(ui, neighbor, "VisibleHallwayFloorDim",
                1490f, 510f, 430f, 330f, new Color(.035f, .07f, .11f, .25f));

            overlay.hallwayNeighborOff.alpha = 0f;
            return overlay;
        }

        public void SetHallwayStates(bool mainRoomOn, bool hallwayOn)
        {
            ownTarget = hallwayOn ? 0f : 1f;
            mainTarget = mainRoomOn ? 0f : 1f;
            if (!stateInitialized)
            {
                if (ownHallwayOff) ownHallwayOff.alpha = ownTarget;
                if (mainRoomOff) mainRoomOff.alpha = mainTarget;
                stateInitialized = true;
            }
        }

        public void SetMainRoomNeighbor(bool hallwayOn)
        {
            neighborTarget = hallwayOn ? 0f : 1f;
            if (!stateInitialized)
            {
                if (hallwayNeighborOff) hallwayNeighborOff.alpha = neighborTarget;
                stateInitialized = true;
            }
        }

        public void Tick(float dt)
        {
            float amount = Mathf.Clamp01(dt / .22f);
            if (ownHallwayOff)
                ownHallwayOff.alpha = Mathf.Lerp(ownHallwayOff.alpha, ownTarget, amount);
            if (mainRoomOff)
                mainRoomOff.alpha = Mathf.Lerp(mainRoomOff.alpha, mainTarget, amount);
            if (hallwayNeighborOff)
                hallwayNeighborOff.alpha = Mathf.Lerp(hallwayNeighborOff.alpha, neighborTarget, amount);
        }

        private static void Soft(UiKit ui, Transform parent, string name,
            float x, float y, float width, float height, Color color)
        {
            RectTransform rect = ui.Rect(parent, name, x, y, width, height);
            rect.gameObject.AddComponent<CanvasRenderer>();
            HallwaySoftRectGraphic graphic = rect.gameObject.AddComponent<HallwaySoftRectGraphic>();
            graphic.color = color;
            graphic.raycastTarget = false;
        }
    }

    public sealed class HallwayMarkerFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform marker;
        private Button button;
        private LaptopSurface glow;
        private CanvasGroup glowGroup;
        private LaptopSurface border;
        private LaptopSurface face;
        private Graphic glyph;
        private CanvasGroup connector;
        private bool hovered;
        private bool selected;
        private bool pressed;

        public void Initialize(RectTransform markerRoot, Button owner,
            LaptopSurface markerGlow, CanvasGroup markerGlowGroup,
            LaptopSurface markerBorder, LaptopSurface markerFace,
            Graphic markerGlyph, CanvasGroup markerConnector)
        {
            marker = markerRoot;
            button = owner;
            glow = markerGlow;
            glowGroup = markerGlowGroup;
            border = markerBorder;
            face = markerFace;
            glyph = markerGlyph;
            connector = markerConnector;
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData eventData) { selected = true; }
        public void OnDeselect(BaseEventData eventData) { selected = false; pressed = false; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) pressed = true;
        }

        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void Update()
        {
            if (!marker || !button) return;

            float focus = button.IsInteractable() && (hovered || selected) ? 1f : 0f;
            float targetScale = pressed ? .965f : Mathf.Lerp(1f, 1.055f, focus);
            float speed = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
            marker.localScale = Vector3.Lerp(marker.localScale,
                new Vector3(targetScale, targetScale, 1f), speed);

            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.35f);
            if (glowGroup)
                glowGroup.alpha = Mathf.Clamp01((.10f + .05f * pulse + .16f * focus) / .35f);

            Color coreGold = new Color(.92f, .70f, .36f, 1f);
            Color brightGold = new Color(1f, .82f, .47f, 1f);
            if (border)
                border.color = Color.Lerp(border.color, Color.Lerp(coreGold, brightGold, focus), speed);
            if (glyph)
                glyph.color = Color.Lerp(glyph.color,
                    Color.Lerp(new Color(.98f, .80f, .43f, 1f), Color.white, focus), speed);
            if (face)
                face.color = Color.Lerp(face.color,
                    Color.Lerp(new Color(.025f, .018f, .014f, .88f),
                        new Color(.055f, .035f, .015f, .94f), focus), speed);
            if (glow)
                glow.color = new Color(.96f, .70f, .34f, .13f + .08f * focus);
            if (connector)
                connector.alpha = Mathf.Lerp(connector.alpha, .94f + .06f * focus, speed);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            if (marker) marker.localScale = Vector3.one;
        }
    }
}
