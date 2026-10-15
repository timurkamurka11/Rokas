using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Optional bridge for the supplied Argon V3 production arm/watch module.
    /// Keeps the ROKAS 2.5D presentation API and save ownership intact.
    /// The V3 module is delivered as a separate asset installer, therefore
    /// no compile-time ROKAS.BedSleep assembly dependency is introduced.
    /// </summary>
    internal sealed class RokasBedSleepProductionRigBridge : IDisposable
    {
        private const int RigLayer = 30;
        private readonly GameObject root;
        private readonly Camera camera;
        private readonly RenderTexture target;
        private readonly MonoBehaviour controller;
        private readonly object oled;
        private readonly object armAnimation;
        private readonly object watch;
        private readonly Type stateType;
        private bool released;

        public RenderTexture Texture => target;
        public bool Ready => !released && controller != null && camera != null;

        private RokasBedSleepProductionRigBridge(GameObject root, Camera camera,
            RenderTexture target, MonoBehaviour controller, object oled,
            object armAnimation, object watch, Type stateType)
        {
            this.root = root;
            this.camera = camera;
            this.target = target;
            this.controller = controller;
            this.oled = oled;
            this.armAnimation = armAnimation;
            this.watch = watch;
            this.stateType = stateType;
        }

        public static RokasBedSleepProductionRigBridge TryCreate(
            float currentClockHours, bool lampOn)
        {
            // A missing installer must NEVER break the existing hub.
            Type controllerType = FindType("ROKAS.BedSleep.BedSleepCinematicController");
            Type phaseType = FindType("ROKAS.BedSleep.BedSleepStateV3");
            if (controllerType == null || phaseType == null) return null;

            GameObject container = null;
            RenderTexture rt = null;
            try
            {
                container = new GameObject("ROKAS_BedSleep_V3_3DOnly");
                var script = container.AddComponent(controllerType) as MonoBehaviour;
                if (script == null) throw new InvalidOperationException(
                    "V3 controller is not a MonoBehaviour");
                Call(script, "InitializeV3Module",
                    Mathf.RoundToInt(Mathf.Repeat(currentClockHours, 24f) * 60f));
                // The module's runtime mesh/rig builder has now created
                // the real SkinnedMeshRenderer and separate watch meshes.
                object arm = GetProperty(script, "ArmAnimationController");
                object watch = GetProperty(script, "Smartwatch3D");
                object oled = GetProperty(script, "OLEDPresenter");
                if (arm == null || watch == null || oled == null)
                    throw new InvalidOperationException("V3 3D rig components missing");

                var skinned = GetProperty(arm, "ArmSkinnedMeshRenderer")
                    as SkinnedMeshRenderer;
                if (skinned == null || skinned.sharedMesh == null ||
                    skinned.bones == null || skinned.bones.Length != 25)
                    throw new InvalidOperationException("V3 SkinnedMeshRenderer/25-bone rig missing");

                foreach (Transform t in container.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = RigLayer;

                rt = new RenderTexture(1920, 1080, 24,
                    RenderTextureFormat.ARGB32);
                rt.name = "RT_ROKAS_BedSleep_V3_Real3DArm";
                rt.antiAliasing = 2;
                rt.Create();

                GameObject cameraGo = new GameObject("IsolatedArmWatchCamera");
                cameraGo.transform.SetParent(container.transform, false);
                cameraGo.layer = RigLayer;
                var cam = cameraGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = .38f;
                cam.transform.position = new Vector3(-.04f, -.14f, -1.10f);
                cam.transform.rotation = Quaternion.identity;
                cam.nearClipPlane = .02f;
                cam.farClipPlane = 5f;
                cam.cullingMask = 1 << RigLayer;
                cam.allowHDR = false;
                cam.targetTexture = rt;
                cam.depth = -100f;

                var fill = new GameObject("WarmKeyLight");
                fill.transform.SetParent(container.transform, false);
                fill.layer = RigLayer;
                fill.transform.position = new Vector3(-.65f, .8f, -.3f);
                Light warm = fill.AddComponent<Light>();
                warm.type = LightType.Point;
                warm.color = new Color(1f, .77f, .51f);
                warm.intensity = 2.1f;
                warm.range = 3f;
                warm.cullingMask = 1 << RigLayer;

                var rim = new GameObject("WindowCyanRim");
                rim.transform.SetParent(container.transform, false);
                rim.layer = RigLayer;
                rim.transform.position = new Vector3(.5f, .15f, -.25f);
                Light cold = rim.AddComponent<Light>();
                cold.type = LightType.Point;
                cold.color = new Color(.4f, .75f, 1f);
                cold.intensity = .65f;
                cold.range = 2f;
                cold.cullingMask = 1 << RigLayer;

                var result = new RokasBedSleepProductionRigBridge(
                    container, cam, rt, script, oled, arm, watch, phaseType);
                result.ChangeState("LyingViewActive");
                result.SetLight(lampOn);
                result.ConfigureClock(currentClockHours);
                result.Sample(0f);
                return result;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ROKAS BedSleep V3] Rig unavailable: " + e);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
                if (container != null) UnityEngine.Object.Destroy(container);
                return null;
            }
        }

        public void ChangeState(string name)
        {
            if (!Ready) return;
            object value = Enum.Parse(stateType, name);
            Call(controller, "TransitionToState", value);
        }

        public void Sample(float normalized)
        {
            if (!Ready) return;
            Call(controller, "SampleSkeletalRaiseProgress",
                Mathf.Clamp01(normalized), Time.unscaledDeltaTime);
            // The parent ROKAS controller drives real sleep duration
            // and SaveData, rather than importing a second save manager.
            Call(watch, "TickWatch3DMotion", Time.unscaledDeltaTime);
            Call(oled, "TickOLEDAnimation", Time.unscaledDeltaTime);
        }

        public void SetDuration(float durationHours)
        {
            if (!Ready) return;
            // InitializeOLED owns the UI and keeps its time readout aligned.
            // Reinitialize only upon actual steps (never per frame).
            float h = Mathf.Clamp(Mathf.Round(durationHours * 2f) * .5f,
                1f, 12f);
            Call(oled, "InitializeOLED", Mathf.RoundToInt(
                Mathf.Repeat(currentGameTimeHours, 24f) * 60f), h);
        }

        private float currentGameTimeHours;
        public void ConfigureClock(float clockHours)
        {
            currentGameTimeHours = clockHours;
            SetDuration(7f);
        }

        public void CrownStep(int direction)
        {
            if (!Ready || direction == 0) return;
            Call(watch, "RotateCrownForStep", direction);
            Call(armAnimation, "TriggerCrownTickImpulse", direction);
        }

        public void Confirm()
        {
            if (!Ready) return;
            Call(watch, "TriggerSideButtonPress");
            Call(armAnimation, "TriggerConfirmRelaxImpulse");
        }

        public void SetLight(bool on)
        {
            if (!Ready) return;
            Call(controller, "SyncRoomLightingState", on);
        }

        public void Dispose()
        {
            if (released) return;
            released = true;
            if (camera != null) camera.targetTexture = null;
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
            if (root != null) UnityEngine.Object.Destroy(root);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = assembly.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static object GetProperty(object obj, string property)
        {
            return obj.GetType().GetProperty(property,
                BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj, null);
        }

        private static object Call(object obj, string method, params object[] args)
        {
            if (obj == null) return null;
            var mi = obj.GetType().GetMethod(method,
                BindingFlags.Public | BindingFlags.Instance);
            if (mi == null) throw new MissingMethodException(
                obj.GetType().FullName, method);
            return mi.Invoke(obj, args);
        }
    }
}
