using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Formats.GFF;

/// <summary>
/// Stores a collection of GFF fields.
/// </summary>
public class GFFStruct(int structId = 0) : IEnumerable<(string label, GFFFieldType fieldType, object value)>
{
    public int StructId { get; set; } = structId;

    private readonly Dictionary<string, GFFField> _fields = new();

    public int Count => _fields.Count;

    public bool Exists(string label) => _fields.ContainsKey(label);

    public void Remove(string label)
    {
        _fields.Remove(label);
    }

    public GFFFieldType? GetFieldType(string label)
    {
        return _fields.TryGetValue(label, out var field) ? field.FieldType : null;
    }

    public bool TryGetFieldType(string label, out GFFFieldType fieldType)
    {
        if (_fields.TryGetValue(label, out var field))
        {
            fieldType = field.FieldType;
            return true;
        }
        fieldType = default;
        return false;
    }

    public object? GetValue(string label, object? defaultValue = null)
    {
        return _fields.TryGetValue(label, out var field) ? field.Value : defaultValue;
    }

    public T? Acquire<T>(string label, T? defaultValue = default)
    {
        if (!_fields.TryGetValue(label, out var field))
        {
            return defaultValue;
        }

        return field.Value is T value ? value : defaultValue;
    }

    // Getters for specific types
    public byte GetUInt8(string label) => Convert.ToByte(GetValue(label, (byte)0));
    public sbyte GetInt8(string label) => Convert.ToSByte(GetValue(label, (sbyte)0));
    public ushort GetUInt16(string label) => Convert.ToUInt16(GetValue(label, (ushort)0));
    public short GetInt16(string label) => Convert.ToInt16(GetValue(label, (short)0));
    public uint GetUInt32(string label) => Convert.ToUInt32(GetValue(label, 0u));
    public int GetInt32(string label) => Convert.ToInt32(GetValue(label, 0));
    public ulong GetUInt64(string label) => Convert.ToUInt64(GetValue(label, 0ul));
    public long GetInt64(string label) => Convert.ToInt64(GetValue(label, 0L));
    public float GetSingle(string label) => Convert.ToSingle(GetValue(label, 0f));
    public double GetDouble(string label) => Convert.ToDouble(GetValue(label, 0.0));
    public string GetString(string label) => GetValue(label)?.ToString() ?? string.Empty;
    public ResRef GetResRef(string label) => GetValue(label) as ResRef ?? ResRef.FromBlank();
    public LocalizedString GetLocString(string label) => GetValue(label) as LocalizedString ?? LocalizedString.FromInvalid();
    public byte[] GetBinary(string label) => GetValue(label) as byte[] ?? Array.Empty<byte>();
    public Vector3 GetVector3(string label) => (Vector3)(GetValue(label) ?? new Vector3());
    public Vector4 GetVector4(string label) => (Vector4)(GetValue(label) ?? new Vector4());
    public GFFStruct GetStruct(string label) => GetValue(label) as GFFStruct ?? new GFFStruct();
    public GFFList GetList(string label) => GetValue(label) as GFFList ?? new GFFList();

    public bool TryGetLocString(string label, out LocalizedString? value)
    {
        if (_fields.TryGetValue(label, out var field) && field.Value is LocalizedString locString)
        {
            value = locString;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetStruct(string label, out GFFStruct? value)
    {
        if (_fields.TryGetValue(label, out var field) && field.Value is GFFStruct gffStruct)
        {
            value = gffStruct;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetList(string label, out GFFList? value)
    {
        if (_fields.TryGetValue(label, out var field) && field.Value is GFFList gffList)
        {
            value = gffList;
            return true;
        }
        value = null;
        return false;
    }

    // Setters for specific types
    public void SetUInt8(string label, byte value) => SetField(label, GFFFieldType.UInt8, value);
    public void SetInt8(string label, sbyte value) => SetField(label, GFFFieldType.Int8, value);
    public void SetUInt16(string label, ushort value) => SetField(label, GFFFieldType.UInt16, value);
    public void SetInt16(string label, short value) => SetField(label, GFFFieldType.Int16, value);
    public void SetUInt32(string label, uint value) => SetField(label, GFFFieldType.UInt32, value);
    public void SetInt32(string label, int value) => SetField(label, GFFFieldType.Int32, value);
    public void SetUInt64(string label, ulong value) => SetField(label, GFFFieldType.UInt64, value);
    public void SetInt64(string label, long value) => SetField(label, GFFFieldType.Int64, value);
    public void SetSingle(string label, float value) => SetField(label, GFFFieldType.Single, value);
    public void SetDouble(string label, double value) => SetField(label, GFFFieldType.Double, value);
    public void SetString(string label, string value) => SetField(label, GFFFieldType.String, value);
    public void SetResRef(string label, ResRef value) => SetField(label, GFFFieldType.ResRef, value);
    public void SetLocString(string label, LocalizedString value) => SetField(label, GFFFieldType.LocalizedString, value);
    public void SetBinary(string label, byte[] value) => SetField(label, GFFFieldType.Binary, value);
    public void SetVector3(string label, Vector3 value) => SetField(label, GFFFieldType.Vector3, value);
    public void SetVector4(string label, Vector4 value) => SetField(label, GFFFieldType.Vector4, value);
    public void SetStruct(string label, GFFStruct value) => SetField(label, GFFFieldType.Struct, value);
    public void SetList(string label, GFFList value) => SetField(label, GFFFieldType.List, value);

    public void SetField(string label, GFFFieldType fieldType, object value)
    {
        _fields[label] = new GFFField(fieldType, value);
    }

    public IEnumerator<(string label, GFFFieldType fieldType, object value)> GetEnumerator()
    {
        foreach (var (label, field) in _fields)
        {
            yield return (label, field.FieldType, field.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public object this[string label]
    {
        get => GetValue(label) ?? throw new KeyError(label, "Field does not exist in this GFFStruct");
        set
        {
            if (_fields.TryGetValue(label, out var field))
            {
                _fields[label] = new GFFField(field.FieldType, value);
            }
            else
            {
                throw new KeyError(label, "Cannot set field that doesn't exist. Use Set* methods to create fields.");
            }
        }
    }

    /// <summary>
    /// Internal field storage class
    /// </summary>
    private class GFFField
    {
        public GFFFieldType FieldType { get; }
        public object Value { get; }

        public GFFField(GFFFieldType fieldType, object value)
        {
            FieldType = fieldType;
            Value = value;
        }
    }
}

public class KeyError : Exception
{
    public KeyError(string key, string message) : base($"Key '{key}': {message}") { }
}

