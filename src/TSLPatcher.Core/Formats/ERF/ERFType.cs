namespace TSLPatcher.Core.Formats.ERF;

/// <summary>
/// The type of ERF/capsule file.
/// </summary>
public enum ERFType
{
    /// <summary>
    /// Standard ERF archive (ERF)
    /// </summary>
    ERF,

    /// <summary>
    /// Module file (MOD) or Save file (SAV) - same format
    /// </summary>
    MOD
}

public static class ERFTypeExtensions
{
    public static string ToFourCC(this ERFType type)
    {
        return type switch
        {
            ERFType.ERF => "ERF ",
            ERFType.MOD => "MOD ",
            _ => "ERF "
        };
    }

    public static ERFType FromFourCC(string fourCC)
    {
        return fourCC?.Trim() switch
        {
            "ERF" => ERFType.ERF,
            "MOD" => ERFType.MOD,
            _ => ERFType.ERF
        };
    }

    public static ERFType FromExtension(string extension)
    {
        string ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "erf" => ERFType.ERF,
            "mod" => ERFType.MOD,
            "sav" => ERFType.MOD, // SAV files use MOD format
            _ => throw new System.ArgumentException($"Invalid ERF extension: {extension}")
        };
    }
}

