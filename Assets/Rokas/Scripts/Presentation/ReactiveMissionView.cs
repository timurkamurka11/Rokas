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
        private RectTransform root;
        private ReactiveCombatArena arena;
        private RawImage hunter;
        private RawImage enemy;
        private Text hunterHp;
        private Text ap;
        private Text targetName;
        private Text targetHp;
        private Text targetSeal;
        private Text wave;
        private Text forecast;
        private Text telegraph;
        private Text detail;
        private Text commandPreview;
        private Text hitFeedback;
        private Text waveBanner;
        private Image hunterFlash;
        private Image enemyFlash;
        private Image timingBeacon;
        private Image contactTrack;
        private Image contactFill;
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

        public string SelectedTargetId { get { return selectedTargetId; } }
        public int AnimatedEnemyCount { get { return arena == null ? 0 : arena.VisibleEnemyCount; } }
        public bool AnimatedActorsReady { get { return arena != null && arena.Ready; } }

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

            // The illustration is the environment. Only translucent HUD layers cover it.
            ui.Box(root, "ReactiveLowerShade", 0, 702, 1920, 204, new Color(.015f, .02f, .04f, .77f));
            ui.Box(root, "ReactiveTopShade", 0, 0, 1920, 127, new Color(.015f, .02f, .045f, .69f));
            ui.Box(root, "ReactiveHunterGround", 250, 674, 365, 4, new Color(.42f, .77f, .93f, .49f));
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

            ui.Box(root, "ReactiveHunterPanel", 30, 739, 344, 143, new Color(.018f, .028f, .049f, .94f));
            ui.Box(root, "ReactiveHunterRule", 30, 739, 344, 3, UiKit.Jade);
            hunter = ui.Art(root, "ReactiveHunterKeiko", assets.vnKeikoCharacterSheet, 40, 748, 103, 124);
            hunter.uvRect = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").BodyUv;
            ui.Label(root, "ReactiveHunterName", "КЕЙКО  /  ОХОТНИК", 155, 746, 211, 33, 18, UiKit.Gold);
            hunterHp = ui.Label(root, "ReactiveHunterHp", "HP —", 155, 785, 211, 43, 24, UiKit.Paper, true);
            ap = ui.Label(root, "ReactiveAp", "AP —", 155, 833, 211, 36, 23, UiKit.Jade, true);

            for (int i = 0; i < targetButtons.Length; i++)
            {
                int slot = i;
                targetButtons[i] = ui.Button(root, "ReactiveTarget" + (i + 1), "ЁКАЙ " + (i + 1),
                    975 + 220 * i, 642, 205, 54, () => SelectTarget(slot));
                targetTexts[i] = targetButtons[i].GetComponentInChildren<Text>();
                targetButtons[i].gameObject.SetActive(false);
            }

            telegraph = ui.Label(root, "ReactiveTelegraph", "", 644, 377, 634, 91, 39,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            detail = ui.Label(root, "ReactiveDetail", "", 620, 473, 681, 64, 21,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            hitFeedback = ui.Label(root, "ReactiveHitFeedback", "", 645, 546, 631, 80, 35,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            contactTrack = ui.Box(root, "ReactiveContactTrack", 630, 561, 430, 8,
                new Color(.34f, .43f, .5f, .68f));
            contactFill = ui.Box(root, "ReactiveContactFill", 630, 561, 0, 8, UiKit.Jade);
            timingBeacon = ui.Box(root, "ReactiveTimingBeacon", 843, 543, 11, 43, UiKit.Jade);
            defenseHint = ui.Label(root, "ReactiveDefenseHint",
                "D — УКЛОНЕНИЕ     F — ПАРИРОВАНИЕ",
                606, 583, 478, 34, 17, UiKit.Paper, false, TextAnchor.MiddleCenter);
            waveBanner = ui.Label(root, "ReactiveWaveBanner", "", 534, 324, 852, 135, 51,
                UiKit.Paper, true, TextAnchor.MiddleCenter);

            commandPanel = ui.Rect(root, "ReactiveCommands", 390, 738, 1485, 144).gameObject;
            ui.Box(commandPanel.transform, "CommandShade", 0, 0, 1485, 144, new Color(.018f, .028f, .049f, .95f));
            ui.Label(commandPanel.transform, "CommandTitle", "ВАШ ХОД  /  ВЫБЕРИТЕ ДЕЙСТВИЕ", 18, 4, 1444, 31, 17,
                UiKit.Gold, false, TextAnchor.MiddleCenter);
            ui.Button(commandPanel.transform, "ReactiveBasic", "Обычный удар", 15, 37, 230, 72, basic, true);
            sealStrikeButton = ui.Button(commandPanel.transform, "ReactiveSealStrike",
                "Удар печати · 3 AP", 260, 37, 230, 72, sealStrike);
            ui.Button(commandPanel.transform, "ReactiveDefend", "Защита", 505, 37, 230, 72, defend);
            sweepButton = ui.Button(commandPanel.transform, "ReactiveSweep", "Размах · 3 AP",
                750, 37, 230, 72, sweep);
            anchorButton = ui.Button(commandPanel.transform, "ReactiveAnchor", "Якорь · 2 AP",
                995, 37, 230, 72, anchor);
            heavyButton = ui.Button(commandPanel.transform, "ReactiveHeavy", "Тяжёлый · 5 AP",
                1240, 37, 230, 72, heavy);
            commandPreview = ui.Label(commandPanel.transform, "ReactiveCommandPreview", "",
                18, 111, 1450, 29, 16, UiKit.Muted, false, TextAnchor.MiddleCenter);

            defensePanel = ui.Rect(root, "ReactiveDefense", 879, 738, 996, 144).gameObject;
            ui.Box(defensePanel.transform, "DefenseShade", 0, 0, 996, 144,
                new Color(.014f, .026f, .051f, .92f));
            ui.Box(defensePanel.transform, "DefenseRule", 0, 0, 996, 2, UiKit.Jade);
            ui.Label(defensePanel.transform, "DefenseTitle", "РЕАКТИВНАЯ ЗАЩИТА", 14, 5, 968, 35, 21,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Button(defensePanel.transform, "ReactiveDodge", "Уклонение", 24, 47, 457, 75, dodge, true);
            ui.Button(defensePanel.transform, "ReactiveParry", "Парирование", 515, 47, 457, 75, parry, true);

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
            hitFeedback.text = string.Empty;
            waveBanner.gameObject.SetActive(false);
            feedbackTime = 0f;
        }

        private void SelectTarget(int slot)
        {
            if (slot < 0 || slot >= targetIds.Length || string.IsNullOrEmpty(targetIds[slot])) return;
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
            if (combatEvent.Kind != CombatEventKind.HitResolved) return;
            feedbackOnHunter = combatEvent.TargetId == ReactiveDuelDefinitions.HunterId;
            feedbackColor = feedbackOnHunter ? UiKit.Red : UiKit.Gold;
            if (feedbackOnHunter)
            {
                switch (combatEvent.Detail)
                {
                    case "Dodge": hitFeedback.text = "УКЛОНЕНИЕ"; feedbackColor = UiKit.Jade; break;
                    case "Parry": hitFeedback.text = "ПАРИРОВАНИЕ"; feedbackColor = UiKit.Jade; break;
                    case "Perfect": hitFeedback.text = "ИДЕАЛЬНОЕ ПАРИРОВАНИЕ"; feedbackColor = UiKit.Gold; break;
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
            wave.text = "ВОЛНА  " + display.Wave + " / " + display.WaveCount +
                        "    •    " + display.DefeatedCount + " / 8";
            forecast.text = string.IsNullOrEmpty(display.Forecast) ? "ОЧЕРЁДНОСТЬ  —" : display.Forecast;
            telegraph.text = display.Telegraph ?? string.Empty;
            detail.text = display.Detail ?? string.Empty;
            commandPreview.text = display.CommandPreview ?? string.Empty;

            activeIds.Clear();
            IReadOnlyList<ReactiveEnemyDisplay> displays = display.Enemies;
            if (displays != null)
                for (int i = 0; i < displays.Count; i++)
                    if (displays[i] != null && !string.IsNullOrEmpty(displays[i].Id))
                        activeIds.Add(displays[i].Id);
            selectedTargetId = display.SelectedTargetId;
            arena?.SetEnemies(activeIds, display.ActingId);

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
            {
                bool visible = displays != null && i < displays.Count && displays[i] != null;
                targetButtons[i].gameObject.SetActive(visible);
                targetIds[i] = visible ? displays[i].Id : null;
                if (!visible) continue;
                targetTexts[i].text = (displays[i].Id == display.ActingId ? "▶ " : "") +
                    displays[i].Id + "   " + displays[i].Hp + " HP";
                targetTexts[i].fontSize = 20;
                float x = displays.Count == 1 ? 1320f : displays.Count == 2 ? 1120f + 285f * i :
                    displays.Count == 3 ? 990f + 260f * i : 920f + 235f * i;
                targetButtons[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(x, -642f);
            }
            UpdateTargetHighlights();

            float progress = Mathf.Clamp01(display.ContactProgress);
            contactFill.rectTransform.sizeDelta = new Vector2(430 * progress, 8);
            contactFill.color = progress > .72f ? UiKit.Gold : UiKit.Jade;
            bool offenseTiming = display.Phase == ReactiveDisplayPhase.OffenseTiming;
            timingBeacon.color = offenseTiming ? UiKit.Gold :
                display.Phase == ReactiveDisplayPhase.Reacting
                    ? new Color(1f, .78f, .35f, .38f + .6f * progress) : Color.clear;
            timingBeacon.rectTransform.anchoredPosition = new Vector2(
                630 + 430 * (offenseTiming ? .8f : progress) - 6, -543);
            bool timingVisible = display.Phase == ReactiveDisplayPhase.Reacting || offenseTiming;
            contactTrack.gameObject.SetActive(timingVisible);
            contactFill.gameObject.SetActive(timingVisible);
            timingBeacon.gameObject.SetActive(timingVisible);
            defenseHint.gameObject.SetActive(timingVisible);
            defenseHint.text = offenseTiming ? "SPACE / ЛКМ — ТЯЖЁЛЫЙ УДАР" :
                "D — УКЛОНЕНИЕ     F — ПАРИРОВАНИЕ";
            commandPanel.SetActive(display.Phase == ReactiveDisplayPhase.Command);
            defensePanel.SetActive(display.Phase == ReactiveDisplayPhase.Reacting);
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
                commandPreview = hitFeedback = waveBanner = null;
            contactFill = contactTrack = timingBeacon = hunterFlash = enemyFlash = null;
            defenseHint = null;
            commandPanel = defensePanel = counterPanel = saveBlockedPanel = null;
            sealStrikeButton = sweepButton = heavyButton = anchorButton = null;
            selectedTargetId = null;
            activeIds.Clear();
            for (int i = 0; i < targetIds.Length; i++)
            {
                targetIds[i] = null;
                targetButtons[i] = null;
                targetTexts[i] = null;
            }
        }
    }
}
