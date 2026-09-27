using System;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public enum ReactiveDisplayPhase
    {
        Command,
        Reacting,
        CounterOffer,
        Result,
        Suspended,
        SaveBlocked
    }

    public struct ReactiveBattleDisplay
    {
        public int HunterHp;
        public int HunterAp;
        public int EnemyHp;
        public int EnemySeal;
        public int EnemySealMax;
        public bool Broken;
        public bool CanSealStrike;
        public string Forecast;
        public string Telegraph;
        public string Detail;
        public string CommandPreview;
        public float ContactProgress;
        public ReactiveDisplayPhase Phase;
    }

    // The view receives a Core snapshot and publishes only button intents. It never changes combat state.
    public sealed class ReactiveMissionView
    {
        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly Action basic;
        private readonly Action sealStrike;
        private readonly Action defend;
        private readonly Action dodge;
        private readonly Action parry;
        private readonly Action counter;
        private readonly Action retrySave;
        private RectTransform root;
        private RawImage hunter;
        private RawImage enemy;
        private Text hunterHp;
        private Text ap;
        private Text targetHp;
        private Text targetSeal;
        private Text forecast;
        private Text telegraph;
        private Text detail;
        private Text commandPreview;
        private Text hitFeedback;
        private Image hunterFlash;
        private Image enemyFlash;
        private Image timingBeacon;
        private Image contactFill;
        private GameObject commandPanel;
        private GameObject defensePanel;
        private GameObject counterPanel;
        private GameObject saveBlockedPanel;
        private Button sealStrikeButton;
        private float age;
        private float feedbackTime;
        private bool feedbackOnHunter;
        private Color feedbackColor;

        public ReactiveMissionView(UiKit ui, RokasAssets assets, Action basic, Action sealStrike,
            Action defend, Action dodge, Action parry, Action counter, Action retrySave)
        {
            this.ui = ui;
            this.assets = assets;
            this.basic = basic;
            this.sealStrike = sealStrike;
            this.defend = defend;
            this.dodge = dodge;
            this.parry = parry;
            this.counter = counter;
            this.retrySave = retrySave;
        }

        public void Build(RectTransform parent)
        {
            root = ui.Rect(parent, "ReactiveArena", 0, 100, 1920, 906);
            age = 0f;

            // Keep the existing Kisaragi location in view. Framing remains fixed during defense.
            ui.Box(root, "ReactiveFloorShade", 0, 465, 1920, 441, new Color(.015f, .025f, .03f, .32f));
            ui.Box(root, "ReactiveCenterShade", 578, 89, 700, 666, new Color(.025f, .045f, .05f, .24f));
            ui.Box(root, "ReactiveHunterGround", 108, 779, 520, 3, new Color(.45f, .8f, .72f, .48f));
            ui.Box(root, "ReactiveEnemyGround", 1238, 779, 510, 3, new Color(.82f, .37f, .29f, .5f));

            hunter = ui.Art(root, "ReactiveHunterKeiko", assets.vnKeikoCharacterSheet, 132, 167, 350, 612);
            hunter.uvRect = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko").BodyUv;
            enemy = ui.Art(root, "ReactiveEnemyFacelessCommuter", assets.enemy, 1240, 142, 464, 637);
            hunterFlash = ui.Box(root, "ReactiveHunterImpact", 135, 175, 344, 595, Color.clear);
            enemyFlash = ui.Box(root, "ReactiveEnemyImpact", 1248, 150, 449, 615, Color.clear);

            ui.Box(root, "ReactiveHunterPanel", 60, 32, 425, 103, new Color(.025f, .055f, .06f, .91f));
            ui.Box(root, "ReactiveHunterRule", 60, 32, 425, 3, UiKit.Jade);
            ui.Label(root, "ReactiveHunterName", "КЕЙКО  /  ОХОТНИК", 78, 43, 392, 29, 17, UiKit.Gold);
            hunterHp = ui.Label(root, "ReactiveHunterHp", "HP —", 78, 75, 215, 50, 30, UiKit.Paper, true);
            ap = ui.Label(root, "ReactiveAp", "AP —", 303, 75, 167, 50, 26, UiKit.Jade, true, TextAnchor.MiddleRight);

            ui.Box(root, "ReactiveEnemyPanel", 1435, 32, 425, 103, new Color(.025f, .055f, .06f, .91f));
            ui.Box(root, "ReactiveEnemyRule", 1435, 32, 425, 3, UiKit.Red);
            ui.Label(root, "ReactiveEnemyName", "БЕЗЛИКИЙ ПАССАЖИР", 1452, 43, 390, 29, 17, UiKit.Gold);
            targetHp = ui.Label(root, "ReactiveTargetHp", "HP —", 1452, 75, 189, 50, 30, UiKit.Paper, true);
            targetSeal = ui.Label(root, "ReactiveTargetSeal", "SEAL —", 1628, 75, 215, 50, 25, UiKit.Jade, true, TextAnchor.MiddleRight);

            ui.Box(root, "ReactiveForecastPanel", 568, 32, 782, 76, new Color(.025f, .055f, .06f, .9f));
            ui.Box(root, "ReactiveForecastRule", 568, 32, 782, 2, UiKit.Gold);
            forecast = ui.Label(root, "ReactiveForecast", "ОЧЕРЁДНОСТЬ  —", 592, 43, 730, 52, 20, UiKit.Paper, false, TextAnchor.MiddleCenter);

            telegraph = ui.Label(root, "ReactiveTelegraph", "", 634, 232, 652, 112, 43, UiKit.Paper, true, TextAnchor.MiddleCenter);
            detail = ui.Label(root, "ReactiveDetail", "", 600, 350, 720, 96, 24, UiKit.Gold, false, TextAnchor.MiddleCenter);
            hitFeedback = ui.Label(root, "ReactiveHitFeedback", "", 650, 445, 620, 88, 38,
                UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Box(root, "ReactiveContactTrack", 647, 550, 626, 9, new Color(.34f, .43f, .44f, .65f));
            contactFill = ui.Box(root, "ReactiveContactFill", 647, 550, 0, 9, UiKit.Jade);
            timingBeacon = ui.Box(root, "ReactiveTimingBeacon", 954, 527, 12, 45, UiKit.Jade);
            ui.Label(root, "ReactiveDefenseHint", "ПКМ / D — УКЛОНЕНИЕ     SPACE / F — ПАРИРОВАНИЕ",
                570, 573, 780, 39, 20, UiKit.Paper, false, TextAnchor.MiddleCenter);

            commandPanel = ui.Rect(root, "ReactiveCommands", 594, 636, 766, 180).gameObject;
            ui.Box(commandPanel.transform, "CommandShade", 0, 0, 766, 180, new Color(.02f, .045f, .05f, .94f));
            ui.Label(commandPanel.transform, "CommandTitle", "ВАШ ХОД  /  ВЫБЕРИТЕ ДЕЙСТВИЕ", 22, 6, 722, 38, 18, UiKit.Gold);
            ui.Button(commandPanel.transform, "ReactiveBasic", "Обычный удар", 23, 53, 226, 75, basic, true);
            sealStrikeButton = ui.Button(commandPanel.transform, "ReactiveSealStrike", "Удар печати · 3 AP", 270, 53, 226, 75, sealStrike);
            ui.Button(commandPanel.transform, "ReactiveDefend", "Защита", 517, 53, 226, 75, defend);
            commandPreview = ui.Label(commandPanel.transform, "ReactiveCommandPreview", "", 23, 136, 720, 37,
                16, UiKit.Muted, false, TextAnchor.MiddleCenter);

            defensePanel = ui.Rect(root, "ReactiveDefense", 634, 660, 656, 164).gameObject;
            ui.Box(defensePanel.transform, "DefenseShade", 0, 0, 656, 164, new Color(.02f, .045f, .05f, .94f));
            ui.Label(defensePanel.transform, "DefenseTitle", "СЛЕДИ ЗА УДАРОМ", 21, 3, 614, 38, 19, UiKit.Gold, false, TextAnchor.MiddleCenter);
            ui.Button(defensePanel.transform, "ReactiveDodge", "Уклонение", 39, 55, 270, 80, dodge, true);
            ui.Button(defensePanel.transform, "ReactiveParry", "Парирование", 347, 55, 270, 80, parry, true);

            counterPanel = ui.Rect(root, "ReactiveCounter", 634, 666, 656, 150).gameObject;
            ui.Box(counterPanel.transform, "CounterShade", 0, 0, 656, 150, new Color(.07f, .105f, .09f, .96f));
            ui.Label(counterPanel.transform, "CounterTitle", "КОНТРАТАКА  /  ШАНС ОТКРЫТ", 20, 8, 616, 43, 22, UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Button(counterPanel.transform, "ReactiveCounterConfirm", "Контратаковать", 133, 58, 390, 75, counter, true);
            saveBlockedPanel = ui.Rect(root, "ReactiveSaveBlocked", 634, 666, 656, 150).gameObject;
            ui.Box(saveBlockedPanel.transform, "SaveBlockedShade", 0, 0, 656, 150, new Color(.15f, .055f, .04f, .96f));
            ui.Label(saveBlockedPanel.transform, "SaveBlockedTitle", "ПРОФИЛЬ НЕ ЗАПИСАН", 20, 8, 616, 43, 22,
                UiKit.Gold, true, TextAnchor.MiddleCenter);
            ui.Button(saveBlockedPanel.transform, "ReactiveRetrySave", "Повторить запись", 133, 58, 390, 75, retrySave, true);
            commandPanel.SetActive(false);
            defensePanel.SetActive(false);
            counterPanel.SetActive(false);
            saveBlockedPanel.SetActive(false);
            hitFeedback.text = string.Empty;
            feedbackTime = 0f;
        }

        public void Present(CombatEvent combatEvent)
        {
            if (root == null || combatEvent == null || combatEvent.Kind != CombatEventKind.HitResolved) return;
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
                hitFeedback.text = combatEvent.Detail == "Counter" ? "КОНТРАТАКА  −" + combatEvent.Amount :
                    "УДАР  −" + combatEvent.Amount;
            hitFeedback.color = feedbackColor;
            feedbackTime = .8f;
        }

        public void Refresh(ReactiveBattleDisplay display)
        {
            if (root == null) return;
            hunterHp.text = "HP " + display.HunterHp;
            ap.text = "AP " + display.HunterAp + " / 6";
            targetHp.text = "HP " + display.EnemyHp;
            targetSeal.text = display.Broken ? "BROKEN" : "SEAL " + display.EnemySeal + " / " + display.EnemySealMax;
            targetSeal.color = display.Broken ? UiKit.Gold : UiKit.Jade;
            forecast.text = string.IsNullOrEmpty(display.Forecast) ? "ОЧЕРЁДНОСТЬ  —" : display.Forecast;
            telegraph.text = display.Telegraph ?? string.Empty;
            detail.text = display.Detail ?? string.Empty;
            commandPreview.text = display.CommandPreview ?? string.Empty;
            float progress = Mathf.Clamp01(display.ContactProgress);
            contactFill.rectTransform.sizeDelta = new Vector2(626 * progress, 9);
            contactFill.color = progress > .72f ? UiKit.Gold : UiKit.Jade;
            timingBeacon.color = display.Phase == ReactiveDisplayPhase.Reacting
                ? new Color(1f, .78f, .35f, .38f + .6f * progress) : Color.clear;
            bool command = display.Phase == ReactiveDisplayPhase.Command;
            bool defense = display.Phase == ReactiveDisplayPhase.Reacting;
            commandPanel.SetActive(command);
            defensePanel.SetActive(defense);
            counterPanel.SetActive(display.Phase == ReactiveDisplayPhase.CounterOffer);
            saveBlockedPanel.SetActive(display.Phase == ReactiveDisplayPhase.SaveBlocked);
            sealStrikeButton.interactable = display.CanSealStrike;
        }

        public void Tick(float seconds)
        {
            if (!hunter || !enemy) return;
            age += Mathf.Max(0, seconds);
            hunter.rectTransform.anchoredPosition = new Vector2(132, -167 + Mathf.Sin(age * 1.3f) * 2f);
            enemy.rectTransform.anchoredPosition = new Vector2(1240, -142 + Mathf.Sin(age * 1.05f + .7f) * 3f);
            feedbackTime = Mathf.Max(0, feedbackTime - seconds);
            float flash = Mathf.Clamp01(feedbackTime * 2.5f);
            hitFeedback.color = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash);
            hunterFlash.color = feedbackOnHunter ? new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash * .22f) : Color.clear;
            enemyFlash.color = feedbackOnHunter ? Color.clear : new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, flash * .2f);
        }

        public void ClearReferences()
        {
            root = null;
            hunter = enemy = null;
            hunterHp = ap = targetHp = targetSeal = forecast = telegraph = detail = commandPreview = null;
            contactFill = null;
            timingBeacon = hunterFlash = enemyFlash = null;
            hitFeedback = null;
            commandPanel = defensePanel = counterPanel = saveBlockedPanel = null;
            sealStrikeButton = null;
        }
    }
}
