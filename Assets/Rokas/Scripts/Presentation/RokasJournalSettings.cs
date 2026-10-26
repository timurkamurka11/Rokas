using System;
using UnityEngine;

namespace Rokas.Presentation
{
    // Content and layout are editable separately from the source artwork.
    // The three original book/panel images never contain baked UI text.
    [CreateAssetMenu(fileName = "JournalSettings", menuName = "ROKAS/Quest Journal Settings")]
    public sealed class RokasJournalSettings : ScriptableObject
    {
        public const string ResourcePath = "Rokas/Journal/JournalSettings";

        [Header("Original artwork & media")]
        public Texture2D avatar;
        public AudioClip openSound;
        [Range(0f, 1f)] public float openVolume = .78f;

        [Header("Hud book (1920 x 1080 authored coordinates)")]
        public Vector2 iconPosition = new Vector2(34f, 26f);
        [Range(80f, 320f)] public float iconSize = 166f;
        [Range(0f, .25f)] public float hoverScale = .072f;
        [Range(0f, 20f)] public float hoverRise = 5f;
        [Range(0f, 18f)] public float hoverFloatAmplitude = 5f;
        [Range(.1f, 5f)] public float hoverFloatHz = .45f;
        [Range(1f, 24f)] public float hoverResponse = 13f;
        [Range(0f, 1f)] public float hoverGlow = .19f;

        [Header("Window and transitions")]
        public Vector2 panelPosition = new Vector2(360f, 90f);
        public Vector2 panelSize = new Vector2(1200f, 900f);
        [Range(0f, .9f)] public float backdropDim = .58f;
        [Range(.2f, 15f)] public float openSpeed = 5f;
        [Range(0f, 50f)] public float panelSlide = 18f;
        [Range(0f, .2f)] public float panelScaleIntro = .035f;
        public Color selectionColor = new Color(1f, .62f, .19f, .12f);

        [Header("Portrait / round mask in panel-local coordinates")]
        public bool showAvatar = true;
        public Rect avatarRect = new Rect(84f, 138f, 184f, 184f);
        public Color avatarTint = Color.white;

        [Header("Text (added in Unity, never baked into image)")]
        public Font font;
        public Color textColor = new Color(.87f, .94f, 1f, 1f);
        public Color mutedTextColor = new Color(.57f, .72f, .83f, 1f);
        public string characterName = "";
        public string journalTitle = "";
        [TextArea(2, 7)] public string questDescription = "";
        [TextArea(2, 5)] public string notes = "";
        public Rect characterNameRect = new Rect(59f, 92f, 270f, 47f);
        public Rect journalTitleRect = new Rect(395f, 100f, 710f, 54f);
        public Rect questDescriptionRect = new Rect(406f, 246f, 685f, 250f);
        public Rect notesRect = new Rect(407f, 658f, 678f, 128f);
        [Range(12, 60)] public int headingFontSize = 30;
        [Range(12, 42)] public int bodyFontSize = 22;
        [Range(10, 40)] public int categoryFontSize = 20;
        public bool showText = true;

        [Header("Six navigation categories")]
        public Rect categoryListRect = new Rect(49f, 330f, 255f, 65f);
        [Range(40f, 130f)] public float categoryStep = 75f;
        public string[] categoryTitles = new string[6];
        public string[] questObjectives = new string[4];

        [Header("Objectives on the right")]
        public Rect objectivesStartRect = new Rect(407f, 532f, 670f, 40f);
        [Range(26f, 85f)] public float objectiveSpacing = 44f;
        public bool showCheckboxes = true;

        public static string CategoryAt(string[] source, int index)
        {
            return source != null && index >= 0 && index < source.Length
                ? source[index] ?? string.Empty : string.Empty;
        }

        public void Normalize()
        {
            if (categoryTitles == null || categoryTitles.Length != 6)
                categoryTitles = Resize(categoryTitles, 6);
            if (questObjectives == null || questObjectives.Length != 4)
                questObjectives = Resize(questObjectives, 4);
            panelSize.x = Mathf.Clamp(panelSize.x, 600f, 1800f);
            panelSize.y = Mathf.Clamp(panelSize.y, 450f, 1050f);
            iconSize = Mathf.Clamp(iconSize, 80f, 320f);
            avatarRect.width = Mathf.Max(10f, avatarRect.width);
            avatarRect.height = Mathf.Max(10f, avatarRect.height);
        }

        private static string[] Resize(string[] previous, int count)
        {
            var result = new string[count];
            if (previous != null)
                Array.Copy(previous, result, Mathf.Min(previous.Length, count));
            for (int i = 0; i < result.Length; ++i)
                if (result[i] == null) result[i] = "";
            return result;
        }

        private void OnValidate() { Normalize(); }
    }
}
