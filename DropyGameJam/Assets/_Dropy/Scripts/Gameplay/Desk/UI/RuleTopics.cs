using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Yönetmelik kuralı ile belgedeki satırları eşleştirir: her konunun bir rengi ve harfli rozeti var.
    /// Kitapta açılmış (günü gelmiş) konular renklenir; renk "neye bakacağını" söyler, "neyin yanlış olduğunu" değil.
    /// Bir satırın ya da kuralın üstüne gelince <see cref="Highlighted"/> yayınlanır, karşı taraf parlar.
    /// </summary>
    public static class RuleTopics
    {
        static readonly HashSet<RuleTopic> active = new HashSet<RuleTopic>();

        /// <summary>Fare bir konunun üstünde (None = ayrıldı).</summary>
        public static event System.Action<RuleTopic> Highlighted;

        public static Color Color(RuleTopic topic)
        {
            switch (topic)
            {
                case RuleTopic.Beton: return new Color(0.09f, 0.6f, 0.62f);
                case RuleTopic.YumusakKat: return new Color(0.93f, 0.52f, 0.13f);
                case RuleTopic.Kolon: return new Color(0.56f, 0.36f, 0.86f);
                case RuleTopic.Cekic: return new Color(0.82f, 0.62f, 0.08f);
                default: return new Color(0.5f, 0.5f, 0.5f);
            }
        }

        /// <summary>Rozetteki harf (renk körü oyuncu da eşleştirebilsin).</summary>
        public static string Letter(RuleTopic topic)
        {
            switch (topic)
            {
                case RuleTopic.Beton: return "B";
                case RuleTopic.YumusakKat: return "Y";
                case RuleTopic.Kolon: return "K";
                case RuleTopic.Cekic: return "Ç";
                default: return "";
            }
        }

        /// <summary>Belge satırının konusu.</summary>
        public static RuleTopic OfField(string key)
        {
            switch (key)
            {
                case FieldKey.Claimed:
                case FieldKey.Lab: return RuleTopic.Beton;
                case FieldKey.Hammer: return RuleTopic.Cekic;
                case FieldKey.Use:
                case FieldKey.Height:
                case FieldKey.ShearWall: return RuleTopic.YumusakKat;
                case FieldKey.Columns: return RuleTopic.Kolon;
                default: return RuleTopic.None;
            }
        }

        /// <summary>Kitapta sayfası açılmış konular (yalnız bunlar renklenir).</summary>
        public static void SetActive(IEnumerable<RuleTopic> topics)
        {
            active.Clear();
            foreach (var t in topics)
                if (t != RuleTopic.None)
                    active.Add(t);
        }

        public static bool IsActive(RuleTopic topic) => topic != RuleTopic.None && active.Contains(topic);

        public static void Highlight(RuleTopic topic) => Highlighted?.Invoke(topic);

        /// <summary>Konunun harfli renkli rozeti (yuvarlak köşeli kare).</summary>
        public static RectTransform Badge(Transform parent, RuleTopic topic, float size)
        {
            var badge = Ui.Image(parent, "Topic", Color(topic), Ui.Rounded, raycast: false).rectTransform;
            badge.sizeDelta = new Vector2(size, size);
            var letter = Ui.Text(badge, "Letter", Letter(topic), size * 0.62f, UnityEngine.Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            letter.rectTransform.Stretch();
            return badge;
        }
    }
}
