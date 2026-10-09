using System;
using System.Collections.Generic;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum ReactiveDisplayPhase
    {
        Command, Reacting, OffenseTiming, CounterOffer, WaveTransition, Result, Suspended, SaveBlocked
    }

    public sealed class ReactiveEnemyDisplay
    {
        public string Id;
        public int Slot;
        public string Name;
        public int Hp;
        public int MaxHp;
        public int Seal;
        public int SealMax;
        public bool Broken;
    }

    public struct ReactiveBattleDisplay
    {
        public int HunterHp;
        public int HunterAp;
        public IReadOnlyList<ReactiveEnemyDisplay> Enemies;
        public int WaveSlotCount;
        public IReadOnlyList<string> WaveEnemyIds;
        public int Wave;
        public int WaveCount;
        public int DefeatedCount;
        public string SelectedTargetId;
        public string ActingId;
        public bool CanSealStrike;
        public bool CanSweep;
        public bool CanHeavy;
        public bool CanPreviewHeavy;
        public bool CanThrow;
        public bool CanPreviewThrow;
        public bool CanAnchor;
        public string Forecast;
        public string Telegraph;
        public string Detail;
        public string CommandPreview;
        public float ContactProgress;
        public string ActionId;
        public string HitId;
        public long TimingStartUs;
        public long TimingImpactUs;
        public long TimingEndUs;
        public DefenseWindowProfile DefenseWindow;
        public bool IncomingHit;
        public DefenseResponseMask AllowedResponses;
        public long OffenseEarlyUs;
        public long OffenseLateUs;
        public ReactiveDisplayPhase Phase;
    }

    // The view publishes command and selection intent only. Core remains authoritative.
    public sealed class ReactiveMissionView
    {
        private static readonly Unity.Profiling.ProfilerMarker ResearchTimingMarker = new Unity.Profiling.ProfilerMarker("Rokas.Combat.TimingRefresh");

        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly Action basic;
        private readonly Action sealStrike;
        private readonly Action defend;
        private readonly Action sweep;
        private readonly Action heavy;
        private readonly Action anchor;
        private readonly Action dodge;
        private readonly Action parry;
        private readonly Action counter;
        private readonly Action retrySave;
        private readonly Action<string> chooseTarget;
        private readonly List<string> activeIds = new List<string>(4);
        private readonly List<GameObject> hunterStatusElements = new List<GameObject>();
        private readonly List<GameObject> entryHudElements = new List<GameObject>(48);
        private readonly string[] targetIds = new string[4];
        private readonly Button[] targetButtons = new Button[4];
        private readonly Text[] targetTexts = new Text[4];
        private readonly Text[] targetHpTexts = new Text[4];
        private readonly Image[] targetHpFills = new Image[4];
        private readonly Image[] targetHighlights = new Image[4];
        private readonly string[] hpOwners = new string[4];
        private readonly float[] targetHpCurrent = new float[4];
        private readonly float[] targetHpGoal = new float[4];
        private readonly float[] targetHpStart = new float[4];
        private readonly float[] targetHpTween = new float[4];
        private readonly RectTransform[] targetSlotRects = new RectTransform[4];
        private readonly Text[] hunterApPips = new Text[6];
        private RectTransform root;
        private ReactiveCombatArena arena;
        private RawImage hunter;
        private RawImage enemy;
        private Text hunterHp;
        private Text ap;
        private Image hunterHpFill;
        private Text targetName;
        private Text targetHp;
        private Text targetSeal;
        private Text wave;
        private Text forecast;
        private Text telegraph;
        private Text detail;
        private Text selectionHint;
        private Text commandPreview;
        private Text hitFeedback;
        private Text selectedAction;
        private Text waveBanner;
        private Text announcement;
        private float announcementRemaining;
        private const float AnnouncementDuration = .85f;
        private Image timingBeacon;
        private RawImage contactTrack;
        private RawImage contactFill;
        private RawImage attackWarningArt;
        private RawImage timingPromptArt;
        private RawImage timingNearArt;
        private RawImage timingPerfectArt;
        private Text defenseHint;
        private Image dodgeWindow;
        private Image blockWindow;
        private Image perfectWindow;
        private RectTransform offenseTiming;
        private Image offenseCursor;
        private Image offenseSuccessWindow;
        private Text offenseHint;
        private GameObject commandPanel;
        private GameObject defensePanel;
        private GameObject counterPanel;
        private GameObject saveBlockedPanel;
        private Button sealStrikeButton;
        private Button sweepButton;
        private Button heavyButton;
        private Button throwButton;
        private Image normalSelected, heavySelected, throwSelected;
        private readonly Action throwingBlade;
        private Button anchorButton;
        private float feedbackTime;
        private double feedbackStartedAt;
        private string feedbackTargetId;
        private bool feedbackOnHunter;
        private Color feedbackColor;
        private string selectedTargetId;
        private bool presentationLocked;
        private float hunterHpCurrent;
        private float hunterHpGoal;
        private float hunterHpStart;
        private float hunterHpTween;
        private bool hunterHpInitialized;
        private ReactiveBattleDisplay latestDisplay;
        private string timingActionId;
        private string timingHitId;
        private string resolvedActionId;
        private string resolvedHitId;
        private float cursorFreezeTime;
        private float frozenProgress;
        private bool defenseAttemptAccepted;
        private float resolutionHold;
        private Vector2 feedbackOrigin;
        private const float TimingX = 690f;
        private const float TimingY = 668f;
        private const float TimingWidth = 540f;
        private const float HpTweenDuration = .30f;

        public string SelectedTargetId { get { return selectedTargetId; } }
        public int AnimatedEnemyCount { get { return arena == null ? 0 : arena.VisibleEnemyCount; } }
        public bool AnimatedActorsReady { get { return arena != null && arena.Ready; } }
        public bool HunterApproachComplete { get { return arena == null || arena.HunterApproachComplete; } }
        public bool HunterAtHome { get { return arena == null || arena.HunterAtHome; } }
        public bool ArenaSettled => arena == null || arena.PresentationReady;
        public bool HunterEntryComplete => arena != null && arena.HunterEntryComplete;
        public string HunterPresentationActionId => arena?.HunterPresentationActionId;
        public event Action<string, string, bool> AttackPresentationStarted;
        public event Action<string, string, bool, string> SwingStarted;
        public event Action<string> ThrowReleased;
        public bool AnnouncementActive => announcementRemaining > 0f;
        public bool PresentationReady => (arena == null || arena.CommandReady) && !AnnouncementActive;
        public bool PreviewConfirmed => arena != null && arena.PreviewConfirmed;
        public bool ThrowReady => arena != null && arena.ThrowReady;
        public bool SelectHunterPreview(string action) => arena != null && arena.SelectHunterPreview(action);
        public bool StartHunterThrow(string target) { if (arena == null || !arena.StartHunterThrow(target)) return false; SetPresentationLocked(true); return true; }
        public bool ConfirmHunterPreview() => arena != null && arena.ConfirmHunterPreview();
        public void CancelHunterPreview() { arena?.CancelHunterPreview(); }
        public float ReactiveCorpseElapsed(string id) => arena == null ? -1f : arena.CorpseElapsed(id);

        public void ShowAnnouncement(string message)
        {
            if (announcement == null || string.IsNullOrEmpty(message) || EntryPresentationActive) return;
            announcement.text = message;
            announcementRemaining = AnnouncementDuration;
            announcement.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, 0f);
            announcement.gameObject.SetActive(true);
            commandPanel?.SetActive(false);
            counterPanel?.SetActive(false);
            for (int i = 0; i < targetButtons.Length; i++)
                targetButtons[i]?.gameObject.SetActive(false);
            RefreshTiming();
        }

        public void ShowTurnAnnouncement(bool playerTurn)
        { ShowAnnouncement(playerTurn ? "ВАШ ХОД" : "ЁКАЙ АТАКУЕТ"); }

        public bool StartHunterApproach(string enemyId, bool heavy = false)
        {
            if (arena == null || !arena.StartHunterApproach(enemyId, heavy)) return false;
            SetPresentationLocked(true);
            return true;
        }

        public void StartHunterReturn()
        {
            SetPresentationLocked(true);
            arena?.StartHunterReturn();
        }

        public void CancelHunterMotion()
        {
            arena?.CancelHunterMotion();
            SetPresentationLocked(false);
        }

        public void SetPresentationLocked(bool value)
        {
            presentationLocked = value;
            if (commandPanel != null && value) commandPanel.SetActive(false);
            if (selectionHint != null && value) selectionHint.gameObject.SetActive(false);
            if (root != null) RefreshTiming();
            if (!value) return;
            for (int i = 0; i < targetButtons.Length; i++)
                if (targetButtons[i] != null)
                {
                    targetButtons[i].interactable = false;
                    targetButtons[i].gameObject.SetActive(false);
                }
        }

        public ReactiveMissionView(UiKit ui, RokasAssets assets, Action basic, Action sealStrike,
            Action defend, Action sweep, Action heavy, Action anchor, Action dodge, Action parry,
            Action counter, Action retrySave, Action<string> chooseTarget, Action throwingBlade = null)
        {
            this.ui = ui;
            this.assets = assets;
            this.basic = basic;
            this.sealStrike = sealStrike;
            this.defend = defend;
            this.sweep = sweep;
            this.heavy = heavy;
            this.throwingBlade = throwingBlade;
            this.anchor = anchor;
            this.dodge = dodge;
            this.parry = parry;
            this.counter = counter;
            this.retrySave = retrySave;
            this.chooseTarget = chooseTarget;
        }

        public void Build(RectTransform parent)
        {
            root = ui.Rect(parent, "ReactiveArena", 0, 100, 1920, 906);
            arena = new ReactiveCombatArena(ui, root);
            arena.ActorFrameRendered += RefreshRenderedHitFeedback;
            arena.AttackPresentationStarted += (id, actor, heavy) => AttackPresentationStarted?.Invoke(id, actor, heavy);
            arena.SwingStarted += (id, actor, heavy, hit) => SwingStarted?.Invoke(id, actor, heavy, hit);
            arena.ThrowReleased += id => ThrowReleased?.Invoke(id);
            arena.BeginEncounterIntro();
            Sprite enemyCardSprite = Resources.Load<Sprite>(
                "Combat/ReactiveTurns/UI/HUD/Hp bar button ui of monscter");
            Sprite keikoProfileSprite = Resources.Load<Sprite>(
                "Combat/ReactiveTurns/UI/HUD/Keiko ui profile during battle");

            // The illustration is the environment. Only translucent HUD layers cover it.
            ui.Box(root, "ReactiveLowerShade", 0, 702, 1920, 204, new Color(.015f, .02f, .04f, .77f));
            ui.Box(root, "ReactiveTopShade", 0, 0, 1920, 127, new Color(.015f, .02f, .045f, .69f));
            ui.Box(root, "ReactiveEnemyGround", 1110, 674, 616, 4, new Color(.94f, .19f, .36f, .5f));

            ui.Box(root, "ReactiveForecastPanel", 450, 11, 891, 97, new Color(.018f, .027f, .052f, .88f));
            ui.Box(root, "ReactiveForecastRule", 450, 11, 891, 2, UiKit.Gold);
            ui.Label(root, "ReactiveForecastTitle", "TURN FORECAST  /  ОЧЕРЁДНОСТЬ", 472, 16, 847, 27, 17,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            forecast = ui.Label(root, "ReactiveForecast", "", 474, 46, 845, 54, 22,
                UiKit.Paper, false, TextAnchor.MiddleCenter);

            ui.Box(root, "ReactiveWavePanel", 1370, 11, 503, 116, new Color(.018f, .027f, .052f, .9f));
            ui.Box(root, "ReactiveWaveRule", 1370, 11, 503, 2, UiKit.Red);
            wave = ui.Label(root, "ReactiveWave", "ВОЛНА 1 / 3", 1390, 15, 463, 36, 24,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            enemy = ui.Art(root, "ReactiveEnemyFacelessCommuter", assets.enemy, 1393, 56, 53, 60);
            targetName = ui.Label(root, "ReactiveTargetName", "ЁКАЙ", 1462, 56, 384, 28, 20, UiKit.Gold);
            targetHp = ui.Label(root, "ReactiveTargetHp", "HP —", 1462, 82, 174, 34, 22, UiKit.Paper, true);
            targetSeal = ui.Label(root, "ReactiveTargetSeal", "SEAL —", 1633, 82, 213, 34, 19,
                UiKit.Jade, true, TextAnchor.MiddleRight);

            ui.Box(root, "ReactiveHunterPanel", 30, 739, 344, 143, new Color(.026f, .025f, .037f, .97f));
            ui.Box(root, "ReactiveHunterRule", 30, 739, 344, 3, UiKit.Gold);
            ui.Box(root, "ReactiveHunterBottomRule", 30, 880, 344, 2, UiKit.Gold);
            ui.Box(root, "ReactiveHunterPortraitFrame", 37, 746, 110, 130, UiKit.Gold);
            hunter = ui.Art(root, "ReactiveHunterKeiko", assets.vnKeikoCharacterSheet, 40, 749, 104, 124);
            hunter.uvRect = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").BodyUv;
            ApplyArtwork(hunter, keikoProfileSprite, new Rect(170, 0, 700, 790), 1920, 819);
            ui.Label(root, "ReactiveHunterName", "КЕЙКО", 155, 746, 211, 29, 18, UiKit.Gold);
            hunterHp = ui.Label(root, "ReactiveHunterHp", "HP —", 155, 778, 211, 30, 22, UiKit.Paper, true);
            ui.Box(root, "ReactiveHunterHpTrack", 155, 813, 198, 8, new Color(.21f, .12f, .15f));
            hunterHpFill = ui.Box(root, "ReactiveHunterHpFill", 155, 813, 198, 8,
                new Color(.86f, .12f, .23f));
            ap = ui.Label(root, "ReactiveAp", "AP —", 155, 825, 211, 27, 19, UiKit.Jade, true);
            for (int i = 0; i < hunterApPips.Length; i++)
                hunterApPips[i] = ui.Label(root, "ReactiveApPip" + (i + 1), "●",
                    155 + 31 * i, 850, 28, 28, 23, UiKit.Jade, true, TextAnchor.MiddleCenter);

            for (int i = 0; i < targetButtons.Length; i++)
            {
                hpOwners[i] = null;
                int slot = i;
                targetSlotRects[i] = ui.Rect(root, "ReactiveEnemySlot" + (i + 1),
                    TargetSlotX(3, i), 320, 205, 43);
                targetButtons[i] = ui.Button(targetSlotRects[i], "ReactiveTarget" + (i + 1), "",
                    0, 0, 205, 43, () => SelectTarget(slot));
                targetHighlights[i] = ui.Box(targetButtons[i].transform, "SelectedTargetRule",
                    0, 0, 205, 3, UiKit.Gold);
                targetTexts[i] = targetButtons[i].GetComponentInChildren<Text>();
                targetTexts[i].rectTransform.anchoredPosition = new Vector2(8, -2);
                targetTexts[i].rectTransform.sizeDelta = new Vector2(118, 24);
                targetTexts[i].fontSize = 13;
                targetTexts[i].color = UiKit.Gold;
                RawImage cardTop = ui.Art(targetButtons[i].transform, "EnemyCardOrnament",
                    null, 1, 0, 203, 3);
                ApplyArtwork(cardTop, enemyCardSprite, new Rect(270, 295, 1240, 44), 1774, 887);
                RawImage portrait = ui.Art(targetButtons[i].transform, "EnemyPortrait",
                    assets.enemy, 7, 10, 53, 57);
                ApplyArtwork(portrait, enemyCardSprite, new Rect(318, 337, 284, 260), 1774, 887);
                portrait.gameObject.SetActive(false);
                targetHpTexts[i] = ui.Label(targetButtons[i].transform, "EnemyHp", "HP —",
                    126, 2, 72, 24, 11, UiKit.Paper, true);
                ui.Box(targetButtons[i].transform, "EnemyHpTrack", 39, 29, 127, 7,
                    new Color(.22f, .10f, .15f));
                targetHpFills[i] = ui.Box(targetButtons[i].transform, "EnemyHpFill", 39, 29,
                    127, 7, new Color(.89f, .13f, .27f));
                ui.Box(targetButtons[i].transform, "EnemyCardBottomRule", 7, 41,
                    191, 2, UiKit.Gold);
                targetButtons[i].gameObject.SetActive(false);
            }

            selectionHint = ui.Label(root, "ReactiveSelectionHint", "ВЫБЕРИ ЦЕЛЬ И ДЕЙСТВИЕ",
                390, 703, 1485, 32, 19, UiKit.Gold, false, TextAnchor.MiddleCenter);
            selectionHint.gameObject.SetActive(false);

            attackWarningArt = AddArtwork(root, "ReactiveAttackWarningArt",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI ATTACK SHOWS WHEN ENEMY ATTACKS",
                474, 565, 180, 44, new Rect(17, 4, 2138, 690), 2172, 724);
            telegraph = ui.Label(root, "ReactiveTelegraph", "", TimingX, TimingY - 52, TimingWidth, 31, 19,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            detail = ui.Label(root, "ReactiveDetail", "", 473, 669, 541, 28, 17,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            timingPromptArt = AddArtwork(root, "ReactiveTimingPromptArt",
                "Combat/ReactiveTurns/UI/DefenceReaction/TEXT UI SHOWS CLICK IN TIME",
                617, 603, 254, 29, new Rect(170, 207, 1830, 307), 2172, 724);
            contactTrack = AddArtwork(root, "ReactiveContactTrack",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI TIMING BAR",
                TimingX - 12, TimingY - 10, TimingWidth + 24, 43,
                new Rect(72, 288, 2027, 143), 2172, 724);
            contactFill = AddArtwork(root, "ReactiveContactFill",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI MOVING BAR",
                TimingX, TimingY, 0, 23, new Rect(183, 268, 1408, 335), 1774, 887);
            dodgeWindow = ui.Box(root, "ReactiveDodgeWindow", TimingX, TimingY, 0, 23,
                new Color(.18f, .74f, .83f, .42f));
            blockWindow = ui.Box(root, "ReactiveBlockWindow", TimingX, TimingY, 0, 23,
                new Color(.30f, .95f, .87f, .75f));
            perfectWindow = ui.Box(root, "ReactivePerfectZone", TimingX, TimingY, 0, 23,
                new Color(1f, .82f, .30f, .94f));
            timingPerfectArt = AddArtwork(root, "ReactivePerfectWindow",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI BAR CLICK ON RIGHT TIME",
                TimingX, TimingY - 7, 15, 37, new Rect(445, 352, 363, 592), 1254, 1254);
            timingNearArt = AddArtwork(root, "ReactiveNearBars",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI NEAR BARS",
                TimingX, TimingY - 2, 80, 28, new Rect(429, 259, 1314, 201), 2172, 724);
            timingBeacon = ui.Box(root, "ReactiveTimingBeacon", TimingX, TimingY - 8, 6, 39,
                Color.white);
            ui.Box(timingBeacon.transform, "CursorTip", -4, -2, 14, 4, UiKit.Paper);
            defenseHint = ui.Label(root, "ReactiveDefenseHint",
                "Q — УКЛОНЕНИЕ     E — БЛОК",
                TimingX - 20, TimingY + 35, TimingWidth + 40, 25, 14, UiKit.Paper, false, TextAnchor.MiddleCenter);
            offenseTiming = ui.Rect(root, "ReactiveOffenseTiming", 740, 716, 440, 55);
            ui.Box(offenseTiming, "OffenseTrack", 0, 33, 440, 6,
                new Color(.16f, .14f, .13f, .9f));
            offenseSuccessWindow = ui.Box(offenseTiming, "OffenseSuccessWindow", 0, 29, 0, 14,
                new Color(1f, .75f, .28f, .6f));
            offenseCursor = ui.Box(offenseTiming, "OffenseCursor", 0, 26, 4, 20, UiKit.Gold);
            offenseHint = ui.Label(offenseTiming, "OffenseHint", "SPACE / ЛКМ — ТЯЖЁЛЫЙ УДАР",
                0, 0, 440, 25, 17, UiKit.Gold, true, TextAnchor.MiddleCenter);
            hitFeedback = ui.Label(root, "ReactiveHitFeedback", "", 174, 426, 430, 44, 25,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            selectedAction = ui.Label(root, "ReactiveSelectedAction", "", 390, 704, 1485, 31, 18,
                UiKit.Gold, true, TextAnchor.MiddleCenter);
            selectedAction.gameObject.SetActive(false);
            waveBanner = ui.Label(root, "ReactiveWaveBanner", "", 534, 324, 852, 135, 51,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            announcement = ui.Label(root, "ReactiveAnnouncement", "", 480, -56, 960, 68, 32,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            var announcementOutline = announcement.gameObject.AddComponent<Outline>();
            announcementOutline.effectColor = new Color(.005f, .008f, .012f, .8f);
            announcementOutline.effectDistance = new Vector2(1.5f, -1.5f);
            announcement.gameObject.SetActive(false);
            announcementRemaining = 0f;

            commandPanel = ui.Rect(root, "ReactiveCommands", 390, 738, 1485, 144).gameObject;
            ui.Box(commandPanel.transform, "CommandShade", 0, 0, 1485, 144, new Color(.018f, .028f, .049f, .95f));
            ui.Label(commandPanel.transform, "CommandTitle", "ВАШ ХОД  /  ВЫБЕРИТЕ ДЕЙСТВИЕ", 18, 4, 1444, 31, 17,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            Button basicButton = ui.Button(commandPanel.transform, "ReactiveBasic", "Обычный удар",
                170, 33, 320, 94, basic, true, true, false);
            AttachButtonArtwork(basicButton, "Combat/ReactiveTurns/UI/Actions/Normal hit button",
                new Rect(426, 144, 1320, 436), 2172, 724);
            sealStrikeButton = ui.Button(commandPanel.transform, "ReactiveSealStrike",
                "Удар печати · 3 AP", 260, 37, 230, 72, sealStrike, false, true, false);
            AttachButtonArtwork(sealStrikeButton, "Combat/ReactiveTurns/UI/Actions/Pring impact",
                new Rect(283, 142, 1607, 429), 2172, 724);
            AddCostLabel(sealStrikeButton, "3 AP");
            Button defendButton = ui.Button(commandPanel.transform, "ReactiveDefend", "Защита",
                505, 37, 230, 72, defend);
            AttachButtonArtwork(defendButton, "Combat/ReactiveTurns/UI/Actions/Defence button",
                new Rect(398, 145, 1377, 418), 2172, 724);
            sweepButton = ui.Button(commandPanel.transform, "ReactiveSweep", "Размах · 3 AP",
                750, 37, 230, 72, sweep, false, true, false);
            AttachButtonArtwork(sweepButton, "Combat/ReactiveTurns/UI/Actions/Whifh button",
                new Rect(345, 163, 1482, 391), 2172, 724);
            AddCostLabel(sweepButton, "3 AP");
            anchorButton = ui.Button(commandPanel.transform, "ReactiveAnchor", "Якорь · 2 AP",
                995, 37, 230, 72, anchor);
            AttachButtonArtwork(anchorButton, "Combat/ReactiveTurns/UI/Actions/Ancot button",
                new Rect(432, 170, 1308, 379), 2172, 724);
            AddCostLabel(anchorButton, "2 AP");
            heavyButton = ui.Button(commandPanel.transform, "ReactiveHeavy", "Тяжёлый · 5 AP",
                566, 33, 340, 94, heavy, false, true, false);
            AttachButtonArtwork(heavyButton, "Combat/ReactiveTurns/UI/Actions/Heavy hit",
                new Rect(247, 142, 1678, 462), 2172, 724);
            AddCostLabel(heavyButton, "5 AP");
            throwButton = ui.Button(commandPanel.transform, "ReactiveThrow", "БРОСОК КИНЖАЛА",
                996, 33, 302, 94, throwingBlade, false, true, false);
            throwButton.GetComponent<Image>().color = new Color(.015f, .025f, .038f, .96f);
            throwButton.transform.Find("Title").gameObject.SetActive(false);
            throwButton.transform.Find("LeftRule").gameObject.SetActive(false);
            ui.Box(throwButton.transform, "RightRule", 301, 0, 1, 94, UiKit.Gold);
            ui.Box(throwButton.transform, "DaggerLeftRule", 0, 0, 1, 94, UiKit.Gold);
            ui.Box(throwButton.transform, "BottomRule", 0, 93, 302, 1, UiKit.Gold);
            ui.Label(throwButton.transform, "ThrowTitle", "Бросок\nкинжала", 78, 8, 204, 70, 26,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Label(throwButton.transform, "ThrowGlyph", "➶", 14, 9, 62, 72, 46,
                UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Label(throwButton.transform, "ActionCost", "2 AP", 240, 75, 53, 16, 13,
                UiKit.Jade, true, TextAnchor.MiddleRight);
            normalSelected = ui.Box(commandPanel.transform, "NormalSelected", 195, 123, 270, 3, UiKit.Jade);
            heavySelected = ui.Box(commandPanel.transform, "HeavySelected", 596, 123, 280, 3, UiKit.Jade);
            throwSelected = ui.Box(commandPanel.transform, "ThrowSelected", 1010, 123, 270, 3, UiKit.Jade);
            sealStrikeButton.gameObject.SetActive(false);
            defendButton.gameObject.SetActive(false);
            sweepButton.gameObject.SetActive(false);
            anchorButton.gameObject.SetActive(false);
            commandPreview = ui.Label(commandPanel.transform, "ReactiveCommandPreview", "",
                18, 127, 1450, 17, 13, UiKit.Muted, false, TextAnchor.MiddleCenter);

            defensePanel = ui.Rect(root, "ReactiveDefense", 642, 730, 636, 114).gameObject;
            ui.Box(defensePanel.transform, "DefenseShade", 0, 0, 636, 114,
                new Color(.014f, .026f, .051f, .66f));
            ui.Box(defensePanel.transform, "DefenseRule", 0, 0, 636, 2, UiKit.Jade);
            ui.Label(defensePanel.transform, "DefenseTitle", "Q — УКЛОНЕНИЕ     E — БЛОК", 14, 5, 608, 31, 18,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            Button dodgeButton = ui.Button(defensePanel.transform, "ReactiveDodge", "Q — Уклонение",
                24, 14, 276, 81, dodge, true);
            AttachButtonArtwork(dodgeButton,
                "Combat/ReactiveTurns/UI/DefenceReaction/UI DODGE BUTTON",
                new Rect(65, 343, 1325, 443), 1448, 1086);
            Button blockButton = ui.Button(defensePanel.transform, "ReactiveParry", "E — Блок",
                336, 14, 276, 81, parry, true);
            AttachButtonArtwork(blockButton,
                "Combat/ReactiveTurns/UI/DefenceReaction/UI BLOCK BUTTON",
                new Rect(56, 321, 1340, 446), 1448, 1086);
            ui.Label(dodgeButton.transform, "DefenseKeyQ", "Q", 12, 6, 28, 27, 18,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Label(blockButton.transform, "DefenseKeyE", "E", 12, 6, 28, 27, 18,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            defensePanel.transform.Find("DefenseTitle").gameObject.SetActive(false);

            counterPanel = ui.Rect(root, "ReactiveCounter", 622, 712, 680, 170).gameObject;
            ui.Box(counterPanel.transform, "CounterShade", 0, 0, 680, 170,
                new Color(.055f, .07f, .074f, .97f));
            ui.Label(counterPanel.transform, "CounterTitle", "КОНТРАТАКА  /  ШАНС ОТКРЫТ",
                20, 8, 640, 49, 24, UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Button(counterPanel.transform, "ReactiveCounterConfirm", "Контратаковать",
                145, 68, 390, 75, counter, true, true, false);

            saveBlockedPanel = ui.Rect(root, "ReactiveSaveBlocked", 622, 712, 680, 170).gameObject;
            ui.Box(saveBlockedPanel.transform, "SaveBlockedShade", 0, 0, 680, 170,
                new Color(.15f, .055f, .04f, .97f));
            ui.Label(saveBlockedPanel.transform, "SaveBlockedTitle", "ПРОФИЛЬ НЕ ЗАПИСАН",
                20, 8, 640, 49, 24, UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Button(saveBlockedPanel.transform, "ReactiveRetrySave", "Повторить запись",
                145, 68, 390, 75, retrySave, true);

            commandPanel.SetActive(false);
            defensePanel.SetActive(false);
            counterPanel.SetActive(false);
            saveBlockedPanel.SetActive(false);
            attackWarningArt.gameObject.SetActive(false);
            timingPromptArt.gameObject.SetActive(false);
            timingNearArt.gameObject.SetActive(false);
            timingPerfectArt.gameObject.SetActive(false);
            contactTrack.gameObject.SetActive(false);
            contactFill.gameObject.SetActive(false);
            timingBeacon.gameObject.SetActive(false);
            defenseHint.gameObject.SetActive(false);
            dodgeWindow.gameObject.SetActive(false);
            blockWindow.gameObject.SetActive(false);
            perfectWindow.gameObject.SetActive(false);
            offenseTiming.gameObject.SetActive(false);
            hitFeedback.text = string.Empty;
            hitFeedback.color = Color.clear;
            waveBanner.gameObject.SetActive(false);
            feedbackTime = 0f;
            hunterHpInitialized = false;
            timingActionId = timingHitId = resolvedActionId = resolvedHitId = null;
            cursorFreezeTime = resolutionHold = 0f;
            feedbackTime = 0f;
            feedbackStartedAt = 0d;
            feedbackTargetId = null;
            foreach (string clutter in new[] { "ReactiveTopShade", "ReactiveLowerShade", "ReactiveEnemyGround",
                "ReactiveForecastPanel", "ReactiveForecastRule", "ReactiveForecastTitle", "ReactiveForecast",
                "ReactiveWavePanel", "ReactiveWaveRule", "ReactiveWave", "ReactiveEnemyFacelessCommuter",
                "ReactiveTargetName", "ReactiveTargetHp", "ReactiveTargetSeal", "ReactiveSelectionHint" })
                root.Find(clutter).gameObject.SetActive(false);
            commandPanel.transform.Find("CommandShade").gameObject.SetActive(false);
            commandPanel.transform.Find("CommandTitle").gameObject.SetActive(false);
            commandPreview.gameObject.SetActive(false);
            hunterStatusElements.Clear();
            entryHudElements.Clear();
            foreach (string statusName in new[] { "ReactiveHunterPanel", "ReactiveHunterRule", "ReactiveHunterBottomRule",
                "ReactiveHunterPortraitFrame", "ReactiveHunterKeiko", "ReactiveHunterName", "ReactiveHunterHp",
                "ReactiveHunterHpTrack", "ReactiveHunterHpFill", "ReactiveAp" })
                hunterStatusElements.Add(root.Find(statusName).gameObject);
            for (int i = 0; i < hunterApPips.Length; i++) hunterStatusElements.Add(hunterApPips[i].gameObject);
            SetHunterStatusVisible(false);
            CacheEntryHudElements();
            HideEntryHud();
        }

        private bool EntryPresentationActive => arena != null && !arena.IntroComplete;

        private void CacheEntryHudElements()
        {
            entryHudElements.Clear();
            foreach (string objectName in new[]
            {
                "ReactiveTopShade", "ReactiveLowerShade", "ReactiveEnemyGround",
                "ReactiveForecastPanel", "ReactiveForecastRule", "ReactiveForecastTitle", "ReactiveForecast",
                "ReactiveWavePanel", "ReactiveWaveRule", "ReactiveWave", "ReactiveEnemyFacelessCommuter",
                "ReactiveTargetName", "ReactiveTargetHp", "ReactiveTargetSeal",
                "ReactiveHunterPanel", "ReactiveHunterRule", "ReactiveHunterBottomRule",
                "ReactiveHunterPortraitFrame", "ReactiveHunterKeiko", "ReactiveHunterName",
                "ReactiveHunterHp", "ReactiveHunterHpTrack", "ReactiveHunterHpFill", "ReactiveAp",
                "ReactiveSelectionHint", "ReactiveTelegraph", "ReactiveDetail",
                "ReactiveAttackWarningArt", "ReactiveTimingPromptArt", "ReactiveContactTrack",
                "ReactiveContactFill", "ReactiveDodgeWindow", "ReactiveBlockWindow", "ReactivePerfectZone",
                "ReactivePerfectWindow", "ReactiveNearBars", "ReactiveTimingBeacon", "ReactiveDefenseHint",
                "ReactiveOffenseTiming", "ReactiveHitFeedback", "ReactiveSelectedAction",
                "ReactiveWaveBanner", "ReactiveAnnouncement"
            })
            {
                Transform found = root.Find(objectName);
                if (found != null) entryHudElements.Add(found.gameObject);
            }

            for (int i = 0; i < hunterApPips.Length; i++)
                if (hunterApPips[i] != null) entryHudElements.Add(hunterApPips[i].gameObject);
            for (int i = 0; i < targetButtons.Length; i++)
                if (targetButtons[i] != null) entryHudElements.Add(targetButtons[i].gameObject);

            if (commandPanel != null) entryHudElements.Add(commandPanel);
            if (defensePanel != null) entryHudElements.Add(defensePanel);
            if (counterPanel != null) entryHudElements.Add(counterPanel);
            if (saveBlockedPanel != null) entryHudElements.Add(saveBlockedPanel);
        }

        private void HideEntryHud()
        {
            for (int i = 0; i < entryHudElements.Count; i++)
            {
                GameObject element = entryHudElements[i];
                if (element != null && element.activeSelf) element.SetActive(false);
            }
        }

        private void SetHunterStatusVisible(bool visible)
        {
            for (int i = 0; i < hunterStatusElements.Count; i++)
                if (hunterStatusElements[i].activeSelf != visible) hunterStatusElements[i].SetActive(visible);
        }

        private RawImage AddArtwork(Transform parent, string objectName, string resourcePath,
            float x, float y, float width, float height, Rect crop, float sourceWidth, float sourceHeight)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            RawImage image = ui.Art(parent, objectName, null, x, y, width, height);
            ApplyArtwork(image, sprite, crop, sourceWidth, sourceHeight);
            return image;
        }

        private static void ApplyArtwork(RawImage image, Sprite sprite, Rect crop,
            float sourceWidth, float sourceHeight)
        {
            if (sprite == null)
            {
                if (image.texture == null) image.gameObject.SetActive(false);
                return;
            }
            image.texture = sprite.texture;
            image.uvRect = new Rect(crop.x / sourceWidth,
                1f - (crop.y + crop.height) / sourceHeight,
                crop.width / sourceWidth, crop.height / sourceHeight);
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private void AttachButtonArtwork(Button button, string resourcePath, Rect crop,
            float sourceWidth, float sourceHeight)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null) return;
            RectTransform rect = button.GetComponent<RectTransform>();
            RawImage art = ui.Art(button.transform, "SuppliedCombatArt", null,
                0, 0, rect.rect.width, rect.rect.height);
            ApplyArtwork(art, sprite, crop, sourceWidth, sourceHeight);
            button.targetGraphic = art;
            Text title = button.GetComponentInChildren<Text>();
            if (title != null) title.gameObject.SetActive(false);
            Transform topRule = button.transform.Find("TopRule");
            if (topRule != null) topRule.gameObject.SetActive(false);
            Transform leftRule = button.transform.Find("LeftRule");
            if (leftRule != null) leftRule.gameObject.SetActive(false);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, .87f, 1f);
            colors.selectedColor = new Color(.91f, 1f, 1f, 1f);
            colors.pressedColor = new Color(.72f, .85f, .92f, 1f);
            colors.disabledColor = new Color(.58f, .58f, .62f, .72f);
            button.colors = colors;
        }

        private void AddCostLabel(Button button, string cost)
        {
            if (button.targetGraphic == null || !(button.targetGraphic is RawImage)) return;
            RectTransform rect = button.GetComponent<RectTransform>();
            ui.Label(button.transform, "ActionCost", cost, rect.rect.width - 61,
                rect.rect.height - 19, 53, 16, 13, UiKit.Jade, true, TextAnchor.MiddleRight);
        }

        private static float TargetSlotX(int waveSlotCount, int slot)
        {
            if (waveSlotCount <= 1) return 1320f;
            if (waveSlotCount == 2) return 1120f + 285f * slot;
            if (waveSlotCount == 3) return 990f + 260f * slot;
            return 920f + 235f * slot;
        }

        private void SelectTarget(int slot)
        {
            if (presentationLocked || !PresentationReady || slot < 0 || slot >= targetIds.Length ||
                string.IsNullOrEmpty(targetIds[slot])) return;
            chooseTarget?.Invoke(targetIds[slot]);
        }

        private void UpdateTargetHighlights()
        {
            for (int i = 0; i < targetButtons.Length; i++)
            {
                if (targetButtons[i] == null || !targetButtons[i].gameObject.activeSelf) continue;
                var panel = targetButtons[i].GetComponent<Image>();
                panel.color = targetIds[i] == selectedTargetId
                    ? new Color(.27f, .13f, .22f, .96f) : new Color(.075f, .105f, .13f, .88f);
                targetHighlights[i].gameObject.SetActive(targetIds[i] == selectedTargetId);
            }
        }

        public void Present(CombatEvent combatEvent, AttackSequenceDefinition sequence = null)
        {
            if (root == null || combatEvent == null) return;
            int acceptedBefore = arena == null ? 0 : arena.AcceptedContactCount;
            arena?.Present(combatEvent, sequence);
            if (combatEvent.Kind == CombatEventKind.HitResolved && arena != null &&
                arena.AcceptedContactCount == acceptedBefore) return;
            if (combatEvent.Kind == CombatEventKind.AttackStarted)
            {
                return;
            }
            if (combatEvent.Kind != CombatEventKind.HitResolved) return;
            feedbackTargetId = combatEvent.Amount > 0 ? combatEvent.TargetId : null;
            feedbackOnHunter = combatEvent.TargetId == ReactiveDuelDefinitions.HunterId;
            feedbackColor = feedbackOnHunter ? UiKit.Red : UiKit.Gold;
            if (feedbackOnHunter)
            {
                resolvedActionId = combatEvent.ActionId;
                resolvedHitId = combatEvent.HitId;
                resolutionHold = .30f;
                feedbackOrigin = new Vector2(174f, -426f);
                switch (combatEvent.Detail)
                {
                    case "Dodge": hitFeedback.text = "УКЛОНЕНИЕ"; feedbackColor = UiKit.Jade; break;
                    case "Parry": hitFeedback.text = "БЛОК"; feedbackColor = UiKit.Jade; break;
                    case "Perfect": hitFeedback.text = "ИДЕАЛЬНЫЙ БЛОК"; feedbackColor = UiKit.Gold; break;
                    case "EarlyFail": hitFeedback.text = "СЛИШКОМ РАНО"; break;
                    case "LateFail": hitFeedback.text = "СЛИШКОМ ПОЗДНО"; break;
                    case "WrongDefense": hitFeedback.text = "НЕВЕРНАЯ ЗАЩИТА"; break;
                    default: hitFeedback.text = "ПОПАДАНИЕ  −" + combatEvent.Amount + " HP"; break;
                }
            }
            else
            {
                int targetSlot = Array.IndexOf(targetIds, combatEvent.TargetId);
                float x = targetSlot >= 0 ? targetSlotRects[targetSlot].anchoredPosition.x - 112f : 1120f;
                feedbackOrigin = new Vector2(x, -422f);
                hitFeedback.text = combatEvent.Detail == "Counter"
                    ? "КОНТРАТАКА  −" + combatEvent.Amount
                    : combatEvent.Detail == "TimedHeavy"
                    ? "ТОЧНЫЙ УДАР  −" + combatEvent.Amount
                    : "УДАР  −" + combatEvent.Amount;
            }
            hitFeedback.color = feedbackColor;
            BeginHitFeedback();
        }

        public void PresentDefenseAttempt(DefenseAttempt attempt, long pressUs)
        {
            if (root == null || presentationLocked || attempt == null || !latestDisplay.IncomingHit) return;
            if (defenseAttemptAccepted && !attempt.Accepted) return;
            long duration = latestDisplay.TimingEndUs - latestDisplay.TimingStartUs;
            frozenProgress = duration > 0 ? Mathf.Clamp01((float)(pressUs - latestDisplay.TimingStartUs) / duration) : 1f;
            cursorFreezeTime = .18f;
            defenseAttemptAccepted = attempt.Accepted;
            if (attempt.Accepted && (attempt.Outcome == DefenseOutcome.Dodge ||
                attempt.Outcome == DefenseOutcome.Parry || attempt.Outcome == DefenseOutcome.Perfect))
                arena?.BeginDefense(attempt.Outcome == DefenseOutcome.Dodge,
                    Mathf.Max(0f, (float)((latestDisplay.TimingImpactUs - pressUs) / 1000000d)));
            feedbackOnHunter = true;
            feedbackOrigin = new Vector2(174f, -426f);
            feedbackColor = UiKit.Red;
            switch (attempt.Outcome)
            {
                case DefenseOutcome.Dodge: hitFeedback.text = "УКЛОНЕНИЕ"; feedbackColor = UiKit.Jade; break;
                case DefenseOutcome.Parry: hitFeedback.text = "БЛОК"; feedbackColor = UiKit.Jade; break;
                case DefenseOutcome.Perfect: hitFeedback.text = "ИДЕАЛЬНЫЙ БЛОК"; feedbackColor = UiKit.Gold; break;
                case DefenseOutcome.EarlyFail: hitFeedback.text = "СЛИШКОМ РАНО"; break;
                case DefenseOutcome.LateFail: hitFeedback.text = "СЛИШКОМ ПОЗДНО"; break;
                case DefenseOutcome.WrongDefense: hitFeedback.text = "НЕВЕРНАЯ ЗАЩИТА"; break;
                default:
                    long acquireOpen = latestDisplay.TimingImpactUs -
                        (latestDisplay.DefenseWindow ?? DefenseWindowProfile.Standard).AcquireEarlyUs;
                    hitFeedback.text = pressUs < acquireOpen ? "СЛИШКОМ РАНО" :
                        pressUs > latestDisplay.TimingEndUs ? "СЛИШКОМ ПОЗДНО" : "ЗАЩИТА НЕ ПРИНЯТА";
                    break;
            }
            feedbackTargetId = null;
            hitFeedback.color = feedbackColor;
            BeginHitFeedback();
            RefreshTiming();
        }

        public void Refresh(ReactiveBattleDisplay display)
        {
            if (root == null) return;
            latestDisplay = display;
            if (arena != null) arena.EnemyContactResolutionDelay =
                (float)((display.DefenseWindow ?? DefenseWindowProfile.Standard).AcquireLateUs / 1000000d) + .04f;
            hunterHp.text = "HP " + display.HunterHp + " / 100";
            ap.text = "AP " + display.HunterAp + " / 6";
            float hunterGoal = Mathf.Clamp01(display.HunterHp / 100f);
            if (!hunterHpInitialized)
            {
                hunterHpStart = hunterHpCurrent = hunterHpGoal = hunterGoal;
                hunterHpTween = HpTweenDuration;
                hunterHpInitialized = true;
                hunterHpFill.rectTransform.sizeDelta = new Vector2(198f * hunterGoal, 8f);
            }
            else if (!Mathf.Approximately(hunterHpGoal, hunterGoal))
            {
                hunterHpStart = hunterHpCurrent;
                hunterHpGoal = hunterGoal;
                hunterHpTween = 0f;
            }
            for (int i = 0; i < hunterApPips.Length; i++)
                hunterApPips[i].color = i < display.HunterAp ? UiKit.Jade :
                    new Color(.25f, .31f, .34f, .84f);
            wave.text = "ВОЛНА  " + display.Wave + " / " + display.WaveCount +
                        "    •    " + display.DefeatedCount + " / 8";
            forecast.text = string.IsNullOrEmpty(display.Forecast) ? "ОЧЕРЁДНОСТЬ  —" : display.Forecast;
            bool reacting = display.Phase == ReactiveDisplayPhase.Reacting;
            attackWarningArt.gameObject.SetActive(false);
            telegraph.text = reacting ? "ЗАЩИТИСЬ" : display.Phase == ReactiveDisplayPhase.OffenseTiming
                ? "ТЯЖЁЛЫЙ УДАР" : string.Empty;
            detail.text = display.Phase == ReactiveDisplayPhase.Command || reacting ||
                display.Phase == ReactiveDisplayPhase.OffenseTiming
                ? string.Empty : display.Detail ?? string.Empty;
            selectionHint.gameObject.SetActive(false);
            commandPreview.text = display.CommandPreview ?? string.Empty;

            activeIds.Clear();
            IReadOnlyList<ReactiveEnemyDisplay> displays = display.Enemies;
            if (displays != null)
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i] != null && !string.IsNullOrEmpty(displays[i].Id))
                        activeIds.Add(displays[i].Id);
            selectedTargetId = display.SelectedTargetId;
            arena?.SetEnemies(activeIds, display.ActingId, display.WaveEnemyIds);
            bool entranceComplete = arena == null || arena.IntroComplete;
            SetHunterStatusVisible(entranceComplete);

            ReactiveEnemyDisplay selected = null;
            if (displays != null)
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i] != null && displays[i].Id == selectedTargetId)
                    { selected = displays[i]; break; }
            targetName.text = selected == null ? "ЦЕЛЬ НЕ ВЫБРАНА" : selected.Name;
            targetHp.text = selected == null ? "HP —" : "HP " + selected.Hp + " / " + selected.MaxHp;
            targetSeal.text = selected == null ? "SEAL —" : selected.Broken ? "BROKEN" :
                "SEAL " + selected.Seal + " / " + selected.SealMax;
            targetSeal.color = selected != null && selected.Broken ? UiKit.Gold : UiKit.Jade;

            for (int i = 0; i < targetButtons.Length; i++)
                targetIds[i] = null;
            int waveSlotCount = Mathf.Clamp(display.WaveSlotCount, 1, targetButtons.Length);
            if (displays != null)
            {
                for (int i = 0; i < displays.Count; i++)
                {
                    ReactiveEnemyDisplay entry = displays[i];
                    if (entry == null || string.IsNullOrEmpty(entry.Id)) continue;
                    int slot = entry.Slot;
                    if (slot < 0 || slot >= targetButtons.Length || targetIds[slot] != null)
                        continue;
                    targetIds[slot] = entry.Id;
                    targetTexts[slot].text = (entry.Id == display.ActingId ? "▶ " : "") + entry.Name;
                    targetHpTexts[slot].text = "HP " + entry.Hp + " / " + entry.MaxHp;
                    float hpGoal = Mathf.Clamp01((float)entry.Hp / Mathf.Max(1, entry.MaxHp));
                    if (hpOwners[slot] != entry.Id)
                    {
                        hpOwners[slot] = entry.Id;
                        targetHpStart[slot] = targetHpCurrent[slot] = targetHpGoal[slot] = hpGoal;
                        targetHpTween[slot] = HpTweenDuration;
                        targetHpFills[slot].rectTransform.sizeDelta = new Vector2(127f * hpGoal, 7f);
                    }
                    else if (!Mathf.Approximately(targetHpGoal[slot], hpGoal))
                    {
                        targetHpStart[slot] = targetHpCurrent[slot];
                        targetHpGoal[slot] = hpGoal;
                        targetHpTween[slot] = 0f;
                    }
                    targetSlotRects[slot].anchoredPosition = new Vector2(
                        TargetSlotX(waveSlotCount, slot), -320f);
                }
            }
            for (int i = 0; i < targetButtons.Length; i++)
            {
                bool visible = targetIds[i] != null && entranceComplete &&
                    display.Phase == ReactiveDisplayPhase.Command && !presentationLocked && PresentationReady;
                if (targetButtons[i].gameObject.activeSelf != visible)
                    targetButtons[i].gameObject.SetActive(visible);
                if (visible) targetButtons[i].interactable =
                    display.Phase == ReactiveDisplayPhase.Command && !presentationLocked && PresentationReady;
            }
            UpdateTargetHighlights();

            bool preserveSuspendedTiming = (display.Phase == ReactiveDisplayPhase.Suspended ||
                display.Phase == ReactiveDisplayPhase.SaveBlocked) && timingActionId == display.ActionId;
            if (!preserveSuspendedTiming && (timingActionId != display.ActionId || timingHitId != display.HitId))
            {
                timingActionId = display.ActionId;
                timingHitId = display.HitId;
                cursorFreezeTime = 0f;
                defenseAttemptAccepted = false;
            }
            RefreshTiming();
            commandPanel.SetActive(display.Phase == ReactiveDisplayPhase.Command && !presentationLocked && PresentationReady);
            counterPanel.SetActive(display.Phase == ReactiveDisplayPhase.CounterOffer && !presentationLocked && !AnnouncementActive);
            saveBlockedPanel.SetActive(display.Phase == ReactiveDisplayPhase.SaveBlocked);
            waveBanner.gameObject.SetActive(false);
            sealStrikeButton.interactable = display.CanSealStrike;
            sweepButton.interactable = display.CanSweep;
            heavyButton.interactable = display.CanPreviewHeavy || display.CanHeavy;
            throwButton.gameObject.SetActive(display.CanPreviewThrow && ThrowReady);
            throwButton.interactable = display.CanPreviewThrow;
            anchorButton.interactable = display.CanAnchor;

            if (!entranceComplete) HideEntryHud();
        }

        private void RefreshTiming()
        {
            using var researchTiming = ResearchTimingMarker.Auto();
#if UNITY_EDITOR
            RokasView.ResearchRefreshObserved?.Invoke(2);
#endif
            if (EntryPresentationActive)
            {
                HideEntryHud();
                return;
            }

            bool reacting = latestDisplay.Phase == ReactiveDisplayPhase.Reacting && latestDisplay.IncomingHit;
            bool resolved = latestDisplay.ActionId == resolvedActionId && latestDisplay.HitId == resolvedHitId;
            bool visible = reacting && !presentationLocked && !AnnouncementActive && !resolved && resolutionHold <= 0f;
            float progress = cursorFreezeTime > 0f || defenseAttemptAccepted
                ? frozenProgress : Mathf.Clamp01(latestDisplay.ContactProgress);
            contactFill.rectTransform.sizeDelta = new Vector2(TimingWidth * progress, 23f);
            contactFill.color = new Color(.24f, .74f, .80f, .28f);
            timingBeacon.rectTransform.anchoredPosition = new Vector2(
                TimingX + TimingWidth * progress - 3f, -TimingY + 8f);
            timingBeacon.color = cursorFreezeTime > 0f || defenseAttemptAccepted ? feedbackColor : Color.white;
            DefenseWindowProfile profile = latestDisplay.DefenseWindow ?? DefenseWindowProfile.Standard;
            SetTimingWindow(dodgeWindow, profile.DodgeEarlyUs, profile.DodgeLateUs);
            SetTimingWindow(blockWindow, profile.ParryEarlyUs, profile.ParryLateUs);
            SetTimingWindow(perfectWindow, profile.PerfectEarlyUs, profile.PerfectLateUs);
            long duration = latestDisplay.TimingEndUs - latestDisplay.TimingStartUs;
            float impact = duration > 0 ? Mathf.Clamp01((float)(latestDisplay.TimingImpactUs -
                latestDisplay.TimingStartUs) / duration) : 1f;
            timingPerfectArt.rectTransform.anchoredPosition = new Vector2(
                TimingX + TimingWidth * impact - 7.5f, -TimingY + 7f);
            contactTrack.gameObject.SetActive(visible && contactTrack.texture != null);
            contactFill.gameObject.SetActive(visible && contactFill.texture != null);
            timingPromptArt.gameObject.SetActive(false);
            timingPerfectArt.gameObject.SetActive(visible && timingPerfectArt.texture != null);
            // The imported ornaments do not define timing windows; Core's authored intervals do.
            timingNearArt.gameObject.SetActive(false);
            bool allowsDodge = (latestDisplay.AllowedResponses & DefenseResponseMask.Dodge) != 0;
            bool allowsBlock = (latestDisplay.AllowedResponses & DefenseResponseMask.Parry) != 0;
            dodgeWindow.gameObject.SetActive(visible && allowsDodge);
            blockWindow.gameObject.SetActive(visible && allowsBlock);
            perfectWindow.gameObject.SetActive(visible && allowsBlock);
            timingBeacon.gameObject.SetActive(visible);
            defenseHint.gameObject.SetActive(visible);
            defenseHint.text = "ГОЛУБОЕ — УКЛОНЕНИЕ    ·    ЗОЛОТО — ТОЧНЫЙ БЛОК";
            defensePanel.SetActive(visible && !defenseAttemptAccepted);
            attackWarningArt.gameObject.SetActive(false);
            bool offenseVisible = latestDisplay.Phase == ReactiveDisplayPhase.OffenseTiming && !presentationLocked;
            telegraph.gameObject.SetActive(visible || offenseVisible);
            offenseTiming.gameObject.SetActive(offenseVisible);
            offenseCursor.rectTransform.anchoredPosition = new Vector2(440f *
                Mathf.Clamp01(latestDisplay.ContactProgress) - 2f, -26f);
            float offenseOpen = duration > 0 ? Mathf.Clamp01((float)(latestDisplay.TimingImpactUs -
                latestDisplay.OffenseEarlyUs - latestDisplay.TimingStartUs) / duration) : 0f;
            float offenseClose = duration > 0 ? Mathf.Clamp01((float)(latestDisplay.TimingImpactUs +
                latestDisplay.OffenseLateUs - latestDisplay.TimingStartUs) / duration) : 0f;
            offenseSuccessWindow.rectTransform.anchoredPosition = new Vector2(440f * offenseOpen, -29f);
            offenseSuccessWindow.rectTransform.sizeDelta = new Vector2(440f * (offenseClose - offenseOpen), 14f);
            offenseHint.text = latestDisplay.Telegraph == "ТОЧНЫЙ ТАЙМИНГ" ? "ТОЧНЫЙ УДАР" :
                "SPACE / ЛКМ — ТЯЖЁЛЫЙ УДАР";
        }

        private void SetTimingWindow(Image image, long earlyUs, long lateUs)
        {
            long duration = latestDisplay.TimingEndUs - latestDisplay.TimingStartUs;
            float open = duration > 0 ? Mathf.Clamp01((float)(latestDisplay.TimingImpactUs - earlyUs -
                latestDisplay.TimingStartUs) / duration) : 0f;
            float close = duration > 0 ? Mathf.Clamp01((float)(latestDisplay.TimingImpactUs + lateUs -
                latestDisplay.TimingStartUs) / duration) : 0f;
            image.rectTransform.anchoredPosition = new Vector2(TimingX + TimingWidth * open, -TimingY);
            image.rectTransform.sizeDelta = new Vector2(TimingWidth * (close - open), 23f);
        }

        public void Tick(float seconds)
        {
            if (root == null) return;
            arena?.Tick(seconds);
            if (selectedAction != null)
            {
                selectedAction.gameObject.SetActive(arena != null && arena.SelectionVisible);
                bool visible = arena != null && arena.SelectionVisible;
                selectedAction.text = visible && arena.SelectedThrow ? "БРОСОК КИНЖАЛА" :
                    visible && arena.SelectedHeavy ? "ТЯЖЁЛЫЙ УДАР" : "ОБЫЧНЫЙ УДАР";
                normalSelected.gameObject.SetActive(visible && !arena.SelectedHeavy && !arena.SelectedThrow);
                heavySelected.gameObject.SetActive(visible && arena.SelectedHeavy);
                throwSelected.gameObject.SetActive(visible && arena.SelectedThrow);
                commandPreview.gameObject.SetActive(visible);
                commandPreview.text = "ПОВТОРНЫЙ КЛИК — ПОДТВЕРДИТЬ   ·   ESC / ПКМ — ОТМЕНА";
            }
            if (announcementRemaining > 0f)
            {
                announcementRemaining = Mathf.Max(0f, announcementRemaining - seconds);
                float elapsed = AnnouncementDuration - announcementRemaining;
                float fade = Mathf.Min(Mathf.Clamp01(elapsed / .14f), Mathf.Clamp01(announcementRemaining / .22f));
                announcement.color = new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, fade);
                if (announcementRemaining <= 0f) announcement.gameObject.SetActive(false);
            }
            cursorFreezeTime = Mathf.Max(0f, cursorFreezeTime - seconds);
            resolutionHold = Mathf.Max(0f, resolutionHold - seconds);
            RefreshTiming();
            hunterHpTween = Mathf.Min(HpTweenDuration, hunterHpTween + seconds);
            hunterHpCurrent = Mathf.Lerp(hunterHpStart, hunterHpGoal,
                Mathf.Clamp01(hunterHpTween / HpTweenDuration));
            hunterHpFill.rectTransform.sizeDelta = new Vector2(198f * hunterHpCurrent, 8f);
            for (int i = 0; i < targetHpFills.Length; i++)
            {
                targetHpTween[i] = Mathf.Min(HpTweenDuration, targetHpTween[i] + seconds);
                targetHpCurrent[i] = Mathf.Lerp(targetHpStart[i], targetHpGoal[i],
                    Mathf.Clamp01(targetHpTween[i] / HpTweenDuration));
                targetHpFills[i].rectTransform.sizeDelta = new Vector2(127f * targetHpCurrent[i], 7f);
            }
            if (feedbackTime > 0f)
            {
                feedbackTime = arena == null ? Mathf.Max(0f, feedbackTime - seconds) :
                    Mathf.Max(0f, .45f - (float)Math.Max(0d, arena.PresentationClock - feedbackStartedAt));
                float flash = Mathf.Clamp01(feedbackTime / .15f);
                hitFeedback.color = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash);
                if (feedbackTime <= 0f) hitFeedback.gameObject.SetActive(false);
                PositionHitFeedback();
            }
        }

        private void BeginHitFeedback()
        {
            feedbackTime = .45f;
            feedbackStartedAt = arena == null ? 0d : arena.PresentationClock;
            hitFeedback.gameObject.SetActive(!EntryPresentationActive);
            PositionHitFeedback();
        }

        // Skinned bounds are refreshed by the actor-camera render. Read them before
        // compositing UI; this callback never advances combat or presentation time.
        private void RefreshRenderedHitFeedback()
        {
            if (feedbackTime > 0f && !EntryPresentationActive) PositionHitFeedback();
        }

        private void PositionHitFeedback()
        {
            if (hitFeedback == null) return;
            Vector2 anchor = feedbackOrigin;
            bool projected = !string.IsNullOrEmpty(feedbackTargetId) && arena != null &&
                arena.TryActorFeedbackAnchor(feedbackTargetId, out anchor);
            RectTransform rect = hitFeedback.rectTransform;
            rect.pivot = projected ? new Vector2(.5f, .5f) : new Vector2(0f, 1f);
            hitFeedback.alignment = projected ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            Vector2 position = (projected ? anchor + new Vector2(0f, 12f) : feedbackOrigin) +
                new Vector2(0f, (.45f - feedbackTime) * 35f);
            if (projected)
            {
                position.x = Mathf.Clamp(position.x, rect.rect.width * .5f, 1920f - rect.rect.width * .5f);
                position.y = Mathf.Clamp(position.y, -906f + rect.rect.height * .5f, -rect.rect.height * .5f);
            }
            rect.anchoredPosition = position;
        }

        public void ClearReferences()
        {
            arena?.Dispose();
            arena = null;
            root = null;
            hunter = enemy = null;
            hunterHp = ap = targetName = targetHp = targetSeal = wave = forecast = telegraph = detail =
                selectionHint = commandPreview = hitFeedback = waveBanner = null;
            announcement = null;
            selectedAction = null;
            announcementRemaining = 0f;
            hunterHpFill = timingBeacon = null;
            dodgeWindow = blockWindow = perfectWindow = offenseCursor = offenseSuccessWindow = null;
            offenseTiming = null;
            offenseHint = null;
            contactFill = contactTrack = attackWarningArt = timingPromptArt = timingNearArt =
                timingPerfectArt = null;
            defenseHint = null;
            commandPanel = defensePanel = counterPanel = saveBlockedPanel = null;
            sealStrikeButton = sweepButton = heavyButton = anchorButton = null;
            selectedTargetId = null;
            presentationLocked = false;
            latestDisplay = default(ReactiveBattleDisplay);
            timingActionId = timingHitId = resolvedActionId = resolvedHitId = null;
            cursorFreezeTime = resolutionHold = 0f;
            defenseAttemptAccepted = hunterHpInitialized = false;
            activeIds.Clear();
            hunterStatusElements.Clear();
            entryHudElements.Clear();
            for (int i = 0; i < targetIds.Length; i++)
            {
                targetIds[i] = null;
                targetButtons[i] = null;
                targetTexts[i] = null;
                targetHpTexts[i] = null;
                targetHpFills[i] = null;
                targetHighlights[i] = null;
                hpOwners[i] = null;
                targetSlotRects[i] = null;
            }
            for (int i = 0; i < hunterApPips.Length; i++) hunterApPips[i] = null;
        }
    }
}
