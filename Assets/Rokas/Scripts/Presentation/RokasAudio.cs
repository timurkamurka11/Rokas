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
        private readonly AudioClip messageArrive;
        private readonly AudioClip reactionCue;
        private AudioClip[] thunderBank;
        private float homeWeatherMix = 1f;
        private int voice;
        private bool mission;
        private bool hasLocation;
        private bool laptopMode;
        private bool lampStateKnown;
        private bool lampOn;

        public float VideoVolume { get { return Mathf.Clamp01(settings.masterVolume * settings.sfxVolume); } }

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
            messageArrive = Resources.Load<AudioClip>("Messages/Audio/MessageArrive");
            reactionCue = Resources.Load<AudioClip>("Messages/Audio/Reaction");
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

        public void SetHomeWeatherMix(float value)
        {
            homeWeatherMix = Mathf.Clamp(value, .15f, 1f);
        }

        public void SetLaptopMode(bool active) { laptopMode = active; }

        public void Tick(float dt, bool focused)
        {
            float master = focused ? Mathf.Clamp01(settings.masterVolume) : 0;
            float weather = mission ? 1f : homeWeatherMix;
            ambience.volume = Mathf.MoveTowards(ambience.volume, master * settings.sfxVolume * .65f * weather, dt);
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
            if (string.Equals(cue, "Reaction", System.StringComparison.Ordinal))
            {
                PlayScaled(reactionCue ? reactionCue : assets.click, .72f);
                return;
            }
            if (string.Equals(cue, "PlayerSend", System.StringComparison.Ordinal))
            {
                PlayScaled(assets.laptopMouseClick ? assets.laptopMouseClick : assets.click, .30f);
                return;
            }

            AudioClip clip = messageArrive ? messageArrive : (assets.laptopMouseClick ? assets.laptopMouseClick : assets.click);
            float scale = cue == "WorldNotification" ? .92f :
                cue == "LaptopNotification" ? .84f :
                cue == "ActiveReceive" ? .76f :
                cue == "SoftReceive" ? .40f : .70f;
            PlayScaled(clip, scale);
        }

        public void PlayThunder(int variant, float volume)
        {
            EnsureThunderBank();
            int index = Mathf.Abs(variant) % thunderBank.Length;
            PlayScaled(thunderBank[index], Mathf.Clamp01(volume) * .72f);
        }

        private void EnsureThunderBank()
        {
            if (thunderBank != null) return;
            thunderBank = new AudioClip[3];
            for (int i = 0; i < thunderBank.Length; i++) thunderBank[i] = BuildThunder(i);
        }

        private static AudioClip BuildThunder(int variant)
        {
            const int rate = 22050;
            float duration = 2.7f + variant * .42f;
            int count = Mathf.CeilToInt(rate * duration);
            var samples = new float[count];
            uint state = (uint)(0xA341316Cu + variant * 0x9E3779B9u);
            float low = 0f;
            float low2 = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                float noise = ((state & 0xFFFFu) / 32767.5f) - 1f;
                low += (noise - low) * (.018f + variant * .002f);
                low2 += (low - low2) * .055f;
                float attack = Mathf.Clamp01(t / (.055f + variant * .018f));
                float decay = Mathf.Exp(-t / (1.15f + variant * .30f));
                float body = Mathf.Sin(2f * Mathf.PI * (31f + variant * 3f) * t) * .17f +
                             Mathf.Sin(2f * Mathf.PI * (47f + variant * 2f) * t) * .08f;
                float distantCrack = variant == 1 ? Mathf.Exp(-Mathf.Pow((t - .17f) / .10f, 2f)) * .08f : 0f;
                samples[i] = Mathf.Clamp((low2 * 1.85f + body + distantCrack) * attack * decay, -.72f, .72f);
            }
            AudioClip clip = AudioClip.Create("HomeThunderDistant0" + (variant + 1), count, 1, rate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            return clip;
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

        public void Dispose()
        {
            if (thunderBank == null) return;
            for (int i = 0; i < thunderBank.Length; i++)
                if (thunderBank[i]) Object.Destroy(thunderBank[i]);
            thunderBank = null;
        }
    }
}
