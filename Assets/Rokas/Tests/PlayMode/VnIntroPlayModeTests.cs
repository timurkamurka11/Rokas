using System;
using System.Collections;
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
