#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Rokas.Editor
{
    // Temporary CI-only authoring utility. It asks the installed uGUI package to import
    // its own TMP essentials, then uses Unity serialization to author the ROKAS font.
    [InitializeOnLoad]
    internal static class RokasTmpAssetGenerator
    {
        private const string CanonicalSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string FontPath = "Assets/Rokas/Resources/RokasSans TMP.asset";
        private const string SourceFontPath = "Assets/Rokas/Art/UI/Fonts/RokasSans.ttf";
        private const string RussianAlphabet = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        private const string CyrillicProbe = RussianAlphabet + " ПОИСК Имя или роль Сообщения Назад В сети Не в сети";

        static RokasTmpAssetGenerator()
        {
            if (Application.isBatchMode)
            {
                EditorApplication.delayCall += GenerateAndExport;
            }
        }

        private static void GenerateAndExport()
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(CanonicalSettingsPath);
            if (settings == null)
            {
                Debug.Log("ROKAS TMP authoring: importing official TMP Essential Resources from com.unity.ugui.");
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorApplication.delayCall += GenerateAndExport;
                return;
            }

            Shader sdfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (sdfShader == null)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                sdfShader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                if (sdfShader == null)
                {
                    throw new InvalidOperationException("Official TMP Essentials were imported, but the TextMeshPro/Mobile/Distance Field shader is unavailable.");
                }
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null || font.atlasPopulationMode != AtlasPopulationMode.Static || font.atlasWidth != 512 || !HasRequiredCharacters(font))
            {
                if (font != null)
                {
                    AssetDatabase.DeleteAsset(FontPath);
                }
                font = GenerateProductionFont();
            }

            SerializedObject settingsSerialized = new SerializedObject(settings);
            SetString(settingsSerialized, "assetVersion", "2");
            SetBool(settingsSerialized, "m_ClearDynamicDataOnBuild", false);
            SetObject(settingsSerialized, "m_defaultFontAsset", font);
            SetString(settingsSerialized, "m_defaultFontAssetPath", string.Empty);
            SerializedProperty fallbacks = settingsSerialized.FindProperty("m_fallbackFontAssets");
            if (fallbacks != null)
            {
                fallbacks.arraySize = 0;
            }
            settingsSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            TMP_Settings loadedSettings = Resources.Load<TMP_Settings>("TMP Settings");
            TMP_FontAsset loadedFont = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            if (loadedSettings == null || loadedFont == null)
            {
                throw new InvalidOperationException("ROKAS TMP production assets were not loadable through Resources after official import and Unity serialization.");
            }
            if (!HasRequiredCharacters(loadedFont))
            {
                throw new InvalidOperationException("ROKAS TMP production font is missing required Cyrillic glyphs after generation.");
            }

            ExportArtifacts();
            Debug.Log("ROKAS TMP production assets generated from official essentials and exported for persistence.");
        }

        private static TMP_FontAsset GenerateProductionFont()
        {
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                throw new InvalidOperationException("ROKAS source font is missing: " + SourceFontPath);
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
                source,
                32,
                4,
                GlyphRenderMode.SDFAA,
                512,
                512,
                AtlasPopulationMode.Dynamic,
                true);
            if (font == null || font.material == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
            {
                throw new InvalidOperationException("TextMesh Pro could not create the ROKAS production font asset from RokasSans.ttf.");
            }

            string productionGlyphs = BuildProductionGlyphSet();
            if (!font.TryAddCharacters(productionGlyphs, out string missingCharacters, true) || !string.IsNullOrEmpty(missingCharacters))
            {
                throw new InvalidOperationException("RokasSans TMP could not bake required production glyphs before serialization: " + missingCharacters);
            }
            if (!HasRequiredCharacters(font))
            {
                throw new InvalidOperationException("RokasSans TMP in-memory font did not contain required Cyrillic glyphs before serialization.");
            }

            font.name = "RokasSans TMP";
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.material.name = "RokasSans TMP Material";
            for (int index = 0; index < font.atlasTextures.Length; index++)
            {
                if (font.atlasTextures[index] != null)
                {
                    font.atlasTextures[index].name = index == 0 ? "RokasSans TMP Atlas" : "RokasSans TMP Atlas " + index;
                }
            }

            Material material = font.material;
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.AddObjectToAsset(material, font);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                if (atlas != null)
                {
                    AssetDatabase.AddObjectToAsset(atlas, font);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport);

            TMP_FontAsset loaded = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (loaded == null || !HasRequiredCharacters(loaded))
            {
                throw new InvalidOperationException("RokasSans TMP lost required glyphs during Unity serialization.");
            }
            return loaded;
        }

        private static bool HasRequiredCharacters(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }
            bool hasCharacters = font.HasCharacters(CyrillicProbe, out uint[] missing, false, false);
            return hasCharacters && (missing == null || missing.Length == 0);
        }

        private static string BuildProductionGlyphSet()
        {
            var builder = new StringBuilder(192);
            for (int codePoint = 0x20; codePoint <= 0x7E; codePoint++)
            {
                builder.Append((char)codePoint);
            }
            builder.Append(RussianAlphabet);
            builder.Append("–—…„“”’№₽");
            return builder.ToString();
        }

        private static void ExportArtifacts()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string exportRoot = Path.Combine(projectRoot, "artifacts", "tmp-production-assets");
            if (Directory.Exists(exportRoot)) Directory.Delete(exportRoot, true);
            Directory.CreateDirectory(exportRoot);

            CopyTree(projectRoot, "Assets/TextMesh Pro", exportRoot);
            CopyFile(projectRoot, "Assets/TextMesh Pro.meta", exportRoot);
            CopyFile(projectRoot, FontPath, exportRoot);
            CopyFile(projectRoot, FontPath + ".meta", exportRoot);
        }

        private static void CopyTree(string projectRoot, string relativeDirectory, string exportRoot)
        {
            string sourceRoot = Path.Combine(projectRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException("Expected TMP essentials directory is missing: " + sourceRoot);
            }
            foreach (string source in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string relative = source.Substring(projectRoot.Length + 1);
                string destination = Path.Combine(exportRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(source, destination, true);
            }
        }

        private static void CopyFile(string projectRoot, string relativePath, string exportRoot)
        {
            string source = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("Expected Unity-authored TMP artifact is missing.", source);
            }
            string destination = Path.Combine(exportRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        private static void SetString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            property.stringValue = value;
        }

        private static void SetBool(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            property.boolValue = value;
        }

        private static void SetObject(SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            property.objectReferenceValue = value;
        }
    }
}
#endif
