using System;
using System.Runtime.CompilerServices;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnWorkshopPreviewSampleStore
    {
        public const string DefaultText = "Привет, Мина! Это пробный текст VN Workshop.\nМожно вставить свой текст или загрузить UTF-8 TXT.";

        private sealed class Holder
        {
            public string Value;
        }

        private static readonly ConditionalWeakTable<VnPresentationWorkshopPreset, Holder> Values =
            new ConditionalWeakTable<VnPresentationWorkshopPreset, Holder>();

        public static string Get(VnPresentationWorkshopPreset preset, string fallback = DefaultText)
        {
            if (preset == null) return fallback ?? string.Empty;
            Holder holder;
            return Values.TryGetValue(preset, out holder) ? holder.Value ?? string.Empty : fallback ?? string.Empty;
        }

        public static void Set(VnPresentationWorkshopPreset preset, string value)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            Holder holder = Values.GetOrCreateValue(preset);
            holder.Value = value ?? string.Empty;
        }
    }
}
