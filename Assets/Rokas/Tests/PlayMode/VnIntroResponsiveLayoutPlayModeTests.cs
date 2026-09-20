using System.Collections;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VnIntroResponsiveLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResponsiveCompositionFits1280x720WithoutUiClipping()
        {
            yield return VerifyResponsiveComposition(1280, 720);
        }

        [UnityTest]
        public IEnumerator ResponsiveCompositionFits1024x768WithoutUiClipping()
        {
            yield return VerifyResponsiveComposition(1024, 768);
        }

        private static IEnumerator VerifyResponsiveComposition(int screenWidth, int screenHeight)
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            VnIntroArt art = CreateArt(assets);
            var host = new GameObject($"VnResponsiveProof_{screenWidth}x{screenHeight}");
            VnIntroView view = VnIntroView.Create(host.transform, assets.sans, art,
                () => { }, () => { }, () => { }, () => { });

            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.PresentLine("Keiko",
                    "The responsive proof keeps authored dialogue text wrapped safely inside the full lower panel while every control remains usable.");

                yield return null;
                Canvas.ForceUpdateCanvases();

                RectTransform root = GameObject.Find("VnIntroRoot").GetComponent<RectTransform>();
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));

                Rect canvasRect = new Rect(Vector2.zero,
                    CalculateVirtualCanvasSize(screenWidth, screenHeight, scaler.referenceResolution, scaler.matchWidthOrHeight));

                RawImage panel = GameObject.Find("DialoguePanel").GetComponent<RawImage>();
                RectTransform panelTransform = panel.rectTransform;
                Rect panelRect = ResolveChildRect(panelTransform, canvasRect);
                Assert.That(panel.texture, Is.SameAs(art.DialoguePanelKeikoDark));
                Assert.That(panel.uvRect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                AspectRatioFitter panelAspect = panel.GetComponent<AspectRatioFitter>();
                Assert.That(panelAspect, Is.Not.Null);
                Assert.That(panelAspect.aspectRatio, Is.EqualTo(2048f / 682f).Within(.001f));
                Assert.That(panelRect.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - 1f));
                Assert.That(panelRect.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + 1f));
                Assert.That(panelRect.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + 1f));
                float visiblePanelHeight = Mathf.Min(panelRect.yMax, canvasRect.yMax) -
                                           Mathf.Max(panelRect.yMin, canvasRect.yMin);
                Assert.That(visiblePanelHeight / panelRect.height, Is.GreaterThan(.76f),
                    $"The authored panel must remain substantially visible at {screenWidth}x{screenHeight}; only the intentional lower-edge bleed may leave the canvas.");

                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitMask"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "Portrait"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitPrevious"), Is.Null);
                Assert.That(FindDescendantIncludingInactive(host.transform, "PortraitFrame"), Is.Null);

                GameObject primaryObject = FindDescendantIncludingInactive(host.transform, "CharacterPrimary");
                GameObject secondaryObject = FindDescendantIncludingInactive(host.transform, "CharacterSecondary");
                Assert.That(primaryObject, Is.Not.Null);
                Assert.That(secondaryObject, Is.Not.Null);
                Assert.That(primaryObject.activeSelf, Is.False,
                    $"Keiko protagonist presentation must remain text-only at {screenWidth}x{screenHeight}.");
                Assert.That(secondaryObject.activeSelf, Is.False);

                RectTransform speakerTransform = GameObject.Find("SpeakerName").GetComponent<RectTransform>();
                Rect speakerRect = ResolveChildRect(speakerTransform, panelRect);
                AssertRectInside(speakerRect, panelRect, "SpeakerName", screenWidth, screenHeight);
                AssertRectInside(speakerRect, canvasRect, "SpeakerName", screenWidth, screenHeight);

                RectTransform dialogueTransform = GameObject.Find("DialogueText").GetComponent<RectTransform>();
                Rect dialogueRect = ResolveChildRect(dialogueTransform, panelRect);
                AssertRectInside(dialogueRect, panelRect, "DialogueText", screenWidth, screenHeight);
                AssertRectInside(dialogueRect, canvasRect, "DialogueText", screenWidth, screenHeight);
                Assert.That(speakerRect.yMin, Is.GreaterThanOrEqualTo(dialogueRect.yMax - 2f),
                    $"The name/header region must remain above the body copy at {screenWidth}x{screenHeight}.");

                Text dialogue = dialogueTransform.GetComponent<Text>();
                Assert.That(dialogue.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(dialogue.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));

                RectTransform controlsTransform = GameObject.Find("ControlsRow").GetComponent<RectTransform>();
                Rect controlsRect = ResolveChildRect(controlsTransform, panelRect);
                AssertRectInside(controlsRect, panelRect, "ControlsRow", screenWidth, screenHeight);

                Rect nextOrBackLeftMost = default;
                bool capturedRightCluster = false;
                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton", "BackButton", "NextButton" })
                {
                    GameObject control = GameObject.Find(name);
                    Assert.That(control, Is.Not.Null, name + " must exist.");
                    RectTransform buttonTransform = control.GetComponent<RectTransform>();
                    Rect buttonRect = ResolveChildRect(buttonTransform, controlsRect);
                    AssertRectInside(buttonRect, panelRect, name, screenWidth, screenHeight);
                    AssertRectInside(buttonRect, canvasRect, name, screenWidth, screenHeight);

                    Button button = control.GetComponent<Button>();
                    Assert.That(button.targetGraphic, Is.Not.Null);
                    Assert.That(button.targetGraphic.color.a, Is.LessThanOrEqualTo(.001f),
                        name + " must remain a transparent hit target without a black backing artifact.");

                    if (name == "BackButton" || name == "NextButton")
                    {
                        if (!capturedRightCluster || buttonRect.xMin < nextOrBackLeftMost.xMin)
                            nextOrBackLeftMost = buttonRect;
                        capturedRightCluster = true;
                    }
                }

                Assert.That(GameObject.Find("BackButton").GetComponent<Button>().interactable, Is.False);
                Assert.That(GameObject.Find("NextButton").GetComponent<Button>().interactable, Is.True);
                Assert.That(capturedRightCluster, Is.True);
                Assert.That(dialogueRect.xMax, Is.LessThanOrEqualTo(nextOrBackLeftMost.xMin - 16f),
                    $"Dialogue copy must keep a safe readable gap before the right-side controls at {screenWidth}x{screenHeight}.");

                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                view.PresentLine("Mina", "Я дома, приходи, нужно поговорить.");
                yield return new WaitForSecondsRealtime(.25f);
                Canvas.ForceUpdateCanvases();

                Assert.That(panel.texture, Is.SameAs(art.DialoguePanelMinaLight));
                Assert.That(GameObject.Find("SpeakerName").GetComponent<Text>().text, Is.EqualTo("Mina"));
                Assert.That(primaryObject.activeSelf, Is.True);
                Assert.That(secondaryObject.activeSelf, Is.False);

                RawImage minaBody = primaryObject.GetComponent<RawImage>();
                RectTransform minaTransform = primaryObject.GetComponent<RectTransform>();
                Assert.That(minaBody.texture, Is.SameAs(art.MinaCharacterSheet));
                Assert.That(minaTransform.rect.height, Is.GreaterThanOrEqualTo(1180f),
                    "Mina must retain the approved closer thigh-up staging rather than the old distant framing.");
                Assert.That(minaTransform.localScale.x, Is.EqualTo(1f).Within(.01f),
                    "A single staged Mina body must remain neutral rather than receiving active-speaker focus scale.");

                Rect minaRect = ScaleRectAroundPivot(ResolveChildRect(minaTransform, canvasRect),
                    minaTransform.pivot, minaTransform.localScale);
                Assert.That(minaRect.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - 1f));
                Assert.That(minaRect.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + 1f));
                Assert.That(minaRect.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + 1f),
                    $"Closer Mina staging must not clip at the top of {screenWidth}x{screenHeight}.");
                Assert.That(Mathf.Abs(minaRect.center.x - canvasRect.center.x), Is.LessThan(2f),
                    $"Solo Mina should remain centered at {screenWidth}x{screenHeight}.");

                Vector2 stablePosition = minaTransform.anchoredPosition;
                Vector3 stableScale = minaTransform.localScale;
                yield return new WaitForSecondsRealtime(.35f);
                Assert.That(Vector2.Distance(stablePosition, minaTransform.anchoredPosition), Is.LessThan(.01f),
                    $"Solo Mina must not bob at {screenWidth}x{screenHeight}.");
                Assert.That(Vector3.Distance(stableScale, minaTransform.localScale), Is.LessThan(.0002f),
                    $"Solo Mina must not pulse at {screenWidth}x{screenHeight}.");

                Assert.That(minaTransform.GetSiblingIndex(), Is.LessThan(panelTransform.GetSiblingIndex()));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
        }

        private static GameObject FindDescendantIncludingInactive(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform descendant in descendants)
            {
                if (descendant.name == name)
                    return descendant.gameObject;
            }

            return null;
        }

        private static Vector2 CalculateVirtualCanvasSize(int screenWidth, int screenHeight,
            Vector2 referenceResolution, float matchWidthOrHeight)
        {
            float logWidth = Mathf.Log(screenWidth / referenceResolution.x, 2f);
            float logHeight = Mathf.Log(screenHeight / referenceResolution.y, 2f);
            float logScale = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
            float scaleFactor = Mathf.Pow(2f, logScale);
            return new Vector2(screenWidth / scaleFactor, screenHeight / scaleFactor);
        }

        private static Rect ResolveChildRect(RectTransform child, Rect parentRect)
        {
            Vector2 min = new Vector2(
                parentRect.xMin + parentRect.width * child.anchorMin.x + child.offsetMin.x,
                parentRect.yMin + parentRect.height * child.anchorMin.y + child.offsetMin.y);
            Vector2 max = new Vector2(
                parentRect.xMin + parentRect.width * child.anchorMax.x + child.offsetMax.x,
                parentRect.yMin + parentRect.height * child.anchorMax.y + child.offsetMax.y);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect ScaleRectAroundPivot(Rect rect, Vector2 pivot, Vector3 scale)
        {
            Vector2 pivotPoint = new Vector2(
                rect.xMin + rect.width * pivot.x,
                rect.yMin + rect.height * pivot.y);
            float xMin = pivotPoint.x + (rect.xMin - pivotPoint.x) * scale.x;
            float xMax = pivotPoint.x + (rect.xMax - pivotPoint.x) * scale.x;
            float yMin = pivotPoint.y + (rect.yMin - pivotPoint.y) * scale.y;
            float yMax = pivotPoint.y + (rect.yMax - pivotPoint.y) * scale.y;
            return Rect.MinMaxRect(Mathf.Min(xMin, xMax), Mathf.Min(yMin, yMax),
                Mathf.Max(xMin, xMax), Mathf.Max(yMin, yMax));
        }

        private static void AssertRectInside(Rect inner, Rect outer, string name, int screenWidth, int screenHeight)
        {
            const float tolerance = 1f;
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance),
                $"{name} leaves the left bound at {screenWidth}x{screenHeight}.");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance),
                $"{name} leaves the right bound at {screenWidth}x{screenHeight}.");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance),
                $"{name} leaves the bottom bound at {screenWidth}x{screenHeight}.");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance),
                $"{name} leaves the top bound at {screenWidth}x{screenHeight}.");
        }

        private static VnIntroArt CreateArt(RokasAssets assets)
        {
            return new VnIntroArt(
                assets.vnKeikoCharacterSheet,
                assets.vnMinaCharacterSheet,
                assets.vnBusStopRainNight,
                assets.vnNightSkyRain,
                assets.vnBusStopPhoneMessageMina,
                assets.vnDialoguePanelKeikoDark,
                assets.vnDialoguePanelMinaLight,
                assets.vnIconMute,
                assets.vnIconPause,
                assets.vnIconSkip);
        }
    }
}

