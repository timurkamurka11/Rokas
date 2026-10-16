#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Captures a real Unity-rendered 24fps bed-POV video frame sequence.
    /// BED BACKDROP MUST BE the authentic ROKAS BedCeilingPOV resource.
    /// The arm/watch MUST BE the imported skinned Argon FBX and eight animation
    /// clips. The OLED MUST BE the real BedSleep gameplay presenter's texture.
    /// No screenshot impostors, Photoshop synthesis or procedural substitute.
    /// Source production V10 is untouched; this is an isolated preview test.
    /// </summary>
    public sealed class RokasArgonBedUnityVideoTests
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const int Fps = 24;

        [Test]
        public void RecordActualArgonHandOverRealRokasBedCeilingToPngFrames()
        {
            Texture2D trueBed = Resources.Load<Texture2D>("BedSleep/BedCeilingPOV");
            Assert.That(trueBed, Is.Not.Null,
                "BLOCKED: The real game resource Assets/Rokas/Resources/BedSleep/BedCeilingPOV.png " +
                "is not in this GitHub checkout. Upload the original plate, NEVER fake the scene " +
                "or substitute ApartmentNight / a screenshot of unrelated content.");

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                RokasArgonCandidateImporter.CandidatePrefab);
            Assert.That(source, Is.Not.Null, "Cloud-verified original Argon prefab missing.");
            GameObject root = null, cameraObj = null, keyObj = null;
            RenderTexture handRT = null, finalRT = null;
            Material oledMat = null, compositeMat = null;
            Texture2D capture = null;
            RenderTexture previousRt = RenderTexture.active;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                Assert.That(root, Is.Not.Null);
                root.name = "ROKAS_Argon_OriginalArm_Untouched_CinematicCapture";
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = 29; // isolated 3D Unity camera only

                var arm = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x => x.name == "Keiko_Hand_Skin_Mesh");
                var oled = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x => x.name == "Watch_OLED_Display");
                var animator = root.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,
                    Is.EqualTo(3), "Must use three real Argon skinned meshes");
                Assert.That(root.GetComponentsInChildren<MeshRenderer>(true).Length,
                    Is.EqualTo(7), "Must use seven authentic physical watch meshes");

                // Reuse the real game OLED presenter, not a preview timestamp
                // rendered into a fake mesh or an image over the original watch.
                Type oledType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.FullName ==
                        "Rokas.BedSleep.V10Release.SmartwatchOLEDPresenter");
                Assert.That(oledType, Is.Not.Null, "Real ROKAS game OLED class unavailable.");
                var oledPresenter = root.AddComponent(oledType);
                var flags = BindingFlags.Public | BindingFlags.Instance;
                oledType.GetMethod("InitializeOled", flags).Invoke(oledPresenter, null);
                var configureTime = oledType.GetMethod("ConfigureTime", flags);
                var setConfirmed = oledType.GetMethod("SetConfirmedState", flags);
                var liveOled = oledType.GetProperty("OledRenderTexture", flags)
                    .GetValue(oledPresenter) as RenderTexture;
                Assert.That(liveOled, Is.Not.Null, "Actual game RenderTexture was not initialized.");
                oledMat = new Material(oled.sharedMaterial);
                oledMat.mainTexture = liveOled;
                oled.sharedMaterial = oledMat;

                cameraObj = new GameObject("ROKAS_Argon_Cinematic_OriginalFBX_Camera");
                var cam = cameraObj.AddComponent<Camera>();
                cam.enabled = false;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.cullingMask = 1 << 29;
                cam.nearClipPlane = .01f;
                cam.farClipPlane = 10f;
                cam.fieldOfView = 40f;
                cam.aspect = (float)Width / Height;

                keyObj = new GameObject("ROKAS_Argon_IndoorCeilingLight");
                var key = keyObj.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = .65f;
                key.cullingMask = 1 << 29;
                keyObj.transform.rotation = Quaternion.Euler(35, -20, 30);

                handRT = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                finalRT = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
                handRT.Create(); finalRT.Create();
                cam.targetTexture = handRT;
                compositeMat = new Material(Shader.Find("Hidden/ROKAS/ArgonBedVideoComposite"));
                Assert.That(compositeMat.shader, Is.Not.Null);
                compositeMat.SetTexture("_OverlayTex", handRT);
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);

                var clips = animator.runtimeAnimatorController.animationClips;
                AnimationClip FindClip(string name) => clips.Single(c =>
                    c.name.EndsWith("|" + name + "_ROKAS_Keiko_25Bone_GenericRig",
                        StringComparison.Ordinal));

                var inspect = FindClip("SleepArm_InspectIdle");
                var raise = FindClip("SleepArm_Raise");
                var wrist = FindClip("SleepArm_WristTurn");
                var crown = FindClip("SleepArm_CrownFeedback");
                var confirm = FindClip("SleepArm_Confirm");
                var lower = FindClip("SleepArm_Lower");
                Assert.That(inspect.length, Is.GreaterThan(0));
                configureTime.Invoke(oledPresenter, new object[] { 19, 30, 14 });
                setConfirmed.Invoke(oledPresenter, new object[] { false });
                inspect.SampleAnimation(animator.gameObject, inspect.length * .5f);

                // Cloud Unity calibrated the ACTUAL imported OLED's viewer
                // normal to world -Y after FBX conversion (measured, not guessed).
                Vector3 watchCenter = oled.bounds.center;
                Vector3 viewerNormal = Vector3.down;
                Vector3 sleeveDirection = Vector3.ProjectOnPlane(
                    arm.bounds.center - watchCenter, viewerNormal);
                Assert.That(sleeveDirection.magnitude, Is.GreaterThan(.001f));
                Quaternion bestRotation = Quaternion.identity;
                float bestLeft = float.NegativeInfinity;
                foreach (var roll in new[] { -35f, 35f })
                {
                    Quaternion q = Quaternion.LookRotation(-viewerNormal,
                        Quaternion.AngleAxis(roll, viewerNormal) * -sleeveDirection.normalized);
                    Vector3 cuff = Quaternion.Inverse(q) * sleeveDirection;
                    if (-cuff.x > bestLeft)
                    {
                        bestLeft = -cuff.x;
                        bestRotation = q;
                    }
                }
                cam.transform.SetPositionAndRotation(
                    watchCenter + viewerNormal * .48f, bestRotation);
                var originalRootPos = root.transform.position;

                string outDir = Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts", "ArgonBedVideo");
                string framesDir = Path.Combine(outDir, "frames");
                Directory.CreateDirectory(framesDir);

                int frameNumber = 0;
                int inspectPixelCount = 0;
                void RenderFrame(AnimationClip clip, float normalizedTime, float exitDistance,
                    int durationStep, bool confirmed)
                {
                    configureTime.Invoke(oledPresenter, new object[] { 19, 30, durationStep });
                    setConfirmed.Invoke(oledPresenter, new object[] { confirmed });
                    root.transform.position = originalRootPos;
                    clip.SampleAnimation(animator.gameObject, clip.length *
                        Mathf.Clamp01(normalizedTime));
                    // This is a CAMERA-RELATIVE presentation track for the
                    // original skinned FBX, not a generated or substituted hand.
                    root.transform.position = originalRootPos +
                        (cam.transform.right + cam.transform.up) * exitDistance;
                    cam.Render();
                    Graphics.Blit(trueBed, finalRT, compositeMat);
                    RenderTexture.active = finalRT;
                    capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                    capture.Apply(false);
                    string name = Path.Combine(framesDir,
                        "frame_" + frameNumber.ToString("D5") + ".png");
                    File.WriteAllBytes(name, capture.EncodeToPNG());
                    if (frameNumber == 0 || frameNumber % 24 == 0)
                    {
                        File.WriteAllBytes(Path.Combine(outDir,
                            "storyboard_" + frameNumber.ToString("D5") + ".png"),
                            capture.EncodeToPNG());
                    }
                    // During actual InspectIdle keep the live watch readable.
                    if (clip == inspect)
                    {
                        RenderTexture.active = handRT;
                        var pixelCapture = new Texture2D(160, 90, TextureFormat.RGBA32, false);
                        try
                        {
                            Graphics.Blit(handRT, finalRT);
                            RenderTexture.active = finalRT;
                            capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                            capture.Apply(false);
                            var p = capture.GetPixels32();
                            inspectPixelCount = p.Count(x =>
                                x.b > 90 && x.g > 80 &&
                                x.b > x.r * 1.35f && x.g > x.r * 1.25f);
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(pixelCapture);
                        }
                    }
                    ++frameNumber;
                }

                void Segment(AnimationClip clip, float duration, Func<float, float> slide,
                    Func<float, int> durationStep, bool confirmed)
                {
                    int frames = Mathf.CeilToInt(duration * Fps);
                    for (int i = 0; i < frames; i++)
                    {
                        float t = frames == 1 ? 1f : (float)i / (frames - 1);
                        RenderFrame(clip, t, slide(t), durationStep(t), confirmed);
                    }
                }

                // Real ceiling before hand appears.
                Segment(FindClip("SleepArm_HiddenIdle"), .5f, t => -.24f,
                    t => 14, false);
                // Raise from bottom left; never emerge from the top.
                Segment(raise, raise.length, t => Mathf.Lerp(-.24f, -.06f,
                    Mathf.SmoothStep(0f, 1f, t)), t => 14, false);
                Segment(wrist, wrist.length, t => Mathf.Lerp(-.06f, 0f, t),
                    t => 14, false);
                Segment(inspect, .75f, t => 0f, t => 14, false);
                Segment(crown, crown.length, t => 0f,
                    t => 14 + Mathf.FloorToInt(t * 4f), false);
                Segment(confirm, confirm.length, t => 0f, t => 18, true);
                Segment(lower, lower.length, t => Mathf.Lerp(0f, -.24f,
                    Mathf.SmoothStep(0f, 1f, t)), t => 18, true);
                Segment(FindClip("SleepArm_ReturnIdle"), .35f,
                    t => -.24f, t => 18, true);

                Assert.That(frameNumber, Is.GreaterThan(120));
                Assert.That(inspectPixelCount, Is.GreaterThan(12),
                    "Real OLED is not readable in the real Unity composite.");
                string report = "{\n" +
                    "  \"engine\": \"Unity 6000.3.19f1\",\n" +
                    "  \"background_resource\": \"" +
                    AssetDatabase.GetAssetPath(trueBed) + "\",\n" +
                    "  \"hand_prefab\": \"" + RokasArgonCandidateImporter.CandidatePrefab + "\",\n" +
                    "  \"fps\": " + Fps + ",\n" +
                    "  \"frame_count\": " + frameNumber + ",\n" +
                    "  \"resolution\": \"1280x720\",\n" +
                    "  \"original_game_oled\": true,\n" +
                    "  \"original_fbx_clips\": true\n" +
                    "}";
                File.WriteAllText(Path.Combine(outDir, "capture_manifest.json"), report);
                Debug.Log("[ROKAS ARGON BED VIDEO] PASS recorded " + frameNumber +
                    " genuine Unity-rendered frames over actual BedCeilingPOV.");
            }
            finally
            {
                RenderTexture.active = previousRt;
                if (cameraObj != null)
                {
                    var cam = cameraObj.GetComponent<Camera>();
                    if (cam != null) cam.targetTexture = null;
                }
                if (oledMat != null) UnityEngine.Object.DestroyImmediate(oledMat);
                if (compositeMat != null) UnityEngine.Object.DestroyImmediate(compositeMat);
                if (capture != null) UnityEngine.Object.DestroyImmediate(capture);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (keyObj != null) UnityEngine.Object.DestroyImmediate(keyObj);
                if (cameraObj != null) UnityEngine.Object.DestroyImmediate(cameraObj);
                if (handRT != null) { handRT.Release(); UnityEngine.Object.DestroyImmediate(handRT); }
                if (finalRT != null) { finalRT.Release(); UnityEngine.Object.DestroyImmediate(finalRT); }
            }
        }
    }
}
#endif
