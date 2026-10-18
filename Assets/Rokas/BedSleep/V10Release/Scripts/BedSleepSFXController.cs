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
                watchCrownClickClip = ArgonWatchFoley.CreateClip(ArgonFoleyCue.CrownTick);
                ownsCrown = true;
            }
            if (watchButtonConfirmClip == null)
            {
                watchButtonConfirmClip = ArgonWatchFoley.CreateClip(ArgonFoleyCue.Confirmation);
                ownsConfirm = true;
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
            if (watchCrownClickClip != null)
                _source.PlayOneShot(watchCrownClickClip, masterVolume * .90f);
        }
        private void OnDestroy()
        {
            // Only generated game-instance PCM. Never destroy user-assigned clips.
            if (ownsRaise && sleeveRaiseClip != null) Destroy(sleeveRaiseClip);
            if (ownsLower && sleeveLowerClip != null) Destroy(sleeveLowerClip);
            if (ownsCrown && watchCrownClickClip != null) Destroy(watchCrownClickClip);
            if (ownsConfirm && watchButtonConfirmClip != null) Destroy(watchButtonConfirmClip);
            if (ownsPower && watchOledPowerOnClip != null) Destroy(watchOledPowerOnClip);
        }
    }
}
