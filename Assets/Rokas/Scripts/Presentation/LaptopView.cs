using System;
using System.Globalization;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    // Presentation only: contracts, food and equipment still belong to the existing session.
    public sealed class LaptopView
    {
        private static readonly Color White = new Color(.94f, .94f, .91f);
        private static readonly Color Soft = new Color(.64f, .71f, .75f);
        private static readonly string[] Titles =
            { "Контракты", "Новости", "Еда", "Магазин", "Бестиарий", "Гайды", "Сообщения", "Профиль", "Скидки" };
        private static readonly string[] Names =
            { "LaptopContracts", "LaptopNews", "LaptopFood", "LaptopShop", "LaptopBestiary", "LaptopGuides", "LaptopMessages", "LaptopProfile", "LaptopDiscounts" };
        private static readonly Color[] TileColors =
        {
            new Color(.62f, .25f, .24f), new Color(.37f, .46f, .57f), new Color(.70f, .51f, .23f),
            new Color(.37f, .55f, .35f), new Color(.38f, .30f, .52f), new Color(.23f, .49f, .54f),
            new Color(.69f, .35f, .40f), new Color(.39f, .42f, .44f), new Color(.76f, .39f, .16f)
        };
        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;
        private readonly ContractPanels contracts;
        private readonly LaptopFoodView food;
        private readonly LaptopMessagesView messages;
        private readonly Action click;
        private readonly Action close;
        private readonly VideoSequencePresenter video;
        private readonly Func<float> videoVolume;
        private readonly Func<bool> videoTransitions;
        private RectTransform frame;
        private RectTransform content;
        private CanvasGroup windowGroup;
        private CanvasGroup contentGroup;
        private Text clock;
        private Text date;
        private int clockMinute = -1;
        private int section = -1;
        private int lastTile;
        private float openTime;
        private float pageTime;
        private Action closed;
        private bool bootConsumedThisHomeVisit;
        public bool IsClosing { get; private set; }
        public bool Booting { get; private set; }
        public bool MessagesOpen { get { return section == 6 && !IsClosing && !Booting; } }
        public string ActiveMessageContactId { get { return MessagesOpen ? messages.ActiveContactId : string.Empty; } }

        public LaptopView(UiKit ui, RokasAssets assets, GameSession session, ContractPanels contracts,
            Action<Func<bool>, string> act, Action click, Action<string> notify, Action close,
            VideoSequencePresenter video, Func<float> videoVolume, Func<bool> videoTransitions)
        {
            this.ui = ui;
            this.assets = assets;
            this.session = session;
            this.contracts = contracts;
            this.click = click;
            this.close = close;
            this.video = video ?? throw new ArgumentNullException(nameof(video));
            this.videoVolume = videoVolume ?? throw new ArgumentNullException(nameof(videoVolume));
            this.videoTransitions = videoTransitions ?? throw new ArgumentNullException(nameof(videoTransitions));
            food = new LaptopFoodView(ui, session, act, click, notify);
            messages = new LaptopMessagesView(ui, assets, session);
        }

        public void Reset()
        {
            video.Cancel();
            messages.Hide();
            section = -1;
            lastTile = 0;
            IsClosing = false;
            Booting = false;
            closed = null;
            frame = null;
            content = null;
            windowGroup = null;
            contentGroup = null;
            clock = null;
            date = null;
        }

        public void BeginHomeVisit()
        {
            bootConsumedThisHomeVisit = false;
        }

        public void Build(RectTransform parent)
        {
            Surface(parent, "LaptopShadow", 66, 50, 1792, 1008, 22, new Color(0, 0, 0, .5f));
            frame = Surface(parent, "YomiLaptop", 64, 36, 1792, 1008, 18, new Color(.018f, .023f, .027f), true).rectTransform;
            windowGroup = frame.gameObject.AddComponent<CanvasGroup>();
            windowGroup.alpha = 0;
            openTime = 0;
            pageTime = 0;
            content = null;
            contentGroup = null;
            clock = null;
            date = null;
            clockMinute = -1;
            if (!videoTransitions() || bootConsumedThisHomeVisit)
            {
                Booting = false;
                BuildReadyFrame();
                return;
            }
            bootConsumedThisHomeVisit = true;
            Booting = true;
            video.PlayInHost(frame, "LaptopBoot.mp4", "LaptopBootSurface", 1792, 1008, videoVolume(), FinishBoot);
        }

        private void FinishBoot()
        {
            if (!Booting || IsClosing || !frame) return;
            Booting = false;
            BuildReadyFrame();
        }

        private void BuildReadyFrame()
        {
            Surface(frame, "Camera", 892, 12, 8, 8, 4, new Color(.08f, .10f, .11f));
            ui.Box(frame, "ScreenEdge", 22, 30, 1748, 940, new Color(.12f, .16f, .18f));
            var screen = ui.Rect(frame, "LaptopScreen", 24, 32, 1744, 936);
            var wallpaper = ui.Art(screen, "LaptopWallpaper", assets.laptopWallpaper, 0, 0, 1744, 936);
            // Cover without stretching the original Japanese landscape.
            float sourceAspect = (float)assets.laptopWallpaper.width / assets.laptopWallpaper.height;
            float screenAspect = 1744f / 936f;
            if (sourceAspect > screenAspect)
                wallpaper.uvRect = new Rect((1 - screenAspect / sourceAspect) * .5f, 0, screenAspect / sourceAspect, 1);
            else
                wallpaper.uvRect = new Rect(0, (1 - sourceAspect / screenAspect) * .5f, 1, sourceAspect / screenAspect);
            ui.Box(screen, "WallpaperTone", 0, 0, 1744, 936, new Color(.01f, .025f, .05f, .08f));
            ui.Box(screen, "LaptopHeader", 0, 0, 1744, 62, new Color(.035f, .065f, .095f, .88f));
            Icon(screen, "BrandSeal", LaptopGlyph.Contracts, 28, 10, 40, White);
            ui.Label(screen, "LaptopBrand", "R O K A S", 88, 7, 250, 47, 25, White);
            ui.Label(screen, "LaptopNetwork", "YOMI  /  ЛЮДИ. ЁКАИ. БАЛАНС.", 334, 13, 620, 37, 15, Soft);
            Icon(screen, "NetworkSignal", LaptopGlyph.Wifi, 1550, 10, 38, White);
            Icon(screen, "PowerIndicator", LaptopGlyph.Battery, 1600, 9, 40, White);
            var exit = ChromeButton(screen, "ClosePanel", "", 1664, 9, 54, 44, close);
            Icon(exit.transform, "CloseGlyph", LaptopGlyph.Close, 11, 6, 32, White);
            content = ui.Rect(screen, "LaptopContent", 0, 62, 1744, 812);
            BuildContent();
            ui.Box(screen, "LaptopFooter", 0, 874, 1744, 62, new Color(.025f, .055f, .075f, .9f));
            ui.Box(screen, "FooterRule", 0, 874, 1744, 1, new Color(.55f, .68f, .72f, .25f));
            Icon(screen, "BalanceSeal", LaptopGlyph.Contracts, 29, 886, 38, Soft);
            ui.Label(screen, "BalanceMotto", "СИЛА В РАВНОВЕСИИ", 92, 888, 590, 35, 15, Soft);
            ui.Label(screen, "LaptopControls", "TAB  выбор     ENTER  открыть     ESC  назад", 660, 890, 645, 32, 14, Soft);
            date = ui.Label(screen, "LaptopDate", "", 1334, 888, 210, 35, 15, Soft, false, TextAnchor.MiddleRight);
            clock = ui.Label(screen, "LaptopClock", "", 1570, 881, 140, 46, 31, White, false, TextAnchor.MiddleRight);
            clockMinute = -1;
            UpdateClock();
        }

        private void BuildContent()
        {
            ui.Clear(content);
            var page = ui.Rect(content, "LaptopPage", 0, 0, 1744, 812);
            contentGroup = page.gameObject.AddComponent<CanvasGroup>();
            contentGroup.alpha = 0;
            pageTime = 0;
            if (section < 0)
            {
                BuildDesktop(page);
                return;
            }
            if (section == 2)
            {
                food.Build(page, Titles, TileColors, OpenSection);
                ChromeButton(page, "FoodBack", "← Назад", 208, 732, 180, 52, Back);
                return;
            }
            if (section == 6)
            {
                ui.Box(page, "AppShade", 0, 0, 1744, 812, new Color(.018f, .038f, .065f, .60f));
                var messagesBack = ChromeButton(page, "LaptopBack", "Назад", 42, 24, 158, 48, Back);
                Icon(messagesBack.transform, "BackGlyph", LaptopGlyph.Back, 8, 9, 28, White);
                Icon(page, "AppGlyph", (LaptopGlyph)section, 238, 23, 47, TileColors[section] * 1.4f);
                ui.Label(page, "AppTitle", Titles[section], 302, 21, 950, 50, 28, White);
                messages.Build(page);
                if (!EventSystem.current || !EventSystem.current.currentSelectedGameObject ||
                    !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(page)) Select(messagesBack);
                return;
            }
            ui.Box(page, "AppShade", 0, 0, 1744, 812, new Color(.018f, .038f, .065f, .60f));
            var back = ChromeButton(page, "LaptopBack", "Назад", 42, 24, 158, 48, Back);
            Icon(back.transform, "BackGlyph", LaptopGlyph.Back, 8, 9, 28, White);
            Icon(page, "AppGlyph", (LaptopGlyph)section, 238, 23, 47, TileColors[section] * 1.4f);
            ui.Label(page, "AppTitle", Titles[section], 302, 21, 950, 50, 28, White);
            var card = Surface(page, "LaptopAppCard", 242, 102, 1260, 674, 20,
                new Color(.035f, .065f, .085f, .97f), true).rectTransform;
            if (section == 0 || section == 2 || section == 3)
                contracts.BuildLaptopPage(card, section == 0 ? "contracts" : section == 2 ? "tea" : "workbench");
            else BuildPlaceholder(card);
            RestyleActions(card);
            FocusPreferred(card, "AcceptContract", "ClaimPayment", "PrepareTea", "ContractAccepted", "UpgradeWeapon");
            if (!EventSystem.current || !EventSystem.current.currentSelectedGameObject ||
                !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(page)) Select(back);
        }

        private void BuildDesktop(RectTransform parent)
        {
            Button focus = null;
            for (int i = 0; i < Titles.Length; i++)
            {
                int index = i;
                float x = 78 + (i % 4) * 194;
                float y = 74 + (i / 4) * 212;
                var tile = ui.Rect(parent, Names[i], x, y, 144, 144);
                tile.pivot = new Vector2(.5f, .5f);
                tile.anchoredPosition += new Vector2(72, -72);
                Surface(tile, "TileShadow", 0, 7, 144, 144, 25, new Color(.01f, .02f, .04f, .3f));
                var ring = Surface(tile, "SelectionRing", -4, -4, 152, 152, 29, new Color(.84f, .91f, .94f));
                var ringGroup = ring.gameObject.AddComponent<CanvasGroup>();
                ringGroup.alpha = 0;
                var face = Surface(tile, "TileFace", 0, 0, 144, 144, 25, TileColors[i], true);
                var button = tile.gameObject.AddComponent<Button>();
                Wire(button, face, () => OpenSection(index));
                tile.gameObject.AddComponent<LaptopTileFeedback>().Initialize(tile, ringGroup);
                Icon(tile, "AppIcon", (LaptopGlyph)i, 22, 22, 100, White);
                var label = ui.Label(tile, "AppName", Titles[i], -29, 152, 202, 38, 24, White, false, TextAnchor.MiddleCenter);
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, .015f, .03f, .85f);
                shadow.effectDistance = new Vector2(0, -2);
                if (i == 0 && session.State.phase == RunPhase.Payment)
                    Surface(tile, "PaymentNotification", 123, -4, 18, 18, 9, new Color(.96f, .71f, .34f));
                if (i == lastTile) focus = button;
            }
            Select(focus);
        }

        private void RestyleActions(RectTransform card)
        {
            foreach (var button in card.GetComponentsInChildren<Button>())
            {
                var original = button.targetGraphic as Image;
                if (!original) continue;
                var bounds = (RectTransform)button.transform;
                var face = Surface(bounds, "LaptopActionFace", 0, 0, bounds.rect.width, bounds.rect.height,
                    12, original.color, true);
                face.transform.SetAsFirstSibling();
                original.enabled = false;
                original.raycastTarget = false;
                button.targetGraphic = face;
                foreach (Transform child in bounds)
                {
                    if (child.name == "TopRule" || child.name == "LeftRule") child.gameObject.SetActive(false);
                    var text = child.GetComponent<Text>();
                    if (text) { text.alignment = TextAnchor.MiddleCenter; text.color = White; }
                }
            }
        }

        private void BuildPlaceholder(RectTransform card)
        {
            Surface(card, "AppPreviewTile", 558, 139, 144, 144, 28, TileColors[section]);
            Icon(card, "AppPreviewIcon", (LaptopGlyph)section, 581, 162, 98, White);
            ui.Label(card, "AppComingSoon", "Раздел будет доступен позже", 100, 316, 1060, 70, 31, White, false, TextAnchor.MiddleCenter);
            ui.Label(card, "AppComingSoonNote", "Возвращайтесь на рабочий стол YOMI", 100, 390, 1060, 46, 20, Soft, false, TextAnchor.MiddleCenter);
            ChromeButton(card, "PlaceholderBack", "На рабочий стол", 445, 498, 370, 60, Back);
        }

        private void OpenSection(int index)
        {
            if (IsClosing || Booting) return;
            section = lastTile = index;
            BuildContent();
        }

        public bool BackToDesktop()
        {
            if (Booting || section < 0 || IsClosing) return false;
            Back();
            return true;
        }

        private void Back()
        {
            if (section == 6) messages.Hide();
            section = -1;
            BuildContent();
        }

        public void BeginClose(Action complete)
        {
            if (IsClosing) return;
            if (Booting)
            {
                video.Cancel();
                Booting = false;
            }
            messages.Hide();
            IsClosing = true;
            closed = complete;
            openTime = .18f;
            if (windowGroup) windowGroup.interactable = false;
            // De-select while closing, so keyboard activation cannot reopen a section.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        public void Tick(float dt)
        {
            if (!frame || !windowGroup) return;
            if (IsClosing)
            {
                openTime = Mathf.Max(0, openTime - dt);
                windowGroup.alpha = Mathf.SmoothStep(0, 1, openTime / .18f);
                if (openTime <= 0)
                {
                    var callback = closed;
                    closed = null;
                    IsClosing = false;
                    callback?.Invoke();
                    return;
                }
            }
            else
            {
                openTime = Mathf.Min(.24f, openTime + dt);
                windowGroup.alpha = Mathf.SmoothStep(0, 1, openTime / .24f);
            }
            frame.anchoredPosition = new Vector2(64, -36 - 12 * (1 - windowGroup.alpha));
            if (contentGroup)
            {
                pageTime = Mathf.Min(.16f, pageTime + dt);
                contentGroup.alpha = Mathf.SmoothStep(0, 1, pageTime / .16f);
                ((RectTransform)contentGroup.transform).anchoredPosition = new Vector2(0, -6 * (1 - contentGroup.alpha));
            }
            if (section == 6) messages.Tick(dt);
            UpdateClock();
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            int stamp = now.DayOfYear * 1440 + now.Hour * 60 + now.Minute;
            if (!clock || stamp == clockMinute) return;
            clockMinute = stamp;
            clock.text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            date.text = now.ToString("d MMM", CultureInfo.GetCultureInfo("ru-RU")).ToUpperInvariant();
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

        private Button ChromeButton(Transform parent, string name, string title, float x, float y, float w, float h, Action action)
        {
            var face = Surface(parent, name, x, y, w, h, 12, new Color(.16f, .21f, .26f, .85f), true);
            var button = face.gameObject.AddComponent<Button>();
            Wire(button, face, action);
            ui.Label(face.transform, "Title", title, 20, 0, w - 30, h, 20, White, false, TextAnchor.MiddleCenter);
            return button;
        }

        private void Wire(Button button, Graphic graphic, Action action)
        {
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.83f, .86f, .90f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            button.onClick.AddListener(() => { if (IsClosing) return; click?.Invoke(); action?.Invoke(); });
        }

        private static void Select(Button button)
        { if (button && EventSystem.current) EventSystem.current.SetSelectedGameObject(button.gameObject); }

        private static void FocusPreferred(Transform parent, params string[] names)
        {
            foreach (string name in names)
                foreach (var button in parent.GetComponentsInChildren<Button>())
                    if (button.name == name && button.IsInteractable()) { Select(button); return; }
        }
    }
}
