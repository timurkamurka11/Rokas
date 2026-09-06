using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public sealed class HomeView
    {
        private readonly UiKit ui;
        private readonly RokasAssets assets;
        private readonly GameSession session;
        private readonly RokasAudio audio;
        private readonly Action<string> open;
        private readonly Action<Func<bool>, string> act;
        private readonly Action<Func<bool>, string> travel;
        private readonly Action<string> toast;
        private RawImage mame;
        private Image weaponWard;
        private Text objective;
        private Text prepared;
        private float time;
        private float mameReaction;

        public HomeView(UiKit ui, RokasAssets assets, GameSession session, RokasAudio audio, Action<string> open,
            Action<Func<bool>, string> act, Action<Func<bool>, string> travel, Action<string> toast)
        {
            this.ui = ui; this.assets = assets; this.session = session; this.audio = audio;
            this.open = open; this.act = act; this.travel = travel; this.toast = toast;
        }

        public void Build(RectTransform parent)
        {
            ui.Label(parent, "HomeChapter", "МЕЖДУ ДВУМЯ МИРАМИ", 65, 138, 680, 37, 18, UiKit.Gold);
            ui.Label(parent, "HomeTitle", "С возвращением.", 61, 177, 820, 90, 52, UiKit.Paper, true);
            objective = ui.Label(parent, "HomeObjective", "", 65, 267, 820, 80, 23, UiKit.Paper);
            prepared = ui.Label(parent, "PreparationStatus", "", 1360, 137, 480, 60, 19, UiKit.Jade, false, TextAnchor.MiddleRight);
            mame = ui.Art(parent, "Mame", assets.familiar, 150, 772, 218, 218);
            weaponWard = ui.Box(parent, "WardedBlade", 1202, 344, 13, 71, UiKit.Paper);
            weaponWard.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
            ui.Box(weaponWard.transform, "WardInk", 5, 12, 3, 40, UiKit.Red);

            Hotspot(parent, "LampHotspot", 65, 650, 155, 48, () =>
                act(() => { session.SetLamp(!session.State.lampOn); return true; }, session.State.lampOn ? "За окном кто-то есть?.." : "Комната снова наполнилась теплом."));
            Hotspot(parent, "LaptopHotspot", 935, 653, 280, 55, () => open("laptop"));
            Hotspot(parent, "TeaHotspot", 644, 727, 250, 50, () => open("tea"));
            Hotspot(parent, "WorkbenchHotspot", 1184, 474, 265, 50, () => open("workbench"));
            Hotspot(parent, "MameHotspot", 132, 925, 210, 49, () =>
            {
                act(() => { session.PetMame(); return true; }, "");
                mameReaction = 1;
                audio.Play(assets.mame);
                toast(session.State.mameInteractions % 3 == 0 ? "Мамэ внимательно смотрит в пустой угол." : "Мамэ довольно щурится. Почти как обычный питомец.");
            });
            Hotspot(parent, "DoorHotspot", 1610, 658, 256, 58, () =>
            {
                if (session.State.phase == RunPhase.Accepted)
                    travel(session.LeaveHome, "Дождь. Последний переход.\nСвятилище между домами.");
                else if (session.State.phase == RunPhase.Payment)
                    toast("На ноутбук пришло подтверждение оплаты.");
                else { open("laptop"); toast("Сначала выберите контракт в YOMI."); }
            });
            Hotspot(parent, "WindowHotspot", 502, 443, 194, 48,
                () => toast("Поезд проходит без остановки. На этот раз — настоящий."));
            Refresh();
        }

        private Button Hotspot(RectTransform parent, string name, float x, float y, float w, float h, Action action)
        {
            // Reuse UiKit.Button so click audio and existing navigation stay intact, then strip the visible chrome.
            var button = ui.Button(parent, name, "", x, y, w, h, action);
            button.transition = Selectable.Transition.None;

            var background = button.targetGraphic as Image;
            if (background)
            {
                background.color = new Color(UiKit.Jade.r, UiKit.Jade.g, UiKit.Jade.b, 0f);
                background.raycastTarget = true;
            }

            foreach (Transform child in button.transform)
                child.gameObject.SetActive(false);

            var oldFeedback = button.GetComponent<InteractionFeedback>();
            if (oldFeedback) oldFeedback.enabled = false;

            button.gameObject.AddComponent<HomeHotspotFeedback>().Initialize(background, button);
            return button;
        }

        public void Refresh()
        {
            if (!objective) return;
            objective.text = session.State.phase == RunPhase.Accepted
                ? "Контракт принят. Чашка чая — и можно выходить."
                : session.State.phase == RunPhase.Payment
                    ? "Вы снова дома. В YOMI вас ждёт оплата контракта."
                    : session.State.completedRuns > 0
                        ? "За окном всё ещё дождь. Может, ещё один контракт?"
                        : "Ночной город спит. В YOMI появился первый заказ.";
            prepared.text = string.IsNullOrEmpty(session.State.preparedFoodId) ? "" : "ЗЕЛЁНЫЙ ЧАЙ  /  +5% АВТОАТАКА";
            if (weaponWard) weaponWard.gameObject.SetActive(session.State.weaponLevel >= 2);
        }

        public void Tick(float dt)
        {
            time += dt;
            mameReaction = Mathf.Max(0, mameReaction - dt);
            if (mame)
            {
                mame.rectTransform.localScale = new Vector3(1, 1 + Mathf.Sin(time * 2) * .018f, 1);
                mame.rectTransform.anchoredPosition = new Vector2(150, -772 + Mathf.Sin(mameReaction * Mathf.PI * 4) * mameReaction * 12);
            }
        }

        public void ClearReferences() { mame = null; weaponWard = null; objective = null; prepared = null; }
    }

    // HOME uses diegetic interaction: invisible hit areas only reveal a soft tint on hover/selection.
    public sealed class HomeHotspotFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Image glow;
        private Button button;
        private bool hovered;
        private bool selected;
        private bool pressed;

        public void Initialize(Image target, Button owner)
        {
            glow = target;
            button = owner;
            SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnSelect(BaseEventData e) { selected = true; }
        public void OnDeselect(BaseEventData e) { selected = false; pressed = false; }
        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) pressed = true;
        }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        private void Update()
        {
            if (!glow || !button) return;

            bool active = button.IsInteractable() && (hovered || selected);
            float targetAlpha = active ? (pressed ? .16f : selected ? .11f : .085f) : 0f;
            float factor = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            var color = glow.color;
            color.r = UiKit.Jade.r;
            color.g = UiKit.Jade.g;
            color.b = UiKit.Jade.b;
            color.a = Mathf.Lerp(color.a, targetAlpha, factor);
            glow.color = color;
        }

        private void SetAlpha(float alpha)
        {
            if (!glow) return;
            glow.color = new Color(UiKit.Jade.r, UiKit.Jade.g, UiKit.Jade.b, alpha);
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            SetAlpha(0f);
        }
    }
}
