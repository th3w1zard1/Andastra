using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.NSS;

namespace TSLPatcher.Core.Mods.GFF;

/// <summary>
/// Abstract base for field values that can be constants or memory references.
/// 1:1 port from Python FieldValue in pykotor/tslpatcher/mods/gff.py
/// </summary>
public abstract class FieldValue
{
    public abstract object Value(PatcherMemory memory, GFFFieldType fieldType);

    protected object Validate(object value, GFFFieldType fieldType)
    {
        // Handle ResRef type
        if (fieldType == GFFFieldType.ResRef && value is not ResRef)
        {
            if (value is string strValue)
            {
                return string.IsNullOrWhiteSpace(strValue) ? ResRef.FromBlank() : new ResRef(strValue);
            }
            return new ResRef(value.ToString() ?? "");
        }

        // Handle String type
        if (fieldType == GFFFieldType.String && value is not string)
        {
            return value.ToString() ?? "";
        }

        // Handle integer types
        Type returnType = GetReturnType(fieldType);
        if (returnType == typeof(int) && value is string strVal)
        {
            return string.IsNullOrWhiteSpace(strVal) ? 0 : int.Parse(strVal);
        }

        // Handle float types
        if (returnType == typeof(float) && value is string floatStr)
        {
            return string.IsNullOrWhiteSpace(floatStr) ? 0f : float.Parse(floatStr);
        }

        if (returnType == typeof(double) && value is string doubleStr)
        {
            return string.IsNullOrWhiteSpace(doubleStr) ? 0.0 : double.Parse(doubleStr);
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
        if (!memory.Memory2DA.ContainsKey(TokenId))
        {
            throw new Common.KeyError($"2DAMEMORY{TokenId} was not defined before use");
        }
        return Validate(memory.Memory2DA[TokenId], fieldType);
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
        if (!memory.MemoryStr.ContainsKey(TokenId))
        {
            throw new Common.KeyError($"StrRef{TokenId} was not defined before use!");
        }
        return Validate(memory.MemoryStr[TokenId], fieldType);
    }
}

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

    public void Apply(LocalizedString locstring, PatcherMemory memory)
    {
        if (StringRef != null)
        {
            locstring.StringRef = (int)StringRef.Value(memory, GFFFieldType.UInt32);
        }

        // Copy all language/gender/text entries
        foreach (var (language, gender, text) in this)
        {
            locstring.SetData(language, gender, text);
        }
    }

    public override string ToString()
    {
        return $"LocalizedString(stringref={StringRef})";
    }
}

