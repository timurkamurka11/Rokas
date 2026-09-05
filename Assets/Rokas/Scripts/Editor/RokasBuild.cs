using System;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
            if (!assets || !assets.IsComplete()) throw new BuildFailedException("ROKAS: required presentation assets are not assigned.");
            if (!File.Exists(ScenePath)) throw new BuildFailedException("ROKAS: the boot scene is missing.");
            bool sceneEnabled = false;
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path == ScenePath && scene.enabled) sceneEnabled = true;
            if (!sceneEnabled) throw new BuildFailedException("ROKAS: add the enabled Rokas scene to Build Settings.");
            foreach (var texture in new[] { assets.home, assets.portal, assets.subway })
                if (texture.width < 1600 || texture.height < 900) throw new BuildFailedException("ROKAS: room artwork must be HD: " + texture.name);
            if (!assets.homeAmbience || !assets.subwayAmbience || !assets.homeMusic || !assets.missionMusic || !assets.hit || !assets.portalSound || !assets.seal)
                throw new BuildFailedException("ROKAS: audio references are incomplete.");
            Debug.Log("ROKAS asset references and enabled startup scene validated. This is not a PlayMode or Windows runtime check.");
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
