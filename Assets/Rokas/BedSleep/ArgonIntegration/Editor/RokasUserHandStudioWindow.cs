#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.Argon.Editor
{
    /// <summary>
    /// Tunable, persisted calibration of original game Hub -> real Bed shot.
    /// Open during Play Mode; sliders and Scene gizmos work on the live rig.
    /// Profile.asset survives Play Mode/reopening without modifying V16.
    /// </summary>
    public sealed class RokasUserHandStudioWindow : EditorWindow
    {
        public const string ProfilePath =
            "Assets/Rokas/BedSleep/ArgonIntegration/NewUserGLB/ROKAS_HandStudioProfile.asset";
        private RokasUserHandStudioProfile profile;
        private Vector2 scroll;
        private bool draggingHand = true;
        private bool showAdvanced;
        private double nextRepaint;

        [MenuItem("ROKAS/Bed Sleep/Hand Studio - Положение, часы, пальцы")]
        public static void Open()
        {
            var w = GetWindow<RokasUserHandStudioWindow>("ROKAS Hand Studio");
            w.minSize = new Vector2(410, 630);
            w.Show();
        }

        public static RokasUserHandStudioProfile LoadOrCreateProfile()
        {
            var p=AssetDatabase.LoadAssetAtPath<RokasUserHandStudioProfile>(ProfilePath);
            if (p != null) return p;
            string folder=Path.GetDirectoryName(ProfilePath).Replace('\\','/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent="Assets";
                foreach (string part in folder.Substring("Assets/".Length).Split('/'))
                {
                    string child=parent+"/"+part;
                    if (!AssetDatabase.IsValidFolder(child))
                        AssetDatabase.CreateFolder(parent,part);
                    parent=child;
                }
            }
            p=CreateInstance<RokasUserHandStudioProfile>();
            p.ResetToDefaults();
            AssetDatabase.CreateAsset(p,ProfilePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ROKAS HAND STUDIO] Project profile created: "+ProfilePath);
            return p;
        }

        private void OnEnable()
        {
            profile=LoadOrCreateProfile();
            SceneView.duringSceneGui+=OnSceneGUI;
            EditorApplication.update+=UpdateDisplay;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui-=OnSceneGUI;
            EditorApplication.update-=UpdateDisplay;
        }

        private void UpdateDisplay()
        {
            if (EditorApplication.isPlaying && EditorApplication.timeSinceStartup>nextRepaint)
            {
                nextRepaint=EditorApplication.timeSinceStartup+.12;
                Repaint();
            }
        }

        private static RokasUserGLBRealBedFollower ActiveFollower()
        {
            if (!EditorApplication.isPlaying) return null;
            var f=UnityEngine.Object.FindObjectsByType<RokasUserGLBRealBedFollower>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var item in f)
                if (item != null && item.gameObject.activeInHierarchy &&
                    item.HandGizmo != null) return item;
            return null;
        }

        private void Changed()
        {
            EditorUtility.SetDirty(profile);
            SceneView.RepaintAll();
            Repaint();
        }

        private void Float(string label,ref float current,float min,float max)
        {
            float value=EditorGUILayout.Slider(label,current,min,max);
            if (Mathf.Abs(current-value)<.00001f) return;
            Undo.RecordObject(profile,"ROKAS Hand Studio "+label);
            current=value;
            Changed();
        }

        private void Vector(string label,ref Vector3 current)
        {
            Vector3 value=EditorGUILayout.Vector3Field(label,current);
            if (Vector3.Distance(value,current)<.000001f) return;
            Undo.RecordObject(profile,"ROKAS Hand Studio "+label);
            current=value;
            Changed();
        }

        private void Toggle(string label,ref bool current)
        {
            bool value=EditorGUILayout.ToggleLeft(label,current);
            if (value==current) return;
            Undo.RecordObject(profile,"ROKAS Hand Studio "+label);
            current=value;
            Changed();
        }

        private void OnGUI()
        {
            if(profile==null) profile=LoadOrCreateProfile();
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("ROKAS — HAND STUDIO",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Твой новый GLB, настоящие часы ROKAS и исходная сцена кровати. " +
                "Изменения видны во время Play Mode. Никакой замены V16.",MessageType.Info);
            var follow=ActiveFollower();
            EditorGUILayout.LabelField("Сцена: "+(follow!=null
                ? "РУКА В КРОВАТИ — НАСТРАИВАЙ" :
                  "запусти Play и нажми на кровать"),EditorStyles.miniBoldLabel);

            scroll=EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("1. РУКА — РАСПОЛОЖЕНИЕ",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "X < 0 уводит рукав ЗА левый край кадра. Y < 0 — ниже. " +
                "Z меняет глубину. Это реальные оси игровой камеры.",MessageType.None);
            Vector("Сдвиг руки (метры)",ref profile.handCameraOffset);
            Vector("Поворот руки (градусы)",ref profile.handEuler);
            Float("Масштаб руки",ref profile.handScale,.65f,1.35f);
            Float("Яркость кожи/рукава",ref profile.handBrightness,.35f,1.5f);

            EditorGUILayout.Space(9);
            EditorGUILayout.LabelField("2. ЧАСЫ — НА ГОЛОМ ЗАПЯСТЬЕ",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Смещай часы ВПРАВО от манжеты. Корпус, ремешок, " +
                "экран, заводная головка перемещаются ВМЕСТЕ.",MessageType.None);
            Float("К пальцам от манжеты (м)",ref profile.watchTowardsFingers,-.06f,.18f);
            Vector("Сдвиг на запястье (м)",ref profile.watchWristOffset);
            Vector("Поворот часов (градусы)",ref profile.watchEuler);
            Float("Размер часов/ремешка",ref profile.watchScale,.75f,1.25f);
            Float("Яркость OLED",ref profile.oledBrightness,.25f,2f);

            EditorGUILayout.Space(9);
            EditorGUILayout.LabelField("3. ПАЛЬЦЫ И МИКРОАНИМАЦИЯ",EditorStyles.boldLabel);
            Float("Большой палец",ref profile.thumbDegrees,0f,5f);
            Float("Указательный",ref profile.indexDegrees,0f,5f);
            Float("Средний",ref profile.middleDegrees,0f,5f);
            Float("Безымянный",ref profile.ringDegrees,0f,5f);
            Float("Мизинец",ref profile.littleDegrees,0f,5f);
            Float("Микродвижение кисти",ref profile.wristDegrees,0f,2f);
            Float("Скорость / плавность",ref profile.motionSpeed,.15f,2.5f);
            Toggle("Заморозить анимацию (для настройки ракурса)",
                ref profile.freezeAnimation);

            EditorGUILayout.Space(8);
            showAdvanced=EditorGUILayout.Foldout(showAdvanced,"4. МАНИПУЛЯТОРЫ В SCENE VIEW",true);
            if(showAdvanced)
            {
                Toggle("Показывать ручки перемещения/вращения",
                    ref profile.showSceneHandles);
                draggingHand=GUILayout.Toolbar(draggingHand?0:1,
                    new []{"Настраивать РУКУ","Настраивать ЧАСЫ"})==0;
                EditorGUILayout.HelpBox("Открой Scene рядом с Game. " +
                    "Перетаскивай цветные стрелки и вращай окружности. " +
                    "Game показывает результат сразу.",MessageType.Info);
                using(new EditorGUI.DisabledScope(follow==null))
                {
                    if(GUILayout.Button("Выделить объект для настройки в Hierarchy"))
                    {
                        Transform obj=draggingHand?follow.HandGizmo:follow.WatchGizmo;
                        if(obj!=null)
                        {
                            Selection.activeGameObject=obj.gameObject;
                            SceneView.lastActiveSceneView?.Frame(
                                new Bounds(obj.position,Vector3.one*.4f),false);
                        }
                    }
                }
            }

            EditorGUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("СОХРАНИТЬ НАСТРОЙКИ",GUILayout.Height(34)))
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                Debug.Log("[ROKAS HAND STUDIO] Saved persisted pose: "+ProfilePath);
                ShowNotification(new GUIContent("Настройки сохранены в проекте"));
            }
            if(GUILayout.Button("СБРОС",GUILayout.Height(34)))
            {
                if(EditorUtility.DisplayDialog("Сбросить Hand Studio?",
                    "Вернуть начальные параметры руки и часов?", "Сбросить","Отмена"))
                {
                    Undo.RecordObject(profile,"Reset ROKAS Hand Studio");
                    profile.ResetToDefaults();
                    Changed();
                    AssetDatabase.SaveAssets();
                }
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Файл настроек:",EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(ProfilePath,
                EditorStyles.wordWrappedMiniLabel,GUILayout.Height(37));
            EditorGUILayout.HelpBox(
                "Параметры сохраняются как .asset в проекте. После выхода из Play " +
                "и нового входа в кровать появятся те же значения. " +
                "Исходные FBX, V16, часы, OLED и сцена остаются неизменными.",
                MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView view)
        {
            if(profile==null || !profile.showSceneHandles) return;
            var f=ActiveFollower();
            if(f==null) return;
            Transform hand=f.HandGizmo;
            Transform watch=f.WatchGizmo;
            Transform marker=draggingHand?hand:watch;
            if(marker==null) return;
            Handles.color=draggingHand?Color.cyan:Color.yellow;
            Handles.Label(marker.position + Vector3.up*.025f,
                draggingHand?"ROKAS HAND":"ROKAS WATCH");
            EditorGUI.BeginChangeCheck();
            Vector3 pos=Handles.PositionHandle(marker.position,marker.rotation);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,draggingHand?"Move hand":"Move watch");
                Vector3 world=pos-marker.position;
                if(draggingHand)
                    profile.handCameraOffset += f.GameplayCamera!=null
                        ? f.GameplayCamera.transform.InverseTransformVector(world)
                        : world;
                else if(f.NativeWrist!=null)
                    profile.watchWristOffset +=
                        Quaternion.Inverse(f.NativeWrist.rotation)*world;
                Changed();
            }
            EditorGUI.BeginChangeCheck();
            Quaternion rot=Handles.RotationHandle(marker.rotation,marker.position);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile,draggingHand?"Rotate hand":"Rotate watch");
                Quaternion relative=Quaternion.Inverse(marker.rotation)*rot;
                if(draggingHand)
                    profile.handEuler =
                        (Quaternion.Euler(profile.handEuler)*relative).eulerAngles;
                else
                    profile.watchEuler =
                        (Quaternion.Euler(profile.watchEuler)*relative).eulerAngles;
                Changed();
            }
        }
    }
}
#endif
