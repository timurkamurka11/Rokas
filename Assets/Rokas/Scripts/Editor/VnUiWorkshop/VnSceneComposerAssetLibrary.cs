using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public enum VnSceneComposerAssetPurpose
    {
        CharacterState,
        Background,
        ImageStill,
        UiOverlay,
        ReferenceImage
    }

    [Serializable]
    public sealed class VnSceneComposerAssetEntry
    {
        public string stableAssetId;
        public VnSceneComposerAssetPurpose purpose;
        public string displayName;
        public string assetPath;
        public string assetGuid;
        public string contentHash;
        public string character;
        public string stateName;
        public string stateId;
        public bool missing;
        public string warning;
        public bool managedByComposer;
        public bool productionReadOnly;
    }

    [Serializable]
    public sealed class VnSceneComposerAssetCatalog
    {
        public int schemaVersion = 1;
        public List<VnSceneComposerAssetEntry> entries = new List<VnSceneComposerAssetEntry>();
    }

    public sealed class VnSceneComposerAssetOnboardResult
    {
        public bool Success;
        public bool Duplicate;
        public string Error;
        public VnSceneComposerAssetEntry Entry;
    }

    public static class VnSceneComposerAssetLibrary
    {
        public const string ManagedRootRelative = "Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets";
        public const string CatalogFileName = "ROKAS_VN_SCENE_COMPOSER_ASSET_CATALOG.json";
        public const int CatalogSchemaVersion = 1;

        private static readonly string[] SupportedStillExtensions = { ".png", ".jpg", ".jpeg" };

        public static VnSceneComposerAssetOnboardResult Onboard(
            string projectRoot,
            string sourcePath,
            VnSceneComposerAssetPurpose purpose,
            string displayName,
            string character,
            string stateName)
        {
            try
            {
                projectRoot = RequireProjectRoot(projectRoot);
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                    return Failure("Source asset does not exist: " + (sourcePath ?? string.Empty));

                string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
                if (!SupportedStillExtensions.Contains(extension))
                    return Failure("Unsupported still-image extension: " + extension + ".");
                if (purpose == VnSceneComposerAssetPurpose.CharacterState && extension != ".png")
                    return Failure("Character full-body states must use PNG so alpha can be preserved.");
                if (purpose == VnSceneComposerAssetPurpose.CharacterState &&
                    (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(stateName)))
                    return Failure("Character State onboarding requires both Character and State / Pose name.");

                VnSceneComposerAssetCatalog catalog = LoadCatalog(projectRoot);
                string hash = Sha256File(sourcePath);
                VnSceneComposerAssetEntry duplicate = FindDuplicate(catalog, purpose, hash, character, stateName);
                if (duplicate != null)
                {
                    return new VnSceneComposerAssetOnboardResult
                    {
                        Success = true,
                        Duplicate = true,
                        Entry = duplicate,
                        Error = "Asset already exists in the Composer library as '" + duplicate.displayName + "'."
                    };
                }

                string category = PurposeFolder(purpose);
                string characterFolder = purpose == VnSceneComposerAssetPurpose.CharacterState
                    ? "/" + SafePathSegment(character)
                    : string.Empty;
                string relativeFolder = ManagedRootRelative + "/" + category + characterFolder;
                string absoluteFolder = ToAbsolute(projectRoot, relativeFolder);
                Directory.CreateDirectory(absoluteFolder);

                string sourceName = SafeFileName(Path.GetFileNameWithoutExtension(sourcePath));
                if (string.IsNullOrWhiteSpace(sourceName)) sourceName = SafeFileName(displayName);
                if (string.IsNullOrWhiteSpace(sourceName)) sourceName = "asset";
                string destinationPath = relativeFolder + "/" + sourceName + extension;
                string destinationAbsolute = ToAbsolute(projectRoot, destinationPath);
                if (File.Exists(destinationAbsolute))
                {
                    string existingHash = Sha256File(destinationAbsolute);
                    if (!string.Equals(existingHash, hash, StringComparison.OrdinalIgnoreCase))
                    {
                        destinationPath = relativeFolder + "/" + sourceName + "-" + hash.Substring(0, 8).ToLowerInvariant() + extension;
                        destinationAbsolute = ToAbsolute(projectRoot, destinationPath);
                    }
                }

                if (!File.Exists(destinationAbsolute)) File.Copy(sourcePath, destinationAbsolute, false);
                AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                ConfigureStillImporter(destinationPath, purpose);

                VnSceneComposerAssetEntry entry = BuildEntry(
                    purpose,
                    string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(sourcePath) : displayName.Trim(),
                    destinationPath,
                    hash,
                    character,
                    stateName,
                    true,
                    false);
                catalog.entries.Add(entry);
                SaveCatalog(projectRoot, catalog);
                return new VnSceneComposerAssetOnboardResult { Success = true, Duplicate = false, Entry = entry, Error = string.Empty };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        public static VnSceneComposerAssetOnboardResult RegisterExistingProjectAsset(
            string projectRoot,
            string assetPath,
            VnSceneComposerAssetPurpose purpose,
            string displayName,
            string character,
            string stateName)
        {
            try
            {
                projectRoot = RequireProjectRoot(projectRoot);
                assetPath = NormalizeAssetPath(assetPath);
                if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    return Failure("Existing project asset must use an Assets/... project path.");
                string absolute = ToAbsolute(projectRoot, assetPath);
                if (!File.Exists(absolute)) return Failure("Existing project asset is missing: " + assetPath);
                if (purpose == VnSceneComposerAssetPurpose.CharacterState &&
                    (string.IsNullOrWhiteSpace(character) || string.IsNullOrWhiteSpace(stateName)))
                    return Failure("Character State registration requires Character and State / Pose name.");

                VnSceneComposerAssetCatalog catalog = LoadCatalog(projectRoot);
                VnSceneComposerAssetEntry byPath = catalog.entries.FirstOrDefault(entry =>
                    string.Equals(NormalizeAssetPath(entry.assetPath), assetPath, StringComparison.OrdinalIgnoreCase));
                if (byPath != null)
                {
                    return new VnSceneComposerAssetOnboardResult
                    {
                        Success = true,
                        Duplicate = true,
                        Entry = byPath,
                        Error = "Project asset is already registered in the Composer library."
                    };
                }

                string hash = Sha256File(absolute);
                VnSceneComposerAssetEntry duplicate = FindDuplicate(catalog, purpose, hash, character, stateName);
                if (duplicate != null)
                {
                    return new VnSceneComposerAssetOnboardResult
                    {
                        Success = true,
                        Duplicate = true,
                        Entry = duplicate,
                        Error = "Equivalent asset is already registered in the Composer library."
                    };
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                VnSceneComposerAssetEntry entry = BuildEntry(
                    purpose,
                    string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(assetPath) : displayName.Trim(),
                    assetPath,
                    hash,
                    character,
                    stateName,
                    false,
                    true);
                catalog.entries.Add(entry);
                SaveCatalog(projectRoot, catalog);
                return new VnSceneComposerAssetOnboardResult { Success = true, Entry = entry, Error = string.Empty };
            }
            catch (Exception exception)
            {
                return Failure(exception.Message);
            }
        }

        public static VnSceneComposerAssetCatalog Refresh(string projectRoot)
        {
            projectRoot = RequireProjectRoot(projectRoot);
            VnSceneComposerAssetCatalog catalog = LoadCatalog(projectRoot);
            EnsureEntries(catalog);

            foreach (VnSceneComposerAssetEntry entry in catalog.entries)
            {
                ValidateEntry(projectRoot, entry);
            }

            string managedAbsolute = ToAbsolute(projectRoot, ManagedRootRelative);
            if (Directory.Exists(managedAbsolute))
            {
                HashSet<string> knownPaths = new HashSet<string>(
                    catalog.entries.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.assetPath))
                        .Select(entry => NormalizeAssetPath(entry.assetPath)),
                    StringComparer.OrdinalIgnoreCase);
                HashSet<string> knownHashes = new HashSet<string>(
                    catalog.entries.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.contentHash))
                        .Select(entry => entry.contentHash),
                    StringComparer.OrdinalIgnoreCase);

                foreach (string absolutePath in Directory.GetFiles(managedAbsolute, "*.*", SearchOption.AllDirectories)
                             .Where(path => SupportedStillExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())))
                {
                    string assetPath = ToProjectRelative(projectRoot, absolutePath);
                    if (knownPaths.Contains(assetPath)) continue;
                    string hash = Sha256File(absolutePath);
                    if (knownHashes.Contains(hash)) continue;
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                    VnSceneComposerAssetEntry discovered = BuildEntry(
                        VnSceneComposerAssetPurpose.ReferenceImage,
                        Path.GetFileNameWithoutExtension(assetPath),
                        assetPath,
                        hash,
                        string.Empty,
                        string.Empty,
                        true,
                        false);
                    discovered.warning = "Auto-discovered as Reference Image. Use Onboard Asset to assign a more specific purpose.";
                    catalog.entries.Add(discovered);
                    knownPaths.Add(assetPath);
                    knownHashes.Add(hash);
                }
            }

            SaveCatalog(projectRoot, catalog);
            return CloneCatalog(catalog);
        }

        public static string SerializeCatalog(string projectRoot)
        {
            projectRoot = RequireProjectRoot(projectRoot);
            return SerializeDeterministic(LoadCatalog(projectRoot));
        }

        public static VnSceneComposerAssetEntry[] FindCharacterStates(string projectRoot, string character)
        {
            projectRoot = RequireProjectRoot(projectRoot);
            string requested = character == null ? string.Empty : character.Trim();
            return LoadCatalog(projectRoot).entries
                .Where(entry => entry != null && entry.purpose == VnSceneComposerAssetPurpose.CharacterState && !entry.missing &&
                                string.Equals(entry.character ?? string.Empty, requested, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.stateName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.stableAssetId ?? string.Empty, StringComparer.Ordinal)
                .Select(CloneEntry)
                .ToArray();
        }

        public static VnSceneComposerAssetEntry[] FindByPurpose(string projectRoot, VnSceneComposerAssetPurpose purpose)
        {
            projectRoot = RequireProjectRoot(projectRoot);
            return LoadCatalog(projectRoot).entries
                .Where(entry => entry != null && entry.purpose == purpose && !entry.missing)
                .OrderBy(entry => entry.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.stableAssetId ?? string.Empty, StringComparer.Ordinal)
                .Select(CloneEntry)
                .ToArray();
        }

        public static bool TryFindCharacterState(string projectRoot, string character, string stateId, out VnSceneComposerAssetEntry result)
        {
            result = FindCharacterStates(projectRoot, character)
                .FirstOrDefault(entry => string.Equals(entry.stateId ?? string.Empty, stateId ?? string.Empty, StringComparison.Ordinal));
            return result != null;
        }

        public static bool Unregister(string projectRoot, string stableAssetId)
        {
            projectRoot = RequireProjectRoot(projectRoot);
            if (string.IsNullOrWhiteSpace(stableAssetId)) return false;
            VnSceneComposerAssetCatalog catalog = LoadCatalog(projectRoot);
            int removed = catalog.entries.RemoveAll(entry => entry != null &&
                string.Equals(entry.stableAssetId, stableAssetId, StringComparison.Ordinal));
            if (removed == 0) return false;
            SaveCatalog(projectRoot, catalog);
            return true;
        }

        public static string GetDefaultProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent == null) throw new InvalidOperationException("Could not resolve Unity project root from Application.dataPath.");
            return parent.FullName;
        }

        private static VnSceneComposerAssetEntry FindDuplicate(
            VnSceneComposerAssetCatalog catalog,
            VnSceneComposerAssetPurpose purpose,
            string contentHash,
            string character,
            string stateName)
        {
            EnsureEntries(catalog);
            if (purpose == VnSceneComposerAssetPurpose.CharacterState)
            {
                VnSceneComposerAssetEntry logical = catalog.entries.FirstOrDefault(entry => entry != null &&
                    entry.purpose == VnSceneComposerAssetPurpose.CharacterState &&
                    string.Equals(entry.character ?? string.Empty, character ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(entry.stateName ?? string.Empty, stateName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                if (logical != null) return logical;
            }
            return catalog.entries.FirstOrDefault(entry => entry != null && !string.IsNullOrEmpty(contentHash) &&
                string.Equals(entry.contentHash, contentHash, StringComparison.OrdinalIgnoreCase));
        }

        private static VnSceneComposerAssetEntry BuildEntry(
            VnSceneComposerAssetPurpose purpose,
            string displayName,
            string assetPath,
            string hash,
            string character,
            string stateName,
            bool managed,
            bool productionReadOnly)
        {
            assetPath = NormalizeAssetPath(assetPath);
            string normalizedCharacter = string.IsNullOrWhiteSpace(character) ? string.Empty : character.Trim();
            string normalizedState = string.IsNullOrWhiteSpace(stateName) ? string.Empty : stateName.Trim();
            string stateId = purpose == VnSceneComposerAssetPurpose.CharacterState
                ? "onboarded:" + Slug(normalizedCharacter) + ":" + Slug(normalizedState)
                : string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(assetPath) ?? string.Empty;
            string stableId = purpose == VnSceneComposerAssetPurpose.CharacterState
                ? "asset:" + stateId
                : (productionReadOnly
                    ? "project:" + PurposeSlug(purpose) + ":" + (string.IsNullOrWhiteSpace(guid) ? hash.Substring(0, Math.Min(16, hash.Length)).ToLowerInvariant() : guid)
                    : "onboarded:" + PurposeSlug(purpose) + ":" + hash.Substring(0, Math.Min(16, hash.Length)).ToLowerInvariant());
            return new VnSceneComposerAssetEntry
            {
                stableAssetId = stableId,
                purpose = purpose,
                displayName = displayName ?? string.Empty,
                assetPath = assetPath,
                assetGuid = guid,
                contentHash = hash ?? string.Empty,
                character = normalizedCharacter,
                stateName = normalizedState,
                stateId = stateId,
                missing = false,
                warning = string.Empty,
                managedByComposer = managed,
                productionReadOnly = productionReadOnly
            };
        }

        private static void ValidateEntry(string projectRoot, VnSceneComposerAssetEntry entry)
        {
            if (entry == null) return;
            entry.assetPath = NormalizeAssetPath(entry.assetPath);
            if (string.IsNullOrWhiteSpace(entry.assetPath) || Path.IsPathRooted(entry.assetPath))
            {
                entry.missing = true;
                entry.warning = "Invalid portable project asset path.";
                return;
            }
            string absolute = ToAbsolute(projectRoot, entry.assetPath);
            entry.missing = !File.Exists(absolute);
            if (entry.missing)
            {
                entry.warning = "Project asset is missing: " + entry.assetPath;
                return;
            }
            entry.warning = string.Empty;
            string guid = AssetDatabase.AssetPathToGUID(entry.assetPath);
            if (!string.IsNullOrWhiteSpace(guid)) entry.assetGuid = guid;
            entry.contentHash = Sha256File(absolute);
        }

        private static void ConfigureStillImporter(string assetPath, VnSceneComposerAssetPurpose purpose)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
            }
            importer.SaveAndReimport();
        }

        private static VnSceneComposerAssetCatalog LoadCatalog(string projectRoot)
        {
            string path = ToAbsolute(projectRoot, CatalogAssetPath());
            if (!File.Exists(path)) return new VnSceneComposerAssetCatalog();
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return new VnSceneComposerAssetCatalog();
            VnSceneComposerAssetCatalog catalog = JsonUtility.FromJson<VnSceneComposerAssetCatalog>(json) ?? new VnSceneComposerAssetCatalog();
            if (catalog.schemaVersion != CatalogSchemaVersion)
                throw new InvalidDataException("Unsupported Scene Composer asset catalog schemaVersion: " + catalog.schemaVersion + ".");
            EnsureEntries(catalog);
            return catalog;
        }

        private static void SaveCatalog(string projectRoot, VnSceneComposerAssetCatalog catalog)
        {
            EnsureEntries(catalog);
            catalog.schemaVersion = CatalogSchemaVersion;
            string relative = CatalogAssetPath();
            string absolute = ToAbsolute(projectRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            File.WriteAllText(absolute, SerializeDeterministic(catalog), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static string SerializeDeterministic(VnSceneComposerAssetCatalog source)
        {
            VnSceneComposerAssetCatalog sorted = CloneCatalog(source);
            sorted.schemaVersion = CatalogSchemaVersion;
            sorted.entries = sorted.entries
                .Where(entry => entry != null)
                .OrderBy(entry => entry.stableAssetId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(entry => entry.assetPath ?? string.Empty, StringComparer.Ordinal)
                .ToList();
            foreach (VnSceneComposerAssetEntry entry in sorted.entries)
            {
                if (Path.IsPathRooted(entry.assetPath ?? string.Empty))
                    throw new InvalidDataException("Portable Composer asset catalog cannot serialize absolute asset paths.");
            }
            return JsonUtility.ToJson(sorted, true);
        }

        private static VnSceneComposerAssetCatalog CloneCatalog(VnSceneComposerAssetCatalog source)
        {
            var clone = new VnSceneComposerAssetCatalog { schemaVersion = source == null ? CatalogSchemaVersion : source.schemaVersion };
            if (source != null && source.entries != null) clone.entries = source.entries.Where(entry => entry != null).Select(CloneEntry).ToList();
            return clone;
        }

        private static VnSceneComposerAssetEntry CloneEntry(VnSceneComposerAssetEntry entry)
        {
            return new VnSceneComposerAssetEntry
            {
                stableAssetId = entry.stableAssetId,
                purpose = entry.purpose,
                displayName = entry.displayName,
                assetPath = entry.assetPath,
                assetGuid = entry.assetGuid,
                contentHash = entry.contentHash,
                character = entry.character,
                stateName = entry.stateName,
                stateId = entry.stateId,
                missing = entry.missing,
                warning = entry.warning,
                managedByComposer = entry.managedByComposer,
                productionReadOnly = entry.productionReadOnly
            };
        }

        private static void EnsureEntries(VnSceneComposerAssetCatalog catalog)
        {
            if (catalog.entries == null) catalog.entries = new List<VnSceneComposerAssetEntry>();
        }

        private static string CatalogAssetPath()
        {
            return ManagedRootRelative + "/" + CatalogFileName;
        }

        private static string RequireProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot)) throw new ArgumentException("Unity project root is required.", nameof(projectRoot));
            string full = Path.GetFullPath(projectRoot);
            if (!Directory.Exists(Path.Combine(full, "Assets")))
                throw new DirectoryNotFoundException("Unity project root does not contain Assets/: " + full);
            return full;
        }

        private static string ToAbsolute(string projectRoot, string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(projectRoot, NormalizeAssetPath(projectRelativePath).Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToProjectRelative(string projectRoot, string absolutePath)
        {
            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(absolutePath);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Asset is outside the Unity project: " + full);
            return NormalizeAssetPath(full.Substring(root.Length));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        }

        private static string PurposeFolder(VnSceneComposerAssetPurpose purpose)
        {
            switch (purpose)
            {
                case VnSceneComposerAssetPurpose.CharacterState: return "CharacterState";
                case VnSceneComposerAssetPurpose.Background: return "Background";
                case VnSceneComposerAssetPurpose.ImageStill: return "ImageStill";
                case VnSceneComposerAssetPurpose.UiOverlay: return "UiOverlay";
                case VnSceneComposerAssetPurpose.ReferenceImage: return "ReferenceImage";
                default: throw new ArgumentOutOfRangeException(nameof(purpose), purpose, null);
            }
        }

        private static string PurposeSlug(VnSceneComposerAssetPurpose purpose)
        {
            return Slug(PurposeFolder(purpose));
        }

        private static string Slug(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "asset";
            var builder = new StringBuilder(value.Length);
            bool separator = false;
            foreach (char ch in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                    separator = false;
                }
                else if (!separator && builder.Length > 0)
                {
                    builder.Append('_');
                    separator = true;
                }
            }
            return builder.ToString().Trim('_');
        }

        private static string SafePathSegment(string value)
        {
            string slug = Slug(value);
            return string.IsNullOrWhiteSpace(slug) ? "asset" : slug;
        }

        private static string SafeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (char ch in value.Trim()) builder.Append(invalid.Contains(ch) ? '_' : ch);
            return builder.ToString().Trim().Trim('.');
        }

        private static string Sha256File(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        private static VnSceneComposerAssetOnboardResult Failure(string error)
        {
            return new VnSceneComposerAssetOnboardResult
            {
                Success = false,
                Duplicate = false,
                Error = error ?? "Unknown asset onboarding error.",
                Entry = null
            };
        }
    }
}
