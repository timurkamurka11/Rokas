using System;
using System.IO;
using Rokas.Presentation;
using UnityEditor;
using UnityEngine;

namespace Rokas.EditorTools
{
    /// <summary>
    /// Standalone authoring window. No scene rebuild and no modification of
    /// gameplay scripts: a Resources ScriptableObject is the sole source of
    /// configurable journal text, portraits, layout, SFX and animation values.
    /// </summary>
    public sealed class RokasJournalWorkshopWindow : EditorWindow
    {
        private const string SettingsAsset =
            "Assets/Rokas/Resources/Rokas/Journal/JournalSettings.asset";
        private const string PanelAsset =
            "Assets/Rokas/Resources/Rokas/Journal/Journal_Panel.png";
        private const string ClosedAsset =
            "Assets/Rokas/Resources/Rokas/Journal/Journal_Closed.png";
        private const string OpenAsset =
            "Assets/Rokas/Resources/Rokas/Journal/Journal_Open.png";
        private const string SoundAsset =
            "Assets/Rokas/Resources/Rokas/Journal/Journal_OpenSfx.mp3";
        private RokasJournalSettings settings;
        private Vector2 scroll;
        private int page;
        private int preview;
        private int activeHandle;
        private bool dirty;
        private bool dragging;
        private Vector2 dragStart;
        private Rect startingRect;

        private readonly string[] pages =
            { "Artwork & Audio", "Text & Quests", "Layout", "Hover & Motion", "Preview" };
        private readonly string[] handles =
            { "Avatar", "Name", "Quest title", "Description", "Notes", "Category list", "Objectives" };

        [MenuItem("ROKAS/Quest Journal Workshop/Open")]
        public static void Open()
        {
            var window = GetWindow<RokasJournalWorkshopWindow>(
                "ROKAS Quest Journal Workshop");
            window.minSize = new Vector2(900f, 580f);
            window.Show();
            window.Focus();
        }

        [MenuItem("ROKAS/Quest Journal Workshop/Select Settings Asset")]
        public static void SelectSettings()
        {
            RokasJournalSettings asset = LoadOrCreate();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private void OnEnable()
        {
            settings = LoadOrCreate();
        }

        private static RokasJournalSettings LoadOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RokasJournalSettings>(
                SettingsAsset);
            if (existing != null) return existing;
            string folder = Path.GetDirectoryName(SettingsAsset).Replace('\\', '/');
            string[] parts = folder.Split('/');
            string cumulative = parts[0];
            for (int i = 1; i < parts.Length; ++i)
            {
                if (!AssetDatabase.IsValidFolder(cumulative + "/" + parts[i]))
                    AssetDatabase.CreateFolder(cumulative, parts[i]);
                cumulative += "/" + parts[i];
            }
            var config = CreateInstance<RokasJournalSettings>();
            config.Normalize();
            config.openSound =
                AssetDatabase.LoadAssetAtPath<AudioClip>(SoundAsset);
            AssetDatabase.CreateAsset(config, SettingsAsset);
            AssetDatabase.SaveAssets();
            return config;
        }

        private void OnGUI()
        {
            if (settings == null)
            {
                if (GUILayout.Button("Create Journal Settings"))
                    settings = LoadOrCreate();
                return;
            }

            DrawHeader();
            page = GUILayout.Toolbar(page, pages, GUILayout.Height(29));
            GUILayout.Space(6f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(
                position.width * .43f, 360f, 510f)));
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUI.BeginChangeCheck();
            switch (page)
            {
                case 0: DrawArtwork(); break;
                case 1: DrawContent(); break;
                case 2: DrawLayout(); break;
                case 3: DrawAnimation(); break;
                case 4: DrawPreviewSettings(); break;
            }
            if (EditorGUI.EndChangeCheck())
            {
                settings.Normalize();
                EditorUtility.SetDirty(settings);
                dirty = true;
                Repaint();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            GUILayout.Space(12f);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawVisualPreview();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            DrawFooter();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(
                "ROKAS  /  QUEST JOURNAL  /  UI AUTHORING",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Настройки сохраняются отдельно от PNG и кода. " +
                "Вводи тексты и перетаскивай изображения в поля ниже. " +
                "Изменения появятся при следующем запуске Play Mode.",
                MessageType.Info);
        }

