using System;
using System.Collections.Generic;
using System.Linq;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class MissionView
    {
        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;
        private readonly RokasAudio audio;
        private readonly Action<Func<bool>, string> act;
        private readonly Action<Func<bool>, string> travel;
        private readonly Action<string> toast;
        private readonly WorldEffects world;
        private readonly Func<bool> paused;
        private readonly ReactiveMissionView reactiveView;
        private readonly ReactiveCombatAudio reactiveAudio;
        private RawImage enemy;
        private CombatHud combatHud;
        private Image combatFlash;
        private readonly Image[] sparks = new Image[8];
        private float sparkTime;
        public float HitStopRemaining { get; private set; }
        private RectTransform slash;
        private readonly Text[] numbers = new Text[12];
        private readonly float[] numberTimes = new float[12];
        private int numberIndex;
        private float age;
        private float hitTime;
        private float enemyAttackTime;
        public string SelectedReactiveTargetId { get { return reactiveView.SelectedTargetId; } }
        public int AnimatedEnemyCount { get { return reactiveView.AnimatedEnemyCount; } }
        public bool AnimatedActorsReady { get { return reactiveView.AnimatedActorsReady; } }
        public bool HunterApproachComplete { get { return reactiveView.HunterApproachComplete; } }
        public bool HunterAtHome { get { return reactiveView.HunterAtHome; } }
        public bool StartHunterApproach(string enemyId) { return reactiveView.StartHunterApproach(enemyId); }
        public void CancelHunterMotion() { reactiveView.CancelHunterMotion(); }
        public void SetReactivePresentationLocked(bool value) { reactiveView.SetPresentationLocked(value); }

        public MissionView(UiKit ui, RokasAssets assets, GameSession session, RokasAudio audio,
            Action<Func<bool>, string> act, Action<Func<bool>, string> travel, Action<string> toast, WorldEffects world,
            Func<bool> paused, Action<CommandKind, string> reactiveCommand,
            Action<ReactivePressKind> reactiveDefense, Action reactiveCounter, Action reactiveRetrySave,
            Action<string> reactiveTarget)
        {
            this.ui = ui; this.assets = assets; this.session = session; this.audio = audio;
            this.act = act; this.travel = travel; this.toast = toast; this.world = world; this.paused = paused;
            reactiveView = new ReactiveMissionView(ui, assets,
                () => reactiveCommand(CommandKind.Basic, null),
                () => reactiveCommand(CommandKind.Skill, "seal_strike"),
                () => reactiveCommand(CommandKind.Defend, null),
                () => reactiveCommand(CommandKind.Skill, "sweep"),
                () => reactiveCommand(CommandKind.Skill, "heavy"),
                () => reactiveCommand(CommandKind.Skill, "anchor"),
                () => reactiveDefense(ReactivePressKind.Dodge),
                () => reactiveDefense(ReactivePressKind.Parry), reactiveCounter, reactiveRetrySave, reactiveTarget);
            reactiveAudio = new ReactiveCombatAudio(audio);
        }

        public void CancelInput() { combatHud?.CancelInput(); }
        public void HandleInput(bool dodge, bool deflect, bool resonance)
        {
            if (paused() || session.State.phase != RunPhase.Combat || session.CombatMode != CombatMode.Legacy) return;
            if (deflect) session.Deflect();
            else if (dodge) session.Dodge();
            if (resonance) session.ActivateResonance();
        }

        public void Build(RectTransform parent)
        {
            age = hitTime = enemyAttackTime = 0;
            if (session.State.phase == RunPhase.Portal)
            {
                ui.Label(parent, "PortalEyebrow", "МЕСТО, КОТОРОГО НЕТ НА КАРТЕ", 66, 148, 1120, 42, 20, UiKit.Gold);
                ui.Label(parent, "PortalTitle", "Другая сторона.", 63, 207, 1030, 102, 62, UiKit.Paper, true);
                ui.Label(parent, "PortalNote", "Город стихает. За воротами слышен поезд.", 67, 321, 1050, 70, 27);
                ui.Button(parent, "EnterReactivePortal", "Войти в искажение", 1240, 866, 600, 76,
                    () => travel(session.EnterReactiveTestEncounter, "ПЛАТФОРМА КИСАРАГИ\nСледующая остановка не объявлена."), true);
                ui.Button(parent, "ReturnFromPortal", "Вернуться домой", 67, 881, 450, 60,
                    () => travel(session.ReturnHome, "Вы возвращаетесь по мокрым улицам."));
                return;
            }

            bool fighting = session.State.phase == RunPhase.Combat;
            if (fighting && session.CombatMode == CombatMode.ReactiveTurns)
            {
                combatHud = null;
                reactiveAudio.Reset();
                reactiveView.Build(parent);
                Refresh();
                return;
            }
            ui.Label(parent, "DepthLabel", "ГЛУБИНА 01     /     КОНТРАКТ E", 66, 134, 700, 43, 18, UiKit.Gold);
            ui.Label(parent, "EnemyName", "Безликий пассажир", 580, 131, 800, 64, 39, UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Label(parent, "EnemyIdentity", "НОППЭРА-БО  /  ПОВРЕЖДЁННЫЙ", 650, 202, 660, 36, 16, UiKit.Muted, false, TextAnchor.MiddleCenter);
            enemy = ui.Art(parent, "FacelessCommuter", assets.enemy, 750, 254, 480, 720);
            if (fighting)
            {
                combatHud = new CombatHud(ui, parent, session, paused);
                combatFlash = ui.Box(parent, "CombatImpactFlash", 605, 305, 720, 690, Color.clear);
                for (int i = 0; i < sparks.Length; i++)
                {
                    sparks[i] = ui.Box(parent, "ImpactSpark" + i, 970, 535, 5, 24, Color.clear);
                    sparks[i].rectTransform.localRotation = Quaternion.Euler(0, 0, i * 45);
                }
                slash = ui.Box(parent, "Slash", 880, 485, 285, 4, UiKit.Paper).rectTransform;
                slash.localRotation = Quaternion.Euler(0, 0, 38);
                slash.gameObject.SetActive(false);
                for (int i = 0; i < numbers.Length; i++)
                {
                    numbers[i] = ui.Label(parent, "Damage" + i, "", 990, 445, 260, 80, 46, UiKit.Paper, true, TextAnchor.MiddleCenter);
                    numbers[i].gameObject.SetActive(false);
                    numberTimes[i] = 0;
                }
            }
            else
            {
                bool success = session.State.phase == RunPhase.Sealed;
                audio.Play(success ? assets.seal : assets.portalSound);
                ui.Box(parent, "ResultShade", 0, 99, 1920, 907, new Color(.02f, .055f, .06f, .6f));
                ui.Label(parent, "SealStamp", success ? "ПЕЧАТЬ НАЛОЖЕНА" : "ПЕЧАТЬ НЕ УДЕРЖАЛА", 400, 334, 1120, 55, 22, UiKit.Gold, false, TextAnchor.MiddleCenter);
                ui.Label(parent, "MissionResult", success ? "Контракт выполнен." : "Сегодня — домой.", 250, 414, 1420, 112, 63, UiKit.Paper, true, TextAnchor.MiddleCenter);
                ui.Label(parent, "ResultNote", success ? "Ёкай рассыпается в пепел. Вдалеке открывается обратный путь." : "Защитный талисман вытянул вас из искажения. Оплаты не будет — но вы живы.",
                    420, 556, 1080, 104, 27, UiKit.Paper, false, TextAnchor.MiddleCenter);
                ui.Button(parent, "ReturnHome", "Вернуться домой", 660, 731, 600, 78,
                    () => travel(session.ReturnHome, "Знакомый свет в окне.\nВы снова дома."), true);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (session.State.phase == RunPhase.Combat && session.CombatMode == CombatMode.ReactiveTurns)
            {
                ReactiveCombatSession combat = session.ReactiveCombat;
                if (combat == null) return;
                ReactiveDisplayPhase displayPhase = ReactiveDisplayPhase.Result;
                if (session.SaveBlocked) displayPhase = ReactiveDisplayPhase.SaveBlocked;
                else if (combat.Phase == ReactivePhase.Suspended) displayPhase = ReactiveDisplayPhase.Suspended;
                else if (combat.Phase == ReactivePhase.PlayerCommand) displayPhase = ReactiveDisplayPhase.Command;
                else if (combat.CurrentPlayerSkillId == "heavy") displayPhase = ReactiveDisplayPhase.OffenseTiming;
                else if (combat.Phase == ReactivePhase.EnemyExecution) displayPhase = ReactiveDisplayPhase.Reacting;
                else if (combat.Phase == ReactivePhase.CounterWindow) displayPhase = ReactiveDisplayPhase.CounterOffer;

                string telegraph = string.Empty;
                string detail = string.Empty;
                float contactProgress = 0f;
                if (combat.Phase == ReactivePhase.EnemyExecution && combat.CurrentAttack != null)
                {
                    int hits = combat.CurrentAttack.Hits.Count;
                    long elapsed = Math.Max(0, combat.CurrentCombatUs - combat.CurrentActionStartUs);
                    long previousImpact = 0;
                    int upcoming = hits - 1;
                    for (int i = 0; i < hits; i++)
                    {
                        if (elapsed <= combat.CurrentAttack.Hits[i].ImpactUs)
                        {
                            upcoming = i;
                            break;
                        }
                        previousImpact = combat.CurrentAttack.Hits[i].ImpactUs;
                    }
                    long nextImpact = combat.CurrentAttack.Hits[upcoming].ImpactUs;
                    contactProgress = nextImpact > previousImpact
                        ? Mathf.Clamp01((float)(elapsed - previousImpact) / (nextImpact - previousImpact)) : 1f;
                    telegraph = hits == 1 ? "ВРАГ АТАКУЕТ" : "СЕРИЯ УДАРОВ  " + (upcoming + 1) + " / " + hits;
                    detail = "ЦЕЛЬ: КЕЙКО    •    ПОДГОТОВЬ ЗАЩИТУ";
                }
                else if (displayPhase == ReactiveDisplayPhase.OffenseTiming)
                {
                    long elapsed = Math.Max(0, combat.CurrentCombatUs - combat.CurrentActionStartUs);
                    contactProgress = Mathf.Clamp01(elapsed / 250000f);
                    telegraph = combat.CurrentOffenseTimingAccepted ? "ТОЧНЫЙ ТАЙМИНГ" : "ТЯЖЁЛЫЙ УДАР";
                    detail = combat.CurrentOffenseTimingAccepted ? "ПОПАДАНИЕ ПОДГОТОВЛЕНО" :
                        "SPACE / ЛКМ — НАЖМИ В ОКНЕ КОНТАКТА";
                }
                else if (combat.Phase == ReactivePhase.WaveTransition)
                {
                    displayPhase = ReactiveDisplayPhase.WaveTransition;
                    detail = "Следующая волна выходит на арену.";
                }
                else if (displayPhase == ReactiveDisplayPhase.Command) detail = "Выбери цель и действие. Порядок ходов указан выше.";
                else if (displayPhase == ReactiveDisplayPhase.CounterOffer) detail = "Подтверди контратаку до закрытия окна.";
                else if (displayPhase == ReactiveDisplayPhase.Suspended) detail = "Пауза. Отпусти клавиши защиты перед продолжением.";
                else if (displayPhase == ReactiveDisplayPhase.SaveBlocked) detail = "Не удалось сохранить бой. Повтори запись профиля.";

                IReadOnlyList<string> activeIds = combat.ActiveEnemyIds;
                IReadOnlyList<string> waveEnemyIds = combat.CurrentWaveEnemyIds;
                var enemies = new List<ReactiveEnemyDisplay>(activeIds.Count);
                for (int i = 0; i < activeIds.Count; i++)
                {
                    string id = activeIds[i];
                    CombatActorState state = combat.GetActorState(id);
                    if (state == null || !state.Alive) continue;
                    int slot = -1;
                    for (int waveSlot = 0; waveSlot < waveEnemyIds.Count; waveSlot++)
                        if (waveEnemyIds[waveSlot] == id) { slot = waveSlot; break; }
                    if (slot < 0) continue;
                    enemies.Add(new ReactiveEnemyDisplay
                    {
                        Id = id,
                        Slot = slot,
                        Name = "ЁКАЙ " + id.Substring(1),
                        Hp = state.Hp,
                        MaxHp = combat.GetActorMaxHp(id),
                        Seal = state.Seal,
                        SealMax = combat.GetActorSealMax(id),
                        Broken = state.Seal <= 0
                    });
                }
                string selectedId = combat.SelectedTargetId;
                if (string.IsNullOrEmpty(selectedId) || !activeIds.Contains(selectedId))
                    selectedId = activeIds.Count > 0 ? activeIds[0] : null;
                TurnForecast forecast = combat.Preview(null, 5);
                CommandImpactPreview basic = selectedId == null ? null :
                    combat.PreviewCommandImpact(CommandKind.Basic, null, selectedId);
                CommandImpactPreview seal = selectedId == null ? null :
                    combat.PreviewCommandImpact(CommandKind.Skill, "seal_strike", selectedId);
                CommandImpactPreview sweep = selectedId == null ? null :
                    combat.PreviewCommandImpact(CommandKind.Skill, "sweep", selectedId);
                CommandImpactPreview heavy = selectedId == null ? null :
                    combat.PreviewCommandImpact(CommandKind.Skill, "heavy", selectedId);
                CommandImpactPreview anchor = selectedId == null ? null :
                    combat.PreviewCommandImpact(CommandKind.Skill, "anchor", selectedId);
                string commandPreview = basic == null || seal == null ? string.Empty :
                    "ОБЫЧНЫЙ: −" + basic.Damage + " HP, +" + basic.ApGain + " AP     ПЕЧАТЬ: −" +
                    seal.Damage + " HP, −" + seal.SealDamage + " SEAL     ТЯЖЁЛЫЙ: SPACE / ЛКМ НА КОНТАКТЕ";
                string forecastText = string.Empty;
                for (int i = 0; i < forecast.Slots.Count; i++)
                {
                    if (i > 0) forecastText += "  →  ";
                    string actorId = forecast.Slots[i].ActorId;
                    forecastText += actorId == ReactiveDuelDefinitions.HunterId ? "КЕЙКО" :
                        "ЁКАЙ " + actorId.Substring(1);
                }
                int defeatedCount = 0;
                for (int i = 1; i <= 8; i++)
                {
                    CombatActorState actor = combat.GetActorState("E" + i);
                    if (actor != null && actor.Hp <= 0) defeatedCount++;
                }
                reactiveView.Refresh(new ReactiveBattleDisplay
                {
                    HunterHp = combat.HunterHp,
                    HunterAp = combat.HunterAp,
                    Enemies = enemies,
                    WaveEnemyIds = waveEnemyIds,
                    WaveSlotCount = waveEnemyIds.Count,
                    Wave = combat.CurrentWaveIndex + 1,
                    WaveCount = combat.WaveCount,
                    DefeatedCount = defeatedCount,
                    SelectedTargetId = combat.SelectedTargetId,
                    ActingId = combat.ActiveActorId,
                    CanSealStrike = seal != null && seal.CanAfford,
                    CanSweep = sweep != null && sweep.CanAfford,
                    CanHeavy = heavy != null && heavy.CanAfford,
                    CanAnchor = anchor != null && anchor.CanAfford,
                    Forecast = forecastText,
                    Telegraph = telegraph,
                    Detail = detail,
                    CommandPreview = commandPreview,
                    ContactProgress = contactProgress,
                    Phase = displayPhase
                });
                return;
            }
            combatHud?.Refresh();
        }

        public void OnAction(CombatAction action)
        {
            if (combatHud == null) return;
            combatHud.ShowFeedback(action);
            bool strong = action == CombatAction.Deflect || action == CombatAction.PerfectCut || action == CombatAction.Finisher || action == CombatAction.SealBreak || action == CombatAction.RitualSuccess;
            if (strong)
            {
                sparkTime = .32f;
                HitStopRemaining = action == CombatAction.RitualSuccess ? .12f : .06f;
                hitTime = .3f;
                world.Impact(action == CombatAction.RitualSuccess || action == CombatAction.SealBreak ? 1.4f : .9f);
                audio.Play(action == CombatAction.SealBreak || action == CombatAction.RitualSuccess ? assets.seal : assets.critical);
            }
            if (action == CombatAction.Resonance) { audio.Play(assets.critical); sparkTime = .45f; world.Impact(1); }
        }

        public void OnHit(CombatHit hit)
        {
            if (!enemy) return;
            if (hit.targetIsEnemy) hitTime = .22f; else enemyAttackTime = .3f;
            audio.Play(hit.critical ? assets.critical : assets.hit);
            world.Impact(hit.critical ? 1 : .3f);
            if (!session.State.settings.damageNumbers || numbers[0] == null) return;
            int index = numberIndex++ % numbers.Length;
            numberTimes[index] = .8f;
            var text = numbers[index];
            text.text = (hit.targetIsEnemy ? "" : "−") + Mathf.CeilToInt(hit.damage) + (hit.critical ? "!" : "");
            text.fontSize = hit.critical ? 63 : 43;
            text.color = hit.targetIsEnemy ? hit.critical ? UiKit.Gold : UiKit.Paper : UiKit.Red;
            text.rectTransform.anchoredPosition = hit.targetIsEnemy ? new Vector2(875 + index % 3 * 66, -408) : new Vector2(206, -728);
            text.gameObject.SetActive(true);
        }

        public void PresentReactiveCombatStep(CombatStep step)
        {
            if (step == null || session.CombatMode != CombatMode.ReactiveTurns) return;
            foreach (CombatEvent combatEvent in step.Events)
            {
                reactiveView.Present(combatEvent);
                bool combatCueHandled = reactiveAudio.Present(combatEvent);
                if (combatEvent.Kind != CombatEventKind.HitResolved) continue;
                bool perfect = combatEvent.Detail == "Perfect" || combatEvent.Detail == "Counter";
                bool defended = combatEvent.Detail == "Dodge" || combatEvent.Detail == "Parry" || perfect;
                if (combatEvent.Amount > 0 && !combatCueHandled)
                    audio.Play(perfect ? assets.critical : assets.hit);
                world.Impact(perfect ? .9f : defended ? .45f : .65f);
            }
        }

        public void Tick(float dt, bool paused)
        {
            if (paused || session.SaveBlocked) { CancelInput(); return; }
            if (session.State.phase == RunPhase.Combat && session.CombatMode == CombatMode.ReactiveTurns)
            {
                reactiveView.Tick(dt);
                ReactiveCombatSession combat = session.ReactiveCombat;
                reactiveAudio.Tick(dt, combat != null && !session.SaveBlocked &&
                    combat.Phase == ReactivePhase.PlayerCommand && combat.ActiveEnemyIds.Count > 0);
                return;
            }
            HitStopRemaining = Mathf.Max(0, HitStopRemaining - dt);
            combatHud?.Tick(dt);
            sparkTime = Mathf.Max(0, sparkTime - dt);
            if (combatFlash) combatFlash.color = new Color(.5f, .85f, .78f, sparkTime * .22f);
            for (int i = 0; i < sparks.Length; i++)
            {
                if (!sparks[i]) continue;
                float angle = i * Mathf.PI / 4;
                float distance = (1 - sparkTime / .45f) * 120;
                sparks[i].rectTransform.anchoredPosition = new Vector2(980 + Mathf.Cos(angle) * distance, -525 + Mathf.Sin(angle) * distance);
                sparks[i].color = new Color(.96f, .83f, .5f, sparkTime * 3);
            }
            age += dt;
            hitTime = Mathf.Max(0, hitTime - dt);
            enemyAttackTime = Mathf.Max(0, enemyAttackTime - dt);
            if (enemy)
            {
                float breathe = Mathf.Sin(age * 1.6f);
                float fade = session.State.phase == RunPhase.Sealed ? Mathf.Clamp01(1 - age * .85f) : 1;
                enemy.color = new Color(1, 1 - hitTime * 1.5f, 1 - hitTime * 2, fade);
                enemy.rectTransform.anchoredPosition = new Vector2(750 + Mathf.Sin(hitTime * 72) * hitTime * 35, -254 + breathe * 5);
                float scale = 1 + enemyAttackTime * .1f;
                enemy.rectTransform.localScale = new Vector3(scale, scale + breathe * .005f, 1);
            }
            if (slash)
            {
                slash.gameObject.SetActive(hitTime > .1f);
                slash.sizeDelta = new Vector2(session.Combat.ResonanceActive ? 390 : 285, session.Combat.ResonanceActive ? 7 : 4);
            }
            for (int i = 0; i < numbers.Length; i++)
            {
                if (!numbers[i] || numberTimes[i] <= 0) continue;
                numberTimes[i] -= dt;
                numbers[i].rectTransform.anchoredPosition += new Vector2(0, 90 * dt);
                var color = numbers[i].color;
                color.a = Mathf.Clamp01(numberTimes[i] * 2.5f);
                numbers[i].color = color;
                if (numberTimes[i] <= 0) numbers[i].gameObject.SetActive(false);
            }
        }

        public void ClearReferences()
        {
            CancelInput();
            reactiveView.ClearReferences();
            reactiveAudio.Reset();
            combatHud = null;
            combatFlash = null;
            HitStopRemaining = sparkTime = 0;
            enemy = null; slash = null;
            for (int i = 0; i < sparks.Length; i++) sparks[i] = null;
            for (int i = 0; i < numbers.Length; i++) { numbers[i] = null; numberTimes[i] = 0; }
        }
    }
}
