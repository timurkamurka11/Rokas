using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    internal static class LaptopNewsBinder
    {
        private static int lastScanFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            Canvas.willRenderCanvases -= Bind;
            Canvas.willRenderCanvases += Bind;
        }

        private static void Bind()
        {
            if (lastScanFrame == Time.frameCount) return;
            lastScanFrame = Time.frameCount;

            var cards = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var card in cards)
            {
                if (!card || card.name != "LaptopAppCard" || card.GetComponent<LaptopNewsRuntime>()) continue;

                var page = card.parent;
                if (!page) continue;

                bool isNews = false;
                foreach (var label in page.GetComponentsInChildren<Text>(true))
                {
                    if (label.name == "AppTitle" && label.text == "Новости")
                    {
                        isNews = true;
                        break;
                    }
                }
                if (!isNews) continue;

                for (int i = card.childCount - 1; i >= 0; i--)
                {
                    var child = card.GetChild(i).gameObject;
                    child.SetActive(false);
                    UnityEngine.Object.Destroy(child);
                }

                card.gameObject.AddComponent<LaptopNewsRuntime>();
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class LaptopNewsRuntime : MonoBehaviour
    {
        private static readonly Color White = new Color(.94f, .94f, .91f);
        private static readonly Color Soft = new Color(.64f, .71f, .75f);
        private static readonly Color Jade = new Color(.34f, .74f, .71f);
        private static readonly Color Gold = new Color(.78f, .61f, .34f);

        private UiKit ui;
        private Texture posterTexture;
        private RawImage posterImage;
        private RawImage videoImage;
        private GameObject videoControls;
        private Component player;
        private Type videoPlayerType;
        private RenderTexture renderTexture;
        private RectTransform progressFill;
        private Text timeLabel;
        private Text playLabel;
        private Text volumeLabel;
        private bool playWhenPrepared;
        private bool preparationRequested;
        private bool playbackStarted;
        private bool muted;
        private Action click;

        private void Awake()
        {
            var assets = Resources.Load<RokasAssets>("RokasAssets");
            if (!assets)
            {
                Debug.LogError("ROKAS YOMI News requires Resources/RokasAssets.");
                return;
            }

            var bootstrap = GetComponentInParent<RokasBootstrap>();
            click = bootstrap != null ? bootstrap.PlayUiClick : null;
            ui = new UiKit(assets, click);
            Build((RectTransform)transform);
        }

        private void Build(RectTransform card)
        {
            posterTexture = LoadPoster();
            if (!posterTexture)
                Debug.LogError("ROKAS YOMI News poster could not be reconstructed from embedded media.");

            Surface(card, "NewsHeroFrame", 22, 20, 730, 410, 18, new Color(.012f, .024f, .035f, 1f));
            var media = ui.Rect(card, "NewsHeroMedia", 27, 25, 720, 405);

            if (posterTexture)
            {
                posterImage = ui.Art(media, "NewsHeroPoster", posterTexture, 0, 0, 720, 405);
                posterImage.raycastTarget = false;
            }

            renderTexture = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "YomiNewsVideoRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            renderTexture.Create();

            videoImage = ui.Art(media, "NewsVideoSurface", renderTexture, 0, 0, 720, 405);
            videoImage.gameObject.SetActive(false);

            BuildVideoPlayer(media);

            var playHotspotFace = ui.Box(media, "NewsPlayPause", 284, 136, 152, 152, new Color(1, 1, 1, .001f), true);
            var playHotspot = playHotspotFace.gameObject.AddComponent<Button>();
            playHotspot.targetGraphic = playHotspotFace;
            playHotspot.onClick.AddListener(() => { click?.Invoke(); TogglePlay(); });
            playHotspot.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            videoControls = ui.Rect(media, "NewsVideoControls", 12, 347, 696, 48).gameObject;
            Surface(videoControls.transform, "ControlShade", 0, 0, 696, 48, 10, new Color(.01f, .018f, .025f, .88f));
            var controlButton = Button(videoControls.transform, "NewsPlayControl", "Пауза", 10, 8, 92, 32, TogglePlay);
            playLabel = controlButton.GetComponentInChildren<Text>();
            var track = Surface(videoControls.transform, "NewsProgressTrack", 116, 20, 352, 8, 4, new Color(.35f, .43f, .49f, .55f));
            var fill = Surface(track.transform, "NewsProgressFill", 0, 0, 0, 8, 4, Jade);
            progressFill = fill.rectTransform;
            timeLabel = ui.Label(videoControls.transform, "NewsVideoTime", "0:00 / 0:10", 480, 6, 108, 36, 14, White,
                false, TextAnchor.MiddleRight);
            var volumeButton = Button(videoControls.transform, "NewsVolume", "Звук", 598, 8, 88, 32, ToggleMute);
            volumeLabel = volumeButton.GetComponentInChildren<Text>();
            videoControls.SetActive(false);

            ui.Label(card, "NewsKicker", "ГОРОДСКИЕ ЛЕГЕНДЫ  ·  ВИДЕО", 784, 28, 438, 26, 14, Jade);
            ui.Label(card, "NewsHeadline", "Ночная рамэнная снова открылась после полуночи", 784, 58, 438, 96,
                31, White, true, TextAnchor.UpperLeft);
            ui.Label(card, "NewsDeck", "Жители района сообщают о странных посетителях, которые появляются только после закрытия метро.",
                784, 162, 438, 78, 20, Soft, false, TextAnchor.UpperLeft);
            ui.Box(card, "NewsArticleRule", 784, 252, 438, 1, new Color(.42f, .72f, .72f, .28f));
            ui.Label(card, "NewsBody",
                "Небольшая лавка KAS / YOMI зажигает фонари ближе к полуночи. По словам соседей, часть гостей не отражается в мокрых витринах.\n\nСлужба YOMI просит контрактников не вступать в контакт без подтверждённого заказа и присылать записи аномалий в городской архив.",
                784, 268, 438, 128, 17, White, false, TextAnchor.UpperLeft);
            ui.Label(card, "NewsMeta", "6 СЕНТ.  ·  ТОКИО  /  НОЧНАЯ СВОДКА", 784, 401, 438, 25, 13, Gold);

            ui.Label(card, "NewsLatestTitle", "ПОСЛЕДНИЕ НОВОСТИ", 26, 454, 330, 30, 16, Soft);
            BuildStory(card, "NewsStory1", 26, 492, 588, 72, "АНОМАЛИИ  ·  02:17",
                "В Сэтагая снова замечены пустые поезда после 02:00");
            BuildStory(card, "NewsStory2", 632, 492, 600, 72, "СЛУЖБА YOMI  ·  СЕГОДНЯ",
                "Западный сектор переведён на повышенный уровень угрозы");
            BuildStory(card, "NewsStory3", 26, 578, 588, 72, "БЕСТИАРИЙ  ·  3 НОВЫХ",
                "Городской архив пополнился тремя неизвестными ёкаями");
            BuildStory(card, "NewsStory4", 632, 578, 600, 72, "РЫНОК  ·  ВЕЧЕР",
                "Талисманы дорожают после серии ночных исчезновений");

            if (EventSystem.current && playHotspot.IsInteractable())
                EventSystem.current.SetSelectedGameObject(playHotspot.gameObject);
        }

        private void BuildVideoPlayer(Transform media)
        {
            videoPlayerType = ResolveVideoPlayerType();
            if (videoPlayerType == null)
            {
                Debug.LogWarning("ROKAS YOMI News: Unity Video module is unavailable; news page will remain on the poster.");
                return;
            }

            var host = new GameObject("NewsVideoPlayer");
            host.transform.SetParent(media, false);
            player = host.AddComponent(videoPlayerType);
            SetProperty("playOnAwake", false);
            SetProperty("waitForFirstFrame", true);
            SetProperty("skipOnDrop", true);
            SetProperty("isLooping", false);
            SetEnumProperty("renderMode", "RenderTexture");
            SetProperty("targetTexture", renderTexture);
            SetEnumProperty("audioOutputMode", "Direct");
            SetEnumProperty("source", "Url");

            string file = EnsureVideoFile();
            SetProperty("url", string.IsNullOrEmpty(file) ? string.Empty : new Uri(file).AbsoluteUri);
        }

        private static Type ResolveVideoPlayerType()
        {
            var type = Type.GetType("UnityEngine.Video.VideoPlayer, UnityEngine.VideoModule", false);
            if (type != null) return type;

            try
            {
                var assembly = Assembly.Load("UnityEngine.VideoModule");
                type = assembly.GetType("UnityEngine.Video.VideoPlayer", false);
                if (type != null) return type;
            }
            catch { }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "UnityEngine.VideoModule") continue;
                type = assembly.GetType("UnityEngine.Video.VideoPlayer", false);
                if (type != null) return type;
            }

            return null;
        }

        private Texture2D LoadPoster()
        {
            var bytes = LoadEmbeddedBytes("News/YomiRamenPoster", 4);
            if (bytes == null || bytes.Length == 0) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false, false)
            {
                name = "YomiRamenPoster",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            if (!ImageConversion.LoadImage(texture, bytes, true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            return texture;
        }

        private string EnsureVideoFile()
        {
            var bytes = LoadEmbeddedBytes("News/YomiRamenVideo", 6);
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("ROKAS YOMI News video media is missing.");
                return string.Empty;
            }

            string directory = Application.temporaryCachePath;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "YomiRamenNews.mp4");
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
                File.WriteAllBytes(path, bytes);
            return path;
        }

        private static byte[] LoadEmbeddedBytes(string resourcePrefix, int chunkCount)
        {
            var base64 = new System.Text.StringBuilder(chunkCount * 20000);

            for (int i = 0; i < chunkCount; i++)
            {
                string suffix = "_" + i.ToString("00");
                string text = null;

                var chunk = Resources.Load<TextAsset>(resourcePrefix + suffix);
                if (chunk) text = chunk.text;

                if (string.IsNullOrWhiteSpace(text))
                {
                    string relative = resourcePrefix.Replace('/', Path.DirectorySeparatorChar) + suffix + ".txt";
                    string diskPath = Path.Combine(Application.dataPath, "Rokas", "Resources", relative);
                    if (File.Exists(diskPath))
                    {
                        try { text = File.ReadAllText(diskPath); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("ROKAS YOMI News could not read media chunk " + diskPath + ": " + ex.Message);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    Debug.LogError("ROKAS YOMI News media chunk missing: " + resourcePrefix + suffix);
                    return null;
                }

                base64.Append(text.Trim());
            }

            try
            {
                return Convert.FromBase64String(base64.ToString());
            }
            catch (FormatException ex)
            {
                Debug.LogError("ROKAS YOMI News media Base64 is invalid: " + ex.Message);
                return null;
            }
        }

        private void TogglePlay()
        {
            if (!player)
            {
                Debug.LogWarning("ROKAS YOMI News video player is unavailable.");
                return;
            }

            if (GetBool("isPlaying"))
            {
                InvokePlayer("Pause");
                SetPlayText("Играть");
                return;
            }

            if (GetBool("isPrepared"))
            {
                ShowVideo();
                ApplyAudioState();
                InvokePlayer("Play");
                playbackStarted = true;
                SetPlayText("Пауза");
                return;
            }

            playWhenPrepared = true;
            preparationRequested = true;
            SetPlayText("Загрузка…");
            InvokePlayer("Prepare");
        }

        private void ShowVideo()
        {
            if (posterImage) posterImage.gameObject.SetActive(false);
            if (videoImage) videoImage.gameObject.SetActive(true);
            if (videoControls) videoControls.SetActive(true);
        }

        private void ToggleMute()
        {
            muted = !muted;
            if (player && GetBool("isPrepared")) ApplyAudioState();
            if (volumeLabel) volumeLabel.text = muted ? "Без звука" : "Звук";
        }

        private void ApplyAudioState()
        {
            InvokePlayer("SetDirectAudioMute", (ushort)0, muted);
            InvokePlayer("SetDirectAudioVolume", (ushort)0, 1f);
        }

        private void Update()
        {
            if (!player) return;

            bool prepared = GetBool("isPrepared");
            if (preparationRequested && prepared)
            {
                preparationRequested = false;
                ApplyAudioState();
                ShowVideo();

                if (playWhenPrepared)
                {
                    playWhenPrepared = false;
                    InvokePlayer("Play");
                    playbackStarted = true;
                    SetPlayText("Пауза");
                }
            }

            if (!prepared) return;

            double duration = GetDouble("length", 9.708);
            if (duration <= .01) duration = 9.708;
            double current = Math.Max(0, Math.Min(GetDouble("time", 0), duration));

            if (progressFill)
            {
                var size = progressFill.sizeDelta;
                size.x = 352f * (float)(current / duration);
                progressFill.sizeDelta = size;
            }

            if (timeLabel)
                timeLabel.text = FormatTime(current) + " / " + FormatTime(duration);

            if (playbackStarted && !GetBool("isPlaying") && current >= duration - .08)
            {
                playbackStarted = false;
                SetProperty("time", 0d);
                SetPlayText("Снова");
            }
        }

        private object GetProperty(string name)
        {
            if (!player || videoPlayerType == null) return null;
            var property = videoPlayerType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            return property != null ? property.GetValue(player, null) : null;
        }

        private bool GetBool(string name)
        {
            var value = GetProperty(name);
            return value is bool && (bool)value;
        }

        private double GetDouble(string name, double fallback)
        {
            var value = GetProperty(name);
            if (value == null) return fallback;
            try { return Convert.ToDouble(value); }
            catch { return fallback; }
        }

        private void SetProperty(string name, object value)
        {
            if (!player || videoPlayerType == null) return;
            var property = videoPlayerType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
                property.SetValue(player, value, null);
        }

        private void SetEnumProperty(string name, string value)
        {
            if (!player || videoPlayerType == null) return;
            var property = videoPlayerType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite || !property.PropertyType.IsEnum) return;
            property.SetValue(player, Enum.Parse(property.PropertyType, value), null);
        }

        private void InvokePlayer(string name, params object[] args)
        {
            if (!player || videoPlayerType == null) return;

            foreach (var method in videoPlayerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != name || method.GetParameters().Length != args.Length) continue;
                try
                {
                    method.Invoke(player, args);
                    return;
                }
                catch (ArgumentException) { }
                catch (TargetInvocationException ex)
                {
                    Debug.LogWarning("ROKAS YOMI News video call failed: " + ex.InnerException?.Message);
                    return;
                }
            }
        }

        private void BuildStory(Transform parent, string name, float x, float y, float w, float h, string meta, string title)
        {
            var story = Surface(parent, name, x, y, w, h, 12, new Color(.055f, .09f, .112f, .94f), true).rectTransform;
            ui.Box(story, "Accent", 0, 0, 4, h, new Color(Jade.r, Jade.g, Jade.b, .65f));
            ui.Label(story, "Meta", meta, 18, 8, w - 36, 18, 11, Gold);
            ui.Label(story, "Title", title, 18, 27, w - 36, h - 31, 16, White, false, TextAnchor.UpperLeft);
        }

        private Button Button(Transform parent, string name, string title, float x, float y, float w, float h, Action action)
        {
            var surface = Surface(parent, name, x, y, w, h, 9, new Color(.12f, .17f, .20f, .96f), true);
            var button = surface.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.14f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.72f, .86f, .83f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            ui.Label(surface.transform, "Title", title, 8, 0, w - 16, h, 14, White, false, TextAnchor.MiddleCenter);
            button.onClick.AddListener(() => { click?.Invoke(); action?.Invoke(); });
            return button;
        }

        private LaptopSurface Surface(Transform parent, string name, float x, float y, float w, float h, float radius,
            Color color, bool blocks = false)
        {
            var go = ui.Rect(parent, name, x, y, w, h).gameObject;
            if (!go.TryGetComponent<CanvasRenderer>(out _)) go.AddComponent<CanvasRenderer>();
            var surface = go.AddComponent<LaptopSurface>();
            surface.Radius = radius;
            surface.color = color;
            surface.raycastTarget = blocks;
            return surface;
        }

        private void SetPlayText(string value)
        {
            if (playLabel) playLabel.text = value;
        }

        private static string FormatTime(double seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt((float)seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        private void OnDestroy()
        {
            if (player) InvokePlayer("Stop");

            if (renderTexture)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                renderTexture = null;
            }

            if (posterTexture)
            {
                UnityEngine.Object.Destroy(posterTexture);
                posterTexture = null;
            }
        }
    }
}
