using System.Collections.Generic;

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