        private void DrawArtwork()
        {
            Section("АВАТАР И ТЕКСТУРЫ");
            settings.avatar = (Texture2D)EditorGUILayout.ObjectField(
                "Avatar (в круг)", settings.avatar,
                typeof(Texture2D), false);
            settings.showAvatar = EditorGUILayout.Toggle(
                "Показывать аватар", settings.showAvatar);
            settings.avatarTint = EditorGUILayout.ColorField(
                "Оттенок", settings.avatarTint);
            settings.avatarRect = RectField("Круглая область", settings.avatarRect);
            GUILayout.Space(8);
            Section("ЗВУК КНИГИ");
            settings.openSound = (AudioClip)EditorGUILayout.ObjectField(
                "При открытии", settings.openSound, typeof(AudioClip), false);
            settings.openVolume = EditorGUILayout.Slider(
                "Громкость", settings.openVolume, 0f, 1f);
            if (settings.openSound == null)
            {
                EditorGUILayout.HelpBox(
                    "Если поле пустое, игра использует Journal_OpenSfx.mp3 " +
                    "из Resources. При выборе клипа здесь он имеет приоритет.",
                    MessageType.None);
            }
            GUILayout.Space(8);
            Section("ОРИГИНАЛЬНЫЕ PNG");
            AssetStatus(ClosedAsset);
            AssetStatus(OpenAsset);
            AssetStatus(PanelAsset);
            AssetStatus(SoundAsset);
            if (GUILayout.Button("Показать папку ресурсов"))
                EditorUtility.RevealInFinder(
                    Path.GetFullPath("Assets/Rokas/Resources/Rokas/Journal"));
        }

        private void DrawContent()
        {
            Section("ДИНАМИЧЕСКИЙ ТЕКСТ");
            settings.showText = EditorGUILayout.Toggle(
                "Включить текст", settings.showText);
            settings.font = (Font)EditorGUILayout.ObjectField(
                "Шрифт", settings.font, typeof(Font), false);
            settings.textColor = EditorGUILayout.ColorField(
                "Основной цвет", settings.textColor);
            settings.mutedTextColor = EditorGUILayout.ColorField(
                "Вторичный цвет", settings.mutedTextColor);
            settings.headingFontSize = EditorGUILayout.IntSlider(
                "Заголовок", settings.headingFontSize, 12, 60);
            settings.bodyFontSize = EditorGUILayout.IntSlider(
                "Текст / описание", settings.bodyFontSize, 12, 42);
            settings.categoryFontSize = EditorGUILayout.IntSlider(
                "Категории", settings.categoryFontSize, 10, 40);
            GUILayout.Space(8);
            Section("ОПИСАНИЕ И ИМЯ");
            settings.characterName = EditorGUILayout.TextField(
                "Имя у аватара", settings.characterName);
            settings.journalTitle = EditorGUILayout.TextField(
                "Название квеста", settings.journalTitle);
            EditorGUILayout.LabelField("Описание");
            settings.questDescription = EditorGUILayout.TextArea(
                settings.questDescription, GUILayout.MinHeight(88));
            EditorGUILayout.LabelField("Примечания / подсказка");
            settings.notes = EditorGUILayout.TextArea(
                settings.notes, GUILayout.MinHeight(54));
            GUILayout.Space(8);
            Section("ШЕСТЬ ВКЛАДОК");
            settings.Normalize();
            for (int i = 0; i < 6; ++i)
                settings.categoryTitles[i] = EditorGUILayout.TextField(
                    "Вкладка " + (i + 1), settings.categoryTitles[i]);
            GUILayout.Space(8);
            Section("ЧЕТЫРЕ ПУНКТА ЦЕЛИ");
            for (int i = 0; i < 4; ++i)
                settings.questObjectives[i] = EditorGUILayout.TextField(
                    "Цель " + (i + 1), settings.questObjectives[i]);
            settings.showCheckboxes = EditorGUILayout.Toggle(
                "Маркеры целей", settings.showCheckboxes);
            EditorGUILayout.HelpBox(
                "Здесь задаётся только содержимое UI. " +
                "Завершение квестов и сохранения пока не меняются.",
                MessageType.None);
        }

