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
        private int selected;
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
            SetSelected(0, false);
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
            if (index < 0 || index >= 5) return;
            selected = index;
            for (int i = 0; i < items.Count; i++)
                items[i].SetSelected(i == index && i < 5);
            if (dispatch) selectTab?.Invoke(index);
        }
    }

    public sealed class RokasBedWatchPngState : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private Image normal, highlighted;
        private Sprite normalSprite, selectedSprite;
        private bool persistent, selected, hovering, pressed;
        private float highlight;
        private Action clicked, feedback;
        private RectTransform rect;

        public void Initialize(Texture2D baseTex, Texture2D activeTex,
            bool isTab, Action onClick, Action onFeedback)
        {
            persistent = isTab;
            clicked = onClick;
            feedback = onFeedback;
            rect = (RectTransform)transform;
            normalSprite = Sprite.Create(baseTex,
                new Rect(0f, 0f, baseTex.width, baseTex.height),
                new Vector2(.5f,.5f));
            selectedSprite = Sprite.Create(activeTex,
                new Rect(0f, 0f, activeTex.width, activeTex.height),
                new Vector2(.5f,.5f));
            normal = MakeLayer("Normal_AuthoredPNG",normalSprite,true);
            highlighted = MakeLayer("Selected_AuthoredPNG",selectedSprite,false);
            highlight = 0f;
            highlighted.color = new Color(1f,1f,1f,0f);
        }

        private Image MakeLayer(string name, Sprite sprite, bool interactive)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.raycastTarget = interactive;
            img.color = Color.white;
            return img;
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
                pressed = true;
        }

        public void OnPointerUp(PointerEventData e) { pressed = false; }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            feedback?.Invoke();
            clicked?.Invoke();
        }

        private void Update()
        {
            if (highlighted == null || normal == null) return;
            float target = selected || hovering || pressed ? 1f : 0f;
            highlight = Mathf.MoveTowards(highlight, target,
                Time.unscaledDeltaTime / .15f);
            highlighted.color = new Color(1f,1f,1f,highlight);
            normal.color = Color.white;
            // Do not move/resize the hit box or redraw the source art.
            // Micromotion is confined to the image transform and always
            // returns to a fixed resting scale on exit.
            float breathing = selected ? .003f *
                Mathf.Sin(Time.unscaledTime * 2f) : 0f;
            float scale = pressed ? .985f :
                (hovering ? 1.008f : 1f + breathing);
            rect.localScale = Vector3.Lerp(rect.localScale,
                new Vector3(scale,scale,1f),
                Mathf.Clamp01(Time.unscaledDeltaTime * 13f));
        }

        private void OnDestroy()
        {
            if (normalSprite != null) Destroy(normalSprite);
            if (selectedSprite != null) Destroy(selectedSprite);
        }
    }
}
