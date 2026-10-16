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
        [Timeout(600000)] // Rendering 149 true 720p Unity PNGs can exceed the 180s default.
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
            Material oledMat = null, compositeMat = null, skinPreviewMat = null;
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
                // Original Argon albedo/normal/roughness remain bound.
                // The old white directional light washed out the pale
                // original texture; use soft warm bedroom-key illumination.
                key.intensity = .32f;
                key.color = new Color(1f, .86f, .74f);
                var skinRenderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x => x.name == "Keiko_Hand_Skin_Mesh");
                skinPreviewMat = new Material(skinRenderer.sharedMaterial);
                skinPreviewMat.SetColor("_Color", new Color(.97f, .88f, .83f));
                skinPreviewMat.SetFloat("_BumpScale", .09f);
                skinRenderer.sharedMaterial = skinPreviewMat;
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
                // Snapshot untouched imported GLB/FBX finger bind rotations
                // BEFORE sampling the animation. Its base-only MCP curls are
                // why the previous video showed bent / broken fingers.
                var heroPose = new RokasArgonHorizontalHeroPose(root);
                inspect.SampleAnimation(animator.gameObject, inspect.length * .5f);
                heroPose.CaptureHeroPose();
                heroPose.RestoreNaturalAnatomy();
                // 2D reference geometry: sleeve LEFT, wrist near center, all
                // fingers RIGHT. Face the actual imported OLED, not palm-down.
                // Original GLB: titanium case Y=0.0157..0.0275,
                // physical OLED Y=0.0259..0.0271. Front is +Y.
                // Prior -Y view was the UNDERSIDE of the hand, with the
                // ZTest Always shader painting a flat square through skin.
                // +Y sees the real case, straps, raised glass and nails.
                heroPose.ConfigureCamera(cam, Vector3.up, .41f);

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
                    clip.SampleAnimation(animator.gameObject, clip.length *
                        Mathf.Clamp01(normalizedTime));
                    // Keep the REAL 25-bone source and FBX clip, but eliminate
                    // base-only finger curling and excessive wrist twisting.
                    heroPose.RestoreNaturalAnatomy();
                    // Rigid cinematic slide from OFF-SCREEN LEFT to horizontal
                    // hero pose; no longer rise diagonally from bottom edge.
                    heroPose.SetPresentation(exitDistance);
                    cam.Render();
                    if (clip == inspect && frameNumber % 8 == 0)
                    {
                        var sleeveV = heroPose.SleeveScreen();
                        var watchV = heroPose.WatchScreen();
                        var fingerV = heroPose.FingertipScreen();
                        Assert.That(sleeveV.x, Is.LessThan(watchV.x - .09f),
                            "Sleeve must be LEFT of the genuine watch");
                        Assert.That(watchV.x, Is.InRange(.24f, .58f),
                            "Watch must stay near center-left");
                        Assert.That(fingerV.x, Is.GreaterThan(watchV.x + .09f),
                            "Fingertips must extend RIGHT of wrist");
                        Assert.That(Mathf.Abs(fingerV.y-watchV.y), Is.LessThan(.18f),
                            "Hand must be horizontally aligned, not vertical");
                        Debug.Log("[ROKAS ARGON HORIZONTAL QA] cuff=" + sleeveV +
                            " watch=" + watchV + " fingertip=" + fingerV);
                    }
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
                    if (clip == inspect && frameNumber % 12 == 0)
                    {
                        // The real composited pixel buffer was already captured.
                        // Avoid reading the GPU TWO extra times per inspect frame.
                        var pixels = capture.GetPixels32();
                        // Must count pixels IN the real watch-face rectangle,
                        // not blue light from the window in the bed background.
                        var watchPixel = heroPose.WatchScreen();
                        int cx = Mathf.RoundToInt(watchPixel.x * Width);
                        int cy = Mathf.RoundToInt(watchPixel.y * Height);
                        int lit = 0;
                        for (int y = Mathf.Max(0, cy - 52);
                            y <= Mathf.Min(Height - 1, cy + 52); ++y)
                            for (int x = Mathf.Max(0, cx - 58);
                                x <= Mathf.Min(Width - 1, cx + 58); ++x)
                            {
                                var px = pixels[y * Width + x];
                                if (px.b > 90 && px.g > 80 &&
                                    px.b > px.r * 1.35f && px.g > px.r * 1.25f)
                                    ++lit;
                            }
                        inspectPixelCount = lit;
                        Debug.Log("[ROKAS ARGON REAL OLED DEPTH QA] genuine watch ROI cyan=" +
                            lit + " watchViewport=" + watchPixel);
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

                // Reference: sleeve enters from off-screen LEFT. The whole
                // skinned hand translates rigidly toward watch-center at the
                // exact horizontal viewing angle, never swings up to ceiling.
                Segment(FindClip("SleepArm_HiddenIdle"), .65f,
                    t => 0f, t => 14, false);
                Segment(raise, raise.length, t => t * .86f,
                    t => 14, false);
                Segment(wrist, wrist.length, t => .86f + t * .14f,
                    t => 14, false);
                Segment(inspect, 1.65f, t => 1f, t => 14, false);
                Segment(crown, crown.length, t => 1f,
                    t => 14 + Mathf.FloorToInt(t * 4f), false);
                Segment(confirm, confirm.length, t => 1f, t => 18, true);
                Segment(lower, lower.length, t => 1f - t * .92f,
                    t => 18, true);
                Segment(FindClip("SleepArm_ReturnIdle"), .55f,
                    t => .08f * (1f-t), t => 18, true);

                Assert.That(frameNumber, Is.GreaterThan(105),
                    "Need a genuine continuous 24fps clip, not static animation snapshots.");
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
                    "  \"original_fbx_clips\": true,\n" +
                    "  \"hero_pose\": \"sleeve-left-watch-center-fingers-right\",\n" +
                    "  \"finger_correction\": \"rest-relative-MCP-clamp-11deg\"\n" +
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
                if (skinPreviewMat != null)
                    UnityEngine.Object.DestroyImmediate(skinPreviewMat);
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
