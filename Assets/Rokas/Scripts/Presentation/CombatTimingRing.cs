using UnityEngine;
using UnityEngine.UI;
namespace Rokas.Presentation
{
    // A small uGUI mesh keeps timing circles crisp without new textures or materials.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CombatTimingRing : MaskableGraphic
    {
        private float progress;
        private float windowStart;
        private float windowEnd;
        private Color accent = UiKit.Gold;
        public void Set(float value, float start, float end, Color tint)
        {
            progress = Mathf.Clamp01(value); windowStart = start; windowEnd = end; accent = tint; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            const int segments = 80;
            var rect = rectTransform.rect;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            float inner = radius - 9;
            for (int i = 0; i < segments; i++)
            {
                float fraction = (i + .5f) / segments;
                Color tint = fraction <= progress ? accent : new Color(.32f,.43f,.43f,.45f);
                if (fraction >= windowStart && fraction <= windowEnd) tint = UiKit.Paper;
                float a = Mathf.PI * .5f - i * Mathf.PI * 2 / segments;
                float b = Mathf.PI * .5f - (i + 1) * Mathf.PI * 2 / segments;
                int index = mesh.currentVertCount;
                mesh.AddVert(center + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*inner,tint,Vector2.zero);
                mesh.AddVert(center + new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);
                mesh.AddVert(center + new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,tint,Vector2.zero);
                mesh.AddVert(center + new Vector2(Mathf.Cos(b),Mathf.Sin(b))*inner,tint,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2); mesh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
