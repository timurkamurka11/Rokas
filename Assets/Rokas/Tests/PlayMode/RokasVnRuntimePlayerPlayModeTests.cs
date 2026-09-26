using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class RokasVnRuntimePlayerPlayModeTests
    {
        private GameObject host;
        private RokasVnRuntimeIntroPackage package;
        private readonly List<Object> owned = new List<Object>();

        [UnityTest]
        public IEnumerator PackagedPlayerBuildsRuntimePresentationAndCompletesExactlyOnce()
        {
            package = CreatePackage();
            host = new GameObject("RuntimeVnPlayerFixture");
            int completed = 0;

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, () => completed++);
            yield return null;

            Assert.That(Find("RokasVnRuntimeRoot"), Is.Not.Null);
            RawImage background = Find("VnBackground").GetComponent<RawImage>();
            Assert.That(background.texture, Is.SameAs(package.Assets[0].asset));

            RawImage mina = Find("VnCharacter_Mina").GetComponent<RawImage>();
            Assert.That(mina.texture, Is.SameAs(package.CharacterStates[0].texture));
            Assert.That(mina.gameObject.activeSelf, Is.True);

            RawImage plaque = Find("VnDialoguePlaque").GetComponent<RawImage>();
            Assert.That(plaque.texture, Is.SameAs(package.DialoguePlaque));
            Assert.That(Find("VnSpeaker").GetComponent<Text>().text, Is.EqualTo("Mina"));
            string initialDialogue = Find("VnDialogue").GetComponent<Text>().text;
            Assert.That("Первый кадр".StartsWith(initialDialogue), Is.True,
                "Initial runtime dialogue must be the authored typewriter prefix.");

            Assert.That(FindButton("VnForwardButton"), Is.Not.Null);
            Assert.That(FindButton("VnMuteButton"), Is.Not.Null);
            Assert.That(FindButton("VnMenuButton"), Is.Not.Null);

            Assert.That(player.RequestAdvance(), Is.False,
                "First advance while typewriter is incomplete must reveal the line only.");
            Assert.That(Find("VnDialogue").GetComponent<Text>().text,
                Is.EqualTo("Первый кадр"));
            Assert.That(player.RequestAdvance(), Is.True,
                "Second advance must move to the next Beat.");
            string terminalPrefix = Find("VnDialogue").GetComponent<Text>().text;
            Assert.That("Финал".StartsWith(terminalPrefix), Is.True,
                "Entering the terminal Beat must restart its authored typewriter.");

            Assert.That(player.RequestAdvance(), Is.False,
                "The terminal Beat still completes its typewriter before advancing.");
            Assert.That(Find("VnDialogue").GetComponent<Text>().text,
                Is.EqualTo("Финал"));
            Assert.That(player.RequestAdvance(), Is.True,
                "The next advance must enter the authored terminal fade.");
            player.TickForTests(.2f);
            Assert.That(Find("VnTerminalFade").GetComponent<CanvasGroup>().alpha,
                Is.GreaterThan(0f).And.LessThan(1f));
            player.TickForTests(.3f);
            Assert.That(completed, Is.EqualTo(1));

            player.TickForTests(5f);
            player.RequestAdvance();
            Assert.That(completed, Is.EqualTo(1),
                "Runtime player completion callback must be exactly-once.");

            player.Dispose();
            yield return null;
            Assert.That(Find("RokasVnRuntimeRoot"), Is.Null);
        }

        [UnityTest]
        public IEnumerator RuntimeUsesAuthoredFontBindingInsteadOfFallbackSans()
        {
            package = CreatePackage();
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.sans, Is.Not.Null);
            Assert.That(assets.serif, Is.Not.Null);
            Assert.That(assets.serif, Is.Not.SameAs(assets.sans));

            const string speakerFontGuid = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
            package.Snapshot.scenes[0].presentation.speakerFontAssetGuid =
                speakerFontGuid;
            package.Snapshot.scenes[0].presentation.speakerFontPreset = 1;

            var bindings = package.Assets.ToList();
            bindings.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = speakerFontGuid,
                displayName = assets.serif.name,
                kind = RokasVnRuntimeAssetKind.Font,
                asset = assets.serif
            });
            package.Configure(
                package.ProjectId,
                package.SourceProjectSha256,
                package.PortableProjectJson,
                package.Snapshot,
                bindings,
                package.CharacterStates.ToList(),
                package.DialoguePlaque,
                package.ControlSheet,
                package.MutedSpeaker,
                package.CompletionTriangle);

            host = new GameObject("RuntimeVnAuthoredFontFixture");
            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, null);
            yield return null;

            Text speaker = Find("VnSpeaker").GetComponent<Text>();
            Assert.That(
                speaker.font,
                Is.SameAs(assets.serif),
                "Runtime must resolve the same authored project Font as the immutable Preview oracle instead of reusing fallback sans.");

            player.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeUsesSceneAuthoredPlaqueVisualBinding()
        {
            package = CreatePackage();
            const string plaqueGuid =
                "12121212121212121212121212121212";
            Texture2D authoredPlaque =
                MakeTexture("RuntimeAuthoredPlaque");
            package.Snapshot.scenes[0].presentation
                .dialoguePlaqueAssetGuid = plaqueGuid;

            var bindings = package.Assets.ToList();
            bindings.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = plaqueGuid,
                displayName = authoredPlaque.name,
                kind = RokasVnRuntimeAssetKind.Texture,
                asset = authoredPlaque
            });
            package.Configure(
                package.ProjectId,
                package.SourceProjectSha256,
                package.PortableProjectJson,
                package.Snapshot,
                bindings,
                package.CharacterStates.ToList(),
                package.DialoguePlaque,
                package.ControlSheet,
                package.MutedSpeaker,
                package.CompletionTriangle);

            host = new GameObject(
                "RuntimeVnAuthoredPlaqueFixture");
            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(
                    host.transform,
                    package,
                    null);
            yield return null;

            Assert.That(
                Find("VnDialoguePlaque")
                    .GetComponent<RawImage>()
                    .texture,
                Is.SameAs(authoredPlaque));

            player.Dispose();
            yield return null;
        }

        [Test]
        public void RuntimeControlSemanticsMatchFrozen3dbOracleSamples()
        {
            Rect plaque =
                new Rect(100f, -98f, 1200f, 400f);
            RokasVnPlaqueUiLayout layout =
                RokasVnRuntimeUiSemantics.Layout(plaque);

            Assert.That(layout.Mute.x, Is.EqualTo(1102.4f).Within(.001f));
            Assert.That(layout.Mute.y, Is.EqualTo(120f).Within(.001f));
            Assert.That(layout.Mute.width, Is.EqualTo(40f).Within(.001f));
            Assert.That(layout.Forward.x, Is.EqualTo(1155.2f).Within(.001f));
            Assert.That(layout.Menu.x, Is.EqualTo(1208f).Within(.001f));
            Assert.That(layout.Triangle.x, Is.EqualTo(1182f).Within(.001f));
            Assert.That(layout.Triangle.y, Is.EqualTo(8.888889f).Within(.001f));
            Assert.That(layout.Triangle.width, Is.EqualTo(27.2f).Within(.001f));
            Assert.That(layout.Triangle.height, Is.EqualTo(30.222223f).Within(.001f));

            RokasVnTriangleUiSample triangle =
                RokasVnRuntimeUiSemantics.SampleTriangle(.1875f);
            Assert.That(triangle.OffsetY, Is.EqualTo(2.25f).Within(.001f));
            Assert.That(triangle.Scale, Is.EqualTo(1.025f).Within(.001f));
            Assert.That(triangle.Alpha, Is.EqualTo(.97f).Within(.001f));

            RokasVnButtonUiSample hover =
                RokasVnRuntimeUiSemantics.SampleButton(
                    new Rect(0f, 0f, 100f, 100f),
                    true,
                    false,
                    true,
                    1f);
            Assert.That(hover.Rect.width, Is.EqualTo(100f).Within(.001f));
            Assert.That(hover.Brightness, Is.EqualTo(.97f).Within(.001f));
            Assert.That(hover.Alpha, Is.EqualTo(.90f).Within(.001f));

            RokasVnButtonUiSample pressed =
                RokasVnRuntimeUiSemantics.SampleButton(
                    new Rect(0f, 0f, 100f, 100f),
                    true,
                    true,
                    true,
                    1f);
            Assert.That(pressed.Rect.width, Is.EqualTo(95f).Within(.001f));
            Assert.That(pressed.Brightness, Is.EqualTo(.92f).Within(.001f));
            Assert.That(pressed.Alpha, Is.EqualTo(.90f).Within(.001f));
        }

        [UnityTest]
        public IEnumerator PlaqueControlsAndCompletionIndicatorUseSharedPreviewSemantics()
        {
            package = CreatePackage();
            host = new GameObject("RuntimeVnUiParityFixture");
            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(
                    host.transform,
                    package,
                    null);
            yield return null;

            RectTransform plaque =
                Find("VnDialoguePlaque")
                .GetComponent<RectTransform>();
            Rect plaqueLogical = new Rect(
                1920f * .04f,
                -98f,
                plaque.sizeDelta.x,
                plaque.sizeDelta.y);
            RokasVnPlaqueUiLayout layout =
                RokasVnRuntimeUiSemantics.Layout(
                    plaqueLogical);

            AssertPlaqueRect(
                Find("VnMuteButton")
                    .GetComponent<RectTransform>(),
                layout.Mute,
                plaqueLogical);
            AssertPlaqueRect(
                Find("VnForwardButton")
                    .GetComponent<RectTransform>(),
                layout.Forward,
                plaqueLogical);
            AssertPlaqueRect(
                Find("VnMenuButton")
                    .GetComponent<RectTransform>(),
                layout.Menu,
                plaqueLogical);

            Button forward =
                FindButton("VnForwardButton");
            RawImage forwardGraphic =
                forward.targetGraphic as RawImage;
            Assert.That(forwardGraphic, Is.Not.Null);
            Assert.That(
                forward.transition,
                Is.EqualTo(Selectable.Transition.None));
            Assert.That(
                forwardGraphic.rectTransform.localScale.x,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .ButtonBaseScale)
                    .Within(.001f));
            Assert.That(
                forwardGraphic.color.r,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .ButtonBaseBrightness)
                    .Within(.001f));
            Assert.That(
                forwardGraphic.color.a,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .ButtonEnabledAlpha)
                    .Within(.001f));

            Assert.That(
                player.RequestAdvance(),
                Is.False,
                "First advance should complete the typewriter.");
            RectTransform triangle =
                Find("VnCompletionTriangle")
                .GetComponent<RectTransform>();
            Assert.That(
                triangle.gameObject.activeSelf,
                Is.True);
            AssertPlaqueRect(
                triangle,
                layout.Triangle,
                plaqueLogical);

            Vector2 triangleBase =
                triangle.anchoredPosition;
            player.TickForTests(.1875f);
            RokasVnTriangleUiSample sample =
                RokasVnRuntimeUiSemantics.SampleTriangle(
                    .1875f);
            Assert.That(
                triangle.anchoredPosition.x,
                Is.EqualTo(triangleBase.x)
                    .Within(.001f));
            Assert.That(
                triangle.anchoredPosition.y,
                Is.EqualTo(
                    triangleBase.y +
                    sample.OffsetY)
                    .Within(.001f));
            Assert.That(
                triangle.localScale.x,
                Is.EqualTo(sample.Scale)
                    .Within(.001f));
            RawImage triangleGraphic =
                triangle.GetComponent<RawImage>();
            Assert.That(
                triangleGraphic.color.a,
                Is.EqualTo(sample.Alpha)
                    .Within(.001f));

            FindButton("VnMenuButton")
                .onClick.Invoke();
            Assert.That(player.IsMenuOpen, Is.True);
            foreach (string name in new[]
            {
                "VnMuteButton",
                "VnForwardButton",
                "VnMenuButton"
            })
            {
                Button button = FindButton(name);
                Assert.That(
                    button.interactable,
                    Is.False,
                    name + " must be disabled while the modal VN menu is open.");
                RawImage graphic =
                    button.targetGraphic as RawImage;
                Assert.That(
                    graphic.color.a,
                    Is.EqualTo(
                        RokasVnRuntimeUiSemantics
                            .ButtonDisabledAlpha)
                        .Within(.001f));
            }

            player.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BackgroundFitFillAndStretchUsePreviewEquivalentAspectPolicies()
        {
            int[] modes = { 0, 1, 2 };
            AspectRatioFitter.AspectMode[] expected =
            {
                AspectRatioFitter.AspectMode.FitInParent,
                AspectRatioFitter.AspectMode.EnvelopeParent,
                AspectRatioFitter.AspectMode.None
            };

            for (int i = 0; i < modes.Length; i++)
            {
                package = CreatePackage();
                package.Snapshot.scenes[0].media.scaleMode =
                    modes[i];
                host = new GameObject(
                    "RuntimeVnMediaScaleMode" + modes[i]);

                RokasVnRuntimePlayer player =
                    RokasVnRuntimePlayer.Create(
                        host.transform,
                        package,
                        null);
                yield return null;

                RawImage background =
                    Find("VnBackground")
                    .GetComponent<RawImage>();
                AspectRatioFitter fitter =
                    background.GetComponent<
                        AspectRatioFitter>();
                Assert.That(fitter, Is.Not.Null);
                Assert.That(
                    background.uvRect,
                    Is.EqualTo(
                        new Rect(0f, 0f, 1f, 1f)));

                if (modes[i] == 2)
                {
                    Assert.That(
                        fitter.enabled,
                        Is.False,
                        "Stretch alone may break authored source aspect.");
                }
                else
                {
                    Assert.That(
                        fitter.enabled,
                        Is.True);
                    Assert.That(
                        fitter.aspectMode,
                        Is.EqualTo(expected[i]),
                        modes[i] == 0
                            ? "Fit must match Preview ScaleToFit."
                            : "Fill must match Preview ScaleAndCrop.");
                    Assert.That(
                        fitter.aspectRatio,
                        Is.EqualTo(
                            (float)background.texture.width /
                            background.texture.height)
                            .Within(.0001f));
                }

                player.Dispose();
                Object.Destroy(host);
                host = null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AuthoredBgmAndSfxRespectMuteAndDisposeWithRuntime()
        {
            package = CreateAudioPackage();
            host = new GameObject("RuntimeVnAudioFixture");

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, null);
            yield return null;

            GameObject runtimeRoot = Find("RokasVnRuntimeRoot");
            Assert.That(runtimeRoot, Is.Not.Null);
            AudioSource[] sources =
                runtimeRoot.GetComponentsInChildren<AudioSource>(true);

            AudioClip bgm = package.Assets
                .First(binding => binding.authoredKey == "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
                .asset as AudioClip;
            AudioClip sfx = package.Assets
                .First(binding => binding.authoredKey == "cccccccccccccccccccccccccccccccc")
                .asset as AudioClip;

            AudioSource bgmSource = sources.FirstOrDefault(source => source.clip == bgm);
            AudioSource sfxSource = sources.FirstOrDefault(source => source.clip == sfx);
            Assert.That(bgmSource, Is.Not.Null,
                "Scene-authored BGM must be owned by the runtime VN player.");
            Assert.That(sfxSource, Is.Not.Null,
                "Scene/Beat-authored SFX must be owned by the runtime VN player.");
            Assert.That(bgmSource.volume, Is.GreaterThan(0f));
            Assert.That(sfxSource.volume, Is.GreaterThan(0f));

            player.SetMuted(true);
            Assert.That(bgmSource.volume, Is.Zero);
            Assert.That(sfxSource.volume, Is.Zero);

            player.SetMuted(false);
            player.TickForTests(.02f);
            Assert.That(bgmSource.volume, Is.GreaterThan(0f));
            Assert.That(sfxSource.volume, Is.GreaterThan(0f));

            player.Dispose();
            yield return null;
            Assert.That(Find("RokasVnRuntimeRoot"), Is.Null,
                "VN-owned audio and controls must disappear before gameplay owns input/audio.");
        }

        [UnityTest]
        public IEnumerator RuntimeMenuUsesImmutableOracleDialogueFont()
        {
            package = CreatePackage();
            RokasAssets assets =
                Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.serif, Is.Not.Null);

            const string dialogueFontGuid =
                "abababababababababababababababab";
            RokasVnRuntimeSceneSnapshot scene =
                package.Snapshot.scenes[0];
            scene.presentation.dialogueFontAssetGuid =
                dialogueFontGuid;
            scene.presentation.dialogueFontPreset = 1;

            var bindings = package.Assets.ToList();
            bindings.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = dialogueFontGuid,
                displayName = assets.serif.name,
                kind = RokasVnRuntimeAssetKind.Font,
                asset = assets.serif
            });
            package.Configure(
                package.ProjectId,
                package.SourceProjectSha256,
                package.PortableProjectJson,
                package.Snapshot,
                bindings,
                package.CharacterStates.ToList(),
                package.DialoguePlaque,
                package.ControlSheet,
                package.MutedSpeaker,
                package.CompletionTriangle);

            host = new GameObject(
                "RuntimeVnMenuOracleFontFixture");
            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(
                    host.transform,
                    package,
                    null);
            yield return null;

            Text[] menuTexts =
                Find("VnRuntimeMenu")
                    .GetComponentsInChildren<Text>(true);
            Assert.That(menuTexts.Length, Is.EqualTo(5));
            Assert.That(
                menuTexts.All(item =>
                    item.font == assets.serif),
                Is.True,
                "Immutable 3db14827 menu renders title and rows with frame.DialogueFont.");

            player.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RuntimeMenuMatchesAuthoritativePreviewStructureAndFreezesPlayback()
        {
            package = CreatePackage();
            host = new GameObject(
                "RuntimeVnMenuParityFixture");
            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(
                    host.transform,
                    package,
                    null);
            yield return null;

            float before =
                player.Playback.BeatElapsedSeconds;
            FindButton("VnMenuButton")
                .onClick.Invoke();

            GameObject menu =
                Find("VnRuntimeMenu");
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.activeSelf, Is.True);
            Assert.That(player.IsMenuOpen, Is.True);

            Image shade =
                Find("Shade").GetComponent<Image>();
            Assert.That(
                shade.color,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .MenuShadeColor));

            Rect canvasLogical =
                new Rect(0f, 0f, 1920f, 1080f);
            RokasVnMenuUiLayout layout =
                RokasVnRuntimeUiSemantics.MenuLayout(
                    canvasLogical);
            Assert.That(
                layout.Panel,
                Is.EqualTo(
                    new Rect(740f, 335f, 440f, 410f)),
                "Frozen 3db14827 menu panel geometry changed.");
            Assert.That(
                layout.Title,
                Is.EqualTo(
                    new Rect(764f, 353f, 392f, 32f)),
                "Frozen 3db14827 menu title geometry changed.");
            Assert.That(
                RokasVnRuntimeUiSemantics.MenuTitleFontSize(
                    1080f),
                Is.EqualTo(24));
            Assert.That(
                RokasVnRuntimeUiSemantics.MenuRowFontSize(
                    48f),
                Is.EqualTo(17));
            Image panel =
                Find("VnMenuPanel")
                .GetComponent<Image>();
            Assert.That(
                panel.color,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .MenuPanelColor));
            AssertCanvasTopLeftRect(
                panel.rectTransform,
                layout.Panel,
                canvasLogical);

            Text title =
                Find("VnMenuTitle")
                .GetComponent<Text>();
            Assert.That(
                title.text,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .MenuTitle));
            Assert.That(
                title.color,
                Is.EqualTo(
                    RokasVnRuntimeUiSemantics
                        .MenuTitleColor));
            AssertCanvasTopLeftRect(
                title.rectTransform,
                layout.Title,
                canvasLogical);

            string[] names =
            {
                "VnResumeButton",
                "VnSettingsButton",
                "VnSaveButton",
                "VnMainMenuButton"
            };
            Rect[] rows =
            {
                layout.Resume,
                layout.Settings,
                layout.Save,
                layout.MainMenu
            };
            for (int i = 0;
                 i < names.Length;
                 i++)
            {
                Button button =
                    FindButton(names[i]);
                Assert.That(button, Is.Not.Null);
                Assert.That(
                    button.interactable,
                    Is.EqualTo(i == 0));
                Text label =
                    button.GetComponentInChildren<
                        Text>(true);
                Assert.That(
                    label.text,
                    Is.EqualTo(
                        RokasVnRuntimeUiSemantics
                            .MenuLabel(i)));
                Assert.That(
                    label.alignment,
                    Is.EqualTo(
                        TextAnchor.MiddleLeft));
                AssertCanvasTopLeftRect(
                    button.GetComponent<
                        RectTransform>(),
                    rows[i],
                    canvasLogical);
            }

            player.TickForTests(1f);
            Assert.That(
                player.Playback.BeatElapsedSeconds,
                Is.EqualTo(before)
                    .Within(.001f),
                "Modal VN menu must freeze dialogue/typewriter presentation.");

            FindButton("VnResumeButton")
                .onClick.Invoke();
            Assert.That(player.IsMenuOpen, Is.False);
            Assert.That(menu.activeSelf, Is.False);

            player.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredFadeUsesOracleSoftForegroundAndBackgroundComposition()
        {
            package = CreateTransitionPackage();
            package.Snapshot.scenes[1].sceneTransitionType = 2;
            host = new GameObject("RuntimeVnFadeTransitionFixture");

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, null);
            yield return null;

            RawImage background =
                Find("VnBackground").GetComponent<RawImage>();
            Texture firstBackground = package.Assets
                .First(binding =>
                    binding.authoredKey ==
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                .asset as Texture;
            Texture secondBackground = package.Assets
                .First(binding =>
                    binding.authoredKey ==
                    "ffffffffffffffffffffffffffffffff")
                .asset as Texture;

            Assert.That(background.texture, Is.SameAs(firstBackground));
            Assert.That(player.RequestAdvance(), Is.True);

            CanvasGroup overlay =
                Find("VnSceneTransitionOverlay")
                    .GetComponent<CanvasGroup>();
            CanvasGroup characterForeground =
                Find("VnCharacters")
                    .GetComponent<CanvasGroup>();
            CanvasGroup plaqueForeground =
                Find("VnDialoguePlaque")
                    .GetComponent<CanvasGroup>();
            RawImage sourceBackground =
                Find("VnTransitionSourceBackground")
                    .GetComponent<RawImage>();

            player.TickForTests(.10f);
            Assert.That(
                overlay.alpha,
                Is.EqualTo(.11f).Within(.005f),
                "Immutable Preview Fade uses only the .22 max dark overlay, not a near-black fullscreen fade.");
            Assert.That(
                characterForeground.alpha,
                Is.EqualTo(.5f).Within(.01f));
            Assert.That(
                plaqueForeground.alpha,
                Is.EqualTo(.5f).Within(.01f));
            Assert.That(
                player.Playback.CurrentSceneIndex,
                Is.Zero);

            player.TickForTests(.10f);
            Assert.That(
                player.Playback.CurrentSceneIndex,
                Is.EqualTo(1));
            Assert.That(
                sourceBackground.texture,
                Is.SameAs(firstBackground),
                "Outgoing background must stay available under the incoming image during Fade reveal.");
            Assert.That(
                sourceBackground.gameObject.activeSelf,
                Is.True);
            Assert.That(
                background.texture,
                Is.SameAs(secondBackground));
            Assert.That(
                background.color.a,
                Is.Zero.Within(.01f),
                "Incoming background begins the reveal fully transparent, matching Preview composition.");

            player.TickForTests(.10f);
            Assert.That(
                background.color.a,
                Is.EqualTo(.5f).Within(.03f),
                "Incoming background must crossfade over the retained outgoing frame.");
            Assert.That(
                overlay.alpha,
                Is.EqualTo(.11f).Within(.005f));
            Assert.That(
                characterForeground.alpha,
                Is.EqualTo(.5f).Within(.01f));
            Assert.That(
                plaqueForeground.alpha,
                Is.EqualTo(.5f).Within(.01f));

            player.TickForTests(.10f);
            Assert.That(player.IsSceneTransitionActive, Is.False);
            Assert.That(background.color.a, Is.EqualTo(1f).Within(.001f));
            Assert.That(sourceBackground.gameObject.activeSelf, Is.False);
            Assert.That(characterForeground.alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(plaqueForeground.alpha, Is.EqualTo(1f).Within(.001f));

            player.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoredDarkCurtainCoversSceneSwapAndLocksAdvance()
        {
            package = CreateTransitionPackage();
            host = new GameObject("RuntimeVnTransitionFixture");

            RokasVnRuntimePlayer player =
                RokasVnRuntimePlayer.Create(host.transform, package, null);
            yield return null;

            RawImage background = Find("VnBackground").GetComponent<RawImage>();
            GameObject outgoingCharacter = Find("VnCharacter_Mina");
            Assert.That(outgoingCharacter, Is.Not.Null);
            Texture firstBackground = package.Assets
                .First(binding => binding.authoredKey == "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
                .asset as Texture;
            Texture secondBackground = package.Assets
                .First(binding => binding.authoredKey == "ffffffffffffffffffffffffffffffff")
                .asset as Texture;
            Assert.That(background.texture, Is.SameAs(firstBackground));

            Assert.That(player.RequestAdvance(), Is.True);
            Assert.That(player.IsSceneTransitionActive, Is.True);
            Assert.That(player.Playback.CurrentSceneIndex, Is.Zero,
                "Incoming Scene must not become interactive before the cover midpoint.");

            CanvasGroup overlay =
                Find("VnSceneTransitionOverlay").GetComponent<CanvasGroup>();
            Assert.That(overlay.blocksRaycasts, Is.True);

            player.TickForTests(.10f);
            Assert.That(overlay.alpha, Is.EqualTo(1f).Within(.001f),
                "The authored wipe must be opaque everywhere it covers the outgoing frame.");
            Assert.That(player.Playback.CurrentSceneIndex, Is.Zero);
            Assert.That(Find("VnCharacter_Mina"), Is.SameAs(outgoingCharacter),
                "Outgoing character render state must remain intact until the covered swap point.");
            Assert.That(player.RequestAdvance(), Is.False,
                "Dialogue input must be locked during the authored Scene boundary.");

            player.TickForTests(.10f);
            Assert.That(player.Playback.CurrentSceneIndex, Is.EqualTo(1));
            Assert.That(background.texture, Is.SameAs(secondBackground),
                "Background swap must happen under the authored dark cover.");
            Assert.That(overlay.alpha, Is.EqualTo(1f).Within(.001f),
                "The Scene swap must commit while the authored curtain is fully closed.");
            Assert.That(outgoingCharacter.activeSelf, Is.False,
                "Outgoing character GameObjects must stop rendering synchronously at the swap.");
            Assert.That(outgoingCharacter.transform.parent, Is.Null,
                "Outgoing character GameObjects must leave the runtime render tree synchronously.");
            Assert.That(Find("VnCharacter_Mina"), Is.Null,
                "No outgoing character render entry may survive into the incoming Scene tree.");

            GameObject incomingCharacter = Find("VnCharacter_Keiko");
            Assert.That(incomingCharacter, Is.Not.Null,
                "Incoming character render state must already be prepared under the closed curtain.");
            Assert.That(incomingCharacter.activeSelf, Is.True);
            RawImage incomingImage = incomingCharacter.GetComponent<RawImage>();
            Texture incomingTexture = package.CharacterStates
                .First(binding => binding.stateId == "keiko_runtime").texture;
            Assert.That(incomingImage.texture, Is.SameAs(incomingTexture));

            player.TickForTests(.20f);
            Assert.That(player.IsSceneTransitionActive, Is.False);
            Assert.That(overlay.alpha, Is.Zero.Within(.001f));
            Assert.That(overlay.blocksRaycasts, Is.False);

            player.Dispose();
            yield return null;
        }

        [Test]
        public void RuntimePlayerLivesInPlayerAssemblyWithoutUnityEditorReference()
        {
            string[] references = typeof(RokasVnRuntimePlayer).Assembly
                .GetReferencedAssemblies()
                .Select(item => item.Name)
                .ToArray();
            Assert.That(references, Does.Not.Contain("UnityEditor"));
        }

        private RokasVnRuntimeIntroPackage CreatePackage()
        {
            var background = MakeTexture("RuntimeBackground");
            var character = MakeTexture("RuntimeMina");
            var plaque = MakeTexture("RuntimePlaque");
            var controls = MakeTexture("RuntimeControls");
            var mute = MakeTexture("RuntimeMuted");
            var triangle = MakeTexture("RuntimeTriangle");

            var scene = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "11111111111111111111111111111111",
                label = "Final",
                isTerminal = true,
                terminalFadeDuration = .4f,
                media = new RokasVnRuntimeMediaSnapshot
                {
                    kind = 1,
                    reference = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    runtimeAssetKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    scaleMode = 1
                }
            };
            scene.characters.Add(new RokasVnRuntimeCharacterSnapshot
            {
                characterId = "Mina",
                stateId = "mina_runtime",
                stageSlot = 1
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "22222222222222222222222222222222",
                speaker = "Mina",
                text = "Первый кадр"
            });
            scene.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "33333333333333333333333333333333",
                speaker = "Mina",
                text = "Финал"
            });

            var snapshot = new RokasVnRuntimeIntroSnapshot
            {
                projectId = RokasVnRuntimeIntroPackage.ExpectedProjectId,
                sourceProjectSha256 = "runtime-player-fixture",
                sceneCount = 1,
                beatCount = 2
            };
            snapshot.scenes.Add(scene);

            var result = ScriptableObject.CreateInstance<RokasVnRuntimeIntroPackage>();
            owned.Add(result);
            result.Configure(
                RokasVnRuntimeIntroPackage.ExpectedProjectId,
                "runtime-player-fixture",
                "{}",
                snapshot,
                new[]
                {
                    new RokasVnRuntimeAssetBinding
                    {
                        authoredKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                        displayName = "RuntimeBackground",
                        kind = RokasVnRuntimeAssetKind.Texture,
                        asset = background
                    }
                },
                new[]
                {
                    new RokasVnRuntimeCharacterStateBinding
                    {
                        stateId = "mina_runtime",
                        characterId = "Mina",
                        texture = character,
                        bodyUv = new Rect(0f, 0f, 1f, 1f)
                    }
                },
                plaque, controls, mute, triangle);
            return result;
        }

        private RokasVnRuntimeIntroPackage CreateTransitionPackage()
        {
            RokasVnRuntimeIntroPackage result = CreatePackage();
            Texture2D secondBackground = MakeTexture("RuntimeBackgroundTwo");
            Texture2D secondCharacter = MakeTexture("RuntimeKeiko");

            RokasVnRuntimeSceneSnapshot first = result.Snapshot.scenes[0];
            first.label = "Before Transition";
            first.isTerminal = false;
            first.dialogueBeats.Clear();
            first.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "44444444444444444444444444444444",
                speaker = "Mina",
                text = string.Empty
            });

            var second = new RokasVnRuntimeSceneSnapshot
            {
                sceneId = "55555555555555555555555555555555",
                label = "After Transition",
                isTerminal = true,
                terminalFadeDuration = .4f,
                sceneTransitionType = 1,
                sceneTransitionDirection = 0,
                sceneTransitionDuration = .4f,
                media = new RokasVnRuntimeMediaSnapshot
                {
                    kind = 1,
                    reference = "ffffffffffffffffffffffffffffffff",
                    runtimeAssetKey = "ffffffffffffffffffffffffffffffff",
                    scaleMode = 1
                }
            };
            second.characters.Add(new RokasVnRuntimeCharacterSnapshot
            {
                characterId = "Keiko",
                stateId = "keiko_runtime",
                stageSlot = 1
            });
            second.dialogueBeats.Add(new RokasVnRuntimeBeatSnapshot
            {
                beatId = "66666666666666666666666666666666",
                speaker = "Keiko",
                text = string.Empty
            });

            result.Snapshot.scenes.Clear();
            result.Snapshot.scenes.Add(first);
            result.Snapshot.scenes.Add(second);
            result.Snapshot.sceneCount = 2;
            result.Snapshot.beatCount = 2;

            var assets = result.Assets.ToList();
            assets.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = "ffffffffffffffffffffffffffffffff",
                displayName = "RuntimeBackgroundTwo",
                kind = RokasVnRuntimeAssetKind.Texture,
                asset = secondBackground
            });
            var characterStates = result.CharacterStates.ToList();
            characterStates.Add(new RokasVnRuntimeCharacterStateBinding
            {
                stateId = "keiko_runtime",
                characterId = "Keiko",
                texture = secondCharacter,
                bodyUv = new Rect(0f, 0f, 1f, 1f)
            });
            result.Configure(
                result.ProjectId,
                result.SourceProjectSha256,
                result.PortableProjectJson,
                result.Snapshot,
                assets,
                characterStates,
                result.DialoguePlaque,
                result.ControlSheet,
                result.MutedSpeaker,
                result.CompletionTriangle);
            return result;
        }

        private RokasVnRuntimeIntroPackage CreateAudioPackage()
        {
            var result = CreatePackage();
            RokasVnRuntimeSceneSnapshot scene = result.Snapshot.scenes[0];

            AudioClip bgm = AudioClip.Create(
                "RuntimeBgm", 4410, 1, 44100, false);
            AudioClip sfx = AudioClip.Create(
                "RuntimeSfx", 4410, 1, 44100, false);
            owned.Add(bgm);
            owned.Add(sfx);

            scene.music = new RokasVnRuntimeMusicSnapshot
            {
                mode = 1,
                assetGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                displayName = "Runtime BGM",
                volume = .65f,
                loop = true
            };
            scene.additionalAudioCues.Add(
                new RokasVnRuntimeAudioCueSnapshot
                {
                    cueId = "dddddddddddddddddddddddddddddddd",
                    displayName = "Runtime SFX",
                    assetGuid = "cccccccccccccccccccccccccccccccc",
                    enabled = true,
                    category = 0,
                    volume = .55f,
                    loop = true,
                    trigger = 0,
                    startDelaySeconds = 0f,
                    stopMode = 1
                });

            var assets = result.Assets.ToList();
            assets.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                displayName = "Runtime BGM",
                kind = RokasVnRuntimeAssetKind.Audio,
                asset = bgm
            });
            assets.Add(new RokasVnRuntimeAssetBinding
            {
                authoredKey = "cccccccccccccccccccccccccccccccc",
                displayName = "Runtime SFX",
                kind = RokasVnRuntimeAssetKind.Audio,
                asset = sfx
            });

            result.Configure(
                result.ProjectId,
                result.SourceProjectSha256,
                result.PortableProjectJson,
                result.Snapshot,
                assets,
                result.CharacterStates.ToList(),
                result.DialoguePlaque,
                result.ControlSheet,
                result.MutedSpeaker,
                result.CompletionTriangle);
            return result;
        }

        private Texture2D MakeTexture(string name)
        {
            var texture = new Texture2D(8, 8) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            owned.Add(texture);
            return texture;
        }

        private GameObject Find(string name)
        {
            if (host == null) return null;
            foreach (Transform item in host.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        private Button FindButton(string name)
        {
            GameObject go = Find(name);
            return go != null ? go.GetComponent<Button>() : null;
        }

        private static void AssertCanvasTopLeftRect(
            RectTransform actual,
            Rect expected,
            Rect canvasLogical)
        {
            Vector2 expectedPosition =
                new Vector2(
                    expected.center.x -
                    canvasLogical.center.x,
                    canvasLogical.center.y -
                    expected.center.y);
            Assert.That(
                actual.sizeDelta.x,
                Is.EqualTo(expected.width)
                    .Within(.01f));
            Assert.That(
                actual.sizeDelta.y,
                Is.EqualTo(expected.height)
                    .Within(.01f));
            Assert.That(
                actual.anchoredPosition.x,
                Is.EqualTo(expectedPosition.x)
                    .Within(.01f));
            Assert.That(
                actual.anchoredPosition.y,
                Is.EqualTo(expectedPosition.y)
                    .Within(.01f));
        }

        private static void AssertPlaqueRect(
            RectTransform actual,
            Rect expected,
            Rect plaqueLogical)
        {
            Vector2 expectedPosition = new Vector2(
                expected.center.x -
                plaqueLogical.center.x,
                expected.center.y -
                plaqueLogical.y);
            Assert.That(
                actual.sizeDelta.x,
                Is.EqualTo(expected.width)
                    .Within(.01f));
            Assert.That(
                actual.sizeDelta.y,
                Is.EqualTo(expected.height)
                    .Within(.01f));
            Assert.That(
                actual.anchoredPosition.x,
                Is.EqualTo(expectedPosition.x)
                    .Within(.01f));
            Assert.That(
                actual.anchoredPosition.y,
                Is.EqualTo(expectedPosition.y)
                    .Within(.01f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (host != null) Object.Destroy(host);
            yield return null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            host = null;
            package = null;
        }
    }
}
