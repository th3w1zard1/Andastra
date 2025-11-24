using System;
using System.IO;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.GFF;

/// <summary>
/// GFF modification algorithms for TSLPatcher/HoloPatcher.
/// 
/// This module implements GFF field modification logic for applying patches from changes.ini files.
/// Handles field additions, modifications, list operations, and struct manipulations.
/// 
/// References:
/// ----------
///     vendor/TSLPatcher/TSLPatcher.pl - Original Perl GFF modification logic (broken and unfinished)
///     vendor/Kotor.NET/Kotor.NET.Patcher/ - Incomplete C# patcher
/// </summary>

/// <summary>
/// Localized string with delta changes.
/// 1:1 port from Python LocalizedStringDelta
/// </summary>
public class LocalizedStringDelta : LocalizedString
{
    public new FieldValue? StringRef { get; set; }

    public LocalizedStringDelta(FieldValue? stringref = null) : base(0)
    {
        StringRef = stringref;
    }

    public override string ToString()
    {
        return $"LocalizedString(stringref={StringRef})";
    }

    /// <summary>
    /// Applies a LocalizedString patch to a LocalizedString object.
    /// 
    /// Args:
    /// ----
    ///     locstring: LocalizedString object to apply patch to
    ///     memory: PatcherMemory object for resolving references
    /// 
    /// Processing Logic:
    /// ----------------
    ///     - Checks if stringref is set and sets locstring stringref if so
    ///     - Iterates through tuple returned from function and sets language, gender and text on locstring.
    /// </summary>
    public void Apply(LocalizedString locstring, PatcherMemory memory)
    {
        if (StringRef != null)
        {
            locstring.StringRef = (int)StringRef.Value(memory, GFFFieldType.UInt32);
        }
        foreach ((Language language, Gender gender, string text) in this)
        {
            locstring.SetData(language, gender, text);
        }
    }
}

#region Value Returners

/// <summary>
/// Abstract base for field values that can be constants or memory references.
/// 1:1 port from Python FieldValue in pykotor/tslpatcher/mods/gff.py
/// </summary>
public abstract class FieldValue
{
    public abstract object Value(PatcherMemory memory, GFFFieldType fieldType);

    /// <summary>
    /// Validate a value based on its field type.
    /// 
    /// Args:
    /// ----
    ///     value: The value to validate
    ///     field_type: The field type to validate against
    /// 
    /// Returns:
    /// -------
    ///     value: The validated value
    /// 
    /// Processing Logic:
    /// ----------------
    ///     - Check if value matches field type
    ///     - Convert value to expected type if needed
    ///     - Return validated value
    /// </summary>
    protected static object Validate(object value, GFFFieldType fieldType)
    {
        // !FieldPath - In Python this checks for PureWindowsPath, in C# we check for path-like strings
        if (value is string strValue && (strValue.Contains('/') || strValue.Contains('\\')))
        {
            return strValue;
        }
        if (fieldType == GFFFieldType.ResRef && value is not ResRef)
        {
            // This is here to support empty statements like 'resref=' in ini (allow_no_entries=True in configparser)
            if (value is string resRefStr)
            {
                return string.IsNullOrWhiteSpace(resRefStr) ? ResRef.FromBlank() : new ResRef(resRefStr);
            }
            return new ResRef(value.ToString() ?? "");
        }
        else if (fieldType == GFFFieldType.String && value is not string)
        {
            return value.ToString() ?? "";
        }
        else if (GetReturnType(fieldType) == typeof(int) && value is string intStr)
        {
            // Python: int(value) if value.strip() else "0"
            return string.IsNullOrWhiteSpace(intStr) ? 0 : int.Parse(intStr);
        }
        else if (GetReturnType(fieldType) == typeof(float) && value is string floatStr)
        {
            // Python: float(value) if value.strip() else "0.0"
            return string.IsNullOrWhiteSpace(floatStr) ? 0f : float.Parse(floatStr);
        }
        return value;
    }

    private static Type GetReturnType(GFFFieldType fieldType)
    {
        return fieldType switch
        {
            GFFFieldType.UInt8 or GFFFieldType.Int8 or
            GFFFieldType.UInt16 or GFFFieldType.Int16 or
            GFFFieldType.UInt32 or GFFFieldType.Int32 => typeof(int),
            GFFFieldType.UInt64 or GFFFieldType.Int64 => typeof(long),
            GFFFieldType.Single => typeof(float),
            GFFFieldType.Double => typeof(double),
            GFFFieldType.String => typeof(string),
            GFFFieldType.ResRef => typeof(ResRef),
            _ => typeof(object)
        };
    }
}

/// <summary>
/// Field value that stores a constant.
/// 1:1 port from Python FieldValueConstant
/// </summary>
public class FieldValueConstant : FieldValue
{
    private readonly object _stored;

    public object Stored => _stored;

    public FieldValueConstant(object value)
    {
        _stored = value;
    }

    public override object Value(PatcherMemory memory, GFFFieldType fieldType)
    {
        return Validate(_stored, fieldType);
    }
}

/// <summary>
/// Field value that can be "listindex" or a constant.
/// 1:1 port from Python FieldValueListIndex
/// </summary>
public class FieldValueListIndex : FieldValueConstant
{
    private readonly object _stored;

    public FieldValueListIndex(object value) : base(value)
    {
        _stored = value;
    }

    public override object Value(PatcherMemory memory, GFFFieldType fieldType)
    {
        if (_stored is string str && str == "listindex")
        {
            return "listindex";
        }
        return Validate(_stored, fieldType);
    }
}

/// <summary>
/// Field value from 2DA memory.
/// 1:1 port from Python FieldValue2DAMemory
/// </summary>
public class FieldValue2DAMemory : FieldValue
{
    public int TokenId { get; }

    public FieldValue2DAMemory(int tokenId)
    {
        TokenId = tokenId;
    }

    public override object Value(PatcherMemory memory, GFFFieldType fieldType)
    {
        // Python: memory_val: str | PureWindowsPath | None = memory.memory_2da.get(self.token_id, None)
        // In C#, Memory2DA is Dictionary<int, string> - paths are stored as strings
        if (!memory.Memory2DA.TryGetValue(TokenId, out string? memoryVal))
        {
            throw new Common.KeyError($"2DAMEMORY{TokenId} was not defined before use");
        }
        // In C#, memory values are stored as strings (paths are string values containing '/' or '\')
        return Validate(memoryVal, fieldType);
    }
}

/// <summary>
/// Field value from TLK memory.
/// 1:1 port from Python FieldValueTLKMemory
/// </summary>
public class FieldValueTLKMemory : FieldValue
{
    public int TokenId { get; }

    public FieldValueTLKMemory(int tokenId)
    {
        TokenId = tokenId;
    }

    public override object Value(PatcherMemory memory, GFFFieldType fieldType)
    {
        if (!memory.MemoryStr.TryGetValue(TokenId, out int memoryVal))
        {
            throw new Common.KeyError($"StrRef{TokenId} was not defined before use!");
        }
        return Validate(memoryVal, fieldType);
    }
}

#endregion
