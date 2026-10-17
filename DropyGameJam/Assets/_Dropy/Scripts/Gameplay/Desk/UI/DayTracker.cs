using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Sağ üstteki gün göstergesi: her gün için bir daire (bugün renkli ve büyük, geçenler tamam, gelecek soluk),
    /// altında o günün dosyaları için küçük noktalar. Deprem beklenen günün altında kırmızı ünlem, hafif sarsıntı
    /// günü altında dalga; üstüne gelince o günün uyarısı (Story > News > forecast) görünür. Altta masadaki müteahhit.
    /// </summary>
    public class DayTracker : MonoBehaviour
    {
        static readonly Color Panel = new Color(0.1f, 0.1f, 0.12f, 0.85f);
        static readonly Color Current = new Color(0.98f, 0.76f, 0.3f);
        static readonly Color Past = new Color(0.45f, 0.62f, 0.45f);
        static readonly Color Future = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color Alarm = new Color(0.86f, 0.2f, 0.17f);
        static readonly Color Tremor = new Color(0.95f, 0.6f, 0.25f);

        class DayView
        {
            public RectTransform circle;
            public Image fill;
            public TextMeshProUGUI number;
            public readonly List<Image> pips = new List<Image>();
            public Image marker;
        }

        readonly List<DayView> days = new List<DayView>();
        TextMeshProUGUI contractorText;
        RectTransform tooltip;
        TextMeshProUGUI tooltipText;
        System.Func<int, string> forecast;
        int quakeDay;

        /// <param name="forecastOf">Günün uyarı metni (yoksa null): üstüne gelince görünür.</param>
        /// <param name="isQuakeDay">Altında kırmızı ünlem olacak günler.</param>
        /// <param name="isTremorDay">Altında dalga olacak günler.</param>
        public void Build(RectTransform root, int dayCount, int filesPerDay, System.Func<int, string> forecastOf,
            System.Func<int, bool> isQuakeDay, System.Func<int, bool> isTremorDay)
        {
            forecast = forecastOf;
            const float circle = 54f, gap = 34f;
            float width = dayCount * circle + (dayCount - 1) * gap + 60f;
            var panel = Ui.Image(root, "DayTracker", Panel, Ui.Rounded).rectTransform;
            panel.Place(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(Mathf.Max(width, 300f), 136f));

            float startX = -(dayCount - 1) * (circle + gap) / 2f;
            for (int d = 1; d <= dayCount; d++)
            {
                var view = new DayView();
                float x = startX + (d - 1) * (circle + gap);
                view.circle = Ui.Rect($"Day{d}", panel);
                view.circle.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -42f), new Vector2(circle, circle));
                view.fill = Ui.Image(view.circle, "Fill", Future);
                view.fill.sprite = PrintArt.Disc;
                view.fill.rectTransform.Stretch();
                view.number = Ui.Text(view.circle, "Number", d.ToString(), 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                view.number.rectTransform.Stretch();

                // O günün dosyaları: dairenin altında küçük noktalar.
                for (int f = 0; f < filesPerDay; f++)
                {
                    var pip = Ui.Image(panel, "Pip", Future, raycast: false);
                    pip.sprite = PrintArt.Disc;
                    float px = x + (f - (filesPerDay - 1) / 2f) * 14f;
                    pip.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(px, -80f), new Vector2(9f, 9f));
                    view.pips.Add(pip);
                }

                if (isQuakeDay(d) || isTremorDay(d))
                {
                    bool quake = isQuakeDay(d);
                    if (quake)
                        quakeDay = d;
                    view.marker = Ui.Image(view.circle, "Marker", quake ? Alarm : Tremor, raycast: false);
                    view.marker.sprite = PrintArt.Disc;
                    view.marker.rectTransform.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 4f), new Vector2(22f, 22f));
                    var mark = Ui.Text(view.marker.rectTransform, "Mark", quake ? "!" : "~", 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
                    mark.rectTransform.Stretch();
                }

                int dayIndex = d;
                var trigger = view.fill.gameObject.AddComponent<EventTrigger>();
                AddTrigger(trigger, EventTriggerType.PointerEnter, () => ShowTooltip(dayIndex, view.circle));
                AddTrigger(trigger, EventTriggerType.PointerExit, () => tooltip.gameObject.SetActive(false));
                days.Add(view);
            }

            contractorText = Ui.Text(panel, "Contractor", "", 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center);
            contractorText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(width + 40f, 28f));

            tooltip = Ui.Image(root, "DayTooltip", new Color(0.05f, 0.05f, 0.07f, 0.95f), Ui.Rounded).rectTransform;
            tooltip.pivot = new Vector2(1f, 1f);
            tooltipText = Ui.Text(tooltip, "Text", "", 20f, Color.white, TextAlignmentOptions.TopLeft);
            tooltipText.richText = true;
            tooltipText.rectTransform.Stretch(14f, 10f, 14f, 10f);
            tooltip.gameObject.SetActive(false);
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void ShowTooltip(int day, RectTransform circle)
        {
            string text = forecast != null ? forecast(day) : null;
            if (string.IsNullOrEmpty(text))
                return;
            tooltipText.text = $"<b>{day}. gün</b>\n{text}";
            var size = tooltipText.GetPreferredValues(tooltipText.text, 360f, 0f);
            tooltip.sizeDelta = new Vector2(Mathf.Min(size.x, 360f) + 28f, size.y + 20f);
            tooltip.anchorMin = tooltip.anchorMax = new Vector2(1f, 1f);
            var corners = new Vector3[4];
            circle.GetWorldCorners(corners);
            tooltip.position = new Vector3(corners[2].x, corners[0].y - 70f, 0f);
            tooltip.SetAsLastSibling();
            tooltip.gameObject.SetActive(true);
        }

        /// <param name="file">Bugünün kaçıncı dosyası masada (1'den).</param>
        public void Set(int day, int file, string contractor)
        {
            for (int i = 0; i < days.Count; i++)
            {
                int d = i + 1;
                var view = days[i];
                view.fill.color = d < day ? Past : d == day ? Current : Future;
                view.number.color = d > day ? new Color(1f, 1f, 1f, 0.45f) : d == day ? new Color(0.15f, 0.1f, 0.05f) : Color.white;
                view.circle.localScale = Vector3.one * (d == day ? 1.15f : 1f);
                for (int f = 0; f < view.pips.Count; f++)
                {
                    bool done = d < day || (d == day && f < file - 1);
                    bool now = d == day && f == file - 1;
                    view.pips[f].color = done ? Past : now ? Current : Future;
                }
            }
            contractorText.text = contractor;
        }

        void Update()
        {
            // Deprem günü ünlemi nabız gibi atar; jüri bir bakışta "sonda deprem var" desin.
            if (quakeDay <= 0 || quakeDay > days.Count || days[quakeDay - 1].marker == null)
                return;
            float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 4f);
            days[quakeDay - 1].marker.rectTransform.localScale = Vector3.one * pulse;
        }
    }
}
