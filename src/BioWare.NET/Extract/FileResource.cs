using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:269-561
    // Original: class FileResource:
    /// <summary>
    /// Stores information for a resource regarding its name, type and where the data can be loaded from.
    /// Represents a resource entry with metadata (name, type, size, offset) and file location.
    /// Used throughout PyKotor for resource abstraction and lazy loading.
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
        private readonly bool _insideBif;
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

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:303-313
            // Original: filepath_str = str(self._filepath).lower()
            // Original: self.inside_capsule: bool = filepath_str.endswith(_CAPSULE_EXTENSIONS)
            // Original: self.inside_bif: bool = filepath_str.endswith(".bif")
            string filepathStr = filepath.ToLowerInvariant();
            _insideCapsule = filepathStr.EndsWith(".erf") || filepathStr.EndsWith(".mod") ||
                           filepathStr.EndsWith(".rim") || filepathStr.EndsWith(".sav") ||
                           filepathStr.EndsWith(".hak");
            _insideBif = filepathStr.EndsWith(".bif");

            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:309-313
            // Original: self._path_ident_obj: Path = (self._filepath / str(self._identifier) if self.inside_capsule or self.inside_bif else self._filepath)
            if (_insideCapsule || _insideBif)
            {
                _pathIdentifier = Path.Combine(filepath, _identifier.ToString());
            }
            else
            {
                _pathIdentifier = filepath;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:356-406
        // Python uses methods, but C# codebase uses properties - keeping properties for compatibility
        // Original: def identifier(self) -> ResourceIdentifier:
        public ResourceIdentifier Identifier => _identifier;

        // Original: def resname(self) -> str:
        public string ResName => _resname;

        // Original: def resref(self) -> ResRef:
        public ResRef ResRef() => new ResRef(_resname);

        // Original: def restype(self) -> ResourceType:
        public ResourceType ResType => _restype;

        // Original: def size(self) -> int:
        public int Size => _size;

        // Original: def filename(self) -> str:
        public string Filename() => _identifier.ToString();

        // Original: def filepath(self) -> Path:
        public string FilePath => _filepath;

        // Original: def path_ident(self) -> Path:
        public string PathIdent() => _pathIdentifier;

        // Original: def offset(self) -> int:
        public int Offset => _offset;

        // Properties for C# style access
        public bool InsideCapsule => _insideCapsule;
        public bool InsideBif => _insideBif;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:408-458
        // Original: def _index_resource(self):
        private void IndexResource()
        {
            // Fast path: check if the file exists directly on the filesystem
            if (File.Exists(_filepath))
            {
                if (_insideCapsule)
                {
                    var capsule = new Extract.Capsule.LazyCapsule(_filepath);
                    var res = capsule.GetResourceInfo(_resname, _restype);
                    if (res == null && _identifier.ToString() == Path.GetFileName(_filepath))
                    {
                        // The capsule is the resource itself
                        _offset = 0;
                        _size = (int)new FileInfo(_filepath).Length;
                        return;
                    }
                    if (res == null)
                    {
                        throw new FileNotFoundException($"Resource '{_identifier}' not found in Capsule", _filepath);
                    }

                    _offset = res.Offset;
                    _size = res.Size;
                }
                else if (!_insideBif) // bifs are read-only, offset/data will never change
                {
                    _offset = 0;
                    _size = (int)new FileInfo(_filepath).Length;
                }
                return;
            }

            // Slow path: handle nested capsule paths
            var (realPath, nestedParts) = FileResourceHelpers.FindRealFilesystemPath(_filepath);

            if (realPath == null)
            {
                throw new FileNotFoundException($"Cannot find file or capsule to index: {_filepath}", _filepath);
            }

            if (nestedParts.Count == 0)
            {
                throw new FileNotFoundException($"Path exists but cannot be indexed: {_filepath}", _filepath);
            }

            // For nested capsule paths, the offset is always 0 relative to the extracted resource
            // and the size is determined during extraction. We can't efficiently get the size
            // without extracting, so we'll set size to 0 and let data() handle it.
            _offset = 0;
            _size = 0; // Size will be determined during extraction
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:460-497
        // Original: def exists(self) -> bool:
        public bool Exists()
        {
            try
            {
                // Fast path: check if the file exists directly on the filesystem
                if (File.Exists(_filepath))
                {
                    if (!_insideCapsule && !_insideBif)
                    {
                        return true;
                    }
                    // It's a capsule file that exists - verify the resource is inside it
                    var capsule = new Extract.Capsule.LazyCapsule(_filepath);
                    return capsule.GetResourceInfo(_resname, _restype) != null;
                }

                // Check for nested capsule path
                var (realPath, nestedParts) = FileResourceHelpers.FindRealFilesystemPath(_filepath);

                if (realPath == null)
                {
                    return false;
                }

                if (nestedParts.Count == 0)
                {
                    // Real path exists but isn't a regular file - might be a directory or special file
                    return false;
                }

                // Verify the resource exists inside the nested capsule structure
                // We do this by attempting to extract - if it fails, the resource doesn't exist
                try
                {
                    FileResourceHelpers.ExtractFromNestedCapsules(realPath, nestedParts);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
                catch (InvalidDataException)
                {
                    return false;
                }
            }
            catch (Exception)
            {
                // Matching PyKotor: RobustLogger().exception("Failed to check existence of FileResource.")
                return false;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:499-556
        // Original: def data(self, *, reload: bool = False) -> bytes:
        public byte[] Data(bool reload = false)
        {
            if (reload)
            {
                IndexResource();
            }

            // Fast path: try to open the file directly
            // This handles the common case of non-nested paths efficiently
            if (File.Exists(_filepath))
            {
                using (FileStream fs = File.OpenRead(_filepath))
                {
                    fs.Seek(_offset, SeekOrigin.Begin);
                    byte[] buffer = new byte[_size];
                    fs.Read(buffer, 0, _size);
                    return buffer;
                }
            }

            // Slow path: check for nested capsule path
            // This handles paths like SAVEGAME.sav/inner.sav/resource.utc
            var (realPath, nestedParts) = FileResourceHelpers.FindRealFilesystemPath(_filepath);

            if (realPath == null)
            {
                // No part of the path exists on the filesystem
                throw new FileNotFoundException($"Cannot find file or capsule: {_filepath}", _filepath);
            }

            if (nestedParts.Count == 0)
            {
                // The path exists but is_file() returned False earlier - race condition or permission issue
                // Try opening it anyway
                using (FileStream fs = File.OpenRead(realPath))
                {
                    fs.Seek(_offset, SeekOrigin.Begin);
                    byte[] buffer = new byte[_size];
                    fs.Read(buffer, 0, _size);
                    return buffer;
                }
            }

            // We have a nested capsule path - extract through the nesting levels
            // Note: We don't use self._offset/self._size here because the extraction
            // function re-parses the capsule headers and gets fresh offset/size values.
            // The stored offset/size would be redundant (same values from the same source).
            return FileResourceHelpers.ExtractFromNestedCapsules(realPath, nestedParts);
        }

        /// <summary>
        /// Opens the file and returns the bytes data of the resource.
        /// </summary>
        public byte[] GetData()
        {
            return Data();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:344-354
        // Original: @classmethod def from_path(cls, path: os.PathLike | str) -> Self:
        public static FileResource FromPath(string path)
        {
            FileInfo fileInfo = new FileInfo(path);
            ResourceIdentifier identifier = ResourceIdentifier.FromPath(path);
            return new FileResource(
                identifier.ResName,
                identifier.ResType,
                (int)fileInfo.Length,
                0,
                path
            );
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/file.py:558-560
        // Original: def as_file_resource(self) -> Self:
        public FileResource AsFileResource() => this;

        public override bool Equals(object obj)
        {
            return Equals(obj as FileResource);
        }

        public bool Equals(FileResource other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            if (other is null)
            {
                return false;
            }
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

        public static bool operator ==(FileResource left, [CanBeNull] FileResource right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null)
            {
                return false;
            }
            return left.Equals(right);
        }

        public static bool operator !=(FileResource left, [CanBeNull] FileResource right)
        {
            return !(left == right);
        }
    }

    /// <summary>
    /// Result containing resource data and metadata.
    /// </summary>
    public sealed class ExtractResourceResult
    {
        public string ResName { get; }
        public ResourceType ResType { get; }
        public string FilePath { get; }
        public byte[] Data { get; }
        public FileResource FileResource { get; private set; }

        public ExtractResourceResult(string resName, ResourceType resType, string filePath, byte[] data)
        {
            ResName = resName;
            ResType = resType;
            FilePath = filePath;
            Data = data;
        }

        public void SetFileResource(FileResource resource)
        {
            if (!ReferenceEquals(FileResource, null))
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
            return $"ExtractResourceResult({ResName}, {ResType}, {FilePath}, byte[{Data.Length}])";
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
        public FileResource FileResource { get; private set; }

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
            if (FileResource is null)
            {
                throw new InvalidOperationException("FileResource not assigned");
            }
            return FileResource.Identifier;
        }

        public override string ToString()
        {
            return $"LocationResult({FilePath}, {Offset}, {Size})";
        }

        public override bool Equals(object obj)
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
}

