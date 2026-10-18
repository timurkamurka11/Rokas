using UnityEngine;
using Rokas.BedSleep.Argon.Candidate;

namespace Rokas.BedSleep.V10Release
{
    [DisallowMultipleComponent]
    public sealed class BedSleepSFXController : MonoBehaviour
    {
        public AudioClip sleeveRaiseClip, watchOledPowerOnClip, watchCrownClickClip, watchButtonConfirmClip, sleeveLowerClip;
        [Range(0f, 1f)] public float masterVolume = 0.85f;
        public int TotalCrownTicksPlayed { get; private set; }
        // Optional authored audio slots: do not overwrite Inspector clips.
        public AudioClip watchButtonMechanicalClip, watchUiSelectClip, watchUiToggleClip;
        private AudioClip[] generatedCrownTicks;
        private bool ownsButtonMechanical, ownsUiSelect, ownsUiToggle;
        private int crownVariation;
        private float lastButton=-10f, lastUiSelect=-10f, lastUiToggle=-10f;

        private AudioSource _source;
        private int _pendingClicks;
        private float _nextClickIn;
        private float _lastPlayed = -10f;
        private float _lastRaise = -10f, _lastLower = -10f;
        private float _lastConfirm = -10f, _lastPower = -10f;
        private bool ownsRaise, ownsLower, ownsCrown, ownsConfirm, ownsPower;

