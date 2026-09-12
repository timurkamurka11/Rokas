using System.Collections;
using System.Linq;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rokas.Tests
{
    public sealed class VnIntroPlayModeTests
    {
        [UnityTest]
        public IEnumerator TransientMuteDoesNotMutateSavedVolumeSettings()
        {
            var settings = new SettingsData
            {
                masterVolume = .73f,
                musicVolume = .41f,
                sfxVolume = .62f
            };
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
                Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator TransientMuteSilencesAndRestoresLoopOutput()
        {
            var settings = new SettingsData
            {
                masterVolume = .8f,
                musicVolume = .6f,
                sfxVolume = .75f
            };
            RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
            Assert.That(assets, Is.Not.Null);
            Assert.That(assets.homeAmbience, Is.Not.Null);
            var host = new GameObject("VnMuteRestoreFixture");
            var audio = new RokasAudio(host, assets, settings);
            try
            {
                audio.SetLocation(false);
                audio.Tick(.2f, true);
                AudioSource ambience = host.GetComponentsInChildren<AudioSource>(true)
                    .First(source => source.clip == assets.homeAmbience);
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
                Object.Destroy(host);
            }
        }
    }
}