        private void DrawLayout()
        {
            Section("ПОЛОЖЕНИЕ ИКОНКИ В ЛЕВОМ ВЕРХНЕМ УГЛУ");
            settings.iconPosition = EditorGUILayout.Vector2Field(
                "Отступ X / Y", settings.iconPosition);
            settings.iconSize = EditorGUILayout.Slider(
                "Размер книги", settings.iconSize, 80, 320);
            GUILayout.Space(8);
            Section("ОКНО ЖУРНАЛА");
            settings.panelPosition = EditorGUILayout.Vector2Field(
                "Позиция окна", settings.panelPosition);
            settings.panelSize = EditorGUILayout.Vector2Field(
                "Размер окна", settings.panelSize);
            settings.backdropDim = EditorGUILayout.Slider(
                "Затемнение хаба", settings.backdropDim, 0f, .9f);
            settings.selectionColor = EditorGUILayout.ColorField(
                "Выбранная строка", settings.selectionColor);
            GUILayout.Space(8);
            Section("ОБЛАСТИ — МОЖНО ПЕРЕТАСКИВАТЬ В ПРЕВЬЮ");
            activeHandle = EditorGUILayout.Popup(
                "Перемещаемая область", activeHandle, handles);
            settings.avatarRect = RectField("Аватар", settings.avatarRect);
            settings.characterNameRect = RectField("Имя", settings.characterNameRect);
            settings.journalTitleRect = RectField("Название", settings.journalTitleRect);
            settings.questDescriptionRect = RectField(
                "Описание", settings.questDescriptionRect);
            settings.notesRect = RectField("Примечания", settings.notesRect);
            settings.categoryListRect = RectField(
                "Категории", settings.categoryListRect);
            settings.objectivesStartRect = RectField(
                "Список целей", settings.objectivesStartRect);
            settings.categoryStep = EditorGUILayout.Slider(
                "Шаг строк", settings.categoryStep, 40f, 130f);
            settings.objectiveSpacing = EditorGUILayout.Slider(
                "Шаг целей", settings.objectiveSpacing, 26f, 85f);
        }

        private void DrawAnimation()
        {
            Section("РАСКРЫТИЕ ПРИ HOVER");
            settings.hoverScale = EditorGUILayout.Slider(
                "Увеличение", settings.hoverScale, 0f, .25f);
            settings.hoverRise = EditorGUILayout.Slider(
                "Первичный подъём", settings.hoverRise, 0f, 20f);
            settings.hoverFloatAmplitude = EditorGUILayout.Slider(
                "Амплитуда левитации", settings.hoverFloatAmplitude, 0f, 18f);
            settings.hoverFloatHz = EditorGUILayout.Slider(
                "Частота движения (Гц)", settings.hoverFloatHz, .1f, 5f);
            settings.hoverResponse = EditorGUILayout.Slider(
                "Плавность смены книги", settings.hoverResponse, 1f, 24f);
            settings.hoverGlow = EditorGUILayout.Slider(
                "Подсветка", settings.hoverGlow, 0f, 1f);
            GUILayout.Space(9);
            Section("ОТКРЫТИЕ БОЛЬШОГО МЕНЮ");
            settings.openSpeed = EditorGUILayout.Slider(
                "Скорость", settings.openSpeed, .2f, 15f);
            settings.panelSlide = EditorGUILayout.Slider(
                "Смещение входа", settings.panelSlide, 0f, 50f);
            settings.panelScaleIntro = EditorGUILayout.Slider(
                "Масштаб при входе", settings.panelScaleIntro, 0f, .2f);
            EditorGUILayout.HelpBox(
                "Анимация работает через Time.unscaledDeltaTime; " +
                "не влияет на игровой таймер и не создаёт новых объектов за кадр.",
                MessageType.None);
        }

