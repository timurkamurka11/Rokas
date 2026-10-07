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
    // Composite ONLY that doorway from the existing ON/OFF main-room photographs;
    // never draw a dark geometric overlay across unrelated parts of the home.
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

        // Tight strip inside the existing Home opening. Outer boundary is alpha zero;
        // a 5px inner ring prevents a seam on the frame without a circular mask.
        private static readonly Vector2[] Outer = {
            new Vector2(1403,42), new Vector2(1620,30),
            new Vector2(1618,618), new Vector2(1410,625)
        };
        private static readonly Vector2[] Inner = {
            new Vector2(1411,48), new Vector2(1612,39),
            new Vector2(1610,609), new Vector2(1417,616)
        };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            // Alpha-feathered polygon patch with full-scene UV mapping.
            for(int i=0;i<4;i++) AddVertex(mesh,Outer[i],0f);
            for(int i=0;i<4;i++) AddVertex(mesh,Inner[i],1f);
            for(int i=0;i<4;i++)
            {
                int next=(i+1)%4;
                mesh.AddTriangle(i,next,4+next);
                mesh.AddTriangle(i,4+next,4+i);
            }
            mesh.AddTriangle(4,5,6);
            mesh.AddTriangle(4,6,7);
        }

        private void AddVertex(VertexHelper mesh,Vector2 point,float alpha)
        {
            Rect rect = GetPixelAdjustedRect();
            Color c = color;
            c.a *= alpha;
            mesh.AddVert(new Vector3(rect.xMin+point.x,rect.yMax-point.y),
                c,new Vector2(point.x/1920f,1f-point.y/1080f));
        }
    }
}
