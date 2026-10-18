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
        const float PastAlpha = 0.35f;
        const float NoteMargin = 24f;
        /// <summary>El yazısı eğik ve not hafif döndürülmüş: ölçülen genişliğin biraz fazlası ayrılır.</summary>
        const float SlantAllowance = 30f;

        TutorialData data;
        RectTransform overlay;
        /// <summary>Çizilmekte olan hamlenin katmanı; çizgiler ve not buna eklenir.</summary>
        RectTransform layer;
        System.Func<TutorialTarget, RectTransform> resolve;
        readonly HashSet<TutorialStep> shown = new HashSet<TutorialStep>();
        readonly List<CanvasGroup> layers = new List<CanvasGroup>();
        /// <summary>Ekrandaki notların kapladığı alanlar: yeni not bunlarla çakışmayan bir yere yazılır.</summary>
        readonly List<Rect> placedNotes = new List<Rect>();
        Coroutine running;

        public void Build(RectTransform root, TutorialData tutorial, System.Func<TutorialTarget, RectTransform> resolver)
        {
            data = tutorial;
            resolve = resolver;
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
            var rt = resolve(target);
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
            var line = Ui.Image(layer, "Ink", data.ink, raycast: false).rectTransform;
            var delta = b - a;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = (a + b) / 2f - overlay.rect.center;
            line.sizeDelta = new Vector2(delta.magnitude + Thickness * 0.6f, Thickness);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Notu aday yerlerden ekrana tam sığan ve önceki notlarla çakışmayan ilkine yazar; hiçbiri olmazsa ekrana sığan ilkine,
        /// o da yoksa ilk adayı kenarlara kıstırır.
        /// </summary>
        IEnumerator Note(string text, System.Func<Vector2, List<Vector2>> spots)
        {
            var note = Ui.Text(layer, "Note", text, 34f, data.ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            if (data.font != null)
                note.font = data.font;
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
            Vector2 position;
            if (candidates.Any(p => fits(area(p)) && free(area(p))))
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
