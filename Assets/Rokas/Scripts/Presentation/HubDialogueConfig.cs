using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class HubDialogueRectData
    {
        // schema v2: authored presence is explicit. A missing object is a
        // deliberate Workshop deletion, not a request for runtime defaults.
        public bool exists = true;
        public bool active = true;

        public float x;
        public float y;
        public float width;
        public float height;
        public float anchoredZ;
        public float rotationX;
        public float rotationY;
        public float rotationZ;
        public float scaleX = 1f;
        public float scaleY = 1f;
        public float scaleZ = 1f;
        public float anchorMinX = 0f;
        public float anchorMinY = 1f;
        public float anchorMaxX = 0f;
        public float anchorMaxY = 1f;
        public float pivotX = 0f;
        public float pivotY = 1f;
        public int siblingIndex = -1;

        public bool graphicEnabled = true;
        public float colorR = 1f;
        public float colorG = 1f;
        public float colorB = 1f;
        public float colorA = 1f;

        public float uvX;
        public float uvY;
        public float uvWidth = 1f;
        public float uvHeight = 1f;

        public HubDialogueRectData() { }

        public HubDialogueRectData(
            float x,
            float y,
            float width,
            float height,
            float rotationZ = 0f)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
            this.rotationZ = rotationZ;
        }
    }

    /// <summary>
    /// Single authoritative transform/visual mapping used by both the
    /// editor Workshop and the Play Mode runtime.
    /// </summary>
    public static class HubDialogueLayout
    {
        public static void Apply(
            RectTransform rect,
            HubDialogueRectData data)
        {
            if (!rect || data == null) return;

            rect.anchorMin =
                new Vector2(data.anchorMinX, data.anchorMinY);
            rect.anchorMax =
                new Vector2(data.anchorMaxX, data.anchorMaxY);
            rect.pivot =
                new Vector2(data.pivotX, data.pivotY);
            rect.anchoredPosition3D =
                new Vector3(data.x, -data.y, data.anchoredZ);
            rect.sizeDelta =
                new Vector2(data.width, data.height);
            rect.localScale =
                new Vector3(data.scaleX, data.scaleY, data.scaleZ);
            rect.localEulerAngles =
                new Vector3(
                    data.rotationX,
                    data.rotationY,
                    data.rotationZ);

            if (data.siblingIndex >= 0 && rect.parent)
                rect.SetSiblingIndex(
                    Mathf.Clamp(
                        data.siblingIndex,
                        0,
                        rect.parent.childCount - 1));

            rect.gameObject.SetActive(data.active);
        }

        public static void Capture(
            RectTransform rect,
            HubDialogueRectData data)
        {
            if (!rect || data == null) return;

            data.exists = true;
            data.active = rect.gameObject.activeSelf;
            data.x = rect.anchoredPosition.x;
            data.y = -rect.anchoredPosition.y;
            data.anchoredZ = rect.anchoredPosition3D.z;
            data.width = rect.sizeDelta.x;
            data.height = rect.sizeDelta.y;

            Vector3 rotation = rect.localEulerAngles;
            data.rotationX = Mathf.DeltaAngle(0f, rotation.x);
            data.rotationY = Mathf.DeltaAngle(0f, rotation.y);
            data.rotationZ = Mathf.DeltaAngle(0f, rotation.z);

            Vector3 scale = rect.localScale;
            data.scaleX = scale.x;
            data.scaleY = scale.y;
            data.scaleZ = scale.z;

            data.anchorMinX = rect.anchorMin.x;
            data.anchorMinY = rect.anchorMin.y;
            data.anchorMaxX = rect.anchorMax.x;
            data.anchorMaxY = rect.anchorMax.y;
            data.pivotX = rect.pivot.x;
            data.pivotY = rect.pivot.y;
            data.siblingIndex = rect.GetSiblingIndex();
        }

        public static void ApplyGraphic(
            Graphic graphic,
            HubDialogueRectData data)
        {
            if (!graphic || data == null) return;

            graphic.enabled = data.graphicEnabled;
            graphic.color =
                new Color(
                    data.colorR,
                    data.colorG,
                    data.colorB,
                    data.colorA);
        }

        public static void CaptureGraphic(
            Graphic graphic,
            HubDialogueRectData data)
        {
            if (!graphic || data == null) return;

            data.graphicEnabled = graphic.enabled;
            Color color = graphic.color;
            data.colorR = color.r;
            data.colorG = color.g;
            data.colorB = color.b;
            data.colorA = color.a;
        }

        public static void ApplyRawImage(
            RawImage image,
            HubDialogueRectData data)
        {
            if (!image || data == null) return;

            ApplyGraphic(image, data);
            image.uvRect =
                new Rect(
                    data.uvX,
                    data.uvY,
                    data.uvWidth,
                    data.uvHeight);
        }

        public static void CaptureRawImage(
            RawImage image,
            HubDialogueRectData data)
        {
            if (!image || data == null) return;

            CaptureGraphic(image, data);
            Rect uv = image.uvRect;
            data.uvX = uv.x;
            data.uvY = uv.y;
            data.uvWidth = uv.width;
            data.uvHeight = uv.height;
        }

        public static void ApplySiblingOrder(
            Transform parent,
            string[] childNames,
            HubDialogueRectData[] data)
        {
            if (!parent ||
                childNames == null ||
                data == null)
                return;

            int count =
                Mathf.Min(
                    childNames.Length,
                    data.Length);
            bool[] applied =
                new bool[count];

            // Move highest authored indices first. This preserves the final
            // relative order even when optional siblings were deleted.
            for (int pass = 0; pass < count; pass++)
            {
                int best = -1;
                int bestIndex = int.MinValue;
                for (int i = 0; i < count; i++)
                {
                    if (applied[i] ||
                        data[i] == null ||
                        !data[i].exists ||
                        data[i].siblingIndex < 0)
                        continue;

                    if (data[i].siblingIndex > bestIndex)
                    {
                        best = i;
                        bestIndex =
                            data[i].siblingIndex;
                    }
                }

                if (best < 0) break;
                applied[best] = true;

                Transform child =
                    parent.Find(childNames[best]);
                if (!child) continue;

                child.SetSiblingIndex(
                    Mathf.Clamp(
                        data[best].siblingIndex,
                        0,
                        parent.childCount - 1));
            }
        }
    }

    [Serializable]
    public sealed class HubDialogueConfigData
    {
        // Missing/zero means legacy v1 and is migrated on load.
        public int schemaVersion;
        public string speakerName = "Keiko";
        public string guildIntroText =
            "Ага… Мина что-то говорила про ноутбук. Надо посмотреть, что там.";
        public string windowText =
            "Поезд проходит без остановки. На этот раз — настоящий.";
        public string floorLampTurnOffText = "За окном кто-то есть?..";
        public string floorLampTurnOnText = "Комната снова наполнилась теплом.";
        public string mameText = "Мамэ довольно щурится. Почти как обычный питомец.";
        public string mameThirdText = "Мамэ внимательно смотрит в пустой угол.";

        public HubDialogueRectData layoutRoot =
            new HubDialogueRectData(0f, 0f, 1920f, 1080f);
        public float layoutScale = 1f;
        public HubDialogueRectData plaque =
            new HubDialogueRectData(370f, 635f, 1180f, 393f);
        public HubDialogueRectData plaqueArt =
            new HubDialogueRectData(0f, 0f, 1180f, 393f);
        public HubDialogueRectData portraitMask =
            new HubDialogueRectData(34f, 112f, 178f, 184f);
        public HubDialogueRectData portrait =
            new HubDialogueRectData(-12f, -9f, 202f, 202f)
            {
                uvWidth = 1f / 6f,
                uvHeight = .5f
            };
        public HubDialogueRectData speaker =
            new HubDialogueRectData(238f, 128f, 530f, 42f)
            {
                colorR = .90f,
                colorG = .96f,
                colorB = 1f,
                colorA = 1f
            };
        public int speakerFontSize = 21;
        public TextAnchor speakerAlignment = TextAnchor.MiddleLeft;
        public HubDialogueRectData dialogue =
            new HubDialogueRectData(238f, 151f, 760f, 150f);
        public int dialogueFontSize = 20;
        public TextAnchor dialogueAlignment = TextAnchor.UpperLeft;
        public float dialogueLineSpacing = 1f;
        public HubDialogueRectData arrowCover =
            new HubDialogueRectData(1057f, 249f, 52f, 54f)
            {
                uvX = 1680f / 2048f,
                uvY = 155f / 682f,
                uvWidth = 90f / 2048f,
                uvHeight = 90f / 682f
            };
        public HubDialogueRectData completionArrow =
            new HubDialogueRectData(1068f, 260f, 27f, 31f);
        public float completionBobScale = 1f;
        public float completionPulseScale = 1f;
        public HubDialogueRectData muteButton =
            new HubDialogueRectData(966f, 73f, 49f, 49f);
        public HubDialogueRectData forwardButton =
            new HubDialogueRectData(1021f, 73f, 49f, 49f);
        public HubDialogueRectData menuButton =
            new HubDialogueRectData(1075f, 73f, 49f, 49f);
        public bool muteButtonEnabled = true;
        public bool forwardButtonEnabled = true;
        public bool menuButtonEnabled = true;
        public float idleFramesPerSecond = 6f;
        public float talkFramesPerSecond = 10f;

        public bool voiceEnabled = true;
        public float voiceVolume = .34f;
        public float voicePitch = 1f;
        public bool voiceLoop = true;
        public string voiceResourcePath = "HubDialogue/KeikoTextVoice";
    }

    public static class HubDialogueConfig
    {
        public const string DefaultResourcePath =
            "HubDialogue/HubDialogueConfig";
        public const string UserResourcePath =
            "HubDialogue/HubDialogueConfig.user";

        private static HubDialogueConfigData cached;

        public static HubDialogueConfigData Current
        {
            get
            {
                if (cached == null) cached = LoadFresh();
                return cached;
            }
        }

        public static void ResetCache() { cached = null; }

        public static HubDialogueConfigData LoadFresh()
        {
            TextAsset source =
                Resources.Load<TextAsset>(UserResourcePath) ??
                Resources.Load<TextAsset>(DefaultResourcePath);
            HubDialogueConfigData data = null;
            if (source && !string.IsNullOrWhiteSpace(source.text))
            {
                try { data = JsonUtility.FromJson<HubDialogueConfigData>(source.text); }
                catch { data = null; }
            }
            return NormalizeForAuthoring(data ?? new HubDialogueConfigData());
        }

        public static HubDialogueDefinition Resolve(string id, string fallbackText)
        {
            HubDialogueConfigData data = Current;
            string text = fallbackText ?? string.Empty;
            switch (id ?? string.Empty)
            {
                case "hub-guild-intro":
                    text = Prefer(data.guildIntroText, text);
                    break;
                case "home-window":
                    text = Prefer(data.windowText, text);
                    break;
                case "home-mame":
                    text = Prefer(data.mameText, text);
                    break;
                case "home-mame-third":
                    text = Prefer(data.mameThirdText, text);
                    break;
            }

            return new HubDialogueDefinition(
                id,
                Prefer(data.speakerName, "Keiko"),
                text);
        }

        public static HubDialogueDefinition ResolveFloorLamp(bool wasOn, string fallbackText)
        {
            HubDialogueConfigData data = Current;
            string configured = wasOn
                ? data.floorLampTurnOffText
                : data.floorLampTurnOnText;
            return new HubDialogueDefinition(
                "home-floor-lamp",
                Prefer(data.speakerName, "Keiko"),
                Prefer(configured, fallbackText));
        }

        public static HubDialogueDefinition GuildIntro()
        {
            return Resolve(
                "hub-guild-intro",
                "Ага… Мина что-то говорила про ноутбук. Надо посмотреть, что там.");
        }

        public static HubDialogueConfigData NormalizeForAuthoring(
            HubDialogueConfigData data)
        {
            if (data == null)
                data = new HubDialogueConfigData();

            bool legacyLayout = data.schemaVersion < 2;

            if (data.layoutRoot == null)
                data.layoutRoot =
                    new HubDialogueRectData(0f, 0f, 1920f, 1080f);
            if (data.plaque == null)
                data.plaque =
                    new HubDialogueRectData(370f, 635f, 1180f, 393f);
            if (data.plaqueArt == null)
                data.plaqueArt =
                    new HubDialogueRectData(0f, 0f, 1180f, 393f);
            if (data.portraitMask == null)
                data.portraitMask =
                    new HubDialogueRectData(34f, 112f, 178f, 184f);
            if (data.portrait == null)
                data.portrait =
                    new HubDialogueRectData(-12f, -9f, 202f, 202f);
            if (data.speaker == null)
                data.speaker =
                    new HubDialogueRectData(238f, 128f, 530f, 42f);
            if (data.dialogue == null)
                data.dialogue =
                    new HubDialogueRectData(238f, 151f, 760f, 150f);
            if (data.arrowCover == null)
                data.arrowCover =
                    new HubDialogueRectData(1057f, 249f, 52f, 54f);
            if (data.completionArrow == null)
                data.completionArrow =
                    new HubDialogueRectData(1068f, 260f, 27f, 31f);
            if (data.muteButton == null)
                data.muteButton =
                    new HubDialogueRectData(966f, 73f, 49f, 49f);
            if (data.forwardButton == null)
                data.forwardButton =
                    new HubDialogueRectData(1021f, 73f, 49f, 49f);
            if (data.menuButton == null)
                data.menuButton =
                    new HubDialogueRectData(1075f, 73f, 49f, 49f);

            data.layoutScale =
                Mathf.Clamp(
                    data.layoutScale <= 0f
                        ? 1f
                        : data.layoutScale,
                    .2f,
                    3f);

            if (legacyLayout)
            {
                UpgradeLegacyRect(data.layoutRoot);
                UpgradeLegacyRect(data.plaque);
                UpgradeLegacyRect(data.plaqueArt);
                UpgradeLegacyRect(data.portraitMask);
                UpgradeLegacyRect(data.portrait);
                UpgradeLegacyRect(data.speaker);
                UpgradeLegacyRect(data.dialogue);
                UpgradeLegacyRect(data.arrowCover);
                UpgradeLegacyRect(data.completionArrow);
                UpgradeLegacyRect(data.muteButton);
                UpgradeLegacyRect(data.forwardButton);
                UpgradeLegacyRect(data.menuButton);

                // v1 stored this one scale outside the RectTransform.
                data.layoutRoot.scaleX = data.layoutScale;
                data.layoutRoot.scaleY = data.layoutScale;

                // Preserve the exact legacy visual defaults while moving them
                // into the authoritative v2 visual state.
                data.portrait.uvX = 0f;
                data.portrait.uvY = 0f;
                data.portrait.uvWidth = 1f / 6f;
                data.portrait.uvHeight = .5f;

                data.speaker.colorR = .90f;
                data.speaker.colorG = .96f;
                data.speaker.colorB = 1f;
                data.speaker.colorA = 1f;

                data.arrowCover.uvX = 1680f / 2048f;
                data.arrowCover.uvY = 155f / 682f;
                data.arrowCover.uvWidth = 90f / 2048f;
                data.arrowCover.uvHeight = 90f / 682f;

                data.schemaVersion = 2;
            }

            data.speakerFontSize =
                Mathf.Clamp(
                    data.speakerFontSize <= 0
                        ? 21
                        : data.speakerFontSize,
                    8,
                    96);
            data.dialogueFontSize =
                Mathf.Clamp(
                    data.dialogueFontSize <= 0
                        ? 20
                        : data.dialogueFontSize,
                    8,
                    96);
            data.dialogueLineSpacing =
                Mathf.Clamp(
                    data.dialogueLineSpacing <= 0f
                        ? 1f
                        : data.dialogueLineSpacing,
                    .5f,
                    3f);
            data.idleFramesPerSecond =
                Mathf.Max(.01f, data.idleFramesPerSecond);
            data.talkFramesPerSecond =
                Mathf.Max(.01f, data.talkFramesPerSecond);
            data.voiceVolume =
                Mathf.Clamp01(data.voiceVolume);
            data.voicePitch =
                Mathf.Clamp(
                    data.voicePitch == 0f
                        ? 1f
                        : data.voicePitch,
                    .25f,
                    3f);
            data.completionBobScale =
                Mathf.Clamp(
                    data.completionBobScale,
                    0f,
                    4f);
            data.completionPulseScale =
                Mathf.Clamp(
                    data.completionPulseScale,
                    0f,
                    4f);
            data.speakerName =
                Prefer(data.speakerName, "Keiko");
            data.voiceResourcePath =
                Prefer(
                    data.voiceResourcePath,
                    "HubDialogue/KeikoTextVoice");
            return data;
        }

        private static void UpgradeLegacyRect(
            HubDialogueRectData data)
        {
            if (data == null) return;

            data.exists = true;
            data.active = true;
            data.anchoredZ = 0f;
            data.rotationX = 0f;
            data.rotationY = 0f;
            data.scaleX = 1f;
            data.scaleY = 1f;
            data.scaleZ = 1f;
            data.anchorMinX = 0f;
            data.anchorMinY = 1f;
            data.anchorMaxX = 0f;
            data.anchorMaxY = 1f;
            data.pivotX = 0f;
            data.pivotY = 1f;
            data.siblingIndex = -1;
            data.graphicEnabled = true;
            data.colorR = 1f;
            data.colorG = 1f;
            data.colorB = 1f;
            data.colorA = 1f;
            data.uvX = 0f;
            data.uvY = 0f;
            data.uvWidth = 1f;
            data.uvHeight = 1f;
        }

        private static string Prefer(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
