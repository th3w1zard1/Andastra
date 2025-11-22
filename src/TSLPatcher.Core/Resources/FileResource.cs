using System;
using System.IO;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Resources;

/// <summary>
/// Stores information for a resource regarding its name, type and where the data can be loaded from.
/// Represents a resource entry with metadata (name, type, size, offset) and file location.
/// </summary>
public class FileResource : IEquatable<FileResource>
{
    private readonly ResourceIdentifier _identifier;
    private readonly string _resname;
    private readonly ResourceType _restype;
    private int _size;
    private int _offset;
    private readonly string _filepath;
    private readonly bool _insideCapsule;
    private readonly string _pathIdentifier;

    public FileResource(string resname, ResourceType restype, int size, int offset, string filepath)
    {
        if (resname != resname.Trim())
        {
            throw new ArgumentException($"Resource name '{resname}' cannot start/end with whitespace", nameof(resname));
        }

        _resname = resname;
        _restype = restype;
        _size = size;
        _offset = offset;
        _filepath = filepath;
        _identifier = new ResourceIdentifier(resname, restype);

        // Check if inside capsule (ERF/MOD/RIM/SAV)
        string ext = Path.GetExtension(filepath).ToLowerInvariant();
        _insideCapsule = ext is ".erf" or ".mod" or ".rim" or ".sav" or ".hak";

        // Build path identifier
        if (_insideCapsule)
        {
            _pathIdentifier = Path.Combine(filepath, _identifier.ToString());
        }
        else
        {
            _pathIdentifier = filepath;
        }
    }

    public ResourceIdentifier Identifier => _identifier;
    public string ResName => _resname;
    public ResourceType ResType => _restype;
    public int Size => _size;
    public int Offset => _offset;
    public string FilePath => _filepath;
    public bool InsideCapsule => _insideCapsule;
    public string PathIdentifier => _pathIdentifier;

    public string Filename() => _identifier.ToString();

    /// <summary>
    /// Opens the file and returns the bytes data of the resource.
    /// </summary>
    public byte[] GetData()
    {
        using FileStream fs = File.OpenRead(_filepath);
        using BinaryReader reader = new(fs);

        reader.BaseStream.Seek(_offset, SeekOrigin.Begin);
        return reader.ReadBytes(_size);
    }

    /// <summary>
    /// Determines if this FileResource exists on disk.
    /// </summary>
    public bool Exists()
    {
        try
        {
            if (_insideCapsule)
            {
                return File.Exists(_filepath);
            }
            return File.Exists(_filepath);
        }
        catch
        {
            return false;
        }
    }

    public static FileResource FromPath(string path)
    {
        FileInfo fileInfo = new(path);
        ResourceIdentifier identifier = ResourceIdentifier.FromPath(path);
        return new FileResource(
            identifier.ResName,
            identifier.ResType,
            (int)fileInfo.Length,
            0,
            path
        );
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as FileResource);
    }

    public bool Equals(FileResource? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        return _pathIdentifier.Equals(other._pathIdentifier, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(_pathIdentifier);
    }

    public override string ToString()
    {
        return _identifier.ToString();
    }

    public static bool operator ==(FileResource? left, FileResource? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(FileResource? left, FileResource? right)
    {
        return !(left == right);
    }
}

/// <summary>
/// Result containing resource data and metadata.
/// </summary>
public sealed class ResourceResult
{
    public string ResName { get; }
    public ResourceType ResType { get; }
    public string FilePath { get; }
    public byte[] Data { get; }
    public FileResource? FileResource { get; private set; }

    public ResourceResult(string resName, ResourceType resType, string filePath, byte[] data)
    {
        ResName = resName;
        ResType = resType;
        FilePath = filePath;
        Data = data;
    }

    public void SetFileResource(FileResource resource)
    {
        if (FileResource != null)
        {
            throw new InvalidOperationException("FileResource can only be set once");
        }
        FileResource = resource;
    }

    public ResourceIdentifier GetIdentifier()
    {
        return new ResourceIdentifier(ResName, ResType);
    }

    public override string ToString()
    {
        return $"ResourceResult({ResName}, {ResType}, {FilePath}, byte[{Data.Length}])";
    }
}

/// <summary>
/// Result containing location information for a resource.
/// </summary>
public sealed class LocationResult
{
    public string FilePath { get; }
    public int Offset { get; }
    public int Size { get; }
    public FileResource? FileResource { get; private set; }

    public LocationResult(string filePath, int offset, int size)
    {
        FilePath = filePath;
        Offset = offset;
        Size = size;
    }

    public void SetFileResource(FileResource resource)
    {
        if (FileResource != null)
        {
            throw new InvalidOperationException("FileResource can only be set once");
        }
        FileResource = resource;
    }

    public ResourceIdentifier GetIdentifier()
    {
        if (FileResource == null)
        {
            throw new InvalidOperationException("FileResource not assigned");
        }
        return FileResource.Identifier;
    }

    public override string ToString()
    {
        return $"LocationResult({FilePath}, {Offset}, {Size})";
    }

    public override bool Equals(object? obj)
    {
        if (obj is LocationResult other)
        {
            return FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase)
                && Offset == other.Offset
                && Size == other.Size;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath),
            Offset,
            Size
        );
    }
}

