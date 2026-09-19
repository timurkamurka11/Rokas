using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    internal static class VnSceneComposerAudioAudition
    {
        private static readonly Type AudioUtilType = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");

        public static bool TryPlay(AudioClip clip, bool loop, out string error)
        {
            error = string.Empty;
            if (clip == null)
            {
                error = "Не выбран музыкальный трек.";
                return false;
            }
            try
            {
                Stop();
                MethodInfo play = AudioUtilType != null
                    ? AudioUtilType.GetMethod("PlayPreviewClip",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                    : null;
                if (play == null)
                {
                    error = "Unity Audio Preview API недоступен в этой версии редактора.";
                    return false;
                }
                play.Invoke(null, new object[] { clip, 0, loop });
                return true;
            }
            catch (Exception exception)
            {
                error = exception.InnerException != null ? exception.InnerException.Message : exception.Message;
                return false;
            }
        }

        public static void Stop()
        {
            if (AudioUtilType == null) return;
            MethodInfo stop = AudioUtilType.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (stop != null) stop.Invoke(null, null);
        }
    }
}
