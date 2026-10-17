using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Masa sesleri koddan üretilir (ses dosyası yok): mühür, kâğıt, telefon titreşimi,
    /// sarsıntı uğultusu ve günler ilerledikçe gerilen arka plan.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;

        static AudioSource oneShot;
        static AudioSource ambience;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static void Stamp() => Play("stamp", 0.22f, StampSample, 0.9f);

        public static void Paper() => Play("paper", 0.14f, PaperSample, 0.35f);

        public static void Buzz() => Play("buzz", 0.7f, BuzzSample, 0.5f);

        /// <summary>Alçak uğultu; sarsıntıda kısa ve kısık, depremde uzun ve yüksek.</summary>
        public static void Rumble(float seconds, float volume)
        {
            seconds = Mathf.Round(seconds * 2f) / 2f;
            Play("rumble" + seconds, seconds, RumbleSample(seconds), volume);
        }

        /// <summary>Sürekli çalan gerilim tonu. 0 susturur.</summary>
        public static void Ambience(float volume)
        {
            Source(ref ambience, "Ambience");
            if (ambience.clip == null)
            {
                ambience.clip = Clip("ambience", 4f, AmbienceSample);
                ambience.loop = true;
            }
            ambience.volume = volume;
            if (volume > 0f && !ambience.isPlaying)
                ambience.Play();
            else if (volume <= 0f)
                ambience.Stop();
        }

        static void Play(string key, float seconds, System.Func<float, float> sample, float volume)
        {
            Source(ref oneShot, "OneShot");
            oneShot.PlayOneShot(Clip(key, seconds, sample), volume);
        }

        static void Source(ref AudioSource source, string name)
        {
            if (source != null)
                return;
            var go = new GameObject("Sfx." + name);
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        static AudioClip Clip(string key, float seconds, System.Func<float, float> sample)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
                data[i] = Mathf.Clamp(sample(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(key, count, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        static float Noise() => Random.value * 2f - 1f;

        static float StampSample(float t) =>
            Noise() * Mathf.Exp(-t * 40f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 22f);

        static float lastPaper;

        static float PaperSample(float t)
        {
            float n = Noise();
            float high = n - lastPaper;
            lastPaper = n;
            return high * 0.5f * Mathf.Sin(Mathf.PI * t / 0.14f);
        }

        static float BuzzSample(float t)
        {
            bool on = t < 0.25f || (t > 0.4f && t < 0.65f);
            return on ? Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 150f * t)) * 0.25f : 0f;
        }

        static System.Func<float, float> RumbleSample(float seconds)
        {
            float low = 0f;
            return t =>
            {
                low += (Noise() - low) * 0.015f;
                return low * 6f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds));
            };
        }

        static float AmbienceSample(float t) =>
            (Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 82.5f * t))
            * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.25f * t)) * 0.35f;
    }
}
