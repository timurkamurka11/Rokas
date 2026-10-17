#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Rokas.BedSleep.Argon.Candidate;
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
        [Timeout(1800000)] // Hard stop 30m; cloud cache + lossless uncompressed 720p video-frame capture.
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
            Mesh candidateCorrectedFingerSkin = null;
            RenderTexture previousRt = RenderTexture.active;
            try
            {
                root = (GameObject)PrefabUtility.InstantiatePrefab(source);
                Assert.That(root, Is.Not.Null);
                root.name = "ROKAS_Argon_OriginalArm_CorrectedFingerCandidatePreview";
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

                // Candidate-only premium Russian moon-and-cloud OLED UI,
                // rendered onto REAL Argon physical face with genuine UVs.
                // Contract remains V10 ConfigureTime/SetConfirmedState so
                // existing real-game inputs and save behavior can be reused.
                Type oledType = typeof(ArgonRussianSleepOledPresenter);
                var oledPresenter = root.AddComponent(oledType);
                var flags = BindingFlags.Public | BindingFlags.Instance;
                oledType.GetMethod("InitializeOled", flags).Invoke(oledPresenter, null);
                var configureTime = oledType.GetMethod("ConfigureTime", flags);
                var setConfirmed = oledType.GetMethod("SetConfirmedState", flags);
                var liveOled = oledType.GetProperty("OledRenderTexture", flags)
                    .GetValue(oledPresenter) as RenderTexture;
                var russianOled = (ArgonRussianSleepOledPresenter)oledPresenter;
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
                key.intensity = .30f;
                key.color = new Color(1f, .86f, .78f);
                var skinRenderer = root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Single(x => x.name == "Keiko_Hand_Skin_Mesh");
                skinPreviewMat = new Material(skinRenderer.sharedMaterial);
                // Run 38984231016 showed harsh stripes from normal/specular.
                // Match its smooth diagnostic #02, preserving real Argon albedo,
                // true geometry and physical RGB lighting. This is QA ONLY.
                skinPreviewMat.SetColor("_Color", new Color(.90f, .80f, .75f));
                skinPreviewMat.SetFloat("_BumpScale", 0f);
                skinPreviewMat.SetFloat("_GlossMapScale", .22f);
                skinPreviewMat.SetFloat("_SpecularHighlights", 0f);
                skinPreviewMat.SetFloat("_GlossyReflections", 0f);
                skinRenderer.sharedMaterial = skinPreviewMat;
                // Genuine source 25-bone FBX remains unchanged. The verified
                // candidate only adds twenty compensated bindpose slots to
                // steer each of the five original finger islands with its
                // actual joints. Unity QA #39025463826 and #39025700502
                // confirmed neutral-shape safety and digit isolation.
                var fingerReport=ArgonFingerBindposeRetarget.ApplyCandidate(
                    skinRenderer,out candidateCorrectedFingerSkin,
                    out var originalSkinBones);
                Assert.That(fingerReport.RetargetedVertices,Is.EqualTo(6970));
                Assert.That(fingerReport.ExtraBoneSlots,Is.EqualTo(20));
                // Geometry socket smoothing is intentionally NOT included:
                // visual QA still shows a hard square seam. No blind changes.
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
                var living = new ArgonLivingRig(root);
                // 2D reference geometry: sleeve LEFT, wrist near center, all
                // fingers RIGHT. Face the actual imported OLED, not palm-down.
                // Original GLB: titanium case Y=0.0157..0.0275,
                // physical OLED Y=0.0259..0.0271. Front is +Y.
                // Prior -Y view was the UNDERSIDE of the hand, with the
                // ZTest Always shader painting a flat square through skin.
                // +Y sees the real case, straps, raised glass and nails.
                heroPose.ConfigureCamera(cam, Vector3.up, .41f);
                var crownRenderer = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Single(x => x.name == "Watch_DigitalCrown_Rotary");
                // REAL source OLED presenter and REAL watch meshes get reusable
                // runtime interactions, not simulated overlaid UI buttons.
                var watchControls = root.AddComponent<ArgonWatchControls>();
                watchControls.Initialize(cam, oledPresenter, oled, crownRenderer);
                // Smoke-test real reusable control logic (not a rendered HUD
                // illusion) before spending minutes on the video sequence.
                Assert.That(watchControls.SelectedHalfHours, Is.EqualTo(14));
                watchControls.Step(1);
                Assert.That(watchControls.SelectedHalfHours, Is.EqualTo(15));
                watchControls.Step(-1);
                Assert.That(watchControls.SelectedHalfHours, Is.EqualTo(14));
                watchControls.Confirm();
                Assert.That(watchControls.IsConfirmed, Is.True);
                watchControls.ResetConfirmation();
                Assert.That(watchControls.IsConfirmed, Is.False);

                string outDir = Path.Combine(Directory.GetCurrentDirectory(),
                    "Artifacts", "ArgonBedVideo");
                string framesDir = Path.Combine(outDir, "frames");
                Directory.CreateDirectory(framesDir);

                // Capture EVERY distinct supplied Russian reference stage at
                // full native OLED resolution, while keeping the physical
                // game camera/framing fixed. Screens are not baked into video:
                // the actual candidate presenter draws each real state.
                void SaveRussianFace(string name)
                {
                    russianOled.RenderAt(0f,true);
                    File.WriteAllBytes(Path.Combine(outDir,
                        "russian_watch_" + name + ".png"),
                        russianOled.OledDynamicTexture.EncodeToPNG());
                }
                russianOled.ConfigureTime(19,30,16);
                SaveRussianFace("01_duration_dial");
                russianOled.ShowPresets();
                SaveRussianFace("02_quick_presets");
                russianOled.SelectPreset(1);
                Assert.That(russianOled.DurationText,Is.EqualTo("00:30"));
                SaveRussianFace("03_30min_preset");
                russianOled.SelectPreset(8);
                SaveRussianFace("04_dial_changed");
                russianOled.SetConfirmedState(true);
                SaveRussianFace("05_alarm_confirmed");
                russianOled.SetFallingAsleep();
                SaveRussianFace("06_falling_asleep");
                russianOled.CancelSelection();
                SaveRussianFace("07_cancelled");
                russianOled.SelectPreset(14);
                russianOled.SetConfirmedState(false);

                int frameNumber = 0;
                int inspectPixelCount = 0;
                int livingBendCount = 0;
                var foleyEvents = new List<ArgonFoleyEvent>();
                void RenderFrame(AnimationClip clip, float normalizedTime, float exitDistance,
                    int durationStep, bool confirmed)
                {
                    // Actual interactive adapter owns duration, OLED and crown.
                    watchControls.SetDurationSteps(durationStep);
                    if (confirmed) watchControls.Confirm();
                    else watchControls.ResetConfirmation();
                    watchControls.AnimateCrown(1f / Fps);
                    // Rebuild only on UI changes or ~10Hz animation ticks.
                    // Never force a full 512px CPU redraw for every 24fps video frame.
                    russianOled.RenderAt((float)frameNumber / Fps, false);
                    Assert.That(russianOled.LastRenderedTextSummary,
                        Does.Contain("ВРЕМЯ СНА"),
                        "OLED must have actual Russian text; no Latin demo labels");
                    clip.SampleAnimation(animator.gameObject, clip.length *
                        Mathf.Clamp01(normalizedTime));
                    // Keep the REAL 25-bone source and FBX clip, but eliminate
                    // base-only finger curling and excessive wrist twisting.
                    heroPose.RestoreNaturalAnatomy();
                    // Organic first-person lift from BELOW and slightly AWAY
                    // from the camera, with genuine wrist/forearm turns.
                    // Withdrawal follows a DIFFERENT natural relaxation track.
                    bool retreating=clip==lower ||
                        clip.name.Contains("SleepArm_ReturnIdle");
                    var gesture=heroPose.SetCinematicPresentation(
                        exitDistance,retreating);
                    living.NotifyAnimationPoseReset();
                    float lifeWeight = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(exitDistance));
                    float elapsed = (float)frameNumber / Fps;
                    living.Apply(elapsed,lifeWeight,clip==crown,
                        heroPose.Right,heroPose.Up,
                        gesture.FingerResponse,retreating);
                    if (clip == inspect && frameNumber % 6 == 0 &&
                        living.FingerBendDegrees("Middle_PIP") > 3f)
                        ++livingBendCount;
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
                    // Lossless uncompressed TGA is far cheaper than 167 PNG
                // deflate passes inside software-rendered cloud Unity.
                // Original 1280x720 RGBA pixels are preserved exactly.
                // Only storyboard and isolated watch-face stills use PNG.
                string name = Path.Combine(framesDir,
                        "frame_" + frameNumber.ToString("D5") + ".tga");
                    File.WriteAllBytes(name, ImageConversion.EncodeToTGA(capture));
                    if (frameNumber == 0 || frameNumber % 24 == 0)
                    {
                        File.WriteAllBytes(Path.Combine(outDir,
                            "storyboard_" + frameNumber.ToString("D5") + ".png"),
                            capture.EncodeToPNG());
                    }
                    // Full resolution FACE texture alongside gameplay shot.
                    // The physical watch is deliberately ~70 px tall within
                    // the user-approved composition; standalone face still
                    // proves Cyrillic legibility and reference fidelity.
                    if (frameNumber == 72 || frameNumber == 120 ||
                        frameNumber == 148)
                        File.WriteAllBytes(Path.Combine(outDir,
                            "russian_oled_face_" +
                            frameNumber.ToString("D5") + ".png"),
                            russianOled.OledDynamicTexture.EncodeToPNG());
                    // Isolated visual diagnostics: determine whether the remaining
                    // skin moire comes from original normal map, specular response,
                    // or the authored color/UV itself. ONE cloud run, one pose.
                    // Never change serialized prefab, source maps or game V10.
                    if (frameNumber == 72)
                    {
                        void SaveSkinVariant(string suffix)
                        {
                            cam.Render();
                            Graphics.Blit(trueBed, finalRT, compositeMat);
                            RenderTexture.active = finalRT;
                            capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                            capture.Apply(false);
                            File.WriteAllBytes(Path.Combine(outDir,
                                "skin_diagnostic_" + suffix + ".png"),
                                capture.EncodeToPNG());
                        }

                        var originalAlbedo = skinPreviewMat.GetTexture("_MainTex");
                        Color originalTint = skinPreviewMat.GetColor("_Color");
                        float originalBump = skinPreviewMat.GetFloat("_BumpScale");
                        float originalGloss = skinPreviewMat.GetFloat("_GlossMapScale");
                        float originalSpec = skinPreviewMat.GetFloat("_SpecularHighlights");
                        float originalReflections = skinPreviewMat.GetFloat("_GlossyReflections");
                        try
                        {
                            // A: remove normal-map microdetail only.
                            skinPreviewMat.SetFloat("_BumpScale", 0f);
                            SaveSkinVariant("01_no_normal");
                            // B: also remove glossy highlights: isolates aliasing.
                            skinPreviewMat.SetFloat("_GlossMapScale", .22f);
                            skinPreviewMat.SetFloat("_SpecularHighlights", 0f);
                            skinPreviewMat.SetFloat("_GlossyReflections", 0f);
                            SaveSkinVariant("02_matte_no_normal");
                            // C: flat replacement for diagnosis ONLY: if banding
                            // persists with no authored albedo, inspect mesh normals
                            // / imported tangents rather than painting textures.
                            skinPreviewMat.SetTexture("_MainTex", Texture2D.whiteTexture);
                            skinPreviewMat.SetColor("_Color",
                                new Color(.72f, .55f, .48f, 1f));
                            SaveSkinVariant("03_flat_albedo");
                        }
                        finally
                        {
                            skinPreviewMat.SetTexture("_MainTex", originalAlbedo);
                            skinPreviewMat.SetColor("_Color", originalTint);
                            skinPreviewMat.SetFloat("_BumpScale", originalBump);
                            skinPreviewMat.SetFloat("_GlossMapScale", originalGloss);
                            skinPreviewMat.SetFloat("_SpecularHighlights", originalSpec);
                            skinPreviewMat.SetFloat("_GlossyReflections", originalReflections);
                            // Restore the actual frame buffer for OLED assertions.
                            cam.Render();
                            Graphics.Blit(trueBed, finalRT, compositeMat);
                            RenderTexture.active = finalRT;
                            capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                            capture.Apply(false);
                        }
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
                    float at = (float)frameNumber / Fps;
                    if (clip == raise || clip == lower)
                    {
                        foleyEvents.Add(new ArgonFoleyEvent(at + .09f,
                            ArgonFoleyCue.Sleeve, .9f));
                        foleyEvents.Add(new ArgonFoleyEvent(at + .53f,
                            ArgonFoleyCue.Sleeve, .42f));
                    }
                    if (clip == crown)
                        for (int i = 0; i < 4; ++i)
                            foleyEvents.Add(new ArgonFoleyEvent(
                                at + .09f + i * duration / 4f,
                                ArgonFoleyCue.CrownTick, .9f));
                    if (clip == confirm)
                        foleyEvents.Add(new ArgonFoleyEvent(
                            at + .065f, ArgonFoleyCue.Confirmation, .85f));
                    int frames = Mathf.CeilToInt(duration * Fps);
                    for (int i = 0; i < frames; i++)
                    {
                        float t = frames == 1 ? 1f : (float)i / (frames - 1);
                        RenderFrame(clip, t, slide(t), durationStep(t), confirmed);
                    }
                }

                // Bed-first-person staging: hidden cuff BELOW camera, then
                // a gradual lift / wrist turn / soft settle into the EXACT
                // same approved sleeve-left, watch-center, fingers-right pose.
                // Two original FBX animation clips are still sampled.
                Segment(FindClip("SleepArm_HiddenIdle"), .55f,
                    t => 0f, t => 14, false);
                // Wrist leads the forearm out of the bedding: two staged
                // movements, no fixed-velocity sliding beam. Hero endpoint
                // stays exactly the previously approved composition.
                Segment(raise,Mathf.Max(1.72f,raise.length*1.38f),
                    t => t*.82f, t => 14, false);
                Segment(wrist,Mathf.Max(.94f,wrist.length*1.16f),
                    t => .82f+.18f*t,t=>14,false);
                Segment(inspect, 2.4f, t => 1f, t => 14, false);
                Segment(crown, crown.length, t => 1f,
                    t => 14 + Mathf.FloorToInt(t * 4f), false);
                Segment(confirm, confirm.length, t => 1f, t => 18, true);
                // Down-and-away lowering has its own different wrist arc.
                Segment(lower,Mathf.Max(1.72f,lower.length*1.38f),
                    t => 1f-t*.92f,t=>18,true);
                Segment(FindClip("SleepArm_ReturnIdle"), .55f,
                    t => .08f*(1f-t), t => 18, true);

                // One identical, deterministic sound bank powers in-game
                // PlayOneShot and the real candidate MP4 audio stream.
                Assert.That(livingBendCount, Is.GreaterThan(4),
                    "Real PIP/DIP articulation must be visible in idle.");
                Assert.That(foleyEvents.Count, Is.GreaterThanOrEqualTo(8),
                    "No fabric/rotation/confirm feedback events were authored.");
                ArgonWatchFoley.WriteSoundtrack(
                    Path.Combine(outDir, "ArgonOriginalFoley_44100Hz.wav"),
                    frameNumber, Fps, foleyEvents);
                Assert.That(File.Exists(Path.Combine(outDir,
                    "ArgonOriginalFoley_44100Hz.wav")), Is.True);
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
                    "  \"original_game_oled\": false,\n" +
                    "  \"candidate_russian_oled\": true,\n" +
                    "  \"russian_labels_verified\": true,\n" +
                    "  \"original_fbx_clips\": true,\n" +
                    "  \"living_secondary_motion\": true,\n" +
                    "  \"gesture_motion\": \"asymmetric_wrist_lead_forearm_lag_quintic_v2\",\n" +
                    "  \"physical_crown_interaction\": true,\n" +
                    "  \"original_foley_audio\": true,\n" +
                    "  \"hero_pose\": \"sleeve-left-watch-center-fingers-right\",\n" +
                    "  \"finger_correction\": \"25-original-bones-plus-20-rest-compensated-finger-slots\"\n" +
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
                if (candidateCorrectedFingerSkin != null)
                    UnityEngine.Object.DestroyImmediate(candidateCorrectedFingerSkin);
                if (keyObj != null) UnityEngine.Object.DestroyImmediate(keyObj);
                if (cameraObj != null) UnityEngine.Object.DestroyImmediate(cameraObj);
                if (handRT != null) { handRT.Release(); UnityEngine.Object.DestroyImmediate(handRT); }
                if (finalRT != null) { finalRT.Release(); UnityEngine.Object.DestroyImmediate(finalRT); }
            }
        }
    }
}
#endif
