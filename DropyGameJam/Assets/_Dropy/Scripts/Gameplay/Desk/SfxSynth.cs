using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// <see cref="Sfx"/>'in ses üreticileri. Her biri 44.1 kHz mono, -1..1 aralığında bir örnek dizisi döndürür.
    /// Gerçek kayıt gibi duyulması zor olanlar (ayak, sandalye, çöküş, cam, ortam) yaklaşıktır; Resources/Sfx
    /// altına aynı adla bir ses dosyası konunca onun yerine o çalar.
    /// </summary>
    static class SfxSynth
    {
        public const int Rate = 44100;
        const float Tau = Mathf.PI * 2f;
        static readonly System.Random Rng = new System.Random(7);

        // ---------------------------------------------------------------- yardımcılar

        static float Rand() => (float)Rng.NextDouble();
        static float Noise() => Rand() * 2f - 1f;
        static float Range(float a, float b) => a + (b - a) * Rand();
        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        static int Index(float seconds) => Mathf.RoundToInt(seconds * Rate);

        public static float[] Render(float seconds, System.Func<float, float> sample)
        {
            var data = Buffer(seconds);
            for (int i = 0; i < data.Length; i++)
                data[i] = sample(i / (float)Rate);
            return data;
        }

        /// <summary>RBJ ikinci derece filtre (alçak, yüksek, bant geçiren). Frekansı örnek örnek değiştirilebilir.</summary>
        sealed class Filter
        {
            enum Kind { Low, High, Band }

            readonly Kind kind;
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            Filter(Kind kind, float freq, float q)
            {
                this.kind = kind;
                Tune(freq, q);
            }

            public static Filter Low(float freq, float q = 0.707f) => new Filter(Kind.Low, freq, q);
            public static Filter High(float freq, float q = 0.707f) => new Filter(Kind.High, freq, q);
            public static Filter Band(float freq, float q) => new Filter(Kind.Band, freq, q);

            public void Tune(float freq, float q)
            {
                float w = Tau * Mathf.Clamp(freq, 10f, Rate * 0.45f) / Rate;
                float cos = Mathf.Cos(w), alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                switch (kind)
                {
                    case Kind.Low:
                        b0 = b2 = (1f - cos) / 2f / a0;
                        b1 = (1f - cos) / a0;
                        break;
                    case Kind.High:
                        b0 = b2 = (1f + cos) / 2f / a0;
                        b1 = -(1f + cos) / a0;
                        break;
                    default:
                        b0 = alpha / a0;
                        b1 = 0f;
                        b2 = -alpha / a0;
                        break;
                }
                a1 = -2f * cos / a0;
                a2 = (1f - alpha) / a0;
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
        }

        /// <summary>Üstel sönümlü sinüs ekler.</summary>
        static void Tone(float[] buf, float start, float freq, float amp, float decay, float attack = 0.002f)
        {
            int from = Index(start);
            for (int i = from; i < buf.Length; i++)
            {
                float t = (i - from) / (float)Rate;
                float env = Mathf.Exp(-t * decay);
                if (env < 0.0005f)
                    break;
                buf[i] += Mathf.Sin(Tau * freq * t) * amp * env * Mathf.Clamp01(t / attack);
            }
        }

        /// <summary>Filtreden geçmiş, üstel sönümlü gürültü patlaması ekler.</summary>
        static void Burst(float[] buf, float start, Filter filter, float amp, float decay, float attack = 0.001f)
        {
            int from = Index(start);
            for (int i = from; i < buf.Length; i++)
            {
                float t = (i - from) / (float)Rate;
                float env = Mathf.Exp(-t * decay);
                if (env < 0.0005f)
                    break;
                buf[i] += filter.Process(Noise()) * amp * env * Mathf.Clamp01(t / attack);
            }
        }

        /// <summary>Çan: uyumsuz üst seslerle metalik, uzun sönümlü ton.</summary>
        static void Bell(float[] buf, float start, float freq, float amp, float decay)
        {
            Tone(buf, start, freq, amp, decay);
            Tone(buf, start, freq * 2f, amp * 0.45f, decay * 1.6f);
            Tone(buf, start, freq * 2.76f, amp * 0.3f, decay * 2.2f);
            Tone(buf, start, freq * 5.4f, amp * 0.12f, decay * 3.5f);
        }

        /// <summary>Gıcırtı: hızı değişen küçük sürtünme tıkları, tahta gibi rezonans yapan iki banttan geçer.</summary>
        static void Creak(float[] buf, float start, float length, float rateFrom, float rateTo, float body, float amp)
        {
            var low = Filter.Band(body, 7f);
            var high = Filter.Band(body * 2.1f, 9f);
            int from = Index(start), count = Index(length);
            float phase = 0f;
            for (int i = 0; i < count && from + i < buf.Length; i++)
            {
                float k = i / (float)count;
                float rate = Mathf.Lerp(rateFrom, rateTo, k) * (1f + 0.25f * Mathf.Sin(Tau * 3f * k));
                phase += rate / Rate;
                float x = 0f;
                if (phase >= 1f)
                {
                    phase -= 1f;
                    x = Range(0.5f, 1f);
                }
                float env = Mathf.Clamp01(k / 0.08f) * Mathf.Clamp01((1f - k) / 0.2f);
                buf[from + i] += (low.Process(x) + 0.5f * high.Process(x)) * amp * env;
            }
        }

        static float[] Normalize(float[] buf, float peak)
        {
            float max = 0f;
            foreach (float s in buf)
                max = Mathf.Max(max, Mathf.Abs(s));
            if (max > 0f)
                for (int i = 0; i < buf.Length; i++)
                    buf[i] *= peak / max;
            return buf;
        }

        /// <summary>Sonu başına harmanlar ki döngü dikişsiz dönsün. Uzunluk fade kadar kısalır.</summary>
        static float[] Loop(float[] buf, float fade)
        {
            int f = Index(fade), n = buf.Length - f;
            var output = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < f)
                {
                    float k = i / (float)f;
                    output[i] = buf[i] * k + buf[n + i] * (1f - k);
                }
                else
                    output[i] = buf[i];
            }
            return output;
        }

        /// <summary>Kahverengi gürültü: alçak, rüzgâr/uğultu gibi.</summary>
        static float[] Brown(float seconds)
        {
            var buf = Buffer(seconds);
            float b = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                b = (b + 0.02f * Noise()) / 1.02f;
                buf[i] = b;
            }
            return buf;
        }

        // ---------------------------------------------------------------- arayüz

        /// <summary>Konuşma mırıltısı: tek hecelik kısa, vızıltılı ses (Animal Crossing usulü).</summary>
        public static float[] Blip(float freq)
        {
            const float length = 0.055f;
            var buf = Buffer(length);
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate, k = t / length;
                phase += freq * (1.06f - 0.12f * k) / Rate;
                float s = Mathf.Sin(Tau * phase) * 0.6f + Mathf.Sin(2f * Tau * phase) * 0.3f + Mathf.Sin(3f * Tau * phase) * 0.15f;
                buf[i] = s * Mathf.Clamp01(t / 0.004f) * Mathf.Pow(1f - k, 1.5f);
            }
            return Normalize(buf, 0.8f);
        }

        public static float[] Hover() => Normalize(Render(0.035f, t =>
            Mathf.Sin(Tau * 1600f * t) * Mathf.Clamp01(t / 0.002f) * Mathf.Pow(1f - t / 0.035f, 2f)), 0.5f);

        /// <summary>Duraklatma menüsü: açılırken parlaklığı düşen, kapanırken yükselen yumuşak "vuup".</summary>
        public static float[] Pause(bool opening)
        {
            const float length = 0.26f;
            var buf = Buffer(length);
            var filter = Filter.Low(1000f);
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float k = i / (float)buf.Length;
                float sweep = opening ? 1f - k : k;
                filter.Tune(Mathf.Lerp(300f, 3000f, sweep), 0.9f);
                phase += Mathf.Lerp(110f, 220f, sweep) / Rate;
                buf[i] = (filter.Process(Noise()) * 0.6f + Mathf.Sin(Tau * phase) * 0.4f) * Mathf.Sin(Mathf.PI * k);
            }
            return Normalize(buf, 0.6f);
        }

        /// <summary>Haber şeridi açılırken: bandı yukarı-aşağı kayan gürültü "vuuş".</summary>
        public static float[] Whoosh()
        {
            const float length = 0.45f;
            var buf = Buffer(length);
            var filter = Filter.Band(500f, 1.2f);
            for (int i = 0; i < buf.Length; i++)
            {
                float k = i / (float)buf.Length;
                filter.Tune(400f + 2100f * Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f)), 1.2f);
                buf[i] = filter.Process(Noise()) * Mathf.Pow(Mathf.Sin(Mathf.PI * k), 1.5f);
            }
            return Normalize(buf, 0.6f);
        }

        // ---------------------------------------------------------------- masa

        /// <summary>Mühürü kaldırma (yüksek) ya da bırakma (alçak): kısa, tahtamsı "tok".</summary>
        public static float[] Thock(float freq)
        {
            var buf = Buffer(0.12f);
            Tone(buf, 0f, freq, 1f, 60f);
            Tone(buf, 0f, freq * 2.3f, 0.3f, 90f);
            Burst(buf, 0f, Filter.High(2000f), 0.3f, 300f);
            return Normalize(buf, 0.7f);
        }

        /// <summary>Çekiçle betona iki vuruş: tiz tık, metalik çınlama, gövde.</summary>
        public static float[] Hammer()
        {
            var buf = Buffer(0.7f);
            foreach (var (at, amp) in new[] { (0f, 1f), (0.26f, 0.75f) })
            {
                Burst(buf, at, Filter.High(5000f), 0.5f * amp, 600f);
                Burst(buf, at, Filter.Band(1900f, 3f), 4f * amp, 70f);
                Burst(buf, at, Filter.Band(3200f, 4f), 2.5f * amp, 90f);
                Tone(buf, at, 210f, 0.7f * amp, 35f);
            }
            return Normalize(buf, 0.9f);
        }

        /// <summary>Sayfa çevirme: dalgalanan hışırtı, sonda kâğıdın şaklaması.</summary>
        public static float[] PageTurn()
        {
            const float length = 0.34f;
            var buf = Buffer(length);
            var body = Filter.Band(3000f, 0.7f);
            float last = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Noise();
                float hiss = (n - last) * 0.5f + body.Process(n);
                last = n;
                float flutter = Mathf.SmoothStep(0f, 1f, t / 0.2f) * (0.6f + 0.4f * Mathf.Sin(Tau * 28f * t));
                float snap = t > 0.22f ? 1.4f * Mathf.Exp(-(t - 0.22f) * 60f) : 0f;
                buf[i] = hiss * (t < 0.22f ? flutter : snap);
            }
            return Normalize(buf, 0.6f);
        }

        /// <summary>Kâğıdı masada sürükleme: yumuşak, boğuk sürtünme.</summary>
        public static float[] PaperSlide()
        {
            const float length = 0.3f;
            var buf = Buffer(length);
            var filter = Filter.Band(1800f, 0.6f);
            float wobble = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                wobble += (Noise() - wobble) * 0.001f;
                buf[i] = filter.Process(Noise()) * Mathf.Sin(Mathf.PI * i / buf.Length) * (0.7f + 3f * wobble);
            }
            return Normalize(buf, 0.5f);
        }

        /// <summary>Keçeli kalemle çizgi: düzensiz vuruşlu kuru sürtünme.</summary>
        public static float[] Pen()
        {
            const float length = 0.55f;
            var buf = Buffer(length);
            var scratch = Filter.Band(3000f, 2f);
            var air = Filter.High(1500f);
            float phase = 0f, rate = Range(5.5f, 7.5f);
            for (int i = 0; i < buf.Length; i++)
            {
                float k = i / (float)buf.Length;
                phase += rate * (1f + 0.3f * Mathf.Sin(Tau * 1.3f * k)) / Rate;
                scratch.Tune(2600f + 800f * Mathf.Sin(Tau * phase * 0.5f), 2f);
                float n = Noise();
                float stroke = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Mathf.PI * phase));
                buf[i] = (scratch.Process(n) + 0.25f * air.Process(n)) * stroke * Mathf.Clamp01(k / 0.05f) * Mathf.Clamp01((1f - k) / 0.15f);
            }
            return Normalize(buf, 0.5f);
        }

        /// <summary>"KAYIT ALTINA ALINDI": alçak darbe ve resmî, iki tonlu uyarı bipleri.</summary>
        public static float[] Alarm()
        {
            var buf = Buffer(0.95f);
            Tone(buf, 0f, 55f, 1f, 6f, 0.004f);
            Burst(buf, 0f, Filter.Low(200f), 2f, 8f);
            var tone = Filter.Low(3500f);
            float[] notes = { 988f, 740f, 988f, 740f };
            for (int n = 0; n < notes.Length; n++)
            {
                float start = 0.05f + n * 0.2f;
                int from = Index(start), count = Index(0.15f);
                for (int i = 0; i < count && from + i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t / 0.005f) * Mathf.Clamp01((0.15f - t) / 0.015f);
                    float square = (float)System.Math.Tanh(3f * Mathf.Sin(Tau * notes[n] * t));
                    buf[from + i] += tone.Process(square) * 0.45f * env;
                }
            }
            return Normalize(buf, 0.85f);
        }

        /// <summary>Para girdi: kasa çekmecesi ve yükselen iki çan ("ka-ching").</summary>
        public static float[] Coin()
        {
            var buf = Buffer(0.9f);
            Burst(buf, 0f, Filter.High(4000f), 0.3f, 40f);
            Bell(buf, 0.02f, 988f, 0.6f, 5f);
            Bell(buf, 0.11f, 1319f, 0.7f, 4f);
            return Normalize(buf, 0.8f);
        }

        /// <summary>Ödeme: yazar kasa tuşu ("ka-çunk") ve tek çan.</summary>
        public static float[] Pay()
        {
            var buf = Buffer(0.8f);
            Burst(buf, 0f, Filter.Band(1500f, 2f), 3f, 120f);
            Burst(buf, 0.05f, Filter.Band(1100f, 2f), 3f, 100f);
            Tone(buf, 0.05f, 140f, 0.4f, 40f);
            Bell(buf, 0.09f, 1175f, 0.6f, 4.5f);
            return Normalize(buf, 0.8f);
        }

        /// <summary>Borç: alçalan, hüzünlü dört nota.</summary>
        public static float[] Debt()
        {
            var buf = Buffer(1.4f);
            var warm = Filter.Low(1600f);
            float[] notes = { 392f, 370f, 349f, 330f };
            float[] lengths = { 0.22f, 0.22f, 0.22f, 0.6f };
            float start = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                int from = Index(start), count = Index(lengths[n]);
                float phase = 0f;
                for (int i = 0; i < count && from + i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float vibrato = n == notes.Length - 1 ? 1f + 0.015f * Mathf.Sin(Tau * 5f * t) * Mathf.Clamp01(t / 0.15f) : 1f;
                    phase += notes[n] * vibrato / Rate;
                    float saw = 0f;
                    for (int h = 1; h <= 6; h++)
                        saw += Mathf.Sin(Tau * phase * h) / h;
                    float env = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((lengths[n] - t) / 0.04f);
                    buf[from + i] += warm.Process(saw) * env;
                }
                start += lengths[n];
            }
            return Normalize(buf, 0.7f);
        }

        /// <summary>Telefon bildirimi: temiz, iki notalı "ding-ding".</summary>
        public static float[] Notify()
        {
            var buf = Buffer(0.6f);
            Tone(buf, 0f, 1319f, 0.7f, 9f);
            Tone(buf, 0f, 2638f, 0.2f, 14f);
            Tone(buf, 0.1f, 1760f, 0.7f, 9f);
            Tone(buf, 0.1f, 3520f, 0.2f, 14f);
            return Normalize(buf, 0.7f);
        }

        /// <summary>Eski tüplü televizyon açılışı: düğme tıkı, tüp darbesi, sönen cızırtı.</summary>
        public static float[] TvOn()
        {
            var buf = Buffer(0.8f);
            Burst(buf, 0f, Filter.High(2500f), 0.8f, 400f);
            Tone(buf, 0f, 60f, 0.5f, 20f);
            Tone(buf, 0.02f, 120f, 0.3f, 4f);
            Burst(buf, 0.03f, Filter.Band(4000f, 0.5f), 1.2f, 5f, 0.02f);
            return Normalize(buf, 0.6f);
        }

        /// <summary>SON DAKİKA jeneriği: alçak vuruş, üç kesik nota ve tutulan akor.</summary>
        public static float[] Breaking()
        {
            var buf = Buffer(1.4f);
            Tone(buf, 0f, 65f, 1f, 8f);
            Burst(buf, 0f, Filter.Low(250f), 2f, 10f);
            Tone(buf, 0.5f, 55f, 0.8f, 5f);
            Burst(buf, 0.5f, Filter.Low(300f), 1.5f, 8f);
            var brass = Filter.Low(2500f);
            void Stab(float start, float length, params float[] freqs)
            {
                int from = Index(start), count = Index(length);
                for (int i = 0; i < count && from + i < buf.Length; i++)
                {
                    float t = i / (float)Rate;
                    float s = 0f;
                    foreach (float f in freqs)
                        for (int h = 1; h <= 8; h++)
                            s += Mathf.Sin(Tau * f * h * t) / h;
                    float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * (length > 0.3f ? 3f : 12f)) * Mathf.Clamp01((length - t) / 0.03f);
                    buf[from + i] += brass.Process(s / freqs.Length) * 0.5f * env;
                }
            }
            Stab(0.05f, 0.12f, 587f);
            Stab(0.2f, 0.12f, 587f);
            Stab(0.35f, 0.12f, 880f);
            Stab(0.5f, 0.85f, 587f, 740f, 880f);
            return Normalize(buf, 0.85f);
        }

        /// <summary>Gazete baskı makinesi: motor uğultusu ve yavaşlayarak duran mekanik takırtı.</summary>
        public static float[] Press()
        {
            const float length = 1.8f;
            var buf = Buffer(length);
            var motor = Filter.Low(300f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float saw = 2f * (t * 50f % 1f) - 1f;
                buf[i] += motor.Process(saw) * 0.25f * Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((length - t) / 0.5f);
            }
            float at = 0f, period = 0.13f;
            while (at < length - 0.2f)
            {
                float fade = Mathf.Clamp01((length - at) / 0.6f);
                Burst(buf, at, Filter.Band(1300f, 3f), 3f * fade, 70f);
                Tone(buf, at, 300f, 0.3f * fade, 50f);
                at += period;
                if (at > length - 0.6f)
                    period *= 1.18f;
            }
            return Normalize(buf, 0.8f);
        }

        // ---------------------------------------------------------------- ofis ve karakterler (yaklaşık)

        /// <summary>Ayak sesi (kundura): topuk tıkı, alçak darbe ve kısa sürtünme. variant farklı tınılar verir.</summary>
        public static float[] Step(int variant)
        {
            var buf = Buffer(0.2f);
            float heel = 1800f + 250f * variant;
            Burst(buf, 0f, Filter.Band(heel, 1.2f), 3f, 90f);
            Tone(buf, 0f, 110f + 12f * variant, 0.6f, 45f);
            Burst(buf, 0.06f, Filter.Band(3500f, 0.8f), 0.4f, 60f, 0.005f);
            return Normalize(buf, 0.8f);
        }

        /// <summary>Kapı açılırken menteşe gıcırtısı.</summary>
        public static float[] DoorOpen()
        {
            var buf = Buffer(0.7f);
            Burst(buf, 0f, Filter.High(2500f), 0.3f, 300f);
            Creak(buf, 0.03f, 0.65f, 70f, 160f, 1100f, 40f);
            return Normalize(buf, 0.7f);
        }

        /// <summary>Kapı kapanırken tok darbe ve dil tıkı.</summary>
        public static float[] DoorClose()
        {
            var buf = Buffer(0.45f);
            Tone(buf, 0f, 75f, 1f, 18f);
            Burst(buf, 0f, Filter.Low(400f), 3f, 30f);
            Burst(buf, 0.02f, Filter.High(3000f), 0.4f, 250f);
            Burst(buf, 0.09f, Filter.Band(2200f, 3f), 0.8f, 120f);
            return Normalize(buf, 0.9f);
        }

        /// <summary>Sandalye: oturunca önce tok darbe sonra yerleşme gıcırtısı; kalkınca yalnız gıcırtı.</summary>
        public static float[] Chair(bool sitting)
        {
            var buf = Buffer(sitting ? 0.6f : 0.35f);
            if (sitting)
            {
                Tone(buf, 0.01f, 90f, 0.8f, 20f);
                Burst(buf, 0.01f, Filter.Low(300f), 2f, 25f);
                Creak(buf, 0.12f, 0.4f, 40f, 110f, 700f, 30f);
            }
            else
                Creak(buf, 0f, 0.3f, 120f, 50f, 700f, 30f);
            return Normalize(buf, 0.7f);
        }

        // ---------------------------------------------------------------- deprem ve ara sahneler

        /// <summary>Bina çökmesi: çatırtı, uzun alçak gümbürtü, dökülen moloz ve ağır düşüşler.</summary>
        public static float[] Collapse()
        {
            const float length = 3.8f;
            var buf = Buffer(length);
            Burst(buf, 0f, Filter.High(800f), 2f, 20f);
            var rumble = Brown(length);
            var low = Filter.Low(120f);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Clamp01(t / 0.2f) * (t < 2.5f ? 1f : Mathf.Clamp01((length - t) / 1.3f));
                buf[i] += low.Process(rumble[i]) * 8f * env;
            }
            for (int n = 0; n < 220; n++)
            {
                float at = 3f * Mathf.Pow(Rand(), 1.6f);
                float amp = Range(0.2f, 0.8f) * (1f - at / 3.5f);
                Burst(buf, at, Filter.Band(Range(300f, 2500f), 1.5f), amp * 6f, Range(15f, 60f));
            }
            for (int n = 0; n < 4; n++)
                Tone(buf, Range(0.1f, 1.5f), Range(45f, 70f), 0.8f, 4f, 0.01f);
            return Normalize(buf, 0.95f);
        }

        /// <summary>Cam kırılması: sert çarpma ve saçılan küçük çınlamalar.</summary>
        public static float[] Glass()
        {
            const float length = 1.4f;
            var buf = Buffer(length);
            Burst(buf, 0f, Filter.High(2500f), 1f, 18f);
            for (int n = 0; n < 140; n++)
            {
                float at = 1.2f * Mathf.Pow(Rand(), 2f);
                float freq = Range(2500f, 7500f);
                float amp = Range(0.1f, 0.5f) * (1f - at / length);
                float decay = Range(25f, 70f);
                Tone(buf, at, freq, amp, decay, 0.0005f);
                Tone(buf, at, freq * 1.5f, amp * 0.3f, decay * 1.4f, 0.0005f);
            }
            return Normalize(buf, 0.85f);
        }

        /// <summary>Uzaktan geçen siren: yükselip alçalan ton, boğuk ve sönerek.</summary>
        public static float[] Siren()
        {
            const float length = 5f;
            var buf = Buffer(length);
            var distance = Filter.Low(2200f);
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                phase += (950f + 400f * Mathf.Sin(Tau * t / 2.6f - Mathf.PI / 2f)) / Rate;
                float s = Mathf.Sin(Tau * phase) + 0.3f * Mathf.Sin(2f * Tau * phase) + 0.15f * Mathf.Sin(3f * Tau * phase);
                buf[i] = distance.Process(s) * Mathf.Clamp01(t / 1.2f) * Mathf.Clamp01((length - t) / 1.8f);
            }
            return Normalize(buf, 0.6f);
        }

        // ---------------------------------------------------------------- ortam döngüleri

        /// <summary>Ofis ortamı: havalandırma uğultusu ve uzaktan gelip giden trafik.</summary>
        public static float[] Room()
        {
            const float length = 9f;
            var hum = Brown(length);
            var low = Filter.Low(250f);
            var air = Filter.Band(1200f, 0.4f);
            var buf = Buffer(length);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)Rate;
                float traffic = 0.5f + 0.5f * Mathf.Sin(Tau * t / 7f);
                buf[i] = low.Process(hum[i]) * 3f + air.Process(Noise()) * 0.3f * traffic;
            }
            return Normalize(Loop(buf, 1f), 0.5f);
        }

        /// <summary>Hapishane koridoru: alçak uğultu, uzak metal tıngırtıları ve damlalar, yankılı.</summary>
        public static float[] Prison()
        {
            const float length = 11.5f;
            var buf = Buffer(length);
            var drone = Brown(length);
            var low = Filter.Low(140f);
            for (int i = 0; i < buf.Length; i++)
                buf[i] = low.Process(drone[i]) * 3f;

            var dry = Buffer(length);
            foreach (float at in new[] { 1.2f, 4.7f, 8.3f })
            {
                float amp = Range(0.4f, 0.7f);
                Tone(dry, at, 310f, amp, 3f);
                Tone(dry, at, 467f, amp * 0.7f, 4f);
                Tone(dry, at, 789f, amp * 0.5f, 5f);
                Tone(dry, at, 1210f, amp * 0.3f, 6f);
                Burst(dry, at, Filter.Band(1800f, 2f), amp, 80f);
            }
            for (int n = 0; n < 5; n++)
            {
                int from = Index(Range(0.5f, length - 1.5f));
                float phase = 0f;
                for (int i = 0; i < Index(0.05f); i++)
                {
                    float t = i / (float)Rate;
                    phase += Mathf.Lerp(1400f, 900f, t / 0.05f) / Rate;
                    dry[from + i] += Mathf.Sin(Tau * phase) * 0.25f * Mathf.Exp(-t * 60f);
                }
            }
            Tone(dry, 6.1f, 60f, 0.6f, 10f);
            Burst(dry, 6.1f, Filter.Low(350f), 1.5f, 18f);

            // Dairesel tarak yankısı: kuyruk döngünün başına sarar.
            var distant = Filter.Low(2500f);
            for (int i = 0; i < dry.Length; i++)
                dry[i] = distant.Process(dry[i]);
            var wet = new float[dry.Length];
            foreach (int delay in new[] { 3114, 3234, 2982, 2844 })
            {
                var comb = new float[dry.Length];
                for (int pass = 0; pass < 3; pass++)
                    for (int i = 0; i < dry.Length; i++)
                        comb[i] = dry[i] + 0.82f * comb[(i - delay + dry.Length) % dry.Length];
                for (int i = 0; i < wet.Length; i++)
                    wet[i] += comb[i] * 0.25f;
            }
            for (int i = 0; i < buf.Length; i++)
                buf[i] += dry[i] * 0.5f + wet[i] * 0.6f;
            return Normalize(Loop(buf, 1.5f), 0.6f);
        }
    }
}
