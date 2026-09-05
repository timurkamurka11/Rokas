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
            if (kind == "laptop") Laptop(panel);
            else if (kind == "tea") Tea(panel);
            else Workbench(panel);
        }

        private void Laptop(RectTransform panel)
        {
            var contract = session.Contract;
            ui.Label(panel, "Network", "Y O M I    /    ЗАКРЫТАЯ СЕТЬ", 45, 31, 900, 47, 24, UiKit.Jade);
            ui.Label(panel, "Connection", "СОЕДИНЕНИЕ ЗАЩИЩЕНО     •     ОПЕРАТОР 07", 46, 91, 1080, 36, 16, UiKit.Muted);
            ui.Box(panel, "Divider", 45, 144, 1170, 1, new Color(.3f, .4f, .38f));
            if (session.State.phase == RunPhase.Payment)
            {
                ui.Label(panel, "PaymentHeading", "Контракт закрыт.", 45, 178, 1100, 85, 44, UiKit.Paper, true);
                ui.Label(panel, "PaymentTitle", contract.title, 47, 270, 1090, 50, 27, UiKit.Jade);
                ui.Label(panel, "PaymentLines", "БАЗОВАЯ ОПЛАТА                         ¥ " + contract.reward.ToString("N0")
                    + "\nДУХОВНЫЙ ПЕПЕЛ                          +" + contract.ashReward
                    + "\nРЕПУТАЦИЯ ОХОТНИКА                    +" + contract.reputationReward,
                    47, 343, 1090, 176, 25);
                ui.Button(panel, "ClaimPayment", "Получить оплату", 47, 572, 470, 66,
                    () => { act(session.ClaimPayment, "Оплата получена. Хорошая работа, охотник."); refresh(); }, true);
                return;
            }
            ui.Label(panel, "ContractRank", "E", 48, 184, 100, 100, 65, UiKit.Gold, true);
            ui.Label(panel, "ContractTitle", contract.title, 171, 184, 1000, 70, 43, UiKit.Paper, true);
            ui.Label(panel, "ContractLocation", contract.location, 174, 268, 960, 45, 24, UiKit.Jade);
            ui.Label(panel, "ClientNote", contract.clientNote, 47, 337, 1150, 126, 25);
            ui.Label(panel, "ContractReward", "ОПЛАТА   ¥ " + contract.reward.ToString("N0") + "     /     РЕПУТАЦИЯ  +" + contract.reputationReward
                + "\nОБЪЕКТ   Безликий пассажир     /     ПЕЧАТЬ НА ГРУДИ — УЯЗВИМОСТЬ", 47, 464, 1140, 72, 19, UiKit.Gold);
            if (session.State.phase == RunPhase.Home)
                ui.Button(panel, "AcceptContract", "Принять контракт", 47, 572, 470, 66,
                    () => { act(session.AcceptContract, "Контракт принят. Подготовьтесь и выходите."); refresh(); }, true);
            else
            {
                ui.Button(panel, "ContractAccepted", "Принят  /  К выходу", 47, 572, 470, 66, close, true);
                ui.Button(panel, "CancelContract", "Отменить контракт", 760, 572, 430, 66,
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
            ui.Button(panel, "PrepareTea", ready ? "Чай уже приготовлен" : "Заварить  /  ¥ 80", 47, 572, 580, 66,
                () => { act(() => session.PrepareFood("food_green_tea"), "Чай готов. Дышать стало чуть легче."); refresh(); }, true, !ready && session.State.yen >= 80);
            if (!ready && session.State.yen < 80) ui.Label(panel, "TeaFunds", "Недостаточно иен", 660, 573, 530, 66, 22, UiKit.Gold);
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
