using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Resources;

namespace TSLPatcher.Core.Formats.Capsule;

/// <summary>
/// Lazy-loading capsule that doesn't load resource data into memory until requested.
/// Used for performance when you only need metadata or specific resources.
/// </summary>
public class LazyCapsule : IEnumerable<FileResource>
{
    private readonly string _filepath;
    private readonly CapsuleType _capsuleType;
    private List<FileResource>? _cachedResources;

    public string FilePath => _filepath;
    public CapsuleType Type => _capsuleType;

    public LazyCapsule(string path, bool createIfNotExist = false)
    {
        if (!IsCapsuleFile(path))
        {
            throw new ArgumentException($"Invalid file extension in capsule filepath '{path}'", nameof(path));
        }

        _filepath = path;
        _capsuleType = DetermineCapsuleType(Path.GetExtension(path));

        if (createIfNotExist && !File.Exists(path))
        {
            CreateEmpty();
        }
    }

    private static bool IsCapsuleFile(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext is "erf" or "mod" or "rim" or "sav" or "hak";
    }

    private static CapsuleType DetermineCapsuleType(string extension)
    {
        string ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "rim" => CapsuleType.RIM,
            "erf" => CapsuleType.ERF,
            "mod" => CapsuleType.MOD,
            "sav" => CapsuleType.SAV,
            "hak" => CapsuleType.ERF,
            _ => throw new ArgumentException($"Unknown capsule type: {extension}")
        };
    }

    private void CreateEmpty()
    {
        if (_capsuleType == CapsuleType.RIM)
        {
            using var writer = new BinaryWriter(File.Create(_filepath));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIM "));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("V1.0"));
            writer.Write(0);
            writer.Write(0);
            writer.Write(120);
        }
        else
        {
            using var writer = new BinaryWriter(File.Create(_filepath));
            string fourCC = _capsuleType == CapsuleType.MOD ? "MOD " : "ERF ";
            writer.Write(System.Text.Encoding.ASCII.GetBytes(fourCC));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("V1.0"));
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0);
            writer.Write(160);
            writer.Write(160);
            writer.Write(0);
            writer.Write(0);
            writer.Write(0xFFFFFFFF);
            for (int i = 0; i < 116; i++)
            {
                writer.Write((byte)0);
            }
        }
    }

    /// <summary>
    /// Gets the list of FileResources from the capsule (metadata only, no data loaded).
    /// </summary>
    public List<FileResource> GetResources()
    {
        if (_cachedResources != null)
        {
            return new List<FileResource>(_cachedResources);
        }

        if (!File.Exists(_filepath))
        {
            return new List<FileResource>();
        }

        using var reader = RawBinaryReader.FromFile(_filepath);
        string fileType = reader.ReadString(4);
        reader.Skip(4); // version

        List<FileResource> resources;
        if (fileType == "RIM ")
        {
            resources = LoadRIMMetadata(reader);
        }
        else if (fileType is "ERF " or "MOD ")
        {
            resources = LoadERFMetadata(reader);
        }
        else
        {
            throw new InvalidDataException($"Unknown capsule file type: {fileType}");
        }

        _cachedResources = resources;
        return new List<FileResource>(resources);
    }

    private List<FileResource> LoadRIMMetadata(RawBinaryReader reader)
    {
        var resources = new List<FileResource>();

        reader.Skip(4); // reserved
        uint entryCount = reader.ReadUInt32();
        uint offsetToKeys = reader.ReadUInt32();

        reader.Seek((int)offsetToKeys);
        for (uint i = 0; i < entryCount; i++)
        {
            string resref = reader.ReadString(16);
            uint restype = reader.ReadUInt32();
            reader.Skip(4); // resid
            uint offset = reader.ReadUInt32();
            uint size = reader.ReadUInt32();

            var resourceType = ResourceType.FromId((int)restype);
            resources.Add(new FileResource(resref, resourceType, (int)size, (int)offset, _filepath));
        }

        return resources;
    }

    private List<FileResource> LoadERFMetadata(RawBinaryReader reader)
    {
        var resources = new List<FileResource>();

        reader.Skip(8); // language count + localized string size
        uint entryCount = reader.ReadUInt32();
        reader.Skip(4); // offset to localized strings
        uint offsetToKeys = reader.ReadUInt32();
        uint offsetToResources = reader.ReadUInt32();

        var resrefs = new List<string>();
        var restypes = new List<ResourceType>();

        reader.Seek((int)offsetToKeys);
        for (uint i = 0; i < entryCount; i++)
        {
            string resref = reader.ReadString(16);
            uint resid = reader.ReadUInt32();
            ushort restype = reader.ReadUInt16();
            reader.Skip(2); // unused
            resrefs.Add(resref);
            restypes.Add(ResourceType.FromId(restype));
        }

        reader.Seek((int)offsetToResources);
        for (int i = 0; i < entryCount; i++)
        {
            uint offset = reader.ReadUInt32();
            uint size = reader.ReadUInt32();
            resources.Add(new FileResource(resrefs[i], restypes[i], (int)size, (int)offset, _filepath));
        }

        return resources;
    }

    /// <summary>
    /// Gets the data for a specific resource.
    /// </summary>
    public byte[]? GetResource(string resname, ResourceType restype)
    {
        var resource = GetResources().FirstOrDefault(r =>
            string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
            r.ResType == restype);

        return resource?.GetData();
    }

    /// <summary>
    /// Gets information about a resource without loading its data.
    /// </summary>
    public FileResource? GetResourceInfo(string resname, ResourceType restype)
    {
        return GetResources().FirstOrDefault(r =>
            string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
            r.ResType == restype);
    }

    /// <summary>
    /// Checks if a resource exists in the capsule.
    /// </summary>
    public bool Contains(string resname, ResourceType restype)
    {
        return GetResourceInfo(resname, restype) != null;
    }

    /// <summary>
    /// Adds or updates a resource in the capsule.
    /// </summary>
    public void Add(string resname, ResourceType restype, byte[] data)
    {
        // Need to load all resources, modify, and save
        var fullCapsule = ToCapsule();
        fullCapsule.SetResource(resname, restype, data);
        fullCapsule.Save();

        // Invalidate cache
        _cachedResources = null;
    }

    /// <summary>
    /// Removes a resource from the capsule.
    /// </summary>
    public bool Delete(string resname, ResourceType restype)
    {
        var fullCapsule = ToCapsule();
        bool removed = fullCapsule.RemoveResource(resname, restype);
        if (removed)
        {
            fullCapsule.Save();
            _cachedResources = null;
        }
        return removed;
    }

    /// <summary>
    /// Converts this lazy capsule to a fully-loaded Capsule.
    /// </summary>
    public Capsule ToCapsule()
    {
        var capsule = new Capsule(_filepath);
        return capsule;
    }

    public IEnumerator<FileResource> GetEnumerator() => GetResources().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => GetResources().Count;
}

