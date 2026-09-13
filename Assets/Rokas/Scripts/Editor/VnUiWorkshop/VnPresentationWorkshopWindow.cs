using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools.VnUiWorkshop
{
    public sealed class VnPresentationWorkshopWindow : EditorWindow
    {
        public const string MenuPath = "ROKAS/VN UI Workshop";

        [MenuItem(MenuPath)]
        public static void Open()
        {
            VnPresentationWorkshopWindow window = GetWindow<VnPresentationWorkshopWindow>();
            window.titleContent = new GUIContent("VN UI Workshop");
            window.minSize = new Vector2(960f, 600f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("VN UI Workshop", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Workshop UI implementation in progress.", MessageType.Info);
        }
    }
}
