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
        private readonly CanvasGroup modalGroup;
        private readonly RectTransform modal;
        private readonly RectTransform panelFrame;
        private readonly RawImage closedBook;
        private readonly RawImage openBook;
        private readonly RawImage hoverLight;
        private readonly Image selectedRow;
        private readonly Action clickSound;
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
            Texture2D closed = Resources.Load<Texture2D>(ClosedPath);
            Texture2D opened = Resources.Load<Texture2D>(OpenPath);
            Texture2D background = Resources.Load<Texture2D>(PanelPath);
            HasArt = closed != null && opened != null && background != null;

            root = ui.Rect(stage, "ROKAS_Journal_HUD_StageOverlay", 0, 0, 1920, 1080);
            // Above room hotspots; below laptop/settings, transition and VN dialogue.
            root.SetSiblingIndex(panelLayer.GetSiblingIndex());
            root.gameObject.SetActive(false);

            // Modal is first child so the journal icon stays above the dimmer.
            modal = ui.Rect(root, "Journal_Modal", 0, 0, 1920, 1080);
            modalGroup = modal.gameObject.AddComponent<CanvasGroup>();
            modalGroup.alpha = 0;
            modalGroup.interactable = false;
            modalGroup.blocksRaycasts = false;
            modal.gameObject.SetActive(false);

            Image shade = ui.Box(modal, "Journal_Backdrop_ClickToClose",
                0, 0, 1920, 1080, new Color(.005f, .014f, .027f, .58f), true);
            var shadeButton = shade.gameObject.AddComponent<Button>();
            shadeButton.targetGraphic = shade;
            shadeButton.transition = Selectable.Transition.None;
            shadeButton.navigation = new Navigation { mode = Navigation.Mode.None };
            shadeButton.onClick.AddListener(Close);

            // Original authored image is 1448 x 1086 (4:3), preserved at 1200x900.
            panelFrame = ui.Rect(modal, "Journal_Panel_Frame", 360, 90, 1200, 900);
            RawImage art = ui.Art(panelFrame, "Journal_AuthoredPanel_NoText",
                background, 0, 0, 1200, 900);
            art.raycastTarget = false;

            // Block backdrop clicks through unused areas of the journal.
            Image panelHit = ui.Box(panelFrame, "Journal_Panel_ClickSurface",
                0, 0, 1200, 900, new Color(1f, 1f, 1f, .001f), true);
            panelHit.raycastTarget = true;

            // Six transparent hit targets aligned to the original left-hand
            // empty rows. Their own titles/content will be supplied later.
            selectedRow = ui.Box(panelFrame, "Journal_SelectedRow_GentleAmber",
                58, 332, 238, 58, new Color(1f, .62f, .19f, .12f));
            selectedRow.raycastTarget = false;
            for (int i = 0; i < 6; i++)
            {
                int row = i;
                Image hit = ui.Box(panelFrame, "Journal_NavSlot_" + row,
                    49, 330 + row * 75, 255, 65, new Color(1f, 1f, 1f, .001f), true);
                Button tab = hit.gameObject.AddComponent<Button>();
                tab.targetGraphic = hit;
                tab.transition = Selectable.Transition.None;
                tab.navigation = new Navigation { mode = Navigation.Mode.None };
                tab.onClick.AddListener(() =>
                {
                    clickSound?.Invoke();
                    selectedRow.rectTransform.anchoredPosition =
                        new Vector2(58f, -(332f + row * 75f));
                });
            }

            // Clicking the book is a separate hit area from the artwork.
            Image hitSurface = ui.Box(root, "Journal_Icon_HitArea",
                34, 26, 166, 166, new Color(1f, 1f, 1f, .001f), true);
            launcher = hitSurface.rectTransform;
            Button opener = hitSurface.gameObject.AddComponent<Button>();
            opener.targetGraphic = hitSurface;
            opener.transition = Selectable.Transition.None;
            opener.navigation = new Navigation { mode = Navigation.Mode.None };
            opener.onClick.AddListener(Toggle);
            var pointer = hitSurface.gameObject.AddComponent<RokasJournalHoverInput>();
            pointer.Configure(value => hovered = value, value => pressed = value);
            hoverLight = ui.Art(launcher, "Journal_HoverSoftLight", opened,
                -5, -6, 176, 176);
            closedBook = ui.Art(launcher, "Journal_ClosedBook", closed,
                0, 0, 166, 166);
            openBook = ui.Art(launcher, "Journal_OpenBook_OnHover", opened,
                0, 0, 166, 166);
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
                1f - Mathf.Exp(-13f * dt));
            if (Mathf.Abs(hoverAmount - hoverTarget) < .001f)
                hoverAmount = hoverTarget;

            float bookScale = 1f + .072f * hoverAmount - (pressed ? .045f : 0f);
            launcher.localScale = new Vector3(bookScale, bookScale, 1f);
            launcher.anchoredPosition = new Vector2(34f, -26f + 5f * hoverAmount);
            closedBook.color = new Color(1f, 1f, 1f, 1f - hoverAmount);
            openBook.color = new Color(1f, 1f, 1f, hoverAmount);
            hoverLight.color = new Color(1f, .74f, .37f, .19f * hoverAmount);

            float openTarget = isOpen ? 1f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, openTarget, dt * 5f);
            if (isOpen && !modal.gameObject.activeSelf) modal.gameObject.SetActive(true);
            float eased = openAmount * openAmount * (3f - 2f * openAmount);
            modalGroup.alpha = eased;
            modalGroup.interactable = isOpen && openAmount > .92f;
            modalGroup.blocksRaycasts = isOpen;
            float panelScale = .965f + .035f * eased;
            panelFrame.localScale = new Vector3(panelScale, panelScale, 1f);
            panelFrame.anchoredPosition = new Vector2(360f, -90f - (1f - eased) * 18f);
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
                clickSound?.Invoke();
                modal.gameObject.SetActive(true);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
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
