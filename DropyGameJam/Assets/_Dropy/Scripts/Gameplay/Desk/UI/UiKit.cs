using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Masa arayüzünü koddan kurmak için küçük yardımcılar ve prosedürel sprite'lar
    /// (yuvarlak köşeli panel, çerçeve, yumuşak gölge, elips). Ek texture gerektirmez.
    /// </summary>
    public static class Ui
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Rounded => RoundedRect("rounded", 64, 16, 0f, 1.5f);
        public static Sprite Frame => RoundedRect("frame", 64, 16, 5f, 1.5f);
        public static Sprite Shadow => RoundedRect("shadow", 64, 28, 0f, 22f);
        public static Sprite Ellipse => EllipseRing("ellipse", 128, 64, 5f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = UnityEngine.UI.Image.Type.Sliced;
            }
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            var rt = Rect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.fontStyle = style;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        public static Button Button(Transform parent, string name, string label, Color background, Color foreground, float fontSize)
        {
            var image = Image(parent, name, background, Rounded);
            var button = image.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            var text = Text(image.transform, "Label", label, fontSize, foreground, TextAlignmentOptions.Center, FontStyles.Bold);
            text.rectTransform.Stretch(12f, 4f, 12f, 4f);
            return button;
        }

        /// <summary>Arkasına yumuşak gölge koyar (aynı parent'ta, hemen önüne).</summary>
        public static Image DropShadow(RectTransform target, float spread = 18f, Vector2 offset = default, float alpha = 0.35f)
        {
            var shadow = Image(target, "Shadow", new Color(0f, 0f, 0f, alpha), Shadow, raycast: false);
            shadow.rectTransform.Stretch(-spread + offset.x, -spread + offset.y, -spread - offset.x, -spread - offset.y);
            shadow.transform.SetAsFirstSibling();
            var ignore = shadow.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;
            return shadow;
        }

        static Sprite RoundedRect(string key, int size, int radius, float ring, float feather)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size / 2f;
            float inner = half - radius;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half), py = Mathf.Abs(y + 0.5f - half);
                float dx = Mathf.Max(px - inner, 0f), dy = Mathf.Max(py - inner, 0f);
                float outside = Mathf.Sqrt(dx * dx + dy * dy) - radius + Mathf.Min(Mathf.Max(px - inner, py - inner), 0f);
                float a = Mathf.Clamp01(0.5f - outside / feather);
                if (ring > 0f)
                    a *= Mathf.Clamp01(outside + ring + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var border = new Vector4(radius + 2, radius + 2, radius + 2, radius + 2);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            Cache[key] = sprite;
            return sprite;
        }

        static Sprite EllipseRing(string key, int width, int height, float thickness)
        {
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[width * height];
            float rx = width / 2f - thickness, ry = height / 2f - thickness;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float nx = (x + 0.5f - width / 2f) / rx, ny = (y + 0.5f - height / 2f) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float distPx = Mathf.Abs(d - 1f) * Mathf.Min(rx, ry);
                float a = Mathf.Clamp01(thickness / 2f - distPx + 0.5f);
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            Cache[key] = sprite;
            return sprite;
        }
    }
}
