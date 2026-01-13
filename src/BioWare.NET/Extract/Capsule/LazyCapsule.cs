using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;
using ERFType = BioWare.NET.Resource.Formats.ERF.ERFType;
using ERF = BioWare.NET.Resource.Formats.ERF.ERF;
using RIM = BioWare.NET.Resource.Formats.RIM.RIM;

namespace BioWare.NET.Extract.Capsule
{

    /// <summary>
    /// Lazy-loading capsule that doesn't load resource data into memory until requested.
    /// Used for performance when you only need metadata or specific resources.
    /// </summary>
    public class LazyCapsule : IEnumerable<FileResource>
    {
        private readonly string _filepath;
        private readonly CapsuleType _capsuleType;
        [CanBeNull]
        private List<FileResource> _cachedResources;

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
            return ext == "erf" || ext == "mod" || ext == "rim" || ext == "sav" || ext == "hak";
        }

        private static CapsuleType DetermineCapsuleType(string extension)
        {
            string ext = extension.TrimStart('.').ToLowerInvariant();
            if (ext == "rim")
            {

                return CapsuleType.RIM;
            }
            else if (ext == "erf")
            {
                return CapsuleType.ERF;
            }
            else if (ext == "mod")
            {
                return CapsuleType.MOD;
            }
            else if (ext == "sav")
            {
                return CapsuleType.SAV;
            }
            else if (ext == "hak")
            {
                return CapsuleType.ERF;
            }
            else
            {
                throw new ArgumentException($"Unknown capsule type: {extension}");
            }
        }

