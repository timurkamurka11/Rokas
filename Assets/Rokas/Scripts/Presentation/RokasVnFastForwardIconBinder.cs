using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Installed on the VN root; cosmetically replaces the copied single-play glyph.
    // The underlying native button, ring, atlas, hover and onClick all remain intact.
    public sealed class RokasVnFastForwardIconBinder : MonoBehaviour
    {
        private Button button;
        private Text marker;
        private RokasVnFastForwardPictogram icon;

        private void LateUpdate()
        {
            if (button == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i] != null && buttons[i].name == "VnFastForwardButton")
                    { button = buttons[i]; break; }
                if (button == null) return;
                Transform oldMark = button.transform.Find("FastForwardModeMark");
                if (oldMark != null) marker = oldMark.GetComponent<Text>();
                var obj = new GameObject("NativeDoubleChevronPictogram",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(RokasVnFastForwardPictogram));
                RectTransform r = (RectTransform)obj.transform;
                r.SetParent(button.transform, false);
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                r.SetAsLastSibling();
                icon = obj.GetComponent<RokasVnFastForwardPictogram>();
                icon.raycastTarget = false;
                if (marker != null) marker.enabled = false;
            }
            if (button == null || icon == null) return;
            bool active = marker != null && marker.color.r > .92f &&
                marker.color.g < .84f && marker.color.b < .63f;
            icon.SetState(button.interactable, active);
        }
    }

    // Vector pictogram uses two actual triangles inside the authentic VN circular frame.
    public sealed class RokasVnFastForwardPictogram : MaskableGraphic
    {
        private bool enabledMode = true;
        private bool active;
        public void SetState(bool enabled, bool running)
        {
            if (enabledMode == enabled && active == running) return;
            enabledMode = enabled; active = running; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float s = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            Color plate = new Color(.055f, .126f, .173f, .99f);
            float radius = s * .305f;
            int baseIndex = vh.currentVertCount;
            vh.AddVert(center, plate, Vector2.zero);
            const int count = 32;
            for (int i = 0; i <= count; i++)
            {
                float a = i * 2f * Mathf.PI / count;
                vh.AddVert(center + radius * new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                    plate, Vector2.zero);
                if (i > 0) vh.AddTriangle(baseIndex, baseIndex + i, baseIndex + i + 1);
            }
            Color arrow = !enabledMode ? new Color(.48f, .61f, .70f, .42f) :
                active ? new Color(1f, .77f, .39f, 1f) : new Color(.85f, .95f, 1f, .98f);
            Triangle(vh, center + new Vector2(-s*.115f, 0f), s, arrow);
            Triangle(vh, center + new Vector2(s*.100f, 0f), s, arrow);
        }
        private static void Triangle(VertexHelper vh, Vector2 c, float s, Color color)
        {
            int n = vh.currentVertCount;
            vh.AddVert(c + new Vector2(-s*.075f,-s*.13f),color,Vector2.zero);
            vh.AddVert(c + new Vector2(-s*.075f, s*.13f),color,Vector2.zero);
            vh.AddVert(c + new Vector2(s*.085f,0f),color,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);
        }
    }
}
