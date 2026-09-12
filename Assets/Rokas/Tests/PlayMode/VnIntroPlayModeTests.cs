using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Rokas.Tests
{
    public sealed class VnIntroPlayModeTests
    {
        private GameObject launchRoot;
        private string storyMediaPath;
        private string hiddenStoryMediaPath;
        private bool previousIgnoreFailingMessages;

        [UnityTest]
        public IEnumerator FirstEnterWorldBuildsVnUnderFullBlackBeforeHomeAndKeepsFlagIncomplete()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitForLaunchObject("RokasMainMenu", 1.5f);

            Button enter = FindLaunchButton("EnterWorldButton");
            Assert.That(enter, Is.Not.Null);
            enter.onClick.Invoke();
            yield return null;

            Assert.That(FindLaunchObject("RokasMainMenu"), Is.Not.Null,
                "Enter World must preserve the menu during the partial fade-to-black.");
            Assert.That(FindLaunchObject("HomeTitle"), Is.Null,
                "Home must not be constructed during the initial partial fade.");

            yield return WaitForLaunchObject("VnIntroRoot", 1.25f);

            Assert.That(FindLaunchObject("RokasMainMenu"), Is.Null,
                "The menu must be disposed at the full-black handoff.");
            Assert.That(FindLaunchObject("HomeTitle"), Is.Null,
                "First-time Enter World must not build Home behind the VN intro.");
            Assert.That(boot.View, Is.Null);
            Assert.That(PlayerPrefs.GetInt(PlayerPrefsVnIntroProgress.CompletedKey, 0), Is.Zero,
                "The intro completion flag must remain unset while the VN is still active.");

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            RawImage background = FindLaunchObject("Background").GetComponent<RawImage>();
            Assert.That(background.texture, Is.SameAs(assets.vnBusStopRainNight),
                "The first revealed VN beat must be the approved rainy bus-stop art.");
        }

        [UnityTest]
        public IEnumerator NaturalCompletionAdvancesOneBeatPerClickThenBuildsHomeBeforeMarkingComplete()
        {
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            HideStoryMediaForDeterministicLinuxFallback();
            RokasBootstrap boot = CreateRealLaunchIgnoringHostDecoderErrors();
            yield return WaitForLaunchObject("RokasMainMenu", 1.5f);

            FindLaunchButton("EnterWorldButton").onClick.Invoke();
            yield return WaitForLaunchObject("VnIntroRoot", 1.25f);

            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Button story = FindLaunchButton("StoryClickSurface");
            Assert.That(story, Is.Not.Null);

            story.onClick.Invoke();
            yield return WaitForLaunchBackground(assets.vnNightSkyRain, .75f);
            Assert.That(FindLaunchObject("HomeTitle"), Is.Null);
            Assert.That(PlayerPrefs.GetInt(PlayerPrefsVnIntroProgress.CompletedKey, 0), Is.Zero);

            story.onClick.Invoke();
            yield return WaitForLaunchBackground(assets.vnBusStopPhoneMessageMina, .75f);
            Assert.That(FindLaunchObject("HomeTitle"), Is.Null);
            Assert.That(PlayerPrefs.GetInt(PlayerPrefsVnIntroProgress.CompletedKey, 0), Is.Zero);

            story.onClick.Invoke();
            yield return WaitForLaunchObject("HomeTitle", 1.5f);

            Assert.That(boot.View, Is.Not.Null,
                "The existing Home/RokasView must be constructed before intro completion is persisted.");
            Assert.That(PlayerPrefs.GetInt(PlayerPrefsVnIntroProgress.CompletedKey, 0), Is.EqualTo(1),
                "Completion may be persisted only after Home exists.");
            Assert.That(FindLaunchObject("VnIntroRoot"), Is.Null,
                "The VN must be disposed during the shared Home handoff.");
        }

        [UnityTest]
        public IEnumerator TransientMuteDoesNotMutateSavedVolumeSettings()
        {
            var settings = new SettingsData { masterVolume = .73f, musicVolume = .41f, sfxVolume = .62f };
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            var host = new GameObject("VnMuteFixture");
            var audio = new RokasAudio(host, assets, settings);
            try
            {
                audio.SetVnMuted(true);
                Assert.That(audio.VnMuted, Is.True);
                Assert.That(settings.masterVolume, Is.EqualTo(.73f));
                Assert.That(settings.musicVolume, Is.EqualTo(.41f));
                Assert.That(settings.sfxVolume, Is.EqualTo(.62f));
                audio.SetVnMuted(false);
                Assert.That(audio.VnMuted, Is.False);
                Assert.That(settings.masterVolume, Is.EqualTo(.73f));
                Assert.That(settings.musicVolume, Is.EqualTo(.41f));
                Assert.That(settings.sfxVolume, Is.EqualTo(.62f));
                yield return null;
            }
            finally
            {
                audio.Dispose();
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator TransientMuteSilencesAndRestoresLoopOutput()
        {
            var settings = new SettingsData { masterVolume = .8f, musicVolume = .6f, sfxVolume = .75f };
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.homeAmbience, Is.Not.Null);
            var host = new GameObject("VnMuteRestoreFixture");
            var audio = new RokasAudio(host, assets, settings);
            try
            {
                audio.SetLocation(false);
                audio.Tick(.2f, true);
                AudioSource ambience = host.GetComponentsInChildren<AudioSource>(true).First(source => source.clip == assets.homeAmbience);
                Assert.That(ambience.volume, Is.GreaterThan(0f));
                audio.SetVnMuted(true);
                audio.Tick(.2f, true);
                Assert.That(ambience.volume, Is.EqualTo(0f).Within(.0001f));
                audio.SetVnMuted(false);
                audio.Tick(.2f, true);
                Assert.That(ambience.volume, Is.GreaterThan(0f));
                Assert.That(settings.masterVolume, Is.EqualTo(.8f));
                Assert.That(settings.musicVolume, Is.EqualTo(.6f));
                Assert.That(settings.sfxVolume, Is.EqualTo(.75f));
                yield return null;
            }
            finally
            {
                audio.Dispose();
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator VnViewUsesApprovedArtAndCropsCharacterSheets()
        {
            RokasAssets baseAssets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(baseAssets, Is.Not.Null);
            VnIntroArt art = CreateArt();
            var host = new GameObject("VnViewFixture");
            VnIntroView view = VnIntroView.Create(host.transform, baseAssets.sans, art, () => { }, () => { }, () => { }, () => { });
            try
            {
                view.ApplyBeat(VnIntroController.MapBeat("bus_stop", "Keiko", "dark", "keiko_neutral"));
                yield return null;
                Assert.That(GameObject.Find("VnIntroRoot"), Is.Not.Null);
                Assert.That(GameObject.Find("Background").GetComponent<RawImage>().texture, Is.SameAs(art.BusStopRainNight));
                Assert.That(GameObject.Find("DialoguePanel").GetComponent<RawImage>().texture, Is.SameAs(art.DialoguePanelKeikoDark));
                RawImage portrait = GameObject.Find("Portrait").GetComponent<RawImage>();
                Assert.That(portrait.texture, Is.SameAs(art.KeikoCharacterSheet));
                Assert.That(portrait.uvRect, Is.Not.EqualTo(new Rect(0f, 0f, 1f, 1f)));

                view.ApplyBeat(VnIntroController.MapBeat("phone", "Mina", "light", "mina_neutral"));
                yield return null;
                Assert.That(GameObject.Find("Background").GetComponent<RawImage>().texture, Is.SameAs(art.BusStopPhoneMessageMina));
                Assert.That(GameObject.Find("DialoguePanel").GetComponent<RawImage>().texture, Is.SameAs(art.DialoguePanelMinaLight));
                portrait = GameObject.Find("Portrait").GetComponent<RawImage>();
                Assert.That(portrait.texture, Is.SameAs(art.MinaCharacterSheet));
                Assert.That(portrait.uvRect, Is.Not.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            }
            finally
            {
                view.Dispose();
                UnityEngine.Object.Destroy(host);
                DestroyArt(art);
            }
        }

        [UnityTest]
        public IEnumerator VnControlsUseProvidedIconsAndNeverAdvanceStory()
        {
            RokasAssets baseAssets = Resources.Load<RokasAssets>("RokasAssets");
            VnIntroArt art = CreateArt();
            var host = new GameObject("VnControlFixture");
            int advances = 0, mutes = 0, pauses = 0, skips = 0;
            VnIntroView view = VnIntroView.Create(host.transform, baseAssets.sans, art,
                () => advances++, () => mutes++, () => pauses++, () => skips++);
            try
            {
                yield return null;
                Button story = GameObject.Find("StoryClickSurface").GetComponent<Button>();
                Button mute = GameObject.Find("MuteButton").GetComponent<Button>();
                Button pause = GameObject.Find("PauseButton").GetComponent<Button>();
                Button skip = GameObject.Find("SkipButton").GetComponent<Button>();
                Assert.That(story, Is.Not.Null);
                Assert.That(mute.transform.Find("Icon").GetComponent<RawImage>().texture, Is.SameAs(art.IconMute));
                Assert.That(pause.transform.Find("Icon").GetComponent<RawImage>().texture, Is.SameAs(art.IconPause));
                Assert.That(skip.transform.Find("Icon").GetComponent<RawImage>().texture, Is.SameAs(art.IconSkip));

                mute.onClick.Invoke();
                pause.onClick.Invoke();
                skip.onClick.Invoke();
                Assert.That(advances, Is.Zero);
                Assert.That(mutes, Is.EqualTo(1));
                Assert.That(pauses, Is.EqualTo(1));
                Assert.That(skips, Is.EqualTo(1));

                story.onClick.Invoke();
                Assert.That(advances, Is.EqualTo(1));
                story.onClick.Invoke();
                Assert.That(advances, Is.EqualTo(2));
            }
            finally
            {
                view.Dispose();
                UnityEngine.Object.Destroy(host);
                DestroyArt(art);
            }
        }

        [Test]
        public void RokasAssetsExposeExactlyTheTenRequiredVnTextures()
        {
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            var art = new VnIntroArt(
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

            Assert.That(art.AllTextures.Length, Is.EqualTo(10));
            Assert.That(art.AllTextures.All(texture => texture != null), Is.True);
            Assert.That(assets.IsComplete(), Is.True);
        }

        private RokasBootstrap CreateRealLaunchIgnoringHostDecoderErrors()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            launchRoot = new GameObject("VnIntroLaunchFixture");
            return launchRoot.AddComponent<RokasBootstrap>();
        }

        private void HideStoryMediaForDeterministicLinuxFallback()
        {
            storyMediaPath = Path.Combine(Application.streamingAssetsPath, "RokasVideo", "StoryIntro.mp4");
            Assert.That(File.Exists(storyMediaPath), Is.True,
                "Focused CI must contain the real StoryIntro.mp4; this helper only hides it from Linux VideoPlayer.");
            hiddenStoryMediaPath = Path.Combine(Path.GetTempPath(), "rokas-vn-hidden-story-" + Guid.NewGuid().ToString("N") + ".mp4");
            File.Move(storyMediaPath, hiddenStoryMediaPath);
        }

        private IEnumerator WaitForLaunchObject(string objectName, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (FindLaunchObject(objectName) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(FindLaunchObject(objectName), Is.Not.Null,
                "Timed out waiting for launch object: " + objectName + ".");
        }

        private IEnumerator WaitForLaunchBackground(Texture expected, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                GameObject backgroundObject = FindLaunchObject("Background");
                if (backgroundObject != null)
                {
                    RawImage image = backgroundObject.GetComponent<RawImage>();
                    if (image != null && image.texture == expected)
                        yield break;
                }
                yield return null;
            }

            GameObject finalBackgroundObject = FindLaunchObject("Background");
            RawImage finalImage = finalBackgroundObject != null ? finalBackgroundObject.GetComponent<RawImage>() : null;
            Assert.That(finalImage, Is.Not.Null, "Timed out waiting for the VN Background object.");
            Assert.That(finalImage.texture, Is.SameAs(expected), "Timed out waiting for the expected VN background beat.");
        }

        private Button FindLaunchButton(string name)
        {
            if (launchRoot == null) return null;
            foreach (Button button in launchRoot.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        private GameObject FindLaunchObject(string name)
        {
            if (launchRoot == null) return null;
            foreach (Transform item in launchRoot.GetComponentsInChildren<Transform>(true))
                if (item.name == name) return item.gameObject;
            return null;
        }

        [UnityTearDown]
        public IEnumerator CleanupLaunchFixture()
        {
            LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            PlayerPrefs.DeleteKey(PlayerPrefsVnIntroProgress.CompletedKey);
            PlayerPrefs.Save();
            if (launchRoot != null) UnityEngine.Object.Destroy(launchRoot);
            yield return null;
            if (!string.IsNullOrEmpty(hiddenStoryMediaPath) && File.Exists(hiddenStoryMediaPath) &&
                !string.IsNullOrEmpty(storyMediaPath) && !File.Exists(storyMediaPath))
                File.Move(hiddenStoryMediaPath, storyMediaPath);
            launchRoot = null;
            storyMediaPath = null;
            hiddenStoryMediaPath = null;
        }

        private static VnIntroArt CreateArt()
        {
            return new VnIntroArt(
                MakeTexture("Keiko"), MakeTexture("Mina"), MakeTexture("BusStop"), MakeTexture("NightSky"),
                MakeTexture("Phone"), MakeTexture("KeikoPanel"), MakeTexture("MinaPanel"),
                MakeTexture("Mute"), MakeTexture("Pause"), MakeTexture("Skip"));
        }

        private static Texture2D MakeTexture(string name)
        {
            var texture = new Texture2D(8, 8) { name = name };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            return texture;
        }

        private static void DestroyArt(VnIntroArt art)
        {
            foreach (Texture2D texture in art.AllTextures)
                UnityEngine.Object.Destroy(texture);
        }
    }
}
