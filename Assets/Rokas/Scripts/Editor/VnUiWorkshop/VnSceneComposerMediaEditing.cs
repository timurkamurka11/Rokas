using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnSceneComposerImagePreview : IDisposable
    {
        public Texture2D texture;
        public string warning;
        public bool ownsTexture;

        internal VnSceneComposerImagePreview(Texture2D texture, string warning, bool ownsTexture)
        {
            this.texture = texture;
            this.warning = warning ?? string.Empty;
            this.ownsTexture = ownsTexture;
        }

        public void Dispose()
        {
            if (ownsTexture && texture != null) UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
            ownsTexture = false;
        }
    }

    public static partial class VnSceneComposerMediaEditing
    {
        public static void SetExistingRokasAsset(VnSceneComposerScene scene, UnityEngine.Object asset,
            VnSceneComposerMediaScaleMode scaleMode)
        {
            RequireScene(scene);
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (!(asset is Texture2D) && !(asset is Sprite))
                throw new ArgumentException("Scene Composer image media must be a Texture2D or Sprite.", nameof(asset));

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException("Existing ROKAS media must be a project asset.", nameof(asset));
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException("Existing ROKAS media must have a stable asset GUID.", nameof(asset));

            scene.media = new VnSceneComposerMediaReference
            {
                kind = VnSceneComposerMediaKind.ExistingRokasAsset,
                reference = guid,
                displayName = asset.name ?? string.Empty,
                contentHash = AssetDatabase.GetAssetDependencyHash(path).ToString(),
                localPreviewDependency = false,
                scaleMode = scaleMode
            };
        }

        public static void SetExternalImage(VnSceneComposerScene scene, string path,
            VnSceneComposerMediaScaleMode scaleMode)
        {
            RequireScene(scene);
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Image path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            RequireImageExtension(fullPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Selected image is missing.", fullPath);

            VnSceneComposerAssetOnboardResult imported = VnSceneComposerAssetLibrary.Onboard(
                VnSceneComposerAssetLibrary.GetDefaultProjectRoot(), fullPath,
                VnSceneComposerAssetPurpose.Background, Path.GetFileNameWithoutExtension(fullPath),
                string.Empty, string.Empty);
            if (!imported.Success || imported.Entry == null)
                throw new IOException("Could not import Scene image into Assets: " + imported.Error);
            VnSceneComposerAssetEntry entry = imported.Entry;
            string importedPath = AssetDatabase.GUIDToAssetPath(entry.assetGuid);
            if (string.IsNullOrEmpty(importedPath) ||
                !string.Equals(importedPath, entry.assetPath, StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadAssetAtPath<Texture2D>(importedPath) == null)
                throw new IOException("Imported Scene image has no readable project asset and GUID: " + entry.assetPath);

            scene.media = new VnSceneComposerMediaReference
            {
                kind = VnSceneComposerMediaKind.ExistingRokasAsset,
                reference = entry.assetGuid,
                displayName = Path.GetFileName(fullPath),
                contentHash = entry.contentHash,
                localPreviewDependency = false,
                scaleMode = scaleMode
            };
        }

        public static VnSceneComposerImagePreview OpenImagePreview(VnSceneComposerMediaReference media)
        {
            if (media == null) return new VnSceneComposerImagePreview(null, "Image media reference is missing.", false);
            if (media.kind == VnSceneComposerMediaKind.ExistingRokasAsset)
                return OpenProjectAsset(media);
            if (media.kind == VnSceneComposerMediaKind.ExternalImage)
                return OpenExternalImage(media);
            return new VnSceneComposerImagePreview(null, "Selected media is not an image.", false);
        }

        public static ScaleMode ToUnityScaleMode(VnSceneComposerMediaScaleMode scaleMode)
        {
            switch (scaleMode)
            {
                case VnSceneComposerMediaScaleMode.Fill:
                    return ScaleMode.ScaleAndCrop;
                case VnSceneComposerMediaScaleMode.Stretch:
                    return ScaleMode.StretchToFill;
                default:
                    return ScaleMode.ScaleToFit;
            }
        }

        public static void DrawImagePreview(Rect viewport, VnSceneComposerMediaReference media,
            VnSceneComposerImagePreview preview)
        {
            GUI.BeginGroup(viewport);
            Rect local = new Rect(0f, 0f, viewport.width, viewport.height);
            if (preview != null && preview.texture != null)
                GUI.DrawTexture(local, preview.texture, ToUnityScaleMode(media != null ? media.scaleMode : VnSceneComposerMediaScaleMode.Fit), true);
            if (preview != null && !string.IsNullOrEmpty(preview.warning))
                GUI.Label(local, preview.warning);
            GUI.EndGroup();
        }

        private static VnSceneComposerImagePreview OpenProjectAsset(VnSceneComposerMediaReference media)
        {
            if (string.IsNullOrEmpty(media.reference))
                return new VnSceneComposerImagePreview(null, "ROKAS asset GUID is missing.", false);
            string path = AssetDatabase.GUIDToAssetPath(media.reference);
            if (string.IsNullOrEmpty(path))
                return new VnSceneComposerImagePreview(null, "ROKAS image asset is missing: " + media.reference, false);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) texture = sprite.texture;
            }
            return texture != null
                ? new VnSceneComposerImagePreview(texture, string.Empty, false)
                : new VnSceneComposerImagePreview(null, "ROKAS asset is not a previewable image: " + path, false);
        }

        private static VnSceneComposerImagePreview OpenExternalImage(VnSceneComposerMediaReference media)
        {
            string path = media.reference;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return new VnSceneComposerImagePreview(null, "External preview image is missing: " + (path ?? string.Empty), false);
            try
            {
                RequireImageExtension(path);
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    return new VnSceneComposerImagePreview(null, "External preview image could not be decoded: " + path, false);
                }
                return new VnSceneComposerImagePreview(texture, string.Empty, true);
            }
            catch (Exception exception)
            {
                return new VnSceneComposerImagePreview(null, "External preview image failed: " + exception.Message, false);
            }
        }

        private static void RequireScene(VnSceneComposerScene scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
        }

        private static void RequireImageExtension(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                throw new ArgumentException("Scene Composer external images must be PNG, JPG or JPEG.", nameof(path));
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

    }
}
