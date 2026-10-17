using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Müteahhidin ağzından çıkacak cümleyi seçer ve yer tutucuları doldurur. Sıra: dosyanın kendi cümlesi →
    /// müteahhidin üzerine yazdığı → kişilik tipi → koddaki varsayılan. Böylece bir yazıyı değiştirmek için kod
    /// değil, ilgili ayar dosyası düzenlenir.
    /// </summary>
    public static class Voice
    {
        static readonly CultureInfo Tr = new CultureInfo("tr-TR");
        static readonly System.Random Rng = new System.Random();
        static readonly Regex Placeholder = new Regex(@"\{([a-z]+)\}");
        /// <summary>Satır cevabının sonuna kendine has lafın eklenme olasılığı.</summary>
        const double TicChance = 0.35;

        static IEnumerable<VoiceLines> Layers(BuildingCase c)
        {
            if (c == null || c.contractor == null)
                yield break;
            if (c.contractor.overrides != null)
                yield return c.contractor.overrides;
            if (c.contractor.personality != null && c.contractor.personality.lines != null)
                yield return c.contractor.personality.lines;
        }

        /// <summary>Cümle seçer: dosyanın kendi cümlesi doluysa o, değilse katmanlardaki ilk dolu listeden biri, yoksa varsayılan.</summary>
        public static string Line(BuildingCase c, System.Func<VoiceLines, List<string>> pool, string caseLine, string fallback,
            IDictionary<string, string> extra = null)
        {
            string text = !string.IsNullOrEmpty(caseLine) ? caseLine : Pick(c, pool);
            return Fill(text ?? fallback, c, extra);
        }

        /// <summary>Katmanlardaki ilk dolu listeden rastgele bir cümle; hiçbiri doluysa null.</summary>
        public static string Pick(BuildingCase c, System.Func<VoiceLines, List<string>> pool)
        {
            foreach (var layer in Layers(c))
            {
                var list = pool(layer);
                if (list != null && list.Count > 0)
                    return list[Rng.Next(list.Count)];
            }
            return null;
        }

        /// <summary>Belge satırı cevabı için kişiliğe ait kalıp; yoksa null (kod kendi cevabını kullanır).</summary>
        public static string FieldAnswer(BuildingCase c, string key, bool defensive)
        {
            foreach (var layer in Layers(c))
                foreach (var line in layer.fields)
                    if (line != null && line.field == key && line.defensive == defensive && !string.IsNullOrEmpty(line.text))
                        return line.text;
            return null;
        }

        /// <summary>Bazen (her seferinde değil) satır cevabının sonuna eklenen kendine has laf.</summary>
        public static string Tic(BuildingCase c) => Rng.NextDouble() < TicChance ? Pick(c, l => l.tics) : null;

        public static string Greeting(BuildingCase c) => Pick(c, l => l.greetings);

        public static float TypingSpeed(BuildingCase c)
        {
            foreach (var layer in Layers(c))
                if (layer.typingSpeed > 0f)
                    return layer.typingSpeed;
            return 1f;
        }

        static string Address(BuildingCase c)
        {
            foreach (var layer in Layers(c))
                if (!string.IsNullOrEmpty(layer.address))
                    return layer.address;
            return "";
        }

        /// <summary>
        /// Yer tutucuları doldurur: {isim} {hitap} {bina} {kat} {kunye} {rapor} {cekic} {tavan} {not}; ek sözlükteki
        /// anahtarlar da ({kasa} {eksik} {hazirlik} gibi). Bilinmeyen yer tutucu olduğu gibi kalır. {hitap} boşsa
        /// geride kalan çift boşluk ve "boşluk + noktalama" temizlenir.
        /// </summary>
        public static string Fill(string text, BuildingCase c, IDictionary<string, string> extra = null)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            string filled = Placeholder.Replace(text, m =>
            {
                string key = m.Groups[1].Value;
                if (extra != null && extra.TryGetValue(key, out var value))
                    return value;
                if (c == null)
                    return m.Value;
                switch (key)
                {
                    case "isim": return c.contractorName;
                    case "hitap": return Address(c);
                    case "bina": return c.buildingName;
                    case "kat": return c.floors.ToString();
                    case "kunye": return BuildingCase.Concrete(c.claimedConcrete);
                    case "rapor": return BuildingCase.Concrete(c.labConcrete);
                    case "cekic": return BuildingCase.Concrete(c.hammerConcrete > 0 ? c.hammerConcrete : c.labConcrete);
                    case "tavan": return c.groundFloorHeight.ToString("0.##", Tr);
                    case "not": return c.note;
                    default: return m.Value;
                }
            });
            filled = Regex.Replace(filled, @" {2,}", " ");
            filled = Regex.Replace(filled, @" ([,.!?;:])", "$1");
            filled = Regex.Replace(filled, @",([.!?])", "$1");
            return filled.Trim();
        }
    }
}
