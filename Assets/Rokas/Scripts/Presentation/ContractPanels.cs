using System;
using Rokas.Core;
using UnityEngine;

namespace Rokas.Presentation
{
    public sealed class ContractPanels
    {
        private readonly UiKit ui;
        private readonly GameSession session;
        private readonly Action<Func<bool>, string> act;
        private readonly Action refresh;
        private readonly Action<Func<bool>, string> travel;
        private readonly Action close;

        public ContractPanels(UiKit ui, GameSession session, Action<Func<bool>, string> act, Action refresh,
            Action<Func<bool>, string> travel, Action close)
        { this.ui = ui; this.session = session; this.act = act; this.refresh = refresh; this.travel = travel; this.close = close; }

        public void Build(RectTransform root, string kind)
        {
            var panel = ui.Panel(root, "InWorldPanel", 330, 180, 1260, 700);
            ui.Button(panel, "ClosePanel", "Закрыть  ×", 1025, 27, 202, 48, close);
            if (kind == "tea") Tea(panel);
            else Workbench(panel);
        }

        public void BuildLaptopPage(RectTransform panel, string kind)
        {
            if (kind == "contracts") Laptop(panel);
            else if (kind == "tea") Tea(panel);
            else Workbench(panel);
        }

        private void Laptop(RectTransform panel)
        {
            var contract = session.Contract;
            ui.Label(panel, "ContractEyebrow", "ДОСКА КОНТРАКТОВ", 48, 30, 900, 40, 17, UiKit.Muted);
            ui.Label(panel, "ContractRank", "РАНГ  E", 1010, 30, 200, 42, 20, UiKit.Gold, false, TextAnchor.MiddleRight);
            if (session.State.phase == RunPhase.Payment)
            {
                ui.Label(panel, "PaymentHeading", "Контракт закрыт", 48, 120, 1150, 80, 43);
                ui.Label(panel, "PaymentTitle", contract.title, 48, 217, 1150, 58, 28, UiKit.Jade);
                ui.Label(panel, "PaymentLines", "Оплата    ¥ " + contract.reward.ToString("N0")
                    + "\nДуховный пепел    +" + contract.ashReward
                    + "\nРепутация    +" + contract.reputationReward, 48, 316, 1150, 156, 27);
                ui.Box(panel, "PaymentRule", 48, 505, 1164, 1, new Color(.3f, .4f, .44f, .45f));
                ui.Button(panel, "ClaimPayment", "Получить оплату", 48, 552, 470, 66,
                    () => { act(session.ClaimPayment, "Оплата получена. Хорошая работа, охотник."); refresh(); }, true);
                return;
            }
            ui.Label(panel, "ContractTitle", contract.title, 48, 115, 1150, 88, 43);
            ui.Label(panel, "ContractLocation", contract.location, 50, 208, 1130, 54, 25, UiKit.Jade);
            ui.Label(panel, "ClientNote", contract.clientNote, 50, 306, 790, 145, 25);
            ui.Label(panel, "RewardCaption", "НАГРАДА", 910, 299, 290, 42, 16, UiKit.Muted);
            ui.Label(panel, "ContractReward", "¥ " + contract.reward.ToString("N0"), 907, 348, 300, 62, 38, UiKit.Gold);
            ui.Label(panel, "ReputationReward", "+" + contract.reputationReward + " к репутации", 911, 414, 290, 44, 20, UiKit.Muted);
            ui.Box(panel, "ContractRule", 48, 505, 1164, 1, new Color(.3f, .4f, .44f, .45f));
            if (session.State.phase == RunPhase.Home)
                ui.Button(panel, "AcceptContract", "Принять контракт", 48, 552, 470, 66,
                    () => { act(session.AcceptContract, "Контракт принят. Подготовьтесь и выходите."); refresh(); }, true);
            else
            {
                ui.Button(panel, "ContractAccepted", "Принят  /  К выходу", 48, 552, 470, 66, close, true);
                ui.Button(panel, "CancelContract", "Отменить контракт", 776, 552, 430, 66,
                    () => { act(session.ReturnHome, "Контракт отменён."); refresh(); });
            }
        }

        private void Tea(RectTransform panel)
        {
            ui.Label(panel, "TeaEyebrow", "ПОДГОТОВКА  /  МАЛЕНЬКИЙ РИТУАЛ", 45, 33, 925, 49, 21, UiKit.Gold);
            ui.Label(panel, "TeaTitle", "Перед другой стороной.", 45, 172, 1140, 90, 46, UiKit.Paper, true);
            ui.Label(panel, "TeaDescription", "Зелёный чай\nТёплая чашка, чтобы руки не дрожали.\n\n+5% к скорости автоматических атак на ближайшем задании.\nЭффект закончится, когда вы вернётесь домой.",
                47, 284, 1130, 226, 27);
            bool ready = !string.IsNullOrEmpty(session.State.preparedFoodId);
            bool atHome = session.State.phase == RunPhase.Home || session.State.phase == RunPhase.Accepted;
            ui.Button(panel, "PrepareTea", ready ? "Чай уже приготовлен" : "Заварить  /  ¥ 80", 47, 572, 580, 66,
                () => { act(() => session.PrepareFood("food_green_tea"), "Чай готов. Дышать стало чуть легче."); refresh(); }, true, atHome && !ready && session.State.yen >= FoodService.GreenTeaCost);
            if (!atHome) ui.Label(panel, "TeaUnavailable", "Сначала получите оплату за контракт", 660, 552, 535, 88, 21, UiKit.Gold);
            else if (!ready && session.State.yen < FoodService.GreenTeaCost) ui.Label(panel, "TeaFunds", "Недостаточно иен", 660, 573, 530, 66, 22, UiKit.Gold);
        }

        private void Workbench(RectTransform panel)
        {
            int level = session.State.weaponLevel;
            long cost = 300L * level;
            ui.Label(panel, "WorkbenchEyebrow", "ВЕРСТАК  /  РИТУАЛЬНОЕ СНАРЯЖЕНИЕ", 45, 33, 925, 49, 21, UiKit.Gold);
            ui.Label(panel, "WeaponTitle", level < 2 ? "Сталь без имени." : "Клинок под защитой.", 45, 172, 1140, 90, 46, UiKit.Paper, true);
            ui.Label(panel, "WeaponStats", "УРОВЕНЬ  " + level + "\n\nРучные и автоматические атаки: +" + ((level - 1) * 20) + "% урона.\nСледующее улучшение: ещё +20% к базовому урону.\nНа втором уровне на клинке появляется защитная печать.",
                47, 278, 1130, 236, 26);
            ui.Button(panel, "UpgradeWeapon", "Улучшить  /  ¥ " + cost.ToString("N0"), 47, 572, 580, 66,
                () => { act(session.UpgradeWeapon, "Клинок стал сильнее. Печать тихо шуршит."); refresh(); }, true,
                session.State.phase == RunPhase.Home && session.State.yen >= cost && level < SaveData.MaxWeaponLevel);
            if (session.State.phase != RunPhase.Home)
                ui.Label(panel, "WorkbenchLocked", "Обслуживание — между контрактами.\nТекущий заказ можно отменить в YOMI.", 660, 558, 535, 108, 21, UiKit.Gold);
        }
    }
}
