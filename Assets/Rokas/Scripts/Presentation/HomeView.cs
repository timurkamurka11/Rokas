using System;
using Rokas.Core;
using UnityEngine;
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

            ui.Button(parent, "LampHotspot", "Свет", 65, 650, 155, 48, () =>
                act(() => { session.SetLamp(!session.State.lampOn); return true; }, session.State.lampOn ? "За окном кто-то есть?.." : "Комната снова наполнилась теплом."));
            ui.Button(parent, "LaptopHotspot", "YOMI  /  Ноутбук", 935, 653, 280, 55, () => open("laptop"), true);
            ui.Button(parent, "TeaHotspot", "Заварить чай", 644, 727, 250, 50, () => open("tea"));
            ui.Button(parent, "WorkbenchHotspot", "Снаряжение", 1184, 474, 265, 50, () => open("workbench"));
            ui.Button(parent, "MameHotspot", "Мамэ", 132, 925, 210, 49, () =>
            {
                act(() => { session.PetMame(); return true; }, "");
                mameReaction = 1;
                audio.Play(assets.mame);
                toast(session.State.mameInteractions % 3 == 0 ? "Мамэ внимательно смотрит в пустой угол." : "Мамэ довольно щурится. Почти как обычный питомец.");
            });
            ui.Button(parent, "DoorHotspot", "Выйти из дома", 1610, 658, 256, 58, () =>
            {
                if (session.State.phase == RunPhase.Accepted)
                    travel(session.LeaveHome, "Дождь. Последний переход.\nСвятилище между домами.");
                else if (session.State.phase == RunPhase.Payment)
                    toast("На ноутбук пришло подтверждение оплаты.");
                else { open("laptop"); toast("Сначала выберите контракт в YOMI."); }
            }, true);
            ui.Button(parent, "WindowHotspot", "За окном", 502, 443, 194, 48,
                () => toast("Поезд проходит без остановки. На этот раз — настоящий."));
            Refresh();
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
}
