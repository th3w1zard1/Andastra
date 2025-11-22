using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Resources;

namespace TSLPatcher.Core.Formats.ERF;

/// <summary>
/// Represents the data of an ERF file.
/// </summary>
public class ERF : IEnumerable<ERFResource>
{
    private readonly List<ERFResource> _resources = new();
    private readonly Dictionary<ResourceIdentifier, ERFResource> _resourceDict = new();

    public ERFType ErfType { get; set; }
    public bool IsSaveErf { get; set; }

    public ERF(ERFType erfType = ERFType.ERF, bool isSave = false)
    {
        ErfType = erfType;
        IsSaveErf = isSave;
    }

    public int Count => _resources.Count;

    /// <summary>
    /// Gets a resource by index or ResourceIdentifier.
    /// </summary>
    public ERFResource this[int index] => _resources[index];

    /// <summary>
    /// Gets a resource by ResourceIdentifier or resname string.
    /// </summary>
    public ERFResource this[string resname]
    {
        get
        {
            string lowerResname = resname.ToLowerInvariant();
            var key = _resourceDict.Keys.FirstOrDefault(k =>
                k.ResName.ToLowerInvariant() == lowerResname);

            if (key != null && _resourceDict.TryGetValue(key, out var resource))
            {
                return resource;
            }

            throw new KeyNotFoundException($"{resname} not found in ERF");
        }
    }

    /// <summary>
    /// Gets a resource by ResourceIdentifier.
    /// </summary>
    public ERFResource this[ResourceIdentifier identifier]
    {
        get
        {
            if (_resourceDict.TryGetValue(identifier, out var resource))
            {
                return resource;
            }
            throw new KeyNotFoundException($"{identifier} not found in ERF");
        }
    }

    public void SetData(string resname, ResourceType restype, byte[] data)
    {
        var ident = new ResourceIdentifier(resname, restype);
        var resref = new ResRef(ident.ResName);

        if (_resourceDict.TryGetValue(ident, out var resource))
        {
            // Update existing resource
            resource.ResRef = resref;
            resource.ResType = restype;
            resource.Data = data;
        }
        else
        {
            // Create new resource
            resource = new ERFResource(resref, restype, data);
            _resources.Add(resource);
            _resourceDict[ident] = resource;
        }
    }

    public byte[]? Get(string resname, ResourceType restype)
    {
        var ident = new ResourceIdentifier(resname, restype);
        return _resourceDict.TryGetValue(ident, out var resource) ? resource.Data : null;
    }

    public void Remove(string resname, ResourceType restype)
    {
        var key = new ResourceIdentifier(resname, restype);
        if (_resourceDict.Remove(key, out var resource))
        {
            _resources.Remove(resource);
        }
    }

    public RIM.RIM ToRim()
    {
        var rim = new RIM.RIM();
        foreach (var resource in _resources)
        {
            rim.SetData(resource.ResRef.ToString(), resource.ResType, resource.Data);
        }
        return rim;
    }

    public override bool Equals(object? obj)
    {
        if (obj is ERF other)
        {
            return _resources.ToHashSet().SetEquals(other._resources);
        }
        if (obj is RIM.RIM rim)
        {
            return _resources.ToHashSet().SetEquals(rim.GetResources().Select(r =>
                new ERFResource(r.ResRef, r.ResType, r.Data)));
        }
        return false;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ErfType);
        hash.Add(IsSaveErf);
        foreach (var resource in _resources.OrderBy(r => r.GetHashCode()))
        {
            hash.Add(resource);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => $"ERF({ErfType})";

    public IEnumerator<ERFResource> GetEnumerator() => _resources.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Represents a single resource within an ERF archive.
/// </summary>
public class ERFResource
{
    public ResRef ResRef { get; set; }
    public ResourceType ResType { get; set; }
    public byte[] Data { get; set; }

    public ERFResource(ResRef resref, ResourceType restype, byte[] data)
    {
        ResRef = resref;
        ResType = restype;
        // Handle bytearray conversion if needed
        Data = data is byte[]? data : data.ToArray();
    }

    public ResourceIdentifier Identifier() => new(ResRef.ToString(), ResType);

    public override bool Equals(object? obj)
    {
        if (obj is ERFResource other)
        {
            return ResRef.Equals(other.ResRef) &&
                   ResType == other.ResType &&
                   Data.SequenceEqual(other.Data);
        }
        if (obj is RIM.RIMResource rimRes)
        {
            return ResRef.Equals(rimRes.ResRef) &&
                   ResType == rimRes.ResType &&
                   Data.SequenceEqual(rimRes.Data);
        }
        return false;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ResRef);
        hash.Add(ResType);
        // Hash first 1000 bytes for performance
        foreach (byte b in Data.Take(1000))
        {
            hash.Add(b);
        }
        return hash.ToHashCode();
    }

    public override string ToString() => $"{ResRef}.{ResType.Extension}";
}
