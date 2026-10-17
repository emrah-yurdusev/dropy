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
    /// Döngü: 2 bölüm × 3 gün, günde 3 dosya (18 dosya). 3. ve 6. günün son dosyasından sonra deprem olur.
    /// Mühür iskândır: onaylanan binaya aileler taşınır, reddedilen bina boş kalır.
    /// Rakamlar dürüst tam yola göre ayarlıdır: 1–5. günlerin harcı, kira düşünce çanta + sabitleme + ev onarımına
    /// (500 + 1.500 + 4.500) yeter ve aileye tek bir küçük istek ödeyecek kadar (500 TL) artar.
    /// Değiştirmeden önce o hesabı yeniden yap (üretici script denetler).
    /// Yanlış onaya para cezası yok: depremde onayladığın bina çökerse hapse girersin. Kasa eksiye düşebilir (borç).
    /// </summary>
    public static class Economy
    {
        public const int Days = 6;
        public const int FilesPerDay = 3;
        public const int StartMoney = 0;
        public const int ApproveFee = 1500;
        public const int ShearWallFee = 1000;
        public const int Rent = 1000;
        /// <summary>Kira 1–5. akşam düşer; dükkân 5. günün sonunda kapanır; 6. gün (deprem günü) mühür para vermez.</summary>
        public const int LastPaidDay = 5;
        /// <summary>Bu günlerin son dosyasından sonra deprem olur.</summary>
        public static readonly int[] QuakeDays = { 3, 6 };

        public static bool IsQuakeDay(int day) => System.Array.IndexOf(QuakeDays, day) >= 0;

        /// <summary>Çekiç 3. günden itibaren gelir ve günde bu kadar kullanılabilir; hangi dosyanın ölçüleceği oyuncunun kararı.</summary>
        public const int HammerDay = 3;
        public const int HammerPerDay = 2;

        public static int Day(int fileIndex) => fileIndex / FilesPerDay + 1;
    }

    public enum ShopItem
    {
        Bag,
        Fixing,
        Retrofit
    }

    public static class Shop
    {
        public static readonly ShopItem[] Items = { ShopItem.Bag, ShopItem.Fixing, ShopItem.Retrofit };

        public static int Price(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Bag: return 500;
                case ShopItem.Fixing: return 1500;
                default: return 4500;
            }
        }

        public static string Name(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Bag: return "Deprem çantası";
                case ShopItem.Fixing: return "Eşya sabitleme";
                default: return "Ev onarımı";
            }
        }

        /// <summary>Ev onarımı ancak ilk depremde ev hasar aldıktan sonra (o akşamdan itibaren) satılır.</summary>
        public static int AvailableFrom(ShopItem item) => item == ShopItem.Retrofit ? Economy.QuakeDays[0] : 1;

        /// <summary>Sabitlemenin işi ilk depremde biter: o akşamdan sonra dükkândan kalkar (geç alınıp "işe yaradı" sanılmasın).</summary>
        public static int AvailableUntil(ShopItem item) => item == ShopItem.Fixing ? Economy.QuakeDays[0] - 1 : Economy.LastPaidDay;

        public static bool Offered(ShopItem item, int day) => day >= AvailableFrom(item) && day <= AvailableUntil(item);
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
                case Flaw.BadConcrete: return 1;
                case Flaw.SoftStory:
                case Flaw.CutColumn: return 2;
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
