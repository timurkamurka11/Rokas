using System;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnWorkshopPreviewScene
    {
        BusStopKeiko,
        NightSkyKeiko,
        MinaBody,
        PhoneMessage,
        TwoCharacterFocus
    }

    public sealed class VnWorkshopPreviewFrame
    {
        public VnWorkshopPreviewFrame(
            Vector2 screenSize,
            Vector2 virtualCanvasSize,
            Texture2D backgroundTexture,
            Texture2D dialoguePanelTexture,
            Font font,
            Texture2D minaTexture,
            Rect minaUv,
            Texture2D keikoTexture,
            Rect keikoUv,
            Rect dialoguePanel,
            Rect minaBody,
            Rect keikoBody,
            Rect speakerName,
            Rect dialogueText,
            Rect muteHitRegion,
            Rect pauseHitRegion,
            Rect skipHitRegion,
            Rect back,
            Rect next,
            string speaker,
            string dialogue,
            bool showMina,
            bool showKeiko,
            VnWorkshopFocusValues focus)
            : this(screenSize, virtualCanvasSize, backgroundTexture, dialoguePanelTexture, font, font,
                new VnWorkshopTypographyValues
                {
                    DialogueFontPreset = VnWorkshopFontPreset.ProjectSans,
                    DialogueFontSize = 22f,
                    DialogueAlignment = VnWorkshopTextAlignment.Left,
                    SpeakerFontPreset = VnWorkshopFontPreset.ProjectSans,
                    SpeakerFontSize = 26f
                },
                minaTexture, minaUv, keikoTexture, keikoUv, dialoguePanel, minaBody, keikoBody, speakerName,
                dialogueText, muteHitRegion, pauseHitRegion, skipHitRegion, back, next, speaker, dialogue,
                showMina, showKeiko, focus)
        {
        }

        public VnWorkshopPreviewFrame(
            Vector2 screenSize,
            Vector2 virtualCanvasSize,
            Texture2D backgroundTexture,
            Texture2D dialoguePanelTexture,
            Font dialogueFont,
            Font speakerFont,
            VnWorkshopTypographyValues typography,
            Texture2D minaTexture,
            Rect minaUv,
            Texture2D keikoTexture,
            Rect keikoUv,
            Rect dialoguePanel,
            Rect minaBody,
            Rect keikoBody,
            Rect speakerName,
            Rect dialogueText,
            Rect muteHitRegion,
            Rect pauseHitRegion,
            Rect skipHitRegion,
            Rect back,
            Rect next,
            string speaker,
            string dialogue,
            bool showMina,
            bool showKeiko,
            VnWorkshopFocusValues focus)
        {
            ScreenSize = screenSize;
            VirtualCanvasSize = virtualCanvasSize;
            BackgroundTexture = backgroundTexture;
            DialoguePanelTexture = dialoguePanelTexture;
            Font = dialogueFont;
            DialogueFont = dialogueFont;
            SpeakerFont = speakerFont;
            Typography = typography;
            MinaTexture = minaTexture;
            MinaUv = minaUv;
            KeikoTexture = keikoTexture;
            KeikoUv = keikoUv;
            DialoguePanel = dialoguePanel;
            MinaBody = minaBody;
            KeikoBody = keikoBody;
            SpeakerName = speakerName;
            DialogueText = dialogueText;
            MuteHitRegion = muteHitRegion;
            PauseHitRegion = pauseHitRegion;
            SkipHitRegion = skipHitRegion;
            Back = back;
            Next = next;
            Speaker = speaker;
            Dialogue = dialogue;
            ShowMina = showMina;
            ShowKeiko = showKeiko;
            Focus = focus;
        }

        public Vector2 ScreenSize { get; }
        public Vector2 VirtualCanvasSize { get; }
        public Texture2D BackgroundTexture { get; internal set; }
        public Texture2D DialoguePanelTexture { get; }
        public Font Font { get; }
        public Font DialogueFont { get; }
        public Font SpeakerFont { get; }
        public VnWorkshopTypographyValues Typography { get; }
        public Texture2D MinaTexture { get; }
        public Rect MinaUv { get; }
        public Texture2D KeikoTexture { get; }
        public Rect KeikoUv { get; }
        public Rect DialoguePanel { get; }
        public Rect MinaBody { get; }
        public Rect KeikoBody { get; }
        public Rect SpeakerName { get; }
        public Rect DialogueText { get; }
        public Rect MuteHitRegion { get; }
        public Rect PauseHitRegion { get; }
        public Rect SkipHitRegion { get; }
        public Rect Back { get; }
        public Rect Next { get; }
        public string Speaker { get; internal set; }
        public string Dialogue { get; internal set; }
        public bool ShowMina { get; internal set; }
        public bool ShowKeiko { get; internal set; }
        public VnWorkshopFocusValues Focus { get; }
        public VnWorkshopPreviewCharacter[] ComposerCharacters { get; internal set; }

        public Rect GetElementRect(VnWorkshopElement element)
        {
            switch (element)
            {
                case VnWorkshopElement.DialoguePanel: return DialoguePanel;
                case VnWorkshopElement.MinaBody: return MinaBody;
                case VnWorkshopElement.SpeakerName: return SpeakerName;
                case VnWorkshopElement.DialogueText: return DialogueText;
                case VnWorkshopElement.Back: return Back;
                case VnWorkshopElement.Next: return Next;
                case VnWorkshopElement.MuteHitRegion: return MuteHitRegion;
                case VnWorkshopElement.PauseHitRegion: return PauseHitRegion;
                case VnWorkshopElement.SkipHitRegion: return SkipHitRegion;
                default: throw new ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }
    }

    public static partial class VnPresentationWorkshopPreviewRenderer
    {
        private const float PanelAnchorMinX = .04f;
        private const float PanelAnchorMaxX = .96f;
        private const float PanelBottom = -98f;
        private const float CharacterBodyHeight = 1240f;

        public static RokasAssets LoadAssets()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            if (assets == null)
                throw new InvalidOperationException("ROKAS VN UI Workshop could not load Resources/RokasAssets.");
            RequireAsset(assets.vnBusStopRainNight, nameof(assets.vnBusStopRainNight));
            RequireAsset(assets.vnNightSkyRain, nameof(assets.vnNightSkyRain));
            RequireAsset(assets.vnBusStopPhoneMessageMina, nameof(assets.vnBusStopPhoneMessageMina));
            RequireAsset(assets.vnDialoguePanelKeikoDark, nameof(assets.vnDialoguePanelKeikoDark));
            RequireAsset(assets.vnDialoguePanelMinaLight, nameof(assets.vnDialoguePanelMinaLight));
            RequireAsset(assets.vnMinaCharacterSheet, nameof(assets.vnMinaCharacterSheet));
            RequireAsset(assets.vnKeikoCharacterSheet, nameof(assets.vnKeikoCharacterSheet));
            RequireAsset(assets.sans, nameof(assets.sans));
            RequireAsset(assets.serif, nameof(assets.serif));
            return assets;
        }

        public static VnWorkshopPreviewFrame BuildFrame(
            VnPresentationWorkshopPreset preset,
            VnWorkshopResolution resolution,
            VnWorkshopPreviewScene scene)
        {
            return BuildFrame(preset, resolution, scene, null);
        }

        internal static VnWorkshopPreviewFrame BuildFrame(
            VnPresentationWorkshopPreset preset,
            VnWorkshopResolution resolution,
            VnWorkshopPreviewScene scene,
            string dialogueOverride)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));

            RokasAssets assets = LoadAssets();
            Vector2 screenSize = VnPresentationWorkshopResolver.GetScreenSize(resolution);
            Vector2 virtualCanvas = VnPresentationWorkshopResolver.CalculateVirtualCanvasSize(
                Mathf.RoundToInt(screenSize.x), Mathf.RoundToInt(screenSize.y));

            ResolveScene(assets, scene, out Texture2D background, out Texture2D panelTexture,
                out string speaker, out string dialogue, out bool showMina, out bool showKeiko);
            if (dialogueOverride != null) dialogue = dialogueOverride;

            Rect panel = ApplyOverride(BuildPanelRect(virtualCanvas, panelTexture), preset.dialoguePanel);
            Rect speakerName = ApplyOverride(RelativeRect(panel, .12f, .48f, .42f, .84f), preset.speakerName);
            Rect dialogueText = ApplyOverride(RelativeRect(panel, .10f, .14f, .90f, .54f), preset.dialogueText);
            Rect mute = ApplyOverride(CenteredRect(panel, .455f, .735f, 76f), preset.muteHitRegion);
            Rect pause = ApplyOverride(CenteredRect(panel, .560f, .735f, 76f), preset.pauseHitRegion);
            Rect skip = ApplyOverride(CenteredRect(panel, .665f, .735f, 76f), preset.skipHitRegion);
            Rect back = ApplyOverride(CenteredRect(panel, .790f, .735f, 82f), preset.back);
            Rect next = ApplyOverride(CenteredRect(panel, .900f, .735f, 82f), preset.next);

            VnCharacterVisualState minaState = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
            VnCharacterVisualState keikoState = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko");
            VnWorkshopFocusValues focus = VnPresentationWorkshopResolver.ResolveFocus(preset);
            VnWorkshopTypographyValues typography = VnPresentationWorkshopVn10Resolver.ResolveTypography(preset);
            Font dialogueFont = ResolveFont(assets, typography.DialogueFontPreset);
            Font speakerFont = ResolveFont(assets, typography.SpeakerFontPreset);

            Rect minaBody = BuildCharacterRect(assets.vnMinaCharacterSheet, minaState.BodyUv, virtualCanvas, 0f);
            Rect keikoBody = BuildCharacterRect(assets.vnKeikoCharacterSheet, keikoState.BodyUv, virtualCanvas, 0f);
            if (scene == VnWorkshopPreviewScene.TwoCharacterFocus)
            {
                minaBody = MoveCenterX(minaBody, virtualCanvas.x * .5f + focus.TwoCharacterOffset);
                keikoBody = MoveCenterX(keikoBody, virtualCanvas.x * .5f - focus.TwoCharacterOffset);
                minaBody = ScaleAroundCenter(minaBody, focus.InactiveScale);
                keikoBody = ScaleAroundCenter(keikoBody, focus.ActiveScale);
            }
            minaBody = ApplyOverride(minaBody, preset.minaBody);

            return new VnWorkshopPreviewFrame(
                screenSize, virtualCanvas, background, panelTexture, dialogueFont, speakerFont, typography,
                assets.vnMinaCharacterSheet, minaState.BodyUv, assets.vnKeikoCharacterSheet, keikoState.BodyUv,
                panel, minaBody, keikoBody, speakerName, dialogueText, mute, pause, skip, back, next,
                speaker, dialogue, showMina, showKeiko, focus);
        }

        public static void Draw(Rect previewRect, VnWorkshopPreviewFrame frame,
            VnWorkshopElement? selected = null, bool showHitRegions = true)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Rect canvasRect = FitAspect(previewRect, frame.ScreenSize.x / frame.ScreenSize.y);
            GUI.Box(previewRect, GUIContent.none);

            GUI.BeginGroup(canvasRect);
            try
            {
                Rect localCanvas = new Rect(0f, 0f, canvasRect.width, canvasRect.height);
                if (!TryDrawRegisteredPlaybackBackground(localCanvas, frame))
                    GUI.DrawTexture(localCanvas, frame.BackgroundTexture, ScaleMode.StretchToFill, false);
                if (frame.ComposerCharacters != null)
                {
                    for (int i = 0; i < frame.ComposerCharacters.Length; i++)
                    {
                        VnWorkshopPreviewCharacter character = frame.ComposerCharacters[i];
                        if (character == null || character.Texture == null) continue;
                        DrawCharacter(localCanvas, frame, character.Body, character.Texture, character.Uv,
                            character.Alpha, character.Brightness);
                    }
                }
                else
                {
                    if (frame.ShowKeiko)
                        DrawCharacter(localCanvas, frame, frame.KeikoBody, frame.KeikoTexture, frame.KeikoUv,
                            frame.Speaker == "Keiko" ? 1f : frame.Focus.InactiveAlpha);
                    if (frame.ShowMina)
                        DrawCharacter(localCanvas, frame, frame.MinaBody, frame.MinaTexture, frame.MinaUv,
                            frame.Speaker == "Mina" ? 1f : frame.Focus.InactiveAlpha);
                }

                GUI.DrawTexture(LogicalToPreview(localCanvas, frame.DialoguePanel, frame), frame.DialoguePanelTexture,
                    ScaleMode.StretchToFill, true);

                DrawText(LogicalToPreview(localCanvas, frame.SpeakerName, frame), frame.Speaker,
                    frame.SpeakerFont, Mathf.RoundToInt(frame.Typography.SpeakerFontSize), FontStyle.Bold);
                DrawText(LogicalToPreview(localCanvas, frame.DialogueText, frame), frame.Dialogue,
                    frame.DialogueFont, Mathf.RoundToInt(frame.Typography.DialogueFontSize), FontStyle.Normal,
                    ToTextAnchor(frame.Typography.DialogueAlignment));
                DrawText(LogicalToPreview(localCanvas, frame.Back, frame), "‹", frame.DialogueFont, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
                DrawText(LogicalToPreview(localCanvas, frame.Next, frame), "›", frame.DialogueFont, 34, FontStyle.Bold, TextAnchor.MiddleCenter);

                if (showHitRegions)
                {
                    DrawHitRegion(localCanvas, frame, frame.MuteHitRegion, "Mute — Baked into panel");
                    DrawHitRegion(localCanvas, frame, frame.PauseHitRegion, "Pause — Baked into panel");
                    DrawHitRegion(localCanvas, frame, frame.SkipHitRegion, "Skip — Baked into panel");
                }
                if (selected.HasValue) DrawOutline(LogicalToPreview(localCanvas, frame.GetElementRect(selected.Value), frame), 2f);
            }
            finally { GUI.EndGroup(); }
        }

        public static Rect ClipLogicalRectToViewport(Rect logicalRect, Vector2 virtualCanvasSize)
        {
            float width = Mathf.Max(0f, virtualCanvasSize.x);
            float height = Mathf.Max(0f, virtualCanvasSize.y);
            float xMin = Mathf.Clamp(logicalRect.xMin, 0f, width);
            float xMax = Mathf.Clamp(logicalRect.xMax, 0f, width);
            float yMin = Mathf.Clamp(logicalRect.yMin, 0f, height);
            float yMax = Mathf.Clamp(logicalRect.yMax, 0f, height);
            if (xMax < xMin) xMax = xMin;
            if (yMax < yMin) yMax = yMin;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static VnWorkshopElement? HitTest(VnWorkshopPreviewFrame frame, Vector2 logicalPoint)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            VnWorkshopElement[] order =
            {
                VnWorkshopElement.Back, VnWorkshopElement.Next, VnWorkshopElement.MuteHitRegion,
                VnWorkshopElement.PauseHitRegion, VnWorkshopElement.SkipHitRegion, VnWorkshopElement.SpeakerName,
                VnWorkshopElement.DialogueText, VnWorkshopElement.MinaBody, VnWorkshopElement.DialoguePanel
            };
            foreach (VnWorkshopElement element in order)
                if (frame.GetElementRect(element).Contains(logicalPoint)) return element;
            return null;
        }

        public static Vector2 PreviewToLogical(Rect previewRect, Vector2 mousePosition, VnWorkshopPreviewFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            Rect canvasRect = FitAspect(previewRect, frame.ScreenSize.x / frame.ScreenSize.y);
            float x = Mathf.InverseLerp(canvasRect.xMin, canvasRect.xMax, mousePosition.x) * frame.VirtualCanvasSize.x;
            float yFromTop = Mathf.InverseLerp(canvasRect.yMin, canvasRect.yMax, mousePosition.y);
            float y = (1f - yFromTop) * frame.VirtualCanvasSize.y;
            return new Vector2(x, y);
        }

        public static Rect LogicalToPreview(Rect previewRect, Rect logicalRect, VnWorkshopPreviewFrame frame)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            float sx = previewRect.width / frame.VirtualCanvasSize.x;
            float sy = previewRect.height / frame.VirtualCanvasSize.y;
            float x = previewRect.x + logicalRect.x * sx;
            float y = previewRect.y + previewRect.height - (logicalRect.y + logicalRect.height) * sy;
            return new Rect(x, y, logicalRect.width * sx, logicalRect.height * sy);
        }

        private static void ResolveScene(RokasAssets assets, VnWorkshopPreviewScene scene,
            out Texture2D background, out Texture2D panel, out string speaker, out string dialogue,
            out bool showMina, out bool showKeiko)
        {
            switch (scene)
            {
                case VnWorkshopPreviewScene.BusStopKeiko:
                    background = assets.vnBusStopRainNight; panel = assets.vnDialoguePanelKeikoDark; speaker = "Keiko";
                    dialogue = "The rain makes the city feel farther away than it is."; showMina = false; showKeiko = true; return;
                case VnWorkshopPreviewScene.NightSkyKeiko:
                    background = assets.vnNightSkyRain; panel = assets.vnDialoguePanelKeikoDark; speaker = "Keiko";
                    dialogue = "Look up. Even tonight, there is still a way forward."; showMina = false; showKeiko = true; return;
                case VnWorkshopPreviewScene.MinaBody:
                    background = assets.vnBusStopRainNight; panel = assets.vnDialoguePanelMinaLight; speaker = "Mina";
                    dialogue = "I found you. Do not disappear again, okay?"; showMina = true; showKeiko = false; return;
                case VnWorkshopPreviewScene.PhoneMessage:
                    background = assets.vnBusStopPhoneMessageMina; panel = assets.vnDialoguePanelMinaLight; speaker = "Mina";
                    dialogue = "One new message."; showMina = false; showKeiko = false; return;
                case VnWorkshopPreviewScene.TwoCharacterFocus:
                    background = assets.vnBusStopRainNight; panel = assets.vnDialoguePanelKeikoDark; speaker = "Keiko";
                    dialogue = "Stay close. We move together from here."; showMina = true; showKeiko = true; return;
                default: throw new ArgumentOutOfRangeException(nameof(scene), scene, null);
            }
        }

        private static Font ResolveFont(RokasAssets assets, VnWorkshopFontPreset preset)
        {
            switch (preset)
            {
                case VnWorkshopFontPreset.ProjectSans: return assets.sans;
                case VnWorkshopFontPreset.ProjectSerif: return assets.serif;
                default: throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        private static TextAnchor ToTextAnchor(VnWorkshopTextAlignment alignment)
        {
            switch (alignment)
            {
                case VnWorkshopTextAlignment.Left: return TextAnchor.MiddleLeft;
                case VnWorkshopTextAlignment.Center: return TextAnchor.MiddleCenter;
                case VnWorkshopTextAlignment.Right: return TextAnchor.MiddleRight;
                default: return TextAnchor.MiddleLeft;
            }
        }

        private static Rect BuildPanelRect(Vector2 virtualCanvas, Texture2D panelTexture)
        {
            float width = virtualCanvas.x * (PanelAnchorMaxX - PanelAnchorMinX);
            float height = width * panelTexture.height / panelTexture.width;
            return new Rect(virtualCanvas.x * PanelAnchorMinX, PanelBottom, width, height);
        }

        private static Rect BuildCharacterRect(Texture2D texture, Rect uv, Vector2 virtualCanvas, float xOffset)
        {
            float sourceWidth = texture.width * uv.width;
            float sourceHeight = texture.height * uv.height;
            float width = CharacterBodyHeight * sourceWidth / sourceHeight;
            Vector2 center = new Vector2(virtualCanvas.x * .5f + xOffset, CharacterBodyHeight * .37f);
            return RectFromCenter(center, new Vector2(width, CharacterBodyHeight));
        }

        private static Rect RelativeRect(Rect parent, float minX, float minY, float maxX, float maxY)
        {
            return new Rect(parent.x + parent.width * minX, parent.y + parent.height * minY,
                parent.width * (maxX - minX), parent.height * (maxY - minY));
        }

        private static Rect CenteredRect(Rect parent, float normalizedX, float normalizedY, float size)
        {
            Vector2 center = new Vector2(parent.x + parent.width * normalizedX, parent.y + parent.height * normalizedY);
            return RectFromCenter(center, new Vector2(size, size));
        }

        private static Rect ApplyOverride(Rect baseline, VnWorkshopElementOverride elementOverride)
        {
            Vector2 center = baseline.center;
            Vector2 size = baseline.size;
            if (elementOverride.hasPositionDelta) center += elementOverride.positionDelta;
            if (elementOverride.hasSizeDelta) size += elementOverride.sizeDelta;
            if (elementOverride.hasScaleMultiplier) size *= elementOverride.scaleMultiplier;
            size.x = Mathf.Max(1f, size.x);
            size.y = Mathf.Max(1f, size.y);
            return RectFromCenter(center, size);
        }

        private static Rect MoveCenterX(Rect rect, float centerX)
        {
            rect.center = new Vector2(centerX, rect.center.y);
            return rect;
        }

        private static Rect ScaleAroundCenter(Rect rect, float scale) => RectFromCenter(rect.center, rect.size * scale);
        private static Rect RectFromCenter(Vector2 center, Vector2 size) => new Rect(center - size * .5f, size);

        private static Rect FitAspect(Rect available, float aspect)
        {
            float availableAspect = available.width / Mathf.Max(1f, available.height);
            if (availableAspect > aspect)
            {
                float width = available.height * aspect;
                return new Rect(available.center.x - width * .5f, available.y, width, available.height);
            }
            float height = available.width / aspect;
            return new Rect(available.x, available.center.y - height * .5f, available.width, height);
        }

        private static void DrawCharacter(Rect canvasRect, VnWorkshopPreviewFrame frame, Rect logicalRect,
            Texture2D texture, Rect uv, float alpha)
        {
            DrawCharacter(canvasRect, frame, logicalRect, texture, uv, alpha, 1f);
        }

        private static void DrawCharacter(Rect canvasRect, VnWorkshopPreviewFrame frame, Rect logicalRect,
            Texture2D texture, Rect uv, float alpha, float brightness)
        {
            Color previous = GUI.color;
            float value = Mathf.Max(0f, brightness);
            GUI.color = new Color(value, value, value, Mathf.Clamp01(alpha));
            GUI.DrawTextureWithTexCoords(LogicalToPreview(canvasRect, logicalRect, frame), texture, uv, true);
            GUI.color = previous;
        }

        private static void DrawText(Rect rect, string text, Font font, int fontSize, FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var guiStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = Mathf.Max(1, fontSize),
                fontStyle = style,
                alignment = alignment,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, text ?? string.Empty, guiStyle);
        }

        private static void DrawHitRegion(Rect canvasRect, VnWorkshopPreviewFrame frame, Rect logicalRect, string label)
        {
            Rect rect = LogicalToPreview(canvasRect, logicalRect, frame);
            DrawOutline(rect, 1f);
            if (rect.width >= 48f)
            {
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.LowerCenter,
                    wordWrap = true,
                    fontSize = 9,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(rect.x - 35f, rect.yMax + 2f, rect.width + 70f, 30f), label, style);
            }
        }

        private static void DrawOutline(Rect rect, float thickness)
        {
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static void RequireAsset(UnityEngine.Object asset, string fieldName)
        {
            if (asset == null)
                throw new InvalidOperationException("ROKAS VN UI Workshop requires authored RokasAssets field '" + fieldName + "'.");
        }
    }
}
