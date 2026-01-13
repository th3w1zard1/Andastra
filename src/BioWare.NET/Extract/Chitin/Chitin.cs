using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract.Chitin
{

    /// <summary>
    /// Chitin object is used for loading the list of resources stored in the chitin.key/.bif files used by the game.
    /// Chitin support is read-only and you cannot write your own key/bif files with this class yet.
    ///
    /// References:
    /// - vendor/PyKotor/Libraries/PyKotor/src/pykotor/extract/chitin.py (Chitin reading)
    /// - vendor/reone/src/libs/resource/format/keyreader.cpp:26-65 (KEY reading)
    /// - vendor/reone/src/libs/resource/format/bifreader.cpp:26-63 (BIF reading)
    /// - vendor/xoreos-tools/src/unkeybif.cpp (KEY/BIF extraction tool)
    /// </summary>
    public class Chitin : IEnumerable<FileResource>
    {
        private const int KEY_ELEMENT_SIZE = 8;

        private readonly string _keyPath;
        private readonly string _basePath;
        private readonly BioWareGame? _game;

        private List<FileResource> _resources;
        private Dictionary<string, List<FileResource>> _resourceDict;

        public string KeyPath => _keyPath;
        public string BasePath => _basePath;
        public BioWareGame? Game => _game;
        public int Count => _resources.Count;

        public Chitin(string keyPath, [CanBeNull] string basePath = null, BioWareGame? game = null)
        {
            _keyPath = keyPath ?? throw new ArgumentNullException(nameof(keyPath));
            _basePath = basePath ?? Path.GetDirectoryName(keyPath) ?? throw new ArgumentException("Cannot determine base path", nameof(keyPath));
            _game = game;

            _resources = new List<FileResource>();
            _resourceDict = new Dictionary<string, List<FileResource>>(StringComparer.OrdinalIgnoreCase);

            Load();
        }

        /// <summary>
        /// Reload the list of resource info linked from the chitin.key file.
        /// </summary>
        public void Load()
        {
            _resources.Clear();
            _resourceDict.Clear();

            (Dictionary<uint, string> keys, List<string> bifs) = GetChitinData();

            foreach (string bif in bifs)
            {
                _resourceDict[bif] = new List<FileResource>();
                string absoluteBifPath = Path.Combine(_basePath, bif);

                // For iOS, chitin.key references .bif but actual files are .bzf
                if (_game?.IsIOS() == true)
                {
                    absoluteBifPath = Path.ChangeExtension(absoluteBifPath, ".bzf");
                }

                ReadBif(absoluteBifPath, keys, bif);
            }
        }

        /// <summary>
        /// Reads resources from a BIF file.
        /// References: vendor/reone/src/libs/resource/format/bifreader.cpp:26-63
        /// </summary>
        private void ReadBif(string bifPath, Dictionary<uint, string> keys, string bifFilename)
        {
            if (!File.Exists(bifPath))
            {
                // BIF file doesn't exist, skip it
                return;
            }

            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(bifPath))
            {
                string bifFileType = reader.ReadString(4);     // 0x0
                string bifFileVersion = reader.ReadString(4);  // 0x4
                uint resourceCount = reader.ReadUInt32();      // 0x8
                uint fixedResourceCount = reader.ReadUInt32(); // 0xC - always 0x00000000?
                uint resourceOffset = reader.ReadUInt32();     // 0x10 - always 0x14 (dec 20)

                reader.Seek((int)resourceOffset); // Skip to 0x14

                // vendor/reone/src/libs/resource/format/bifreader.cpp:50-63
                for (uint i = 0; i < resourceCount; i++)
                {
                    uint resId = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    uint restypeId = reader.ReadUInt32();

                    if (!keys.TryGetValue(resId, out string resname))
                    {
                        // Resource ID not found in keys, skip it
                        continue;
                    }

                    var restype = ResourceType.FromId((int)restypeId);
                    var resource = new FileResource(
                        resname,
                        restype,
                        (int)size,
                        (int)offset,
                        bifPath
                    );

                    _resources.Add(resource);
                    _resourceDict[bifFilename].Add(resource);
                }
            }
        }

        /// <summary>
        /// Parses the chitin.key file to get BIF filenames and resource keys.
        /// References: vendor/reone/src/libs/resource/format/keyreader.cpp:26-65
        /// </summary>
        private (Dictionary<uint, string> keys, List<string> bifs) GetChitinData()
        {
            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_keyPath))
            {
                // Read header
                reader.Skip(8); // file type + version
                uint bifCount = reader.ReadUInt32();
                uint keyCount = reader.ReadUInt32();
                uint fileTableOffset = reader.ReadUInt32();
                reader.Skip(4); // key table offset

                // vendor/reone/src/libs/resource/format/keyreader.cpp:38-59
                var files = new List<(uint offset, ushort length)>();
                reader.Seek((int)fileTableOffset);

                for (uint i = 0; i < bifCount; i++)
                {
                    reader.Skip(4); // mystery field (0x000696E0 in K1, 0x000DDD8A in K2)
                    uint fileOffset = reader.ReadUInt32();
                    ushort fileLength = reader.ReadUInt16();
                    reader.Skip(2); // mystery field (0x0001 in K1, 0x0000 in K2)
                    files.Add((fileOffset, fileLength));
                }

                var bifs = new List<string>();
                foreach ((uint offset, ushort length) in files)
                {
                    reader.Seek((int)offset);
                    string bif = reader.ReadString(length);

                    // Normalize path separators for Windows
                    bif = bif.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    bifs.Add(bif);
                }

                // vendor/reone/src/libs/resource/format/keyreader.cpp:61-68
                var keys = new Dictionary<uint, string>();
                for (uint i = 0; i < keyCount; i++)
                {
                    string resref = reader.ReadString(16);
                    reader.Skip(2); // restype_id uint16
                    uint resId = reader.ReadUInt32();
                    keys[resId] = resref;
                }

                return (keys, bifs);
            }
        }

        /// <summary>
        /// Returns the bytes data of the specified resource.
        /// If the resource does not exist then returns None instead.
        /// </summary>
        [CanBeNull]
        public byte[] GetResource(string resref, ResourceType restype)
        {
            var query = new ResourceIdentifier(resref, restype);
            // Can be null if resource not found
            FileResource resource = _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resref, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
            return resource?.GetData();
        }

        /// <summary>
        /// Returns the FileResource metadata for the specified resource.
        /// </summary>
        [CanBeNull]
        public FileResource GetResourceInfo(string resref, ResourceType restype)
        {
            return _resources.FirstOrDefault(r =>
                string.Equals(r.ResName, resref, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == restype);
        }

        /// <summary>
        /// Checks if a resource exists in the chitin.
        /// </summary>
        public bool Contains(string resref, ResourceType restype)
        {
            return GetResourceInfo(resref, restype) != null;
        }

        /// <summary>
        /// Gets all resources from a specific BIF file.
        /// </summary>
        public List<FileResource> GetBifResources(string bifFilename)
        {
            // Can be null if resources not found
            if (_resourceDict.TryGetValue(bifFilename, out List<FileResource> resources))
            {
                return new List<FileResource>(resources);
            }
            return new List<FileResource>();
        }

        /// <summary>
        /// Gets all BIF filenames referenced by this chitin.key.
        /// </summary>
        public List<string> GetBifFilenames()
        {
            return _resourceDict.Keys.ToList();
        }

        /// <summary>
        /// Gets all resources.
        /// </summary>
        public List<FileResource> GetResources()
        {
            return new List<FileResource>(_resources);
        }

        public IEnumerator<FileResource> GetEnumerator() => _resources.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

