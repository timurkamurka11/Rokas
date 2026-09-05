using System;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rokas.Editor
{
    public static class RokasBuild
    {
        public const string ScenePath = "Assets/Rokas/Scenes/Rokas.unity";

        [MenuItem("Rokas/Validate Project")]
        public static void ValidateProject()
        {
            var assets = AssetDatabase.LoadAssetAtPath<RokasAssets>("Assets/Rokas/Resources/RokasAssets.asset");
            if (!assets || !assets.IsComplete()) throw new BuildFailedException("ROKAS: required artwork, fonts, contract or audio did not import or are not assigned.");
            if (!File.Exists(ScenePath)) throw new BuildFailedException("ROKAS: the boot scene is missing.");
            bool sceneEnabled = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path == ScenePath && scene.enabled) sceneEnabled = true;
            if (!sceneEnabled) throw new BuildFailedException("ROKAS: add the enabled Rokas scene to Build Settings.");
            foreach (var texture in new[] { assets.home, assets.portal, assets.subway })
                if (texture.width < 1600 || texture.height < 900) throw new BuildFailedException("ROKAS: room artwork must be HD: " + texture.name);
            ValidateSceneImport();
            Debug.Log("ROKAS imported asset references, startup scene and bootstrap validated. This is not a PlayMode or Windows runtime check.");
        }

        private static void ValidateSceneImport()
        {
            // Inspect the committed scene without replacing or saving the user's open scene.
            var preview = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                if (!preview.IsValid() || !preview.isLoaded)
                    throw new BuildFailedException("ROKAS: the startup scene could not be imported.");
                int bootstrapCount = 0;
                foreach (var root in preview.GetRootGameObjects())
                {
                    foreach (var child in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                            throw new BuildFailedException("ROKAS: missing script in startup scene on " + child.name);
                    foreach (var bootstrap in root.GetComponentsInChildren<RokasBootstrap>(true))
                    {
                        bootstrapCount++;
                        if (!bootstrap.enabled || !bootstrap.gameObject.activeInHierarchy)
                            throw new BuildFailedException("ROKAS: the startup bootstrap must be active and enabled.");
                    }
                }
                if (bootstrapCount != 1)
                    throw new BuildFailedException("ROKAS: startup scene must contain exactly one RokasBootstrap, found " + bootstrapCount);
            }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        [MenuItem("Rokas/Build Windows x64")]
        public static void BuildWindows()
        {
            ValidateProject();
            string destination = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Windows", "Rokas.exe");
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-rokasBuildPath") destination = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("ROKAS Windows build failed: " + report.summary.result);
            Debug.Log("ROKAS Windows player built: " + destination + ". Launch it separately to validate runtime.");
        }

        [MenuItem("Rokas/Open Game Scene")]
        public static void OpenGameScene()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
