using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.Editor
{
    public static class ReactiveCombatActorPreview
    {
        [MenuItem("ROKAS/Combat/Capture Actor Import Previews")]
        public static void Capture()
        {
            ReactiveCombatActorLibrary library =
                AssetDatabase.LoadAssetAtPath<ReactiveCombatActorLibrary>(
                    "Assets/Rokas/Resources/Combat/ReactiveCombatActorLibrary.asset");
            if (library == null)
            {
                Debug.LogError("Build the combat actor library before capturing previews.");
                return;
            }
            string directory = Path.Combine(Path.GetTempPath(), "RokasCombatActorPreviews");
            Directory.CreateDirectory(directory);
            Capture("Keiko", library.keiko, directory);
            Capture("Mina", library.mina, directory);
            Capture("Yokai", library.yokai, directory);
            Debug.Log("Combat actor previews saved in " + directory);
        }

        private static void Capture(string name, ReactiveCombatActorClips actor, string directory)
        {
            if (actor.model == null || actor.idle == null) return;
            GameObject model = Object.Instantiate(actor.model);
            GameObject cameraObject = new GameObject("ImportPreviewCamera");
            GameObject lightObject = new GameObject("ImportPreviewLight");
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                if (actor.material != null)
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterial = actor.material;
                actor.idle.SampleAnimation(model, Mathf.Min(.5f, actor.idle.length * .25f));
                Bounds bounds = default;
                bool found = false;
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                if (!found) { Debug.LogError("No renderer in " + name); return; }

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.13f, .15f, .2f, 1f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.size.y * .64f, bounds.size.x * .85f);
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100f;
                camera.cullingMask = ~0;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 2f;
                lightObject.transform.rotation = Quaternion.Euler(45f, -40f, 0f);
                target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                for (int side = 0; side < 2; side++)
                {
                    float zSign = side == 0 ? -1f : 1f;
                    cameraObject.transform.position =
                        bounds.center + new Vector3(0f, 0f, zSign * Mathf.Max(5f, bounds.size.magnitude * 2f));
                    cameraObject.transform.LookAt(bounds.center);
                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    string sideName = side == 0 ? "minusZ" : "plusZ";
                    File.WriteAllBytes(Path.Combine(directory, name + "_" + sideName + ".png"),
                        pixels.EncodeToPNG());
                }
            }
            finally
            {
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(model);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }
        }
    }
}
