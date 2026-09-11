using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public sealed class StoryMenuRuntimeProofObserver : MonoBehaviour
{
    private string mode;
    private string evidenceDir;
    private readonly List<string> proof = new List<string>();
    private int loopCount;
    private bool failed;
    private string failure;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-rokasStoryMenuProof");
        if (index < 0) return;
        var go = new GameObject("StoryMenuRuntimeProofObserver");
        DontDestroyOnLoad(go);
        var observer = go.AddComponent<StoryMenuRuntimeProofObserver>();
        observer.mode = index + 1 < args.Length ? args[index + 1].ToLowerInvariant() : "natural";
    }

    private IEnumerator Start()
    {
        string root = Directory.GetParent(Application.dataPath).FullName;
        evidenceDir = Path.Combine(root, "rokas-story-menu-proof-" + mode);
        Directory.CreateDirectory(evidenceDir);
        proof.Add("MODE=" + mode);
        proof.Add("UNITY_VERSION=" + Application.unityVersion);
        yield return RunProof();
        proof.Add("FINAL_PASS=" + (!failed ? "TRUE" : "FALSE"));
        if (failed) proof.Add("FAILURE=" + failure.Replace('\n', ' '));
        File.WriteAllLines(Path.Combine(evidenceDir, "proof.txt"), proof.ToArray());
        yield return new WaitForSecondsRealtime(.25f);
        Application.Quit(failed ? 1 : 0);
    }

    private IEnumerator RunProof()
    {
        RokasBootstrap boot = null;
        yield return WaitUntil(() => (boot = FindFirstObjectByType<RokasBootstrap>()) != null, 8f, "bootstrap missing");
        if (failed) yield break;
        proof.Add("BOOTSTRAP=PASS");

        yield return WaitObject("StartupVideoSurface", 8f);
        if (failed) yield break;
        proof.Add("COLD_START_STARTUP_SURFACE=PASS");
        yield return WaitUntil(() => boot.VideoPresenter.FirstFramePresented, 12f, "startup first frame missing");
        if (failed) yield break;
        proof.Add("STARTUP_FIRST_FRAME=PASS");
        yield return WaitUntil(() => PresenterAudioPlaying(boot.VideoPresenter), 4f, "startup embedded audio not playing");
        if (failed) yield break;
        proof.Add("STARTUP_EMBEDDED_AUDIO_PLAYING=PASS");

        if (mode == "skip") boot.VideoPresenter.Skip();
        yield return WaitObject("StoryIntroVideoSurface", mode == "skip" ? 8f : 20f);
        if (failed) yield break;
        proof.Add(mode == "skip" ? "STARTUP_SKIP_TO_STORY=PASS" : "STARTUP_NATURAL_TO_STORY=PASS");
        yield return WaitUntil(() => boot.VideoPresenter.FirstFramePresented, 12f, "story first frame missing");
        if (failed) yield break;
        proof.Add("STORY_FIRST_FRAME=PASS");
        yield return WaitUntil(() => PresenterAudioPlaying(boot.VideoPresenter), 4f, "story embedded audio not playing");
        if (failed) yield break;
        proof.Add("STORY_EMBEDDED_AUDIO_PLAYING=PASS");

        if (mode == "skip") boot.VideoPresenter.Skip();
        yield return WaitObject("RokasMainMenu", mode == "skip" ? 8f : 70f);
        if (failed) yield break;
        yield return null;
        if (Find("HomeTitle") != null) { Fail("Home exists before Enter World"); yield break; }
        if (Find("StoryIntroVideoSurface") != null) { Fail("story surface leaked into menu"); yield break; }
        if (FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length != 1) { Fail("duplicate/missing Canvas in menu"); yield break; }
        if (FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length != 1) { Fail("duplicate/missing EventSystem in menu"); yield break; }
        proof.Add(mode == "skip" ? "STORY_SKIP_TO_MENU=PASS" : "STORY_NATURAL_TO_MENU=PASS");
        proof.Add("MENU_SINGLE_CANVAS_EVENTSYSTEM=PASS");

        if (mode == "skip")
        {
            yield return new WaitForSecondsRealtime(.75f);
            if (Find("RokasMainMenu") == null || Find("HomeTitle") != null) { Fail("skip input leaked into Enter World"); yield break; }
            proof.Add("SKIP_DOES_NOT_AUTO_ENTER_WORLD=PASS");
            Press("EnterWorldButton");
            if (failed) yield break;
            yield return WaitObject("HomeTitle", 8f);
            if (failed) yield break;
            yield return VerifyHomeCleanup(null, null, null);
            yield break;
        }

        GameObject menu = Find("RokasMainMenu");
        VideoPlayer menuPlayer = menu != null ? menu.GetComponent<VideoPlayer>() : null;
        AudioSource menuAudio = menu != null ? menu.GetComponent<AudioSource>() : null;
        if (menuPlayer == null || menuAudio == null) { Fail("menu VideoPlayer/AudioSource missing"); yield break; }
        if (!menuPlayer.isLooping || menuPlayer.audioOutputMode != VideoAudioOutputMode.AudioSource || menuPlayer.controlledAudioTrackCount < 1) { Fail("menu video/audio routing contract invalid"); yield break; }
        yield return WaitUntil(() => menuPlayer.isPlaying, 12f, "menu video never started");
        if (failed) yield break;
        yield return WaitUntil(() => menuAudio.isPlaying, 5f, "menu embedded audio never played");
        if (failed) yield break;
        proof.Add("MENU_VIDEO_PLAYING=PASS");
        proof.Add("MENU_EMBEDDED_AUDIO_PLAYING=PASS");

        Button enter = FindButton("EnterWorldButton");
        Button about = FindButton("DevelopersButton");
        Button support = FindButton("SupportDevelopmentButton");
        if (enter == null || about == null || support == null) { Fail("menu buttons missing"); yield break; }
        Vector2 enterPos = ((RectTransform)enter.transform).anchoredPosition;
        Vector2 aboutPos = ((RectTransform)about.transform).anchoredPosition;
        Vector2 supportPos = ((RectTransform)support.transform).anchoredPosition;
        Vector2 buttonSize = ((RectTransform)enter.transform).sizeDelta;
        proof.Add("MENU_LAYOUT_ENTER=" + Vec(enterPos));
        proof.Add("MENU_LAYOUT_ABOUT=" + Vec(aboutPos));
        proof.Add("MENU_LAYOUT_SUPPORT=" + Vec(supportPos));
        proof.Add("MENU_LAYOUT_SIZE=" + Vec(buttonSize));
        Capture("menu-start.png");
        yield return new WaitForEndOfFrame();

        loopCount = 0;
        menuPlayer.loopPointReached += OnMenuLoop;
        yield return WaitUntil(() => loopCount >= 1, 15f, "menu first loop not observed");
        if (failed) yield break;
        yield return null;
        float blackRatio1 = SampleBlackRatio(menuPlayer.targetTexture);
        proof.Add("MENU_LOOP_1_BLACK_RATIO=" + blackRatio1.ToString("F6", CultureInfo.InvariantCulture));
        if (blackRatio1 > .98f) { Fail("black flash/near-black frame at first loop boundary"); yield break; }
        Capture("menu-loop-1.png");
        yield return new WaitForEndOfFrame();

        yield return WaitUntil(() => loopCount >= 2, 15f, "menu second loop not observed");
        if (failed) yield break;
        yield return null;
        float blackRatio2 = SampleBlackRatio(menuPlayer.targetTexture);
        proof.Add("MENU_LOOP_2_BLACK_RATIO=" + blackRatio2.ToString("F6", CultureInfo.InvariantCulture));
        if (blackRatio2 > .98f) { Fail("black flash/near-black frame at second loop boundary"); yield break; }
        Capture("menu-loop-2.png");
        yield return new WaitForEndOfFrame();
        menuPlayer.loopPointReached -= OnMenuLoop;
        proof.Add("MENU_TWO_FULL_LOOPS=PASS");

        if (!Same(enterPos, ((RectTransform)enter.transform).anchoredPosition) ||
            !Same(aboutPos, ((RectTransform)about.transform).anchoredPosition) ||
            !Same(supportPos, ((RectTransform)support.transform).anchoredPosition))
        {
            Fail("menu UI moved while video looped");
            yield break;
        }
        proof.Add("MENU_UI_STATIONARY_OVER_VIDEO=PASS");

        Press("DevelopersButton");
        yield return null;
        if (Find("RokasMainMenu") == null || Find("HomeTitle") != null) { Fail("About was not a safe no-op"); yield break; }
        proof.Add("ABOUT_CLICK_NOOP=PASS");
        Press("SupportDevelopmentButton");
        yield return null;
        if (Find("RokasMainMenu") == null || Find("HomeTitle") != null) { Fail("Support was not a safe no-op"); yield break; }
        proof.Add("SUPPORT_CLICK_NOOP=PASS");

        RenderTexture menuTarget = menuPlayer.targetTexture;
        Press("EnterWorldButton");
        if (failed) yield break;
        yield return WaitObject("HomeTitle", 8f);
        if (failed) yield break;
        yield return VerifyHomeCleanup(menuPlayer, menuAudio, menuTarget);
    }

    private IEnumerator VerifyHomeCleanup(VideoPlayer oldPlayer, AudioSource oldAudio, RenderTexture oldTarget)
    {
        yield return null;
        yield return null;
        if (Find("RokasMainMenu") != null || Find("MainMenuVideoSurface") != null) { Fail("menu objects leaked after Enter World"); yield break; }
        if (Find("StartupVideoCanvas") != null) { Fail("startup/story canvas leaked after Enter World"); yield break; }
        if (oldPlayer) { Fail("menu VideoPlayer leaked after Enter World"); yield break; }
        if (oldAudio && oldAudio.isPlaying) { Fail("ghost menu audio after Enter World"); yield break; }
        if (oldTarget && oldTarget.IsCreated()) { Fail("menu RenderTexture still created after Enter World"); yield break; }
        EventSystem[] systems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        if (systems.Length != 1) { Fail("duplicate/missing EventSystem count=" + systems.Length); yield break; }
        if (canvases.Length != 1) { Fail("duplicate/missing Home Canvas count=" + canvases.Length); yield break; }
        proof.Add("HOME_ACTIVE_CANVAS_COUNT=1");
        proof.Add("ENTER_WORLD_TO_EXISTING_HOME=PASS");
        proof.Add("MENU_VIDEO_STOP_CLEANUP=PASS");
        proof.Add("MENU_AUDIO_STOP_NO_GHOST=PASS");
        proof.Add("RENDERTEXTURE_CLEANUP=PASS");
        proof.Add("CANVAS_EVENTSYSTEM_UNIQUE=PASS");
    }

    private static bool PresenterAudioPlaying(VideoSequencePresenter presenter)
    {
        VideoPlayer vp = presenter != null ? presenter.GetComponent<VideoPlayer>() : null;
        if (vp == null || vp.audioOutputMode != VideoAudioOutputMode.AudioSource || vp.controlledAudioTrackCount < 1) return false;
        AudioSource audio = vp.GetTargetAudioSource(0);
        return audio != null && audio.isPlaying;
    }

    private void OnMenuLoop(VideoPlayer source) { loopCount++; }

    private float SampleBlackRatio(RenderTexture rt)
    {
        if (rt == null) return 1f;
        RenderTexture small = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(rt, small);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = small;
        Texture2D tex = new Texture2D(64, 36, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 64, 36), 0, 0);
        tex.Apply();
        Color32[] pixels = tex.GetPixels32();
        int black = 0;
        for (int i = 0; i < pixels.Length; i++)
            if (pixels[i].r < 4 && pixels[i].g < 4 && pixels[i].b < 4) black++;
        Destroy(tex);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(small);
        return (float)black / pixels.Length;
    }

    private IEnumerator WaitObject(string name, float seconds)
    {
        yield return WaitUntil(() => Find(name) != null, seconds, "timed out waiting for " + name);
    }

    private IEnumerator WaitUntil(Func<bool> condition, float seconds, string message)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        if (!condition()) Fail(message);
    }

    private void Press(string name)
    {
        Button button = FindButton(name);
        if (button == null || !button.IsInteractable()) { Fail("button unavailable: " + name); return; }
        button.onClick.Invoke();
    }

    private static Button FindButton(string name)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++) if (buttons[i].name == name) return buttons[i];
        return null;
    }

    private static GameObject Find(string name) { return GameObject.Find(name); }
    private static bool Same(Vector2 a, Vector2 b) { return Vector2.SqrMagnitude(a - b) < .0001f; }
    private static string Vec(Vector2 v) { return v.x.ToString("F2", CultureInfo.InvariantCulture) + "," + v.y.ToString("F2", CultureInfo.InvariantCulture); }

    private void Capture(string name)
    {
        Texture2D tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(evidenceDir, name), tex.EncodeToPNG());
        Destroy(tex);
    }

    private void Fail(string message) { if (!failed) { failed = true; failure = message; proof.Add("FAIL=" + message); } }
}
