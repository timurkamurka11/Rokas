using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rokas.Presentation
{
    // Seed, ribbons, torn aperture and residual energy share Arena's paused clock.
    // This presentation object cannot move an actor or resolve combat damage.
    public sealed class ReactiveCombatPortalEffect : IDisposable
    {
        private const int Segments = 160, RibbonCount = 7, RibbonSteps = 48;
        private const float CenterHeight = 3.4f;
        private readonly GameObject root;
        private readonly Mesh depthMesh, floorMesh;
        private readonly Mesh[] ribbonMeshes = new Mesh[RibbonCount];
        private readonly Material depthMaterial, energyMaterial, floorMaterial, particleMaterial, distortionMaterial;
        private readonly Material[] ribbonMaterials = new Material[RibbonCount];
        private readonly LineRenderer rimCore, rimHalo, ghostRim;
        private readonly LineRenderer[] forks = new LineRenderer[12];
        private readonly LineRenderer[] depthVeins = new LineRenderer[9];
        private readonly ParticleSystem sparks;
        private readonly Light light;
        private readonly ReactiveCombatEmberLayer wisps, innerVapor, edgeSmoke, collapse;
        private readonly Vector3[] rim = new Vector3[Segments];
        private readonly Vector3[] ribbonVertices = new Vector3[RibbonSteps * 2];
        private readonly Vector3[] forkPoints = new Vector3[10];
        private readonly Vector3[] veinPoints = new Vector3[24];
        private float elapsed, visibility, coreScale, emissionElapsed, closeElapsed;
        private int emitted;
        private bool closing, disposed;
        public Transform Transform => root == null ? null : root.transform;
        public float Visibility => visibility;
        public bool IsClosed => visibility <= .001f;
        public float PresentationElapsed => elapsed;
        public int ParticleCount => sparks == null ? 0 : sparks.particleCount;
        public Vector3 FloorPosition => root.transform.position - Vector3.up * CenterHeight;
        public Vector3 ApertureCenter => root.transform.position;
        public Vector3 PlaneNormal => -root.transform.forward;
        public Vector3 PlaneRight => root.transform.right;
        public Vector2 ApertureRadii => new Vector2(3.15f, 3.35f) * coreScale;
        public float MaskDepth => .85f;
        public static float AngularBoundary(float angle, float phase)
        {
            return 1f + .08f * Mathf.Sin(angle * 3f + phase * .73f)
                + .06f * Mathf.Sin(angle * 7f - phase * 1.19f)
                + .028f * Mathf.Sin(angle * 11f + phase * 1.61f)
                + .018f * Mathf.Sin(angle * 19f - phase * 2.13f);
        }
        public ReactiveCombatPortalEffect(Transform parent, Vector3 floorPosition, int layer)
        {
            root = new GameObject("ReactiveEnemyPortal");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = floorPosition + Vector3.up * CenterHeight;
            root.transform.localRotation = Quaternion.Euler(0, 15, 0); root.layer = layer;
            depthMaterial = MakeMaterial("Combat/ReactiveCombatPortalVolume", "Portal deep moving aperture");
            distortionMaterial = MakeMaterial("Combat/ReactiveCombatPortalDistortion", "Portal background refraction");
            distortionMaterial.SetTexture("_MainTex", Resources.Load<Texture2D>("CombatB/AbyssArenaBackground"));
            floorMaterial = MakeMaterial("Combat/ReactiveCombatPortalFloor", "Portal floor energy");
            energyMaterial = MakeMaterial("Combat/ReactiveCombatPortalLightning", "Portal electric boundary");
            energyMaterial.SetColor("_Color", new Color(2.8f, 2.6f, 3.4f, 1));
            energyMaterial.renderQueue = 3004;
            particleMaterial = ReactiveCombatAshDissolve.CreateQuietMaterial("Portal motes", Color.white, 0);
            particleMaterial.SetFloat("_SoftShape", 1);
            particleMaterial.renderQueue = 3005;
            depthMesh = MakeQuad(3.15f * 1.25f, 3.35f * 1.25f, "Portal depth carrier");
            MakeSurface("PortalSeedDistortion", depthMesh, distortionMaterial, layer);
            MakeSurface("PortalMovingDepth", depthMesh, depthMaterial, layer);
            floorMesh = MakeQuad(3.7f, .42f, "Portal floor elliptical carrier");
            var floor = MakeSurface("PortalFloorIllumination", floorMesh, floorMaterial, layer);
            floor.transform.localPosition = new Vector3(0, -CenterHeight + .06f, -.32f);
            rimHalo = MakeLine("PortalFracturedHalo", Segments, .72f, true, layer, new Color(.70f,.50f,1f,.65f));
            rimCore = MakeLine("PortalElectricCore", Segments, .20f, true, layer, new Color(1f,.95f,1f,1f));
            ghostRim = MakeLine("PortalResidualOutline", Segments, .065f, true, layer, new Color(.40f,.34f,.64f,.18f));
            for (int i = 0; i < forks.Length; i++)
                forks[i] = MakeLine("PortalLightningBranch" + i, 10, .09f, false, layer, new Color(.8f,.65f,1f,.8f));
            for (int i = 0; i < depthVeins.Length; i++)
            {
                depthVeins[i] = MakeLine("PortalDepthVein" + i, 24, .04f, false, layer, Color.white);
                depthVeins[i].startWidth=.003f; depthVeins[i].endWidth=.06f;
                depthVeins[i].startColor=new Color(.15f,.19f,.45f,0);
                depthVeins[i].endColor=i%3==0 ? new Color(.20f,.58f,.75f,.38f) : new Color(.53f,.28f,.76f,.38f);
            }
            for (int i = 0; i < RibbonCount; i++)
            {
                var mesh = new Mesh { name = "Portal tapered energy ribbon " + i }; mesh.MarkDynamic();
                var uv = new Vector2[RibbonSteps * 2]; var indices = new int[(RibbonSteps - 1) * 6];
                for (int s = 0; s < RibbonSteps; s++)
                {
                    uv[s*2] = new Vector2((float)s/(RibbonSteps-1),0);
                    uv[s*2+1] = new Vector2((float)s/(RibbonSteps-1),1);
                    if (s == RibbonSteps-1) continue;
                    int o=s*6, v=s*2;
                    indices[o]=v; indices[o+1]=v+1; indices[o+2]=v+2;
                    indices[o+3]=v+1; indices[o+4]=v+3; indices[o+5]=v+2;
                }
                mesh.vertices = new Vector3[RibbonSteps*2]; mesh.uv=uv; mesh.triangles=indices; ribbonMeshes[i]=mesh;
                var mat=MakeMaterial("Combat/ReactiveCombatPortalRibbon", "Portal flowing ribbon " + i);
                mat.SetColor("_Color", i%3==0 ? new Color(.18f,.66f,.81f,1) : new Color(.78f,.17f,.57f,1));
                mat.SetFloat("_Seed", i * 2.31f); ribbonMaterials[i]=mat;
                MakeSurface("PortalGrowthRibbon"+i,mesh,mat,layer);
            }
            sparks = MakeParticles(layer);
            var lamp = new GameObject("PortalLocalRimLight", typeof(Light));
            lamp.transform.SetParent(root.transform,false); lamp.transform.localPosition=new Vector3(0,-.5f,-1.2f); lamp.layer=layer;
            light=lamp.GetComponent<Light>(); light.type=LightType.Point; light.color=new Color(.58f,.30f,1f);
            light.range=5.5f; light.shadows=LightShadows.None; light.cullingMask=1<<layer;
            wisps=new ReactiveCombatEmberLayer(root.transform,layer,"Portal_Wisps");
            innerVapor=new ReactiveCombatEmberLayer(root.transform,layer,"Portal_InnerVapor");
            edgeSmoke=new ReactiveCombatEmberLayer(root.transform,layer,"Portal_EdgeSmoke");
            collapse=new ReactiveCombatEmberLayer(root.transform,layer,"Portal_Collapse"); SetProgress(0);
        }
        public void SetProgress(float value)
        {
            if (disposed) return; value=Mathf.Clamp01(value);
            if (!closing && value < visibility - .0001f) { closing=true; closeElapsed=0; }
            visibility=value;
            coreScale=closing ? Mathf.Pow(value,1.9f) : Mathf.Lerp(.045f,1f,
                Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,.76f,value)));
            UpdateAppearance();
        }
        public void Tick(float deltaTime)
        {
            if (disposed || deltaTime <= 0) return;
            float step=Mathf.Max(0,deltaTime); elapsed+=step; if (closing) closeElapsed+=step;
            wisps.Tick(step*1.3f); innerVapor.Tick(step*.48f); edgeSmoke.Tick(step*.92f); collapse.Tick(step*.74f);
            UpdateAppearance();
            if (visibility > .02f)
            {
                emissionElapsed+=step; int count=Mathf.Min(8,Mathf.FloorToInt(emissionElapsed/.009f)); emissionElapsed%=.009f;
                for (int i=0;i<count;i++) EmitMote();
            }
            sparks.Simulate(step,false,false,false); sparks.Pause(false);
        }
        private void UpdateAppearance()
        {
            float opening=closing ? 1f : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.53f,.78f,visibility));
            float presence=visibility>.001f ? 1f : 0f;
            float ghost=closing ? .24f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.50f,.80f,closeElapsed))) : 0;
            depthMaterial.SetFloat("_Phase",elapsed); depthMaterial.SetFloat("_Opening",coreScale);
            depthMaterial.SetFloat("_Opacity",presence); depthMaterial.SetFloat("_Seed",1-opening);
            distortionMaterial.SetFloat("_Phase",elapsed); distortionMaterial.SetFloat("_Opening",coreScale);
            distortionMaterial.SetFloat("_Opacity",presence*(closing?visibility:1));
            distortionMaterial.SetFloat("_Seed",1-opening);
            floorMaterial.SetFloat("_Phase",elapsed); floorMaterial.SetFloat("_Opening",Mathf.Max(visibility,ghost));
            floorMaterial.SetFloat("_Opacity",Mathf.Max(presence*(closing?visibility:Mathf.Clamp01(visibility*10)),ghost));
            energyMaterial.SetFloat("_Opacity", Mathf.Max(opening*presence,ghost));
            energyMaterial.SetFloat("_Phase",elapsed);
            particleMaterial.SetFloat("_Opacity",Mathf.Max(presence,ghost));
            light.intensity=Mathf.Max(presence*opening*.68f*(.88f+.12f*Mathf.Sin(elapsed*8)),ghost*.3f);
            float boundaryScale=coreScale;
            for (int s=0;s<Segments;s++)
            {
                float a=s*Mathf.PI*2/Segments; float b=AngularBoundary(a,elapsed);
                rim[s]=new Vector3(Mathf.Cos(a)*3.15f*b*boundaryScale,Mathf.Sin(a)*3.35f*b*boundaryScale,-.08f);
            }
            rimCore.SetPositions(rim); rimHalo.SetPositions(rim);
            for(int s=0;s<Segments;s++)
            {
                float a=s*Mathf.PI*2/Segments;float b=AngularBoundary(a,elapsed);
                rim[s]=new Vector3(Mathf.Cos(a)*3.15f*b,Mathf.Sin(a)*3.35f*b,-.075f);
            }
            ghostRim.SetPositions(rim);ghostRim.enabled=closing&&ghost>.001f;
            rimCore.widthMultiplier=.85f+.25f*Mathf.Sin(elapsed*12);
            rimCore.enabled=rimHalo.enabled=opening*presence>.001f;
            for(int f=0;f<forks.Length;f++)
            {
                float a=f*2*Mathf.PI/forks.Length+elapsed*.19f; float b=AngularBoundary(a,elapsed);
                for(int s=0;s<10;s++)
                {
                    float t=s/9f;
                    float aa=a+(f%2==0?t*.48f:0)+.055f*Mathf.Sin(t*39+f*2+elapsed*11)*Mathf.Sin(t*Mathf.PI);
                    float r=f%2==0 ? AngularBoundary(aa,elapsed)+Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.55f)*(.18f+.11f*Mathf.Abs(Mathf.Sin(f+elapsed)))
                        : b+t*(.09f+.22f*Mathf.Pow(Mathf.Abs(Mathf.Sin(f*2+elapsed*2)),3));
                    forkPoints[s]=new Vector3(Mathf.Cos(aa)*3.15f*r*boundaryScale,Mathf.Sin(aa)*3.35f*r*boundaryScale,-.10f);
                }
                forks[f].SetPositions(forkPoints); forks[f].enabled=rimCore.enabled;
            }
            for(int v=0;v<depthVeins.Length;v++)
            {
                float baseAngle=v*Mathf.PI*2/depthVeins.Length-elapsed*.23f;
                for(int s=0;s<24;s++)
                {
                    float t=s/23f;
                    float a=baseAngle+t*1.25f+.18f*Mathf.Sin(t*8+v+elapsed*.5f);
                    float radius=Mathf.Lerp(.07f,.93f,t)*AngularBoundary(a,elapsed)*coreScale;
                    veinPoints[s]=new Vector3(Mathf.Cos(a)*3.15f*radius,Mathf.Sin(a)*3.35f*radius,-.055f);
                }
                depthVeins[v].SetPositions(veinPoints);depthVeins[v].enabled=rimCore.enabled;
            }
            float fan=closing ? visibility*.35f : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.17f,.38f,visibility));
            float fold=closing ? 1 : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.77f,visibility));
            for (int r=0;r<RibbonCount;r++)
            {
                float baseAngle=r*2*Mathf.PI/RibbonCount+elapsed*.31f;
                for (int s=0;s<RibbonSteps;s++)
                {
                    float t=(float)s/(RibbonSteps-1); float angle=baseAngle+t*Mathf.Lerp(.9f,2.4f,fold)+.11f*Mathf.Sin(t*5+elapsed*.8f+r);
                    float radial=Mathf.Lerp(.18f,1.64f,t)*fan;
                    radial=Mathf.Lerp(radial,Mathf.Lerp(.8f,1.38f,t)*coreScale,fold);
                    float height=Mathf.Sin(angle)*3.35f*radial;
                    // The seed grows above the floor before the fan folds into the aperture.
                    float growthHeight=height<0 ? height*.18f : height;
                    Vector3 p=new Vector3(Mathf.Cos(angle)*3.15f*radial,
                        Mathf.Lerp(growthHeight,height,fold)-2.05f*(1-fold),-.12f-.08f*Mathf.Sin(t*3+r));
                    Vector3 normal=new Vector3(-Mathf.Sin(angle),Mathf.Cos(angle),0);
                    float width=(.16f+.72f*Mathf.Sin(t*Mathf.PI))*Mathf.Pow(1-t,.5f)*fan;
                    ribbonVertices[s*2]=p-normal*width; ribbonVertices[s*2+1]=p+normal*width;
                }
                ribbonMeshes[r].vertices=ribbonVertices; ribbonMeshes[r].RecalculateBounds();
                ribbonMaterials[r].SetFloat("_Phase",elapsed);
                ribbonMaterials[r].SetFloat("_Opacity",presence*fan*Mathf.Lerp(.95f,.28f,fold)*(closing?visibility:1));
            }
            float size=Mathf.Max(.15f,coreScale);
            wisps.FollowLocal(new Vector3(-2.65f*size,.4f*size,-.19f),new Vector2(3.8f,6.5f)*size,
                new Color(1.5f,.92f,1.4f,1),presence*Mathf.Lerp(.45f,.65f,opening)*(closing?visibility:1),elapsed*7);
            innerVapor.FollowLocal(new Vector3(0,0,-.035f),new Vector2(4.8f,5.8f)*size,
                new Color(.40f,.60f,.90f,1),presence*opening*.18f*(closing?visibility:1),-elapsed*2.8f);
            edgeSmoke.FollowLocal(new Vector3(2.5f*size,.3f*size,-.17f),new Vector2(4.4f,7.5f)*size,
                new Color(1.05f,.65f,1.4f,1),presence*opening*.68f*(closing?visibility:1),elapsed*4.1f);
            collapse.FollowLocal(new Vector3(0,0,-.21f),new Vector2(5.5f,6.4f)*Mathf.Max(.25f,coreScale),
                new Color(.80f,.52f,1,1),closing?Mathf.Max(visibility*.65f,ghost):0,-elapsed*11);
        }
        private void EmitMote()
        {
            float angle=emitted++*2.399963f+elapsed*.6f; float b=AngularBoundary(angle,elapsed);Vector2 r=ApertureRadii;
            Vector3 p=new Vector3(Mathf.Cos(angle)*r.x*b,Mathf.Sin(angle)*r.y*b,-.16f);
            if(emitted%3==0)p*=.25f+.57f*Mathf.Abs(Mathf.Sin(angle*3));
            Vector3 v=closing ? -p*1.1f : new Vector3(-Mathf.Sin(angle)*.25f,.12f+Mathf.Cos(angle)*.28f,-.05f);
            sparks.Emit(new ParticleSystem.EmitParams { position=p,velocity=v,startLifetime=.35f+.4f*Mathf.Abs(Mathf.Sin(angle)),
                startSize=.045f+.055f*Mathf.Abs(Mathf.Cos(angle)),startColor=emitted%3==0?new Color(1.6f,1.3f,.8f,.95f):new Color(1.3f,1.2f,1.6f,.9f)},1);
        }
        private static Material MakeMaterial(string path,string name)
        {
            Shader shader=Resources.Load<Shader>(path);
            if (shader==null) throw new InvalidOperationException("Missing portal shader: "+path);
            return new Material(shader) { name=name };
        }
        private MeshRenderer MakeSurface(string name,Mesh mesh,Material material,int layer)
        {
            var item=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));item.transform.SetParent(root.transform,false);item.layer=layer;
            item.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=item.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return renderer;
        }
        private LineRenderer MakeLine(string name,int count,float width,bool loop,int layer,Color color)
        {
            var item=new GameObject(name,typeof(LineRenderer));item.transform.SetParent(root.transform,false);item.layer=layer;
            var line=item.GetComponent<LineRenderer>();line.useWorldSpace=false;line.loop=loop;line.positionCount=count;
            line.startWidth=width;line.endWidth=loop?width:.001f;line.startColor=color;line.endColor=loop?color:new Color(color.r,color.g,color.b,0);
            line.sharedMaterial=energyMaterial;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        private ParticleSystem MakeParticles(int layer)
        {
            var item=new GameObject("PortalOrbitingSparks",typeof(ParticleSystem));item.transform.SetParent(root.transform,false);item.layer=layer;
            var system=item.GetComponent<ParticleSystem>();var main=system.main;
            main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            main.startSpeed=0;main.startLifetime=.6f;main.startSize=.04f;main.maxParticles=96;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var fade=system.colorOverLifetime;fade.enabled=true;var g=new Gradient();
            g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(0,1)});fade.color=g;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);system.Pause(false);return system;
        }
        private static Mesh MakeQuad(float x,float y,string name)
        {
            var mesh=new Mesh { name=name };mesh.vertices=new[]{new Vector3(-x,-y,0),new Vector3(x,-y,0),new Vector3(x,y,0),new Vector3(-x,y,0)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();return mesh;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            // Deferred Unity destruction must not draw the old portal for a frame
            // beside the next sequential entrance.
            root.SetActive(false);
            wisps.Dispose();innerVapor.Dispose();edgeSmoke.Dispose();collapse.Dispose();
            ReactiveCombatAshDissolve.DestroyOwned(root);ReactiveCombatAshDissolve.DestroyOwned(depthMesh);ReactiveCombatAshDissolve.DestroyOwned(floorMesh);
            ReactiveCombatAshDissolve.DestroyOwned(depthMaterial);ReactiveCombatAshDissolve.DestroyOwned(energyMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(distortionMaterial);
            ReactiveCombatAshDissolve.DestroyOwned(floorMaterial);ReactiveCombatAshDissolve.DestroyOwned(particleMaterial);
            for(int i=0;i<RibbonCount;i++){ReactiveCombatAshDissolve.DestroyOwned(ribbonMeshes[i]);ReactiveCombatAshDissolve.DestroyOwned(ribbonMaterials[i]);}
        }
    }
}
