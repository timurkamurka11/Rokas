using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public static class VnSceneComposerStorage
    {
        public const string ProjectFileName = "ROKAS_VN_SCENE_COMPOSER_PROJECT.json";
        private const string LocalBindingsFileName = "local-media-bindings.json";

        private static readonly string[] ProjectsPathParts =
        {
            "Library", "ROKAS", "VnSceneComposer", "projects"
        };

        [Serializable]
        private sealed class LocalBindingDocument
        {
            public List<LocalMediaBinding> bindings = new List<LocalMediaBinding>();
        }

        [Serializable]
        private sealed class LocalMediaBinding
        {
            public string sceneId = string.Empty;
            public string kind = string.Empty;
            public string contentHash = string.Empty;
            public string absolutePath = string.Empty;
        }

        public static string GetProjectsDirectory(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));
            string path = projectRoot;
            for (int i = 0; i < ProjectsPathParts.Length; i++) path = Path.Combine(path, ProjectsPathParts[i]);
            return Path.GetFullPath(path);
        }

        public static string GetProjectPath(string projectRoot, string projectId)
        {
            string directory = GetProjectDirectory(projectRoot, projectId);
            return Path.GetFullPath(Path.Combine(directory, ProjectFileName));
        }

        public static void SaveProject(string projectRoot, VnSceneComposerProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            string json = VnSceneComposerSerialization.SerializePortable(project);
            string projectPath = GetProjectPath(projectRoot, project.projectId);
            string directory = Path.GetDirectoryName(projectPath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(projectPath, json);
            SaveLocalBindings(directory, project);
        }

        public static VnSceneComposerImportResult LoadProject(string projectRoot, string projectId)
        {
            string projectPath;
            try
            {
                projectPath = GetProjectPath(projectRoot, projectId);
            }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed("Scene Composer project path is invalid: " + exception.Message);
            }
            if (!File.Exists(projectPath))
                return VnSceneComposerImportResult.Failed("Scene Composer project does not exist: " + projectId + ".");

            VnSceneComposerImportResult result;
            try
            {
                result = VnSceneComposerSerialization.DeserializePortable(File.ReadAllText(projectPath));
            }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed("Scene Composer project could not be loaded: " + exception.Message);
            }
            if (!result.Success || result.Project == null) return result;

            RestoreLocalBindings(Path.GetDirectoryName(projectPath), result.Project);
            return VnSceneComposerSerialization.RevalidateImported(result.Project);
        }

        public static void ExportProject(string destinationPath, VnSceneComposerProject project)
        {
            if (project == null) throw new ArgumentNullException(nameof(project));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentException("Export path is required.", nameof(destinationPath));
            string fullPath = Path.GetFullPath(destinationPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(fullPath, VnSceneComposerSerialization.SerializePortable(project));
        }

        public static VnSceneComposerImportResult ImportProject(string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
                return VnSceneComposerImportResult.Failed("Import path is required.");
            string fullPath;
            try { fullPath = Path.GetFullPath(sourcePath); }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed("Import path is invalid: " + exception.Message);
            }
            if (!File.Exists(fullPath))
                return VnSceneComposerImportResult.Failed("Scene Composer import file does not exist: " + fullPath + ".");
            try
            {
                return VnSceneComposerSerialization.DeserializePortable(File.ReadAllText(fullPath));
            }
            catch (Exception exception)
            {
                return VnSceneComposerImportResult.Failed("Scene Composer import failed: " + exception.Message);
            }
        }

        private static string GetProjectDirectory(string projectRoot, string projectId)
        {
            if (!VnSceneComposerSerialization.IsStableId(projectId))
                throw new ArgumentException("Scene Composer project ID must be a stable 32-character hexadecimal ID.", nameof(projectId));

            string projectsDirectory = GetProjectsDirectory(projectRoot);
            string candidate = Path.GetFullPath(Path.Combine(projectsDirectory, projectId));
            string prefix = projectsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Scene Composer project path escaped the editor-local projects directory.");
            return candidate;
        }

        private static void SaveLocalBindings(string projectDirectory, VnSceneComposerProject project)
        {
            string path = Path.Combine(projectDirectory, LocalBindingsFileName);
            var document = new LocalBindingDocument();
            if (project.scenes != null)
            {
                for (int i = 0; i < project.scenes.Count; i++)
                {
                    VnSceneComposerScene scene = project.scenes[i];
                    if (scene == null || scene.media == null ||
                        !VnSceneComposerSerialization.IsExternalMedia(scene.media.kind)) continue;
                    string reference = scene.media.reference;
                    if (string.IsNullOrWhiteSpace(reference) ||
                        VnSceneComposerSerialization.IsPortableExternalReference(reference) ||
                        !Path.IsPathRooted(reference)) continue;

                    document.bindings.Add(new LocalMediaBinding
                    {
                        sceneId = scene.sceneId ?? string.Empty,
                        kind = scene.media.kind.ToString(),
                        contentHash = scene.media.contentHash ?? string.Empty,
                        absolutePath = Path.GetFullPath(reference)
                    });
                }
            }

            if (document.bindings.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            File.WriteAllText(path, JsonUtility.ToJson(document, true));
        }

        private static void RestoreLocalBindings(string projectDirectory, VnSceneComposerProject project)
        {
            string path = Path.Combine(projectDirectory, LocalBindingsFileName);
            if (!File.Exists(path) || project == null || project.scenes == null) return;

            LocalBindingDocument document;
            try { document = JsonUtility.FromJson<LocalBindingDocument>(File.ReadAllText(path)); }
            catch { return; }
            if (document == null || document.bindings == null) return;

            for (int i = 0; i < document.bindings.Count; i++)
            {
                LocalMediaBinding binding = document.bindings[i];
                if (binding == null || string.IsNullOrEmpty(binding.sceneId) ||
                    string.IsNullOrEmpty(binding.absolutePath) || !Path.IsPathRooted(binding.absolutePath) ||
                    !File.Exists(binding.absolutePath)) continue;

                for (int s = 0; s < project.scenes.Count; s++)
                {
                    VnSceneComposerScene scene = project.scenes[s];
                    if (scene == null || scene.media == null ||
                        !string.Equals(scene.sceneId, binding.sceneId, StringComparison.Ordinal)) continue;
                    if (!string.Equals(scene.media.kind.ToString(), binding.kind, StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(binding.contentHash) && !string.IsNullOrEmpty(scene.media.contentHash) &&
                        !string.Equals(binding.contentHash, scene.media.contentHash, StringComparison.OrdinalIgnoreCase)) continue;
                    scene.media.reference = Path.GetFullPath(binding.absolutePath);
                    scene.media.localPreviewDependency = true;
                    break;
                }
            }
        }
    }
}