        private void DrawPreviewSettings()
        {
            Section("ИНТЕРАКТИВНЫЙ ПРЕДПРОСМОТР");
            preview = GUILayout.Toolbar(preview,
                new [] { "Закрытая", "Hover", "Окно журнала" });
            activeHandle = EditorGUILayout.Popup(
                "Перетаскивать в превью", activeHandle, handles);
            EditorGUILayout.HelpBox(
                "В режиме «Окно журнала» цветные рамки обозначают только " +
                "редактируемые области мастерской. В игре они не рисуются. " +
                "При перетаскивании меняются сохранённые координаты.",
                MessageType.Info);
            if (GUILayout.Button("Выбрать Settings asset"))
            {
                Selection.activeObject = settings;
                EditorGUIUtility.PingObject(settings);
            }
        }

        private void DrawVisualPreview()
        {
            Section("ПРЕДПРОСМОТР");
            if (page != 4)
                preview = GUILayout.Toolbar(preview,
                    new [] { "Закрытая", "Hover", "Окно журнала" });
            Rect full = GUILayoutUtility.GetRect(
                330f, 500f, GUILayout.ExpandWidth(true));
            GUI.Box(full, GUIContent.none, EditorStyles.helpBox);

            if (preview != 2)
            {
                string path = preview == 0 ? ClosedAsset : OpenAsset;
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (icon != null)
                {
                    float sz = Mathf.Min(full.width - 36f, full.height - 70f, 270f);
                    Rect image = new Rect(
                        full.center.x - sz * .5f, full.y + 35f, sz, sz);
                    GUI.DrawTexture(image, icon, ScaleMode.ScaleToFit, true);
                }
                else GUI.Label(full, "PNG ещё не импортирован",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(PanelAsset);
            if (background == null)
            {
                GUI.Label(full, "Journal_Panel.png не найден",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }
            float aspect = settings.panelSize.x / settings.panelSize.y;
            float width = Mathf.Min(full.width - 24, (full.height - 24) * aspect);
            float height = width / aspect;
            Rect area = new Rect(
                full.center.x - width * .5f,
                full.center.y - height * .5f, width, height);
            GUI.DrawTexture(area, background, ScaleMode.StretchToFill, true);

            float sx = area.width / settings.panelSize.x;
            float sy = area.height / settings.panelSize.y;
            DrawPreviewLabel(settings.characterName, settings.characterNameRect,
                area, sx, sy, settings.mutedTextColor);
            DrawPreviewLabel(settings.journalTitle, settings.journalTitleRect,
                area, sx, sy, settings.textColor);
            DrawPreviewLabel(settings.questDescription,
                settings.questDescriptionRect, area, sx, sy, settings.textColor);
            DrawPreviewLabel(settings.notes, settings.notesRect,
                area, sx, sy, settings.mutedTextColor);
            for (int i = 0; i < 6; i++)
            {
                var r = settings.categoryListRect;
                r.y += settings.categoryStep * i;
                DrawPreviewLabel(RokasJournalSettings.CategoryAt(
                    settings.categoryTitles, i), r,
                    area, sx, sy, settings.textColor);
            }
            if (settings.avatar != null && settings.showAvatar)
            {
                Rect r = ToScreen(settings.avatarRect, area, sx, sy);
                GUI.DrawTexture(r, settings.avatar, ScaleMode.ScaleAndCrop, true);
            }
            DrawHandles(area, sx, sy);
        }

        private void DrawPreviewLabel(string value, Rect rect, Rect area,
            float sx, float sy, Color color)
        {
            if (string.IsNullOrEmpty(value) || !settings.showText) return;
            var style = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = color }
            };
            GUI.Label(ToScreen(rect, area, sx, sy), value, style);
        }

        private void DrawHandles(Rect area, float sx, float sy)
        {
            Rect source = SelectedRect();
            Rect handle = ToScreen(source, area, sx, sy);
            EditorGUI.DrawRect(new Rect(handle.x, handle.y, handle.width, 1),
                new Color(.2f, .96f, .96f, .75f));
            EditorGUI.DrawRect(new Rect(handle.x, handle.yMax - 1, handle.width, 1),
                new Color(.2f, .96f, .96f, .75f));
            EditorGUI.DrawRect(new Rect(handle.x, handle.y, 1, handle.height),
                new Color(.2f, .96f, .96f, .75f));
            EditorGUI.DrawRect(new Rect(handle.xMax - 1, handle.y, 1, handle.height),
                new Color(.2f, .96f, .96f, .75f));

            Event ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0 &&
                handle.Contains(ev.mousePosition))
            {
                dragging = true;
                dragStart = ev.mousePosition;
                startingRect = source;
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && dragging)
            {
                Rect r = startingRect;
                r.x += (ev.mousePosition.x - dragStart.x) / sx;
                r.y += (ev.mousePosition.y - dragStart.y) / sy;
                r.x = Mathf.Clamp(r.x, 0f, settings.panelSize.x - 20f);
                r.y = Mathf.Clamp(r.y, 0f, settings.panelSize.y - 20f);
                SetSelectedRect(r);
                EditorUtility.SetDirty(settings);
                dirty = true;
                Repaint();
                ev.Use();
            }
            else if (ev.rawType == EventType.MouseUp)
                dragging = false;
        }

