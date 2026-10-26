using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Rokas.BedSleep.Argon.Candidate
{
    public enum ArgonFoleyCue { Sleeve, CrownTick, Confirmation }

    public struct ArgonFoleyEvent
    {
        public float Seconds;
        public ArgonFoleyCue Cue;
        public float Gain;
        public ArgonFoleyEvent(float seconds, ArgonFoleyCue cue, float gain = 1f)
        { Seconds = seconds; Cue = cue; Gain = gain; }
    }

    /// <summary>
    /// Original, deterministic layered tactile Foley; no copyrighted external
    /// recordings. Same PCM powers PlayOneShot in runtime and QA WAV soundtrack.
    /// Quiet cloth friction / dual-contact crown tick / soft confirmation chime.
    /// </summary>
    public static class ArgonWatchFoley
    {
        public const int SampleRate = 44100;
        private static readonly Dictionary<ArgonFoleyCue, float[]> Cache =
            new Dictionary<ArgonFoleyCue, float[]>();

        public static float[] Samples(ArgonFoleyCue cue)
        {
            if (Cache.TryGetValue(cue, out var existing)) return existing;
            float length = cue == ArgonFoleyCue.Sleeve ? .43f :
                cue == ArgonFoleyCue.CrownTick ? .085f : .34f;
            int count = Mathf.CeilToInt(SampleRate * length);
            var samples = new float[count];
            uint seed = (uint)(170917 + (int)cue * 9931);
            float prev = 0f;
            for (int i = 0; i < count; ++i)
            {
                float t = (float)i / SampleRate;
                seed = 1664525u * seed + 1013904223u;
                float white = ((seed >> 8) / 16777216f) * 2f - 1f;
                prev += (white - prev) * .16f;
                float value;
                if (cue == ArgonFoleyCue.Sleeve)
                {
                    float flutter = .62f + .38f * Mathf.Sin(t * 63f + .7f)
                        * Mathf.Sin(t * 21f);
                    float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * t / length), 1.8f);
                    value = (white * .24f + prev * .76f) * envelope *
                        flutter * .17f;
                }
                else if (cue == ArgonFoleyCue.CrownTick)
                {
                    float shortTap = Mathf.Exp(-t * 115f);
                    float rebound = Mathf.Exp(-Mathf.Max(0f, t - .022f) * 145f)
                        * (t >= .022f ? 1f : 0f);
                    value = (.15f * white + .28f * Mathf.Sin(t * 2f * Mathf.PI * 1720f))
                        * shortTap + (.13f * Mathf.Sin(
                            (t - .022f) * 2f * Mathf.PI * 980f)) * rebound;
                }
                else
                {
                    float first = Mathf.Sin(t * Mathf.PI * 2f * 850f) * Mathf.Exp(-t * 27f);
                    float secondT = t - .095f;
                    float second = secondT >= 0f ?
                        Mathf.Sin(secondT * Mathf.PI * 2f * 1160f) *
                        Mathf.Exp(-secondT * 22f) : 0f;
                    value = (first * .115f + second * .11f) +
                        white * .025f * Mathf.Exp(-t * 56f);
                }
                samples[i] = Mathf.Clamp(value, -.75f, .75f);
            }
            Cache[cue] = samples;
            return samples;
        }

        public static AudioClip CreateClip(ArgonFoleyCue cue)
        {
            var values = Samples(cue);
            var clip = AudioClip.Create("Argon_Original_" + cue, values.Length,
                1, SampleRate, false);
            clip.SetData(values, 0);
            return clip;
        }

        public static void WriteSoundtrack(string path, int frameCount, int fps,
            IEnumerable<ArgonFoleyEvent> events)
        {
            int n = Mathf.CeilToInt((float)frameCount / fps * SampleRate);
            var output = new float[n];
            foreach (var e in events)
            {
                float[] sample = Samples(e.Cue);
                int offset = Mathf.RoundToInt(e.Seconds * SampleRate);
                for (int i = 0; i < sample.Length && i + offset < n; ++i)
                    if (i + offset >= 0)
                        output[i + offset] += sample[i] * Mathf.Clamp01(e.Gain);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                int bytes = n * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(bytes + 36);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(SampleRate); writer.Write(SampleRate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                foreach (float x in output)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(x, -.95f, .95f) * 32767f));
            }
        }
    }
}
