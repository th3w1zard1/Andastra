namespace AuroraEngine.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:250-285
    // Original: class Game(IntEnum):
    /// <summary>
    /// Represents which KOTOR game / platform variant.
    /// </summary>
    public enum Game
    {
        K1 = 1,
        K2 = 2,
        K1_XBOX = 3,
        K2_XBOX = 4,
        K1_IOS = 5,
        K2_IOS = 6,
        K1_ANDROID = 7,
        K2_ANDROID = 8,
        TSL = K2
    }

    public static class GameExtensions
    {
        public static bool IsK1(this Game game)
        {
            return ((int)game) % 2 != 0;
        }

        public static bool IsK2(this Game game)
        {
            return ((int)game) % 2 == 0;
        }

        public static bool IsTSL(this Game game)
        {
            return game == Game.K2;
        }

        public static bool IsXbox(this Game game)
        {
            return game == Game.K1_XBOX || game == Game.K2_XBOX;
        }

        public static bool IsPc(this Game game)
        {
            return game == Game.K1 || game == Game.K2;
        }

        public static bool IsMobile(this Game game)
        {
            return game == Game.K1_IOS || game == Game.K2_IOS || game == Game.K1_ANDROID || game == Game.K2_ANDROID;
        }

        public static bool IsAndroid(this Game game)
        {
            return game == Game.K1_ANDROID || game == Game.K2_ANDROID;
        }

        public static bool IsIOS(this Game game)
        {
            return game == Game.K1_IOS || game == Game.K2_IOS;
        }
    }
}
