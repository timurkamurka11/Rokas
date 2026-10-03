using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class ReactivePortalStartupEvidencePlayModeTests
    {
        [UnityTest]
        public IEnumerator StartupComponentAblationCapturesActualParticleContribution()
        {
            var parent=new GameObject("PortalStartupAblation");
            var effect=new ReactiveCombatPortalEffect(parent.transform,Vector3.zero,30);
            var item=new GameObject("PortalStartupEvidenceCamera",typeof(Camera));
            var camera=item.GetComponent<Camera>();camera.transform.position=new Vector3(0,3.4f,-20);
            camera.orthographic=true;camera.orthographicSize=4.6f;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var target=new RenderTexture(960,540,24);target.Create();camera.targetTexture=target;
            string directory="D:/Rokas/reactiveturns-b2-staging/wave-portal-choreography/startup-ablation/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(directory);
            try
            {
                for(int i=1;i<=70;i++){effect.SetProgress(i/60f/2.766667f);effect.Tick(1/60f);}
                yield return null;
                var system=effect.Transform.GetComponentInChildren<ParticleSystem>();
                Assert.That(system,Is.Not.Null);
                Save(camera,target,Path.Combine(directory,"all-layers.png"));
                var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.enabled=false;
                yield return null;
                Save(camera,target,Path.Combine(directory,"particles-disabled.png"));renderer.enabled=true;
                File.WriteAllText(Path.Combine(directory,"component.txt"),"ParticleCount="+system.particleCount+"\nShader="+renderer.sharedMaterial.shader.name+"\nProgress="+effect.Visibility+"\nClock="+effect.PresentationElapsed);
                TestContext.WriteLine("STARTUP_ABLATION="+directory);
            }
            finally
            {
                effect.Dispose();camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(item);UnityEngine.Object.Destroy(parent);
            }
            yield return null;
        }
        private static void Save(Camera camera,RenderTexture target,string path)
        {
            camera.Render();var prior=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
            finally{RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(pixels);}
        }
    }
}
