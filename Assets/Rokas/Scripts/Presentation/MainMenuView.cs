using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Rokas.Presentation
{
    /// <summary>
    /// Launch-only menu shown after the story intro. Gameplay presentation is not constructed until Enter World.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        private const int ReferenceWidth = 1920;
        private const int ReferenceHeight = 1080;
        private static readonly Color White = new Color(.96f, .96f, .94f, 1f);
        private static readonly Color Gold = new Color(1f, .63f, .20f, 1f);
        private static readonly Color GoldFace = new Color(.21f, .12f, .08f, .92f);
        private static readonly Color DarkFace = new Color(.055f, .065f, .095f, .90f);
        private static readonly Color CoolEdge = new Color(.55f, .60f, .70f, .82f);

        private RokasAssets assets;
        private Action click;
        private Action enterWorld;
        private VideoPlayer player;
        private AudioSource audioSource;
        private RawImage videoImage;
        private RenderTexture target;
        private float volume;
        private bool consumed;
        private bool disposed;

        public static MainMenuView Create(Transform parent, RokasAssets assets, float videoVolume,
            Action click, Action enterWorld)
        {
            if (!parent) throw new ArgumentNullException("parent");
            if (!assets) throw new ArgumentNullException("assets");
            if (enterWorld == null) throw new ArgumentNullException("enterWorld");

            var root = new GameObject("RokasMainMenu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MainMenuView));
            root.transform.SetParent(parent, false);
            var view = root.GetComponent<MainMenuView>();
            view.Initialize(assets, videoVolume, click, enterWorld);
            return view;
        }

        private void Initialize(RokasAssets menuAssets, float requestedVolume, Action clickAction, Action enterAction)
        {
            assets = menuAssets;
            click = clickAction;
            enterWorld = enterAction;
            volume = Mathf.Clamp01(requestedVolume);

            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            CreateVideoSurface(root);
            CreateButton(root, "EnterWorldButton", "▶", "ВОЙТИ В МИР", 48f, true, EnterWorld);
            CreateButton(root, "DevelopersButton", "●●●", "О РАЗРАБОТЧИКАХ", -54f, false, SecondaryAction);
            CreateButton(root, "SupportDevelopmentButton", "♥", "ПОДДЕРЖАТЬ РАЗРАБОТКУ", -156f, false, SecondaryAction);
        }

        private void CreateVideoSurface(RectTransform root)
        {
            var black = new GameObject("MainMenuBlack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var blackRect = (RectTransform)black.transform;
            blackRect.SetParent(root, false);
            Fill(blackRect);
            var blackImage = black.GetComponent<Image>();
            blackImage.color = Color.black;
            blackImage.raycastTarget = false;

            var surface = new GameObject("MainMenuVideoSurface", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            var surfaceRect = (RectTransform)surface.transform;
            surfaceRect.SetParent(root, false);
            Fill(surfaceRect);
            videoImage = surface.GetComponent<RawImage>();
            videoImage.color = Color.black;
            videoImage.raycastTarget = false;

            target = new RenderTexture(ReferenceWidth, ReferenceHeight, 0, RenderTextureFormat.ARGB32)
            {
                name = "RokasMainMenuRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            ClearToBlack(target);
            videoImage.texture = target;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.isLooping = true;
            player.waitForFirstFrame = true;
            player.skipOnDrop = true;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = target;
            player.source = VideoSource.Url;
            player.sendFrameReadyEvents = true;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = Application.isFocused ? volume : 0f;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audioSource);

            player.prepareCompleted += OnPrepared;
            player.frameReady += OnFrameReady;
            player.errorReceived += OnError;
            string path = ResolveMenuPath();
            player.url = ResolveMenuUrl(path);
            if (File.Exists(path))
                player.Prepare();
            else
                Debug.LogWarning("ROKAS main-menu loop media missing; keeping menu usable over black fallback: " + path);
        }

        private void CreateButton(RectTransform root, string name, string icon, string label,
            float y, bool primary, Action action)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            var rect = (RectTransform)buttonObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(584f, 86f);
            rect.anchoredPosition = new Vector2(0f, y);

            if (primary)
                Surface(rect, name + "Glow", new Vector2(606f, 108f), Gold * new Color(1f, 1f, 1f, .16f), 31f);
            Surface(rect, name + "Edge", new Vector2(590f, 92f), primary ? Gold : CoolEdge, 27f);
            LaptopSurface face = Surface(rect, name + "Face", new Vector2(580f, 82f), primary ? GoldFace : DarkFace, 24f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = face;
            face.raycastTarget = true;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = primary ? new Color(1.12f, 1.06f, .92f, 1f) : new Color(1.08f, 1.08f, 1.12f, 1f);
            colors.pressedColor = new Color(.86f, .86f, .86f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.onClick.AddListener(() => action?.Invoke());

            Text iconText = Label(rect, name + "Icon", icon, 26f, TextAnchor.MiddleCenter);
            var iconRect = (RectTransform)iconText.transform;
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(.5f, .5f);
            iconRect.sizeDelta = new Vector2(92f, 0f);
            iconRect.anchoredPosition = new Vector2(66f, 0f);

            Text title = Label(rect, name + "Label", label, 25f, TextAnchor.MiddleCenter);
            var titleRect = (RectTransform)title.transform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(112f, 0f);
            titleRect.offsetMax = new Vector2(-82f, 0f);

            Text chevron = Label(rect, name + "Chevron", "›", 42f, TextAnchor.MiddleCenter);
            var chevronRect = (RectTransform)chevron.transform;
            chevronRect.anchorMin = new Vector2(1f, 0f);
            chevronRect.anchorMax = new Vector2(1f, 1f);
            chevronRect.pivot = new Vector2(.5f, .5f);
            chevronRect.sizeDelta = new Vector2(64f, 0f);
            chevronRect.anchoredPosition = new Vector2(-48f, 0f);
        }

        private LaptopSurface Surface(RectTransform parent, string name, Vector2 size, Color color, float radius)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(LaptopSurface));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            var surface = obj.GetComponent<LaptopSurface>();
            surface.color = color;
            surface.Radius = radius;
            surface.raycastTarget = false;
            return surface;
        }

        private Text Label(RectTransform parent, string name, string text, float size, TextAnchor alignment)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var label = obj.GetComponent<Text>();
            label.text = text;
            label.font = assets.sans;
            label.fontSize = Mathf.RoundToInt(size);
            label.fontStyle = FontStyle.Normal;
            label.alignment = alignment;
            label.color = White;
            label.raycastTarget = false;
            label.resizeTextForBestFit = false;
            return label;
        }

        private void EnterWorld()
        {
            if (consumed || disposed) return;
            consumed = true;
            click?.Invoke();
            Action callback = enterWorld;
            Dispose();
            callback?.Invoke();
        }

        private void SecondaryAction()
        {
            if (disposed) return;
            click?.Invoke();
        }

        private void OnPrepared(VideoPlayer source)
        {
            if (disposed || source != player) return;
            source.Play();
        }

        private void OnFrameReady(VideoPlayer source, long frameIndex)
        {
            if (disposed || source != player || !videoImage) return;
            videoImage.color = Color.white;
        }

        private void OnError(VideoPlayer source, string message)
        {
            if (disposed || source != player) return;
            Debug.LogWarning("ROKAS main-menu loop playback failed; keeping menu usable over black fallback: " + message);
            if (source) source.Stop();
        }

        private void Update()
        {
            if (disposed || !audioSource) return;
            audioSource.volume = Application.isFocused ? volume : 0f;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CleanupVideo();
            if (gameObject) Destroy(gameObject);
        }

        private void CleanupVideo()
        {
            if (player)
            {
                player.prepareCompleted -= OnPrepared;
                player.frameReady -= OnFrameReady;
                player.errorReceived -= OnError;
                player.Stop();
                player.targetTexture = null;
                player.url = string.Empty;
            }
            if (audioSource)
            {
                audioSource.Stop();
                audioSource.volume = 0f;
            }
            if (videoImage) videoImage.texture = null;
            if (target)
            {
                target.Release();
                Destroy(target);
                target = null;
            }
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearToBlack(RenderTexture renderTexture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = previous;
        }

        private static string ResolveMenuPath()
        {
            return Path.Combine(Application.streamingAssetsPath, "RokasVideo", "MainMenuLoop.mp4");
        }

        private static string ResolveMenuUrl(string path)
        {
            try { return new Uri(path).AbsoluteUri; }
            catch (UriFormatException) { return path; }
        }

        private void OnDestroy()
        {
            if (!disposed)
            {
                disposed = true;
                CleanupVideo();
            }
        }
    }
}
