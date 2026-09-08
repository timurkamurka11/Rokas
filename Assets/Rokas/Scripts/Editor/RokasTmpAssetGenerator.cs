#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    // Temporary CI-only authoring utility. It uses Unity serialization to produce the
    // exact TMP production assets that will be persisted, then this script is removed.
    [InitializeOnLoad]
    internal static class RokasTmpAssetGenerator
    {
        private const string SettingsPath = "Assets/Rokas/Resources/TMP Settings.asset";
        private const string FontPath = "Assets/Rokas/Resources/RokasSans TMP.asset";
        private const string MaterialPath = "Assets/Rokas/Resources/RokasSans TMP Material.mat";
        private const string AtlasPath = "Assets/Rokas/Resources/RokasSans TMP Atlas.asset";
        private const string SourceFontPath = "Assets/Rokas/Art/UI/Fonts/RokasSans.ttf";
        private const string CyrillicProbe = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя ПОИСК Имя или роль Сообщения Назад В сети Не в сети";

        static RokasTmpAssetGenerator()
        {
            if (!Application.isBatchMode)
            {
                return;
            }
            GenerateAndExport();
        }

        private static void GenerateAndExport()
        {
            if (!File.Exists(SettingsPath) || !File.Exists(FontPath))
            {
                GenerateProductionAssets();
            }

            TMP_Settings settings = Resources.Load<TMP_Settings>("TMP Settings");
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("RokasSans TMP");
            if (settings == null || font == null)
            {
                throw new InvalidOperationException("ROKAS TMP production assets were not loadable after Unity serialization.");
            }
            if (!font.HasCharacters(CyrillicProbe, out uint[] missing, true, false) || missing.Length != 0)
            {
                throw new InvalidOperationException("ROKAS TMP production font is missing required Cyrillic glyphs after generation.");
            }

            ExportArtifacts();
            Debug.Log("ROKAS TMP production assets generated and exported for persistence.");
        }

        private static void GenerateProductionAssets()
        {
            DeleteIfPresent(FontPath);
            DeleteIfPresent(MaterialPath);
            DeleteIfPresent(AtlasPath);
            DeleteIfPresent(SettingsPath);

            TMP_Settings settings = ScriptableObject.CreateInstance<TMP_Settings>();
            settings.name = "TMP Settings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            SerializedObject settingsSerialized = new SerializedObject(settings);
            SetString(settingsSerialized, "assetVersion", "2");
            SetBool(settingsSerialized, "m_ClearDynamicDataOnBuild", false);
            settingsSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
            {
                throw new InvalidOperationException("Unity created TMP Settings but Resources.Load<TMP_Settings>(\"TMP Settings\") could not resolve it.");
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (source == null)
            {
                throw new InvalidOperationException("ROKAS source font is missing: " + SourceFontPath);
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source);
            if (font == null || font.material == null || font.atlasTextures == null || font.atlasTextures.Length == 0 || font.atlasTextures[0] == null)
            {
                throw new InvalidOperationException("TextMesh Pro could not create the ROKAS production font asset from RokasSans.ttf.");
            }
            font.name = "RokasSans TMP";
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            Material material = font.material;
            material.name = "RokasSans TMP Material";
            Texture2D atlas = font.atlasTextures[0];
            atlas.name = "RokasSans TMP Atlas";

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.CreateAsset(atlas, AtlasPath);
            AssetDatabase.CreateAsset(font, FontPath);
            AssetDatabase.SaveAssets();

            if (!font.TryAddCharacters(CyrillicProbe, out string missingCharacters, true) || !string.IsNullOrEmpty(missingCharacters))
            {
                throw new InvalidOperationException("RokasSans TMP could not author required Cyrillic glyphs: " + missingCharacters);
            }
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(atlas);

            settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(SettingsPath);
            settingsSerialized = new SerializedObject(settings);
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
        }

        private static void ExportArtifacts()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string destination = Path.Combine(projectRoot, "artifacts", "tmp-production-assets");
            Directory.CreateDirectory(destination);
            foreach (string assetPath in new[] { SettingsPath, FontPath, MaterialPath, AtlasPath })
            {
                Copy(projectRoot, assetPath, destination);
                Copy(projectRoot, assetPath + ".meta", destination);
            }
        }

        private static void Copy(string projectRoot, string relativePath, string destination)
        {
            string source = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("Expected Unity-authored TMP artifact is missing.", source);
            }
            File.Copy(source, Path.Combine(destination, Path.GetFileName(relativePath)), true);
        }

        private static void DeleteIfPresent(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void SetString(SerializedObject target, string propertyName, string value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            }
            property.stringValue = value;
        }

        private static void SetBool(SerializedObject target, string propertyName, bool value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            }
            property.boolValue = value;
        }

        private static void SetObject(SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException("TMP Settings property not found: " + propertyName);
            }
            property.objectReferenceValue = value;
        }
    }
}
#endif
