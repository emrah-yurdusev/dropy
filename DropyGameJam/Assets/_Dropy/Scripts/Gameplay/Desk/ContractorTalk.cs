using System.Collections.Generic;
using System.Globalization;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Müteahhitle konuşmanın kuralları. İki şey üretir:
    /// 1) Oyuncu belgede bir satıra tıklayınca müteahhidin o satır hakkındaki cevabı (dosyanın verisinden).
    /// 2) Müteahhit oyuncuya bir şey sorduğunda ("olmaz değil mi?") oyuncunun iki cevap kartı ve müteahhidin tepkisi.
    /// Çürük dosyanın sahibi kusurlu satırda savunmaya geçer ama kural hakkında yalan söylemez:
    /// yalnızca kendi gerekçesini söyler, doğrusunu yönetmelik kitabı gösterir.
    /// </summary>
    public static class ContractorTalk
    {
        static readonly CultureInfo Tr = new CultureInfo("tr-TR");

        public struct Answer
        {
            public string text;
            /// <summary>Müteahhit kusurlu bir satırı savunuyor (yüz ifadesi şüpheli olur).</summary>
            public bool defensive;
        }

        public class Reply
        {
            public string option;
            public string honestReaction;
            public string dishonestReaction;
        }

        /// <summary>Dosyası temiz mi: yönetmelikte hiçbir kusuru yoksa müteahhit rahat konuşur.</summary>
        public static bool IsHonest(BuildingCase c) => BuildingRules.Flaws(c).Count == 0;

        public static Answer AboutField(BuildingCase c, DocumentField field)
        {
            var flaws = BuildingRules.Flaws(c);
            bool honest = flaws.Count == 0;
            bool softStory = flaws.Contains(Flaw.SoftStory);
            bool sand = flaws.Contains(Flaw.SandFoundation);
            bool hammerLies = c.hammerConcrete > 0 && c.hammerConcrete != c.labConcrete;
            string height = c.groundFloorHeight.ToString("0.##", Tr);

            switch (field.key)
            {
                case FieldKey.Name:
                    return Calm(c.damage != DamageLevel.None
                        ? $"{c.buildingName}. Mahallede yıllardır bu adla bilinir."
                        : $"{c.buildingName}. Tabelası bile asıldı, bir mührünüz kaldı.");
                case FieldKey.Floors:
                    if (c.illegalFloors > 0)
                        return Defend($"{c.floors} kat. Projeye birebir uygun.");
                    return sand
                        ? Defend($"{c.floors} kat. Bu sahilde herkes bu kadar yapıyor.")
                        : Calm($"{c.floors} kat. Fazlasını yapmadık.");
                case FieldKey.Claimed:
                    if (c.claimedConcrete != c.labConcrete)
                        return honest
                            ? Calm($"Künyeye yanlış yazılmış. Döktüğümüz beton ek raporda: {BuildingCase.Concrete(c.labConcrete)}.")
                            : Defend("Künyeyi ofis doldurdu, siz rapora bakın.");
                    return hammerLies
                        ? Defend($"İki kâğıt da {BuildingCase.Concrete(c.labConcrete)} diyor. Daha ne olsun?")
                        : Calm($"{BuildingCase.Concrete(c.claimedConcrete)} döktük, raporda da öyle.");
                case FieldKey.Use:
                    if (c.groundUse == GroundUse.Market)
                        return softStory
                            ? Defend("Altı market, kira getiriyor. Ticaret olmadan bina dönmez.")
                            : Calm("Altı market.");
                    return Calm("Altı da daire, konut.");
                case FieldKey.Height:
                    return c.groundFloorHeight >= BuildingRules.SoftStoryHeight
                        ? (softStory
                            ? Defend($"{height} metre. Ferah olsun diye yüksek tuttuk, müşteri tavan görmek ister.")
                            : Calm($"{height} metre, yüksek ama perdesi var."))
                        : Calm($"{height} metre, standart.");
                case FieldKey.Foundation:
                    if (sand)
                        return Defend("Radye döktük. Her yerde kullanılıyor, kazığa ne gerek var?");
                    return c.foundation == Foundation.Kazik
                        ? Calm("Kazık çaktık, sağlam katmana kadar indik.")
                        : Calm("Radye. Zemin uygun.");
                case FieldKey.Columns:
                    return c.columns == ColumnState.Kesik
                        ? Defend("Biraz açtık, yer kazanmak için. Taşır, taşır.")
                        : Calm("Kolonlara dokunmadık, projede ne varsa o.");
                case FieldKey.ShearWall:
                    if (c.hasShearWall)
                        return Calm("Perde var, projede gösterili.");
                    return softStory
                        ? Defend("Perde koysak vitrin kapanır. Kolonlar yeter.")
                        : Calm("Perde yok. Konut zaten, gerek görmedik.");
                case FieldKey.Lab:
                    return hammerLies
                        ? Defend("Laboratuvar raporu bu, imzalı mühürlü. Tartışacak bir şey yok.")
                        : Calm("Numuneyi laboratuvar aldı, sonuç bu.");
                case FieldKey.Soil:
                    if (c.soil == SoilType.Kum)
                        return sand ? Defend("Sahil burası, kum olur. Herkes böyle yapıyor.") : Calm("Kum, ama bina alçak.");
                    return Calm(c.soil == SoilType.Kil ? "Kil. Sağlam sayılır." : "Kaya. Bundan iyisi yok.");
                case FieldKey.Note:
                    if (string.IsNullOrEmpty(c.note))
                        return Calm("Not yok, temiz dosya.");
                    if (!string.IsNullOrEmpty(c.noteAnswer))
                        return honest ? Calm(c.noteAnswer) : Defend(c.noteAnswer);
                    return honest
                        ? Calm($"\"{c.note}\" mi? Ufak bir şey, binayla ilgisi yok.")
                        : Defend($"\"{c.note}\"... Onu da düzeltiriz, merak etmeyin.");
                case FieldKey.Damage:
                    return c.damage == DamageLevel.Heavy
                        ? Defend("Bir iki çatlak, deprem sonrası hepsinde var. Sıvayı atarız, boyarız, yeni gibi olur.")
                        : Calm("Sıvada ince çatlaklar var, taşıyıcıya bir şey olmamış. Raporda yazıyor.");
                case FieldKey.IllegalFloor:
                    return Defend("Orası kat sayılmaz hocam... Terası kapattık sadece.");
                case FieldKey.Hammer:
                    return hammerLies
                        ? Defend("Alet şaşırmıştır, nemden olur. Rapora güvenin.")
                        : Calm("Gördünüz işte, sağlam.");
                default:
                    return Calm("Kâğıtta ne yazıyorsa o.");
            }
        }

        static Answer Calm(string text) => new Answer { text = text };

        static Answer Defend(string text) => new Answer { text = text, defensive = true };

        /// <summary>
        /// Müteahhidin cümlesi oyuncuya yöneltilmiş bir soru ya da baskıysa iki cevap kartı döner, değilse null.
        /// Konu cümledeki kelimelerden anlaşılır; sıra önemlidir (rüşvet, torpil önce gelir).
        /// <paramref name="afterQuake"/>: ilk deprem oldu; artık "hissettim mi" değil "geçmiş olsun" konuşulur.
        /// </summary>
        public static List<Reply> RepliesTo(string line, bool afterQuake)
        {
            string s = line.ToLower(Tr);
            if (Has(s, "dört bin", "zahmetinize"))
                return Two("Rüşvet mi teklif ediyorsunuz?", "Teşekkür diyelim, teşekkür. Kimse bir şey demez.", null,
                    "Dosyaya bakmadan bir şey demem.", "Bakın, bakın. Teklifim duruyor.", null);
            if (Has(s, "indirim"))
                return Two("Burada pazarlık yapılmaz.", "Peki peki, laf arasında dedim.", null,
                    "Önce dosyaya bakalım.", "Buyurun.", null);
            if (Has(s, "başkan", "selam söyledi"))
                return Two("Kimin selamı olursa olsun, kitap aynı.", "Siz bilirsiniz... Başkana iletirim.", null,
                    "Selamını alayım.", "Sağ olun, sağ olun.", null);
            if (Has(s, "son dosya"))
                return Two("Evet, son dosya. Radyo boşuna demiyor, dikkatli olun.", "Siz de hocam, siz de.", "Bir şey olmaz, bu binalar sağlam.",
                    "Önce dosyanıza bakalım.", "Tabii, buyurun.", null);
            if (afterQuake && Has(s, "sallandı", "sarsıntı", "deprem"))
                return Two("Geçmiş olsun. Evdekiler iyi mi?", "Şükür, herkes sağ. Siz de geçmiş olsun.", "Sağ olun. İşimize bakalım.",
                    "Önce kâğıtlara bakalım.", "Tabii, buyurun.", null);
            if (Has(s, "sallandı", "sarsıntı", "deprem"))
                return Two("Ben de hissettim. Dikkatli olun.", "Siz de dikkat edin hocam.", "Bir şey olmaz, bu binalar sağlam.",
                    "Fark etmedim.", "Bana öyle geldi herhalde.", "Neyse, işimize bakalım.");
            if (Has(s, "değil mi", "olur mu", "olmaz", "takıl", "umarım", "herhalde", "sorun"))
                return Two("Kitaba bakmadan söz veremem.", "Haklısınız, buyurun bakın.", "Kitap kitap... Peki, bakın bakalım.",
                    "Bakalım, kâğıtlar ne diyor.", "Tabii, buyurun.", "Bakın bakın, eksik yok.");
            if (s.TrimEnd().EndsWith("?"))
                return Two("Önce kâğıtlara bakayım.", "Tabii.", null,
                    "Göreceğiz.", "Peki.", null);
            return null;
        }

        static bool Has(string s, params string[] words)
        {
            foreach (var w in words)
                if (s.Contains(w))
                    return true;
            return false;
        }

        static List<Reply> Two(string a, string aHonest, string aDishonest, string b, string bHonest, string bDishonest) =>
            new List<Reply>
            {
                new Reply { option = a, honestReaction = aHonest, dishonestReaction = aDishonest ?? aHonest },
                new Reply { option = b, honestReaction = bHonest, dishonestReaction = bDishonest ?? bHonest },
            };
    }
}
