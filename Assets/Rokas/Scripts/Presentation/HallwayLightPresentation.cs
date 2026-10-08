using System;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // The two approved, architecturally-aligned photographs represent genuine light states.
    // There are no dark circles, black rectangles, or CPU frame readbacks in the runtime.
    public sealed class HallwayLightPresentation
    {
        public const string OffResource = "Home/HallwayLightsOff";
        public const string ShaderResource = "Shaders/HallwayLightStates";
        private Material owned;
        private RawImage image;
        private float mainOn;
        private float hallOn;
        private float targetMain;
        private float targetHall;

        public static Texture2D OffPhoto => Resources.Load<Texture2D>(OffResource);
        public Material Material => owned;

        public void Bind(RawImage background, bool mainRoomLight, bool hallwayLight)
        {
            Unbind();
            var off = OffPhoto;
            var shader = Resources.Load<Shader>(ShaderResource);
            if (!background || !off || !shader)
                throw new InvalidOperationException("Missing Hallway background, HallwayLightsOff photo or HallwayLightStates shader.");
            if (off.width != 1920 || off.height != 1080)
                throw new InvalidOperationException("Hallway OFF photo must match the 1920x1080 ON photo.");

            image = background;
            owned = new Material(shader) { name = "HallwayTwoRoomPhotographicLight",
                hideFlags = HideFlags.DontSave };
            owned.SetTexture("_OffTex",off);
            mainOn = targetMain = mainRoomLight ? 1f : 0f;
            hallOn = targetHall = hallwayLight ? 1f : 0f;
            owned.SetFloat("_MainRoomOn",mainOn);
            owned.SetFloat("_HallwayOn",hallOn);
            background.material = owned;
            background.SetMaterialDirty();
        }

        public void SetStates(bool mainRoomLight, bool hallwayLight)
        {
            targetMain = mainRoomLight ? 1f : 0f;
            targetHall = hallwayLight ? 1f : 0f;
        }

        public void Tick(float dt)
        {
            if (!owned) return;
            // Smooth only while still in the room. Opening a room uses its saved states
            // immediately, preventing a one-frame flash after transitions.
            float t = Mathf.Clamp01(dt / .16f);
            mainOn = Mathf.Lerp(mainOn,targetMain,t);
            hallOn = Mathf.Lerp(hallOn,targetHall,t);
            owned.SetFloat("_MainRoomOn",mainOn);
            owned.SetFloat("_HallwayOn",hallOn);
        }

        public void Unbind()
        {
            if (image && image.material == owned)
            {
                image.material = null;
                image.SetMaterialDirty();
            }
            if (owned) UnityEngine.Object.Destroy(owned);
            owned = null;
            image = null;
        }
    }

    // The MainRoom photograph already depicts its hallway through the right doorway.
    // Composite the ENTIRE visible hallway (including its curtain) from the
    // existing ON/OFF photographs; don't leave half the curtain controlled by
    // MainRoomLight and the other half controlled by HallwayLight.
    public sealed class HomeHallwayDoorwayPresentation : MonoBehaviour
    {
        private HomeDoorwayPhotoGraphic cutout;
        private Texture homeOn;
        private Texture homeOff;

        public static HomeHallwayDoorwayPresentation Create(UiKit ui, RectTransform root, Texture on)
        {
            Texture off = Resources.Load<Texture2D>("Home/ApartmentNightLightOff");
            if (!on || !off) throw new InvalidOperationException("Home light-state photos are required.");
            var rect = ui.Rect(root, "MainRoomHallwayPhotographicPortal", 0f,0f,1920f,1080f);
            rect.gameObject.AddComponent<CanvasRenderer>();
            var overlay = rect.gameObject.AddComponent<HomeHallwayDoorwayPresentation>();
            overlay.homeOn = on;
            overlay.homeOff = off;
            overlay.cutout = rect.gameObject.AddComponent<HomeDoorwayPhotoGraphic>();
            overlay.cutout.raycastTarget = false;
            return overlay;
        }

        public void SetHallwayState(bool on)
        {
            if (cutout) cutout.Photo = on ? homeOn : homeOff;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HomeDoorwayPhotoGraphic : MaskableGraphic
    {
        private Texture photo;
        public override Texture mainTexture => photo ? photo : Texture2D.whiteTexture;
        public Texture Photo
        {
            get => photo;
            set
            {
                if (photo == value) return;
                photo = value;
                SetMaterialDirty();
                SetVerticesDirty();
            }
        }


        private static readonly Vector2[] Doorway = {
            // The entire visible Hallway, INCLUDING the hanging curtain,
            // belongs to HallwayLight. The former right edge (x~1712-1768)
            // stopped halfway across the curtain. Its exposed ON/OFF photo
            // formed the rectangular lighting mismatch seen in both states.
            // These authored 1920x1080 points follow the upper inside frame,
            // the right inner jamb and the curtain's hanging bottom edge.
            // The top follows the actual INNER illuminated lintel rather
            // than leaving a horizontal 12-20px MainRoom-lit strip on the
            // curtain. The right edge stops just inside the vertical frame.
            new Vector2(1622f,72f), new Vector2(1712f,58f),
            new Vector2(1872f,30f), new Vector2(1873f,390f),
            // Continue the Hallway light cutout around the photographed
            // plant/lantern niche immediately left of the television.  The
            // prior contour stepped to x=1751 by y=466, exposing the warm
            // MainRoom photograph inside a Hallway-OFF niche (and vice versa).
            // Stop before the TV glass and the foreground cabinet face.
            new Vector2(1850f,394f), new Vector2(1800f,398f),
            new Vector2(1796f,404f), new Vector2(1795f,433f),
            new Vector2(1795f,454f), new Vector2(1794f,466f),
            // Keep covering the real lamp base and potted niche until
            // its bottom shelf, feathering back before the TV/cabinet face.
            new Vector2(1794f,483f), new Vector2(1784f,492f),
            new Vector2(1769f,501f), new Vector2(1751f,505f),
            // Bottom visible Hallway follows the photographed wooden sill,
            // from the left inner jamb (~1618,623) to the right (~1740,645).
            // The previous flat 614-618 cutoff left a 10-25px lit floor strip.
            // Fill the final lower-right 11px of the visible Hallway without
            // extending the image into the curtain, doorframe or cabinet.
            new Vector2(1751f,645f), new Vector2(1700f,636f),
            new Vector2(1660f,629f), new Vector2(1618f,621f)
        };
        public static float CoverageAt(float x,float y)
        {
            var p=new Vector2(x,y);
            bool inside=false;
            float closest=float.MaxValue;
            for(int i=0,j=Doorway.Length-1;i<Doorway.Length;j=i++)
            {
                var a=Doorway[j];var b=Doorway[i];var e=b-a;
                if((a.y>y)!=(b.y>y) && x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x)
                    inside=!inside;
                float u=Mathf.Clamp01(Vector2.Dot(p-a,e)/e.sqrMagnitude);
                closest=Mathf.Min(closest,(p-(a+u*e)).sqrMagnitude);
            }
            return inside ? Mathf.SmoothStep(0f,1f,Mathf.Sqrt(closest)/4f):0f;
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Include the full sloping sill. y1=630 previously clipped the
            // mesh even if the polygon was extended below it.
            // Extend the mesh to the INNER right doorframe. The old x1=1776
            // clipped any geometry beyond mid-curtain regardless of polygon.
            const float x0=1608f,y0=24f,x1=1883f,y1=652f;
            const int step=3;
            int nx=Mathf.CeilToInt((x1-x0)/step);
            int ny=Mathf.CeilToInt((y1-y0)/step);
            Rect rect=GetPixelAdjustedRect();
            for(int y=0;y<=ny;y++)
            {
                float py=Mathf.Min(y1,y0+y*step);
                for(int x=0;x<=nx;x++)
                {
                    float px=Mathf.Min(x1,x0+x*step);
                    Color c=color;c.a*=CoverageAt(px,py);
                    mesh.AddVert(new Vector3(rect.xMin+px,rect.yMax-py),
                        c,new Vector2(px/1920f,1f-py/1080f));
                }
            }
            for(int y=0;y<ny;y++)
            for(int x=0;x<nx;x++)
            {
                int a=y*(nx+1)+x;int b=a+nx+1;
                mesh.AddTriangle(a,b,a+1);
                mesh.AddTriangle(a+1,b,b+1);
            }
        }
    }
}
