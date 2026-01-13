using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract.Capsule
{

    /// <summary>
    /// Represents a capsule file (ERF, RIM, MOD, or SAV) that contains multiple resources.
    /// This is the main class for accessing game archive files.
    /// </summary>
    public class Capsule : IEnumerable<CapsuleResource>
    {
        private readonly List<CapsuleResource> _resources = new List<CapsuleResource>();
        private readonly CaseAwarePath _path;
        private readonly CapsuleType _capsuleType;
        private readonly bool _existedOnDisk;

        public CaseAwarePath Path => _path;
        public CapsuleType Type => _capsuleType;
        public int Count => _resources.Count;

        public bool ExistedOnDisk => _existedOnDisk;

        public Capsule(string path, bool createIfNotExist = false)
        {
            _path = new CaseAwarePath(path);
            _capsuleType = DetermineCapsuleType(_path.Extension);
            _existedOnDisk = _path.IsFile();

            if (createIfNotExist && !_path.IsFile())
            {
                CreateEmpty();
            }
            else if (_path.IsFile())
            {
                Reload();
            }
        }

        private static CapsuleType DetermineCapsuleType(string extension)
        {
            string ext = extension.TrimStart('.').ToLowerInvariant();
            switch (ext)
            {
                case "rim":
                    return CapsuleType.RIM;
                case "erf":
                    return CapsuleType.ERF;
                case "mod":
                    return CapsuleType.MOD;
                case "sav":
                    return CapsuleType.SAV;
                default:
                    throw new ArgumentException($"Unknown capsule type: {extension}");
            }
            ;
        }

        private void CreateEmpty()
        {
            if (_capsuleType == CapsuleType.RIM)
            {
                // Write empty RIM
                using (var writer = new System.IO.BinaryWriter(File.Create(_path.GetResolvedPath())))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIM "));
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("V1.0"));
                    writer.Write(0); // reserved
                    writer.Write(0); // entry count
                    writer.Write(120); // offset to keys (header size)
                }
            }
            else
            {
                // Write empty ERF
                using (var writer = new System.IO.BinaryWriter(File.Create(_path.GetResolvedPath())))
                {
                    string fourCC = _capsuleType == CapsuleType.ERF ? "ERF " : "MOD ";
                    writer.Write(System.Text.Encoding.ASCII.GetBytes(fourCC));
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("V1.0"));
                    writer.Write(0); // language count
                    writer.Write(0); // localized string size
                    writer.Write(0); // entry count
                    writer.Write(0); // offset to localized strings
                    writer.Write(160); // offset to keys
                    writer.Write(160); // offset to resources
                    writer.Write(0); // build year
                    writer.Write(0); // build day
                    writer.Write(0xFFFFFFFF); // description strref
                                              // Pad to 160 bytes
                    for (int i = 0; i < 116; i++)
                    {
                        writer.Write((byte)0);
                    }
                }
            }
        }

        public void Reload()
        {
            _resources.Clear();

            if (!_path.IsFile())
            {
                return;
            }

            using (var reader = new System.IO.BinaryReader(File.OpenRead(_path.GetResolvedPath())))
            {

                string fileType = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));
                string fileVersion = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (fileVersion != "V1.0")
                {
                    throw new InvalidDataException($"Unsupported capsule version: {fileVersion}");
                }

                if (fileType == "RIM ")
                {
                    LoadRIM(reader);
                }
                else if (fileType == "ERF " || fileType == "MOD ")
                {
                    LoadERF(reader);
                }
                else
                {
                    throw new InvalidDataException($"Unknown capsule file type: {fileType}");
                }
            }
        }

        // REVERSE ENGINEERING NOTES (swkotor.exe / swkotor2.exe):
        // RIM container format structure (matches engine behavior):
        // - Header: "RIM V1.0" (8 bytes)
        // - Reserved: uint32 (4 bytes)
        // - Entry count: uint32 (4 bytes)
        // - Offset to keys: uint32 (4 bytes)
        // - Key entries: Each entry contains:
        //   * ResRef: 16-byte ASCII string (null-padded, NO path separators - subfolders NOT supported)
        //   * Resource Type: uint32 (4 bytes) - NO filtering, any type ID is accepted
        //   * Resource ID: uint32 (4 bytes)
        //   * Offset: uint32 (4 bytes)
        //   * Size: uint32 (4 bytes)
        // - Resource data: Raw bytes at specified offset
        //
        // Key Finding: Container format does NOT filter resource types.
        // The engine will load ANY resource type stored in a RIM file, as long as:
        // 1. The resource type ID is valid
        // 2. The resource data can be parsed by the appropriate loader
        private void LoadRIM(System.IO.BinaryReader reader)
        {
            reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip header
            reader.ReadInt32(); // reserved
            uint entryCount = reader.ReadUInt32();
            uint offsetToKeys = reader.ReadUInt32();

            var entries = new List<(string resref, uint restype, uint resid, uint offset, uint size)>();

            reader.BaseStream.Seek(offsetToKeys, SeekOrigin.Begin);
            for (uint i = 0; i < entryCount; i++)
            {
                // ResRef: 16-byte ASCII, null-padded, flat (no path separators)
                string resref = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(16)).TrimEnd('\0');
                uint restype = reader.ReadUInt32(); // Resource type ID - NO filtering at container level
                uint resid = reader.ReadUInt32();
                uint offset = reader.ReadUInt32();
                uint size = reader.ReadUInt32();
                entries.Add((resref, restype, resid, offset, size));
            }

            foreach ((string resref, uint restype, uint resid, uint offset, uint size) entry in entries)
            {
                reader.BaseStream.Seek(entry.offset, SeekOrigin.Begin);
                byte[] data = reader.ReadBytes((int)entry.size);
                var resType = ResourceType.FromId((int)entry.restype);
                _resources.Add(new CapsuleResource(entry.resref, resType, data, (int)entry.size, (int)entry.offset, _path.ToString()));
            }
        }

        // REVERSE ENGINEERING NOTES (swkotor.exe / swkotor2.exe):
        // ERF/MOD container format structure (matches engine behavior):
        // - Header: "ERF V1.0" or "MOD V1.0" (8 bytes)
        // - Language count: uint32 (4 bytes)
        // - Localized string size: uint32 (4 bytes)
        // - Entry count: uint32 (4 bytes)
        // - Offset to localized strings: uint32 (4 bytes)
        // - Offset to keys: uint32 (4 bytes)
        // - Offset to resources: uint32 (4 bytes)
        // - Key entries: Each entry contains:
        //   * ResRef: 16-byte ASCII string (null-padded, NO path separators - subfolders NOT supported)
        //   * Resource ID: uint32 (4 bytes)
        //   * Resource Type: uint16 (2 bytes) - NO filtering, any type ID is accepted
        //   * Unused: uint16 (2 bytes)
        // - Resource entries: Offset and size for each resource
        // - Resource data: Raw bytes at specified offset
        //
        // Key Finding: Container format does NOT filter resource types.
        // The engine will load ANY resource type stored in an ERF/MOD file, as long as:
        // 1. The resource type ID is valid
        // 2. The resource data can be parsed by the appropriate loader
        //
        // Note: MOD files are ERF containers with "MOD V1.0" header. They follow the same format.
        private void LoadERF(System.IO.BinaryReader reader)
        {
            reader.BaseStream.Seek(8, SeekOrigin.Begin); // Skip header already read
            reader.ReadUInt32(); // language count
            reader.ReadUInt32(); // localized string size
            uint entryCount = reader.ReadUInt32();
            uint offsetToLocalizedStrings = reader.ReadUInt32();
            uint offsetToKeys = reader.ReadUInt32();
            uint offsetToResources = reader.ReadUInt32();

            var resrefs = new List<string>();
            var resids = new List<uint>();
            var restypes = new List<ushort>();

            reader.BaseStream.Seek(offsetToKeys, SeekOrigin.Begin);
            for (uint i = 0; i < entryCount; i++)
            {
                // ResRef: 16-byte ASCII, null-padded, flat (no path separators)
                string resref = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(16)).TrimEnd('\0');
                uint resid = reader.ReadUInt32();
                ushort restype = reader.ReadUInt16(); // Resource type ID - NO filtering at container level
                reader.ReadUInt16(); // unused
                resrefs.Add(resref);
                resids.Add(resid);
                restypes.Add(restype);
            }

            var resoffsets = new List<uint>();
            var ressizes = new List<uint>();

            reader.BaseStream.Seek(offsetToResources, SeekOrigin.Begin);
            for (uint i = 0; i < entryCount; i++)
            {
                resoffsets.Add(reader.ReadUInt32());
                ressizes.Add(reader.ReadUInt32());
            }

            for (int i = 0; i < entryCount; i++)
            {
                reader.BaseStream.Seek(resoffsets[i], SeekOrigin.Begin);
                byte[] data = reader.ReadBytes((int)ressizes[i]);
                var resType = ResourceType.FromId(restypes[i]);
                _resources.Add(new CapsuleResource(resrefs[i], resType, data, (int)ressizes[i], (int)resoffsets[i], _path.ToString()));
            }
        }

        public void Save()
        {
            if (_capsuleType == CapsuleType.RIM)
            {
                var rim = new Resource.Formats.RIM.RIM();
                foreach (CapsuleResource res in _resources)
                {
                    rim.SetData(res.ResName, res.ResType, res.Data);
                }
                var writer = new Resource.Formats.RIM.RIMBinaryWriter(rim);
                using (FileStream fs = File.Create(_path.GetResolvedPath()))
                {
                    writer.Write(fs);
                }
            }
            else
            {
                var erf = new Resource.Formats.ERF.ERF(_capsuleType == CapsuleType.MOD ? Resource.Formats.ERF.ERFType.MOD : Resource.Formats.ERF.ERFType.ERF, true);
                foreach (CapsuleResource res in _resources)
                {
                    erf.SetData(res.ResName, res.ResType, res.Data);
                }
                var writer = new Resource.Formats.ERF.ERFBinaryWriter(erf);
                using (FileStream fs = File.Create(_path.GetResolvedPath()))
                {
                    writer.Write(fs);
                }
            }
        }

        public void Add(string resname, ResourceType restype, byte[] data)
        {
            SetResource(resname, restype, data);
        }

        [CanBeNull]
        public byte[] GetResource(string resname, ResourceType restype)
        {
            // Can be null if resource not found
            CapsuleResource resource = _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
            return resource?.Data;
        }

        public void SetResource(string resname, ResourceType restype, byte[] data)
        {
            // Can be null if resource not found
            CapsuleResource existing = _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            if (existing != null)
            {
                _resources.Remove(existing);
            }

            _resources.Add(new CapsuleResource(resname, restype, data, data.Length, 0, _path.ToString()));
        }

        public bool Contains(string resname, ResourceType restype)
        {
            return _resources.Any(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
        }

        /// <summary>
        /// Removes a resource from the capsule.
        /// </summary>
        public bool RemoveResource(string resname, ResourceType restype)
        {
            // Can be null if resource not found
            CapsuleResource existing = _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);

            if (existing != null)
            {
                _resources.Remove(existing);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets information about a resource without loading the data.
        /// </summary>
        [CanBeNull]
        public CapsuleResource GetResourceInfo(string resname, ResourceType restype)
        {
            return _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resname, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
        }

        public List<CapsuleResource> GetResources() => new List<CapsuleResource>(_resources);

        public IEnumerator<CapsuleResource> GetEnumerator() => _resources.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Type of capsule file.
    /// </summary>
    public enum CapsuleType
    {
        ERF,
        RIM,
        MOD,
        SAV
    }

    /// <summary>
    /// Represents a resource within a capsule.
    /// </summary>
    public class CapsuleResource
    {
        public string ResName { get; }
        public ResourceType ResType { get; }
        public byte[] Data { get; }
        public int Size { get; }
        public int Offset { get; }
        public string FilePath { get; }

        public CapsuleResource(string resname, ResourceType restype, byte[] data, int size, int offset, string filepath)
        {
            ResName = resname;
            ResType = restype;
            Data = data;
            Size = size;
            Offset = offset;
            FilePath = filepath;
        }

        public ResourceIdentifier Identifier => new ResourceIdentifier(ResName, ResType);

        public override string ToString() => $"{ResName}.{ResType.Extension}";
    }
}

