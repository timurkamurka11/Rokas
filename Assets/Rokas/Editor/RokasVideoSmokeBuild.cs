using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Rokas.Editor
{
    /// <summary>CI-only build entrypoint for the GitHub-hosted built-player video smoke experiment.</summary>
    public static class RokasVideoSmokeBuild
    {
        public static void Build()
        {
            string buildPath = GetArgument("-customBuildPath");
            if (string.IsNullOrEmpty(buildPath))
                buildPath = Path.Combine("build", "StandaloneWindows64", "Rokas.exe");
            if (!buildPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                buildPath = Path.Combine(buildPath, "Rokas.exe");

            string directory = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("ROKAS has no enabled build scenes.");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            Debug.Log("ROKAS video smoke Development Build path: " + buildPath);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    "ROKAS Windows video smoke build failed: " + report.summary.result +
                    ", errors=" + report.summary.totalErrors +
                    ", warnings=" + report.summary.totalWarnings);

            Debug.Log("ROKAS video smoke Development Build succeeded: " + report.summary.totalSize + " bytes");
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return string.Empty;
        }
    }
}
