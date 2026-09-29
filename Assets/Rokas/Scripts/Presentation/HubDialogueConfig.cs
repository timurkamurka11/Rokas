using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    [Serializable]
    public sealed class HubDialogueRectData
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public HubDialogueRectData() { }

        public HubDialogueRectData(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }

    [Serializable]
    public sealed class HubDialogueConfigData
    {
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
            new HubDialogueRectData(-12f, -9f, 202f, 202f);
        public HubDialogueRectData speaker =
            new HubDialogueRectData(238f, 128f, 530f, 42f);
        public int speakerFontSize = 21;
        public TextAnchor speakerAlignment = TextAnchor.MiddleLeft;
        public HubDialogueRectData dialogue =
            new HubDialogueRectData(238f, 151f, 760f, 150f);
        public int dialogueFontSize = 20;
        public TextAnchor dialogueAlignment = TextAnchor.UpperLeft;
        public HubDialogueRectData arrowCover =
            new HubDialogueRectData(1057f, 249f, 52f, 54f);
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
            return Normalize(data ?? new HubDialogueConfigData());
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

        private static HubDialogueConfigData Normalize(HubDialogueConfigData data)
        {
            if (data.layoutRoot == null) data.layoutRoot = new HubDialogueRectData(0f, 0f, 1920f, 1080f);
            if (data.plaque == null) data.plaque = new HubDialogueRectData(370f, 635f, 1180f, 393f);
            if (data.plaqueArt == null) data.plaqueArt = new HubDialogueRectData(0f, 0f, 1180f, 393f);
            if (data.portraitMask == null) data.portraitMask = new HubDialogueRectData(34f, 112f, 178f, 184f);
            if (data.portrait == null) data.portrait = new HubDialogueRectData(-12f, -9f, 202f, 202f);
            if (data.speaker == null) data.speaker = new HubDialogueRectData(238f, 128f, 530f, 42f);
            if (data.dialogue == null) data.dialogue = new HubDialogueRectData(238f, 151f, 760f, 150f);
            if (data.arrowCover == null) data.arrowCover = new HubDialogueRectData(1057f, 249f, 52f, 54f);
            if (data.completionArrow == null) data.completionArrow = new HubDialogueRectData(1068f, 260f, 27f, 31f);
            if (data.muteButton == null) data.muteButton = new HubDialogueRectData(966f, 73f, 49f, 49f);
            if (data.forwardButton == null) data.forwardButton = new HubDialogueRectData(1021f, 73f, 49f, 49f);
            if (data.menuButton == null) data.menuButton = new HubDialogueRectData(1075f, 73f, 49f, 49f);
            data.layoutScale = Mathf.Clamp(data.layoutScale <= 0f ? 1f : data.layoutScale, .2f, 3f);
            data.speakerFontSize = Mathf.Clamp(data.speakerFontSize <= 0 ? 21 : data.speakerFontSize, 8, 96);
            data.dialogueFontSize = Mathf.Clamp(data.dialogueFontSize <= 0 ? 20 : data.dialogueFontSize, 8, 96);
            data.idleFramesPerSecond = Mathf.Max(.01f, data.idleFramesPerSecond);
            data.talkFramesPerSecond = Mathf.Max(.01f, data.talkFramesPerSecond);
            data.voiceVolume = Mathf.Clamp01(data.voiceVolume);
            data.voicePitch = Mathf.Clamp(data.voicePitch == 0f ? 1f : data.voicePitch, .25f, 3f);
            data.completionBobScale = Mathf.Clamp(data.completionBobScale, 0f, 4f);
            data.completionPulseScale = Mathf.Clamp(data.completionPulseScale, 0f, 4f);
            data.speakerName = Prefer(data.speakerName, "Keiko");
            data.voiceResourcePath = Prefer(data.voiceResourcePath, "HubDialogue/KeikoTextVoice");
            return data;
        }

        private static string Prefer(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
