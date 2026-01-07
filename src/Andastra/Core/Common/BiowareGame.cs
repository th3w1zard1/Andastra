namespace Andastra.Core.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:250-285
    // Original: class BioWareGame(IntEnum):
    // Extended to support all BioWare engine games: Odyssey (KOTOR), Aurora (NWN), Eclipse (DA/ME), Infinity (BG/IWD/PST)
    // This enum is kept in Andastra.Parsing for backward compatibility with patcher tools (HoloPatcher.NET, HolocronToolset, NCSDecomp, KotorDiff)
    /// <summary>
    /// Represents which BioWare engine BioWareGame / platform variant.
    /// </summary>
    public enum BioWareGame
    {
        // Odyssey Engine
        K1 = 1,
        K2 = 2,
        TSL = 2,
        K1_XBOX = 3,
        K2_XBOX = 4,
        TSL_XBOX = K2_XBOX,
        K1_IOS = 5,
        K2_IOS = 6,
        TSL_IOS = K2_IOS,
        K1_ANDROID = 7,
        K2_ANDROID = 8,
        TSL_ANDROID = K2_ANDROID,

        // Eclipse Engine
        DA = 10,
        DAO = DA,
        DA_ORIGINS = DA,
        DA2 = 11,
        DRAGON_AGE_2 = DA2,
        
        // Mass Effect series (Eclipse Engine)
        ME = 12,
        ME1 = ME,
        MASS_EFFECT = ME,
        ME2 = 13,
        MASS_EFFECT_2 = ME2,
        ME3 = 14,
        MASS_EFFECT_3 = ME3,

        // Aurora Engine
        NWN = 30,
        NEVERWINTER_NIGHTS = NWN,
        NWN2 = 31,
        NEVERWINTER_NIGHTS_2 = NWN2,

        // Infinity Engine
        BG1 = 40,
        BALDURS_GATE = BG1,
        BG2 = 41,
        BALDURS_GATE_2 = BG2,
        IWD = 42,
        ICEWIND_DALE = IWD,
        IWD2 = 43,
        ICEWIND_DALE_2 = IWD2,
        PST = 44,
        PLANESCAPE_TORMENT = PST
    }

    public static class GameExtensions
    {
        public static bool IsK1(this BioWareGame BioWareGame)
        {
            return ((int)BioWareGame) % 2 != 0 && BioWareGame >= BioWareGame.K1 && BioWareGame <= BioWareGame.K2_ANDROID;
        }

        public static bool IsK2(this BioWareGame BioWareGame)
        {
            return ((int)BioWareGame) % 2 == 0 && BioWareGame >= BioWareGame.K1 && BioWareGame <= BioWareGame.K2_ANDROID;
        }

        public static bool IsTSL(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K2;
        }

        public static bool IsXbox(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K1_XBOX || BioWareGame == BioWareGame.K2_XBOX;
        }

        public static bool IsPc(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K1 || BioWareGame == BioWareGame.K2;
        }

        public static bool IsMobile(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K1_IOS || BioWareGame == BioWareGame.K2_IOS || BioWareGame == BioWareGame.K1_ANDROID || BioWareGame == BioWareGame.K2_ANDROID;
        }

        public static bool IsAndroid(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K1_ANDROID || BioWareGame == BioWareGame.K2_ANDROID;
        }

        public static bool IsIOS(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.K1_IOS || BioWareGame == BioWareGame.K2_IOS;
        }

        // Eclipse Engine
        public static bool IsDragonAge(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.DA || BioWareGame == BioWareGame.DA_ORIGINS || BioWareGame == BioWareGame.DA2 || BioWareGame == BioWareGame.DRAGON_AGE_2;
        }

        public static bool IsDragonAgeOrigins(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.DA || BioWareGame == BioWareGame.DA_ORIGINS;
        }

        public static bool IsDragonAge2(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.DA2 || BioWareGame == BioWareGame.DRAGON_AGE_2;
        }

        public static bool IsMassEffect(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.ME || BioWareGame == BioWareGame.ME1 || BioWareGame == BioWareGame.MASS_EFFECT ||
                   BioWareGame == BioWareGame.ME2 || BioWareGame == BioWareGame.MASS_EFFECT_2 ||
                   BioWareGame == BioWareGame.ME3 || BioWareGame == BioWareGame.MASS_EFFECT_3;
        }

        public static bool IsME1(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.ME || BioWareGame == BioWareGame.ME1 || BioWareGame == BioWareGame.MASS_EFFECT;
        }

        public static bool IsME2(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.ME2 || BioWareGame == BioWareGame.MASS_EFFECT_2;
        }

        public static bool IsME3(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.ME3 || BioWareGame == BioWareGame.MASS_EFFECT_3;
        }

        // Aurora Engine
        public static bool IsNeverwinterNights(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.NWN || BioWareGame == BioWareGame.NEVERWINTER_NIGHTS ||
                   BioWareGame == BioWareGame.NWN2 || BioWareGame == BioWareGame.NEVERWINTER_NIGHTS_2;
        }

        public static bool IsNWN1(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.NWN || BioWareGame == BioWareGame.NEVERWINTER_NIGHTS;
        }

        public static bool IsNWN2(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.NWN2 || BioWareGame == BioWareGame.NEVERWINTER_NIGHTS_2;
        }

        // Engine family checks
        public static bool IsOdyssey(this BioWareGame BioWareGame)
        {
            return BioWareGame >= BioWareGame.K1 && BioWareGame <= BioWareGame.K2_ANDROID;
        }

        public static bool IsEclipse(this BioWareGame BioWareGame)
        {
            return IsDragonAge(BioWareGame) || IsMassEffect(BioWareGame);
        }

        public static bool IsAurora(this BioWareGame BioWareGame)
        {
            return IsNeverwinterNights(BioWareGame);
        }

        // Infinity Engine
        public static bool IsBaldursGate(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.BG1 || BioWareGame == BioWareGame.BALDURS_GATE ||
                   BioWareGame == BioWareGame.BG2 || BioWareGame == BioWareGame.BALDURS_GATE_2;
        }

        public static bool IsBaldursGate1(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.BG1 || BioWareGame == BioWareGame.BALDURS_GATE;
        }

        public static bool IsBaldursGate2(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.BG2 || BioWareGame == BioWareGame.BALDURS_GATE_2;
        }

        public static bool IsIcewindDale(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.IWD || BioWareGame == BioWareGame.ICEWIND_DALE ||
                   BioWareGame == BioWareGame.IWD2 || BioWareGame == BioWareGame.ICEWIND_DALE_2;
        }

        public static bool IsIcewindDale1(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.IWD || BioWareGame == BioWareGame.ICEWIND_DALE;
        }

        public static bool IsIcewindDale2(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.IWD2 || BioWareGame == BioWareGame.ICEWIND_DALE_2;
        }

        public static bool IsPlanescapeTorment(this BioWareGame BioWareGame)
        {
            return BioWareGame == BioWareGame.PST || BioWareGame == BioWareGame.PLANESCAPE_TORMENT;
        }

        public static bool IsInfinity(this BioWareGame BioWareGame)
        {
            return BioWareGame >= BioWareGame.BG1 && BioWareGame <= BioWareGame.PLANESCAPE_TORMENT;
        }
    }
}
