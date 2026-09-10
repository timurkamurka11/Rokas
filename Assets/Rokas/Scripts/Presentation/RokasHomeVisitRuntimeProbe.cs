using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Presentation
{
    // Temporary Development Player diagnostic for the manually observed Home-visit laptop replay.
    // It is completely dormant unless -rokasHomeVisitSmoke is present and is removed after RCA.
    public sealed class RokasHomeVisitRuntimeProbe : MonoBehaviour
    {
        private const string ProbeArgument = "-rokasHomeVisitSmoke";
        private const float DefaultWaitSeconds = 20f;
        private const float VideoWaitSeconds = 35f;

        [Serializable]
        private sealed class Evidence
        {
            public string mode = "ROKAS_HOME_VISIT_REAL_PLAYER_DIAGNOSTIC";
            public string unityVersion;
            public string platform;
            public string streamingAssetsPath;
            public bool startupHintVisible;
            public bool startupHintCapture;
            public bool startupCompleted;
            public string initialPhase;
            public int initialViewIdentity;
            public int initialLaptopIdentity;
            public bool visit1FirstBootStarted;
            public bool visit1FirstBootFirstFrame;
            public bool visit1FirstBootNaturalCompletion;
            public bool visit1FirstBootReady;
            public bool visit1SecondOpenReady;
            public bool acceptedViaUi;
            public bool laptopAfterAcceptanceReady;
            public bool actualLeaveReachedPortal;
            public bool actualCombatReached;
            public bool combatFinished;
            public bool actualReturnReachedPayment;
            public int afterReturnViewIdentity;
            public int afterReturnLaptopIdentity;
            public bool sameViewAfterReturn;
            public bool sameLaptopAfterReturn;
            public bool bootConsumedBeforeVisit2Open;
            public bool visit2FirstBootStarted;
            public bool visit2FirstBootFirstFrame;
            public bool visit2FirstBootNaturalCompletion;
            public bool visit2FirstBootReady;
            public bool bootConsumedAfterVisit2Boot;
            public bool visit2SecondOpenReady;
            public bool paymentClaimedViaUi;
            public int afterClaimViewIdentity;
            public int afterClaimLaptopIdentity;
            public bool sameViewAfterClaim;
            public bool sameLaptopAfterClaim;
            public bool bootConsumedAfterClaim;
            public bool unexpectedSecondBootAfterPayment;
            public bool laptopAfterPaymentReady;
            public bool secondReopenReady;
            public bool videoPlayerErrorReceived;
            public string videoPlayerError;
            public string failure;
            public bool finalPass;
        }

        private static readonly FieldInfo LaptopField = typeof(RokasView).GetField("laptop", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ConsumedField = typeof(LaptopView).GetField("bootConsumedThisHomeVisit", BindingFlags.Instance | BindingFlags.NonPublic);

        private RokasBootstrap boot;
        private VideoPlayer player;
        private Evidence evidence;
        private string outputDirectory;
        private string activeVideo;
        private bool activeFirstFrame;
        private bool activeLoopPoint;
        private bool activeVideoError;
        private string activeVideoErrorMessage;
        private bool waitSucceeded;
        private string waitFailure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (Application.isEditor || !Debug.isDebugBuild || !Requested(Environment.GetCommandLineArgs())) return;
            GameObject root = GameObject.Find("Rokas");
            if (!root)
            {
                Debug.LogError("ROKAS_HOME_VISIT_PROBE_INSTALL_FAIL: Rokas root was not found.");
                return;
            }
            if (!root.GetComponent<RokasHomeVisitRuntimeProbe>()) root.AddComponent<RokasHomeVisitRuntimeProbe>();
        }

        private static bool Requested(string[] args)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], ProbeArgument, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private IEnumerator Start()
        {
            outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "rokas-home-visit-smoke");
            Directory.CreateDirectory(outputDirectory);
            evidence = new Evidence
            {
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                streamingAssetsPath = Application.streamingAssetsPath
            };

            IEnumerator scenario = RunScenario();
            while (true)
            {
                bool moved;
                object current = null;
                try
                {
                    moved = scenario.MoveNext();
                    if (moved) current = scenario.Current;
                }
                catch (Exception exception)
                {
                    Fail(exception.GetType().Name + ": " + exception.Message);
                    yield break;
                }
                if (!moved) break;
                yield return current;
            }

            bool pass = evidence.startupHintVisible && evidence.startupCompleted &&
                        evidence.visit1FirstBootStarted && evidence.visit1FirstBootFirstFrame &&
                        evidence.visit1FirstBootNaturalCompletion && evidence.visit1FirstBootReady &&
                        evidence.visit1SecondOpenReady && evidence.acceptedViaUi &&
                        evidence.laptopAfterAcceptanceReady && evidence.actualLeaveReachedPortal &&
                        evidence.actualCombatReached && evidence.combatFinished &&
                        evidence.actualReturnReachedPayment && evidence.visit2FirstBootStarted &&
                        evidence.visit2FirstBootFirstFrame && evidence.visit2FirstBootNaturalCompletion &&
                        evidence.visit2FirstBootReady && evidence.bootConsumedAfterVisit2Boot &&
                        evidence.visit2SecondOpenReady && evidence.paymentClaimedViaUi &&
                        evidence.bootConsumedAfterClaim && !evidence.unexpectedSecondBootAfterPayment &&
                        evidence.laptopAfterPaymentReady && evidence.secondReopenReady &&
                        !evidence.videoPlayerErrorReceived;
            Finish(pass, pass ? string.Empty : "One or more real-player Home-visit invariants failed.");
        }

        private IEnumerator RunScenario()
        {
            boot = GetComponent<RokasBootstrap>();
            Require(boot != null, "RokasBootstrap is missing from the scene root.");

            yield return WaitFor(() => boot.VideoPresenter != null, DefaultWaitSeconds, "VideoSequencePresenter initialization");
            RequireWait();
            player = GetComponent<VideoPlayer>();
            Require(player != null, "Production VideoPlayer was not created.");
            player.frameReady += OnFrameReady;
            player.loopPointReached += OnLoopPointReached;
            player.errorReceived += OnVideoError;

            activeVideo = "StartupPreview.mp4";
            yield return WaitFor(() => StartupHintVisible(), VideoWaitSeconds, "visible StartupSkipHintOverlay after first frame");
            RequireWait();
            evidence.startupHintVisible = true;
            evidence.startupHintCapture = false;

            yield return WaitFor(() => boot.View != null, VideoWaitSeconds, "natural Startup Preview completion and RokasView creation");
            RequireWait();
            Require(!activeVideoError, "Startup VideoPlayer error: " + activeVideoErrorMessage);
            evidence.startupCompleted = true;
            evidence.initialPhase = boot.Session.State.phase.ToString();
            Require(boot.Session.State.phase == RunPhase.Home, "Expected initial Home phase, got " + boot.Session.State.phase);

            LaptopView laptop = GetLaptop();
            evidence.initialViewIdentity = Identity(boot.View);
            evidence.initialLaptopIdentity = Identity(laptop);

            // HOME VISIT #1: real H.264 boot must naturally complete once.
            yield return NaturalLaptopBoot("Home Visit #1");
            evidence.visit1FirstBootStarted = true;
            evidence.visit1FirstBootFirstFrame = activeFirstFrame;
            evidence.visit1FirstBootNaturalCompletion = activeLoopPoint;
            evidence.visit1FirstBootReady = IsLaptopReady();
            Require(evidence.visit1FirstBootFirstFrame && evidence.visit1FirstBootNaturalCompletion && evidence.visit1FirstBootReady,
                "Home Visit #1 real LaptopBoot did not naturally reach READY.");
            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen, DefaultWaitSeconds, "close Laptop after Home Visit #1 boot");
            RequireWait();

            Press("LaptopHotspot");
            yield return null;
            evidence.visit1SecondOpenReady = IsLaptopReady() && Find("LaptopBootSurface") == null && !boot.VideoPresenter.IsPlaying;
            Require(evidence.visit1SecondOpenReady, "Home Visit #1 second Laptop open replayed boot.");

            Press("LaptopContracts");
            Press("AcceptContract");
            yield return null;
            evidence.acceptedViaUi = boot.Session.State.phase == RunPhase.Accepted;
            Require(evidence.acceptedViaUi, "Contract was not accepted through the real Laptop UI.");
            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen, DefaultWaitSeconds, "close Laptop after acceptance");
            RequireWait();

            Press("LaptopHotspot");
            yield return null;
            evidence.laptopAfterAcceptanceReady = IsLaptopReady() && Find("LaptopBootSurface") == null;
            Require(evidence.laptopAfterAcceptanceReady, "Acceptance caused Laptop boot replay inside Home Visit #1.");
            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen, DefaultWaitSeconds, "close Laptop before leaving Home");
            RequireWait();

            Press("DoorHotspot");
            yield return WaitFor(() => boot.Session.State.phase == RunPhase.Portal && IsButtonReady("EnterPortal"), DefaultWaitSeconds,
                "real Home -> Portal transition");
            RequireWait();
            evidence.actualLeaveReachedPortal = true;

            Press("EnterPortal");
            yield return WaitFor(() => boot.Session.State.phase == RunPhase.Combat && FindButton("EnemyAttack") != null, DefaultWaitSeconds,
                "real Portal -> Combat transition");
            RequireWait();
            evidence.actualCombatReached = true;

            for (int i = 0; i < 260 && boot.Session.State.phase == RunPhase.Combat; i++)
            {
                boot.Session.Tick(.48f);
                if (boot.Session.State.phase != RunPhase.Combat) break;
                if (boot.Session.Combat.Stage == CombatStage.Ritual) TraceRitual();
                else if (boot.Session.Combat.Stage == CombatStage.Fighting) TapEnemy();
                yield return null;
            }
            evidence.combatFinished = boot.Session.State.phase == RunPhase.Sealed;
            Require(evidence.combatFinished, "Real combat path did not reach Sealed.");

            yield return WaitFor(() => IsButtonReady("ReturnHome"), DefaultWaitSeconds, "ReturnHome interaction");
            RequireWait();
            Press("ReturnHome");
            yield return WaitFor(() => boot.Session.State.phase == RunPhase.Payment && IsButtonReady("LaptopHotspot"), DefaultWaitSeconds,
                "real result -> Payment/Home return");
            RequireWait();
            evidence.actualReturnReachedPayment = true;

            LaptopView afterReturnLaptop = GetLaptop();
            evidence.afterReturnViewIdentity = Identity(boot.View);
            evidence.afterReturnLaptopIdentity = Identity(afterReturnLaptop);
            evidence.sameViewAfterReturn = evidence.initialViewIdentity == evidence.afterReturnViewIdentity;
            evidence.sameLaptopAfterReturn = evidence.initialLaptopIdentity == evidence.afterReturnLaptopIdentity;
            evidence.bootConsumedBeforeVisit2Open = BootConsumed(afterReturnLaptop);
            Require(!evidence.bootConsumedBeforeVisit2Open,
                "Outside -> Home return did not reset boot eligibility for Home Visit #2.");

            // HOME VISIT #2: first real boot naturally completes, then payment must not create another boot.
            yield return NaturalLaptopBoot("Home Visit #2");
            evidence.visit2FirstBootStarted = true;
            evidence.visit2FirstBootFirstFrame = activeFirstFrame;
            evidence.visit2FirstBootNaturalCompletion = activeLoopPoint;
            evidence.visit2FirstBootReady = IsLaptopReady();
            evidence.bootConsumedAfterVisit2Boot = BootConsumed(GetLaptop());
            Require(evidence.visit2FirstBootFirstFrame && evidence.visit2FirstBootNaturalCompletion &&
                    evidence.visit2FirstBootReady && evidence.bootConsumedAfterVisit2Boot,
                "Home Visit #2 real LaptopBoot did not consume the visit and reach READY.");
            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen, DefaultWaitSeconds, "close Laptop after Home Visit #2 boot");
            RequireWait();

            Press("LaptopHotspot");
            yield return null;
            evidence.visit2SecondOpenReady = IsLaptopReady() && Find("LaptopBootSurface") == null && !boot.VideoPresenter.IsPlaying;
            Require(evidence.visit2SecondOpenReady, "Home Visit #2 second open replayed boot before payment.");

            Press("LaptopContracts");
            Press("ClaimPayment");
            yield return null;
            evidence.paymentClaimedViaUi = boot.Session.State.phase == RunPhase.Home;
            Require(evidence.paymentClaimedViaUi, "Payment was not claimed through the real Laptop UI.");

            LaptopView afterClaimLaptop = GetLaptop();
            evidence.afterClaimViewIdentity = Identity(boot.View);
            evidence.afterClaimLaptopIdentity = Identity(afterClaimLaptop);
            evidence.sameViewAfterClaim = evidence.afterReturnViewIdentity == evidence.afterClaimViewIdentity;
            evidence.sameLaptopAfterClaim = evidence.afterReturnLaptopIdentity == evidence.afterClaimLaptopIdentity;
            evidence.bootConsumedAfterClaim = BootConsumed(afterClaimLaptop);

            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen && IsButtonReady("LaptopHotspot"), DefaultWaitSeconds,
                "close Laptop after ClaimPayment");
            RequireWait();

            Press("LaptopHotspot");
            yield return null;
            evidence.unexpectedSecondBootAfterPayment = Find("LaptopBootSurface") != null || boot.VideoPresenter.IsPlaying;
            evidence.laptopAfterPaymentReady = IsLaptopReady() && !evidence.unexpectedSecondBootAfterPayment;
            Require(evidence.laptopAfterPaymentReady,
                "MANUAL REPLAY REPRODUCED: ClaimPayment caused a second Laptop boot in Home Visit #2.");
            Press("ClosePanel");
            yield return WaitFor(() => !boot.View.LaptopOpen && IsButtonReady("LaptopHotspot"), DefaultWaitSeconds,
                "close first post-payment reopen");
            RequireWait();

            Press("LaptopHotspot");
            yield return null;
            evidence.secondReopenReady = IsLaptopReady() && Find("LaptopBootSurface") == null && !boot.VideoPresenter.IsPlaying;
            Require(evidence.secondReopenReady, "Repeated post-payment reopen did not remain READY.");
        }

        private IEnumerator NaturalLaptopBoot(string label)
        {
            activeVideo = "LaptopBoot.mp4";
            activeFirstFrame = false;
            activeLoopPoint = false;
            activeVideoError = false;
            activeVideoErrorMessage = string.Empty;

            Press("LaptopHotspot");
            Require(Find("LaptopBootSurface") != null, label + " did not enter BOOTING.");
            Require(boot.VideoPresenter.IsPlaying, label + " did not start production VideoSequencePresenter.");
            yield return WaitFor(() => activeFirstFrame, VideoWaitSeconds, label + " first decoded frame");
            RequireWait();
            yield return WaitFor(() => activeLoopPoint && IsLaptopReady(), VideoWaitSeconds, label + " natural completion -> READY");
            RequireWait();
            Require(!activeVideoError, label + " VideoPlayer error: " + activeVideoErrorMessage);
        }

        private IEnumerator WaitFor(Func<bool> predicate, float timeout, string label)
        {
            waitSucceeded = false;
            waitFailure = label;
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (predicate())
                {
                    waitSucceeded = true;
                    yield break;
                }
                yield return null;
            }
        }

        private void RequireWait()
        {
            Require(waitSucceeded, "Timed out waiting for " + waitFailure + ".");
        }

        private LaptopView GetLaptop()
        {
            Require(LaptopField != null, "RokasView.laptop reflection field not found.");
            var laptop = LaptopField.GetValue(boot.View) as LaptopView;
            Require(laptop != null, "Current RokasView has no LaptopView instance.");
            return laptop;
        }

        private static int Identity(object value)
        {
            return value == null ? 0 : RuntimeHelpers.GetHashCode(value);
        }

        private static bool BootConsumed(LaptopView laptop)
        {
            if (ConsumedField == null) throw new InvalidOperationException("LaptopView.bootConsumedThisHomeVisit reflection field not found.");
            return (bool)ConsumedField.GetValue(laptop);
        }

        private bool StartupHintVisible()
        {
            GameObject hint = Find("StartupSkipHintOverlay");
            if (!hint) return false;
            RawImage image = hint.GetComponent<RawImage>();
            CanvasGroup group = hint.GetComponent<CanvasGroup>();
            return image && group && image.color.a > .9f && group.alpha > .9f && boot.VideoPresenter.FirstFramePresented;
        }

        private bool IsLaptopReady()
        {
            LaptopView laptop = GetLaptop();
            return boot.View.LaptopOpen && !laptop.Booting && Find("LaptopContracts") != null;
        }

        private bool IsButtonReady(string name)
        {
            Button button = FindButton(name);
            return button && button.isActiveAndEnabled && button.IsInteractable();
        }

        private void Press(string name)
        {
            Button button = FindButton(name);
            Require(button != null, "Missing active interaction: " + name);
            Require(button.isActiveAndEnabled && button.IsInteractable(), "Interaction is not ready: " + name);
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            Require(ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler), "Pointer click was not handled: " + name);
        }

        private Button FindButton(string name)
        {
            if (!boot) return null;
            foreach (Button button in boot.GetComponentsInChildren<Button>())
                if (button.name == name) return button;
            return null;
        }

        private GameObject Find(string name)
        {
            if (!boot) return null;
            foreach (Transform item in boot.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private void TapEnemy()
        {
            Button button = FindButton("EnemyAttack");
            Require(button != null, "EnemyAttack missing during combat.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        private void TraceRitual()
        {
            Button button = FindButton("EnemyAttack");
            Require(button != null, "EnemyAttack missing during ritual.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            for (int i = 1; i <= 3; i++)
            {
                RectTransform point = null;
                foreach (RectTransform rect in boot.GetComponentsInChildren<RectTransform>())
                    if (rect.name == "RitualPoint" + i) point = rect;
                Require(point != null, "Missing ritual point " + i + ".");
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, point.TransformPoint(point.rect.center));
                if (i == 1) ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                else ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        }

        private void OnFrameReady(VideoPlayer source, long frame)
        {
            if (MatchesActiveVideo(source)) activeFirstFrame = true;
        }

        private void OnLoopPointReached(VideoPlayer source)
        {
            if (MatchesActiveVideo(source)) activeLoopPoint = true;
        }

        private void OnVideoError(VideoPlayer source, string message)
        {
            if (!MatchesActiveVideo(source)) return;
            activeVideoError = true;
            activeVideoErrorMessage = message ?? string.Empty;
            evidence.videoPlayerErrorReceived = true;
            evidence.videoPlayerError = activeVideoErrorMessage;
        }

        private bool MatchesActiveVideo(VideoPlayer source)
        {
            return source == player && !string.IsNullOrEmpty(activeVideo) &&
                   !string.IsNullOrEmpty(source.url) && source.url.IndexOf(activeVideo, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Fail(string message)
        {
            Finish(false, message);
        }

        private void Finish(bool pass, string message)
        {
            if (evidence == null) evidence = new Evidence();
            evidence.finalPass = pass;
            if (!string.IsNullOrEmpty(message)) evidence.failure = message;
            try
            {
                Directory.CreateDirectory(outputDirectory ?? Directory.GetCurrentDirectory());
                File.WriteAllText(Path.Combine(outputDirectory ?? Directory.GetCurrentDirectory(), "rokas-home-visit-smoke-result.json"),
                    JsonUtility.ToJson(evidence, true));
            }
            catch (Exception exception)
            {
                Debug.LogError("ROKAS_HOME_VISIT_PROBE_WRITE_FAIL: " + exception);
                pass = false;
            }

            if (player)
            {
                player.frameReady -= OnFrameReady;
                player.loopPointReached -= OnLoopPointReached;
                player.errorReceived -= OnVideoError;
            }
            Debug.Log(pass ? "ROKAS_HOME_VISIT_SMOKE_PASS" : "ROKAS_HOME_VISIT_SMOKE_FAIL: " + evidence.failure);
            Application.Quit(pass ? 0 : 1);
        }
    }
}
