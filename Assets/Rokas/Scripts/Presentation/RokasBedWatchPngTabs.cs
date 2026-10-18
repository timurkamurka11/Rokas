using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rokas.Presentation
{
    /// <summary>
    /// Seven AUTHORED PNG pairs from the user's UI KNOPKI archive.
    /// Source pixels, transparency and highlight artwork are never generated
    /// or recolored. Only image alpha and a tiny in-place press scale animate.
    /// Five persistent tabs; E/ESC are temporary action states.
    /// </summary>
    public sealed class RokasBedWatchPngTabs : MonoBehaviour
    {
        private readonly List<RokasBedWatchPngState> items =
            new List<RokasBedWatchPngState>();
        private Action<int> selectTab;
        private Action confirm, exit;
        private AudioSource audio;
        private AudioClip uiClick;
        // -1 means initial, genuinely unselected: all five buttons AMBER.
        private int selected = -1;
        public int SelectedTab => selected;
        public bool Available { get; private set; }

        private static readonly string[] ResourceNames = {
            "tab_1", "tab_2", "tab_3", "tab_4",
            "tab_5", "action_confirm", "action_exit"
        };
        // Absolute 1920x1080 UiKit space, matching the supplied screenshot.
        private static readonly float[] Left = { 104, 342, 566, 841, 1079, 1353, 1625 };
        private static readonly float[] Widths = { 232, 218, 264, 230, 266, 265, 205 };
        private const float Top = 36f;
        private const float Height = 72f;

        public void Initialize(Transform parent, Action<int> onSelect,
            Action onConfirm, Action onExit, AudioSource soundSource,
            AudioClip clickSound)
        {
            selectTab = onSelect; confirm = onConfirm; exit = onExit;
            audio = soundSource; uiClick = clickSound;
            var original = new Texture2D[7];
            var active = new Texture2D[7];
            for (int i = 0; i < ResourceNames.Length; i++)
            {
                original[i] = Resources.Load<Texture2D>(
                    "BedSleep/WatchTabs/" + ResourceNames[i] + "_normal");
                active[i] = Resources.Load<Texture2D>(
                    "BedSleep/WatchTabs/" + ResourceNames[i] + "_selected");
                if (original[i] == null || active[i] == null)
                {
                    Debug.LogWarning("[ROKAS WatchTabs] Missing authored PNG pair " +
                        ResourceNames[i] + ". Install the supplied WatchTabs assets.");
                    Available = false;
                    return;
                }
            }

            for (int i = 0; i < ResourceNames.Length; i++)
            {
                int index = i;
                var host = new GameObject("ROKAS_PNG_" + ResourceNames[i],
                    typeof(RectTransform));
                host.transform.SetParent(parent, false);
                var rect = (RectTransform)host.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(Left[i], -Top);
                rect.sizeDelta = new Vector2(Widths[i], Height);
                var view = host.AddComponent<RokasBedWatchPngState>();
                view.Initialize(original[i], active[i], i < 5,
                    () => OnClick(index), PlayFeedback);
                items.Add(view);
            }
            Available = true;
            SetSelected(-1, false);
        }

        private void PlayFeedback()
        {
            if (audio != null && uiClick != null)
                audio.PlayOneShot(uiClick, .28f);
        }

        private void OnClick(int index)
        {
            if (index < 5)
            {
                SetSelected(index, true);
            }
            else if (index == 5)
            {
                confirm?.Invoke();
            }
            else
            {
                exit?.Invoke();
            }
        }

        public void SetSelected(int index, bool dispatch = true)
        {
            if (index < -1 || index >= 5) return;
            selected = index;
            for (int i = 0; i < items.Count; i++)
                items[i].SetSelected(i == index && i < 5);
            if (dispatch) selectTab?.Invoke(index);
        }
    }

    /// <summary>
    /// LaptopChoiceHover timing/feedback, but with the user's ORIGINAL
    /// amber and cyan PNGs. Only ONE Image is rasterized at a time.
    /// The WatchPngBlend shader mixes the PNG pixels before alpha compositing
    /// (no ghostly overlapping glow or double typography).
    /// </summary>
    public sealed class RokasBedWatchPngState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private Image image;
        private Sprite amberSprite, blueSprite;
        private Material blendMaterial;
        private bool persistent, selected, hovering, pressed;
        private float blend;
        private float hoverWeight;
        private float pressRemaining;
        private float animTime;
        private Action clicked, feedback;
        private RectTransform rect;
        private Vector3 baseScale;
        private bool shaderReady;

        public void Initialize(Texture2D baseTex, Texture2D activeTex,
            bool isTab, Action onClick, Action onFeedback)
        {
            if (baseTex == null || activeTex == null)
                throw new ArgumentNullException("Authored watch PNG");
            persistent = isTab;
            clicked = onClick;
            feedback = onFeedback;
            rect = transform as RectTransform;
            baseScale = rect != null ? rect.localScale : Vector3.one;
            amberSprite = Sprite.Create(baseTex,
                new Rect(0f, 0f, baseTex.width, baseTex.height),
                new Vector2(.5f, .5f));
            blueSprite = Sprite.Create(activeTex,
                new Rect(0f, 0f, activeTex.width, activeTex.height),
                new Vector2(.5f, .5f));
            var go = new GameObject("AuthoredWatchPNG_SingleImage",
                typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var size = (RectTransform)go.transform;
            size.anchorMin = Vector2.zero;
            size.anchorMax = Vector2.one;
            size.offsetMin = size.offsetMax = Vector2.zero;
            image = go.GetComponent<Image>();
            image.sprite = amberSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
            image.color = Color.white;

            var shader = Shader.Find("ROKAS/UI/WatchPngBlend");
            shaderReady = shader != null && shader.isSupported &&
                baseTex.width == activeTex.width &&
                baseTex.height == activeTex.height;
            if (shaderReady)
            {
                blendMaterial = new Material(shader);
                blendMaterial.name = "ROKAS_WatchPngBlend_" + name;
                blendMaterial.SetTexture("_MainTex", baseTex);
                blendMaterial.SetTexture("_SelectedTex", activeTex);
                blendMaterial.SetFloat("_Blend", 0f);
                image.material = blendMaterial;
            }
            else
            {
                // If a shader is unsupported we still draw ONLY ONE PNG.
                Debug.LogWarning("[ROKAS WatchTabs] Single-sprite fallback: " +
                    "WatchPngBlend shader unavailable or button atlas mismatch.");
            }
            ResetVisualState();
        }

        private void ResetVisualState()
        {
            hovering = pressed = false;
            selected = false;
            blend = hoverWeight = pressRemaining = animTime = 0f;
            if (rect != null) rect.localScale = baseScale;
            if (image != null)
            {
                image.sprite = amberSprite;
                image.color = Color.white;
            }
            if (blendMaterial != null) blendMaterial.SetFloat("_Blend", 0f);
        }

        public void SetSelected(bool selectedState)
        {
            selected = persistent && selectedState;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovering = true;
            feedback?.Invoke();
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovering = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
            {
                pressed = true;
                pressRemaining = .14f;
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left)
            {
                pressed = false;
                pressRemaining = Mathf.Min(pressRemaining, .06f);
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            feedback?.Invoke();
            clicked?.Invoke();
        }

        private void Update()
        {
            if (image == null || rect == null) return;
            float dt = Mathf.Max(0f, Time.unscaledDeltaTime);
            animTime += dt;
            // Same damped response as LaptopChoiceHover, preserving the
            // 0.15s colour transition from the watch UI brief.
            float target = selected || hovering || pressed ? 1f : 0f;
            blend = Mathf.MoveTowards(blend, target, dt / .15f);
            hoverWeight = Mathf.MoveTowards(hoverWeight,
                hovering ? 1f : 0f, dt * 6.5f);
            pressRemaining = Mathf.Max(0f, pressRemaining - dt);
            if (shaderReady)
                // The supplied gold/cyan artwork has small differences
                // in glyph placement and bezel proportions. The full
                // 150ms transition is the laptop-like hover pulse; restrict
                // actual PNG morphing to its centre ~12ms to eliminate
                // apparent DOUBLE outlines and ghosted lettering.
                blendMaterial.SetFloat("_Blend",
                    Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(.46f, .54f, blend)));
            else
                image.sprite = blend >= .5f ? blueSprite : amberSprite;

            // Hover + tactile press like the laptop. PNG rectangles never
            // move or change their authorized layout coordinates.
            float idlePulse = selected ?
                .003f * Mathf.Sin(animTime * 2.1f) : 0f;
            float hoverPulse = .004f * Mathf.Sin(animTime * 5.2f);
            float press = -.025f * Mathf.Clamp01(pressRemaining / .14f);
            float targetScale = 1f + idlePulse +
                hoverWeight * (.015f + hoverPulse) + press;
            float ease = 1f - Mathf.Exp(-15f * dt);
            rect.localScale = Vector3.Lerp(rect.localScale,
                baseScale * targetScale, ease);
        }

        private void OnDisable()
        {
            ResetVisualState();
        }

        private void OnDestroy()
        {
            if (blendMaterial != null) Destroy(blendMaterial);
            if (amberSprite != null) Destroy(amberSprite);
            if (blueSprite != null) Destroy(blueSprite);
        }
    }
    public sealed class RokasBedWatchFocusButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        private Image surface;
        private Text caption;
        private Button button;
        private bool over, down;
        private float t;
        private AudioSource source;
        private AudioClip hoverCue;

        private void Awake()
        {
            surface = GetComponent<Image>();
            button = GetComponent<Button>();
            caption = GetComponentInChildren<Text>();
            source = GetComponentInParent<AudioSource>();
            if (button != null) button.transition = Selectable.Transition.None;
            const int hz = 22050;
            var samples = new float[950];
            for (int i = 0; i < samples.Length; i++)
            {
                float sec = (float)i / hz;
                samples[i] = Mathf.Sin(sec * 2f * Mathf.PI * 970f)
                    * Mathf.Exp(-sec * 95f) * .13f;
            }
            hoverCue = AudioClip.Create("ROKAS_WatchFocus_Hover", samples.Length,
                1, hz, false);
            hoverCue.SetData(samples, 0);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            over = true;
            if (source != null && hoverCue != null)
                source.PlayOneShot(hoverCue, .15f);
        }
        public void OnPointerExit(PointerEventData e)
        { over = false; down = false; }
        public void OnPointerDown(PointerEventData e)
        { if (e.button == PointerEventData.InputButton.Left) down = true; }
        public void OnPointerUp(PointerEventData e) { down = false; }

        private void Update()
        {
            t = Mathf.MoveTowards(t, over || down ? 1f : 0f,
                Time.unscaledDeltaTime / .16f);
            if (surface != null)
                surface.color = Color.Lerp(
                    new Color(.19f,.12f,.055f,.94f),
                    new Color(.04f,.17f,.24f,.96f),t);
            if (caption != null)
                caption.color = Color.Lerp(
                    new Color(1f,.80f,.50f,1f),
                    new Color(.57f,.91f,1f,1f),t);
            float pulse = over ? 1.008f +
                .002f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
            float scale = down ? .985f : pulse;
            transform.localScale = Vector3.Lerp(transform.localScale,
                new Vector3(scale,scale,1f),
                Mathf.Clamp01(Time.unscaledDeltaTime * 12f));
        }

        private void OnDestroy()
        {
            if (hoverCue != null) Destroy(hoverCue);
        }
    }
}
