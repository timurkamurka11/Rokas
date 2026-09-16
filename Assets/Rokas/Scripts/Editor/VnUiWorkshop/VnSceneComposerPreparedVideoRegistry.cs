using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerPreparedVideoRegistry
    {
        private sealed class Entry
        {
            internal readonly string Signature;
            internal readonly WeakReference Preview;

            internal Entry(string signature, VnSceneComposerVideoPreview preview)
            {
                Signature = signature ?? string.Empty;
                Preview = new WeakReference(preview);
            }
        }

        private static readonly ConditionalWeakTable<VnSceneComposerProject, Dictionary<string, Entry>> ByProject =
            new ConditionalWeakTable<VnSceneComposerProject, Dictionary<string, Entry>>();

        internal static void Prime(VnSceneComposerProject project, VnSceneComposerScene scene,
            VnSceneComposerVideoPreview preview)
        {
            if (project == null || scene == null || preview == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo || string.IsNullOrEmpty(scene.sceneId))
                return;

            Dictionary<string, Entry> entries = ByProject.GetOrCreateValue(project);
            entries[scene.sceneId] = new Entry(BuildSignature(scene.media), preview);
        }

        internal static bool TryBorrow(VnSceneComposerProject project, VnSceneComposerScene scene,
            out VnSceneComposerVideoPreview preview)
        {
            preview = null;
            if (project == null || scene == null || scene.media == null ||
                scene.media.kind != VnSceneComposerMediaKind.ExternalVideo || string.IsNullOrEmpty(scene.sceneId))
                return false;

            if (!ByProject.TryGetValue(project, out Dictionary<string, Entry> entries) ||
                !entries.TryGetValue(scene.sceneId, out Entry entry) || entry == null)
                return false;

            if (!string.Equals(entry.Signature, BuildSignature(scene.media), StringComparison.Ordinal))
            {
                entries.Remove(scene.sceneId);
                return false;
            }

            preview = entry.Preview.Target as VnSceneComposerVideoPreview;
            if (preview == null || preview.texture == null || !string.IsNullOrEmpty(preview.warning) ||
                (!preview.IsPrepared && !preview.IsPreparing))
            {
                entries.Remove(scene.sceneId);
                preview = null;
                return false;
            }

            return true;
        }

        private static string BuildSignature(VnSceneComposerMediaReference media)
        {
            if (media == null) return string.Empty;
            return (media.reference ?? string.Empty) + "|" +
                   (media.contentHash ?? string.Empty) + "|" + media.loop;
        }
    }
}
