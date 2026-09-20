using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    [Serializable]
    public sealed class VnSceneComposerUserAssetManifestEntry
    {
        public string assetPath = string.Empty;
        public string guid = string.Empty;
        public string sourcePath = string.Empty;
        public string sha256 = string.Empty;
        public bool hasMeta;
    }

    [Serializable]
    public sealed class VnSceneComposerUserAssetManifest
    {
        public string projectId = string.Empty;
        public string sourceProjectRoot = string.Empty;
        public string destinationProjectRoot = string.Empty;
        public List<VnSceneComposerUserAssetManifestEntry> assets =
            new List<VnSceneComposerUserAssetManifestEntry>();
    }

    [Serializable]
    internal sealed class VnSceneComposerMigrationBindingDocument
    {
        public List<VnSceneComposerMigrationBinding> bindings = new List<VnSceneComposerMigrationBinding>();
    }

    [Serializable]
    internal sealed class VnSceneComposerMigrationBinding
    {
        public string sceneId = string.Empty;
        public string kind = string.Empty;
        public string contentHash = string.Empty;
        public string absolutePath = string.Empty;
    }

    public static class VnSceneComposerReviewProjectMigration
    {
        public const string ManifestFileName = "user-asset-manifest.json";
        private const string LocalBindingsFileName = "local-media-bindings.json";
        private static readonly Regex Hex32 = new Regex(
            @"\b[0-9a-fA-F]{32}\b", RegexOptions.Compiled);
        private static readonly Regex MetaGuid = new Regex(
            @"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$", RegexOptions.Compiled);

        private static readonly string[] UserOwnedRoots =
        {
            "Assets/Rokas/Scripts/Editor/VnUiWorkshop/OnboardedAssets",
            "Assets/Rokas/Art/UI/Fonts/Imported",
            "Assets/Rokas/Resources/Fonts/TMP"
        };

        public static string FindLatestProjectSource(
            string searchRoot, string projectId, string excludeProjectRoot)
        {
            if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
                return string.Empty;
            if (!VnSceneComposerSerialization.IsStableId(projectId))
                throw new ArgumentException("A stable Scene Composer project ID is required.", nameof(projectId));

            string excluded = NormalizeRoot(excludeProjectRoot);
            var candidates = new List<Tuple<string, DateTime>>();
            IEnumerable<string> roots = new[] { Path.GetFullPath(searchRoot) }
                .Concat(Directory.GetDirectories(searchRoot).Select(path => Path.GetFullPath(path)));

            foreach (string root in roots)
            {
                if (string.Equals(NormalizeRoot(root), excluded, StringComparison.OrdinalIgnoreCase))
                    continue;
                string projectPath;
                try { projectPath = VnSceneComposerStorage.GetProjectPath(root, projectId); }
                catch { continue; }
                if (!File.Exists(projectPath)) continue;
                if (!ProjectJsonHasIdentity(projectPath, projectId)) continue;
                candidates.Add(Tuple.Create(root, File.GetLastWriteTimeUtc(projectPath)));
            }

            return candidates
                .OrderByDescending(item => item.Item2)
                .ThenByDescending(item => item.Item1, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Item1)
                .FirstOrDefault() ?? string.Empty;
        }

        public static void Migrate(
            string searchRoot, string destinationProjectRoot, string projectId)
        {
            destinationProjectRoot = Path.GetFullPath(destinationProjectRoot);
            string sourceRoot = FindLatestProjectSource(
                searchRoot, projectId, destinationProjectRoot);
            if (string.IsNullOrEmpty(sourceRoot))
                throw new FileNotFoundException(
                    "No valid saved Scene Composer project was found for ID " + projectId + ".");

            string sourceProjectFile = VnSceneComposerStorage.GetProjectPath(sourceRoot, projectId);
            string sourceProjectDir = Path.GetDirectoryName(sourceProjectFile);
            string destinationProjectFile =
                VnSceneComposerStorage.GetProjectPath(destinationProjectRoot, projectId);
            string destinationProjectDir = Path.GetDirectoryName(destinationProjectFile);

            string backupRoot = CreateDurableBackup(
                searchRoot, sourceRoot, sourceProjectDir, projectId);
            CopyDirectoryConflictSafe(sourceProjectDir, destinationProjectDir);

            var manifest = new VnSceneComposerUserAssetManifest
            {
                projectId = projectId,
                sourceProjectRoot = sourceRoot,
                destinationProjectRoot = destinationProjectRoot
            };

            for (int i = 0; i < UserOwnedRoots.Length; i++)
            {
                CopyKnownUserRoot(sourceRoot, destinationProjectRoot, UserOwnedRoots[i], manifest);
                CopyKnownUserRoot(sourceRoot, backupRoot, UserOwnedRoots[i], null);
            }

            string projectJson = File.ReadAllText(sourceProjectFile, Encoding.UTF8);
            HashSet<string> referencedGuids = new HashSet<string>(
                Hex32.Matches(projectJson).Cast<Match>().Select(m => m.Value),
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> sourceGuidMap = BuildSourceGuidMap(sourceRoot);

            foreach (string guid in referencedGuids)
            {
                if (!sourceGuidMap.TryGetValue(guid, out string sourceAssetPath)) continue;
                string currentPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(currentPath)) continue;
                CopyReferencedAsset(sourceRoot, destinationProjectRoot, sourceAssetPath, guid, manifest);
                CopyReferencedAsset(sourceRoot, backupRoot, sourceAssetPath, guid, null);
            }

            PreserveExternalBindings(
                searchRoot, projectId, destinationProjectDir, manifest);

            string manifestPath = Path.Combine(destinationProjectDir, ManifestFileName);
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateMigratedProject(destinationProjectRoot, projectId, manifest);
            Debug.Log("ROKAS_VN_USER_PROJECT_MIGRATION=PASS source=" + sourceRoot +
                      " project=" + projectId + " assets=" + manifest.assets.Count);
        }

        public static void MigrateFromCommandLine()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string searchRoot = Argument(args, "-vnMigrationRoot");
                string projectId = Argument(args, "-vnProjectId");
                string destination = Directory.GetParent(Application.dataPath).FullName;
                Migrate(searchRoot, destination, projectId);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("ROKAS_VN_USER_PROJECT_MIGRATION=FAIL " + exception);
                EditorApplication.Exit(1);
            }
        }

        private static void CopyKnownUserRoot(
            string sourceRoot, string destinationRoot, string relativeRoot,
            VnSceneComposerUserAssetManifest manifest)
        {
            string source = ToAbsolute(sourceRoot, relativeRoot);
            if (!Directory.Exists(source)) return;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = ToRelative(sourceRoot, file);
                CopyFileConflictSafe(file, ToAbsolute(destinationRoot, relative));
                if (manifest != null &&
                    !relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    AddManifestEntry(file, relative, manifest);
            }
        }

        private static void CopyReferencedAsset(
            string sourceRoot, string destinationRoot, string assetPath, string guid,
            VnSceneComposerUserAssetManifest manifest)
        {
            string source = ToAbsolute(sourceRoot, assetPath);
            if (!File.Exists(source)) return;
            string destination = ToAbsolute(destinationRoot, assetPath);
            CopyFileConflictSafe(source, destination);
            string sourceMeta = source + ".meta";
            if (File.Exists(sourceMeta))
                CopyFileConflictSafe(sourceMeta, destination + ".meta");
            if (manifest != null) AddManifestEntry(source, assetPath, manifest, guid);
        }

        private static void AddManifestEntry(
            string sourceFile, string assetPath,
            VnSceneComposerUserAssetManifest manifest, string expectedGuid = "")
        {
            if (manifest.assets.Any(e => string.Equals(
                e.assetPath, assetPath, StringComparison.OrdinalIgnoreCase))) return;
            string meta = sourceFile + ".meta";
            string guid = expectedGuid;
            if (File.Exists(meta))
            {
                Match m = MetaGuid.Match(File.ReadAllText(meta));
                if (m.Success) guid = m.Groups[1].Value;
            }
            manifest.assets.Add(new VnSceneComposerUserAssetManifestEntry
            {
                assetPath = assetPath.Replace('\\', '/'),
                guid = guid ?? string.Empty,
                sourcePath = sourceFile,
                sha256 = Sha256(sourceFile),
                hasMeta = File.Exists(meta)
            });
        }

        private static Dictionary<string, string> BuildSourceGuidMap(string sourceRoot)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string assets = Path.Combine(sourceRoot, "Assets");
            if (!Directory.Exists(assets)) return map;
            foreach (string meta in Directory.GetFiles(assets, "*.meta", SearchOption.AllDirectories))
            {
                Match m;
                try { m = MetaGuid.Match(File.ReadAllText(meta)); }
                catch { continue; }
                if (!m.Success) continue;
                string assetFile = meta.Substring(0, meta.Length - ".meta".Length);
                if (!File.Exists(assetFile)) continue;
                map[m.Groups[1].Value] = ToRelative(sourceRoot, assetFile);
            }
            return map;
        }

        private static void PreserveExternalBindings(
            string searchRoot, string projectId, string destinationProjectDir,
            VnSceneComposerUserAssetManifest manifest)
        {
            string bindingPath = Path.Combine(destinationProjectDir, LocalBindingsFileName);
            if (!File.Exists(bindingPath)) return;
            VnSceneComposerMigrationBindingDocument doc;
            try
            {
                doc = JsonUtility.FromJson<VnSceneComposerMigrationBindingDocument>(
                    File.ReadAllText(bindingPath));
            }
            catch { return; }
            if (doc == null || doc.bindings == null) return;

            string durable = Path.Combine(searchRoot, "VNProjects", projectId, "ExternalMedia");
            bool changed = false;
            for (int i = 0; i < doc.bindings.Count; i++)
            {
                VnSceneComposerMigrationBinding b = doc.bindings[i];
                if (b == null || string.IsNullOrWhiteSpace(b.absolutePath) ||
                    !Path.IsPathRooted(b.absolutePath) || !File.Exists(b.absolutePath)) continue;
                string hash = !string.IsNullOrWhiteSpace(b.contentHash)
                    ? b.contentHash : Sha256(b.absolutePath);
                string folder = Path.Combine(durable, SafeSegment(hash));
                string target = Path.Combine(folder, Path.GetFileName(b.absolutePath));
                CopyFileConflictSafe(b.absolutePath, target);
                b.absolutePath = Path.GetFullPath(target);
                changed = true;
            }
            if (changed)
                File.WriteAllText(bindingPath, JsonUtility.ToJson(doc, true), new UTF8Encoding(false));
        }

        private static string CreateDurableBackup(
            string searchRoot, string sourceRoot, string sourceProjectDir, string projectId)
        {
            DateTime saved = File.GetLastWriteTimeUtc(
                Path.Combine(sourceProjectDir, VnSceneComposerStorage.ProjectFileName));
            string stamp = saved.ToString("yyyyMMdd-HHmmss") + "-" + saved.Ticks;
            string backup = Path.Combine(searchRoot, "VNProjects", projectId, "Snapshots", stamp);
            CopyDirectoryConflictSafe(sourceProjectDir, Path.Combine(backup, "ProjectData"));
            string note = Path.Combine(backup, "SOURCE.txt");
            Directory.CreateDirectory(backup);
            if (!File.Exists(note))
                File.WriteAllText(note, Path.GetFullPath(sourceRoot), new UTF8Encoding(false));
            return backup;
        }

        private static void ValidateMigratedProject(
            string destinationRoot, string projectId, VnSceneComposerUserAssetManifest manifest)
        {
            VnSceneComposerImportResult loaded =
                VnSceneComposerStorage.LoadProject(destinationRoot, projectId);
            if (!loaded.Success || loaded.Project == null)
                throw new InvalidDataException(
                    "Migrated VN project cannot be loaded: " + loaded.Error);
            if (!string.Equals(loaded.Project.projectId, projectId, StringComparison.Ordinal))
                throw new InvalidDataException("Migrated VN project identity changed.");

            for (int i = 0; i < manifest.assets.Count; i++)
            {
                VnSceneComposerUserAssetManifestEntry e = manifest.assets[i];
                if (e == null || string.IsNullOrEmpty(e.guid)) continue;
                string resolved = AssetDatabase.GUIDToAssetPath(e.guid);
                if (string.IsNullOrEmpty(resolved))
                    throw new InvalidDataException(
                        "Migrated user asset GUID does not resolve: " + e.guid + " " + e.assetPath);
                if (!string.Equals(
                    resolved.Replace('\\', '/'), e.assetPath.Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        "Migrated GUID resolved to unexpected path: " + e.guid +
                        " expected=" + e.assetPath + " actual=" + resolved);
            }
        }

        private static bool ProjectJsonHasIdentity(string path, string projectId)
        {
            try
            {
                VnSceneComposerProject p = JsonUtility.FromJson<VnSceneComposerProject>(
                    File.ReadAllText(path));
                return p != null && string.Equals(
                    p.projectId, projectId, StringComparison.Ordinal);
            }
            catch { return false; }
        }

        private static void CopyDirectoryConflictSafe(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(
                    source.TrimEnd(Path.DirectorySeparatorChar).Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                CopyFileConflictSafe(file, Path.Combine(destination, relative));
            }
        }

        private static void CopyFileConflictSafe(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if (!File.Exists(destination))
            {
                File.Copy(source, destination, false);
                return;
            }
            if (!string.Equals(Sha256(source), Sha256(destination), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Migration conflict; refusing to overwrite: " + destination);
        }

        private static string Argument(string[] args, string key)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            throw new ArgumentException("Missing command-line argument " + key + ".");
        }

        private static string ToAbsolute(string root, string relative)
        {
            return Path.GetFullPath(Path.Combine(
                root, (relative ?? string.Empty).Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string ToRelative(string root, string path)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Path is outside project root: " + full);
            return full.Substring(prefix.Length).Replace('\\', '/');
        }

        private static string NormalizeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string Sha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string SafeSegment(string value)
        {
            string s = (value ?? string.Empty).Trim();
            return new string(s.Where(char.IsLetterOrDigit).Take(64).ToArray());
        }
    }
}
