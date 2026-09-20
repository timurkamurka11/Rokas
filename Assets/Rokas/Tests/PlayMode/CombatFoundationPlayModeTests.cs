using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Rokas.Tests
{
    public sealed class CombatFoundationPlayModeTests
    {
        private GameObject root;
        private string directory;
        private RokasBootstrap boot;
        [UnitySetUp] public IEnumerator SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "rokas-combat2-" + Guid.NewGuid().ToString("N"));
            root = new GameObject("Combat2Fixture"); boot = root.AddComponent<RokasBootstrap>(); boot.Initialize(directory);
            boot.Session.AcceptContract(); boot.Session.LeaveHome(); boot.Session.EnterPortal();
            yield return null;
            boot.enabled = false; // Deterministic clock; keep the real view and EventSystem input path.
        }
        [UnityTest] public IEnumerator FightShowsSealChargeAndResonanceInsteadOfWeakPointButton()
        {
            Assert.That(Find("EnemySealBar"), Is.Not.Null);
            Assert.That(Find("ChargeTimingRing"), Is.Not.Null);
            Assert.That(Find("ResonanceBar"), Is.Not.Null);
            Assert.That(Find("EnemyTelegraph"), Is.Not.Null);
            Assert.That(Find("WeakPoint"), Is.Null);
            Capture("combat-hud");
            yield return null;
        }
        [UnityTest] public IEnumerator RealPointerTapDamagesOnReleaseAndPauseBlocksIt()
        {
            var target = Find("EnemyAttack"); Assert.That(target, Is.Not.Null);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            float before = boot.Session.State.enemyHp;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(before), "Holding starts a charge, never a duplicate click hit.");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp, Is.LessThan(before));
            boot.Session.Tick(.5f); before = boot.Session.State.enemyHp;
            boot.View.Escape();
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(before));
            yield return null;
        }
        [UnityTest] public IEnumerator HeldReleaseMakesPerfectCutWithoutDoubleClickDamage()
        {
            var target = Find("EnemyAttack"); var pointer = Pointer();
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            boot.Session.Tick(.96f);
            boot.View.Tick(0); Capture("combat-charge");
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
            Assert.That(boot.Session.Combat.LastAction, Is.EqualTo("PerfectCut"));
            Assert.That(boot.Session.State.enemyHp, Is.EqualTo(165));
            Assert.That(boot.Session.Combat.Seal, Is.EqualTo(68));
            yield return null;
        }
        [UnityTest] public IEnumerator PauseCancelsHeldInputAndGuardsDefenseAndResonanceKeys()
        {
            var target=Find("EnemyAttack");var pointer=Pointer();
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            boot.Session.Tick(.8f);boot.View.Escape();
            Assert.That(boot.Session.Combat.IsHolding,Is.False);
            boot.Session.State.enemyTimer=.1f;
            boot.View.HandleCombatInput(false,true,true);
            Assert.That(boot.Session.Combat.Seal,Is.EqualTo(100));
            boot.View.Escape();
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
        }
        [UnityTest] public IEnumerator RealDeflectInputOpensOrderedHeldRitualAndEnablesResonance()
        {
            for(int i=0;i<4;i++)
            {
                boot.Session.Tick(boot.Session.State.enemyTimer-.1f);
                boot.View.HandleCombatInput(false,true,false);
            }
            Assert.That(boot.Session.Combat.Stage,Is.EqualTo(CombatStage.SealBreak));
            boot.Session.Tick(.36f);boot.View.Tick(0);Canvas.ForceUpdateCanvases();
            Capture("combat-ritual");
            var target=Find("EnemyAttack");var pointer=Pointer();
            for(int i=1;i<=3;i++)
            {
                var point=Find("RitualPoint"+i).GetComponent<RectTransform>();
                pointer.position=RectTransformUtility.WorldToScreenPoint(null,point.TransformPoint(point.rect.center));
                if(i==1) ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
                else ExecuteEvents.Execute(target,pointer,ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(108));
            Assert.That(boot.Session.Combat.Resonance,Is.EqualTo(100));
            boot.View.HandleCombatInput(false,false,true);
            Assert.That(boot.Session.Combat.ResonanceActive,Is.True);
            Assert.That(Find("CombatFeedback").GetComponent<Text>().text,Does.Contain("РЕЗОНАНС"));
            yield return null;
        }
        [UnityTest] public IEnumerator PointerExitCancelsChargeWithoutGhostRelease()
        {
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            boot.Session.Tick(.7f);
            pointer.position=new Vector2(-100,-100);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerExitHandler);
            Assert.That(boot.Session.Combat.IsHolding,Is.False,"Leaving combat input must cancel the held gesture.");
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
        }
        [UnityTest] public IEnumerator FocusLossCancelsChargeAndFreezesEnemyClock()
        {
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);boot.Session.Tick(.7f);
            boot.SendMessage("OnApplicationFocus",false);
            float timer=boot.Session.State.enemyTimer;
            boot.SendMessage("Update");
            Assert.That(boot.Session.Combat.IsHolding,Is.False);
            Assert.That(boot.Session.State.enemyTimer,Is.EqualTo(timer));
            boot.SendMessage("OnApplicationFocus",true);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
        }
        [UnityTest] public IEnumerator DisabledCombatSurfaceCancelsCharge()
        {
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);boot.Session.Tick(.7f);
            target.SetActive(false);Assert.That(boot.Session.Combat.IsHolding,Is.False);
            target.SetActive(true);ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
        }
        [UnityTest] public IEnumerator HunterDeathClearsHeldInputAndCombatHud()
        {
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            boot.Session.State.playerHp=1;boot.Session.State.enemyTimer=.1f;boot.Session.Tick(.1f);
            Assert.That(boot.Session.State.phase,Is.EqualTo(RunPhase.Failed));
            Assert.That(boot.Session.Combat.IsHolding,Is.False);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
            Assert.That(Find("ChargeTimingRing"),Is.Null);
            Assert.That(Find("ResonanceBar"),Is.Null);
        }
        [UnityTest] public IEnumerator SealBreakCannotReuseTheChargeHeldBeforeDeflect()
        {
            for(int i=0;i<3;i++){boot.Session.Tick(boot.Session.State.enemyTimer-.1f);boot.View.HandleCombatInput(false,true,false);}
            boot.Session.Tick(boot.Session.State.enemyTimer-.1f);
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            boot.View.HandleCombatInput(false,true,false);boot.Session.Tick(.36f);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.Combat.Stage,Is.EqualTo(CombatStage.Ritual));
            Assert.That(boot.Session.Combat.IsHolding,Is.False);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            yield return null;
        }
        [UnityTest] public IEnumerator LiveCueMatchesDodgeAndBarsMatchActualDamage()
        {
            boot.Session.Tick(2.2f);boot.View.Tick(0);
            Assert.That(Find("EnemyTelegraph").GetComponent<Text>().text,Does.Contain(.4f.ToString("0.00")));
            var pointer=At(Find("EnemyAttack"));pointer.button=PointerEventData.InputButton.Right;
            ExecuteEvents.Execute(Find("EnemyAttack"),pointer,ExecuteEvents.pointerDownHandler);
            Assert.That(boot.Session.State.playerHp,Is.EqualTo(100));
            Assert.That(boot.Session.Combat.Resonance,Is.EqualTo(6));
            Assert.That(Find("EnemyTelegraph").GetComponent<Text>().text,Does.Contain("НАБЛЮДАЙТЕ"));
            boot.Session.BeginAttack();boot.Session.Tick(.96f);boot.Session.ReleaseAttack();
            Assert.That(Find("EnemySealBar").GetComponent<RectTransform>().sizeDelta.x,Is.EqualTo(650*.68f).Within(.01f));
            Assert.That(Find("EnemyBar").GetComponent<RectTransform>().sizeDelta.x,Is.EqualTo(650*165f/180).Within(.01f));
            yield return null;
        }
        [UnityTest] public IEnumerator RitualPointsAreReachableThroughTheRealGraphicRaycaster()
        {
            for(int i=0;i<4;i++){boot.Session.Tick(boot.Session.State.enemyTimer-.1f);boot.View.HandleCombatInput(false,true,false);}
            boot.Session.Tick(.36f);boot.View.Tick(0);Canvas.ForceUpdateCanvases();
            for(int i=1;i<=3;i++)
            {
                var pointer=At(Find("RitualPoint"+i));
                var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Assert.That(hits.Count,Is.GreaterThan(0));
                Assert.That(hits[0].gameObject,Is.EqualTo(Find("EnemyAttack")),"Ritual decoration must not intercept pointer capture.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PauseInterruptsComboBeforeResume()
        {
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            boot.Session.Tick(.48f);boot.View.Escape();boot.View.Escape();
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.Combat.ComboStep,Is.EqualTo(1),"Pause breaks the chain rather than preserving a Perfect beat.");
            yield return null;
        }
        [UnityTest] public IEnumerator LethalHeldReleaseClearsResonanceHudAndRewardRemainsExactlyOnce()
        {
            for(int i=0;i<4;i++){boot.Session.Tick(boot.Session.State.enemyTimer-.1f);boot.Session.Deflect();}
            boot.Session.Tick(.36f);boot.Session.BeginAttack();for(int i=0;i<3;i++)boot.Session.TraceRitualPoint(i);
            boot.Session.ActivateResonance();boot.Session.State.enemyHp=1;
            var target=Find("EnemyAttack");var pointer=At(target);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);boot.Session.Tick(.96f);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.phase,Is.EqualTo(RunPhase.Sealed));
            Assert.That(boot.Session.Combat.IsHolding,Is.False);
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            yield return null;
            Assert.That(Find("ResonanceBar"),Is.Null);
            Assert.That(Find("RitualTrace"),Is.Null);
            Assert.That(boot.Session.ReturnHome(),Is.True);
            int before=boot.Session.State.yen;
            Assert.That(boot.Session.ClaimPayment(),Is.True);
            Assert.That(boot.Session.ClaimPayment(),Is.False);
            Assert.That(boot.Session.State.yen,Is.EqualTo(before+boot.Session.Contract.reward));
        }
        [UnityTest] public IEnumerator PauseDuringRitualFailsOnceAndResumeCannotPayOffStaleTrace()
        {
            for(int i=0;i<4;i++){boot.Session.Tick(boot.Session.State.enemyTimer-.1f);boot.Session.Deflect();}
            boot.Session.Tick(.36f);boot.View.Tick(0);Canvas.ForceUpdateCanvases();
            var target=Find("EnemyAttack");var pointer=At(Find("RitualPoint1"));
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerDownHandler);
            Assert.That(boot.Session.Combat.RitualPoint,Is.EqualTo(1));
            boot.View.Escape();
            Assert.That(boot.Session.Combat.Stage,Is.EqualTo(CombatStage.Fighting));
            Assert.That(boot.Session.Combat.Seal,Is.EqualTo(50));
            float resonance=boot.Session.Combat.Resonance;
            boot.View.Escape();ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerUpHandler);
            Assert.That(boot.Session.State.enemyHp,Is.EqualTo(180));
            Assert.That(boot.Session.Combat.Resonance,Is.EqualTo(resonance));
            yield return null;
        }
        // Opt-in runtime renders for visual review; normal test runs allocate no capture resources.
        private void Capture(string name)
        {
            string output=Environment.GetEnvironmentVariable("ROKAS_COMBAT_CAPTURE_DIR");
            if(string.IsNullOrEmpty(output)||SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
            var canvas=root.GetComponentInChildren<Canvas>();var stage=Find("AuthoredStage").GetComponent<RectTransform>();
            var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldScale=stage.localScale;
            var cameraObject=new GameObject("CombatCaptureCamera");var camera=cameraObject.AddComponent<Camera>();
            var target=new RenderTexture(1920,1080,24);var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.enabled=false;camera.orthographic=true;camera.orthographicSize=540;camera.nearClipPlane=.01f;camera.farClipPlane=100;
                camera.targetTexture=target;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                stage.localScale=Vector3.one;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;stage.localScale=oldScale;RenderTexture.active=previous;
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(cameraObject);
                Canvas.ForceUpdateCanvases();
            }
        }
        [UnityTest] public IEnumerator ChargeRingHasRenderableGeometryAndChangesWhileHeld()
        {
            var ring=Find("ChargeTimingRing").GetComponent<CombatTimingRing>();
            Assert.That(ring.canvasRenderer,Is.Not.Null,"Custom combat Graphic must own a CanvasRenderer.");
            Mesh mesh=null;
            try
            {
                Canvas.ForceUpdateCanvases();mesh=ring.canvasRenderer.GetMesh();
                Assert.That(mesh.vertexCount,Is.GreaterThan(0),"Charge ring must submit visible geometry.");
                var initial=mesh.colors32;
                boot.Session.BeginAttack();boot.Session.Tick(.7f);boot.View.Tick(0);Canvas.ForceUpdateCanvases();
                mesh=ring.canvasRenderer.GetMesh();
                CollectionAssert.AreNotEqual(initial,mesh.colors32,"Held charge must visibly advance the ring.");
            }
            finally { if(mesh) UnityEngine.Object.Destroy(mesh); }
            yield return null;
        }
        private static PointerEventData At(GameObject target)
        {
            var pointer=Pointer();var rect=target.GetComponent<RectTransform>();
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));return pointer;
        }

        private static PointerEventData Pointer() { return new PointerEventData(EventSystem.current) { button=PointerEventData.InputButton.Left }; }

        private GameObject Find(string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t.gameObject;
            return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (root) UnityEngine.Object.Destroy(root); yield return null;
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
