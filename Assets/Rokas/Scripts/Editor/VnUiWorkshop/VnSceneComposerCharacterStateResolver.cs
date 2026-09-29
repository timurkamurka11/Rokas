using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerResolvedCharacterState
    {
        public string Id { get; internal set; }
        public string Character { get; internal set; }
        public Texture2D Texture { get; internal set; }
        public Rect BodyUv { get; internal set; }
        public bool Onboarded { get; internal set; }
        public string AssetId { get; internal set; }
        public string DisplayName { get; internal set; }
    }

    public static class VnSceneComposerCharacterStateResolver
    {
        public static bool TryResolve(string stateId, out VnSceneComposerResolvedCharacterState resolved)
        {
            resolved = null;
            if (string.IsNullOrWhiteSpace(stateId)) return false;

            if (Rokas.Presentation.VnCharacterVisualCatalog.TryResolve(stateId, out VnCharacterVisualState production))
            {
                RokasAssets assets = VnPresentationWorkshopPreviewRenderer.LoadAssets();
                Texture2D texture = ResolveProductionTexture(assets, production.Character);
                if (texture == null) return false;
                resolved = new VnSceneComposerResolvedCharacterState
                {
                    Id = production.Id,
                    Character = production.Character,
                    Texture = texture,
                    BodyUv = production.BodyUv,
                    Onboarded = false,
                    AssetId = string.Empty,
                    DisplayName = production.Id
                };
                return true;
            }

            string projectRoot = VnSceneComposerAssetLibrary.GetDefaultProjectRoot();
            VnSceneComposerAssetEntry entry = VnSceneComposerAssetLibrary
                .FindByPurpose(projectRoot, VnSceneComposerAssetPurpose.CharacterState)
                .FirstOrDefault(candidate => candidate != null && !candidate.missing &&
                    string.Equals(candidate.stateId, stateId, StringComparison.Ordinal));
            if (entry == null || string.IsNullOrEmpty(entry.assetPath)) return false;

            Texture2D onboardedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.assetPath);
            if (onboardedTexture == null) return false;
            resolved = new VnSceneComposerResolvedCharacterState
            {
                Id = entry.stateId ?? string.Empty,
                Character = entry.character ?? string.Empty,
                Texture = onboardedTexture,
                BodyUv = new Rect(0f, 0f, 1f, 1f),
                Onboarded = true,
                AssetId = entry.stableAssetId ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(entry.displayName) ? entry.stateName : entry.displayName
            };
            return true;
        }

        public static string[] GetStateIds(string character)
        {
            string requested = character ?? string.Empty;
            IEnumerable<string> production = EnumerateProductionStates()
                .Where(state => string.Equals(state.Character, requested, StringComparison.OrdinalIgnoreCase))
                .Select(state => state.Id);
            string projectRoot = VnSceneComposerAssetLibrary.GetDefaultProjectRoot();
            IEnumerable<string> onboarded = VnSceneComposerAssetLibrary.FindCharacterStates(projectRoot, requested)
                .Where(entry => entry != null && !entry.missing && !string.IsNullOrWhiteSpace(entry.stateId))
                .Select(entry => entry.stateId);
            return production.Concat(onboarded).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        public static string[] GetCharacters()
        {
            IEnumerable<string> production = EnumerateProductionStates().Select(state => state.Character);
            string projectRoot = VnSceneComposerAssetLibrary.GetDefaultProjectRoot();
            IEnumerable<string> onboarded = VnSceneComposerAssetLibrary
                .FindByPurpose(projectRoot, VnSceneComposerAssetPurpose.CharacterState)
                .Where(entry => entry != null && !entry.missing)
                .Select(entry => entry.character);
            return production.Concat(onboarded)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static Texture2D ResolveProductionTexture(RokasAssets assets, string character)
        {
            if (assets == null) return null;
            if (string.Equals(character, "Mina", StringComparison.OrdinalIgnoreCase)) return assets.vnMinaCharacterSheet;
            if (string.Equals(character, "Keiko", StringComparison.OrdinalIgnoreCase)) return assets.vnKeikoCharacterSheet;
            return null;
        }

        private static VnCharacterVisualState[] EnumerateProductionStates()
        {
            FieldInfo statesField = typeof(Rokas.Presentation.VnCharacterVisualCatalog)
                .GetField("States", BindingFlags.NonPublic | BindingFlags.Static);
            IEnumerable entries = statesField != null ? statesField.GetValue(null) as IEnumerable : null;
            if (entries == null) return Array.Empty<VnCharacterVisualState>();
            var result = new List<VnCharacterVisualState>();
            foreach (object pair in entries)
            {
                if (pair == null) continue;
                PropertyInfo valueProperty = pair.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                object boxed = valueProperty != null ? valueProperty.GetValue(pair, null) : null;
                if (boxed is VnCharacterVisualState state && !string.IsNullOrEmpty(state.Id)) result.Add(state);
            }
            return result.OrderBy(state => state.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
