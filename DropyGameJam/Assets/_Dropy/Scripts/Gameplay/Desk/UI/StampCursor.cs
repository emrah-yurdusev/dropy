using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Mühür elde tutulurken farenin şekli: o mührün renginde lastik damga. Görseller inspector'dan bağlanır
    /// (Texture Type: Cursor); boş olan yerine koddan çizilen damga gelir. Sıcak nokta damganın alt ortasıdır,
    /// yani mühür imlecin ucuna basılır.
    /// </summary>
    [System.Serializable]
    public class StampCursorSet
    {
        public Texture2D approve;
        public Texture2D reject;
        public Texture2D shearWall;
        [Tooltip("Görselin içinde tıklamanın düştüğü nokta (piksel, sol üstten). Boşsa alt orta")]
        public Vector2 hotspot = new Vector2(-1f, -1f);

        static readonly Color ApproveInk = new Color(0.2f, 0.62f, 0.33f);
        static readonly Color RejectInk = new Color(0.8f, 0.2f, 0.17f);
        static readonly Color ShearWallInk = new Color(0.25f, 0.5f, 0.85f);
        static Texture2D drawnApprove, drawnReject, drawnShearWall;

        /// <summary>Elde mühür varsa imleci damgaya çevirir, yoksa sistemin okuna döndürür.</summary>
        public void Apply(Seal seal)
        {
            var texture = Texture(seal);
            if (texture == null)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }
            var spot = hotspot.x >= 0f ? hotspot : new Vector2(texture.width / 2f, texture.height - 2f);
            Cursor.SetCursor(texture, spot, CursorMode.Auto);
        }

        Texture2D Texture(Seal seal)
        {
            switch (seal)
            {
                case Seal.Approve: return approve != null ? approve : Drawn(ref drawnApprove, ApproveInk);
                case Seal.Reject: return reject != null ? reject : Drawn(ref drawnReject, RejectInk);
                case Seal.ShearWall: return shearWall != null ? shearWall : Drawn(ref drawnShearWall, ShearWallInk);
                default: return null;
            }
        }

        static Texture2D Drawn(ref Texture2D cache, Color ink)
        {
            if (cache == null)
                cache = Draw(ink);
            return cache;
        }

        /// <summary>64 piksellik lastik damga: tahta topuz, boyun, gövde ve mürekkepli taban.</summary>
        public static Texture2D Draw(Color ink)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            var wood = new Color(0.55f, 0.34f, 0.18f);
            var woodDark = new Color(0.36f, 0.21f, 0.1f);
            var outline = new Color(0.12f, 0.08f, 0.05f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Texture2D'de y aşağıdan yukarı: taban altta (y küçük), topuz üstte.
                float fx = x + 0.5f, fy = y + 0.5f;
                Color c = Color.clear;
                float knob = Vector2.Distance(new Vector2(fx, fy), new Vector2(32f, 50f));
                if (knob < 11f)
                    c = knob > 9.5f ? outline : Color.Lerp(wood, woodDark, (50f - fy + 8f) / 20f);
                else if (fx > 27f && fx < 37f && fy > 30f && fy <= 41f)
                    c = fx < 28.5f || fx > 35.5f ? outline : woodDark;
                else if (fx > 12f && fx < 52f && fy > 14f && fy <= 30f)
                    c = fx < 13.5f || fx > 50.5f || fy > 28.5f || fy < 15.5f ? outline : wood;
                else if (fx > 10f && fx < 54f && fy > 4f && fy <= 14f)
                    c = fx < 11.5f || fx > 52.5f || fy < 5.5f ? outline : ink;
                pixels[y * size + x] = c;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