        private Rect SelectedRect()
        {
            switch (activeHandle)
            {
                case 0: return settings.avatarRect;
                case 1: return settings.characterNameRect;
                case 2: return settings.journalTitleRect;
                case 3: return settings.questDescriptionRect;
                case 4: return settings.notesRect;
                case 5: return settings.categoryListRect;
                default: return settings.objectivesStartRect;
            }
        }

        private void SetSelectedRect(Rect r)
        {
            switch (activeHandle)
            {
                case 0: settings.avatarRect = r; break;
                case 1: settings.characterNameRect = r; break;
                case 2: settings.journalTitleRect = r; break;
                case 3: settings.questDescriptionRect = r; break;
                case 4: settings.notesRect = r; break;
                case 5: settings.categoryListRect = r; break;
                default: settings.objectivesStartRect = r; break;
            }
        }

        private static Rect ToScreen(Rect local, Rect frame, float sx, float sy)
        {
            return new Rect(frame.x + local.x * sx,
                frame.y + local.y * sy, local.width * sx, local.height * sy);
        }

        private void DrawFooter()
        {
            GUILayout.Space(7);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("СОХРАНИТЬ В ROKAS",
                GUILayout.Height(32)))
            {
                settings.Normalize();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                dirty = false;
                ShowNotification(new GUIContent("Журнал сохранён"));
            }
            if (GUILayout.Button("НАЙТИ В PROJECT", GUILayout.Height(32)))
                SelectSettings();
            if (GUILayout.Button("СБРОСИТЬ НАСТРОЙКИ", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Сброс журнала",
                    "Сбросить координаты, тексты, шрифты и эффекты? " +
                    "Оригинальные PNG и MP3 не будут удалены.",
                    "Сбросить", "Отмена"))
                {
                    Undo.RecordObject(settings, "Reset ROKAS Journal");
                    var temp = CreateInstance<RokasJournalSettings>();
                    EditorUtility.CopySerialized(temp, settings);
                    DestroyImmediate(temp);
                    settings.Normalize();
                    EditorUtility.SetDirty(settings);
                    dirty = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            if (dirty)
                EditorGUILayout.HelpBox(
                    "Изменения ещё не сохранены. Нажми «СОХРАНИТЬ В ROKAS».",
                    MessageType.Warning);
        }

        private static Rect RectField(string title, Rect r)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            r.x = EditorGUILayout.FloatField("X", r.x);
            r.y = EditorGUILayout.FloatField("Y", r.y);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            r.width = EditorGUILayout.FloatField("W", r.width);
            r.height = EditorGUILayout.FloatField("H", r.height);
            EditorGUILayout.EndHorizontal();
            return r;
        }

        private static void AssetStatus(string path)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                path) != null;
            EditorGUILayout.LabelField(
                (exists ? "✓ " : "✗ ") + Path.GetFileName(path),
                exists ? EditorStyles.miniLabel : EditorStyles.boldLabel);
        }

        private static void Section(string title)
        {
            GUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);
        }
    }
}
