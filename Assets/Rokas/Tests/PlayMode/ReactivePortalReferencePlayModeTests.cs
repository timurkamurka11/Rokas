using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class ReactivePortalReferencePlayModeTests
    {
        private GameObject root;
        private string profile;
        private InputSettings.BackgroundBehavior priorBackground;
        private InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;

        [UnityTest]
        public IEnumerator PortalUsesFourNativeAtlasesAndDestroysEveryOwnedMeshAndMaterial()
        {
            var parent=new GameObject("PortalOwnedResourcesFixture");
            var portal=new ReactiveCombatPortalEffect(parent.transform,Vector3.zero,30);
            var meshes=new List<Mesh>();var materials=new HashSet<Material>();
            foreach(var filter in portal.Transform.GetComponentsInChildren<MeshFilter>())meshes.Add(filter.sharedMesh);
            foreach(var renderer in portal.Transform.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)materials.Add(material);
            try
            {
                int native=0;
                foreach(var material in materials)
                {
                    if(material.shader.name!="Rokas/Reactive Combat EmberGen Flipbook")continue;
                    Assert.That(material.mainTexture,Is.Not.Null);
                    Assert.That(material.mainTexture.width,Is.EqualTo(1024));native++;
                }
                Assert.That(native,Is.EqualTo(4));
                Vector3 floor=portal.FloorPosition, center=portal.ApertureCenter;
                portal.SetProgress(.3f);portal.Tick(.83f);
                Assert.That(portal.ApertureRadii.y,Is.LessThan(.3f));
                portal.SetProgress(1);portal.Tick(2);
                Assert.That(portal.ApertureRadii,Is.EqualTo(new Vector2(3.15f,3.35f)));
                portal.SetProgress(.5f);portal.Tick(.3f);
                Assert.That(portal.ApertureRadii.y,Is.LessThan(3.35f));
                Assert.That(portal.FloorPosition,Is.EqualTo(floor));Assert.That(portal.ApertureCenter,Is.EqualTo(center));
                Assert.That(portal.Transform.localScale,Is.EqualTo(Vector3.one));
            }
            finally {portal.Dispose();UnityEngine.Object.Destroy(parent);}
            yield return null;
            foreach(var mesh in meshes)Assert.That(mesh==null,Is.True,"Owned portal mesh must be destroyed.");
            foreach(var material in materials)Assert.That(material==null,Is.True,"Owned portal material must be destroyed.");
        }

        [UnityTest]
        public IEnumerator ThreeLiveSequentialPortalsAreCapturedWithoutRootJitterOrLeftoverEffects()
        {
            priorBackground=InputSystem.settings.backgroundBehavior;
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            profile=Path.Combine(Path.GetTempPath(),"rokas-portal-reference-profile-"+Guid.NewGuid().ToString("N"));
            root=new GameObject("PortalReferenceLiveEncounter");
            var boot=root.AddComponent<RokasBootstrap>();boot.Initialize(profile);boot.SendMessage("OnApplicationFocus",true);
            yield return null;
            Assert.That(boot.Session.AcceptContract(),Is.True);Assert.That(boot.Session.LeaveHome(),Is.True);
            yield return null;
            GameObject enter=Find("EnterReactivePortal");
            Assert.That(ExecuteEvents.Execute(enter,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler),Is.True);
            float bootDeadline=Time.realtimeSinceStartup+8f;
            while(boot.Session.ReactiveCombat==null&&Time.realtimeSinceStartup<bootDeadline)yield return null;
            Assert.That(boot.Session.ReactiveCombat,Is.Not.Null);
            float deadline=Time.realtimeSinceStartup+40f,nextCapture=0,start=Time.realtimeSinceStartup;
            int initialAp=boot.Session.ReactiveCombat.HunterAp;
            string directory="D:/Rokas/reactiveturns-b2-staging/portal-reference-match/runtime/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(directory);
            var records=new List<Frame>();var ids=new HashSet<int>();var positions=new Dictionary<int,Vector3>();
            var scales=new Dictionary<int,Vector3>();var rotations=new Dictionary<int,Quaternion>();
            int maximumParticles=0,maximumMaterials=0;
            while(Time.realtimeSinceStartup<deadline)
            {
                var world=GameObject.Find("ReactiveCombatWorld");
                if(world!=null)
                {
                    int portals=0,maskedActors=0;
                    foreach(Transform child in world.transform)if(child.name=="ReactiveEnemyPortal"&&child.gameObject.activeInHierarchy)portals++;
                    Assert.That(portals,Is.LessThanOrEqualTo(1));
                    var portal=GameObject.Find("ReactiveEnemyPortal");
                    float elapsed=0,opening=0;int id=0;
                    if(portal!=null)
                    {
                        id=portal.GetInstanceID();ids.Add(id);
                        var depth=portal.transform.Find("PortalMovingDepth").GetComponent<MeshRenderer>().sharedMaterial;
                        elapsed=depth.GetFloat("_Phase");opening=depth.GetFloat("_Opening");
                        var unique=new HashSet<Material>();int particles=0;
                        foreach(var renderer in portal.GetComponentsInChildren<Renderer>())foreach(var m in renderer.sharedMaterials)unique.Add(m);
                        foreach(var ps in portal.GetComponentsInChildren<ParticleSystem>())particles+=ps.particleCount;
                        maximumParticles=Mathf.Max(maximumParticles,particles);maximumMaterials=Mathf.Max(maximumMaterials,unique.Count);
                    }
                    foreach(var actor in world.GetComponentsInChildren<ReactiveCombatActorVisual>())
                    {
                        if(actor.name!="CombatActor_Yokai")continue;
                        int key=actor.GetInstanceID();
                        foreach(var surface in actor.ModelRoot.GetComponentsInChildren<SkinnedMeshRenderer>())
                            if(surface.sharedMaterial.shader.name=="Rokas/ReactiveCombat/PortalEmergence"){maskedActors++;break;}
                        if(positions.TryGetValue(key,out Vector3 before))
                        {
                            Assert.That(Vector3.Distance(before,actor.transform.localPosition),Is.LessThan(.45f),"Entrance must not teleport after activation.");
                            Assert.That(actor.transform.localScale,Is.EqualTo(scales[key]));
                            Assert.That(Quaternion.Angle(rotations[key],actor.transform.localRotation),Is.LessThan(.01f));
                        }
                        positions[key]=actor.transform.localPosition;scales[key]=actor.transform.localScale;rotations[key]=actor.transform.localRotation;
                    }
                    Assert.That(maskedActors,Is.LessThanOrEqualTo(1));
                    if(Time.realtimeSinceStartup>=nextCapture)
                    {
                        string path=Path.Combine(directory,"frame-"+records.Count.ToString("D4")+".png");Capture(path);
                        records.Add(new Frame{image=path,realtime=Time.realtimeSinceStartup-start,portalId=id,portalElapsed=elapsed,opening=opening,maskedActors=maskedActors});
                        nextCapture=Time.realtimeSinceStartup+.0667f;
                    }
                    if(ids.Count>=3 && boot.View.ReactivePresentationReady && !boot.ReactivePresentationHeld)break;
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(new Manifest{frames=records.ToArray(),portals=ids.Count,maximumParticles=maximumParticles,maximumMaterials=maximumMaterials},true));
            Assert.That(ids.Count,Is.EqualTo(3));
            Assert.That(boot.View.ReactivePresentationReady,Is.True);
            Assert.That(GameObject.Find("ReactiveEnemyPortal"),Is.Null);
            Assert.That(GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
            foreach(var renderer in GameObject.Find("ReactiveCombatWorld").GetComponentsInChildren<SkinnedMeshRenderer>())
                Assert.That(renderer.sharedMaterial.shader.name,Is.Not.EqualTo("Rokas/ReactiveCombat/PortalEmergence"));
            Assert.That(boot.Session.ReactiveCombat.HunterAp,Is.EqualTo(initialAp));
            Assert.That(records.Count,Is.GreaterThan(120));
            File.WriteAllText(Path.Combine(directory,"manifest.json"),JsonUtility.ToJson(new Manifest{frames=records.ToArray(),portals=ids.Count,maximumParticles=maximumParticles,maximumMaterials=maximumMaterials},true));
            TestContext.WriteLine("PORTAL_CAPTURE_DIRECTORY="+directory);
        }
        private GameObject Find(string name)
        {
            foreach(Transform item in root.GetComponentsInChildren<Transform>(true))if(item.name==name)return item.gameObject;
            throw new InvalidOperationException(name);
        }
        private void Capture(string path)
        {
            const int width=960,height=540;
            Canvas canvas=root.GetComponentInChildren<Canvas>();var stage=Find("AuthoredStage").GetComponent<RectTransform>();
            GameObject.Find("ReactiveActorCamera").GetComponent<Camera>().Render();
            var item=new GameObject("PortalProofCamera",typeof(Camera));var camera=item.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            RenderTexture prior=RenderTexture.active;RenderMode mode=canvas.renderMode;Camera oldCamera=canvas.worldCamera;
            float distance=canvas.planeDistance;Vector3 scale=stage.localScale;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                stage.localScale=Vector3.one*.5f;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                try{pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
                finally{UnityEngine.Object.DestroyImmediate(pixels);}
            }
            finally
            {
                canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;stage.localScale=scale;
                RenderTexture.active=prior;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(item);
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(root)UnityEngine.Object.Destroy(root);yield return null;
            if(!string.IsNullOrEmpty(profile)&&Directory.Exists(profile))Directory.Delete(profile,true);
            if(!string.IsNullOrEmpty(profile))
            {InputSystem.settings.backgroundBehavior=priorBackground;InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;}
        }
        [Serializable] private sealed class Frame{public string image;public float realtime,portalElapsed,opening;public int portalId,maskedActors;}
        [Serializable] private sealed class Manifest{public Frame[] frames;public int portals,maximumParticles,maximumMaterials;}
    }
}
