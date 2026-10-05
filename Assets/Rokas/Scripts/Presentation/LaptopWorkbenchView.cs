using System;
using System.Collections;
using System.Globalization;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class WorkbenchTierViewData
    {
        public int Level;
        public string Title;
        public string Description;
        public WorkbenchTierState State;
    }

    public sealed class WorkbenchWeaponViewData
    {
        public WorkbenchWeaponKind Kind;
        public string Category;
        public string Title;
        public string LevelText;
        public string Description;
        public string StatLine;
        public int UpgradeCost;
        public bool CanUpgrade;
        public string UpgradeButtonText;
        public string LockReason;
        public WorkbenchTierViewData[] Tiers;
    }

    public static class WorkbenchPresentationModel
    {
        private static readonly EconomyService Economy = new EconomyService();
        private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        public static WorkbenchWeaponViewData Create(GameSession session, WorkbenchWeaponKind kind)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return kind == WorkbenchWeaponKind.Dagger ? CreateDagger() : CreateTwoHanded(session);
        }

        private static WorkbenchWeaponViewData CreateTwoHanded(GameSession session)
        {
            SaveData state = session.State;
            int level = Mathf.Max(1, state.weaponLevel);
            bool maxed = level >= SaveData.MaxWeaponLevel;
            int cost = maxed ? int.MaxValue : Economy.GetUpgradeCost(state);
            int damagePercent = Mathf.RoundToInt((Economy.GetWeaponDamageMultiplier(state) - 1f) * 100f);
            bool atHome = state.phase == RunPhase.Home;
            bool enoughCurrency = !maxed && state.yen >= cost;
            bool canUpgrade = !session.SaveBlocked && atHome && !maxed && enoughCurrency;

            string lockReason = string.Empty;
            if (session.SaveBlocked) lockReason = "СОХРАНЕНИЕ НЕДОСТУПНО — УЛУЧШЕНИЕ ЗАБЛОКИРОВАНО";
            else if (!atHome) lockReason = "ОБСЛУЖИВАНИЕ ДОСТУПНО ТОЛЬКО МЕЖДУ КОНТРАКТАМИ";
            else if (maxed) lockReason = "ДОСТИГНУТ МАКСИМАЛЬНЫЙ УРОВЕНЬ";
            else if (!enoughCurrency) lockReason = "НЕДОСТАТОЧНО ИЕН";

            string costText = maxed ? "МАКСИМАЛЬНЫЙ УРОВЕНЬ" :
                "УЛУЧШИТЬ   /   ¥ " + cost.ToString("N0", Russian);

            return new WorkbenchWeaponViewData
            {
                Kind = WorkbenchWeaponKind.TwoHanded,
                Category = "ДВУРУЧНИК",
                Title = level < 2 ? "Сталь без имени" : "Клинок под защитой",
                LevelText = "УРОВЕНЬ  " + level,
                Description = "Ручные и автоматические атаки используют текущую игровую прогрессию оружия. " +
                    "Каждый следующий уровень добавляет ещё +20% к базовому урону.",
                StatLine = "ТЕКУЩИЙ БОНУС К УРОНУ   +" + damagePercent + "%",
                UpgradeCost = maxed ? -1 : cost,
                CanUpgrade = canUpgrade,
                UpgradeButtonText = costText,
                LockReason = lockReason,
                Tiers = new[]
                {
                    new WorkbenchTierViewData
                    {
                        Level = level,
                        Title = "УРОВЕНЬ " + level,
                        Description = "Текущий уровень   •   +" + damagePercent + "% к базовому урону",
                        State = WorkbenchTierState.Current
                    },
                    new WorkbenchTierViewData
                    {
                        Level = level + 1,
                        Title = "УРОВЕНЬ " + (level + 1),
                        Description = maxed ? "Улучшения завершены" : "+20% к базовому урону   •   ¥ " + cost.ToString("N0", Russian),
                        State = maxed ? WorkbenchTierState.Locked : WorkbenchTierState.Available
                    },
                    new WorkbenchTierViewData
                    {
                        Level = level + 2,
                        Title = "УРОВЕНЬ " + (level + 2),
                        Description = "Откроется после уровня " + (level + 1),
                        State = WorkbenchTierState.Locked
                    }
                }
            };
        }

        private static WorkbenchWeaponViewData CreateDagger()
        {
            return new WorkbenchWeaponViewData
            {
                Kind = WorkbenchWeaponKind.Dagger,
                Category = "КИНЖАЛ",
                Title = "Ритуальный кинжал",
                LevelText = "ВЕТКА НЕ ПОДКЛЮЧЕНА",
                Description = "Отдельная игровая прогрессия кинжала пока отсутствует в текущей модели сохранения. " +
                    "Верстак показывает оружие, но не создаёт фиктивные характеристики или стоимость.",
                StatLine = "ДАННЫЕ УЛУЧШЕНИЙ НЕДОСТУПНЫ",
                UpgradeCost = -1,
                CanUpgrade = false,
                UpgradeButtonText = "УЛУЧШЕНИЕ НЕДОСТУПНО",
                LockReason = "ДЛЯ КИНЖАЛА НЕТ ОТДЕЛЬНОЙ АВТОРИТЕТНОЙ ЭКОНОМИКИ",
                Tiers = new[]
                {
                    LockedTier(1),
                    LockedTier(2),
                    LockedTier(3)
                }
            };
        }

        private static WorkbenchTierViewData LockedTier(int level)
        {
            return new WorkbenchTierViewData
            {
                Level = level,
                Title = "УРОВЕНЬ " + level,
                Description = "Данные недоступны",
                State = WorkbenchTierState.Locked
            };
        }
    }

    public sealed class LaptopWorkbenchView
    {
        private static readonly Color Cyan = new Color(.27f, .78f, 1f, .96f);
        private static readonly Color CyanSoft = new Color(.22f, .57f, .75f, .55f);
        private static readonly Color CyanDim = new Color(.16f, .37f, .50f, .45f);
        private static readonly Color Navy = new Color(.018f, .047f, .075f, .97f);
        private static readonly Color NavyLight = new Color(.025f, .075f, .115f, .96f);
        private static readonly Color White = new Color(.91f, .96f, 1f, 1f);
        private static readonly Color Muted = new Color(.57f, .70f, .78f, 1f);

        private readonly UiKit ui;
        private readonly GameSession session;
        private readonly Action<Func<bool>, string> act;
        private WorkbenchWeaponKind selectedKind = WorkbenchWeaponKind.TwoHanded;
        private WorkbenchController controller;

        public string PreferredControlName => selectedKind == WorkbenchWeaponKind.Dagger ?
            "WorkbenchWeaponDagger" : "WorkbenchWeaponTwoHanded";

        public LaptopWorkbenchView(UiKit ui, GameSession session, Action<Func<bool>, string> act)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.act = act ?? throw new ArgumentNullException(nameof(act));
        }

        public void Build(RectTransform parent, Action back)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            float parentWidth = parent.rect.width > 1 ? parent.rect.width : parent.sizeDelta.x;
            float parentHeight = parent.rect.height > 1 ? parent.rect.height : parent.sizeDelta.y;
            float scale = Mathf.Min(parentWidth / 1600f, parentHeight / 740f);
            scale = Mathf.Max(.1f, scale);

            var root = ui.Rect(parent, "WorkbenchRoot", 0, 0, 1600, 740);
            root.localScale = new Vector3(scale, scale, 1);
            root.anchoredPosition = new Vector2(
                Mathf.Max(0, (parentWidth - 1600f * scale) * .5f),
                -Mathf.Max(0, (parentHeight - 740f * scale) * .5f));

            CanvasGroup openGroup = root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<WorkbenchOpenMotion>().Initialize(root, openGroup);

            var mainFrame = AddPanel(root, "WorkbenchMainFrame", 0, 0, 1600, 740, 22, Navy,
                new Color(Cyan.r, Cyan.g, Cyan.b, .74f), 3.0f);
            mainFrame.InnerBorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .22f);
            BuildGrid(root, 1600, 740);
            ui.Box(root, "WorkbenchTopScanLine", 408, 24, 850, 1, new Color(Cyan.r, Cyan.g, Cyan.b, .17f));
            ui.Box(root, "WorkbenchBottomScanLine", 408, 715, 850, 1, new Color(Cyan.r, Cyan.g, Cyan.b, .10f));

            var header = AddPanel(root, "WorkbenchHeader", 28, 18, 392, 66, 14,
                new Color(.024f, .08f, .125f, .98f), Cyan, 2.7f);
            ui.Label(header.transform, "WorkbenchHeaderTitle", "ВЕРСТАК", 28, 7, 282, 31, 26, White);
            ui.Label(header.transform, "WorkbenchHeaderSubtitle", "РИТУАЛЬНОЕ СНАРЯЖЕНИЕ", 28, 35, 302, 20, 12, Muted);
            ui.Box(header.transform, "HeaderAccentA", 337, 18, 9, 28, Cyan);
            ui.Box(header.transform, "HeaderAccentB", 351, 18, 9, 28, Cyan * new Color(1, 1, 1, .74f));
            ui.Box(header.transform, "HeaderAccentC", 365, 18, 9, 28, Cyan * new Color(1, 1, 1, .42f));

            Button closeButton = DecoratedButton(root, "WorkbenchClose", string.Empty, 1525, 19, 48, 48,
                back, out WorkbenchPanelGraphic closeFace, out _);
            closeFace.CornerCut = 11;
            var closeIcon = ui.Rect(closeButton.transform, "CloseGlyph", 8, 8, 31, 31).gameObject.AddComponent<LaptopIcon>();
            closeIcon.Glyph = LaptopGlyph.Close;
            closeIcon.color = White;

            controller = root.gameObject.AddComponent<WorkbenchController>();

            BuildWeaponCard(root, "WorkbenchWeaponTwoHanded", WorkbenchWeaponKind.TwoHanded,
                "ДВУРУЧНИК", 28, 112, controller, out WorkbenchPanelGraphic twoFace, out Outline twoGlow,
                out WorkbenchWeaponIcon3D twoIcon);
            BuildWeaponCard(root, "WorkbenchWeaponDagger", WorkbenchWeaponKind.Dagger,
                "КИНЖАЛ", 28, 358, controller, out WorkbenchPanelGraphic daggerFace, out Outline daggerGlow,
                out WorkbenchWeaponIcon3D daggerIcon);

            var viewer = AddPanel(root, "WorkbenchViewer", 220, 108, 610, 570, 20,
                new Color(.012f, .038f, .064f, .98f), new Color(Cyan.r, Cyan.g, Cyan.b, .74f), 2.8f);
            viewer.InnerBorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .22f);
            var viewerInner = AddPanel(viewer.transform, "WorkbenchViewerInner", 16, 16, 578, 538, 15,
                new Color(.007f, .024f, .041f, .90f), new Color(Cyan.r, Cyan.g, Cyan.b, .30f), 1.4f);
            viewerInner.InnerBorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .15f);
            ui.Box(viewer.transform, "ViewerCornerTLH", 28, 30, 92, 3, new Color(Cyan.r, Cyan.g, Cyan.b, .72f));
            ui.Box(viewer.transform, "ViewerCornerTLV", 28, 30, 3, 58, new Color(Cyan.r, Cyan.g, Cyan.b, .52f));
            ui.Box(viewer.transform, "ViewerCornerBRH", 490, 537, 92, 3, new Color(Cyan.r, Cyan.g, Cyan.b, .58f));
            ui.Box(viewer.transform, "ViewerCornerBRV", 579, 482, 3, 58, new Color(Cyan.r, Cyan.g, Cyan.b, .38f));
            ui.Box(viewer.transform, "ViewerHorizon", 76, 284, 458, 1, new Color(Cyan.r, Cyan.g, Cyan.b, .08f));
            ui.Box(viewer.transform, "ViewerTopTick", 270, 19, 70, 2, CyanSoft);
            ui.Box(viewer.transform, "ViewerBottomTick", 270, 548, 70, 2, CyanSoft);

            var hudBackRect = ui.Rect(viewer.transform, "WorkbenchHudBack", 75, 70, 460, 460);
            CenterPivot(hudBackRect);
            var hudBack = hudBackRect.gameObject.AddComponent<WorkbenchHudGraphic>();
            hudBack.color = new Color(Cyan.r, Cyan.g, Cyan.b, .17f);
            hudBack.RotationSpeed = -2.15f;

            var hudRect = ui.Rect(viewer.transform, "WorkbenchHud", 101, 96, 408, 408);
            CenterPivot(hudRect);
            var hud = hudRect.gameObject.AddComponent<WorkbenchHudGraphic>();
            hud.color = new Color(Cyan.r, Cyan.g, Cyan.b, .46f);
            hud.RotationSpeed = 3.5f;

            var previewRect = ui.Rect(viewer.transform, "WorkbenchWeaponPreview", 86, 42, 438, 486);
            CenterPivot(previewRect);
            var previewImage = previewRect.gameObject.AddComponent<RawImage>();
            previewImage.color = Color.white;
            previewImage.raycastTarget = false;
            CanvasGroup previewGroup = previewRect.gameObject.AddComponent<CanvasGroup>();
            var preview = previewRect.gameObject.AddComponent<WorkbenchWeaponPreview3D>();
            preview.Initialize(previewImage, selectedKind);

            Button previous = DecoratedButton(viewer.transform, "WorkbenchPreviousWeapon", string.Empty, 12, 238, 72, 96,
                () => controller?.Cycle(-1), out WorkbenchPanelGraphic previousFace, out _);
            previousFace.FillColor = new Color(.015f, .065f, .105f, .82f);
            previousFace.BorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .72f);
            previousFace.CornerCut = 13f;
            var previousArrow = ui.Rect(previous.transform, "ArrowGlyph", 6, 9, 60, 78).gameObject.AddComponent<WorkbenchArrowGraphic>();
            previousArrow.Direction = -1;
            previousArrow.color = White;

            Button next = DecoratedButton(viewer.transform, "WorkbenchNextWeapon", string.Empty, 526, 238, 72, 96,
                () => controller?.Cycle(1), out WorkbenchPanelGraphic nextFace, out _);
            nextFace.FillColor = new Color(.015f, .065f, .105f, .82f);
            nextFace.BorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .72f);
            nextFace.CornerCut = 13f;
            var nextArrow = ui.Rect(next.transform, "ArrowGlyph", 6, 9, 60, 78).gameObject.AddComponent<WorkbenchArrowGraphic>();
            nextArrow.Direction = 1;
            nextArrow.color = White;

            var right = ui.Rect(root, "WorkbenchData", 855, 104, 515, 574);
            CanvasGroup rightGroup = right.gameObject.AddComponent<CanvasGroup>();
            var info = AddPanel(right, "WorkbenchInfoPanel", 0, 0, 515, 205, 16, NavyLight,
                new Color(Cyan.r, Cyan.g, Cyan.b, .62f), 2.1f);
            info.InnerBorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .16f);
            Text category = ui.Label(info.transform, "WorkbenchInfoCategory", string.Empty, 24, 15, 220, 24, 13, Muted);
            Text title = ui.Label(info.transform, "WorkbenchInfoTitle", string.Empty, 24, 38, 465, 45, 28, White);
            Text level = ui.Label(info.transform, "WorkbenchInfoLevel", string.Empty, 24, 82, 465, 26, 14, Cyan);
            ui.Box(info.transform, "InfoRule", 24, 114, 465, 1, new Color(Cyan.r, Cyan.g, Cyan.b, .22f));
            Text description = ui.Label(info.transform, "WorkbenchInfoDescription", string.Empty, 24, 122, 465, 54, 16, Muted);
            Text stat = ui.Label(info.transform, "WorkbenchInfoStat", string.Empty, 24, 177, 465, 22, 13, Cyan);

            ui.Label(right, "WorkbenchUpgradesLabel", "УЛУЧШЕНИЯ", 5, 222, 200, 24, 13, Muted);
            WorkbenchTierWidgets[] tiers =
            {
                BuildTier(right, 0, 252),
                BuildTier(right, 1, 334),
                BuildTier(right, 2, 416)
            };

            Button upgrade = DecoratedButton(right, "UpgradeWeapon", string.Empty, 0, 503, 515, 63,
                () => controller?.Upgrade(), out WorkbenchPanelGraphic upgradeFace, out _);
            upgradeFace.FillColor = new Color(.025f, .34f, .34f, .96f);
            upgradeFace.BorderColor = new Color(.22f, .95f, .88f, .95f);
            upgradeFace.InnerBorderColor = new Color(.36f, 1f, .94f, .26f);
            ui.Box(upgrade.transform, "UpgradeGlowLineTop", 20, 8, 210, 2, new Color(.36f, 1f, .94f, .62f));
            ui.Box(upgrade.transform, "UpgradeGlowLineBottom", 285, 53, 210, 2, new Color(.36f, 1f, .94f, .30f));
            Text upgradeText = upgrade.transform.Find("Title").GetComponent<Text>();
            upgradeText.fontSize = 22;
            upgradeText.color = White;
            Text lockReason = ui.Label(right, "WorkbenchLockReason", string.Empty, 7, 568, 501, 28, 12, Muted,
                false, TextAnchor.MiddleCenter);

            BuildFutureColumn(root);

            controller.Initialize(
                session,
                act,
                selectedKind,
                value => selectedKind = value,
                new[] { twoFace, daggerFace },
                new[] { twoGlow, daggerGlow },
                new[] { twoIcon, daggerIcon },
                preview,
                previewRect,
                previewGroup,
                rightGroup,
                category,
                title,
                level,
                description,
                stat,
                tiers,
                upgrade,
                upgradeFace,
                upgradeText,
                lockReason);
        }

        private void BuildWeaponCard(Transform parent, string name, WorkbenchWeaponKind kind, string label,
            float x, float y, WorkbenchController target, out WorkbenchPanelGraphic face, out Outline glow,
            out WorkbenchWeaponIcon3D icon)
        {
            Button button = DecoratedButton(parent, name, string.Empty, x, y, 168, 214,
                () => target?.Select(kind), out face, out glow);
            var iconBack = AddPanel(button.transform, "WeaponCardIconWell", 14, 14, 140, 140, 16,
                new Color(.004f, .019f, .033f, .72f), new Color(Cyan.r, Cyan.g, Cyan.b, .25f), 1.2f);
            iconBack.InnerBorderColor = new Color(Cyan.r, Cyan.g, Cyan.b, .10f);
            var iconRect = ui.Rect(button.transform, "WeaponCardPreview", 19, 19, 130, 130);
            CenterPivot(iconRect);
            var rawIcon = iconRect.gameObject.AddComponent<RawImage>();
            rawIcon.color = Color.white;
            rawIcon.raycastTarget = false;
            icon = iconRect.gameObject.AddComponent<WorkbenchWeaponIcon3D>();
            icon.Initialize(rawIcon, kind);
            ui.Label(button.transform, "WeaponCardLabel", label, 10, 160, 148, 33, 17, White, false, TextAnchor.MiddleCenter);
            ui.Label(button.transform, "WeaponCardIndex", kind == WorkbenchWeaponKind.TwoHanded ? "01" : "02",
                15, 8, 28, 20, 10, CyanSoft);
            ui.Box(button.transform, "WeaponCardAccent", 12, 199, 144, 2, new Color(Cyan.r, Cyan.g, Cyan.b, .40f));
        }

        private WorkbenchTierWidgets BuildTier(Transform parent, int index, float y)
        {
            var panel = AddPanel(parent, "WorkbenchTier" + index, 0, y, 515, 70, 12,
                new Color(.021f, .061f, .092f, .96f), CyanDim, 1.4f);
            var badge = AddPanel(panel.transform, "TierBadge", 12, 12, 46, 46, 10,
                new Color(.02f, .09f, .13f, .92f), CyanSoft, 1.5f);
            Text badgeText = ui.Label(badge.transform, "TierBadgeText", string.Empty, 0, 0, 46, 46, 16, White,
                false, TextAnchor.MiddleCenter);
            Text title = ui.Label(panel.transform, "TierTitle", string.Empty, 72, 9, 418, 23, 14, White);
            Text description = ui.Label(panel.transform, "TierDescription", string.Empty, 72, 31, 418, 29, 13, Muted);
            Text state = ui.Label(panel.transform, "TierState", string.Empty, 392, 8, 96, 23, 11, Cyan,
                false, TextAnchor.MiddleRight);
            return new WorkbenchTierWidgets(panel, badge, badgeText, title, description, state);
        }

        private void BuildFutureColumn(Transform parent)
        {
            var side = AddPanel(parent, "WorkbenchFutureColumn", 1420, 108, 170, 534, 16,
                new Color(.014f, .040f, .064f, .96f), CyanDim, 1.8f);
            ui.Box(side.transform, "FutureTopAccent", 16, 13, 54, 3, CyanSoft);
            ui.Label(side.transform, "FutureHeader", "МОДУЛИ", 16, 29, 138, 25, 15, White, false, TextAnchor.MiddleCenter);
            BuildFutureCard(side.transform, "FutureRecovery", "ВОССТАНОВИТЕЛЬ", 16, 78);
            BuildFutureCard(side.transform, "FutureGuide", "ПРОВОДНИК", 16, 255);
            ui.Label(side.transform, "FutureCaption", "БУДУЩИЕ\nФУНКЦИИ", 16, 446, 138, 50, 13, Muted, false, TextAnchor.MiddleCenter);
        }

        private void BuildFutureCard(Transform parent, string name, string title, float x, float y)
        {
            var card = AddPanel(parent, name, x, y, 138, 144, 13,
                new Color(.018f, .048f, .073f, .88f), new Color(Cyan.r, Cyan.g, Cyan.b, .22f), 1.2f);
            ui.Label(card.transform, "LockMark", "LOCK", 16, 26, 106, 34, 18, CyanSoft, false, TextAnchor.MiddleCenter);
            ui.Label(card.transform, "Title", title, 8, 73, 122, 43, 11, Muted, false, TextAnchor.MiddleCenter);
            ui.Box(card.transform, "LockRule", 28, 124, 82, 1, new Color(Cyan.r, Cyan.g, Cyan.b, .18f));
        }

        private void BuildGrid(Transform parent, float width, float height)
        {
            for (int i = 1; i < 10; i++)
                ui.Box(parent, "GridV" + i, i * width / 10f, 0, 1, height,
                    new Color(Cyan.r, Cyan.g, Cyan.b, .022f));
            for (int i = 1; i < 6; i++)
                ui.Box(parent, "GridH" + i, 0, i * height / 6f, width, 1,
                    new Color(Cyan.r, Cyan.g, Cyan.b, .022f));
        }

        private static void CenterPivot(RectTransform rect)
        {
            Vector2 size = rect.sizeDelta;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition += new Vector2(size.x * .5f, -size.y * .5f);
        }

        private WorkbenchPanelGraphic AddPanel(Transform parent, string name, float x, float y, float w, float h,
            float cut, Color fill, Color border, float borderWidth)
        {
            var rect = ui.Rect(parent, name, x, y, w, h);
            var graphic = rect.gameObject.AddComponent<WorkbenchPanelGraphic>();
            graphic.FillColor = fill;
            graphic.BorderColor = border;
            graphic.CornerCut = cut;
            graphic.BorderWidth = borderWidth;
            return graphic;
        }

        private Button DecoratedButton(Transform parent, string name, string title, float x, float y, float w, float h,
            Action action, out WorkbenchPanelGraphic face, out Outline glow)
        {
            Button button = ui.Button(parent, name, title, x, y, w, h, action);
            Image baseImage = button.GetComponent<Image>();
            baseImage.color = new Color(0, 0, 0, 0);
            foreach (Transform child in button.transform)
                if (child.name == "TopRule" || child.name == "LeftRule") child.gameObject.SetActive(false);
            face = AddPanel(button.transform, "TechFace", 0, 0, w, h, Mathf.Min(14, h * .2f), NavyLight, CyanDim, 1.5f);
            face.transform.SetAsFirstSibling();
            button.targetGraphic = face;
            glow = face.gameObject.AddComponent<Outline>();
            glow.effectDistance = new Vector2(2, -2);
            glow.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0);
            button.gameObject.AddComponent<WorkbenchTechButtonFeedback>().Initialize(button, face, glow);
            return button;
        }
    }

    internal sealed class WorkbenchTierWidgets
    {
        public readonly WorkbenchPanelGraphic Panel;
        public readonly WorkbenchPanelGraphic Badge;
        public readonly Text BadgeText;
        public readonly Text Title;
        public readonly Text Description;
        public readonly Text State;

        public WorkbenchTierWidgets(WorkbenchPanelGraphic panel, WorkbenchPanelGraphic badge, Text badgeText,
            Text title, Text description, Text state)
        {
            Panel = panel;
            Badge = badge;
            BadgeText = badgeText;
            Title = title;
            Description = description;
            State = state;
        }
    }

    internal sealed class WorkbenchController : MonoBehaviour
    {
        private static readonly Color Cyan = new Color(.27f, .78f, 1f, .96f);
        private static readonly Color CyanSoft = new Color(.22f, .57f, .75f, .55f);
        private static readonly Color CyanDim = new Color(.16f, .37f, .50f, .45f);
        private static readonly Color NavyLight = new Color(.025f, .075f, .115f, .96f);
        private static readonly Color Muted = new Color(.57f, .70f, .78f, 1f);

        private GameSession session;
        private Action<Func<bool>, string> act;
        private Action<WorkbenchWeaponKind> selectedChanged;
        private WorkbenchWeaponKind selected;
        private WorkbenchPanelGraphic[] selectorFaces;
        private Outline[] selectorGlows;
        private WorkbenchWeaponIcon3D[] selectorIcons;
        private WorkbenchWeaponPreview3D preview;
        private RectTransform previewRect;
        private CanvasGroup previewGroup;
        private CanvasGroup rightGroup;
        private Text category;
        private Text title;
        private Text level;
        private Text description;
        private Text stat;
        private WorkbenchTierWidgets[] tiers;
        private Button upgrade;
        private WorkbenchPanelGraphic upgradeFace;
        private Text upgradeText;
        private Text lockReason;
        private Coroutine switchRoutine;

        public void Initialize(GameSession session, Action<Func<bool>, string> act, WorkbenchWeaponKind initial,
            Action<WorkbenchWeaponKind> selectedChanged, WorkbenchPanelGraphic[] selectorFaces, Outline[] selectorGlows,
            WorkbenchWeaponIcon3D[] selectorIcons, WorkbenchWeaponPreview3D preview,
            RectTransform previewRect, CanvasGroup previewGroup, CanvasGroup rightGroup,
            Text category, Text title, Text level, Text description, Text stat, WorkbenchTierWidgets[] tiers,
            Button upgrade, WorkbenchPanelGraphic upgradeFace, Text upgradeText, Text lockReason)
        {
            this.session = session;
            this.act = act;
            this.selectedChanged = selectedChanged;
            this.selectorFaces = selectorFaces;
            this.selectorGlows = selectorGlows;
            this.selectorIcons = selectorIcons;
            this.preview = preview;
            this.previewRect = previewRect;
            this.previewGroup = previewGroup;
            this.rightGroup = rightGroup;
            this.category = category;
            this.title = title;
            this.level = level;
            this.description = description;
            this.stat = stat;
            this.tiers = tiers;
            this.upgrade = upgrade;
            this.upgradeFace = upgradeFace;
            this.upgradeText = upgradeText;
            this.lockReason = lockReason;
            selected = initial;
            ApplyModel(initial, true);
        }

        public void Select(WorkbenchWeaponKind kind)
        {
            if (kind == selected && switchRoutine == null) return;
            selected = kind;
            selectedChanged?.Invoke(kind);
            UpdateSelectorState();
            if (switchRoutine != null) StopCoroutine(switchRoutine);
            switchRoutine = StartCoroutine(SwitchRoutine(kind));
        }

        public void Cycle(int direction)
        {
            Select(selected == WorkbenchWeaponKind.TwoHanded ? WorkbenchWeaponKind.Dagger : WorkbenchWeaponKind.TwoHanded);
        }

        public void Upgrade()
        {
            WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, selected);
            if (selected != WorkbenchWeaponKind.TwoHanded || !data.CanUpgrade) return;
            act(session.UpgradeWeapon, "Клинок стал сильнее. Печать тихо шуршит.");
            ApplyModel(selected, true);
        }

        private IEnumerator SwitchRoutine(WorkbenchWeaponKind kind)
        {
            previewGroup.alpha = 1f;
            previewRect.localScale = Vector3.one;
            previewRect.anchoredPosition = new Vector2(305f, -285f);
            rightGroup.alpha = 1f;

            const float outDuration = .11f;
            for (float t = 0; t < outDuration; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / outDuration));
                previewGroup.alpha = 1f - p;
                previewRect.localScale = Vector3.one * Mathf.Lerp(1f, .87f, p);
                previewRect.anchoredPosition = new Vector2(Mathf.Lerp(305f, 326f, p), -285f);
                rightGroup.alpha = 1f - p;
                yield return null;
            }

            ApplyModel(kind, true);
            previewGroup.alpha = 0f;
            rightGroup.alpha = 0f;
            previewRect.anchoredPosition = new Vector2(283f, -285f);

            const float inDuration = .18f;
            for (float t = 0; t < inDuration; t += Time.unscaledDeltaTime)
            {
                float p = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / inDuration));
                previewGroup.alpha = p;
                previewRect.localScale = Vector3.one * Mathf.Lerp(.88f, 1f, p);
                previewRect.anchoredPosition = new Vector2(Mathf.Lerp(283f, 305f, p), -285f);
                rightGroup.alpha = p;
                yield return null;
            }

            previewGroup.alpha = 1f;
            previewRect.localScale = Vector3.one;
            previewRect.anchoredPosition = new Vector2(305f, -285f);
            rightGroup.alpha = 1f;
            switchRoutine = null;
        }

        private void ApplyModel(WorkbenchWeaponKind kind, bool updatePreview)
        {
            WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, kind);
            selected = kind;
            if (updatePreview)
            {
                preview.SetWeapon(kind);
                preview.RenderNow();
            }
            category.text = data.Category;
            title.text = data.Title;
            level.text = data.LevelText;
            description.text = data.Description;
            stat.text = data.StatLine;
            upgrade.interactable = data.CanUpgrade;
            upgradeText.text = data.UpgradeButtonText;
            lockReason.text = data.LockReason;
            upgradeFace.FillColor = data.CanUpgrade ? new Color(.025f, .34f, .34f, .96f) :
                new Color(.025f, .10f, .12f, .82f);
            upgradeFace.BorderColor = data.CanUpgrade ? new Color(.22f, .95f, .88f, .95f) : CyanDim;

            for (int i = 0; i < tiers.Length; i++) ApplyTier(tiers[i], data.Tiers[i]);
            UpdateSelectorState();
        }

        private void ApplyTier(WorkbenchTierWidgets target, WorkbenchTierViewData data)
        {
            target.BadgeText.text = data.Level.ToString(CultureInfo.InvariantCulture);
            target.Title.text = data.Title;
            target.Description.text = data.Description;
            switch (data.State)
            {
                case WorkbenchTierState.Current:
                    target.Panel.FillColor = new Color(.027f, .10f, .15f, .98f);
                    target.Panel.BorderColor = Cyan;
                    target.Badge.BorderColor = Cyan;
                    target.State.text = "ТЕКУЩИЙ";
                    target.State.color = Cyan;
                    break;
                case WorkbenchTierState.Available:
                    target.Panel.FillColor = new Color(.023f, .073f, .105f, .96f);
                    target.Panel.BorderColor = CyanSoft;
                    target.Badge.BorderColor = CyanSoft;
                    target.State.text = "ДОСТУПЕН";
                    target.State.color = Muted;
                    break;
                default:
                    target.Panel.FillColor = new Color(.014f, .035f, .052f, .82f);
                    target.Panel.BorderColor = CyanDim;
                    target.Badge.BorderColor = CyanDim;
                    target.State.text = "LOCK";
                    target.State.color = new Color(Muted.r, Muted.g, Muted.b, .62f);
                    break;
            }
        }

        private void UpdateSelectorState()
        {
            for (int i = 0; i < selectorFaces.Length; i++)
            {
                bool active = (i == 0 && selected == WorkbenchWeaponKind.TwoHanded) ||
                    (i == 1 && selected == WorkbenchWeaponKind.Dagger);
                selectorFaces[i].FillColor = active ? new Color(.025f, .12f, .18f, .98f) : NavyLight;
                selectorFaces[i].BorderColor = active ? Cyan : CyanDim;
                selectorGlows[i].effectColor = active ? new Color(Cyan.r, Cyan.g, Cyan.b, .48f) :
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0);
                WorkbenchTechButtonFeedback feedback =
                    selectorFaces[i].transform.parent.GetComponent<WorkbenchTechButtonFeedback>();
                if (feedback) feedback.RestGlowAlpha = active ? .42f : .02f;
                if (selectorIcons != null && i < selectorIcons.Length && selectorIcons[i])
                    selectorIcons[i].SetSelected(active);
            }
        }

    }

    internal sealed class WorkbenchOpenMotion : MonoBehaviour
    {
        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 target;
        private float elapsed;

        public void Initialize(RectTransform rect, CanvasGroup group)
        {
            this.rect = rect;
            this.group = group;
            target = rect.anchoredPosition;
            rect.anchoredPosition = target + new Vector2(0, -10);
            group.alpha = 0;
        }

        private void Update()
        {
            if (!rect || !group) { Destroy(this); return; }
            elapsed = Mathf.Min(.22f, elapsed + Time.unscaledDeltaTime);
            float p = Mathf.SmoothStep(0, 1, elapsed / .22f);
            group.alpha = p;
            rect.anchoredPosition = Vector2.Lerp(target + new Vector2(0, -10), target, p);
            if (elapsed >= .22f) Destroy(this);
        }
    }
}
