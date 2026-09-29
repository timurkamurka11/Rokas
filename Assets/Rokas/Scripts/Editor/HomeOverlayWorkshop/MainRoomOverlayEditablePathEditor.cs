using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    [CustomEditor(typeof(MainRoomOverlayEditablePath))]
    public sealed class MainRoomOverlayEditablePathEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var path = (MainRoomOverlayEditablePath)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Move this object's points directly in Scene View. Positions are stored in 1920x1080 reference coordinates. Ctrl+S saves the workshop scene.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add point"))
                {
                    Undo.RecordObject(path, "Add outline point");
                    Vector2 seed = path.PointCount > 0 ? path.GetPoint(path.PointCount - 1) + new Vector2(20f, 0f) : new Vector2(960f, 540f);
                    path.AddPoint(seed);
                    EditorUtility.SetDirty(path);
                }

                using (new EditorGUI.DisabledScope(path.PointCount == 0))
                {
                    if (GUILayout.Button("Remove last"))
                    {
                        Undo.RecordObject(path, "Remove outline point");
                        path.RemoveLastPoint();
                        EditorUtility.SetDirty(path);
                    }
                }
            }
        }

        private void OnSceneGUI()
        {
            var path = (MainRoomOverlayEditablePath)target;
            if (path == null || path.PointCount == 0) return;

            Handles.color = new Color(1f, .72f, .28f, 1f);
            for (int index = 0; index < path.PointCount; index++)
            {
                Vector3 world = path.ReferenceToWorld(path.GetPoint(index));
                float size = HandleUtility.GetHandleSize(world) * .045f;
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(world, size, Vector3.zero, Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck()) continue;

                Undo.RecordObject(path, "Move outline point");
                path.SetPoint(index, path.WorldToReference(moved));
                EditorUtility.SetDirty(path);
            }
        }
    }
}
