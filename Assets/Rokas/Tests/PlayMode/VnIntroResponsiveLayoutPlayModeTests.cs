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
        public IEnumerator ResponsiveCompositionFits1280x720WithoutControlOrPortraitClipping()
        {
            yield return VerifyResponsiveComposition(1280, 720);
        }

        [UnityTest]
        public IEnumerator ResponsiveCompositionFits1024x768WithoutControlOrPortraitClipping()
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
                VnCharacterVisualState keiko = VnCharacterVisualCatalog.ResolveOrNeutral("keiko_neutral", "Keiko");
                VnCharacterVisualState mina = VnCharacterVisualCatalog.ResolveOrNeutral("mina_neutral", "Mina");
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                view.SetCharacterStage(keiko, mina);
                view.PresentLine("Keiko",
                    "The responsive proof keeps dialogue wrapping inside the polished lower panel while every control remains usable.");

                yield return new WaitForSecondsRealtime(.25f);
                Canvas.ForceUpdateCanvases();

                RectTransform root = GameObject.Find("VnIntroRoot").GetComponent<RectTransform>();
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));

                Rect canvasRect = new Rect(Vector2.zero,
                    CalculateVirtualCanvasSize(screenWidth, screenHeight, scaler.referenceResolution, scaler.matchWidthOrHeight));
                RectTransform panelTransform = GameObject.Find("DialoguePanel").GetComponent<RectTransform>();
                Rect panelRect = ResolveChildRect(panelTransform, canvasRect);
                AssertRectInside(panelRect, canvasRect, "DialoguePanel", screenWidth, screenHeight);

                RectTransform portraitMaskTransform = GameObject.Find("PortraitMask").GetComponent<RectTransform>();
                Rect portraitMaskRect = ResolveChildRect(portraitMaskTransform, panelRect);
                AssertRectInside(portraitMaskRect, canvasRect, "PortraitMask", screenWidth, screenHeight);

                RectTransform portraitFrameTransform = GameObject.Find("PortraitFrame").GetComponent<RectTransform>();
                Rect portraitFrameRect = ResolveChildRect(portraitFrameTransform, panelRect);
                AssertRectInside(portraitFrameRect, canvasRect, "PortraitFrame", screenWidth, screenHeight);
                Assert.That(portraitFrameRect.Overlaps(portraitMaskRect), Is.True,
                    $"Portrait trim must stay integrated with the portrait at {screenWidth}x{screenHeight}.");

                RectTransform controlsTransform = GameObject.Find("ControlsRow").GetComponent<RectTransform>();
                Rect controlsRect = ResolveChildRect(controlsTransform, panelRect);
                AssertRectInside(controlsRect, panelRect, "ControlsRow", screenWidth, screenHeight);

                foreach (string name in new[] { "MuteButton", "PauseButton", "SkipButton", "BackButton", "NextButton" })
                {
                    RectTransform buttonTransform = GameObject.Find(name).GetComponent<RectTransform>();
                    Rect buttonRect = ResolveChildRect(buttonTransform, controlsRect);
                    AssertRectInside(buttonRect, controlsRect, name, screenWidth, screenHeight);
                }

                Button back = GameObject.Find("BackButton").GetComponent<Button>();
                Button next = GameObject.Find("NextButton").GetComponent<Button>();
                Assert.That(back.interactable, Is.False);
                Assert.That(next.interactable, Is.True);

                RectTransform dialogueTransform = GameObject.Find("DialogueText").GetComponent<RectTransform>();
                Rect dialogueRect = ResolveChildRect(dialogueTransform, panelRect);
                AssertRectInside(dialogueRect, panelRect, "DialogueText", screenWidth, screenHeight);
                Assert.That(dialogueRect.xMin, Is.GreaterThanOrEqualTo(portraitMaskRect.xMax + 16f),
                    $"Dialogue text must clear the circular portrait at {screenWidth}x{screenHeight}.");
                Assert.That(dialogueRect.xMax, Is.LessThanOrEqualTo(controlsRect.xMin - 24f),
                    $"Dialogue text must keep a readable gap before controls at {screenWidth}x{screenHeight}.");

                Text dialogue = dialogueTransform.GetComponent<Text>();
                Assert.That(dialogue.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(dialogue.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));

                RectTransform primaryTransform = GameObject.Find("CharacterPrimary").GetComponent<RectTransform>();
                RectTransform secondaryTransform = GameObject.Find("CharacterSecondary").GetComponent<RectTransform>();
                Rect primaryRect = ScaleRectAroundPivot(ResolveChildRect(primaryTransform, canvasRect),
                    primaryTransform.pivot, primaryTransform.localScale);
                Rect secondaryRect = ScaleRectAroundPivot(ResolveChildRect(secondaryTransform, canvasRect),
                    secondaryTransform.pivot, secondaryTransform.localScale);

                Assert.That(primaryRect.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - 1f));
                Assert.That(primaryRect.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + 1f));
                Assert.That(secondaryRect.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - 1f));
                Assert.That(secondaryRect.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + 1f));
                Assert.That(primaryRect.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + 1f),
                    $"Speaker focus must not clip the primary character at the top of {screenWidth}x{screenHeight}.");
                Assert.That(secondaryRect.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + 1f),
                    $"Speaker focus must not clip the secondary character at the top of {screenWidth}x{screenHeight}.");
                Assert.That(primaryRect.center.x, Is.LessThan(canvasRect.center.x));
                Assert.That(secondaryRect.center.x, Is.GreaterThan(canvasRect.center.x));
                Assert.That(Mathf.Abs((canvasRect.center.x - primaryRect.center.x) -
                                      (secondaryRect.center.x - canvasRect.center.x)), Is.LessThan(2f),
                    $"Two-character staging should remain visually balanced at {screenWidth}x{screenHeight}.");

                Assert.That(primaryTransform.GetSiblingIndex(), Is.LessThan(panelTransform.GetSiblingIndex()));
                Assert.That(secondaryTransform.GetSiblingIndex(), Is.LessThan(panelTransform.GetSiblingIndex()));
            }
            finally
            {
                view.Dispose();
                Object.Destroy(host);
            }
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
