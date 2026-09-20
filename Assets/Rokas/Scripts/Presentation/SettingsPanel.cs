using System;
using Rokas.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    public static class SettingsPanel
    {
        public static void Build(UiKit ui, RectTransform root, SettingsData settings, Action changed, Action close, Action quit)
        {
            var panel = ui.Panel(root, "SettingsPanel", 440, 150, 1040, 770);
            ui.Label(panel, "SettingsTitle", "Тишина по вашему вкусу.", 42, 24, 940, 84, 38, UiKit.Paper, true);
            ui.Label(panel, "PauseHint", "Игра на паузе", 45, 103, 600, 30, 19, UiKit.Muted);
            Volume(ui, panel, "Общая громкость", 177, () => settings.masterVolume, value => settings.masterVolume = value, changed);
            Volume(ui, panel, "Музыка", 252, () => settings.musicVolume, value => settings.musicVolume = value, changed);
            Volume(ui, panel, "Звуки", 327, () => settings.sfxVolume, value => settings.sfxVolume = value, changed);
            Toggle(ui, panel, "Дрожание камеры", 415, () => settings.screenShake, value => settings.screenShake = value, changed);
            Toggle(ui, panel, "Числа урона", 481, () => settings.damageNumbers, value => settings.damageNumbers = value, changed);
            Toggle(ui, panel, "Полный экран без рамки", 547, () => settings.fullscreen, value => settings.fullscreen = value, changed);
            ui.Label(panel, "GlitchTitle", "Искажение реальности", 45, 611, 480, 46, 22);
            Text glitchLabel = null;
            var glitchButton = ui.Button(panel, "GlitchIntensity", settings.glitchIntensity < .5f ? "Низкое" : settings.glitchIntensity > 1.1f ? "Высокое" : "Обычное",
                600, 611, 390, 48, () =>
                {
                    settings.glitchIntensity = settings.glitchIntensity < .5f ? 1 : settings.glitchIntensity > 1.1f ? .15f : 1.5f;
                    glitchLabel.text = settings.glitchIntensity < .5f ? "Низкое" : settings.glitchIntensity > 1.1f ? "Высокое" : "Обычное";
                    changed();
                });
            glitchLabel = glitchButton.GetComponentInChildren<Text>();
            ui.Button(panel, "CloseSettings", "Вернуться в игру", 45, 689, 510, 57, close, true);
            ui.Button(panel, "QuitGame", "Сохранить и выйти", 600, 689, 390, 57, quit);
        }

        private static void Volume(UiKit ui, Transform panel, string name, float y, Func<float> read, Action<float> write, Action changed)
        {
            ui.Label(panel, name, name, 45, y, 480, 47, 22);
            var value = ui.Label(panel, name + "Value", Mathf.RoundToInt(read() * 100) + "%", 717, y, 120, 47, 22, UiKit.Jade, false, TextAnchor.MiddleCenter);
            ui.Button(panel, name + "Down", "−", 602, y, 80, 48, () => { write(Mathf.Clamp01(read() - .1f)); value.text = Mathf.RoundToInt(read() * 100) + "%"; changed(); });
            ui.Button(panel, name + "Up", "+", 908, y, 80, 48, () => { write(Mathf.Clamp01(read() + .1f)); value.text = Mathf.RoundToInt(read() * 100) + "%"; changed(); });
        }

        private static void Toggle(UiKit ui, Transform panel, string name, float y, Func<bool> read, Action<bool> write, Action changed)
        {
            ui.Label(panel, name, name, 45, y, 500, 46, 22);
            Text text = null;
            var button = ui.Button(panel, name + "Toggle", read() ? "Включено" : "Выключено", 600, y, 390, 48,
                () => { write(!read()); text.text = read() ? "Включено" : "Выключено"; changed(); });
            text = button.GetComponentInChildren<Text>();
        }
    }
}
