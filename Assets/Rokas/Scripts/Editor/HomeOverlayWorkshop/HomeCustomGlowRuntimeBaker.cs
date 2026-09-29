#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Rokas.EditorTools.HomeOverlayWorkshop
{
    public static class HomeCustomGlowRuntimeBaker
    {
        private const string WorkshopScenePath =
            "Assets/Rokas/EditorWorkshops/MainRoomOverlayWorkshop.unity";

        private const string ShaderPath =
            "Assets/Rokas/Resources/HomeCustomGlowRuntime.shader";

        private const string MaterialPath =
            "Assets/Rokas/Resources/HomeCustomGlowRuntimeMaterial.mat";

        private const string LayoutPath =
            "Assets/Rokas/Resources/HomeCustomGlowRuntimeLayout.asset";

        public static void BakeFromCommandLine()
        {
            Bake();
        }

        public static void Bake()
        {
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);

            Scene scene =
                EditorSceneManager.OpenScene(
                    WorkshopScenePath,
                    OpenSceneMode.Single);

            GameObject workshopRoot = null;

            foreach (GameObject candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == "MainRoomOverlayWorkshop")
                {
                    workshopRoot = candidate;
                    break;
                }
            }

            if (!workshopRoot)
                throw new InvalidOperationException(
                    "MainRoomOverlayWorkshop root is missing.");

            Transform customGlow =
                workshopRoot.transform.Find("WorkshopCanvas/CustomGlow");

            var entries =
                new List<HomeCustomGlowRuntimeEntry>();

            if (customGlow)
            {
                MainRoomOverlayCustomGlowElement[] markers =
                    customGlow.GetComponentsInChildren<
                        MainRoomOverlayCustomGlowElement>(true);

                for (int i = 0; i < markers.Length; i++)
                {
                    MainRoomOverlayCustomGlowElement marker = markers[i];
                    RectTransform rect =
                        marker.transform as RectTransform;
                    Image image = marker.GetComponent<Image>();

                    if (!rect || !image || !image.sprite)
                        continue;

                    MainRoomOverlayGlowFx authoredFx =
                        marker.GetComponent<MainRoomOverlayGlowFx>();

                    bool haze = marker.HazeElement;

                    entries.Add(
                        new HomeCustomGlowRuntimeEntry
                        {
                            stableId =
                                string.IsNullOrWhiteSpace(marker.StableId)
                                ? marker.name
                                : marker.StableId,

                            sprite = image.sprite,
                            anchoredPosition = rect.anchoredPosition,
                            sizeDelta = rect.sizeDelta,
                            anchorMin = rect.anchorMin,
                            anchorMax = rect.anchorMax,
                            pivot = rect.pivot,
                            localScale = rect.localScale,
                            rotationZ = rect.localEulerAngles.z,
                            active = marker.gameObject.activeSelf,
                            color = image.color,
                            preserveAspect = image.preserveAspect,
                            imageType = (int)image.type,
                            fillCenter = image.fillCenter,
                            hazeElement = haze,

                            // Geometry above is copied exactly.
                            // Only visual movement/glow parameters are tuned.
                            brightness =
                                haze
                                ? (authoredFx ? authoredFx.Brightness : 1f)
                                : Mathf.Max(
                                    1.06f,
                                    authoredFx
                                        ? authoredFx.Brightness
                                        : 1f),

                            pulseEnabled = !haze,
                            pulseSpeed = .24f,
                            pulseAmount = .06f,

                            shimmerEnabled = !haze,
                            shimmerSpeed = .14f,
                            shimmerAmount = .24f,
                            shimmerWidth = .14f,

                            hazeEnabled =
                                haze &&
                                authoredFx &&
                                authoredFx.HazeEnabled,

                            hazeOpacity =
                                authoredFx
                                ? authoredFx.HazeOpacity
                                : .16f,

                            phaseOffset =
                                markers.Length <= 1
                                ? 0f
                                : (float)i / markers.Length
                        });
                }
            }

            Material material = null;

            if (entries.Count > 0)
            {
                Shader shader =
                    AssetDatabase.LoadAssetAtPath<Shader>(
                        ShaderPath);

                if (!shader)
                    throw new InvalidOperationException(
                        "CustomGlow runtime shader is missing: " +
                        ShaderPath);

                material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        MaterialPath);

                if (!material)
                {
                    material =
                        new Material(shader)
                        {
                            name =
                                "ROKAS Home CustomGlow Runtime"
                        };

                    AssetDatabase.CreateAsset(
                        material,
                        MaterialPath);
                }
                else
                {
                    material.shader = shader;
                    EditorUtility.SetDirty(material);
                }
            }

            HomeCustomGlowRuntimeLayout layout =
                AssetDatabase.LoadAssetAtPath<
                    HomeCustomGlowRuntimeLayout>(LayoutPath);

            if (!layout)
            {
                layout =
                    ScriptableObject.CreateInstance<
                        HomeCustomGlowRuntimeLayout>();

                AssetDatabase.CreateAsset(
                    layout,
                    LayoutPath);
            }

            layout.Configure(material, entries);
            EditorUtility.SetDirty(layout);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport);

            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ??
                ".";

            string readyDir =
                Path.Combine(projectRoot, "Library", "ROKAS");

            Directory.CreateDirectory(readyDir);

            File.WriteAllText(
                Path.Combine(
                    readyDir,
                    "HOME_CUSTOMGLOW_R12_BAKE.txt"),
                "PASS\ncount=" + entries.Count + "\n",
                new System.Text.UTF8Encoding(false));

            Debug.Log(
                "ROKAS_HOME_CUSTOMGLOW_R12_BAKE=PASS count=" +
                entries.Count);
        }
    }
}
#endif
