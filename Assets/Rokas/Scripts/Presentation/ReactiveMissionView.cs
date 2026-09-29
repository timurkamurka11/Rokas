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
        public bool CanAnchor;
        public string Forecast;
        public string Telegraph;
        public string Detail;
        public string CommandPreview;
        public float ContactProgress;
        public ReactiveDisplayPhase Phase;
    }

    // The view publishes command and selection intent only. Core remains authoritative.
    public sealed class ReactiveMissionView
    {
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
        private readonly string[] targetIds = new string[4];
        private readonly Button[] targetButtons = new Button[4];
        private readonly Text[] targetTexts = new Text[4];
        private readonly Text[] targetHpTexts = new Text[4];
        private readonly Image[] targetHpFills = new Image[4];
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
        private Text waveBanner;
        private Image hunterFlash;
        private Image enemyFlash;
        private Image timingBeacon;
        private RawImage contactTrack;
        private RawImage contactFill;
        private RawImage attackWarningArt;
        private RawImage timingPromptArt;
        private RawImage timingNearArt;
        private RawImage timingPerfectArt;
        private Text defenseHint;
        private GameObject commandPanel;
        private GameObject defensePanel;
        private GameObject counterPanel;
        private GameObject saveBlockedPanel;
        private Button sealStrikeButton;
        private Button sweepButton;
        private Button heavyButton;
        private Button anchorButton;
        private float feedbackTime;
        private bool feedbackOnHunter;
        private Color feedbackColor;
        private string selectedTargetId;
        private bool presentationLocked;

        public string SelectedTargetId { get { return selectedTargetId; } }
        public int AnimatedEnemyCount { get { return arena == null ? 0 : arena.VisibleEnemyCount; } }
        public bool AnimatedActorsReady { get { return arena != null && arena.Ready; } }
        public bool HunterApproachComplete { get { return arena == null || arena.HunterApproachComplete; } }
        public bool HunterAtHome { get { return arena == null || arena.HunterAtHome; } }

        public bool StartHunterApproach(string enemyId)
        {
            if (arena == null || !arena.StartHunterApproach(enemyId)) return false;
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
            if (!value) return;
            for (int i = 0; i < targetButtons.Length; i++)
                if (targetButtons[i] != null) targetButtons[i].interactable = false;
        }

        public ReactiveMissionView(UiKit ui, RokasAssets assets, Action basic, Action sealStrike,
            Action defend, Action sweep, Action heavy, Action anchor, Action dodge, Action parry,
            Action counter, Action retrySave, Action<string> chooseTarget)
        {
            this.ui = ui;
            this.assets = assets;
            this.basic = basic;
            this.sealStrike = sealStrike;
            this.defend = defend;
            this.sweep = sweep;
            this.heavy = heavy;
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
            Sprite enemyCardSprite = Resources.Load<Sprite>(
                "Combat/ReactiveTurns/UI/HUD/Hp bar button ui of monscter");
            Sprite keikoProfileSprite = Resources.Load<Sprite>(
                "Combat/ReactiveTurns/UI/HUD/Keiko ui profile during battle");

            // The illustration is the environment. Only translucent HUD layers cover it.
            ui.Box(root, "ReactiveLowerShade", 0, 702, 1920, 204, new Color(.015f, .02f, .04f, .77f));
            ui.Box(root, "ReactiveTopShade", 0, 0, 1920, 127, new Color(.015f, .02f, .045f, .69f));
            ui.Box(root, "ReactiveEnemyGround", 1110, 674, 616, 4, new Color(.94f, .19f, .36f, .5f));
            hunterFlash = ui.Box(root, "ReactiveHunterImpact", 135, 295, 480, 365, Color.clear);
            enemyFlash = ui.Box(root, "ReactiveEnemyImpact", 1100, 273, 652, 395, Color.clear);

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
            ui.Label(root, "ReactiveHunterName", "КЕЙКО  /  ОХОТНИК", 155, 746, 211, 29, 18, UiKit.Gold);
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
                int slot = i;
                targetSlotRects[i] = ui.Rect(root, "ReactiveEnemySlot" + (i + 1),
                    TargetSlotX(3, i), 605, 205, 91);
                targetButtons[i] = ui.Button(targetSlotRects[i], "ReactiveTarget" + (i + 1), "",
                    0, 0, 205, 91, () => SelectTarget(slot));
                targetTexts[i] = targetButtons[i].GetComponentInChildren<Text>();
                targetTexts[i].rectTransform.anchoredPosition = new Vector2(68, -11);
                targetTexts[i].rectTransform.sizeDelta = new Vector2(128, 26);
                targetTexts[i].fontSize = 17;
                targetTexts[i].color = UiKit.Gold;
                RawImage cardTop = ui.Art(targetButtons[i].transform, "EnemyCardOrnament",
                    null, 1, 0, 203, 11);
                ApplyArtwork(cardTop, enemyCardSprite, new Rect(270, 295, 1240, 44), 1774, 887);
                RawImage portrait = ui.Art(targetButtons[i].transform, "EnemyPortrait",
                    assets.enemy, 7, 10, 53, 57);
                ApplyArtwork(portrait, enemyCardSprite, new Rect(318, 337, 284, 260), 1774, 887);
                targetHpTexts[i] = ui.Label(targetButtons[i].transform, "EnemyHp", "HP —",
                    68, 34, 130, 25, 17, UiKit.Paper, true);
                ui.Box(targetButtons[i].transform, "EnemyHpTrack", 68, 65, 127, 7,
                    new Color(.22f, .10f, .15f));
                targetHpFills[i] = ui.Box(targetButtons[i].transform, "EnemyHpFill", 68, 65,
                    127, 7, new Color(.89f, .13f, .27f));
                ui.Box(targetButtons[i].transform, "EnemyCardBottomRule", 7, 83,
                    191, 2, UiKit.Gold);
                targetButtons[i].gameObject.SetActive(false);
            }

            selectionHint = ui.Label(root, "ReactiveSelectionHint", "ВЫБЕРИ ЦЕЛЬ И ДЕЙСТВИЕ",
                390, 703, 1485, 32, 19, UiKit.Gold, false, TextAnchor.MiddleCenter);
            selectionHint.gameObject.SetActive(false);

            attackWarningArt = AddArtwork(root, "ReactiveAttackWarningArt",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI ATTACK SHOWS WHEN ENEMY ATTACKS",
                640, 300, 640, 206, new Rect(17, 4, 2138, 690), 2172, 724);
            telegraph = ui.Label(root, "ReactiveTelegraph", "", 644, 438, 634, 62, 31,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            detail = ui.Label(root, "ReactiveDetail", "", 620, 473, 681, 64, 21,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            timingPromptArt = AddArtwork(root, "ReactiveTimingPromptArt",
                "Combat/ReactiveTurns/UI/DefenceReaction/TEXT UI SHOWS CLICK IN TIME",
                690, 503, 540, 66, new Rect(170, 207, 1830, 307), 2172, 724);
            contactTrack = AddArtwork(root, "ReactiveContactTrack",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI TIMING BAR",
                630, 566, 650, 48, new Rect(72, 288, 2027, 143), 2172, 724);
            contactFill = AddArtwork(root, "ReactiveContactFill",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI MOVING BAR",
                644, 578, 0, 23, new Rect(183, 268, 1408, 335), 1774, 887);
            timingPerfectArt = AddArtwork(root, "ReactivePerfectWindow",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI BAR CLICK ON RIGHT TIME",
                1190, 558, 39, 61, new Rect(445, 352, 363, 592), 1254, 1254);
            timingNearArt = AddArtwork(root, "ReactiveNearBars",
                "Combat/ReactiveTurns/UI/DefenceReaction/UI NEAR BARS",
                1134, 568, 150, 42, new Rect(429, 259, 1314, 201), 2172, 724);
            timingBeacon = ui.Box(root, "ReactiveTimingBeacon", 630, 572, 6, 35,
                new Color(1f, .83f, .32f, .94f));
            defenseHint = ui.Label(root, "ReactiveDefenseHint",
                "Q — УКЛОНЕНИЕ     E — БЛОК",
                390, 703, 1485, 32, 18, UiKit.Paper, false, TextAnchor.MiddleCenter);
            hitFeedback = ui.Label(root, "ReactiveHitFeedback", "", 645, 448, 631, 74, 35,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            waveBanner = ui.Label(root, "ReactiveWaveBanner", "", 534, 324, 852, 135, 51,
                UiKit.Paper, true, TextAnchor.MiddleCenter);

            commandPanel = ui.Rect(root, "ReactiveCommands", 390, 738, 1485, 144).gameObject;
            ui.Box(commandPanel.transform, "CommandShade", 0, 0, 1485, 144, new Color(.018f, .028f, .049f, .95f));
            ui.Label(commandPanel.transform, "CommandTitle", "ВАШ ХОД  /  ВЫБЕРИТЕ ДЕЙСТВИЕ", 18, 4, 1444, 31, 17,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            Button basicButton = ui.Button(commandPanel.transform, "ReactiveBasic", "Обычный удар",
                15, 37, 230, 72, basic, true);
            AttachButtonArtwork(basicButton, "Combat/ReactiveTurns/UI/Actions/Normal hit button",
                new Rect(426, 144, 1320, 436), 2172, 724);
            sealStrikeButton = ui.Button(commandPanel.transform, "ReactiveSealStrike",
                "Удар печати · 3 AP", 260, 37, 230, 72, sealStrike);
            AttachButtonArtwork(sealStrikeButton, "Combat/ReactiveTurns/UI/Actions/Pring impact",
                new Rect(283, 142, 1607, 429), 2172, 724);
            AddCostLabel(sealStrikeButton, "3 AP");
            Button defendButton = ui.Button(commandPanel.transform, "ReactiveDefend", "Защита",
                505, 37, 230, 72, defend);
            AttachButtonArtwork(defendButton, "Combat/ReactiveTurns/UI/Actions/Defence button",
                new Rect(398, 145, 1377, 418), 2172, 724);
            sweepButton = ui.Button(commandPanel.transform, "ReactiveSweep", "Размах · 3 AP",
                750, 37, 230, 72, sweep);
            AttachButtonArtwork(sweepButton, "Combat/ReactiveTurns/UI/Actions/Whifh button",
                new Rect(345, 163, 1482, 391), 2172, 724);
            AddCostLabel(sweepButton, "3 AP");
            anchorButton = ui.Button(commandPanel.transform, "ReactiveAnchor", "Якорь · 2 AP",
                995, 37, 230, 72, anchor);
            AttachButtonArtwork(anchorButton, "Combat/ReactiveTurns/UI/Actions/Ancot button",
                new Rect(432, 170, 1308, 379), 2172, 724);
            AddCostLabel(anchorButton, "2 AP");
            heavyButton = ui.Button(commandPanel.transform, "ReactiveHeavy", "Тяжёлый · 5 AP",
                1240, 37, 230, 72, heavy);
            AttachButtonArtwork(heavyButton, "Combat/ReactiveTurns/UI/Actions/Heavy hit",
                new Rect(247, 142, 1678, 462), 2172, 724);
            AddCostLabel(heavyButton, "5 AP");
            commandPreview = ui.Label(commandPanel.transform, "ReactiveCommandPreview", "",
                18, 111, 1450, 29, 16, UiKit.Muted, false, TextAnchor.MiddleCenter);

            defensePanel = ui.Rect(root, "ReactiveDefense", 879, 738, 996, 144).gameObject;
            ui.Box(defensePanel.transform, "DefenseShade", 0, 0, 996, 144,
                new Color(.014f, .026f, .051f, .92f));
            ui.Box(defensePanel.transform, "DefenseRule", 0, 0, 996, 2, UiKit.Jade);
            ui.Label(defensePanel.transform, "DefenseTitle", "РЕАКТИВНАЯ ЗАЩИТА", 14, 5, 968, 35, 21,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            Button dodgeButton = ui.Button(defensePanel.transform, "ReactiveDodge", "Q — Уклонение",
                78, 42, 395, 99, dodge, true);
            AttachButtonArtwork(dodgeButton,
                "Combat/ReactiveTurns/UI/DefenceReaction/UI DODGE BUTTON",
                new Rect(65, 343, 1325, 443), 1448, 1086);
            Button blockButton = ui.Button(defensePanel.transform, "ReactiveParry", "E — Блок",
                523, 42, 395, 99, parry, true);
            AttachButtonArtwork(blockButton,
                "Combat/ReactiveTurns/UI/DefenceReaction/UI BLOCK BUTTON",
                new Rect(56, 321, 1340, 446), 1448, 1086);

            counterPanel = ui.Rect(root, "ReactiveCounter", 622, 712, 680, 170).gameObject;
            ui.Box(counterPanel.transform, "CounterShade", 0, 0, 680, 170,
                new Color(.055f, .07f, .074f, .97f));
            ui.Label(counterPanel.transform, "CounterTitle", "КОНТРАТАКА  /  ШАНС ОТКРЫТ",
                20, 8, 640, 49, 24, UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Button(counterPanel.transform, "ReactiveCounterConfirm", "Контратаковать",
                145, 68, 390, 75, counter, true);

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
            hitFeedback.text = string.Empty;
            waveBanner.gameObject.SetActive(false);
            feedbackTime = 0f;
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
            if (presentationLocked || slot < 0 || slot >= targetIds.Length ||
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
            }
        }

        public void Present(CombatEvent combatEvent)
        {
            if (root == null || combatEvent == null) return;
            arena?.Present(combatEvent);
            if (combatEvent.Kind == CombatEventKind.AttackStarted)
            {
                feedbackTime = 0f;
                hitFeedback.text = string.Empty;
                return;
            }
            if (combatEvent.Kind != CombatEventKind.HitResolved) return;
            feedbackOnHunter = combatEvent.TargetId == ReactiveDuelDefinitions.HunterId;
            feedbackColor = feedbackOnHunter ? UiKit.Red : UiKit.Gold;
            if (feedbackOnHunter)
            {
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
                hitFeedback.text = combatEvent.Detail == "Counter"
                    ? "КОНТРАТАКА  −" + combatEvent.Amount
                    : combatEvent.Detail == "TimedHeavy"
                    ? "ТОЧНЫЙ УДАР  −" + combatEvent.Amount
                    : "УДАР  −" + combatEvent.Amount;
            hitFeedback.color = feedbackColor;
            feedbackTime = .8f;
        }

        public void Refresh(ReactiveBattleDisplay display)
        {
            if (root == null) return;
            hunterHp.text = "HP " + display.HunterHp + " / 100";
            ap.text = "AP " + display.HunterAp + " / 6";
            hunterHpFill.rectTransform.sizeDelta = new Vector2(
                198f * Mathf.Clamp01(display.HunterHp / 100f), 8f);
            for (int i = 0; i < hunterApPips.Length; i++)
                hunterApPips[i].color = i < display.HunterAp ? UiKit.Jade :
                    new Color(.25f, .31f, .34f, .84f);
            wave.text = "ВОЛНА  " + display.Wave + " / " + display.WaveCount +
                        "    •    " + display.DefeatedCount + " / 8";
            forecast.text = string.IsNullOrEmpty(display.Forecast) ? "ОЧЕРЁДНОСТЬ  —" : display.Forecast;
            bool reacting = display.Phase == ReactiveDisplayPhase.Reacting;
            bool warningAvailable = reacting && attackWarningArt.texture != null;
            attackWarningArt.gameObject.SetActive(warningAvailable);
            telegraph.text = warningAvailable && display.Telegraph == "ВРАГ АТАКУЕТ"
                ? string.Empty : display.Telegraph ?? string.Empty;
            detail.text = display.Phase == ReactiveDisplayPhase.Command || reacting ||
                display.Phase == ReactiveDisplayPhase.OffenseTiming
                ? string.Empty : display.Detail ?? string.Empty;
            selectionHint.gameObject.SetActive(display.Phase == ReactiveDisplayPhase.Command &&
                !presentationLocked);
            commandPreview.text = display.CommandPreview ?? string.Empty;

            activeIds.Clear();
            IReadOnlyList<ReactiveEnemyDisplay> displays = display.Enemies;
            if (displays != null)
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i] != null && !string.IsNullOrEmpty(displays[i].Id))
                        activeIds.Add(displays[i].Id);
            selectedTargetId = display.SelectedTargetId;
            arena?.SetEnemies(activeIds, display.ActingId, display.WaveEnemyIds);

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
                    targetHpFills[slot].rectTransform.sizeDelta = new Vector2(
                        127f * Mathf.Clamp01((float)entry.Hp / Mathf.Max(1, entry.MaxHp)), 7f);
                    targetSlotRects[slot].anchoredPosition = new Vector2(
                        TargetSlotX(waveSlotCount, slot), -605f);
                }
            }
            for (int i = 0; i < targetButtons.Length; i++)
            {
                bool visible = targetIds[i] != null;
                if (targetButtons[i].gameObject.activeSelf != visible)
                    targetButtons[i].gameObject.SetActive(visible);
                if (visible) targetButtons[i].interactable =
                    display.Phase == ReactiveDisplayPhase.Command && !presentationLocked;
            }
            UpdateTargetHighlights();

            float progress = Mathf.Clamp01(display.ContactProgress);
            contactFill.rectTransform.sizeDelta = new Vector2(622f * progress, 23f);
            contactFill.color = progress > .72f ? UiKit.Gold : UiKit.Jade;
            bool offenseTiming = display.Phase == ReactiveDisplayPhase.OffenseTiming && !presentationLocked;
            timingBeacon.color = offenseTiming ? UiKit.Gold :
                reacting
                    ? new Color(1f, .78f, .35f, .38f + .6f * progress) : Color.clear;
            timingBeacon.rectTransform.anchoredPosition = new Vector2(
                644f + 622f * progress - 3f, -572f);
            float successX = 644f + 622f * (offenseTiming ? .8f : .93f);
            timingPerfectArt.rectTransform.anchoredPosition = new Vector2(successX - 19f, -558f);
            timingNearArt.rectTransform.anchoredPosition = new Vector2(successX - 75f, -568f);
            bool timingVisible = reacting || offenseTiming;
            contactTrack.gameObject.SetActive(timingVisible && contactTrack.texture != null);
            contactFill.gameObject.SetActive(timingVisible && contactFill.texture != null);
            timingPromptArt.gameObject.SetActive(timingVisible && timingPromptArt.texture != null);
            timingPerfectArt.gameObject.SetActive(timingVisible && timingPerfectArt.texture != null);
            timingNearArt.gameObject.SetActive(timingVisible && timingNearArt.texture != null);
            timingBeacon.gameObject.SetActive(timingVisible);
            defenseHint.gameObject.SetActive(timingVisible);
            defenseHint.text = offenseTiming ? "SPACE / ЛКМ — ТЯЖЁЛЫЙ УДАР" :
                "Q — УКЛОНЕНИЕ     E — БЛОК";
            commandPanel.SetActive(display.Phase == ReactiveDisplayPhase.Command && !presentationLocked);
            defensePanel.SetActive(reacting);
            counterPanel.SetActive(display.Phase == ReactiveDisplayPhase.CounterOffer);
            saveBlockedPanel.SetActive(display.Phase == ReactiveDisplayPhase.SaveBlocked);
            waveBanner.gameObject.SetActive(display.Phase == ReactiveDisplayPhase.WaveTransition);
            waveBanner.text = display.Phase == ReactiveDisplayPhase.WaveTransition
                ? "ВОЛНА " + display.Wave + " ЗАВЕРШЕНА" : string.Empty;
            sealStrikeButton.interactable = display.CanSealStrike;
            sweepButton.interactable = display.CanSweep;
            heavyButton.interactable = display.CanHeavy;
            anchorButton.interactable = display.CanAnchor;
        }

        public void Tick(float seconds)
        {
            if (root == null) return;
            arena?.Tick(seconds);
            feedbackTime = Mathf.Max(0, feedbackTime - seconds);
            float flash = Mathf.Clamp01(feedbackTime * 2.5f);
            hitFeedback.color = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash);
            hunterFlash.color = feedbackOnHunter
                ? new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash * .18f) : Color.clear;
            enemyFlash.color = feedbackOnHunter ? Color.clear
                : new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash * .16f);
        }

        public void ClearReferences()
        {
            arena?.Dispose();
            arena = null;
            root = null;
            hunter = enemy = null;
            hunterHp = ap = targetName = targetHp = targetSeal = wave = forecast = telegraph = detail =
                selectionHint = commandPreview = hitFeedback = waveBanner = null;
            hunterHpFill = timingBeacon = hunterFlash = enemyFlash = null;
            contactFill = contactTrack = attackWarningArt = timingPromptArt = timingNearArt =
                timingPerfectArt = null;
            defenseHint = null;
            commandPanel = defensePanel = counterPanel = saveBlockedPanel = null;
            sealStrikeButton = sweepButton = heavyButton = anchorButton = null;
            selectedTargetId = null;
            presentationLocked = false;
            activeIds.Clear();
            for (int i = 0; i < targetIds.Length; i++)
            {
                targetIds[i] = null;
                targetButtons[i] = null;
                targetTexts[i] = null;
                targetHpTexts[i] = null;
                targetHpFills[i] = null;
                targetSlotRects[i] = null;
            }
            for (int i = 0; i < hunterApPips.Length; i++) hunterApPips[i] = null;
        }
    }
}
