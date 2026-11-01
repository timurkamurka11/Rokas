using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Additive cosmetic component; leaves the actual YES/NO actions intact.
    public sealed class RokasCyberNewGameConfirmationSkin : MonoBehaviour
    {
        private Transform styled;
        private Texture2D art;
        private bool loaded;
        private static readonly Color Cyan = new Color(.12f, .91f, .97f, 1f);
        private static readonly Color Gold = new Color(1f, .69f, .31f, 1f);

        private void LateUpdate()
        {
            Transform modal = transform.Find("NewGameConfirmationDialog");
            if (modal == null || !modal.gameObject.activeInHierarchy)
            {
                styled = null;
                return;
            }
            if (ReferenceEquals(styled, modal)) return;
            if (!loaded) { art = Resources.Load<Texture2D>("Settings/CyberSettingsBackdropV2"); loaded = true; }
            Transform old = modal.Find("NewGameQuestionCard");
            if (old != null)
            {
                Graphic g = old.GetComponent<Graphic>();
                if (g != null) g.enabled = false;
            }
            var surface = new GameObject("CyberNewGameCard", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            RectTransform r = (RectTransform)surface.transform;
            r.SetParent(modal, false);
            r.anchorMin = r.anchorMax = new Vector2(.5f, .5f);
            r.pivot = new Vector2(.5f, .5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(960, 575);
            RawImage image = surface.GetComponent<RawImage>();
            image.texture = art;
            image.color = art != null ? Color.white : new Color(.01f, .04f, .052f, .99f);
            image.raycastTarget = false;
            r.SetSiblingIndex(old != null ? old.GetSiblingIndex() + 1 : 0);
            Transform question = modal.Find("NewGameQuestion");
            if (question != null)
            {
                Text text = question.GetComponent<Text>();
                if (text != null)
                {
                    text.text = "Вы действительно хотите начать сначала?";
                    text.fontSize = 31;
                    text.fontStyle = FontStyle.Bold;
                    text.color = new Color(.94f, .97f, .98f, 1f);
                }
                RectTransform rect = question as RectTransform;
                if (rect != null) { rect.sizeDelta = new Vector2(810f, 128f); rect.anchoredPosition = new Vector2(0, 126); }
            }
            Style(modal.Find("ConfirmNewGameYes"), Cyan, new Color(.008f, .15f, .17f, .98f));
            Style(modal.Find("ConfirmNewGameNo"), Gold, new Color(.13f, .083f, .047f, .98f));
            styled = modal;
        }

        private static void Style(Transform t, Color accent, Color fill)
        {
            if (t == null) return;
            RectTransform rect = t as RectTransform;
            if (rect == null) return;
            rect.sizeDelta = new Vector2(610, 75);
            foreach (Graphic g in t.GetComponentsInChildren<Graphic>(true))
            {
                if (g.name.EndsWith("Face", StringComparison.Ordinal) ||
                    g.name.EndsWith("Glow", StringComparison.Ordinal) ||
                    g.name.EndsWith("Edge", StringComparison.Ordinal))
                    g.color = Color.clear;
            }
            if (t.Find("CyberNewGameBevel") != null) return;
            var obj = new GameObject("CyberNewGameBevel", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RokasCyberConfirmBevelGraphic));
            RectTransform border = (RectTransform)obj.transform;
            border.SetParent(t, false);
            border.anchorMin = Vector2.zero;
            border.anchorMax = Vector2.one;
            border.offsetMin = border.offsetMax = Vector2.zero;
            border.SetAsFirstSibling();
            RokasCyberConfirmBevelGraphic shape = obj.GetComponent<RokasCyberConfirmBevelGraphic>();
            shape.Initialize(accent, fill);
            t.gameObject.AddComponent<RokasCyberConfirmButtonInteraction>().Initialize(shape);
        }
    }

    public sealed class RokasCyberConfirmButtonInteraction : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private RokasCyberConfirmBevelGraphic shape;
        private bool hovered, selected;
        public void Initialize(RokasCyberConfirmBevelGraphic graphic) { shape = graphic; }
        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; }
        private void Update() { if (shape != null) shape.Animate(hovered || selected, Time.unscaledDeltaTime); }
    }

    public sealed class RokasCyberConfirmBevelGraphic : MaskableGraphic
    {
        private Color borderColor, fillColor;
        private float intensity;
        public void Initialize(Color border, Color fill)
        {
            borderColor = border; fillColor = fill; raycastTarget = false; SetVerticesDirty();
        }
        public void Animate(bool active, float dt)
        {
            float n = Mathf.MoveTowards(intensity, active ? 1f : 0f, dt * 5f);
            if (Mathf.Abs(n - intensity) < .0001f) return;
            intensity = n;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float cut = Mathf.Min(14f, r.height * .21f), d = 2.2f;
            Color edge = new Color(Mathf.Clamp01(borderColor.r * (1f + .28f * intensity)),
                Mathf.Clamp01(borderColor.g * (1f + .28f * intensity)),
                Mathf.Clamp01(borderColor.b * (1f + .28f * intensity)), 1f);
            Color face = Color.Lerp(fillColor,
                new Color(fillColor.r * 1.25f, fillColor.g * 1.6f, fillColor.b * 1.5f, .99f), intensity);
            Vector2[] outer = Points(r, cut);
            Vector2[] inner = Points(new Rect(r.x + d, r.y + d, r.width - 2*d, r.height - 2*d), cut*.81f);
            for (int i = 0; i < 8; i++) { vh.AddVert(outer[i], edge, Vector2.zero); vh.AddVert(inner[i], face, Vector2.zero); }
            for (int i = 0; i < 8; i++)
            {
                int a = i*2, b = ((i+1)%8)*2;
                vh.AddTriangle(a,b,a+1); vh.AddTriangle(b,b+1,a+1);
            }
            int center = vh.currentVertCount;
            vh.AddVert(r.center,face,Vector2.zero);
            for (int i = 0; i < 8; i++) vh.AddTriangle(center,i*2+1,((i+1)%8)*2+1);
        }
        private static Vector2[] Points(Rect r, float cut)
        {
            return new[] {
                new Vector2(r.xMin+cut,r.yMin), new Vector2(r.xMax-cut,r.yMin),
                new Vector2(r.xMax,r.yMin+cut), new Vector2(r.xMax,r.yMax-cut),
                new Vector2(r.xMax-cut,r.yMax), new Vector2(r.xMin+cut,r.yMax),
                new Vector2(r.xMin,r.yMax-cut), new Vector2(r.xMin,r.yMin+cut)
            };
        }
    }
}
