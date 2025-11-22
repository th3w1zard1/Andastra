using System;
using System.Collections.Generic;
using System.Linq;

namespace TSLPatcher.Core.Resources;

/// <summary>
/// Represents a resource type that is used within either KOTOR game.
/// </summary>
public class ResourceType
{
    private static readonly Dictionary<int, ResourceType> _byTypeId = new();
    private static readonly Dictionary<string, ResourceType> _byExtension = new();

    public int TypeId { get; }
    public string Extension { get; }
    public string Category { get; }
    public string Contents { get; }
    public bool IsInvalid { get; }

    private ResourceType(int typeId, string extension, string category, string contents, bool isInvalid = false)
    {
        TypeId = typeId;
        Extension = extension.Trim().ToLower();
        Category = category;
        Contents = contents;
        IsInvalid = isInvalid;

        if (!isInvalid)
        {
            _byTypeId[typeId] = this;
            _byExtension[Extension] = this;
        }
    }

    public bool IsGff() => Contents == "gff";

    public override string ToString() => Extension.ToUpper();

    public override int GetHashCode() => TypeId;

    public override bool Equals(object? obj)
    {
        if (obj is ResourceType other)
        {
            return TypeId == other.TypeId;
        }

        return false;
    }

    public static ResourceType FromExtension(string extension)
    {
        string ext = extension.TrimStart('.').ToLower();
        return _byExtension.TryGetValue(ext, out var type)
            ? type
            : new ResourceType(-1, ext, "Unknown", "binary", isInvalid: true);
    }

    public static ResourceType FromTypeId(int typeId)
    {
        return _byTypeId.TryGetValue(typeId, out var type)
            ? type
            : new ResourceType(typeId, "", "Unknown", "binary", isInvalid: true);
    }

    /// <summary>
    /// Alias for FromTypeId to match Python API.
    /// </summary>
    public static ResourceType FromId(int typeId) => FromTypeId(typeId);

    // Common resource types used in KOTOR
    public static readonly ResourceType INVALID = new(-1, "", "Undefined", "binary", isInvalid: true);
    public static readonly ResourceType RES = new(0, "res", "Save Data", "gff");
    public static readonly ResourceType BMP = new(1, "bmp", "Images", "binary");
    public static readonly ResourceType TGA = new(3, "tga", "Textures", "binary");
    public static readonly ResourceType WAV = new(4, "wav", "Audio", "binary");
    public static readonly ResourceType PLT = new(6, "plt", "Other", "binary");
    public static readonly ResourceType INI = new(7, "ini", "Text Files", "plaintext");
    public static readonly ResourceType TXT = new(10, "txt", "Text Files", "plaintext");
    public static readonly ResourceType MDL = new(2002, "mdl", "Models", "binary");
    public static readonly ResourceType NSS = new(2009, "nss", "Scripts", "plaintext");
    public static readonly ResourceType NCS = new(2010, "ncs", "Scripts", "binary");
    public static readonly ResourceType ARE = new(2012, "are", "Area", "gff");
    public static readonly ResourceType IFO = new(2014, "ifo", "Module Info", "gff");
    public static readonly ResourceType DLG = new(2029, "dlg", "Dialogue", "gff");
    public static readonly ResourceType ITP = new(2030, "itp", "Palette", "gff");
    public static readonly ResourceType UTC = new(2027, "utc", "Creature", "gff");
    public static readonly ResourceType UTD = new(2028, "utd", "Door", "gff");
    public static readonly ResourceType UTE = new(2032, "ute", "Encounter", "gff");
    public static readonly ResourceType UTI = new(2025, "uti", "Item", "gff");
    public static readonly ResourceType UTM = new(2030, "utm", "Merchant", "gff");
    public static readonly ResourceType UTP = new(2023, "utp", "Placeable", "gff");
    public static readonly ResourceType UTS = new(2035, "uts", "Sound", "gff");
    public static readonly ResourceType UTT = new(2036, "utt", "Trigger", "gff");
    public static readonly ResourceType UTW = new(2002, "utw", "Waypoint", "gff");
    public static readonly ResourceType GIT = new(2037, "git", "Area Instance", "gff");
    public static readonly ResourceType GUI = new(2047, "gui", "GUI", "gff");
    public static readonly ResourceType TwoDA = new(2017, "2da", "2D Arrays", "plaintext");
    public static readonly ResourceType TLK = new(2018, "tlk", "Talk Tables", "binary");
    public static readonly ResourceType TPC = new(2022, "tpc", "Textures", "binary");
    public static readonly ResourceType ERF = new(2001, "erf", "Capsules", "binary");
    public static readonly ResourceType MOD = new(2005, "mod", "Modules", "binary");
    public static readonly ResourceType SAV = new(2006, "sav", "Saves", "binary");
    public static readonly ResourceType RIM = new(2007, "rim", "RIM", "binary");
    public static readonly ResourceType SSF = new(2008, "ssf", "Sound Set", "binary");
    public static readonly ResourceType LIP = new(2053, "lip", "Lip Sync", "binary");
}

