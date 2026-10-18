using System.Collections.Generic;

namespace Dropy.Gameplay
{
    public enum Flaw
    {
        BadConcrete,
        SoftStory,
        CutColumn,
        SandFoundation,
        HeavyDamage
    }

    /// <summary>İlk depremden sonra gelen dosyalarda ek rapordaki hasar tespiti.</summary>
    public enum DamageLevel
    {
        None,
        Light,
        Heavy
    }

    public enum Seal
    {
        None,
        Approve,
        Reject,
        ShearWall
    }

    public enum BuildingFate
    {
        NotBuilt,
        Standing,
        Collapsed
    }

    /// <summary>
    /// Döngü: 4 gün, günde 2 dosya (8 dosya). 4. günün son dosyasından sonra deprem olur; 3. günün 2. dosyasında hafif sarsıntı.
    /// Mühür iskândır: onaylanan binaya aileler taşınır, reddedilen bina boş kalır.
    /// Rakamlar dürüst tam yola göre ayarlıdır: 1–2. günün harcı, kira düşünce çanta + sabitlemeye (500 + 1.000)
    /// tam yeter; 3. günün harcı son akşama 500 TL bırakır. Aileye ödenen her istek bir güvenlik önleminden ya da rüşvetten çıkar.
    /// Değiştirmeden önce o hesabı yeniden yap (üretici script denetler).
    /// Yanlış onaya para cezası yok: depremde onayladığın bina çökerse hapse girersin. Kasa eksiye düşebilir (borç).
    /// </summary>
    public static class Economy
    {
        public const int Days = 4;
        public const int FilesPerDay = 2;
        public const int StartMoney = 0;
        public const int ApproveFee = 1500;
        public const int ShearWallFee = 1000;
        public const int Rent = 500;
        /// <summary>Harç her gün alınır. Kira ve dükkân yalnız akşamı olan günlerde (son günden önce).</summary>
        public const int LastPaidDay = 4;
        /// <summary>Bu günlerin son dosyasından sonra deprem olur.</summary>
        public static readonly int[] QuakeDays = { 4 };

        public static bool IsQuakeDay(int day) => System.Array.IndexOf(QuakeDays, day) >= 0;

        /// <summary>Son akşam: dükkân bu akşamdan sonra kapanır.</summary>
        public static int LastEvening => Days - 1;

        /// <summary>Çekiç 1. günden (öğreticiyle) gelir ve günde bu kadar kullanılabilir; hangi dosyanın ölçüleceği oyuncunun kararı.</summary>
        public const int HammerDay = 1;
        public const int HammerPerDay = 1;

        public static int Day(int fileIndex) => fileIndex / FilesPerDay + 1;
    }

    public enum ShopItem
    {
        Bag,
        Fixing,
        /// <summary>Aile evini güçlendirme/onarım: pahalı; dürüst kasa ancak kusursuz kayıt primiyle, yoksa rüşvetle yeter.</summary>
        Repair
    }

    public static class Shop
    {
        /// <summary>Hazırlık eşyaları (çanta, sabitleme): baskı cümleleri ve denetim bunlara bakar.</summary>
        public static readonly ShopItem[] Items = { ShopItem.Bag, ShopItem.Fixing };
        /// <summary>Dükkânda listelenen her şey.</summary>
        public static readonly ShopItem[] All = { ShopItem.Bag, ShopItem.Fixing, ShopItem.Repair };

        public static int Price(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Bag: return 500;
                case ShopItem.Fixing: return 1000;
                default: return 3500;
            }
        }

