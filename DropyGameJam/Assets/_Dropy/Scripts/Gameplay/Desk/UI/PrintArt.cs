using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Görsel bağlanmadığında kullanılan, koddan çizilen yer tutucular: dolu daire, televizyon tarama çizgisi
    /// ve gazetenin noktalı baskı (halftone) fotoğrafı. Hepsi beyaz çizilir; rengi Image'ın rengi verir.
    /// </summary>
    public static class PrintArt
    {
        static Sprite disc;
        static Texture2D scanlines;
        static Sprite portrait;
        static Sprite mugshot;

        public static Sprite Disc
        {
            get
            {
                if (disc != null)
                    return disc;
                const int size = 128;
                var tex = NewTexture(size, size);
                var pixels = new Color32[size * size];
                float r = size / 2f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d + 0.5f) * 255));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return disc;
            }
        }

        /// <summary>4 piksellik tekrar eden doku: üst iki satır siyah, alt iki satır boş.</summary>
        public static Texture2D Scanlines
        {
            get
            {
                if (scanlines != null)
                    return scanlines;
                scanlines = new Texture2D(1, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                scanlines.SetPixels32(new[] { new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 255) });
                scanlines.Apply();
                return scanlines;
            }
        }

        /// <summary>Noktalı baskı fotoğraf: masada oturan memur ya da (sabıka) arkasında boy çizgileri olan memur.</summary>
        public static Sprite Halftone(bool mugshotPose)
        {
            if (mugshotPose && mugshot != null)
                return mugshot;
            if (!mugshotPose && portrait != null)
                return portrait;

            const int width = 300, height = 250, cell = 7;
            var tex = NewTexture(width, height);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float cx = (Mathf.Floor(x / (float)cell) + 0.5f) * cell;
                float cy = (Mathf.Floor(y / (float)cell) + 0.5f) * cell;
                float darkness = mugshotPose ? Mugshot(cx / width, cy / height) : Portrait(cx / width, cy / height);
                float radius = cell * 0.58f * Mathf.Sqrt(darkness);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            if (mugshotPose)
                mugshot = sprite;
            else
                portrait = sprite;
            return sprite;
        }

        static float Portrait(float u, float v)
        {
            if (InEllipse(u, v, 0.5f, 0.66f, 0.12f, 0.17f))
                return v > 0.72f ? 0.85f : 0.3f;
            if (InEllipse(u, v, 0.5f, 0.12f, 0.33f, 0.42f))
                return 0.8f;
            if (v < 0.22f)
                return u > 0.7f && u < 0.86f && v > 0.12f ? 0.05f : 0.7f;
            return 0.16f + 0.08f * Mathf.Sin(u * 9f);
        }

        static float Mugshot(float u, float v)
        {
            if (u > 0.3f && u < 0.7f && v > 0.04f && v < 0.2f)
                return 0.04f;
            if (InEllipse(u, v, 0.5f, 0.6f, 0.13f, 0.19f))
                return v > 0.69f ? 0.85f : 0.35f;
            if (InEllipse(u, v, 0.5f, 0f, 0.38f, 0.42f))
                return 0.82f;
            return Mathf.Repeat(v, 0.13f) < 0.018f ? 0.7f : 0.1f;
        }

        /// <summary>Karalama paragrafının bir satırının yüksekliği (piksel).</summary>
        public const float ScribbleLine = 17f;

        static readonly System.Collections.Generic.Dictionary<int, Sprite> scribbles = new System.Collections.Generic.Dictionary<int, Sprite>();

        /// <summary>
        /// Çizgi filmlerdeki gazete yazısı: harf yok, yazı satırı gibi dizilmiş ince dalgalı çizgiler. Kelime arası gibi
        /// küçük kesikler, içeriden başlayan ilk satır ve kısa son satır. Beyaz çizilir; rengi Image verir.
        /// </summary>
        public static Sprite Scribble(int lines, int seed)
        {
            int key = lines * 1000 + seed;
            if (scribbles.TryGetValue(key, out var cached) && cached != null)
                return cached;
            const int width = 420;
            int height = Mathf.CeilToInt(lines * ScribbleLine);
            var tex = NewTexture(width, height);
            var pixels = new Color32[width * height];
            var rng = new System.Random(seed * 7919 + lines);
            for (int line = 0; line < lines; line++)
            {
                float baseY = height - (line + 0.55f) * ScribbleLine;
                float start = line == 0 ? 22f : 2f;
                float end = line == lines - 1 ? width * (0.35f + 0.4f * (float)rng.NextDouble()) : width - 2f;
                float phase = (float)rng.NextDouble() * 10f;
                float x = start;
                while (x < end)
                {
                    // Bir "kelime": 20–60 piksel dalga, ardından 5–8 piksel boşluk.
                    float word = Mathf.Min(end, x + 20f + (float)rng.NextDouble() * 40f);
                    for (float px = x; px < word; px += 0.35f)
                    {
                        float py = baseY + Mathf.Sin(px * 0.45f + phase) * 2.6f + Mathf.Sin(px * 0.11f + phase) * 0.8f;
                        Plot(pixels, width, height, px, py, 1.1f);
                    }
                    x = word + 5f + (float)rng.NextDouble() * 3f;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            scribbles[key] = sprite;
            return sprite;
        }

        /// <summary>Yumuşak kenarlı nokta: karalama çizgisinin kalemi.</summary>
        static void Plot(Color32[] pixels, int width, int height, float cx, float cy, float radius)
        {
            int x0 = Mathf.FloorToInt(cx - radius - 1), x1 = Mathf.CeilToInt(cx + radius + 1);
            int y0 = Mathf.FloorToInt(cy - radius - 1), y1 = Mathf.CeilToInt(cy + radius + 1);
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(height - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(width - 1, x1); x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                if (a > pixels[y * width + x].a)
                    pixels[y * width + x] = new Color32(255, 255, 255, a);
            }
        }

        static bool InEllipse(float u, float v, float cu, float cv, float ru, float rv)
        {
            float du = (u - cu) / ru, dv = (v - cv) / rv;
            return du * du + dv * dv <= 1f;
        }

        static Texture2D NewTexture(int width, int height) =>
            new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
    }
}
