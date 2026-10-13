using UnityEngine;

namespace Rokas.Presentation
{
    /// <summary>
    /// Full-resolution RGBA32 4x2 atlas, faithfully evaluated from the
    /// eight shapes in the user's RokasHubDust EditorTools source.
    /// 1024x512, 256x256 per cell, 8-bit opacity and transparent guards.
    /// Built only once per live dust graphic; never on every Tick.
    /// </summary>
    public static class HubDustAtlasFactory
    {
        public const int Width = 1024;
        public const int Height = 512;
        public const int CellSize = 256;

        public static Texture2D Create()
        {
            var pixels = new Color32[Width * Height];
            for (int row = 0; row < 2; ++row)
            for (int col = 0; col < 4; ++col)
            {
                int shape = row * 4 + col;
                for (int y = 0; y < CellSize; ++y)
                for (int x = 0; x < CellSize; ++x)
                {
                    float u = x / 255f * 2f - 1f;
                    float v = y / 255f * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    // Source editor math, no tiny downsample / Q32 alpha.
                    float alpha = EvaluateAtlasAlpha(shape,u,v);
                    pixels[(row * CellSize + y) * Width +
                           (col * CellSize + x)] =
                        new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255f));
                }
            }
            var tex = new Texture2D(Width,Height,TextureFormat.RGBA32,
                false,false)
            {
                name = "T_RokasDustAtlas_4x2_Runtime_RGBA32",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 1,
                hideFlags = HideFlags.DontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        public static float EvaluateAtlasAlpha(int id,float u,float v)
        {
            float r=Mathf.Sqrt(u*u+v*v);
            if (r>=.88f) return 0f;
            float guard=Mathf.Clamp01((.88f-r)/.22f);
            guard=guard*guard*(3f-2f*guard);
            return Mathf.Clamp01(EvaluateCellShape(id,u,v,r)*guard);
        }

        private static float EvaluateCellShape(int id,float u,float v,float r)
        {
            float theta = Mathf.Atan2(v,u);
            switch (id)
            {
                case 0:
                    return Mathf.Exp(-r*r*6.5f);
                case 1:
                {
                    float t=r/.68f, core=Mathf.Clamp01(1f-t*t*t*t);
                    float d=(r-.56f)/.075f;
                    float rim=Mathf.Exp(-.5f*d*d)*.45f;
                    return Mathf.Clamp01((core*.65f+rim)*
                        (.88f+.12f*Mathf.Cos(theta-.6f)));
                }
                case 2:
                {
                    float d=r*(1f+.16f*Mathf.Sin(3f*theta+.5f)+
                        .09f*Mathf.Cos(5f*theta-1.2f));
                    return Mathf.Exp(-d*d*5.2f);
                }
                case 3:
                {
                    float ur=u*.900447f-v*.434966f;
                    float vr=u*.434966f+v*.900447f;
                    float curve=vr-.28f*ur*ur+.04f;
                    float dx=ur-Mathf.Clamp(ur,-.38f,.38f);
                    float d=dx*dx+curve*curve*3.2f;
                    float end=Mathf.Clamp01(1f-(ur/.44f)*(ur/.44f));
                    return Mathf.Exp(-28f*d)*(.4f+.6f*end);
                }
                case 4:
                {
                    float a=(u-.06f)*1.15f,b=(v+.04f)*.9f;
                    return .75f*Mathf.Exp(-7f*(a*a+b*b))+
                        .25f*Mathf.Exp(-2.8f*r*r);
                }
                case 5:
                {
                    float poly=r*(1f+.04f*Mathf.Cos(6f*theta));
                    float disc=1f/(1f+Mathf.Exp((poly-.58f)*18f));
                    float d=poly-.52f;
                    return Mathf.Clamp01((disc*.62f+
                        Mathf.Exp(-(d*d)/.012f)*.3f)*(.9f+.1f*u));
                }
                case 6:
                {
                    float a=(u+.14f)*(u+.14f)+(v-.08f)*(v-.08f);
                    float b=(u-.18f)*(u-.18f)+(v+.12f)*(v+.12f);
                    return Mathf.Clamp01(Mathf.Exp(-10.5f*a)+
                        .55f*Mathf.Exp(-16f*b));
                }
                default:
                    return Mathf.Exp(-4.75f*r*r);
            }
        }
    }
}