        public static string Name(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Bag: return "Deprem çantası";
                case ShopItem.Fixing: return "Eşya sabitleme";
                default: return "Evi güçlendir";
            }
        }

        /// <summary>
        /// Güçlendirme depremden iki akşam önce vitrine çıkar: oyuncu fiyatı rüşvetten önce görür, ertesi gün
        /// gelen teklif bir ikilem olur. Prim son akşam yattığı için kusursuz oyuncu da o akşam alabilir.
        /// </summary>
        public static int AvailableFrom(ShopItem item) => item == ShopItem.Repair ? Economy.QuakeDays[0] - 2 : 1;

        /// <summary>Dükkân son akşama kadar açık; deprem o geceden sonra.</summary>
        public static int AvailableUntil(ShopItem item) => Economy.LastEvening;

        public static bool Offered(ShopItem item, int day) => day >= AvailableFrom(item) && day <= AvailableUntil(item);
    }

    public enum FamilyOutcome
    {
        /// <summary>Ev ayakta kaldı (hasar almadı ya da onarıldı); çanta ve sabitleme yalnız geceyi etkiler.</summary>
        HomeStood,
        /// <summary>Ev yıkıldı; sabit dolaplar enkazda boşluk bıraktı, çantadaki düdük ekipleri getirdi.</summary>
        Survived,
        /// <summary>Ev yıkıldı; iki hazırlıktan biri eksikti, aile yaralı kurtarıldı.</summary>
        Injured,
        /// <summary>Ev yıkıldı; hiçbir hazırlık yoktu, aile kurtarılamadı.</summary>
        Lost
    }

    /// <summary>Aile evinin depremdeki sonu: sarsıntıda hasar aldıysa ve onarılmadıysa yıkılır.</summary>
    public static class FamilyHome
    {
        public static FamilyOutcome Outcome(bool damaged, bool repaired, bool bag, bool fixing)
        {
            if (!damaged || repaired)
                return FamilyOutcome.HomeStood;
            if (bag && fixing)
                return FamilyOutcome.Survived;
            return bag || fixing ? FamilyOutcome.Injured : FamilyOutcome.Lost;
        }
    }

    /// <summary>Yönetmelik: hangi kusur hangi gün kitaba girer, hangi mühür neyi kurtarır.</summary>
    public static class BuildingRules
    {
        public const float SoftStoryHeight = 4f;
        public const int SandFloorLimit = 5;

        public static bool IsValidConcrete(int value) => value == 25 || value == 30 || value == 35;

        public static List<Flaw> Flaws(BuildingCase c)
        {
            var flaws = new List<Flaw>();
            if (!IsValidConcrete(c.TrueConcrete))
                flaws.Add(Flaw.BadConcrete);
            if (c.groundUse == GroundUse.Market && c.groundFloorHeight >= SoftStoryHeight && !c.hasShearWall)
                flaws.Add(Flaw.SoftStory);
            if (c.columns == ColumnState.Kesik)
                flaws.Add(Flaw.CutColumn);
            if (c.soil == SoilType.Kum && c.floors >= SandFloorLimit && c.foundation == Foundation.Radye)
                flaws.Add(Flaw.SandFoundation);
            if (c.damage == DamageLevel.Heavy)
                flaws.Add(Flaw.HeavyDamage);
            return flaws;
        }

        /// <summary>Kuralın yönetmelik kitabına girdiği gün.</summary>
        public static int RuleDay(Flaw flaw)
        {
            switch (flaw)
            {
                // 1. gün öğretici bütün temel kuralları gösterir: beton (1. dosya), yumuşak kat ve kolon (2. dosya).
                case Flaw.BadConcrete:
                case Flaw.SoftStory:
                case Flaw.CutColumn: return 1;
                case Flaw.SandFoundation: return 3;
                default: return 4;
            }
        }

        public static string Warning(Flaw flaw)
        {
            switch (flaw)
            {
                case Flaw.BadConcrete: return "Beton C25, C30 ya da C35 değil";
                case Flaw.SoftStory: return "Yumuşak kat: perde ya da red";
                case Flaw.CutColumn: return "Kesik kolon: yalnız red";
                case Flaw.SandFoundation: return "Kum zemin, 5+ kat: kazık şart";
                default: return "Ağır hasarlı bina onarılmaz: red";
            }
        }

        /// <summary>Perde yalnızca tek kusuru yumuşak kat olan binayı kurtarır.</summary>
        public static bool ShearWallSaves(BuildingCase c)
        {
            var flaws = Flaws(c);
            return flaws.Count == 1 && flaws[0] == Flaw.SoftStory;
        }

        public static BuildingFate Fate(BuildingCase c, Seal seal)
        {
            switch (seal)
            {
                case Seal.Reject: return BuildingFate.NotBuilt;
                case Seal.ShearWall: return ShearWallSaves(c) ? BuildingFate.Standing : BuildingFate.Collapsed;
                default: return Flaws(c).Count == 0 ? BuildingFate.Standing : BuildingFate.Collapsed;
            }
        }

        public static Seal CorrectSeal(BuildingCase c)
        {
            var flaws = Flaws(c);
            if (flaws.Count == 0)
                return Seal.Approve;
            return ShearWallSaves(c) ? Seal.ShearWall : Seal.Reject;
        }

        /// <summary>
        /// Oyuncunun o güne kadar kitaba girmiş bir kuralı çiğneyip çiğnemediği.
        /// Çiğnediyse kısa uyarı metni, çiğnemediyse null.
        /// </summary>
        public static string KnownViolation(BuildingCase c, Seal seal, int day)
        {
            var flaws = Flaws(c);
            if (seal == Seal.Reject)
                return flaws.Count == 0 ? "Yönetmelikte red sebebi yok" : null;
            if (seal == Seal.ShearWall)
                return null;
            foreach (var flaw in flaws)
                if (RuleDay(flaw) <= day)
                    return Warning(flaw);
            return null;
        }

        /// <summary>Karnede yanlış mühürün kısa açıklaması: dosyanın kendi değerleriyle hangi kural neden geçerliydi.</summary>
        public static string Explain(BuildingCase c, Seal played)
        {
            var flaws = Flaws(c);
            if (flaws.Count == 0)
            {
                string why = "Yönetmelikte red sebebi yoktu.";
                if (c.claimedConcrete != c.labConcrete)
                    why += $" Künyede {BuildingCase.Concrete(c.claimedConcrete)} yazsa da ek rapor {BuildingCase.Concrete(c.labConcrete)} diyordu; ek rapor geçerli.";
                if (!string.IsNullOrEmpty(c.note))
                    why += $" \"{c.note}\" kitapta yazmayan bir kusur.";
                return why;
            }
            if (played == Seal.Reject && ShearWallSaves(c))
                return $"Yumuşak kat perdeyle kurtarılabilirdi: perde eklenir, aileler taşınır, sen de {Economy.ShearWallFee} TL alırdın.";
            var parts = new List<string>();
            foreach (var flaw in flaws)
                parts.Add(Explain(flaw, c));
            return string.Join(" ", parts);
        }

        static string Explain(Flaw flaw, BuildingCase c)
        {
            switch (flaw)
            {
                case Flaw.BadConcrete:
                    return c.hammerConcrete > 0 && c.hammerConcrete != c.labConcrete
                        ? $"Çekiç {BuildingCase.Concrete(c.hammerConcrete)} okudu; kâğıtlardaki {BuildingCase.Concrete(c.labConcrete)} geçersizdi."
                        : $"Gerçek beton {BuildingCase.Concrete(c.TrueConcrete)}: C25–C35 dışında.";
                case Flaw.SoftStory:
                    return $"Zemin kat market, tavan {c.groundFloorHeight.ToString("0.0#", new System.Globalization.CultureInfo("tr-TR"))} m, perde yok: yumuşak kat.";
                case Flaw.CutColumn:
                    return "Kolon kesik: perde de kurtarmaz, yalnız red.";
                case Flaw.SandFoundation:
                    return $"Kum zemin, {c.floors} kat, radye temel: kazık şarttı.";
                default:
                    return "Hasar tespiti ağır (kolonda çapraz çatlak): bu binaya onarım izni verilmez, boşaltılır.";
            }
        }

        public static int Fee(BuildingCase c, Seal seal, int day)
        {
            if (day > Economy.LastPaidDay)
                return 0;
            switch (seal)
            {
                case Seal.Approve: return c.approveFeeOverride > 0 ? c.approveFeeOverride : Economy.ApproveFee;
                case Seal.ShearWall: return Economy.ShearWallFee;
                default: return 0;
            }
        }
    }
}
