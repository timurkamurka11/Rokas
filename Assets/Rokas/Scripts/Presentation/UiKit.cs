using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Authored canvas coordinates use a top-left origin, independent of screen aspect ratio.
    public sealed class UiKit
    {
        public static readonly Color Ink = new Color(.035f, .055f, .06f, .96f);
        public static readonly Color Paper = new Color(.91f, .875f, .77f);
        public static readonly Color Muted = new Color(.56f, .65f, .64f);
        public static readonly Color Gold = new Color(.82f, .64f, .35f);
        public static readonly Color Jade = new Color(.42f, .81f, .72f);
        public static readonly Color Red = new Color(.72f, .27f, .22f);
        private readonly RokasAssets assets;
        private readonly Action clickSound;

        public UiKit(RokasAssets assets, Action clickSound)
        {
            this.assets = assets;
            this.clickSound = clickSound;
        }

        public RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        public Image Box(Transform parent, string name, float x, float y, float w, float h, Color color, bool blocks = false)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocks;
            return image;
        }

        public RawImage Art(Transform parent, string name, Texture texture, float x, float y, float w, float h)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        public Text Label(Transform parent, string name, string value, float x, float y, float w, float h,
            int size = 24, Color? color = null, bool display = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = display ? assets.serif : assets.sans;
            text.fontSize = size;
            text.color = color ?? Paper;
            text.text = value;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        public Button Button(Transform parent, string name, string title, float x, float y, float w, float h,
            Action action, bool primary = false, bool enabled = true)
        {
            Color baseColor = primary ? new Color(.17f, .32f, .29f, .98f) : new Color(.075f, .105f, .11f, .98f);
            var background = Box(parent, name, x, y, w, h, baseColor, true);
            var button = background.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.3f, 1.3f, 1.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.65f, .85f, .8f);
            colors.disabledColor = new Color(.45f, .5f, .5f, .65f);
            colors.fadeDuration = .14f;
            button.colors = colors;
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var edge = primary ? Jade : Gold;
            Box(background.transform, "TopRule", 0, 0, w, 1, edge * new Color(1, 1, 1, .65f));
            Box(background.transform, "LeftRule", 0, 0, 3, h, edge);
            Label(background.transform, "Title", title, 20, 0, w - 40, h, 22);
            button.onClick.AddListener(() => { clickSound?.Invoke(); action?.Invoke(); });
            var feedback = background.gameObject.AddComponent<InteractionFeedback>();
            feedback.Initialize(background.rectTransform);
            return button;
        }

        public RectTransform Panel(Transform parent, string name, float x, float y, float w, float h)
        {
            Box(parent, "PanelShadow", x + 15, y + 18, w, h, new Color(0, 0, 0, .38f));
            var panel = Box(parent, name, x, y, w, h, Ink, true).rectTransform;
            Box(panel, "TopRule", 0, 0, w, 2, Gold);
            Box(panel, "BottomRule", 0, h - 1, w, 1, new Color(.35f, .42f, .41f));
            Box(panel, "Accent", 0, 0, 8, 58, Red);
            return panel;
        }

        public void Clear(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }
    }

    public sealed class InteractionFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private RectTransform rect;
        private Vector2 origin;
        private bool hovered;
        public void Initialize(RectTransform value) { rect = value; origin = value.anchoredPosition; }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; }
        public void OnSelect(BaseEventData e) { hovered = true; }
        public void OnDeselect(BaseEventData e) { hovered = false; }
        private void Update()
        {
            if (rect) rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition,
                origin + (hovered ? new Vector2(3, 0) : Vector2.zero), Time.unscaledDeltaTime * 14);
        }
    }
}
