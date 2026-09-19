using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerTextFontResolver
    {
        internal const string DefaultTmpFontPath = "Assets/Rokas/Resources/RokasSans TMP.asset";
        internal const string DefaultSourceFontPath = "Assets/Rokas/Art/UI/Fonts/RokasSans.ttf";

        internal static string GetDefaultFontAssetGuid()
        {
            TMP_FontAsset asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTmpFontPath);
            return asset != null ? AssetDatabase.AssetPathToGUID(DefaultTmpFontPath) ?? string.Empty : string.Empty;
        }

        internal static UnityEngine.Object ResolveAsset(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadMainAssetAtPath(path);
        }

        internal static bool IsSupportedAsset(UnityEngine.Object asset)
        {
            return asset == null || asset is TMP_FontAsset || asset is Font;
        }

        internal static string GetAssetGuid(UnityEngine.Object asset)
        {
            if (asset == null) return string.Empty;
            if (!IsSupportedAsset(asset))
                throw new ArgumentException("Scene text font must be a TMP Font Asset or project Font.", nameof(asset));
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Scene text font must be stored inside the Unity project.", nameof(asset));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("Scene text font does not have a stable Unity asset GUID.", nameof(asset));
            return guid;
        }

        internal static bool TryResolvePreviewFont(string guid, out Font font, out string warning)
        {
            font = null;
            if (string.IsNullOrWhiteSpace(guid))
            {
                warning = "шрифт не выбран. Элемент сохранён, но не отображается.";
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                warning = "файл шрифта не найден. Элемент сохранён, но не отображается.";
                return false;
            }

            TMP_FontAsset tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (tmp != null)
            {
                font = tmp.sourceFontFile;
                if (font == null &&
                    string.Equals(path, DefaultTmpFontPath, StringComparison.Ordinal))
                {
                    font = AssetDatabase.LoadAssetAtPath<Font>(DefaultSourceFontPath);
                    if (font != null)
                    {
                        warning =
                            "RokasSans TMP не хранит sourceFontFile; предпросмотр использует явно связанную " +
                            "проектную копию RokasSans.ttf.";
                        return true;
                    }
                }
                if (font == null)
                {
                    warning = "TMP Font Asset не содержит исходный Font для предпросмотра. Элемент сохранён.";
                    return false;
                }
                warning = string.Empty;
                return true;
            }

            font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null)
            {
                warning = "Используется исходный Font «" + font.name +
                          "». Для полного TMP-пайплайна создайте TMP Font Asset из этого файла.";
                return true;
            }

            warning = "выбранный ресурс не является TMP Font Asset или Font. Элемент сохранён, но не отображается.";
            return false;
        }

        internal static string GetDisplayName(UnityEngine.Object asset)
        {
            return asset != null ? asset.name ?? string.Empty : string.Empty;
        }

        internal static string DescribeFallbacks(UnityEngine.Object asset)
        {
            TMP_FontAsset tmp = asset as TMP_FontAsset;
            if (tmp == null) return string.Empty;
            int count = tmp.fallbackFontAssetTable != null ? tmp.fallbackFontAssetTable.Count : 0;
            return count > 0
                ? "TMP fallback: " + count + " font asset(s)."
                : "TMP fallback-chain для этого шрифта не настроена.";
        }
    }
}
