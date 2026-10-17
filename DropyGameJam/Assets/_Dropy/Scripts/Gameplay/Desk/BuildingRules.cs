using System.Collections.Generic;
using UnityEngine;

namespace Dropy.Gameplay
{
    public enum Flaw
    {
        BadConcrete,
        SoftStory,
        CutColumn,
        SandFoundation
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
    /// Kilitli tasarım: 5 gün, günde 3 dosya, sabit fiyatlar. Rakamlar dürüst tam yolun
    /// kasayı tam 0'da bitirmesine göre ayarlıdır; değiştirmeden önce o hesabı yeniden yap.
    /// </summary>
    public static class Economy
    {
        public const int Days = 5;
        public const int FilesPerDay = 3;
        public const int StartMoney = 1000;
        public const int ApproveFee = 1000;
        public const int ShearWallFee = 500;
        public const int Rent = 1000;
        /// <summary>Kira 1–4. akşam düşer; dükkân 4. günün sonunda kapanır; 5. gün mühür para vermez.</summary>
        public const int LastPaidDay = 4;

        /// <summary>
        /// Müfettiş tutanağı: kitapta yazan bir kuralı çiğneyen onay. Her tutanakta o dosyanın normal harcı
        /// geri alınır (ilki yalnızca budur: uyarı); ikinciden itibaren üstüne 1.000, 2.000, 3.000... ceza eklenir.
        /// Böylece tek hata kâr getirmez, toptan onaylamak dürüst oyundan geride kalır, rüşvetin fazlası
        /// (4.000 − 1.000) ise cepte kalır. Dürüst yol tutanak yemez, hesabı değişmez. Akşam kasadan düşer.
        /// </summary>
        public const int CitationStep = 1000;

        /// <param name="citationNumber">Bu kaçıncı tutanak (1'den başlar).</param>
        /// <param name="feeTaken">Bu onaydan alınan para (rüşvette 4.000; iade yalnız normal harç kadar).</param>
        public static int CitationFine(int citationNumber, int feeTaken) =>
            Mathf.Min(feeTaken, ApproveFee) + CitationStep * (citationNumber - 1);

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
                default: return 4000;
            }
        }

        public static string Name(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Bag: return "Deprem çantası";
                case ShopItem.Fixing: return "Eşya sabitleme";
                default: return "Apartman güçlendirme";
            }
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
                default: return 3;
            }
        }

        public static string Warning(Flaw flaw)
        {
            switch (flaw)
            {
                case Flaw.BadConcrete: return "Beton C25, C30 ya da C35 değil";
                case Flaw.SoftStory: return "Yumuşak kat: perde ya da red";
                case Flaw.CutColumn: return "Kesik kolon: yalnız red";
                default: return "Kum zemin, 5+ kat: kazık şart";
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
                return "Yumuşak kat perdeyle kurtarılabilirdi: bina yapılır, sen de 500 TL alırdın.";
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
                default:
                    return $"Kum zemin, {c.floors} kat, radye temel: kazık şarttı.";
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