        public void InitializeAudio()
        {
            // Unity's destroyed/missing Component wrappers can be C# non-null.
            // Do not use ?? here: it bypasses UnityEngine.Object's overloaded
            // null/boolean checks and can leave a fake-null AudioSource.
            if (!_source)
            {
                _source = GetComponent<AudioSource>();
                if (!_source)
                    _source = gameObject.AddComponent<AudioSource>();
                if (!_source)
                    throw new MissingComponentException(
                        "ROKAS V10 cannot initialize its required AudioSource");
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            // V10's Inspector audio fields were empty in the integrated game.
            // Populate only missing slots from the already-authored Argon Foley.
            // Existing developer-assigned sound assets always take priority.
            if (sleeveRaiseClip == null)
            {
                sleeveRaiseClip = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Sleeve);
                ownsRaise = true;
            }
            if (sleeveLowerClip == null)
            {
                sleeveLowerClip = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Sleeve);
                ownsLower = true;
            }
            if (watchCrownClickClip == null)
            {
                // Three deterministic, layered mechanical detents, not one
                // plastic sine beep looped at identical pitch each time.
                generatedCrownTicks = new AudioClip[3];
                for (int i = 0; i < generatedCrownTicks.Length; i++)
                    generatedCrownTicks[i] = BuildMechanicalCue(i);
                watchCrownClickClip = generatedCrownTicks[0];
                ownsCrown = true;
            }
            if (watchButtonConfirmClip == null)
            {
                watchButtonConfirmClip = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Confirmation);
                ownsConfirm = true;
            }
            if (watchButtonMechanicalClip == null)
            {
                watchButtonMechanicalClip = BuildMechanicalCue(3);
                ownsButtonMechanical = true;
            }
            if (watchUiSelectClip == null)
            {
                watchUiSelectClip = BuildMechanicalCue(4);
                ownsUiSelect = true;
            }
            if (watchUiToggleClip == null)
            {
                watchUiToggleClip = BuildMechanicalCue(5);
                ownsUiToggle = true;
            }
            if (watchOledPowerOnClip == null)
            {
                // Brief soft watch wake-up, distinct from confirm and crown.
                const int hz = ArgonWatchFoley.SampleRate;
                int n = Mathf.RoundToInt(hz * .12f);
                float[] data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / hz;
                    float smooth = Mathf.Sin(Mathf.PI * i / (n - 1));
                    float tone = Mathf.Sin(2f * Mathf.PI * (720f * t + 450f * t * t));
                    data[i] = tone * smooth * .085f;
                }
                watchOledPowerOnClip = AudioClip.Create(
                    "ROKAS_Argon_Watch_Wake", n, 1, hz, false);
                watchOledPowerOnClip.SetData(data, 0);
                ownsPower = true;
            }
        }

        // Compact original PCM (no third-party licensing, no disk loads
        // and no allocations while the user is turning the watch crown).
        private static AudioClip BuildMechanicalCue(int variant)
        {
            const int rate=44100;
            float seconds=variant==3 ? .105f :
                (variant>=4 ? .085f : .065f);
            int n=Mathf.CeilToInt(rate*seconds);
            float[] samples=new float[n];
            uint seed=(uint)(98173 + variant*17591);
            float bodyHz=variant==3 ? 760f :
                (variant==4 ? 1120f : (variant==5 ? 960f :
                1290f + variant*95f));
            for(int i=0;i<n;i++)
            {
                float t=(float)i/rate;
                seed=1664525u*seed+1013904223u;
                float noise=(((seed>>8)&0xFFFFFF)/8388608f)-1f;
                float snap=Mathf.Exp(-t*(variant==3?75f:135f));
                float reboundT=t-(variant==3?.033f:.019f);
                float rebound=reboundT>0f ?
                    Mathf.Exp(-reboundT*145f):0f;
                float tone=Mathf.Sin(6.2831853f*bodyHz*t);
                float sub=Mathf.Sin(6.2831853f*bodyHz*.48f*t);
                float gain=variant==3 ? .44f :
                    (variant>=4 ? .25f : .46f);
                samples[i]=Mathf.Clamp(
                    gain*(snap*(.62f*tone+.25f*noise+.13f*sub)+
                    rebound*(.20f*sub+.11f*noise)), -.82f,.82f);
            }
            var clip=AudioClip.Create("ROKAS_MechanicalWatch_"+variant,
                n,1,rate,false);
            clip.SetData(samples,0);
            return clip;
        }

        public void PlayButtonMechanical()
        {
            float now=Time.unscaledTime;
            if(now-lastButton<.06f)return;
            lastButton=now;
            PlayIfAssigned(watchButtonMechanicalClip,.83f);
        }
        public void PlayUiSelect()
        {
            float now=Time.unscaledTime;
            if(now-lastUiSelect<.04f)return;
            lastUiSelect=now;
            PlayIfAssigned(watchUiSelectClip,.48f);
        }
        public void PlayUiToggle()
        {
            float now=Time.unscaledTime;
            if(now-lastUiToggle<.07f)return;
            lastUiToggle=now;
            PlayIfAssigned(watchUiToggleClip,.65f);
        }

        private void PlayIfAssigned(AudioClip clip, float gain)
        {
            InitializeAudio();
            if (clip != null && _source != null)
                _source.PlayOneShot(clip, masterVolume * gain);
        }
        private void PlayDebounced(AudioClip clip, float gain, ref float lastTime)
        {
            if (Time.unscaledTime - lastTime < .18f) return;
            lastTime = Time.unscaledTime;
            PlayIfAssigned(clip, gain);
        }
        public void PlaySleeveRaise() =>
            PlayDebounced(sleeveRaiseClip, .85f, ref _lastRaise);
        public void PlayOledPowerOn() =>
            PlayDebounced(watchOledPowerOnClip, .6f, ref _lastPower);
        public void PlayConfirm() =>
            PlayDebounced(watchButtonConfirmClip, .9f, ref _lastConfirm);
        public void PlaySleeveLower() =>
            PlayDebounced(sleeveLowerClip, .8f, ref _lastLower);

        public void QueueCrownClicks(int deltaHalfHours, int selectedHalfHours)
        {
            if (deltaHalfHours == 0) return;
            bool wasEmpty = _pendingClicks == 0;
            _pendingClicks = Mathf.Min(24, _pendingClicks + Mathf.Abs(deltaHalfHours));
            if (wasEmpty) _nextClickIn = 0f;
        }
        public void TickQueuedCrownClicks(float dt)
        {
            if (_pendingClicks <= 0) return;
            _nextClickIn -= Mathf.Max(0f, dt);
            if (_nextClickIn > 0f) return;
            PlayCrownClick();
            --_pendingClicks;
            _nextClickIn += .055f;
        }
        public void PlayCrownClick(int selectedHalfHours = -1)
        {
            InitializeAudio();
            float now = Time.unscaledTime;
            if (now - _lastPlayed < .018f) return;
            _lastPlayed = now;
            ++TotalCrownTicksPlayed;
            _source.pitch = selectedHalfHours >= 2 ? Mathf.Lerp(.94f, 1.12f, (selectedHalfHours - 2f) / 22f) : 1f;
            AudioClip detent = generatedCrownTicks != null
                ? generatedCrownTicks[(crownVariation++ % generatedCrownTicks.Length)]
                : watchCrownClickClip;
            if (detent != null)
                _source.PlayOneShot(detent, masterVolume * 1.02f);
        }
        private void OnDestroy()
        {
            // Only generated game-instance PCM. Never destroy user-assigned clips.
            if (ownsRaise && sleeveRaiseClip != null) Destroy(sleeveRaiseClip);
            if (ownsLower && sleeveLowerClip != null) Destroy(sleeveLowerClip);
            if (generatedCrownTicks != null)
            {
                foreach (var variant in generatedCrownTicks)
                    if (variant != null) Destroy(variant);
            }
            else if (ownsCrown && watchCrownClickClip != null)
                Destroy(watchCrownClickClip);
            if (ownsButtonMechanical && watchButtonMechanicalClip != null)
                Destroy(watchButtonMechanicalClip);
            if (ownsUiSelect && watchUiSelectClip != null)
                Destroy(watchUiSelectClip);
            if (ownsUiToggle && watchUiToggleClip != null)
                Destroy(watchUiToggleClip);
            if (ownsConfirm && watchButtonConfirmClip != null) Destroy(watchButtonConfirmClip);
            if (ownsPower && watchOledPowerOnClip != null) Destroy(watchOledPowerOnClip);
        }
    }
}
