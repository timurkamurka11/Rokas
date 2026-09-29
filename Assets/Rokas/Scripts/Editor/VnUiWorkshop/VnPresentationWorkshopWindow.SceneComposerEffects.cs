using System;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed partial class VnPresentationWorkshopWindow
    {
        private static readonly string[] ReplicaEffectLabels =
        {
            "Нет", "Дрожь", "Импульс", "Вспышка", "Пульсация"
        };

        private void DrawSceneComposerEffectsInspector(VnSceneComposerScene scene)
        {
            EditorGUILayout.LabelField("Эффекты текущей реплики", EditorStyles.boldLabel);
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null)
            {
                EditorGUILayout.HelpBox("Выберите реплику в списке справа.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Эффект запускается в начале выбранной реплики и не меняет переход между сценами.",
                MessageType.None);
            VnSceneComposerReplicaEffect current = beat.replicaEffect ??
                new VnSceneComposerReplicaEffect();
            EditorGUI.BeginChangeCheck();
            var type = (VnSceneComposerReplicaEffectType)EditorGUILayout.Popup(
                "Эффект", (int)current.type, ReplicaEffectLabels);
            float intensity = current.intensity;
            float duration = current.duration;
            float frequency = current.frequency;
            float decay = current.decay;
            Vector2 direction = current.direction;
            Color flashColor = current.flashColor;

            switch (type)
            {
                case VnSceneComposerReplicaEffectType.Shake:
                    intensity = EditorGUILayout.Slider("Сила", intensity, 0f, 1f);
                    duration = EditorGUILayout.Slider("Длительность", duration, .05f, 5f);
                    frequency = EditorGUILayout.Slider("Частота", frequency, 1f, 30f);
                    decay = EditorGUILayout.Slider("Затухание", decay, .25f, 4f);
                    break;
                case VnSceneComposerReplicaEffectType.Punch:
                    intensity = EditorGUILayout.Slider("Сила", intensity, 0f, 1f);
                    duration = EditorGUILayout.Slider("Длительность", duration, .05f, 5f);
                    direction = EditorGUILayout.Vector2Field("Направление", direction);
                    break;
                case VnSceneComposerReplicaEffectType.Flash:
                    flashColor = EditorGUILayout.ColorField("Цвет", flashColor);
                    intensity = EditorGUILayout.Slider("Яркость", intensity, 0f, 1f);
                    duration = EditorGUILayout.Slider("Длительность", duration, .05f, 5f);
                    break;
                case VnSceneComposerReplicaEffectType.Pulse:
                    intensity = EditorGUILayout.Slider("Сила", intensity, 0f, 1f);
                    duration = EditorGUILayout.Slider("Длительность", duration, .05f, 5f);
                    break;
            }
            if (EditorGUI.EndChangeCheck())
                ComposerSetSelectedReplicaEffect(
                    type, intensity, duration, frequency, decay, direction, flashColor);

            using (new EditorGUI.DisabledScope(type == VnSceneComposerReplicaEffectType.None))
                if (GUILayout.Button("▶ Проверить")) ComposerPreviewSelectedReplicaEffect();
            if (type != VnSceneComposerReplicaEffectType.None && GUILayout.Button("Сбросить"))
                ComposerSetSelectedReplicaEffect(
                    VnSceneComposerReplicaEffectType.None, intensity, duration,
                    frequency, decay, direction, flashColor);
        }

        public void ComposerSetSelectedReplicaEffect(
            VnSceneComposerReplicaEffectType type, float intensity, float duration,
            float frequency, float decay, Vector2 direction, Color flashColor)
        {
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            if (beat == null) throw new InvalidOperationException("No dialogue Beat is selected.");
            if (!Enum.IsDefined(typeof(VnSceneComposerReplicaEffectType), type))
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
            RecordSceneComposerUndo("Edit VN Replica Effect");
            if (beat.replicaEffect == null) beat.replicaEffect = new VnSceneComposerReplicaEffect();
            beat.replicaEffect.type = type;
            beat.replicaEffect.intensity = Mathf.Clamp01(intensity);
            beat.replicaEffect.duration = Mathf.Clamp(duration, .05f, 5f);
            beat.replicaEffect.frequency = Mathf.Clamp(frequency, 1f, 30f);
            beat.replicaEffect.decay = Mathf.Clamp(decay, .25f, 4f);
            beat.replicaEffect.direction = direction.sqrMagnitude > .0001f
                ? direction.normalized : Vector2.right;
            beat.replicaEffect.flashColor = flashColor;
            MarkSceneComposerChanged();
        }

        public void ComposerPreviewSelectedReplicaEffect()
        {
            int sceneIndex = GetSelectedSceneIndexOrThrow();
            VnSceneComposerScene scene = _sceneComposerProject.scenes[sceneIndex];
            VnSceneComposerDialogueBeat beat = ComposerGetSelectedDialogueBeat();
            int beatIndex = VnSceneComposerDialogue.FindIndex(
                scene, beat != null ? beat.beatId : string.Empty);
            if (beatIndex < 0) throw new InvalidOperationException("No dialogue Beat is selected.");
            EnsureSceneComposerPlayback().PlayFromHere(sceneIndex, beatIndex);
            BeginSceneComposerPlaybackTick();
        }
    }
}
