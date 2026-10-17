using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Menu
{
    /// <summary>
    /// Giriş ekranı için koddan çizilen sprite'lar: ikaz şeridi, baret, oynat/dişli/hoparlör ikonları
    /// ve geçiş gradyanları. Kenarlar 3x3 süper örneklemeyle yumuşatılır; ek texture gerektirmez.
    /// </summary>
    public static class MenuArt
    {
        public static readonly Color HazardYellow = new Color(0.98f, 0.76f, 0.1f);
        public static readonly Color HazardBlack = new Color(0.1f, 0.09f, 0.08f);

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Döşenebilir sarı-siyah çapraz ikaz şeridi (Image.Type.Tiled ile kullan).</summary>
        public static Sprite HazardStripes => Get("hazard", 64, 64, true,
            (x, y) => Mathf.Repeat(x + y, 64f) < 32f ? HazardYellow : HazardBlack);

        public static Sprite Circle => Get("circle", 64, 64, false,
            (x, y) => Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f)) <= 30f ? Color.white : Color.clear);

        public static Sprite Play => Get("play", 64, 64, false,
            (x, y) => x >= 20f && x <= 50f && Mathf.Abs(y - 32f) <= (50f - x) * 0.62f ? Color.white : Color.clear);

        public static Sprite Gear => Get("gear", 128, 128, false, (x, y) =>
        {
            var d = new Vector2(x - 64f, y - 64f);
            float r = d.magnitude;
            float a = Mathf.Atan2(d.y, d.x);
            bool tooth = Mathf.Repeat(a / (2f * Mathf.PI) * 8f, 1f) < 0.5f;
            bool body = r >= 20f && (r <= 44f || (tooth && r <= 58f));
            return body ? Color.white : Color.clear;
        });

        public static Sprite Speaker => Get("speaker", 64, 64, false, (x, y) =>
        {
            bool box = x >= 6f && x <= 18f && Mathf.Abs(y - 32f) <= 9f;
            bool cone = x >= 18f && x <= 32f && Mathf.Abs(y - 32f) <= 9f + (x - 18f) * 1.1f;
            var d = new Vector2(x - 32f, y - 32f);
            float r = d.magnitude;
            bool arcSide = d.x > 0f && Mathf.Abs(Mathf.Atan2(d.y, d.x)) < 0.8f;
            bool waves = arcSide && (Mathf.Abs(r - 14f) <= 2.6f || Mathf.Abs(r - 25f) <= 2.6f);
            return box || cone || waves ? Color.white : Color.clear;
        });

        /// <summary>Soldan sağa 0→1 alfa gradyanı (karartma için).</summary>
        public static Sprite FadeRight => Get("fadeRight", 64, 4, false,
            (x, y) => new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, x / 64f)));

        /// <summary>Aşağıdan yukarıya 0→1 alfa gradyanı.</summary>
        public static Sprite FadeUp => Get("fadeUp", 4, 64, false,
            (x, y) => new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, y / 64f)));

        /// <summary>Gölgeli, çerçeveli sarı inşaat bareti.</summary>
        public static Sprite HardHat => Get("hardhat", 256, 176, false, (x, y) =>
        {
            if (HatShape(x, y, 0f, out bool dome))
            {
                if (!dome)
                    return new Color(0.9f, 0.6f, 0.06f);
                float light = Mathf.Clamp01((y - 58f) / 92f) * 0.25f
                              + Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(98f, 118f)) / 34f) * 0.35f;
                var baseColor = Mathf.Abs(x - 128f) < 11f ? new Color(0.92f, 0.66f, 0.06f) : HazardYellow;
                return Color.Lerp(baseColor, new Color(1f, 0.95f, 0.7f), light);
            }
            return HatShape(x, y, 6f, out _) ? new Color(0.2f, 0.13f, 0.05f) : Color.clear;
        });

        static bool HatShape(float x, float y, float grow, out bool dome)
        {
            float dx = (x - 128f) / (90f + grow), dy = (y - 58f) / (92f + grow);
            dome = y >= 54f - grow && dx * dx + dy * dy <= 1f;
            // Siper: uçları yuvarlatılmış yatay kapsül
            float cx = Mathf.Clamp(x, 32f, 224f);
            bool brim = Vector2.Distance(new Vector2(x, y), new Vector2(cx, 48f)) <= 12f + grow;
            if (brim && !(dome && y > 60f)) dome = false;
            return dome || brim;
        }

        static Sprite Get(string key, int width, int height, bool tiled, System.Func<float, float, Color> paint)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            const int ss = 3;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Premultiplied ortalama: şeffaf kenarlarda koyu hale oluşmasın
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    var c = paint(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                }
                pixels[y * width + x] = a > 0f ? new Color(r / a, g / a, b / a, a / (ss * ss)) : Color.clear;
            }
            tex.SetPixels(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            Cache[key] = sprite;
            return sprite;
        }
    }
}
