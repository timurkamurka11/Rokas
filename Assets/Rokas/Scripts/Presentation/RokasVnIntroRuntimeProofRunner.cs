#nullable enable
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Development-player-only CI evidence seam for the real VN intro flow.
    /// It is dormant unless -rokasVnIntroProof is supplied explicitly.
    /// </summary>
    public sealed class RokasVnIntroRuntimeProofRunner : MonoBehaviour
    {
        private const string ProofArgument = "-rokasVnIntroProof";
        private const string ScenarioArgument = "-rokasVnIntroScenario";
        private const string OutputArgument = "-rokasVnIntroOutput";
        private const string ScreenWidthArgument = "-screen-width";
        private const string ScreenHeightArgument = "-screen-height";
        private const int DefaultScreenWidth = 1920;
        private const int DefaultScreenHeight = 1080;
        private const float StartupTimeoutSeconds = 90f;
        private const float UiTimeoutSeconds = 12f;
        private const string MinaLine = "Я дома, приходи, нужно поговорить.";

        private static bool spawned;
        private Evidence evidence = new Evidence();
        private string outputDirectory = string.Empty;
        private bool waitSucceeded;

        [Serializable]
        private sealed class Evidence
        {
            public string mode = "ROKAS_BUILT_WINDOWS_PLAYER_VN_INTRO_PROOF";
            public string scenario = string.Empty;
            public string unityVersion = string.Empty;
            public string platform = string.Empty;
            public bool bootstrapFound;
            public bool mainMenuReached;
            public bool enteredIntro;
            public bool busFrame;
            public bool vnUiPresent;
            public bool pauseBlocksAdvance;
            public bool pauseKeepsTimeScale;
            public bool muteEnabledByUi;
            public bool muteRestoredByUi;
            public bool nightSkyFrame;
            public bool oneClickOneBeat;
            public bool minaPhoneFrame;
            public bool minaLineExact;
            public bool safeHomeHandoff;
            public bool progressPersisted;
            public bool skipUsed;
            public bool bypassStartedCompleted;
            public bool bypassedIntro;
            public bool finalPass;
            public string error = string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachForProof()
        {
            if (Application.isEditor || !Debug.isDebugBuild || spawned ||
                !IsRequested(Environment.GetCommandLineArgs()))
            {
                return;
            }

            spawned = true;
            var root = new GameObject("RokasVnIntroRuntimeProofRunner");
            DontDestroyOnLoad(root);
            root.AddComponent<RokasVnIntroRuntimeProofRunner>();
        }

        public static bool IsRequested(string[]? args)
        {
            if (args == null) return false;
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], ProofArgument, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static string GetScenario(string[]? args)
        {
            string value = GetArgumentValue(args, ScenarioArgument);
            if (string.Equals(value, "natural", StringComparison.OrdinalIgnoreCase)) return "natural";
            if (string.Equals(value, "skip", StringComparison.OrdinalIgnoreCase)) return "skip";
            if (string.Equals(value, "bypass", StringComparison.OrdinalIgnoreCase)) return "bypass";
            return string.Empty;
        }

        public static Vector2Int GetRequestedResolution(string[]? args)
        {
            int width = ParsePositiveInt(GetArgumentValue(args, ScreenWidthArgument), DefaultScreenWidth);
            int height = ParsePositiveInt(GetArgumentValue(args, ScreenHeightArgument), DefaultScreenHeight);
            return new Vector2Int(width, height);
        }

        public static bool IsVnPresentationUnobscured()
        {
            return GameObject.Find("EnterWorldCurtain") == null;
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            evidence.scenario = GetScenario(args);
            evidence.unityVersion = Application.unityVersion;
            evidence.platform = Application.platform.ToString();

            string requestedOutput = GetArgumentValue(args, OutputArgument);
            string root = string.IsNullOrWhiteSpace(requestedOutput)
                ? Path.Combine(Directory.GetCurrentDirectory(), "rokas-vn-intro-proof")
                : Path.GetFullPath(requestedOutput);
            outputDirectory = Path.Combine(root, string.IsNullOrEmpty(evidence.scenario) ? "invalid" : evidence.scenario);
            Directory.CreateDirectory(outputDirectory);

            if (string.IsNullOrEmpty(evidence.scenario))
            {
                Finish(false, "Missing or unsupported -rokasVnIntroScenario value.");
                yield break;
            }

            Vector2Int requestedResolution = GetRequestedResolution(args);
            Screen.SetResolution(requestedResolution.x, requestedResolution.y, FullScreenMode.Windowed);

            RokasBootstrap? bootstrap = null;
            yield return WaitFor(() =>
            {
                bootstrap = UnityEngine.Object.FindFirstObjectByType<RokasBootstrap>();
                return bootstrap != null;
            }, UiTimeoutSeconds);
            evidence.bootstrapFound = waitSucceeded && bootstrap != null;
            if (!evidence.bootstrapFound)
            {
                Finish(false, "RokasBootstrap was not found in the built player.");
                yield break;
            }

            if (evidence.scenario == "natural" || evidence.scenario == "skip")
            {
                PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
                PlayerPrefs.Save();
            }

            yield return WaitFor(() => GameObject.Find("RokasMainMenu") != null, StartupTimeoutSeconds);
            evidence.mainMenuReached = waitSucceeded;
            if (!evidence.mainMenuReached)
            {
                Finish(false, "Normal startup did not reach RokasMainMenu.");
                yield break;
            }

            if (evidence.scenario == "natural")
                yield return RunNatural(bootstrap!);
            else if (evidence.scenario == "skip")
                yield return RunSkip(bootstrap!);
            else
                yield return RunBypass(bootstrap!);

            if (!string.IsNullOrEmpty(evidence.error))
            {
                Finish(false, evidence.error);
                yield break;
            }

            bool pass = evidence.scenario == "natural"
                ? evidence.enteredIntro && evidence.busFrame && evidence.vnUiPresent &&
                  evidence.pauseBlocksAdvance && evidence.pauseKeepsTimeScale &&
                  evidence.muteEnabledByUi && evidence.muteRestoredByUi &&
                  evidence.nightSkyFrame && evidence.oneClickOneBeat &&
                  evidence.minaPhoneFrame && evidence.minaLineExact &&
                  evidence.safeHomeHandoff && evidence.progressPersisted
                : evidence.scenario == "skip"
                    ? evidence.enteredIntro && evidence.busFrame && evidence.skipUsed &&
                      evidence.safeHomeHandoff && evidence.progressPersisted
                    : evidence.bypassStartedCompleted && evidence.bypassedIntro && evidence.safeHomeHandoff;
            Finish(pass, pass ? string.Empty : "Runtime VN intro proof did not satisfy all required assertions.");
        }

        private IEnumerator RunNatural(RokasBootstrap bootstrap)
        {
            ClickNamedButton(GameObject.Find("RokasMainMenu"), "EnterWorldButton");
            yield return WaitFor(() => GameObject.Find("VnIntroRoot") != null, UiTimeoutSeconds);
            evidence.enteredIntro = waitSucceeded;
            if (!evidence.enteredIntro)
            {
                evidence.error = "First Enter World did not open VnIntroRoot.";
                yield break;
            }

            GameObject vnRoot = GameObject.Find("VnIntroRoot");
            yield return WaitFor(() => HasBackground(vnRoot, "VN_BusStop_Rain_Night") && HasNonEmptyLine(vnRoot), UiTimeoutSeconds);
            evidence.busFrame = waitSucceeded;
            evidence.vnUiPresent = HasRequiredVnUi(vnRoot);
            if (!evidence.busFrame || !evidence.vnUiPresent)
            {
                evidence.error = "Initial VN bus-stop beat or required VN UI was missing.";
                yield break;
            }

            yield return WaitFor(IsVnPresentationUnobscured, UiTimeoutSeconds);
            if (!waitSucceeded)
            {
                evidence.error = "Initial VN presentation remained obscured by the Enter World transition curtain.";
                yield break;
            }
            yield return Capture("natural-01-bus-stop.png");

            float timeScaleBeforePause = Time.timeScale;
            string backgroundBeforePause = BackgroundTextureName(vnRoot);
            string lineBeforePause = DirectText(vnRoot, "DialogueText");
            ClickDirectButton(vnRoot, "PauseButton");
            ClickDirectButton(vnRoot, "StoryClickSurface");
            yield return WaitRealtime(.35f);
            evidence.pauseBlocksAdvance = backgroundBeforePause == BackgroundTextureName(vnRoot) &&
                                          lineBeforePause == DirectText(vnRoot, "DialogueText");
            evidence.pauseKeepsTimeScale = Mathf.Approximately(timeScaleBeforePause, Time.timeScale);
            ClickDirectButton(vnRoot, "PauseButton");

            RokasAudio? audio = GetAudio(bootstrap);
            ClickDirectButton(vnRoot, "MuteButton");
            yield return null;
            evidence.muteEnabledByUi = audio != null && audio.VnMuted;
            ClickDirectButton(vnRoot, "MuteButton");
            yield return null;
            evidence.muteRestoredByUi = audio != null && !audio.VnMuted;

            ClickDirectButton(vnRoot, "StoryClickSurface");
            yield return WaitFor(() => HasBackground(vnRoot, "VN_NightSky_Rain"), UiTimeoutSeconds);
            evidence.nightSkyFrame = waitSucceeded;
            if (!evidence.nightSkyFrame)
            {
                evidence.error = "One continue did not advance from bus stop to night sky.";
                yield break;
            }
            yield return Capture("natural-02-night-sky.png");
            yield return WaitRealtime(.35f);
            evidence.oneClickOneBeat = HasBackground(vnRoot, "VN_NightSky_Rain");

            ClickDirectButton(vnRoot, "StoryClickSurface");
            yield return WaitFor(() => HasBackground(vnRoot, "VN_BusStop_PhoneMessage_Mina") &&
                                       DirectText(vnRoot, "SpeakerName") == "Mina" &&
                                       DirectText(vnRoot, "DialogueText") == MinaLine,
                UiTimeoutSeconds);
            evidence.minaPhoneFrame = waitSucceeded && HasBackground(vnRoot, "VN_BusStop_PhoneMessage_Mina");
            evidence.minaLineExact = waitSucceeded && DirectText(vnRoot, "DialogueText") == MinaLine;
            if (!evidence.minaPhoneFrame || !evidence.minaLineExact)
            {
                evidence.error = "Mina phone beat or exact Mina line was not presented.";
                yield break;
            }
            yield return Capture("natural-03-mina-phone.png");
            yield return WaitRealtime(.35f);
            evidence.oneClickOneBeat &= HasBackground(vnRoot, "VN_BusStop_PhoneMessage_Mina");

            ClickDirectButton(vnRoot, "StoryClickSurface");
            yield return WaitFor(() => bootstrap.View != null && GameObject.Find("VnIntroRoot") == null, UiTimeoutSeconds);
            evidence.safeHomeHandoff = waitSucceeded && bootstrap.View != null && GameObject.Find("VnIntroRoot") == null;
            evidence.progressPersisted = new PlayerPrefsVnIntroProgress().IsCompleted;
            if (evidence.safeHomeHandoff)
            {
                yield return WaitRealtime(.45f);
                yield return Capture("natural-04-home.png");
            }
        }

        private IEnumerator RunSkip(RokasBootstrap bootstrap)
        {
            ClickNamedButton(GameObject.Find("RokasMainMenu"), "EnterWorldButton");
            yield return WaitFor(() => GameObject.Find("VnIntroRoot") != null, UiTimeoutSeconds);
            evidence.enteredIntro = waitSucceeded;
            if (!evidence.enteredIntro)
            {
                evidence.error = "Skip scenario did not open VnIntroRoot.";
                yield break;
            }

            GameObject vnRoot = GameObject.Find("VnIntroRoot");
            yield return WaitFor(() => HasBackground(vnRoot, "VN_BusStop_Rain_Night"), UiTimeoutSeconds);
            evidence.busFrame = waitSucceeded;
            if (!evidence.busFrame)
            {
                evidence.error = "Skip scenario did not reach the bus-stop beat.";
                yield break;
            }

            yield return WaitFor(IsVnPresentationUnobscured, UiTimeoutSeconds);
            if (!waitSucceeded)
            {
                evidence.error = "Skip proof remained obscured by the Enter World transition curtain.";
                yield break;
            }
            yield return Capture("skip-01-before.png");

            evidence.skipUsed = ClickDirectButton(vnRoot, "SkipButton");
            yield return WaitFor(() => bootstrap.View != null && GameObject.Find("VnIntroRoot") == null, UiTimeoutSeconds);
            evidence.safeHomeHandoff = waitSucceeded && bootstrap.View != null && GameObject.Find("VnIntroRoot") == null;
            evidence.progressPersisted = new PlayerPrefsVnIntroProgress().IsCompleted;
            if (evidence.safeHomeHandoff)
            {
                yield return WaitRealtime(.45f);
                yield return Capture("skip-02-home.png");
            }
        }

        private IEnumerator RunBypass(RokasBootstrap bootstrap)
        {
            evidence.bypassStartedCompleted = new PlayerPrefsVnIntroProgress().IsCompleted;
            if (!evidence.bypassStartedCompleted)
            {
                evidence.error = "Bypass scenario requires a persisted completed intro from the preceding run.";
                yield break;
            }

            bool introObserved = false;
            ClickNamedButton(GameObject.Find("RokasMainMenu"), "EnterWorldButton");
            float deadline = Time.realtimeSinceStartup + UiTimeoutSeconds;
            while (bootstrap.View == null && Time.realtimeSinceStartup < deadline)
            {
                if (GameObject.Find("VnIntroRoot") != null) introObserved = true;
                yield return null;
            }
            if (GameObject.Find("VnIntroRoot") != null) introObserved = true;
            evidence.safeHomeHandoff = bootstrap.View != null;
            evidence.bypassedIntro = bootstrap.View != null && !introObserved;
            if (evidence.safeHomeHandoff)
            {
                yield return WaitRealtime(.45f);
                yield return Capture("bypass-01-home.png");
            }
        }

        private IEnumerator WaitFor(Func<bool> predicate, float timeoutSeconds)
        {
            waitSucceeded = false;
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    waitSucceeded = true;
                    yield break;
                }
                yield return null;
            }
            waitSucceeded = predicate();
        }

        private static IEnumerator WaitRealtime(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private IEnumerator Capture(string fileName)
        {
            string path = Path.Combine(outputDirectory, fileName);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0) yield break;
                yield return null;
            }
            if (string.IsNullOrEmpty(evidence.error))
                evidence.error = "Screenshot was not written: " + fileName;
        }

        private static bool ClickNamedButton(GameObject? root, string childName)
        {
            Transform? child = FindDescendant(root, childName);
            Button? button = child ? child.GetComponent<Button>() : null;
            if (!button) return false;
            button.onClick.Invoke();
            return true;
        }

        private static bool ClickDirectButton(GameObject root, string childName)
        {
            return ClickNamedButton(root, childName);
        }

        public static bool HasRequiredVnUi(GameObject root)
        {
            if (!root) return false;
            return FindDescendant(root, "DialoguePanel") != null &&
                   FindDescendant(root, "Portrait") != null &&
                   FindDescendant(root, "SpeakerName") != null &&
                   FindDescendant(root, "DialogueText") != null &&
                   FindDescendant(root, "MuteButton") != null &&
                   FindDescendant(root, "PauseButton") != null &&
                   FindDescendant(root, "SkipButton") != null &&
                   FindDescendant(root, "BackButton") != null &&
                   FindDescendant(root, "NextButton") != null;
        }

        private static Transform? FindDescendant(GameObject? root, string name)
        {
            if (!root) return null;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (string.Equals(descendant.name, name, StringComparison.Ordinal))
                    return descendant;
            }
            return null;
        }

        private static bool HasBackground(GameObject root, string expectedTextureName)
        {
            return string.Equals(BackgroundTextureName(root), expectedTextureName, StringComparison.Ordinal);
        }

        private static string BackgroundTextureName(GameObject root)
        {
            if (!root) return string.Empty;
            Transform? child = root.transform.Find("Background");
            RawImage? image = child ? child.GetComponent<RawImage>() : null;
            return image && image.texture ? image.texture.name : string.Empty;
        }

        private static bool HasNonEmptyLine(GameObject root)
        {
            return !string.IsNullOrEmpty(DirectText(root, "DialogueText"));
        }

        private static string DirectText(GameObject root, string childName)
        {
            Transform? child = FindDescendant(root, childName);
            Text? text = child ? child.GetComponent<Text>() : null;
            return text ? text.text : string.Empty;
        }

        private static RokasAudio? GetAudio(RokasBootstrap bootstrap)
        {
            FieldInfo? field = typeof(RokasBootstrap).GetField("sound", BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(bootstrap) as RokasAudio;
        }

        private static string GetArgumentValue(string[]? args, string name)
        {
            if (args == null) return string.Empty;
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                    return args[index + 1] ?? string.Empty;
            }
            return string.Empty;
        }

        private static int ParsePositiveInt(string value, int fallback)
        {
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : fallback;
        }

        private void Finish(bool pass, string error)
        {
            evidence.finalPass = pass;
            if (!string.IsNullOrEmpty(error)) evidence.error = error;
            try
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "vn-intro-proof-result.json"),
                    JsonUtility.ToJson(evidence, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("ROKAS VN intro proof could not write evidence: " + exception);
                pass = false;
            }

            Debug.Log(pass ? "ROKAS_VN_INTRO_PROOF_PASS" : "ROKAS_VN_INTRO_PROOF_FAIL: " + evidence.error);
            Application.Quit(pass ? 0 : 1);
        }
    }
}