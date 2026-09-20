using System;
using System.Collections.Generic;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Specialized laptop page for the approved YOMI food reference. It reuses the existing
    // laptop chrome, input model, GameSession prepared-food slot and rounded-surface language.
    public sealed class LaptopFoodView
    {
        private static readonly Color White = new Color(.94f, .94f, .91f);
        private static readonly Color Soft = new Color(.64f, .71f, .75f);
        private static readonly Color Gold = new Color(.93f, .61f, .20f);
        private static readonly Color Panel = new Color(.025f, .055f, .082f, .94f);
        private static readonly Color Row = new Color(.035f, .072f, .10f, .96f);
        private static readonly Color RowSelected = new Color(.13f, .105f, .072f, .97f);

        private sealed class FoodItem
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string Summary;
            public readonly string Symbol;
            public readonly string Description;
            public readonly string EffectOne;
            public readonly string EffectTwo;
            public readonly string Duration;
            public readonly Rect ThumbUv;
            public readonly Rect DetailUv;
            public readonly string DetailResource;

            public FoodItem(string id, string name, string summary, string symbol, string description,
                string effectOne, string effectTwo, string duration, Rect thumbUv, Rect detailUv, string detailResource)
            {
                Id = id;
                Name = name;
                Summary = summary;
                Symbol = symbol;
                Description = description;
                EffectOne = effectOne;
                EffectTwo = effectTwo;
                Duration = duration;
                ThumbUv = thumbUv;
                DetailUv = detailUv;
                DetailResource = detailResource;
            }
        }

        private static readonly FoodItem[] Items =
        {
            new FoodItem(
                FoodService.RamenId,
                "Рамен Юмико",
                "Восстановление здоровья",
                "♥",
                "Фирменный рамен от Юмико. Тёплый, сытный, с заботой.\nОна всегда готовит его для охотников перед опасными контрактами.",
                "+25% восстановление здоровья",
                "+10% сопротивление страху",
                "Длительность: 15 мин",
                new Rect(0f, .25f, .333125f, .25f),
                new Rect(0f, .5f, 1f, .5f), null),
            new FoodItem(
                FoodService.OnigiriId,
                "Онигири странника",
                "Больше выносливости",
                "↟",
                "Простой дорожный онигири. Лёгкий, плотный и удобный перед длинным маршрутом.",
                "+15% запас выносливости",
                "+10% восстановление выносливости",
                "Длительность: 15 мин",
                new Rect(.333125f, .25f, .333125f, .25f),
                new Rect(.333125f, .25f, .333125f, .25f), "Food/Details/TravelerOnigiri"),
            new FoodItem(
                FoodService.MisoId,
                "Острый мисо-суп",
                "Защита от ёкаев",
                "◆",
                "Острый мисо согревает и помогает держать страх под контролем рядом с ёкаями.",
                "+15% защита от ёкаев",
                "+10% сопротивление страху",
                "Длительность: 15 мин",
                new Rect(.66625f, .25f, .33375f, .25f),
                new Rect(.66625f, .25f, .33375f, .25f), "Food/Details/SpicyMiso"),
            new FoodItem(
                FoodService.TempuraId,
                "Тэмпура охотника",
                "Урон в бою",
                "✦",
                "Хрустящая тэмпура для тех, кому предстоит тяжёлый бой и короткое окно для удара.",
                "+12% урон в бою",
                "+5% шанс критического удара",
                "Длительность: 15 мин",
                new Rect(0f, 0f, .333125f, .25f),
                new Rect(0f, 0f, .333125f, .25f), "Food/Details/HunterTempura"),
            new FoodItem(
                FoodService.MochiId,
                "Моти луны",
                "Концентрация и фокус",
                "◎",
                "Мягкие моти с лёгкой сладостью. Помогают собраться перед встречей с неизвестным.",
                "+10% концентрация",
                "+10% длительность слабых точек",
                "Длительность: 15 мин",
                new Rect(.333125f, 0f, .333125f, .25f),
                new Rect(.333125f, 0f, .333125f, .25f), "Food/Details/MoonMochi"),
            new FoodItem(
                FoodService.GreenTeaId,
                "Зелёный чай YOMI",
                "Восстановление энергии",
                "⚡",
                "Тёплый зелёный чай YOMI. Небольшой ритуал перед тем, как перейти на другую сторону.",
                "+5% скорость автоматических атак",
                "Спокойствие перед контрактом",
                "До возвращения домой",
                new Rect(.66625f, 0f, .33375f, .25f),
                new Rect(.66625f, 0f, .33375f, .25f), "Food/Details/GreenTea")
        };

        private readonly UiKit ui;
        private readonly GameSession session;
        private readonly Action<Func<bool>, string> act;
        private readonly Action click;
        private readonly Action<string> notify;

        private Texture2D atlas;
        private Texture2D[] detailTextures;
        private int selected;
        private Button[] rowButtons;
        private LaptopSurface[] rowRings;
        private LaptopSurface[] rowFaces;
        private Text[] rowTitles;
        private Button foodSidebarButton;
        private Button eatButton;
        private Text eatTitle;
        private Text detailTitle;
        private RawImage detailArt;
        private Text description;
        private Text effectOne;
        private Text effectTwo;
        private Text duration;

        public LaptopFoodView(UiKit ui, GameSession session, Action<Func<bool>, string> act, Action click, Action<string> notify)
        {
            this.ui = ui;
            this.session = session;
            this.act = act;
            this.click = click;
            this.notify = notify;
        }

        public void Build(RectTransform parent, string[] sectionTitles, Color[] sectionColors, Action<int> openSection)
        {
            atlas = Resources.Load<Texture2D>("Food/ROKAS_FoodAtlas");
            if (!atlas)
                throw new InvalidOperationException("ROKAS Food atlas is missing from Resources/Food.");

            detailTextures = new Texture2D[Items.Length];
            for (int i = 0; i < Items.Length; i++)
            {
                if (string.IsNullOrEmpty(Items[i].DetailResource)) continue;
                detailTextures[i] = Resources.Load<Texture2D>(Items[i].DetailResource);
                if (!detailTextures[i])
                    throw new InvalidOperationException("ROKAS Food detail image is missing: " + Items[i].DetailResource);
            }

            selected = Mathf.Clamp(selected, 0, Items.Length - 1);
            ui.Box(parent, "FoodPageShade", 0, 0, 1744, 812, new Color(.008f, .03f, .055f, .64f));

            BuildSidebar(parent, sectionTitles, sectionColors, openSection);
            BuildFoodHeader(parent);
            BuildRows(parent);
            BuildDetails(parent);
            ConfigureNavigation();
            UpdateSelection(selected);
            FocusSelected();
        }

        private void BuildSidebar(RectTransform parent, string[] titles, Color[] colors, Action<int> openSection)
        {
            var sidebar = Surface(parent, "FoodSidebar", 0, 0, 184, 812, 0,
                new Color(.018f, .055f, .092f, .76f)).rectTransform;
            ui.Box(sidebar, "FoodSidebarRule", 182, 0, 2, 812, new Color(.48f, .68f, .78f, .19f));

            int count = Mathf.Min(titles.Length, colors.Length);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float y = 7 + i * 88;
                var hit = Surface(sidebar, "FoodNav_" + i, 7, y, 170, 82, 12,
                    i == 2 ? new Color(.24f, .16f, .07f, .32f) : new Color(0, 0, 0, .015f), true);
                var button = hit.gameObject.AddComponent<Button>();
                StyleButton(button, hit);
                button.onClick.AddListener(() =>
                {
                    if (index == 2) return;
                    click?.Invoke();
                    openSection?.Invoke(index);
                });

                if (i == 2)
                {
                    Surface(sidebar, "FoodNavGlow", 55, y + 1, 70, 70, 15, new Color(.94f, .63f, .22f, .93f));
                    foodSidebarButton = button;
                }

                Surface(sidebar, "FoodNavFace_" + i, 60, y + 6, 60, 60, 13, colors[i]);
                Icon(sidebar, "FoodNavIcon_" + i, (LaptopGlyph)i, 70, y + 16, 40, White);
                ui.Label(sidebar, "FoodNavLabel_" + i, titles[i], 8, y + 62, 162, 20, 14,
                    i == 2 ? Gold : White, false, TextAnchor.MiddleCenter);
            }
        }

        private void BuildFoodHeader(RectTransform parent)
        {
            Icon(parent, "FoodHeadingIcon", LaptopGlyph.Food, 218, 24, 58, White);
            ui.Label(parent, "FoodHeading", "Еда", 292, 16, 500, 50, 31, White);
            ui.Label(parent, "FoodSubheading", "Блюда Юмико — сила для тех, кто идёт дальше.",
                292, 57, 510, 28, 17, Soft);
        }

        private void BuildRows(RectTransform parent)
        {
            rowButtons = new Button[Items.Length];
            rowRings = new LaptopSurface[Items.Length];
            rowFaces = new LaptopSurface[Items.Length];
            rowTitles = new Text[Items.Length];

            for (int i = 0; i < Items.Length; i++)
            {
                int index = i;
                float y = 108 + i * 98;
                rowRings[i] = Surface(parent, "FoodRowRing_" + i, 208, y, 600, 88, 16,
                    new Color(.35f, .49f, .58f, .35f), true);
                rowFaces[i] = Surface(parent, "FoodRowFace_" + i, 211, y + 3, 594, 82, 14, Row);

                var button = rowRings[i].gameObject.AddComponent<Button>();
                StyleButton(button, rowFaces[i]);
                rowButtons[i] = button;

                var art = RoundedArt(rowFaces[i].transform, "FoodThumb_" + i, 10, 8, 126, 66, 11);
                art.uvRect = Items[i].ThumbUv;
                rowTitles[i] = ui.Label(rowFaces[i].transform, "FoodName_" + i, Items[i].Name,
                    158, 7, 390, 34, 21, White);
                ui.Label(rowFaces[i].transform, "FoodEffectSymbol_" + i, Items[i].Symbol,
                    158, 43, 34, 27, 22, i == 0 ? Gold : Soft, false, TextAnchor.MiddleCenter);
                ui.Label(rowFaces[i].transform, "FoodEffect_" + i, Items[i].Summary,
                    199, 42, 358, 29, 16, Soft);

                BindFoodRow(button, index);
            }
        }

        private void BuildDetails(RectTransform parent)
        {
            var detail = Surface(parent, "FoodDetails", 832, 24, 880, 746, 19, Panel, true).rectTransform;
            Surface(detail, "FoodDetailsInner", 12, 12, 856, 722, 16, new Color(.018f, .047f, .073f, .55f));

            detailTitle = ui.Label(detail, "FoodDetailTitle", "", 28, 20, 520, 49, 30, White);
            ui.Label(detail, "FoodDetailCategory", "Еда", 30, 67, 180, 28, 16, Soft);

            var badge = Surface(detail, "YumikoBadge", 606, 24, 232, 42, 12,
                new Color(.10f, .12f, .15f, .88f)).rectTransform;
            Icon(badge, "YumikoBadgeIcon", LaptopGlyph.Food, 10, 7, 28, White);
            ui.Label(badge, "YumikoBadgeText", "Приготовлено Юмико", 44, 3, 180, 36, 15, White);

            Surface(detail, "FoodHeroFrame", 27, 103, 826, 332, 18, new Color(.47f, .58f, .62f, .45f));
            detailArt = RoundedArt(detail, "FoodHero", 31, 107, 818, 324, 16);

            description = ui.Label(detail, "FoodDescription", "", 29, 449, 822, 82, 17, Soft);

            var effects = Surface(detail, "FoodEffects", 28, 540, 824, 124, 16,
                new Color(.022f, .048f, .069f, .94f)).rectTransform;
            ui.Label(effects, "EffectHeart", "♥", 22, 10, 34, 30, 22, new Color(.98f, .38f, .42f),
                false, TextAnchor.MiddleCenter);
            effectOne = ui.Label(effects, "EffectOne", "", 66, 8, 720, 32, 17, White);
            ui.Label(effects, "EffectWard", "◆", 22, 45, 34, 30, 18, Soft,
                false, TextAnchor.MiddleCenter);
            effectTwo = ui.Label(effects, "EffectTwo", "", 66, 43, 720, 32, 17, White);
            ui.Label(effects, "EffectTime", "◷", 22, 80, 34, 30, 18, Soft,
                false, TextAnchor.MiddleCenter);
            duration = ui.Label(effects, "EffectDuration", "", 66, 78, 720, 32, 17, White);

            Surface(detail, "EatGlow", 28, 681, 824, 58, 14, new Color(.99f, .62f, .16f, .96f));
            var eatFace = Surface(detail, "EatButton", 31, 684, 818, 52, 12,
                new Color(.71f, .43f, .12f, .98f), true);
            eatButton = eatFace.gameObject.AddComponent<Button>();
            StyleButton(eatButton, eatFace);
            Icon(eatFace.transform, "EatIcon", LaptopGlyph.Food, 291, 10, 31, White);
            eatTitle = ui.Label(eatFace.transform, "EatTitle", "Съесть", 330, 0, 220, 52, 20,
                White, false, TextAnchor.MiddleCenter);
            eatButton.onClick.AddListener(() =>
            {
                click?.Invoke();
                Consume(selected);
            });
        }

        private void BindFoodRow(Button button, int index)
        {
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            AddTrigger(trigger, EventTriggerType.Select, () => UpdateSelection(index));
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => UpdateSelection(index));
            AddTrigger(trigger, EventTriggerType.PointerClick, () =>
            {
                click?.Invoke();
                UpdateSelection(index);
            });
            AddTrigger(trigger, EventTriggerType.Submit, () =>
            {
                UpdateSelection(index);
                click?.Invoke();
                Consume(index);
            });
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void ConfigureNavigation()
        {
            if (rowButtons == null || rowButtons.Length == 0 || !eatButton) return;
            for (int i = 0; i < rowButtons.Length; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = rowButtons[Mathf.Max(0, i - 1)],
                    selectOnDown = rowButtons[Mathf.Min(rowButtons.Length - 1, i + 1)],
                    selectOnLeft = foodSidebarButton,
                    selectOnRight = eatButton
                };
                rowButtons[i].navigation = nav;
            }
            var eatNav = eatButton.navigation;
            eatNav.mode = Navigation.Mode.Explicit;
            eatNav.selectOnUp = rowButtons[rowButtons.Length - 1];
            eatNav.selectOnDown = rowButtons[0];
            eatNav.selectOnLeft = rowButtons[selected];
            eatButton.navigation = eatNav;
        }

        private void UpdateSelection(int index)
        {
            selected = Mathf.Clamp(index, 0, Items.Length - 1);
            var item = Items[selected];
            for (int i = 0; i < rowRings.Length; i++)
            {
                bool active = i == selected;
                rowRings[i].color = active ? Gold : new Color(.35f, .49f, .58f, .35f);
                rowFaces[i].color = active ? RowSelected : Row;
                rowTitles[i].color = active ? White : new Color(.86f, .89f, .90f);
            }

            detailTitle.text = item.Name;
            Texture2D detailTexture = detailTextures != null ? detailTextures[selected] : null;
            detailArt.texture = detailTexture ? detailTexture : atlas;
            detailArt.uvRect = detailTexture ? new Rect(0, 0, 1, 1) : item.DetailUv;
            description.text = item.Description;
            effectOne.text = item.EffectOne;
            effectTwo.text = item.EffectTwo;
            duration.text = item.Duration;

            if (eatButton)
            {
                var nav = eatButton.navigation;
                nav.selectOnLeft = rowButtons[selected];
                eatButton.navigation = nav;
            }
            UpdateActionState();
        }

        private void Consume(int index)
        {
            selected = Mathf.Clamp(index, 0, Items.Length - 1);
            var item = Items[selected];
            if (session.GetFoodConsumeBlockReason(item.Id) == FoodConsumeBlockReason.ContractPaymentPending)
            {
                notify?.Invoke("Сначала сдайте контракт.");
                UpdateActionState();
                return;
            }
            if (act != null)
                act(() => session.ConsumeFood(item.Id), item.Name + " — эффект активен.");
            else
                session.ConsumeFood(item.Id);
            UpdateActionState();
        }

        private void UpdateActionState()
        {
            if (!eatButton || !eatTitle) return;
            bool validPhase = session.State.phase == RunPhase.Home || session.State.phase == RunPhase.Accepted;
            bool empty = string.IsNullOrEmpty(session.State.preparedFoodId);
            bool active = session.State.preparedFoodId == Items[selected].Id;
            bool paymentPending = session.State.phase == RunPhase.Payment;
            eatButton.interactable = paymentPending || (validPhase && empty);
            eatTitle.text = active ? "Эффект активен" : empty ? "Съесть" : "Другой эффект активен";
        }

        private void FocusSelected()
        {
            if (!EventSystem.current || rowButtons == null || rowButtons.Length == 0) return;
            EventSystem.current.SetSelectedGameObject(rowButtons[selected].gameObject);
        }

        private RawImage RoundedArt(Transform parent, string name, float x, float y, float w, float h, float radius)
        {
            var maskSurface = Surface(parent, name + "Mask", x, y, w, h, radius, Color.white);
            var mask = maskSurface.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            return ui.Art(maskSurface.transform, name, atlas, 0, 0, w, h);
        }

        private LaptopSurface Surface(Transform parent, string name, float x, float y, float w, float h,
            float radius, Color color, bool blocks = false)
        {
            var go = ui.Rect(parent, name, x, y, w, h).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _)) go.AddComponent<CanvasRenderer>();
            var surface = go.AddComponent<LaptopSurface>();
            surface.Radius = radius;
            surface.color = color;
            surface.raycastTarget = blocks;
            return surface;
        }

        private void Icon(Transform parent, string name, LaptopGlyph glyph, float x, float y, float size, Color color)
        {
            var go = ui.Rect(parent, name, x, y, size, size).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _)) go.AddComponent<CanvasRenderer>();
            var icon = go.AddComponent<LaptopIcon>();
            icon.Glyph = glyph;
            icon.color = color;
        }

        private static void StyleButton(Button button, Graphic graphic)
        {
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.82f, .86f, .90f);
            colors.disabledColor = new Color(.52f, .55f, .56f, .60f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        }
    }
}
