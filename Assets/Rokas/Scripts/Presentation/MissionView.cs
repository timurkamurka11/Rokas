using System;
using Rokas.Core;
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
        private RawImage enemy;
        private RectTransform enemyBar;
        private RectTransform playerBar;
        private RectTransform autoBar;
        private Text enemyHp;
        private Text playerHp;
        private Text weakHint;
        private Button weakPoint;
        private RectTransform slash;
        private readonly Text[] numbers = new Text[12];
        private readonly float[] numberTimes = new float[12];
        private int numberIndex;
        private float age;
        private float hitTime;
        private float enemyAttackTime;

        public MissionView(UiKit ui, RokasAssets assets, GameSession session, RokasAudio audio,
            Action<Func<bool>, string> act, Action<Func<bool>, string> travel, Action<string> toast, WorldEffects world, Func<bool> paused)
        { this.ui = ui; this.assets = assets; this.session = session; this.audio = audio; this.act = act; this.travel = travel; this.toast = toast; this.world = world; this.paused = paused; }

        private void Attack(bool weak)
        {
            if (!paused()) session.ClickAttack(weak);
        }

        public void Build(RectTransform parent)
        {
            age = hitTime = enemyAttackTime = 0;
            if (session.State.phase == RunPhase.Portal)
            {
                ui.Label(parent, "PortalEyebrow", "МЕСТО, КОТОРОГО НЕТ НА КАРТЕ", 66, 148, 1120, 42, 20, UiKit.Gold);
                ui.Label(parent, "PortalTitle", "Другая сторона.", 63, 207, 1030, 102, 62, UiKit.Paper, true);
                ui.Label(parent, "PortalNote", "Город стихает. За воротами слышен поезд.", 67, 321, 1050, 70, 27);
                ui.Button(parent, "EnterPortal", "Войти в искажение", 1240, 866, 600, 76,
                    () => travel(session.EnterPortal, "ПЛАТФОРМА КИСАРАГИ\nСледующая остановка не объявлена."), true);
                ui.Button(parent, "ReturnFromPortal", "Вернуться домой", 67, 881, 450, 60,
                    () => travel(session.ReturnHome, "Вы возвращаетесь по мокрым улицам."));
                return;
            }

            bool fighting = session.State.phase == RunPhase.Combat;
            ui.Label(parent, "DepthLabel", "ГЛУБИНА 01     /     КОНТРАКТ E", 66, 134, 700, 43, 18, UiKit.Gold);
            ui.Label(parent, "EnemyName", "Безликий пассажир", 580, 131, 800, 64, 39, UiKit.Paper, true, TextAnchor.MiddleCenter);
            ui.Label(parent, "EnemyIdentity", "НОППЭРА-БО  /  ПОВРЕЖДЁННЫЙ", 650, 202, 660, 36, 16, UiKit.Muted, false, TextAnchor.MiddleCenter);
            enemy = ui.Art(parent, "FacelessCommuter", assets.enemy, 750, 254, 480, 720);
            if (fighting)
            {
                // The transparent sprite is visual; a narrow body target avoids clicking large empty margins.
                var target = ui.Box(parent, "EnemyAttack", 839, 297, 286, 644, new Color(1, 1, 1, 0), true);
                var attack = target.gameObject.AddComponent<Button>();
                attack.targetGraphic = target;
                attack.transition = Selectable.Transition.None;
                attack.onClick.AddListener(() => Attack(false));
                weakPoint = ui.Button(parent, "WeakPoint", "ПЕЧАТЬ", 1050, 439, 183, 59,
                    () => Attack(true), true);
                weakPoint.gameObject.SetActive(false);
                weakHint = ui.Label(parent, "WeakHint", "", 1270, 411, 530, 110, 23, UiKit.Jade);

                ui.Box(parent, "EnemyBarBack", 656, 239, 650, 8, new Color(.08f, .13f, .14f));
                enemyBar = ui.Box(parent, "EnemyBar", 656, 239, 650, 8, UiKit.Red).rectTransform;
                enemyHp = ui.Label(parent, "EnemyHp", "", 1364, 214, 350, 58, 22, UiKit.Paper);
                var playerPanel = ui.Panel(parent, "HunterVitals", 67, 735, 500, 220);
                ui.Label(playerPanel, "HunterVitalsLabel", "ОХОТНИК  /  КЛИНОК " + session.State.weaponLevel, 25, 17, 450, 43, 18, UiKit.Gold);
                playerHp = ui.Label(playerPanel, "HunterHp", "", 25, 68, 440, 40, 23);
                ui.Box(playerPanel, "HunterBarBack", 25, 124, 447, 7, new Color(.15f, .19f, .2f));
                playerBar = ui.Box(playerPanel, "HunterBar", 25, 124, 447, 7, UiKit.Jade).rectTransform;
                ui.Label(playerPanel, "AutoLabel", "АВТОАТАКА АКТИВНА", 25, 151, 440, 37, 17, UiKit.Muted);
                autoBar = ui.Box(playerPanel, "AutoTimer", 25, 198, 447, 3, UiKit.Gold).rectTransform;
                ui.Label(parent, "AttackHint", "Нажимайте на ёкая, чтобы помочь автоатаке.\nУспейте снять печать, когда она проявится.", 1280, 738, 540, 160, 23);
                ui.Button(parent, "ManualAttack", "Удар клинком  /  +клик", 1280, 899, 542, 58, () => Attack(false), true);
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
            if (!enemyHp) return;
            var state = session.State;
            enemyHp.text = Mathf.CeilToInt(state.enemyHp) + " / " + Mathf.CeilToInt(session.Contract.enemyHealth);
            playerHp.text = "ЗДОРОВЬЕ   " + Mathf.CeilToInt(state.playerHp) + " / 100";
            enemyBar.sizeDelta = new Vector2(650 * Mathf.Clamp01(state.enemyHp / session.Contract.enemyHealth), 8);
            playerBar.sizeDelta = new Vector2(447 * Mathf.Clamp01(state.playerHp / 100f), 7);
            float interval = session.Contract.autoInterval / (string.IsNullOrEmpty(state.preparedFoodId) ? 1 : 1.05f);
            autoBar.sizeDelta = new Vector2(447 * Mathf.Clamp01(1 - state.autoTimer / interval), 3);
            if (weakPoint)
            {
                bool active = session.Combat.WeakPointActive;
                weakPoint.gameObject.SetActive(active);
                weakHint.text = active ? "СНИМИТЕ ПЕЧАТЬ\nДвойной урон" : state.weakPointClaimed ? "Печать снята. Точный удар." : "";
            }
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

        public void Tick(float dt, bool paused)
        {
            if (paused) return;
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
            if (slash) slash.gameObject.SetActive(hitTime > .1f);
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
            enemy = null; enemyBar = playerBar = autoBar = slash = null;
            enemyHp = playerHp = weakHint = null; weakPoint = null;
            for (int i = 0; i < numbers.Length; i++) { numbers[i] = null; numberTimes[i] = 0; }
        }
    }
}
