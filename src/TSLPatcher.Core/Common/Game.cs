namespace TSLPatcher.Core.Common;

/// <summary>
/// Represents which KOTOR game the installation is for.
/// </summary>
public enum Game
{
    /// <summary>
    /// Knights of the Old Republic (KOTOR 1)
    /// </summary>
    K1,

    /// <summary>
    /// Knights of the Old Republic II: The Sith Lords (KOTOR 2 / TSL)
    /// </summary>
    K2,

    /// <summary>
    /// Alias for K2 - The Sith Lords
    /// </summary>
    TSL = K2
}

public static class GameExtensions
{
    public static bool IsK1(this Game game) => game == Game.K1;
    public static bool IsK2(this Game game) => game == Game.K2;
    public static bool IsTSL(this Game game) => game == Game.K2;

    /// <summary>
    /// Checks if this is an iOS version of the game.
    /// Note: Currently not supported, but included for future compatibility.
    /// </summary>
    public static bool IsIOS(this Game game) => false; // Not currently supported
}

