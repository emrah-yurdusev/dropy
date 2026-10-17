using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>Bina incelemesinde büyüteçle bulunan, müteahhide gösterilebilen tek kusur.</summary>
    public class ModelDefect
    {
        public string title;
        public string detail;
        /// <summary>Tıklanınca müteahhide sorulan satır (<see cref="FieldKey"/>).</summary>
        public DocumentField field;
        /// <summary>Binanın yerel uzayında tıklama kutusu.</summary>
        public Bounds bounds;
        public Mesh bars;
        public Mesh fill;
        public bool found;
    }

    /// <summary>
    /// Vakadan prosedürel low-poly bina: dış görünüş (maket) ve taşıyıcı iskelet (büyütecin içi).
    /// Ölçüler metre; ön cephe -z. Kusurlar iskelette ayrı mesh'tir ve kırmızı çizilir.
    /// Kaçak kat ruhsattan fazla çıkılmış kattır: dışarıdan kapalı teras gibi görünür, iskelette kırmızıdır.
    /// </summary>
    public class BuildingModel
    {
        public const float Bay = 3f;
        public const float FloorHeight = 3f;
        static readonly float[] Xs = { -4.5f, -1.5f, 1.5f, 4.5f };
        static readonly float[] Zs = { -3f, 0f, 3f };
        const float Width = 9f;
        const float Depth = 6f;

        static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        static readonly Color[] Plasters =
        {
            new Color(0.87f, 0.79f, 0.63f),
            new Color(0.8f, 0.57f, 0.46f),
            new Color(0.7f, 0.76f, 0.79f),
            new Color(0.86f, 0.83f, 0.71f),
            new Color(0.76f, 0.7f, 0.82f),
        };
        static readonly Color Concrete = new Color(0.82f, 0.81f, 0.78f);
        static readonly Color Glass = new Color(0.36f, 0.52f, 0.62f);
        static readonly Color ShopGlass = new Color(0.5f, 0.68f, 0.76f);
        static readonly Color Frame = new Color(0.95f, 0.94f, 0.9f);
        static readonly Color Door = new Color(0.42f, 0.27f, 0.17f);
        static readonly Color Sign = new Color(0.78f, 0.22f, 0.18f);
        static readonly Color Aluminium = new Color(0.78f, 0.82f, 0.84f);
        static readonly Color Pavement = new Color(0.6f, 0.6f, 0.57f);
        static readonly Color Tank = new Color(0.25f, 0.42f, 0.62f);

        public static readonly Color Line = new Color(1f, 0.66f, 0.58f);
        public static readonly Color Ghost = new Color(0.42f, 0.2f, 0.2f);
        public static readonly Color Permit = new Color(0.96f, 0.95f, 0.88f);
        public static readonly Color Wall = new Color(0.45f, 0.65f, 1f);
        public static readonly Color DefectLine = new Color(1f, 0.12f, 0.08f);
        public static readonly Color DefectFill = new Color(1f, 0.1f, 0.06f, 0.26f);

        public Mesh Solid { get; private set; }
        public Mesh Wire { get; private set; }
        public readonly List<ModelDefect> Defects = new List<ModelDefect>();
        public float Height { get; private set; }
        public Vector3 Size => new Vector3(Width, Height, Depth);

        int floors;
        int built;
        float ground;

        float Level(int i) => i <= 0 ? 0f : ground + (i - 1) * FloorHeight;

        public static BuildingModel Build(BuildingCase c)
        {
            var model = new BuildingModel();
            model.Create(c);
            return model;
        }

        public void Destroy()
        {
            Object.Destroy(Solid);
            Object.Destroy(Wire);
            foreach (var d in Defects)
            {
                Object.Destroy(d.bars);
                Object.Destroy(d.fill);
            }
        }

        void Create(BuildingCase c)
        {
            floors = Mathf.Max(1, c.floors);
            built = floors + Mathf.Max(0, c.illegalFloors);
            ground = Mathf.Max(2.6f, c.groundFloorHeight);
            Height = Level(built) + 1.4f;
            var flaws = BuildingRules.Flaws(c);

            Solid = BuildSolid(c);
            Wire = BuildWire(c, flaws);
            AddDefects(c, flaws);
        }

        // ---------------- Dış görünüş ----------------

        Mesh BuildSolid(BuildingCase c)
        {
            var m = new MeshBuilder();
            var plaster = Plasters[Mathf.Abs(Hash(c.buildingName)) % Plasters.Length];
            m.Box(new Vector3(0f, -0.15f, 0f), new Vector3(Width + 5f, 0.3f, Depth + 5f), Pavement);

            for (int i = 0; i < built; i++)
            {
                float y0 = Level(i), y1 = Level(i + 1), h = y1 - y0;
                if (i >= floors)
                {
                    IllegalFloorShell(m, y0, h);
                    continue;
                }
                if (i == 0 && c.groundUse == GroundUse.Market)
                    Shop(m, plaster, h);
                else
                {
                    m.Box(new Vector3(0f, y0 + h / 2f, 0f), new Vector3(Width, h, Depth), plaster);
                    Windows(m, y0, h, door: i == 0);
                }
                // Döşeme bandı.
                m.Box(new Vector3(0f, y1 - 0.12f, 0f), new Vector3(Width + 0.25f, 0.24f, Depth + 0.25f), Concrete);
            }

            // Ruhsatlı çatının parapeti; kaçak kat varsa onun içinde kalır.
            float roof = Level(floors);
            Parapet(m, roof, Width + 0.25f, Depth + 0.25f, plaster);
            float top = Level(built);
            if (built > floors)
                Parapet(m, top, Width - 0.7f, Depth - 0.7f, Aluminium);
            m.Box(new Vector3(2.6f, top + 0.55f, 1.2f), new Vector3(1.3f, 1.1f, 1.3f), Tank);
            m.Bar(new Vector3(-3f, top, 1.8f), new Vector3(-3f, top + 1.6f, 1.8f), 0.06f, Concrete);
            return m.ToMesh("BuildingSolid");
        }

        void Shop(MeshBuilder m, Color plaster, float h)
        {
            // Arka ve yanlar sıvalı, ön cephe boydan cam vitrin ve tabela.
            m.Box(new Vector3(0f, h / 2f, 0.3f), new Vector3(Width, h, Depth - 0.6f), plaster);
            m.Box(new Vector3(0f, h / 2f, -Depth / 2f + 0.35f), new Vector3(Width - 0.2f, h, 0.7f), ShopGlass);
            for (int b = 0; b < Xs.Length; b++)
                m.Box(new Vector3(Xs[b], h / 2f, -Depth / 2f + 0.05f), new Vector3(0.3f, h, 0.3f), Frame);
            m.Box(new Vector3(0f, h - 0.55f, -Depth / 2f - 0.05f), new Vector3(Width - 0.3f, 0.7f, 0.18f), Sign);
        }

        void Windows(MeshBuilder m, float y0, float h, bool door)
        {
            float wy = y0 + h * 0.52f;
            for (int b = 0; b < 3; b++)
            {
                float x = -Bay + b * Bay;
                bool isDoor = door && b == 1;
                if (isDoor)
                    m.Box(new Vector3(x, y0 + 1.05f, -Depth / 2f - 0.03f), new Vector3(1.1f, 2.1f, 0.1f), Door);
                else
                    Window(m, new Vector3(x, wy, -Depth / 2f), Vector3.back);
                Window(m, new Vector3(x, wy, Depth / 2f), Vector3.forward);
            }
            for (int b = 0; b < 2; b++)
            {
                float z = -Bay / 2f + b * Bay;
                Window(m, new Vector3(-Width / 2f, wy, z), Vector3.left);
                Window(m, new Vector3(Width / 2f, wy, z), Vector3.right);
            }
        }

        static void Window(MeshBuilder m, Vector3 at, Vector3 outward)
        {
            var across = Mathf.Abs(outward.x) > 0.5f ? new Vector3(0.12f, 1.4f, 1.4f) : new Vector3(1.4f, 1.4f, 0.12f);
            var glass = Mathf.Abs(outward.x) > 0.5f ? new Vector3(0.14f, 1.2f, 1.2f) : new Vector3(1.2f, 1.2f, 0.14f);
            m.Box(at + outward * 0.02f, across, Frame);
            m.Box(at + outward * 0.04f, glass, Glass);
        }

        void IllegalFloorShell(MeshBuilder m, float y0, float h)
        {
            // Kapalı teras görünümü: içeri çekilmiş alüminyum doğrama, sık cam şeritler.
            const float inset = 0.35f;
            float w = Width - inset * 2f, d = Depth - inset * 2f;
            m.Box(new Vector3(0f, y0 + h / 2f, 0f), new Vector3(w, h - 0.1f, d), Aluminium);
            for (int k = 0; k < 7; k++)
            {
                float x = -w / 2f + 0.65f + k * (w - 1.3f) / 6f;
                m.Box(new Vector3(x, y0 + h * 0.5f, -d / 2f - 0.04f), new Vector3(0.9f, h * 0.62f, 0.1f), Glass);
                m.Box(new Vector3(x, y0 + h * 0.5f, d / 2f + 0.04f), new Vector3(0.9f, h * 0.62f, 0.1f), Glass);
            }
            for (int k = 0; k < 4; k++)
            {
                float z = -d / 2f + 0.6f + k * (d - 1.2f) / 3f;
                m.Box(new Vector3(-w / 2f - 0.04f, y0 + h * 0.5f, z), new Vector3(0.1f, h * 0.62f, 0.9f), Glass);
                m.Box(new Vector3(w / 2f + 0.04f, y0 + h * 0.5f, z), new Vector3(0.1f, h * 0.62f, 0.9f), Glass);
            }
            m.Box(new Vector3(0f, y0 + h - 0.05f, 0f), new Vector3(w + 0.3f, 0.12f, d + 0.3f), Aluminium * 0.92f);
        }

        static void Parapet(MeshBuilder m, float y, float w, float d, Color color)
        {
            const float t = 0.2f, h = 0.6f;
            m.Box(new Vector3(0f, y + h / 2f, -d / 2f + t / 2f), new Vector3(w, h, t), color);
            m.Box(new Vector3(0f, y + h / 2f, d / 2f - t / 2f), new Vector3(w, h, t), color);
            m.Box(new Vector3(-w / 2f + t / 2f, y + h / 2f, 0f), new Vector3(t, h, d - 2f * t), color);
            m.Box(new Vector3(w / 2f - t / 2f, y + h / 2f, 0f), new Vector3(t, h, d - 2f * t), color);
        }

        // ---------------- İskelet ----------------

        static bool IsCutColumn(int floor, float x, float z) => floor == 0 && x == 1.5f && z == 3f;

        Mesh BuildWire(BuildingCase c, List<Flaw> flaws)
        {
            var m = new MeshBuilder();
            bool cut = flaws.Contains(Flaw.CutColumn);

            // Zemin çizgisi (silik).
            Rectangle(m, -0.02f, Width + 5f, Depth + 5f, 0.04f, Ghost);
            for (int k = -3; k <= 3; k++)
            {
                m.Bar(new Vector3(k * 2f, -0.02f, -(Depth + 5f) / 2f), new Vector3(k * 2f, -0.02f, (Depth + 5f) / 2f), 0.025f, Ghost);
            }

            // Temel.
            if (!flaws.Contains(Flaw.SandFoundation))
                Foundation(m, c.foundation, Line * 0.8f);

            // Ruhsatlı katların kolon ve kirişleri.
            for (int i = 0; i < floors; i++)
            {
                float y0 = Level(i), y1 = Level(i + 1);
                foreach (var x in Xs)
                foreach (var z in Zs)
                    if (!(cut && IsCutColumn(i, x, z)))
                        m.Bar(new Vector3(x, y0, z), new Vector3(x, y1, z), 0.16f, Line);
                Beams(m, y1, 0.11f, Line);
            }
            Beams(m, 0f, 0.09f, Line * 0.8f);

            if (c.hasShearWall)
            {
                // Perde: yan cephelerde zemin katta çaprazlı panel.
                foreach (var x in new[] { -Width / 2f, Width / 2f })
                for (int b = 0; b < 2; b++)
                {
                    float z0 = Zs[b], z1 = Zs[b + 1];
                    m.Bar(new Vector3(x, 0f, z0), new Vector3(x, ground, z1), 0.08f, Wall);
                    m.Bar(new Vector3(x, ground, z0), new Vector3(x, 0f, z1), 0.08f, Wall);
                    m.Box(new Vector3(x, ground / 2f, (z0 + z1) / 2f), new Vector3(0.06f, ground - 0.1f, Bay - 0.1f), Wall * 0.45f);
                }
            }

            // Kaçak kat varsa ruhsat sınırı: ruhsatlı çatıda kesikli açık çizgi.
            if (built > floors)
                Dashed(m, Level(floors) + 0.02f, Width + 0.6f, Depth + 0.6f, Permit);
            return m.ToMesh("BuildingWire");
        }

        static void Foundation(MeshBuilder m, Foundation type, Color color)
        {
            Rectangle(m, -0.6f, Width + 1f, Depth + 1f, 0.09f, color);
            Rectangle(m, 0f, Width + 1f, Depth + 1f, 0.06f, color);
            foreach (var x in new[] { -(Width + 1f) / 2f, (Width + 1f) / 2f })
            foreach (var z in new[] { -(Depth + 1f) / 2f, (Depth + 1f) / 2f })
                m.Bar(new Vector3(x, -0.6f, z), new Vector3(x, 0f, z), 0.06f, color);
            if (type == Gameplay.Foundation.Kazik)
                foreach (var x in Xs)
                foreach (var z in Zs)
                    m.Bar(new Vector3(x, -0.6f, z), new Vector3(x, -4.2f, z), 0.12f, color);
        }

        static void Beams(MeshBuilder m, float y, float t, Color color)
        {
            foreach (var z in Zs)
                m.Bar(new Vector3(Xs[0], y, z), new Vector3(Xs[Xs.Length - 1], y, z), t, color);
            foreach (var x in Xs)
                m.Bar(new Vector3(x, y, Zs[0]), new Vector3(x, y, Zs[Zs.Length - 1]), t, color);
        }

        static void Rectangle(MeshBuilder m, float y, float w, float d, float t, Color color)
        {
            var a = new Vector3(-w / 2f, y, -d / 2f);
            var b = new Vector3(w / 2f, y, -d / 2f);
            var c = new Vector3(w / 2f, y, d / 2f);
            var e = new Vector3(-w / 2f, y, d / 2f);
            m.Bar(a, b, t, color);
            m.Bar(b, c, t, color);
            m.Bar(c, e, t, color);
            m.Bar(e, a, t, color);
        }

        static void Dashed(MeshBuilder m, float y, float w, float d, Color color)
        {
            var corners = new[]
            {
                new Vector3(-w / 2f, y, -d / 2f), new Vector3(w / 2f, y, -d / 2f),
                new Vector3(w / 2f, y, d / 2f), new Vector3(-w / 2f, y, d / 2f)
            };
            for (int k = 0; k < 4; k++)
            {
                var a = corners[k];
                var b = corners[(k + 1) % 4];
                float length = Vector3.Distance(a, b);
                int dashes = Mathf.RoundToInt(length / 0.6f);
                for (int s = 0; s < dashes; s += 2)
                    m.Bar(Vector3.Lerp(a, b, s / (float)dashes), Vector3.Lerp(a, b, (s + 1) / (float)dashes), 0.07f, color);
            }
        }

        // ---------------- Kusurlar ----------------

        void AddDefects(BuildingCase c, List<Flaw> flaws)
        {
            if (built > floors)
            {
                var bars = new MeshBuilder();
                var fill = new MeshBuilder();
                float y0 = Level(floors), y1 = Level(built);
                for (int i = floors; i < built; i++)
                {
                    float a = Level(i), b = Level(i + 1);
                    foreach (var x in Xs)
                    foreach (var z in Zs)
                        bars.Bar(new Vector3(x, a, z), new Vector3(x, b, z), 0.16f, DefectLine);
                    Beams(bars, b, 0.11f, DefectLine);
                }
                var size = new Vector3(Width + 0.2f, y1 - y0, Depth + 0.2f);
                var center = new Vector3(0f, (y0 + y1) / 2f, 0f);
                fill.Box(center, size, DefectFill);
                int extra = built - floors;
                Add("KAÇAK KAT", $"Künyede {floors} kat, yerinde {built} kat" + (extra > 1 ? $" ({extra} kat fazla)" : ""),
                    FieldKey.IllegalFloor, "Ruhsatsız kat", $"{floors + 1}. kat", new Bounds(center, size), bars, fill);
            }

            if (flaws.Contains(Flaw.CutColumn))
            {
                var bars = new MeshBuilder();
                var fill = new MeshBuilder();
                const float x = 1.5f, z = 3f;
                bars.Bar(new Vector3(x, 0f, z), new Vector3(x, 0.55f, z), 0.18f, DefectLine);
                bars.Bar(new Vector3(x, ground - 0.7f, z), new Vector3(x, ground, z), 0.18f, DefectLine);
                // Kopuk uçlarda zikzak.
                Zigzag(bars, new Vector3(x, 0.55f, z));
                Zigzag(bars, new Vector3(x, ground - 0.7f, z));
                var center = new Vector3(x, ground / 2f, z);
                var size = new Vector3(1f, ground, 1f);
                fill.Box(center, size, DefectFill);
                Add("KESİK KOLON", "Arka cephe, zemin kat kolonu kesilmiş",
                    FieldKey.Columns, "Kolon", "Kesik", new Bounds(center, size), bars, fill);
            }

            if (flaws.Contains(Flaw.SoftStory))
            {
                var bars = new MeshBuilder();
                var fill = new MeshBuilder();
                Rectangle(bars, 0.05f, Width + 0.1f, Depth + 0.1f, 0.08f, DefectLine);
                Rectangle(bars, ground - 0.05f, Width + 0.1f, Depth + 0.1f, 0.08f, DefectLine);
                // Duvarsız kat: yan cephelerde kesikli çapraz, "burada perde yok".
                foreach (var xs in new[] { -Width / 2f, Width / 2f })
                    DashedLine(bars, new Vector3(xs, 0f, -Depth / 2f), new Vector3(xs, ground, Depth / 2f), DefectLine);
                var center = new Vector3(0f, ground / 2f, 0f);
                var size = new Vector3(Width + 0.3f, ground, Depth + 0.3f);
                fill.Box(center, size, DefectFill * new Color(1f, 1f, 1f, 0.6f));
                string h = c.groundFloorHeight.ToString("0.0#", Tr);
                Add("YUMUŞAK KAT", $"Zemin kat {h} m, duvarsız ve perdesiz",
                    FieldKey.Height, "Zemin kat yük.", h + " m", new Bounds(center, size), bars, fill);
            }

            if (flaws.Contains(Flaw.SandFoundation))
            {
                var bars = new MeshBuilder();
                var fill = new MeshBuilder();
                Foundation(bars, c.foundation, DefectLine);
                var center = new Vector3(0f, -0.3f, 0f);
                var size = new Vector3(Width + 1.2f, 0.7f, Depth + 1.2f);
                fill.Box(center, size, DefectFill);
                Add("ZAYIF TEMEL", $"Kum zemin, {built} kat, radye temel",
                    FieldKey.Foundation, "Temel", "Radye", new Bounds(center, size), bars, fill);
            }

            if (flaws.Contains(Flaw.HeavyDamage))
            {
                var bars = new MeshBuilder();
                var fill = new MeshBuilder();
                const float x = -1.5f, z = -3f;
                bars.Bar(new Vector3(x - 0.35f, 0.4f, z - 0.15f), new Vector3(x + 0.35f, ground - 0.4f, z - 0.15f), 0.07f, DefectLine);
                bars.Bar(new Vector3(x + 0.35f, 0.4f, z - 0.15f), new Vector3(x - 0.35f, ground - 0.4f, z - 0.15f), 0.07f, DefectLine);
                var center = new Vector3(x, ground / 2f, z);
                var size = new Vector3(1f, ground, 1f);
                fill.Box(center, size, DefectFill);
                Add("ÇAPRAZ ÇATLAK", "Ön cephe kolonu: kesme çatlağı",
                    FieldKey.Damage, "Hasar tespiti", "Ağır", new Bounds(center, size), bars, fill);
            }
        }

        void Add(string title, string detail, string key, string label, string value, Bounds bounds, MeshBuilder bars, MeshBuilder fill)
        {
            Defects.Add(new ModelDefect
            {
                title = title,
                detail = detail,
                field = new DocumentField { key = key, label = label, value = value },
                bounds = bounds,
                bars = bars.ToMesh(title + " Bars"),
                fill = fill.ToMesh(title + " Fill"),
            });
        }

        static void Zigzag(MeshBuilder m, Vector3 at)
        {
            var a = at + new Vector3(-0.35f, 0f, 0f);
            var b = at + new Vector3(-0.12f, 0.12f, 0f);
            var c = at + new Vector3(0.12f, -0.12f, 0f);
            var d = at + new Vector3(0.35f, 0f, 0f);
            m.Bar(a, b, 0.06f, DefectLine);
            m.Bar(b, c, 0.06f, DefectLine);
            m.Bar(c, d, 0.06f, DefectLine);
        }

        static void DashedLine(MeshBuilder m, Vector3 a, Vector3 b, Color color)
        {
            int dashes = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.5f));
            for (int s = 0; s < dashes; s += 2)
                m.Bar(Vector3.Lerp(a, b, s / (float)dashes), Vector3.Lerp(a, b, (s + 1) / (float)dashes), 0.06f, color);
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s ?? "")
                    h = h * 31 + ch;
                return h;
            }
        }
    }

    /// <summary>Köşe renkli, düz yüzlü kutu ve çubuklardan mesh kurar.</summary>
    public class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public void Box(Vector3 center, Vector3 size, Color color) => Box(center, Quaternion.identity, size, color);

        /// <summary>a'dan b'ye t kalınlığında kare kesitli çubuk.</summary>
        public void Bar(Vector3 a, Vector3 b, float t, Color color)
        {
            var dir = b - a;
            float length = dir.magnitude;
            if (length < 1e-4f)
                return;
            Box((a + b) / 2f, Quaternion.FromToRotation(Vector3.up, dir / length), new Vector3(t, length + t, t), color);
        }

        public void Box(Vector3 center, Quaternion rotation, Vector3 size, Color color)
        {
            var half = size / 2f;
            Face(center, rotation, Vector3.right, Vector3.up, half, color);
            Face(center, rotation, Vector3.left, Vector3.up, half, color);
            Face(center, rotation, Vector3.up, Vector3.forward, half, color);
            Face(center, rotation, Vector3.down, Vector3.forward, half, color);
            Face(center, rotation, Vector3.forward, Vector3.up, half, color);
            Face(center, rotation, Vector3.back, Vector3.up, half, color);
        }

        void Face(Vector3 center, Quaternion rotation, Vector3 n, Vector3 up, Vector3 half, Color color)
        {
            // Dışarıdan bakan için saat yönünde: sol üst, sağ üst, sağ alt, sol alt.
            var right = Vector3.Cross(up, -n);
            float hn = Mathf.Abs(Vector3.Dot(half, n));
            float hr = Mathf.Abs(Vector3.Dot(half, right));
            float hu = Mathf.Abs(Vector3.Dot(half, up));
            var c = n * hn;
            int start = vertices.Count;
            vertices.Add(center + rotation * (c - right * hr + up * hu));
            vertices.Add(center + rotation * (c + right * hr + up * hu));
            vertices.Add(center + rotation * (c + right * hr - up * hu));
            vertices.Add(center + rotation * (c - right * hr - up * hu));
            var normal = rotation * n;
            for (int k = 0; k < 4; k++)
            {
                normals.Add(normal);
                colors.Add(color);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
