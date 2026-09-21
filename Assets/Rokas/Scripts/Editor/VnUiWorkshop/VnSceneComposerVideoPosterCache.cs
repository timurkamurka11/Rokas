using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerVideoPosterCache
    {
        private const string Folder =
            VnSceneComposerAssetLibrary.ManagedRootRelative + "/VideoPosters";

        internal static Texture2D TryLoad(VnSceneComposerMediaReference media)
        {
            string path = PosterAssetPath(media);
            return string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static void CaptureIfMissing(
            VnSceneComposerMediaReference media, RenderTexture source)
        {
            string assetPath = PosterAssetPath(media);
            if (string.IsNullOrEmpty(assetPath) || source == null || !source.IsCreated())
                return;
            string absolute = Path.Combine(
                VnSceneComposerAssetLibrary.GetDefaultProjectRoot(),
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(absolute)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            RenderTexture previous = RenderTexture.active;
            Texture2D poster = null;
            try
            {
                RenderTexture.active = source;
                poster = new Texture2D(
                    source.width, source.height, TextureFormat.RGB24, false);
                poster.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height), 0, 0, false);
                poster.Apply(false, false);
                File.WriteAllBytes(absolute, poster.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (poster != null) Object.DestroyImmediate(poster);
            }
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static string PosterAssetPath(VnSceneComposerMediaReference media)
        {
            if (media == null ||
                media.kind != VnSceneComposerMediaKind.ExternalVideo)
                return string.Empty;
            string hash = new string((media.contentHash ?? string.Empty)
                .Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (hash.Length == 0) return string.Empty;
            if (hash.Length > 32) hash = hash.Substring(0, 32);
            return Folder + "/" + hash + ".png";
        }
    }
}
