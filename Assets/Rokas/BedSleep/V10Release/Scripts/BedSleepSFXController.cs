using UnityEngine;

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
            if (watchCrownClickClip == null)
            {
                const int sampleRate = 44100;
                int count = Mathf.RoundToInt(sampleRate * 0.015f);
                float[] data = new float[count];
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / sampleRate;
                    data[i] = Mathf.Sin(2f * Mathf.PI * 2400f * t) * Mathf.Exp(-t * 420f) * .5f;
                }
                watchCrownClickClip = AudioClip.Create("ROKAS_WatchCrownMechanicalTick", count, 1, sampleRate, false);
                watchCrownClickClip.SetData(data, 0);
            }
        }

        private void PlayIfAssigned(AudioClip clip, float gain)
        {
            InitializeAudio();
            if (clip != null) _source.PlayOneShot(clip, masterVolume * gain);
        }
        public void PlaySleeveRaise() => PlayIfAssigned(sleeveRaiseClip, .5f);
        public void PlayOledPowerOn() => PlayIfAssigned(watchOledPowerOnClip, .4f);
        public void PlayConfirm() => PlayIfAssigned(watchButtonConfirmClip, .65f);
        public void PlaySleeveLower() => PlayIfAssigned(sleeveLowerClip, .5f);

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
            _source.PlayOneShot(watchCrownClickClip, masterVolume * .62f);
        }
    }
}
