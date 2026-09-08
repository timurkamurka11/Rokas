using Rokas.Core;
using UnityEngine;

namespace Rokas.Presentation
{
    public sealed class RokasAudio
    {
        private readonly RokasAssets assets;
        private readonly SettingsData settings;
        private readonly RokasBootstrap bootstrap;
        private readonly AudioSource ambience;
        private readonly AudioSource music;
        private readonly AudioSource[] effects = new AudioSource[4];
        private int voice;
        private bool mission;
        private bool hasLocation;
        private bool laptopMode;
        private bool lampStateKnown;
        private bool lampOn;

        public RokasAudio(GameObject parent, RokasAssets assets, SettingsData settings)
        {
            this.assets = assets;
            this.settings = settings;
            bootstrap = parent.GetComponent<RokasBootstrap>();
            if (bootstrap && bootstrap.Session != null)
            {
                lampOn = bootstrap.Session.State.lampOn;
                lampStateKnown = true;
            }

            var audioRoot = new GameObject("Audio");
            audioRoot.transform.SetParent(parent.transform, false);
            ambience = MakeSource(audioRoot, true);
            music = MakeSource(audioRoot, true);
            for (int i = 0; i < effects.Length; i++) effects[i] = MakeSource(audioRoot, false);
        }

        private static AudioSource MakeSource(GameObject root, bool loop)
        {
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0;
            source.volume = 0;
            return source;
        }

        public void SetLocation(bool isMission)
        {
            if (hasLocation && mission == isMission) return;
            mission = isMission;
            hasLocation = true;
            ambience.Stop();
            music.Stop();
            ambience.clip = mission ? assets.subwayAmbience : assets.homeAmbience;
            music.clip = mission ? assets.missionMusic : assets.homeMusic;
            ambience.volume = music.volume = 0;
            if (ambience.clip) ambience.Play();
            if (music.clip) music.Play();
        }

        public void SetLaptopMode(bool active) { laptopMode = active; }

        public void Tick(float dt, bool focused)
        {
            float master = focused ? Mathf.Clamp01(settings.masterVolume) : 0;
            ambience.volume = Mathf.MoveTowards(ambience.volume, master * settings.sfxVolume * .65f, dt);
            music.volume = Mathf.MoveTowards(music.volume, master * settings.musicVolume * .6f, dt);

            if (bootstrap && bootstrap.Session != null)
            {
                bool currentLampOn = bootstrap.Session.State.lampOn;
                if (!lampStateKnown)
                {
                    lampOn = currentLampOn;
                    lampStateKnown = true;
                }
                else if (currentLampOn != lampOn)
                {
                    lampOn = currentLampOn;
                    Play(lampOn ? assets.lampOn : assets.lampOff);
                }
            }
        }

        public void Play(AudioClip clip)
        {
            if (!clip) return;
            var source = effects[voice++ % effects.Length];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume);
            source.Play();
        }

        public void PlayMessageCue(string cue)
        {
            AudioClip clip = assets.laptopMouseClick ? assets.laptopMouseClick : assets.click;
            float scale = cue == "WorldNotification" ? .34f :
                cue == "LaptopNotification" ? .30f :
                cue == "ActiveReceive" ? .22f :
                cue == "SoftReceive" ? .16f : .26f;
            PlayScaled(clip, scale);
        }

        private void PlayScaled(AudioClip clip, float scale)
        {
            if (!clip) return;
            var source = effects[voice++ % effects.Length];
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(settings.masterVolume * settings.sfxVolume * Mathf.Clamp01(scale));
            source.Play();
        }

        public void Click()
        {
            if (!laptopMode) Play(assets.click);
        }

        public void LaptopMouseClick() { Play(assets.laptopMouseClick); }
    }
}
