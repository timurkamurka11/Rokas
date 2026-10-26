using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// First-pass visual quest journal for the authored 1920x1080 hub UI.
    /// Uses the three supplied artwork PNGs, with all text deliberately absent.
    /// No changes to quest progression, VN, saves or the Keiko cursor.
    /// </summary>
    public sealed class RokasQuestJournalHud : IDisposable
    {
        private const string ClosedPath = "Rokas/Journal/Journal_Closed";
        private const string OpenPath = "Rokas/Journal/Journal_Open";
        private const string PanelPath = "Rokas/Journal/Journal_Panel";

        private readonly RectTransform root;
        private readonly RectTransform launcher;
        private readonly RectTransform bookVisual;
        private readonly CanvasGroup modalGroup;
        private readonly RectTransform modal;
        private readonly RectTransform panelFrame;
        private readonly RawImage closedBook;
        private readonly RawImage openBook;
        private readonly RawImage hoverLight;
        private readonly Image selectedRow;
        private readonly Action clickSound;
        private readonly RokasJournalSettings settings;
        private readonly bool ownedDefaults;
        private readonly AudioSource bookAudio;
        private readonly AudioClip openClip;
        private static Sprite avatarMaskSprite;
        private bool available;
        private bool hovered;
        private bool pressed;
        private bool isOpen;
        private bool disposed;
        private float hoverAmount;
        private float openAmount;

        public bool IsOpen => isOpen;
        public bool HasArt { get; }

        public RokasQuestJournalHud(UiKit ui, RectTransform stage, Transform panelLayer,
            Action clickSound)
        {
            this.clickSound = clickSound;
            settings = Resources.Load<RokasJournalSettings>(
                RokasJournalSettings.ResourcePath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<RokasJournalSettings>();
                ownedDefaults = true;
            }
            settings.Normalize();
            openClip = settings.openSound != null
                ? settings.openSound
                : Resources.Load<AudioClip>("Rokas/Journal/Journal_OpenSfx");
            Texture2D closed = Resources.Load<Texture2D>(ClosedPath);
            Texture2D opened = Resources.Load<Texture2D>(OpenPath);
            Texture2D background = Resources.Load<Texture2D>(PanelPath);
            HasArt = closed != null && opened != null && background != null;

            root = ui.Rect(stage, "ROKAS_Journal_HUD_StageOverlay", 0, 0, 1920, 1080);
            // Above room hotspots; below laptop/settings, transition and VN dialogue.
            root.SetSiblingIndex(panelLayer.GetSiblingIndex());
            bookAudio = root.gameObject.AddComponent<AudioSource>();
            bookAudio.playOnAwake = false;
            bookAudio.spatialBlend = 0f;
            root.gameObject.SetActive(false);

            // Modal is first child so the journal icon stays above the dimmer.
            modal = ui.Rect(root, "Journal_Modal", 0, 0, 1920, 1080);
            modalGroup = modal.gameObject.AddComponent<CanvasGroup>();
            modalGroup.alpha = 0;
            modalGroup.interactable = false;
            modalGroup.blocksRaycasts = false;
            modal.gameObject.SetActive(false);

            Image shade = ui.Box(modal, "Journal_Backdrop_ClickToClose",
                0, 0, 1920, 1080,
                new Color(.005f, .014f, .027f, settings.backdropDim), true);
            var shadeButton = shade.gameObject.AddComponent<Button>();
            shadeButton.targetGraphic = shade;
            shadeButton.transition = Selectable.Transition.None;
            shadeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            shadeButton.onClick.AddListener(Close);

            // Original authored image is 1448 x 1086 (4:3), preserved at 1200x900.
            panelFrame = ui.Rect(modal, "Journal_Panel_Frame",
                settings.panelPosition.x, settings.panelPosition.y,
                settings.panelSize.x, settings.panelSize.y);
            RawImage art = ui.Art(panelFrame, "Journal_AuthoredPanel_NoText",
                background, 0, 0, settings.panelSize.x, settings.panelSize.y);
            art.raycastTarget = false;

            // Block backdrop clicks through unused areas of the journal.
            Image panelHit = ui.Box(panelFrame, "Journal_Panel_ClickSurface",
                0, 0, settings.panelSize.x, settings.panelSize.y,
                new Color(1f, 1f, 1f, .001f), true);
            panelHit.raycastTarget = true;

            // Six transparent hit targets aligned to the original left-hand
            // empty rows. Their own titles/content will be supplied later.
            var rowRect = settings.categoryListRect;
            selectedRow = ui.Box(panelFrame, "Journal_SelectedRow_GentleAmber",
                rowRect.x + 9f, rowRect.y + 2f, rowRect.width - 18f,
                rowRect.height - 7f, settings.selectionColor);
            selectedRow.raycastTarget = false;

            if (settings.showAvatar && settings.avatar != null)
                CreateMaskedAvatar(ui);
            if (settings.showText)
            {
                CreateJournalText(ui);
                CreateObjectives(ui);
            }
            for (int i = 0; i < 6; i++)
            {
                int row = i;
                Image hit = ui.Box(panelFrame, "Journal_NavSlot_" + row,
                    rowRect.x, rowRect.y + row * settings.categoryStep,
                    rowRect.width, rowRect.height,
                    new Color(1f, 1f, 1f, .001f), true);
                Button tab = hit.gameObject.AddComponent<Button>();
                tab.targetGraphic = hit;
                tab.transition = Selectable.Transition.None;
                tab.navigation = new Navigation { mode = Navigation.Mode.None };
                tab.onClick.AddListener(() =>
                {
                    clickSound?.Invoke();
                    selectedRow.rectTransform.anchoredPosition =
                        new Vector2(rowRect.x + 9f,
                            -(rowRect.y + 2f + row * settings.categoryStep));
                });
            }

            // Clicking the book is a separate hit area from the artwork.
            float size = settings.iconSize;
            Image hitSurface = ui.Box(root, "Journal_Icon_HitArea",
                settings.iconPosition.x, settings.iconPosition.y,
                size, size, new Color(1f, 1f, 1f, .001f), true);
            launcher = hitSurface.rectTransform;
            Button opener = hitSurface.gameObject.AddComponent<Button>();
            opener.targetGraphic = hitSurface;
            opener.transition = Selectable.Transition.None;
            opener.navigation = new Navigation { mode = Navigation.Mode.None };
            opener.onClick.AddListener(Toggle);
            var pointer = hitSurface.gameObject.AddComponent<RokasJournalHoverInput>();
            pointer.Configure(value => hovered = value, value => pressed = value);
            // Keep the hitbox stationary. Only the artwork moves/bobs, so
            // hovering never causes pointer-exit flicker at the hitbox edge.
            bookVisual = ui.Rect(launcher, "Journal_BookFloatingVisual",
                0, 0, size, size);
            bookVisual.pivot = new Vector2(.5f, .5f);
            bookVisual.anchoredPosition = new Vector2(size * .5f, -size * .5f);
            float glowPad = size * .06f;
            hoverLight = ui.Art(bookVisual, "Journal_HoverSoftLight",
                opened, -glowPad, -glowPad, size + 2f * glowPad,
                size + 2f * glowPad);
            closedBook = ui.Art(bookVisual, "Journal_ClosedBook",
                closed, 0, 0, size, size);
            openBook = ui.Art(bookVisual, "Journal_OpenBook_OnHover",
                opened, 0, 0, size, size);
            hoverLight.raycastTarget = false;
            closedBook.raycastTarget = false;
            openBook.raycastTarget = false;
            hoverLight.color = new Color(1f, .78f, .39f, 0f);
            openBook.color = new Color(1f, 1f, 1f, 0f);

            if (!HasArt)
                Debug.LogWarning("[ROKAS Journal] Missing one or more authored resources: " +
                    ClosedPath + ", " + OpenPath + ", " + PanelPath +
                    ". Journal remains hidden until the three real PNGs are imported.");
        }

        private void CreateJournalText(UiKit ui)
        {
            AddText(ui, "Journal_CharacterName", settings.characterName,
                settings.characterNameRect, settings.categoryFontSize,
                settings.mutedTextColor, TextAnchor.MiddleCenter);
            AddText(ui, "Journal_QuestTitle", settings.journalTitle,
                settings.journalTitleRect, settings.headingFontSize,
                settings.textColor, TextAnchor.MiddleLeft);
            AddText(ui, "Journal_Description", settings.questDescription,
                settings.questDescriptionRect, settings.bodyFontSize,
                settings.textColor, TextAnchor.UpperLeft);
            AddText(ui, "Journal_Notes", settings.notes,
                settings.notesRect, settings.bodyFontSize,
                settings.mutedTextColor, TextAnchor.UpperLeft);
            for (int i = 0; i < 6; ++i)
            {
                var r = settings.categoryListRect;
                r.y += settings.categoryStep * i;
                r.x += 16;
                r.width -= 26;
                AddText(ui, "Journal_Category_" + i,
                    RokasJournalSettings.CategoryAt(settings.categoryTitles, i),
                    r, settings.categoryFontSize, settings.textColor,
                    TextAnchor.MiddleLeft);
            }
        }

        private void CreateObjectives(UiKit ui)
        {
            for (int i = 0; i < 4; ++i)
            {
                string value = RokasJournalSettings.CategoryAt(
                    settings.questObjectives, i);
                if (string.IsNullOrEmpty(value)) continue;
                Rect r = settings.objectivesStartRect;
                r.y += i * settings.objectiveSpacing;
                if (settings.showCheckboxes)
                {
                    var circle = ui.Box(panelFrame,
                        "Journal_Objective_Checkbox_" + i,
                        r.x, r.y + r.height * .5f - 8f, 16f, 16f,
                        new Color(.20f, .52f, .64f, .66f));
                    circle.raycastTarget = false;
                    r.x += 29f;
                    r.width -= 29f;
                }
                AddText(ui, "Journal_Objective_" + i, value, r,
                    settings.bodyFontSize, settings.textColor,
                    TextAnchor.MiddleLeft);
            }
        }

        private void AddText(UiKit ui, string name, string value,
            Rect rect, int size, Color tint, TextAnchor align)
        {
            if (string.IsNullOrEmpty(value)) return;
            Text label = ui.Label(panelFrame, name, value,
                rect.x, rect.y, rect.width, rect.height,
                size, tint, false, align);
            if (settings.font != null) label.font = settings.font;
        }

        private void CreateMaskedAvatar(UiKit ui)
        {
            if (avatarMaskSprite == null)
            {
                const int pixels = 128;
                var maskTexture = new Texture2D(
                    pixels, pixels, TextureFormat.RGBA32, false, true);
                maskTexture.name = "ROKAS_Journal_CircularAvatarMask";
                maskTexture.wrapMode = TextureWrapMode.Clamp;
                Color32[] pixelData = new Color32[pixels * pixels];
                for (int y = 0; y < pixels; ++y)
                for (int x = 0; x < pixels; ++x)
                {
                    float nx = (x + .5f - pixels * .5f) / (pixels * .5f);
                    float ny = (y + .5f - pixels * .5f) / (pixels * .5f);
                    float alpha = Mathf.Clamp01(
                        (1f - Mathf.Sqrt(nx * nx + ny * ny)) * 80f);
                    pixelData[y * pixels + x] = new Color32(
                        255, 255, 255, (byte)(255f * alpha));
                }
                maskTexture.SetPixels32(pixelData);
                maskTexture.Apply(false, true);
                avatarMaskSprite = Sprite.Create(maskTexture,
                    new Rect(0, 0, pixels, pixels), new Vector2(.5f, .5f),
                    100f);
            }
            Rect r = settings.avatarRect;
            var parent = ui.Rect(panelFrame, "Journal_Avatar_RoundSlot",
                r.x, r.y, r.width, r.height);
            var shape = parent.gameObject.AddComponent<Image>();
            shape.sprite = avatarMaskSprite;
            shape.raycastTarget = false;
            var mask = parent.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            RawImage portrait = ui.Art(parent, "Journal_CustomAvatar",
                settings.avatar, 0, 0, r.width, r.height);
            portrait.color = settings.avatarTint;
            portrait.raycastTarget = false;
        }

        public void Tick(float unscaledDeltaTime, bool showInHub)
        {
            if (disposed) return;
            bool show = HasArt && showInHub;
            if (show != available)
            {
                available = show;
                if (!show)
                {
                    isOpen = false;
                    openAmount = 0;
                    hoverAmount = 0;
                    hovered = false;
                    pressed = false;
                    modal.gameObject.SetActive(false);
                }
                root.gameObject.SetActive(show);
            }
            if (!available) return;

            float dt = Mathf.Clamp(unscaledDeltaTime, 0f, .1f);
            float hoverTarget = hovered || isOpen ? 1f : 0f;
            hoverAmount = Mathf.Lerp(hoverAmount, hoverTarget,
                1f - Mathf.Exp(-settings.hoverResponse * dt));
            if (Mathf.Abs(hoverAmount - hoverTarget) < .001f)
                hoverAmount = hoverTarget;

            float wave = Mathf.Sin(Time.unscaledTime *
                (Mathf.PI * 2f) * settings.hoverFloatHz);
            float floatOffset = settings.hoverFloatAmplitude * wave * hoverAmount;
            float bookScale = 1f + settings.hoverScale * hoverAmount -
                (pressed ? .045f : 0f);
            bookVisual.localScale = new Vector3(bookScale, bookScale, 1f);
            bookVisual.anchoredPosition = new Vector2(
                settings.iconSize * .5f,
                -settings.iconSize * .5f +
                settings.hoverRise * hoverAmount + floatOffset);
            closedBook.color = new Color(1f, 1f, 1f, 1f - hoverAmount);
            openBook.color = new Color(1f, 1f, 1f, hoverAmount);
            hoverLight.color = new Color(1f, .74f, .37f,
                settings.hoverGlow * hoverAmount);

            float openTarget = isOpen ? 1f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, openTarget,
                dt * settings.openSpeed);
            if (isOpen && !modal.gameObject.activeSelf) modal.gameObject.SetActive(true);
            float eased = openAmount * openAmount * (3f - 2f * openAmount);
            modalGroup.alpha = eased;
            modalGroup.interactable = isOpen && openAmount > .92f;
            modalGroup.blocksRaycasts = isOpen;
            float panelScale = 1f - settings.panelScaleIntro +
                settings.panelScaleIntro * eased;
            panelFrame.localScale = new Vector3(panelScale, panelScale, 1f);
            panelFrame.anchoredPosition = new Vector2(
                settings.panelPosition.x,
                -settings.panelPosition.y - (1f - eased) * settings.panelSlide);
            if (!isOpen && openAmount <= 0 && modal.gameObject.activeSelf)
                modal.gameObject.SetActive(false);
        }

        public void Close()
        {
            if (disposed || !isOpen) return;
            isOpen = false;
            pressed = false;
            clickSound?.Invoke();
        }

        private void Toggle()
        {
            if (!available || disposed) return;
            if (isOpen) Close();
            else
            {
                isOpen = true;
                // Dedicated authored paper/book Foley. Avoid double UI clicks.
                if (openClip != null)
                    bookAudio.PlayOneShot(openClip, settings.openVolume);
                else
                    clickSound?.Invoke();
                modal.gameObject.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            if (ownedDefaults && settings != null)
                UnityEngine.Object.Destroy(settings);
        }
    }

    // Independent event relay: avoids raycasting or moving GUI every frame.
    public sealed class RokasJournalHoverInput : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private Action<bool> hover;
        private Action<bool> press;

        public void Configure(Action<bool> onHover, Action<bool> onPress)
        {
            hover = onHover;
            press = onPress;
        }

        public void OnPointerEnter(PointerEventData eventData)
        { hover?.Invoke(true); }

        public void OnPointerExit(PointerEventData eventData)
        {
            hover?.Invoke(false);
            press?.Invoke(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                press?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                press?.Invoke(false);
        }

        private void OnDisable()
        {
            hover?.Invoke(false);
            press?.Invoke(false);
        }
    }
}
