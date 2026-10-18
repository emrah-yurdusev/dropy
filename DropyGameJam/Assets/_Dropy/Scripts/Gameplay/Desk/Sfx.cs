using System.Collections;
using System.Collections.Generic;
using Dropy.Core;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oyunun ses efektleri. Hepsi koddan üretilir (<see cref="SfxSynth"/>), ama Resources/Sfx altına sesin
    /// adıyla bir dosya konursa (ör. Resources/Sfx/collapse.wav) o çalar; böylece gerçek kayıt kod değişmeden takılır.
    /// Seviyeler ayarlardaki efekt sesine (<see cref="GameAudio.SfxVolume"/>) uyar.
    /// </summary>
    public static class Sfx
    {
        const int Rate = SfxSynth.Rate;

        static AudioSource oneShot;
        static AudioSource ui;
        static bool listening;
        static float lastCollapse = -10f;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioClip> Overrides = new Dictionary<string, AudioClip>();
        static readonly Dictionary<string, AudioSource> Loops = new Dictionary<string, AudioSource>();
        static readonly Dictionary<string, float> LoopLevels = new Dictionary<string, float>();

        // ---------------------------------------------------------------- masa

        public static void Stamp() => Play("stamp", () => SfxSynth.Render(0.22f, StampSample), 0.9f);

        public static void Paper() => Play("paper", () => SfxSynth.Render(0.14f, PaperSample), 0.35f);

        public static void Buzz() => Play("buzz", () => SfxSynth.Render(0.7f, BuzzSample), 0.5f);

        public static void StampPick() => Play("stamp_pick", () => SfxSynth.Thock(520f), 0.45f);

        public static void StampDrop() => Play("stamp_drop", () => SfxSynth.Thock(380f), 0.35f);

        public static void Hammer() => Play("hammer", SfxSynth.Hammer, 0.8f);

        public static void PageTurn() => Play("page", SfxSynth.PageTurn, 0.5f);

        public static void PaperSlide() => Play("paper_slide", SfxSynth.PaperSlide, 0.35f);

        public static void Pen() => Play("pen", SfxSynth.Pen, 0.3f);

        public static void Alarm() => Play("alarm", SfxSynth.Alarm, 0.7f);

        public static void Coin() => Play("coin", SfxSynth.Coin, 0.55f);

        public static void Pay() => Play("pay", SfxSynth.Pay, 0.55f);

        public static void Debt() => Play("debt", SfxSynth.Debt, 0.5f);

        public static void Notify() => Play("notify", SfxSynth.Notify, 0.5f);

        public static void TvOn() => Play("tv_on", SfxSynth.TvOn, 0.5f);

        public static void Breaking() => Play("breaking", SfxSynth.Breaking, 0.6f);

        public static void Newspaper() => Play("press", SfxSynth.Press, 0.6f);

        /// <summary>
        /// Konuşma mırıltısı. Her konuşmacının adından sabit bir ses perdesi çıkar; heceler pentatonik
        /// aralıktan rastgele seçilir ki gevezelik müzikal dursun.
        /// </summary>
        public static void Talk(string speaker)
        {
            int voice = Mathf.Abs((speaker ?? "").GetHashCode()) % 6;
            int[] steps = { 0, 2, 4, 7, 9 };
            int step = steps[Random.Range(0, steps.Length)];
            float freq = 150f * Mathf.Pow(2f, (voice * 1.5f + step) / 12f);
            Play("talk", $"talk{voice}_{step}", () => SfxSynth.Blip(freq), 0.22f);
        }

        // ---------------------------------------------------------------- arayüz (duraklatmada da çalar)

        /// <summary>Buton "blup"u; üç perdeden biri rastgele seçilir ki art arda tıklar tekdüze olmasın.</summary>
        public static void Click()
        {
            int variant = Random.Range(0, 3);
            float low = 330f + 55f * variant;
            Play("click", "click" + variant, () => SfxSynth.Render(0.13f, t => BloopSample(t, low)), 0.5f, true);
        }

        public static void Hover() => Play("hover", SfxSynth.Hover, 0.25f, true);

        public static void PauseOpen() => Play("pause_open", () => SfxSynth.Pause(true), 0.4f, true);

        public static void PauseClose() => Play("pause_close", () => SfxSynth.Pause(false), 0.4f, true);

        public static void Whoosh() => Play("whoosh", SfxSynth.Whoosh, 0.4f);

        // ---------------------------------------------------------------- dünyadaki sesler (kameraya uzaklıkla kısılır)

        public static void Step(Vector3 position)
        {
            int variant = Random.Range(0, 4);
            Play("step", "step" + variant, () => SfxSynth.Step(variant), 0.35f * Nearness(position));
        }

        public static void DoorOpen(Vector3 position) => Play("door_open", SfxSynth.DoorOpen, 0.5f * Nearness(position));

        public static void DoorClose(Vector3 position) => Play("door_close", SfxSynth.DoorClose, 0.7f * Nearness(position));

        public static void ChairSit(Vector3 position) => Play("chair_sit", () => SfxSynth.Chair(true), 0.5f * Nearness(position));

        public static void ChairStand(Vector3 position) => Play("chair_stand", () => SfxSynth.Chair(false), 0.4f * Nearness(position));

        // ---------------------------------------------------------------- deprem ve ara sahneler

        /// <summary>Alçak uğultu; sarsıntıda kısa ve kısık, depremde uzun ve yüksek.</summary>
        public static void Rumble(float seconds, float volume)
        {
            seconds = Mathf.Round(seconds * 2f) / 2f;
            Play("rumble", "rumble" + seconds, () => SfxSynth.Render(seconds, RumbleSample(seconds)), volume);
        }

        /// <summary>Bina çökmesi. Aynı anda birkaç bina yıkılınca üst üste binip patlamasın diye saniyede bir çalar.</summary>
        public static void Collapse()
        {
            if (Time.time - lastCollapse < 1f)
                return;
            lastCollapse = Time.time;
            Play("collapse", SfxSynth.Collapse, 1f);
        }

        public static void Glass() => Play("glass", SfxSynth.Glass, 0.7f);

        public static void Siren() => Play("siren", SfxSynth.Siren, 0.35f);

        // ---------------------------------------------------------------- döngüler

        /// <summary>Sürekli çalan gerilim tonu. 0 susturur.</summary>
        public static void Ambience(float volume) => Loop("ambience", () => SfxSynth.Render(4f, AmbienceSample), volume);

        /// <summary>Ofisin oda sesi (havalandırma, uzak trafik). 0 susturur.</summary>
        public static void Room(float volume) => Loop("room", SfxSynth.Room, volume);

        /// <summary>Hapishane koridorunun yankılı ortamı.</summary>
        public static void Prison(bool on) => Loop("prison", SfxSynth.Prison, on ? 0.5f : 0f);

        /// <summary>
        /// Üretimi uzun süren sesleri (çöküş, cam ~0.3 sn) önceden hazırlar ki deprem anında oyun takılmasın.
        /// Her kare bir ses; masa sahnesi açılırken başlatılır.
        /// </summary>
        public static IEnumerator Prewarm()
        {
            var heavy = new (string name, System.Func<float[]> render)[]
            {
                ("collapse", SfxSynth.Collapse), ("glass", SfxSynth.Glass), ("prison", SfxSynth.Prison),
                ("room", SfxSynth.Room), ("siren", SfxSynth.Siren), ("breaking", SfxSynth.Breaking),
            };
            foreach (var (name, render) in heavy)
            {
                yield return null;
                Clip(name, name, render);
            }
        }

        // ---------------------------------------------------------------- çalma

        static void Play(string name, System.Func<float[]> render, float volume, bool uiSound = false) =>
            Play(name, name, render, volume, uiSound);

        /// <param name="name">Dosya adı (Resources/Sfx/name); aynı sesin perde çeşitleri bu adı paylaşır.</param>
        /// <param name="key">Üretilen çeşidin önbellek anahtarı.</param>
        /// <param name="uiSound">Arayüz sesi: oyun duraklatılınca (AudioListener.pause) da çalar.</param>
        static void Play(string name, string key, System.Func<float[]> render, float volume, bool uiSound = false)
        {
            float level = volume * GameAudio.SfxVolume;
            if (level <= 0f)
                return;
            var source = uiSound ? Source(ref ui, "Ui", ignorePause: true) : Source(ref oneShot, "OneShot", ignorePause: false);
            source.PlayOneShot(Clip(name, key, render), level);
        }

        static void Loop(string name, System.Func<float[]> render, float volume)
        {
            LoopLevels[name] = volume;
            Loops.TryGetValue(name, out var source);
            if (volume <= 0f)
            {
                if (source != null)
                    source.Stop();
                return;
            }
            if (source == null)
            {
                Source(ref source, "Loop." + name, ignorePause: false);
                source.loop = true;
                Loops[name] = source;
            }
            if (source.clip == null)
                source.clip = Clip(name, name, render);
            source.volume = volume * GameAudio.SfxVolume;
            if (!source.isPlaying)
                source.Play();
        }

        /// <summary>Ayarlardan efekt sesi değişince çalan döngüler de hemen uyar.</summary>
        static void ApplyLevels()
        {
            foreach (var pair in Loops)
                if (pair.Value != null && LoopLevels.TryGetValue(pair.Key, out float level))
                    pair.Value.volume = level * GameAudio.SfxVolume;
        }

        static AudioSource Source(ref AudioSource source, string name, bool ignorePause)
        {
            if (source != null)
                return source;
            if (!listening)
            {
                listening = true;
                GameAudio.Changed += ApplyLevels;
            }
            var go = new GameObject("Sfx." + name);
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignorePause;
            return source;
        }

        static AudioClip Clip(string name, string key, System.Func<float[]> render)
        {
            if (!Overrides.TryGetValue(name, out var file))
            {
                file = Resources.Load<AudioClip>("Sfx/" + name);
                Overrides[name] = file;
            }
            if (file != null)
                return file;

            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var data = render();
            for (int i = 0; i < data.Length; i++)
                data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(key, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        /// <summary>Kameraya yakın ses tam, uzaklaştıkça kısık (2 m'ye kadar tam, 6 m'de yarı).</summary>
        static float Nearness(Vector3 position)
        {
            var cam = Camera.main;
            if (cam == null)
                return 1f;
            float distance = Vector3.Distance(cam.transform.position, position);
            return 1f / (1f + Mathf.Max(0f, distance - 2f) * 0.25f);
        }

        // ---------------------------------------------------------------- tek satırlık eski üreticiler

        static float Noise() => Random.value * 2f - 1f;

        static float StampSample(float t) =>
            Noise() * Mathf.Exp(-t * 40f) * 0.6f + Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t * 22f);

        // Baloncuk sesi: perde hızla yükselir (low -> 2.4x), yumuşak giriş ve kısa sönüm.
        // Faz, frekansın integrali; böylece perde kayarken tıkırtı olmaz.
        static float BloopSample(float t, float low)
        {
            const float rise = 45f;
            float high = low * 2.4f;
            float phase = 2f * Mathf.PI * (high * t - (high - low) * (1f - Mathf.Exp(-rise * t)) / rise);
            float envelope = Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t * 38f);
            return (Mathf.Sin(phase) + 0.18f * Mathf.Sin(2f * phase)) * envelope * 0.8f;
        }

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