        private void CreateEmpty()
        {
            if (_capsuleType == CapsuleType.RIM)
            {
                using (var writer = new System.IO.BinaryWriter(File.Create(_filepath)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIM "));
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("V1.0"));
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(120);
                }
            }
            else
            {
                using (var writer = new System.IO.BinaryWriter(File.Create(_filepath)))
                {
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
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:207-244
        // Original: def resources(self) -> list[FileResource]:
        public List<FileResource> Resources()
        {
            // Check if file is empty (0 bytes) - empty files cannot be valid capsules
            if (File.Exists(_filepath) && new FileInfo(_filepath).Length == 0)
            {
                return new List<FileResource>();
            }

            if (!File.Exists(_filepath))
            {
                return new List<FileResource>();
            }

            using (var reader = RawBinaryReader.FromFile(_filepath))
            {
                string fileType = reader.ReadString(4);
                reader.Skip(4); // file version

                List<FileResource> resources;
                if (fileType == "ERF " || fileType == "MOD " || fileType == "SAV " || fileType == "HAK ")
                {
                    resources = LoadERFMetadata(reader);
                }
                else if (fileType == "RIM ")
                {
                    resources = LoadRIMMetadata(reader);
                }
                else
                {
                    throw new NotImplementedException($"File '{_filepath}' must be a ERF/MOD/SAV/RIM capsule, '{Path.GetExtension(_filepath)}' is not implemented.");
                }

                return resources;
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

            using (var reader = RawBinaryReader.FromFile(_filepath))
            {
                string fileType = reader.ReadString(4);
                reader.Skip(4); // version

                List<FileResource> resources;
                if (fileType == "RIM ")
                {
                    resources = LoadRIMMetadata(reader);
                }
                else if (fileType == "ERF " || fileType == "MOD ")
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
        [CanBeNull]
        public byte[] GetResource(string resname, ResourceType restype)
        {
            // Can be null if resource not found
            FileResource resource = GetResources().FirstOrDefault(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            return resource?.GetData();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:182-205
        // Original: def info(self, resref: str, restype: ResourceType) -> FileResource | None:
        [CanBeNull]
        public FileResource Info(string resref, ResourceType restype)
        {
            var query = new ResourceIdentifier(resref, restype);
            return Resources().FirstOrDefault(r => r.Identifier.Equals(query));
        }

        /// <summary>
        /// Gets information about a resource without loading its data.
        /// </summary>
        [CanBeNull]
        public FileResource GetResourceInfo(string resname, ResourceType restype)
        {
            return Info(resname, restype);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:153-180
        // Original: def contains(self, resref: str, restype: ResourceType) -> bool:
        public bool Contains(string resref, ResourceType restype)
        {
            var query = new ResourceIdentifier(resref, restype);
            return Resources().Any(r => r.Identifier.Equals(query));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:105-151
        // Original: def batch(self, queries: list[ResourceIdentifier]) -> dict[ResourceIdentifier, ResourceResult | None]:
        public Dictionary<ResourceIdentifier, ExtractResourceResult> Batch(List<ResourceIdentifier> queries)
        {
            var results = new Dictionary<ResourceIdentifier, ExtractResourceResult>();
            using (var reader = RawBinaryReader.FromFile(_filepath))
            {
                foreach (var query in queries)
                {
                    results[query] = null;

                    var resource = Resources().FirstOrDefault(r => r.Identifier.Equals(query));
                    if (resource == null)
                    {
                        continue;
                    }

                    reader.Seek(resource.Offset);
                    byte[] data = reader.ReadBytes(resource.Size);
                    results[query] = new ExtractResourceResult(
                        query.ResName,
                        query.ResType,
                        _filepath,
                        data
                    );
                }
            }
            return results;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:246-286
        // Original: def add(self, resname: str, restype: ResourceType, resdata: bytes):
        public void Add(string resname, ResourceType restype, byte[] resdata)
        {
            string ext = Path.GetExtension(_filepath).ToLowerInvariant();
            if (ext == ".rim")
            {
                var container = new RIM();
                container.SetData(resname, restype, resdata);
                foreach (var resource in Resources())
                {
                    container.SetData(resource.ResName, resource.ResType, resource.Data());
                }
                Resource.Formats.RIM.RIMAuto.WriteRim(container, _filepath, ResourceType.RIM);
            }
            else if (ext == ".erf" || ext == ".mod" || ext == ".sav" || ext == ".hak")
            {
                ERFType erfType = Resource.Formats.ERF.ERFTypeExtensions.FromExtension(ext);
                var container = new ERF(erfType);
                container.SetData(resname, restype, resdata);
                foreach (var resource in Resources())
                {
                    container.SetData(resource.ResName, resource.ResType, resource.Data());
                }
                ResourceType fileFormat = ext == ".erf" ? ResourceType.ERF : (ext == ".mod" ? ResourceType.MOD : ResourceType.SAV);
                Resource.Formats.ERF.ERFAuto.WriteErf(container, _filepath, fileFormat);
            }
            else
            {
                throw new NotImplementedException($"File '{_filepath}' is not a ERF/MOD/SAV/RIM capsule.");
            }

            // Invalidate cache
            _cachedResources = null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:288-327
        // Original: def delete(self, resname: str, restype: ResourceType):
        public void Delete(string resname, ResourceType restype)
        {
            string ext = Path.GetExtension(_filepath).ToLowerInvariant();
            if (ext == ".rim")
            {
                var container = new RIM();
                foreach (var resource in Resources())
                {
                    if (string.Equals(resource.ResName, resname, StringComparison.OrdinalIgnoreCase) && resource.ResType == restype)
                    {
                        continue; // Skip the resource to delete
                    }
                    container.SetData(resource.ResName, resource.ResType, resource.Data());
                }
                Resource.Formats.RIM.RIMAuto.WriteRim(container, _filepath, ResourceType.RIM);
            }
            else if (ext == ".erf" || ext == ".mod" || ext == ".sav" || ext == ".hak")
            {
                ERFType erfType = Resource.Formats.ERF.ERFTypeExtensions.FromExtension(ext);
                var container = new ERF(erfType);
                foreach (var resource in Resources())
                {
                    if (string.Equals(resource.ResName, resname, StringComparison.OrdinalIgnoreCase) && resource.ResType == restype)
                    {
                        continue; // Skip the resource to delete
                    }
                    container.SetData(resource.ResName, resource.ResType, resource.Data());
                }
                ResourceType fileFormat = ext == ".erf" ? ResourceType.ERF : (ext == ".mod" ? ResourceType.MOD : ResourceType.SAV);
                Resource.Formats.ERF.ERFAuto.WriteErf(container, _filepath, fileFormat);
            }
            else
            {
                throw new NotImplementedException($"File '{_filepath}' is not a ERF/MOD/SAV/RIM capsule.");
            }

            // Invalidate cache
            _cachedResources = null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:329-333
        // Original: def as_cached_erf(self, erf_type: ERFType | None = None) -> ERF:
        /// <summary>
        /// Converts this lazy capsule to a fully-loaded ERF by loading all resources into memory.
        /// </summary>
        public ERF AsCachedErf(ERFType? erfType = null)
        {
            ERFType type = erfType ?? DetermineErfType();
            ERF erf = new ERF(type);
            foreach (FileResource resource in Resources())
            {
                byte[] data = resource.Data();
                erf.SetData(resource.ResName, resource.ResType, data);
            }
            return erf;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:335-339
        // Original: def as_cached_rim(self) -> RIM:
        /// <summary>
        /// Converts this lazy capsule to a fully-loaded RIM by loading all resources into memory.
        /// </summary>
        public RIM AsCachedRim()
        {
            RIM rim = new RIM();
            foreach (FileResource resource in Resources())
            {
                byte[] data = resource.Data();
                rim.SetData(resource.ResName, resource.ResType, data);
            }
            return rim;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/capsule.py:341-346
        // Original: def as_cached(self) -> ERF | RIM:
        /// <summary>
        /// Converts this lazy capsule to a fully-loaded ERF or RIM based on the file type.
        /// </summary>
        public object AsCached()
        {
            // TODO: HACK - Inlined FileHelpers.IsAnyErfTypeFile to avoid circular dependency (Extract ↔ Tools)
            string ext = Path.GetExtension(_filepath ?? "").ToLowerInvariant();
            if (ext == ".erf" || ext == ".mod" || ext == ".sav")
            {
                return AsCachedErf();
            }
            else
            {
                return AsCachedRim();
            }
        }

        // Helper method to determine ERFType from capsule type
        private ERFType DetermineErfType()
        {
            switch (_capsuleType)
            {
                case CapsuleType.ERF:
                    return ERFType.ERF;
                case CapsuleType.MOD:
                case CapsuleType.SAV:
                    return ERFType.MOD; // SAV files use MOD format
                default:
                    return ERFType.ERF; // Default fallback
            }
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
}
