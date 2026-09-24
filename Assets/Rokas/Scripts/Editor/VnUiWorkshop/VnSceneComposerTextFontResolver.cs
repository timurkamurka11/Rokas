using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
#if UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerInstalledFontFace
    {
        public string DisplayName { get; internal set; }
        public string SourcePath { get; internal set; }
        public string Identity { get; internal set; }
    }

    public sealed class VnSceneComposerFontImportResult
    {
        public bool Success { get; internal set; }
        public string Error { get; internal set; }
        public string SourceAssetPath { get; internal set; }
        public string TmpFontAssetPath { get; internal set; }
        public string TmpFontAssetGuid { get; internal set; }
        public string Identity { get; internal set; }
    }

    internal static class VnSceneComposerTextFontResolver
    {
        internal const string DefaultTmpFontPath = "Assets/Rokas/Resources/RokasSans TMP.asset";
        internal const string DefaultSourceFontPath = "Assets/Rokas/Art/UI/Fonts/RokasSans.ttf";
        internal const string ImportedFontFolder = "Assets/Rokas/Art/UI/Fonts/Imported";
        internal const string ImportedTmpFolder = "Assets/Rokas/Resources/Fonts/TMP";

        private static VnSceneComposerInstalledFontFace[] _installedWindowsFonts;

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
                throw new ArgumentException("Dialogue font must be a TMP Font Asset or project Font.", nameof(asset));
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Dialogue font must be stored inside the Unity project.", nameof(asset));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("Dialogue font does not have a stable Unity asset GUID.", nameof(asset));
            return guid;
        }

        internal static bool TryResolvePreviewFont(string guid, out Font font, out string warning)
        {
            font = null;
            if (string.IsNullOrWhiteSpace(guid))
            {
                warning = string.Empty;
                return false;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                warning = "font asset не найден; сохранена выбранная ссылка, используется RokasSans.";
                return false;
            }

            TMP_FontAsset tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (tmp != null)
            {
                font = tmp.sourceFontFile;
                if (font == null && string.Equals(path, DefaultTmpFontPath, StringComparison.Ordinal))
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
                    warning = "TMP font asset не содержит исходный project Font; используется RokasSans.";
                    return false;
                }

                warning = string.Empty;
                return true;
            }

            font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font != null)
            {
                warning = "Используется project Font «" + font.name + "».";
                return true;
            }

            warning = "выбранный project font asset имеет неподдерживаемый тип; используется RokasSans.";
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

        internal static VnSceneComposerInstalledFontFace[] GetInstalledWindowsFonts()
        {
            if (_installedWindowsFonts != null) return _installedWindowsFonts;
            var faces = new List<VnSceneComposerInstalledFontFace>();
#if UNITY_EDITOR_WIN
            CollectRegistryFonts(Registry.LocalMachine,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", faces);
            CollectRegistryFonts(Registry.CurrentUser,
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", faces);
#endif
            _installedWindowsFonts = faces
                .Where(f => f != null && File.Exists(f.SourcePath))
                .GroupBy(f => f.Identity, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(f => f.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return _installedWindowsFonts;
        }

        internal static void RefreshInstalledWindowsFonts()
        {
            _installedWindowsFonts = null;
        }

#if UNITY_EDITOR_WIN
        private static void CollectRegistryFonts(RegistryKey root, string keyPath,
            List<VnSceneComposerInstalledFontFace> faces)
        {
            try
            {
                using (RegistryKey key = root.OpenSubKey(keyPath))
                {
                    if (key == null) return;
                    foreach (string valueName in key.GetValueNames())
                    {
                        string rawPath = key.GetValue(valueName) as string;
                        if (string.IsNullOrWhiteSpace(rawPath)) continue;
                        string sourcePath = ResolveWindowsFontPath(rawPath);
                        if (!IsSupportedSourceFile(sourcePath) || !File.Exists(sourcePath)) continue;
                        string displayName = NormalizeWindowsDisplayName(valueName, sourcePath);
                        string hash = ComputeFileHash(sourcePath);
                        faces.Add(new VnSceneComposerInstalledFontFace
                        {
                            DisplayName = displayName,
                            SourcePath = sourcePath,
                            Identity = BuildStableIdentity(displayName, hash)
                        });
                    }
                }
            }
            catch
            {
                // Registry availability differs by machine. One inaccessible hive must not break authoring.
            }
        }

        private static string ResolveWindowsFontPath(string value)
        {
            string expanded = Environment.ExpandEnvironmentVariables(value.Trim());
            if (Path.IsPathRooted(expanded)) return Path.GetFullPath(expanded);

            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string systemFont = Path.Combine(windows, "Fonts", expanded);
            if (File.Exists(systemFont)) return Path.GetFullPath(systemFont);

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.GetFullPath(Path.Combine(local, "Microsoft", "Windows", "Fonts", expanded));
        }

        private static string NormalizeWindowsDisplayName(string registryName, string sourcePath)
        {
            string name = (registryName ?? string.Empty).Trim();
            string[] suffixes = { " (TrueType)", " (OpenType)", " (All res)" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (name.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - suffixes[i].Length).Trim();
            }
            return string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(sourcePath) : name;
        }
#endif

        internal static VnSceneComposerFontImportResult ImportWindowsFont(VnSceneComposerInstalledFontFace face)
        {
            if (face == null)
                return Failed("Installed Windows font entry is missing.");
            return ImportProjectFont(face.SourcePath, face.DisplayName);
        }

        internal static VnSceneComposerFontImportResult ImportProjectFont(string sourcePath, string displayName)
        {
            try
            {
                string absoluteSource = ResolveSourcePath(sourcePath);
                if (!File.Exists(absoluteSource))
                    return Failed("Font source file does not exist: " + sourcePath);
                if (!IsSupportedSourceFile(absoluteSource))
                    return Failed("Only .ttf and .otf font files are supported.");

                string hash = ComputeFileHash(absoluteSource);
                string label = string.IsNullOrWhiteSpace(displayName)
                    ? Path.GetFileNameWithoutExtension(absoluteSource)
                    : displayName.Trim();
                string identity = BuildStableIdentity(label, hash);
                string safeName = SanitizeFileName(label);
                string shortHash = hash.Substring(0, Math.Min(12, hash.Length)).ToLowerInvariant();
                string sourceAssetPath = ImportedFontFolder + "/" + safeName + "-" + shortHash +
                                         Path.GetExtension(absoluteSource).ToLowerInvariant();
                string tmpAssetPath = ImportedTmpFolder + "/" + safeName + "-" + shortHash + " SDF.asset";

                EnsureAssetFolder(ImportedFontFolder);
                EnsureAssetFolder(ImportedTmpFolder);

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string absoluteTarget = Path.GetFullPath(Path.Combine(projectRoot,
                    sourceAssetPath.Replace('/', Path.DirectorySeparatorChar)));

                if (!File.Exists(absoluteTarget))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(absoluteTarget));
                    File.Copy(absoluteSource, absoluteTarget, false);
                }
                else if (!string.Equals(ComputeFileHash(absoluteTarget), hash, StringComparison.OrdinalIgnoreCase))
                {
                    return Failed("A different font already occupies deterministic import path: " + sourceAssetPath);
                }

                AssetDatabase.ImportAsset(sourceAssetPath, ImportAssetOptions.ForceSynchronousImport);
                Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceAssetPath);
                if (sourceFont == null)
                    return Failed("Unity could not import font source: " + sourceAssetPath);

                TMP_FontAsset tmp = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
                if (tmp == null)
                {
                    tmp = TMP_FontAsset.CreateFontAsset(sourceFont);
                    if (tmp == null) return Failed("TextMeshPro could not create a font asset.");
                    tmp.name = safeName + "-" + shortHash + " SDF";
                    tmp.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                    AssetDatabase.CreateAsset(tmp, tmpAssetPath);
                    PersistTmpSubAssets(tmp, tmpAssetPath);
                    EditorUtility.SetDirty(tmp);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(tmpAssetPath, ImportAssetOptions.ForceSynchronousImport);
                }

                string guid = AssetDatabase.AssetPathToGUID(tmpAssetPath);
                if (string.IsNullOrWhiteSpace(guid))
                    return Failed("Imported TMP Font Asset does not have a stable project GUID.");

                return new VnSceneComposerFontImportResult
                {
                    Success = true,
                    Error = string.Empty,
                    SourceAssetPath = sourceAssetPath,
                    TmpFontAssetPath = tmpAssetPath,
                    TmpFontAssetGuid = guid,
                    Identity = identity
                };
            }
            catch (Exception exception)
            {
                return Failed("Font import failed: " + exception.Message);
            }
        }

        internal static bool ConfigureFallback(string primaryGuid, string fallbackGuid, out string error)
        {
            error = string.Empty;
            TMP_FontAsset primary = ResolveAsset(primaryGuid) as TMP_FontAsset;
            TMP_FontAsset fallback = ResolveAsset(fallbackGuid) as TMP_FontAsset;
            if (primary == null)
            {
                error = "Primary TMP font asset is missing.";
                return false;
            }
            if (fallback == null)
            {
                error = "Fallback TMP font asset is missing.";
                return false;
            }
            if (ReferenceEquals(primary, fallback)) return true;
            if (primary.fallbackFontAssetTable == null)
                primary.fallbackFontAssetTable = new List<TMP_FontAsset>();
            if (!primary.fallbackFontAssetTable.Contains(fallback))
            {
                primary.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(primary);
                AssetDatabase.SaveAssets();
            }
            return true;
        }

        internal static string BuildStableIdentity(string displayName, string sourceHash)
        {
            return (displayName ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                   (sourceHash ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string ResolveSourcePath(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return string.Empty;
            if (Path.IsPathRooted(sourcePath)) return Path.GetFullPath(sourcePath);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, sourcePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static bool IsSupportedSourceFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeFileHash(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string SanitizeFileName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "ImportedFont" : value.Trim();
            var builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') builder.Append(c);
                else if (char.IsWhiteSpace(c)) builder.Append('-');
            }
            string result = builder.ToString().Trim('-');
            return string.IsNullOrEmpty(result) ? "ImportedFont" : result;
        }

        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void PersistTmpSubAssets(TMP_FontAsset asset, string assetPath)
        {
            if (asset == null) return;
            if (asset.material != null && !AssetDatabase.Contains(asset.material))
                AssetDatabase.AddObjectToAsset(asset.material, assetPath);
            Texture2D[] atlases = asset.atlasTextures;
            if (atlases == null) return;
            for (int i = 0; i < atlases.Length; i++)
            {
                Texture2D atlas = atlases[i];
                if (atlas != null && !AssetDatabase.Contains(atlas))
                    AssetDatabase.AddObjectToAsset(atlas, assetPath);
            }
        }

        private static VnSceneComposerFontImportResult Failed(string error)
        {
            return new VnSceneComposerFontImportResult
            {
                Success = false,
                Error = error ?? "Unknown font import error.",
                SourceAssetPath = string.Empty,
                TmpFontAssetPath = string.Empty,
                TmpFontAssetGuid = string.Empty,
                Identity = string.Empty
            };
        }
    }
}
