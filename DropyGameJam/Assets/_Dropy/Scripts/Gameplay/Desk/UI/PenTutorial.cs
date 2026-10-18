using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Kırmızı kalemle öğretici: belgeler açılınca o güne ve dosyaya ait hamleleri (<see cref="TutorialData"/>)
    /// sırayla ekrana çizer: hedefin etrafına daire, iki hedef arasına ok, yanına el yazısıyla not. Çizgi kalem
    /// gidiyormuş gibi uçtan uca uzar. Oyuncuyu durdurmaz (tıklamalar alttaki belgelere geçer), mühür basılınca silinir;
    /// her hamle oyunda bir kez gösterilir. Yeni hamle çizilirken öncekiler soluklaşır; notlar birbirinin üstüne binmez.
    /// </summary>
    public class PenTutorial : MonoBehaviour
    {
        const float ReferenceWidth = 1920f;
        const float Thickness = 5f;
        /// <summary>Yeni hamle çizilirken öncekiler bu saydamlığa iner: göz hep son hamlede kalır.</summary>
        const float PastAlpha = 0.85f;
        /// <summary>Kalem izinin ve yazının arkasındaki yumuşak krem hale: karışık zeminde de okunsun, kutu gibi sırıtmasın.</summary>
        static readonly Color Halo = new Color(1f, 0.97f, 0.9f, 0.25f);
        const float HaloWidth = 2f;
        const float NoteMargin = 24f;
        /// <summary>
        /// Notun arkasına çekilen fosforlu kalem şeridi: takım elbise, beyaz gömlek, bej kâğıt ve kahve duvar;
        /// hangi zeminden geçerse geçsin yazı sarının üstünde okunur.
        /// </summary>
        static readonly Color Highlighter = new Color(1f, 0.9f, 0.18f, 0.85f);
        /// <summary>Not yazısı koyu bordo: sarı şeridin üstünde en net okunan mürekkep.</summary>
        static readonly Color NoteInk = new Color(0.45f, 0.03f, 0.08f);
        const float HighlighterSweep = 0.22f;
        /// <summary>Şeridin yazının iki ucundan taştığı pay (piksel).</summary>
        const float HighlighterOverhang = 30f;
        static Sprite highlighterSprite;
        /// <summary>El yazısı eğik ve not hafif döndürülmüş: ölçülen genişliğin biraz fazlası ayrılır.</summary>
        const float SlantAllowance = 30f;

        TutorialData data;
        RectTransform overlay;
        /// <summary>Çizilmekte olan hamlenin katmanı; çizgiler ve not buna eklenir.</summary>
        RectTransform layer;
        RectTransform haloLayer;
        System.Func<TutorialTarget, RectTransform> resolve;
        /// <summary>Notun mümkünse üstüne yazılmayacağı paneller (belgeler, bina kartı).</summary>
        System.Func<IEnumerable<RectTransform>> obstacles;
        readonly HashSet<TutorialStep> shown = new HashSet<TutorialStep>();
        readonly List<CanvasGroup> layers = new List<CanvasGroup>();
        /// <summary>Ekrandaki notların kapladığı alanlar: yeni not bunlarla çakışmayan bir yere yazılır.</summary>
        readonly List<Rect> placedNotes = new List<Rect>();
        Coroutine running;

        public void Build(RectTransform root, TutorialData tutorial, System.Func<TutorialTarget, RectTransform> resolver,
            System.Func<IEnumerable<RectTransform>> avoid = null)
        {
            data = tutorial;
            resolve = resolver;
            obstacles = avoid;
            overlay = Ui.Rect("PenTutorial", root).Stretch();
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        /// <summary>Bu gün ve dosya için henüz gösterilmemiş hamleleri çizer.</summary>
        public void Play(int day, int fileOfDay)
        {
            if (data == null)
                return;
            Clear();
            var steps = data.steps.Where(s => s.day == day && s.fileOfDay == fileOfDay && !shown.Contains(s)).ToList();
            if (steps.Count > 0)
                running = StartCoroutine(Run(steps));
        }

        /// <summary>Çizimleri siler (mühür basılınca, dosya kapanınca, akış kesilince).</summary>
        public void Clear()
        {
            if (running != null)
                StopCoroutine(running);
            running = null;
            layers.Clear();
            placedNotes.Clear();
            if (overlay == null)
                return;
            foreach (Transform child in overlay)
                Destroy(child.gameObject);
        }

        IEnumerator Run(List<TutorialStep> steps)
        {
            overlay.SetAsLastSibling();
            // Belgeler açılırken yerleşsin.
            for (float t = 0f; t < 0.45f; t += Time.unscaledDeltaTime)
                yield return null;
            foreach (var step in steps)
            {
                shown.Add(step);
                var a = Bounds(step.from);
                if (a == null)
                    continue;
                var b = Bounds(step.to);
                foreach (var past in layers)
                    past.alpha = PastAlpha;
                layer = Ui.Rect("Step", overlay).Stretch();
                layers.Add(layer.gameObject.AddComponent<CanvasGroup>());
                // Haleler hep mürekkebin altında kalsın diye adımın ilk çocuğu ayrı bir katman.
                haloLayer = Ui.Rect("Halo", layer).Stretch();
                yield return Draw(step, a.Value, b);
                for (float t = 0f; t < data.pause; t += Time.unscaledDeltaTime)
                    yield return null;
            }
            running = null;
        }

        /// <summary>Hedefin overlay içindeki dikdörtgeni (merkez ve yarı boyut); bulunamazsa null.</summary>
        Rect? Bounds(TutorialTarget target)
        {
            if (target == null || target.kind == TutorialTargetKind.None)
                return null;
            return Bounds(resolve(target));
        }

        Rect? Bounds(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy)
                return null;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector2 min = overlay.InverseTransformPoint(corners[0]);
            Vector2 max = overlay.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        IEnumerator Draw(TutorialStep step, Rect a, Rect? b)
        {
            var strokes = new List<List<Vector2>>();
            // Notun aday yerleri (sol kenar ortası), tercih sırasıyla; boyut not ölçülünce belli olur.
            System.Func<Vector2, List<Vector2>> spots;
            switch (step.shape)
            {
                case PenShape.Underline:
                    strokes.Add(Underline(a));
                    spots = size => new List<Vector2>
                    {
                        new Vector2(a.xMin, a.yMin - size.y / 2f - 16f),
                        new Vector2(a.xMin, a.yMax + size.y / 2f + 16f),
                        new Vector2(a.xMax - size.x - SlantAllowance, a.yMax + size.y / 2f + 16f),
                        new Vector2(a.xMin - size.x - SlantAllowance - 20f, a.center.y),
                    };
                    break;
                case PenShape.Arrow:
                case PenShape.DoubleArrow:
                    if (b == null)
                        goto default;
                    var curve = Curve(a, b.Value);
                    strokes.Add(curve);
                    strokes.AddRange(Head(curve[curve.Count - 2], curve[curve.Count - 1]));
                    if (step.shape == PenShape.DoubleArrow)
                        strokes.AddRange(Head(curve[1], curve[0]));
                    var dir = (curve[curve.Count - 1] - curve[0]).normalized;
                    var normal = new Vector2(-dir.y, dir.x);
                    spots = size =>
                    {
                        var list = new List<Vector2>();
                        float away = 34f + size.y / 2f;
                        foreach (float along in new[] { 0.5f, 0.35f, 0.65f, 0.2f, 0.8f })
                        {
                            var p = curve[Mathf.RoundToInt(along * (curve.Count - 1))];
                            list.Add(p + normal * away - new Vector2(size.x / 2f, 0f));
                            list.Add(p - normal * away - new Vector2(size.x / 2f, 0f));
                        }
                        return list;
                    };
                    break;
                default:
                    strokes.Add(Circle(a));
                    spots = size => new List<Vector2>
                    {
                        new Vector2(a.xMax + 26f, a.center.y + a.height * 0.2f + 14f),
                        new Vector2(a.xMin - 26f - size.x - SlantAllowance, a.center.y),
                        new Vector2(a.center.x - (size.x + SlantAllowance) / 2f, a.yMin - size.y / 2f - 22f),
                        new Vector2(a.center.x - (size.x + SlantAllowance) / 2f, a.yMax + size.y / 2f + 22f),
                    };
                    break;
            }

            float speed = data.penSpeed * overlay.rect.width / ReferenceWidth;
            foreach (var stroke in strokes)
                yield return Stroke(stroke, speed);
            if (!string.IsNullOrEmpty(step.note))
                yield return Note(step.note, spots);
        }

        IEnumerator Stroke(List<Vector2> points, float speed)
        {
            float budget = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                budget -= Vector2.Distance(points[i - 1], points[i]);
                while (budget < 0f)
                {
                    yield return null;
                    budget += speed * Time.unscaledDeltaTime;
                }
                Segment(points[i - 1], points[i]);
            }
        }

        void Segment(Vector2 a, Vector2 b)
        {
            if (haloLayer != null)
                Line(haloLayer, a, b, Thickness * HaloWidth, Halo, Ui.Rounded);
            Line(layer, a, b, Thickness, data.ink, null);
        }

        void Line(RectTransform parent, Vector2 a, Vector2 b, float thickness, Color color, Sprite sprite)
        {
            var image = Ui.Image(parent, "Ink", color, sprite, raycast: false);
            var line = image.rectTransform;
            var delta = b - a;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = (a + b) / 2f - overlay.rect.center;
            line.sizeDelta = new Vector2(delta.magnitude + thickness * 0.6f, thickness);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>İnce Caveat harfleri aynı mürekkeple biraz kalınlaşır: şeridin üstünde dolgun okunur.</summary>
        static void Thicken(TextMeshProUGUI text, Color ink)
        {
            var material = text.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, ink);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.1f);
            text.fontMaterial = material;
        }

        /// <summary>Fosforlu kalem izi: uçları tırtıklı, boyuna hafif çizgili yarı saydam şerit (bir kez üretilir).</summary>
        static Sprite HighlighterSprite()
        {
            if (highlighterSprite != null)
                return highlighterSprite;
            const int w = 256, h = 64;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Highlighter" };
            var pixels = new Color32[w * h];
            var random = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs(y / (float)(h - 1) - 0.5f) * 2f;
                // Boyuna çizgiler, uçları kalemin yatık ucu gibi tırtıklı; üst ve alt kenar yumuşak.
                float streak = 0.82f + 0.18f * (float)random.NextDouble();
                float left = 4f + 10f * v * v + 4f * (float)random.NextDouble();
                float right = w - 4f - 14f * v * v - 6f * (float)random.NextDouble();
                float edge = Mathf.Clamp01((1f - v) * 6f);
                for (int x = 0; x < w; x++)
                {
                    float ends = Mathf.Clamp01(Mathf.Min(x - left, right - x) / 3f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(255f * streak * edge * ends));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            highlighterSprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return highlighterSprite;
        }

        /// <summary>
        /// Notu aday yerlerden ekrana sığan, önceki notlarla çakışmayan ve mümkünse belgelerin üstüne binmeyen ilkine yazar;
        /// belgesiz yer yoksa çakışmayan ilkine, o da yoksa ekrana sığan ilkine, hiçbiri olmazsa ilk adayı kenarlara kıstırır.
        /// Önce fosforlu şerit çekilir, sonra kalem üstüne yazar.
        /// </summary>
        IEnumerator Note(string text, System.Func<Vector2, List<Vector2>> spots)
        {
            var note = Ui.Text(layer, "Note", text, 40f, NoteInk, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            if (data.font != null)
                note.font = data.font;
            Thicken(note, NoteInk);
            note.textWrappingMode = TextWrappingModes.NoWrap;
            var size = note.GetPreferredValues(text);
            var rt = note.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = size;

            var r = overlay.rect;
            var screen = Rect.MinMaxRect(r.xMin + NoteMargin, r.yMin + NoteMargin, r.xMax - NoteMargin, r.yMax - NoteMargin);
            var candidates = spots(size);
            System.Func<Vector2, Rect> area = p => new Rect(p.x, p.y - size.y / 2f, size.x + SlantAllowance, size.y);
            System.Func<Rect, bool> fits = n => screen.Contains(n.min) && screen.Contains(n.max);
            System.Func<Rect, bool> free = n => !placedNotes.Any(o => o.Overlaps(new Rect(n.x - 6f, n.y - 6f, n.width + 12f, n.height + 12f)));
            var panels = obstacles == null ? new List<Rect>()
                : obstacles().Select(Bounds).Where(b => b.HasValue).Select(b => b.Value).ToList();
            System.Func<Rect, bool> clear = n => !panels.Any(o => o.Overlaps(n));
            Vector2 position;
            if (candidates.Any(p => fits(area(p)) && free(area(p)) && clear(area(p))))
                position = candidates.First(p => fits(area(p)) && free(area(p)) && clear(area(p)));
            else if (candidates.Any(p => fits(area(p)) && free(area(p))))
                position = candidates.First(p => fits(area(p)) && free(area(p)));
            else if (candidates.Any(p => fits(area(p))))
                position = candidates.First(p => fits(area(p)));
            else
            {
                position = candidates[0];
                position.x = Mathf.Clamp(position.x, screen.xMin, screen.xMax - size.x - SlantAllowance);
                position.y = Mathf.Clamp(position.y, screen.yMin + size.y / 2f, screen.yMax - size.y / 2f);
            }
            placedNotes.Add(area(position));

            rt.anchoredPosition = position - r.center;
            rt.localRotation = Quaternion.Euler(0f, 0f, -3f);
            note.maxVisibleCharacters = 0;

            // Fosforlu şerit yazının altında, aynı eğimde; soldan sağa çekilir.
            var strip = Ui.Image(layer, "Highlighter", Highlighter, HighlighterSprite(), raycast: false);
            strip.transform.SetSiblingIndex(note.transform.GetSiblingIndex());
            strip.type = Image.Type.Filled;
            strip.fillMethod = Image.FillMethod.Horizontal;
            strip.fillOrigin = (int)Image.OriginHorizontal.Left;
            strip.fillAmount = 0f;
            var band = strip.rectTransform;
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.pivot = new Vector2(0f, 0.5f);
            band.sizeDelta = new Vector2(size.x + SlantAllowance + 2f * HighlighterOverhang, size.y * 0.82f);
            band.anchoredPosition = rt.anchoredPosition + new Vector2(-HighlighterOverhang, -2f);
            band.localRotation = rt.localRotation;
            for (float t = 0f; t < HighlighterSweep; t += Time.unscaledDeltaTime)
            {
                strip.fillAmount = t / HighlighterSweep;
                yield return null;
            }
            strip.fillAmount = 1f;

            for (float shownChars = 0f; shownChars < text.Length; shownChars += 40f * Time.unscaledDeltaTime)
            {
                note.maxVisibleCharacters = Mathf.FloorToInt(shownChars);
                yield return null;
            }
            note.maxVisibleCharacters = text.Length;
        }

        // ---------------------------------------------------------------- elle çizilmiş şekiller

        static List<Vector2> Circle(Rect r)
        {
            var points = new List<Vector2>();
            float rx = r.width / 2f + 18f, ry = r.height / 2f + 14f;
            float seed = Random.Range(0f, 10f);
            const int count = 56;
            // Biraz fazla döner (elle çizilmiş daire kapanırken üst üste biner) ve hafif eğridir.
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                float angle = Mathf.PI * (1.15f + 2.12f * t);
                float wobble = 1f + 0.05f * Mathf.Sin(angle * 3f + seed) + 0.04f * t;
                points.Add(r.center + new Vector2(Mathf.Cos(angle) * rx * wobble, Mathf.Sin(angle) * ry * wobble + 4f * t));
            }
            return points;
        }

        static List<Vector2> Underline(Rect r)
        {
            var points = new List<Vector2>();
            const int count = 24;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                points.Add(new Vector2(Mathf.Lerp(r.xMin - 6f, r.xMax + 6f, t), r.yMin - 6f + Mathf.Sin(t * Mathf.PI * 3f) * 3f));
            }
            return points;
        }

        /// <summary>İki dikdörtgenin kenarından kenarına hafif kavisli çizgi.</summary>
        static List<Vector2> Curve(Rect a, Rect b)
        {
            var dir = (b.center - a.center).normalized;
            var start = Edge(a, dir) + dir * 10f;
            var end = Edge(b, -dir) - dir * 10f;
            float length = Vector2.Distance(start, end);
            var control = (start + end) / 2f + new Vector2(-dir.y, dir.x) * length * 0.18f;
            var points = new List<Vector2>();
            const int count = 32;
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                points.Add((1 - t) * (1 - t) * start + 2 * (1 - t) * t * control + t * t * end);
            }
            return points;
        }

        static Vector2 Edge(Rect r, Vector2 dir)
        {
            float sx = Mathf.Abs(dir.x) > 0.001f ? r.width / 2f / Mathf.Abs(dir.x) : float.MaxValue;
            float sy = Mathf.Abs(dir.y) > 0.001f ? r.height / 2f / Mathf.Abs(dir.y) : float.MaxValue;
            return r.center + dir * Mathf.Min(sx, sy);
        }

        /// <summary>Ok ucu: <paramref name="tip"/> noktasından geriye iki kısa çizgi.</summary>
        static IEnumerable<List<Vector2>> Head(Vector2 from, Vector2 tip)
        {
            var back = (from - tip).normalized * 24f;
            yield return new List<Vector2> { tip, tip + (Vector2)(Quaternion.Euler(0f, 0f, 28f) * back) };
            yield return new List<Vector2> { tip, tip + (Vector2)(Quaternion.Euler(0f, 0f, -28f) * back) };
        }
    }
}
