using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.BIF
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:201-575
    // Original: class BIF(BiowareArchive):
    /// <summary>
    /// Represents a BIF/BZF file in the Aurora engine.
    /// 
    /// BIF (Binary Index Format) files are the primary data containers for KotOR game resources.
    /// They store thousands of game assets (models, textures, scripts, etc.) in a single file.
    /// BIF files work in conjunction with KEY files: the BIF contains the data and resource IDs,
    /// while the KEY file maps filenames (ResRefs) to resource IDs and BIF locations.
    /// </summary>
    public class BIF : IEnumerable<BIFResource>
    {
        // vendor/reone/src/libs/resource/format/bifreader.cpp:27-42
        // vendor/Kotor.NET/Kotor.NET/Formats/KotorBIF/BIFBinaryStructure.cs:41-47
        // vendor/KotOR_IO/KotOR_IO/File Formats/BIF.cs:46-51
        public const int HeaderSize = 20; // Fixed header size
        public const int VarEntrySize = 16; // Size of each variable resource entry
        public const int FixEntrySize = 16; // Size of each fixed resource entry
        public const string FileVersion = "V1  "; // BIF file format version

        // vendor/reone/src/libs/resource/format/bifreader.cpp:48-76
        // File type (BIF vs BZF determines compression)
        public BIFType BifType { get; set; }

        // vendor/reone/include/reone/resource/format/bifreader.h:52
        // vendor/Kotor.NET/Kotor.NET/Formats/KotorBIF/BIFBinaryStructure.cs:18
        // vendor/KotOR_IO/KotOR_IO/File Formats/BIF.cs:96
        // List of all resources in file (ordered)
        private readonly List<BIFResource> _resources = new List<BIFResource>();

        // PyKotor optimization: ResRef+Type -> Resource lookup (O(1) access)
        private readonly Dictionary<ResourceIdentifier, BIFResource> _resourceDict = new Dictionary<ResourceIdentifier, BIFResource>();

        // PyKotor optimization: Resource ID -> Resource lookup (for KEY coordination)
        private readonly Dictionary<int, BIFResource> _idLookup = new Dictionary<int, BIFResource>();

        // Modification tracking flag
        private bool _modified;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:269-292
        // Original: def __init__(self, bif_type: BIFType = BIFType.BIF):
        public BIF(BIFType bifType = BIFType.BIF)
        {
            BifType = bifType;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:294-297
        // Original: @property def resources(self) -> list[BIFResource]:
        public List<BIFResource> Resources => _resources;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:299-302
        // Original: @property def var_count(self) -> int:
        public int VarCount => _resources.Count;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:304-307
        // Original: @property def fixed_count(self) -> int:
        public int FixedCount => 0; // Currently no fixed resources supported

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:328-343
        // Original: def set_data(self, resref: ResRef, restype: ResourceType, data: bytes, res_id: int | None = None) -> BIFResource:
        public BIFResource SetData(ResRef resref, ResourceType restype, byte[] data, int? resId = null)
        {
            var resource = new BIFResource(resref, restype, data);
            if (resId.HasValue)
            {
                resource.ResnameKeyIndex = resId.Value;
            }
            _resources.Add(resource);
            _resourceDict[new ResourceIdentifier(resref.ToString(), restype)] = resource;
            _idLookup[resource.ResnameKeyIndex] = resource;
            _modified = true;
            return resource;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:345-357
        // Original: def remove_resource(self, resource: BIFResource) -> None:
        public void RemoveResource(BIFResource resource)
        {
            if (!_resources.Remove(resource))
            {
                throw new ArgumentException($"Resource '{resource}' not found in BIF", nameof(resource));
            }
            _resourceDict.Remove(new ResourceIdentifier(resource.ResRef.ToString(), resource.ResType));
            _idLookup.Remove(resource.ResnameKeyIndex);
            _modified = true;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:359-369
        // Original: def reorder_resources(self, new_order: list[BIFResource]) -> None:
        public void ReorderResources(List<BIFResource> newOrder)
        {
            if (!new HashSet<BIFResource>(_resources).SetEquals(newOrder))
            {
                throw new ArgumentException("New order must contain exactly the same resources", nameof(newOrder));
            }
            _resources.Clear();
            _resources.AddRange(newOrder);
            BuildLookupTables();
            _modified = true;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:371-376
        // Original: def get_resource_by_id(self, resource_id: int) -> BIFResource | None:
        [CanBeNull]
        public BIFResource GetResourceById(int resourceId)
        {
            return _idLookup.TryGetValue(resourceId, out BIFResource resource) ? resource : null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:378-383
        // Original: def get_resources_by_type(self, restype: ResourceType) -> list[BIFResource]:
        public List<BIFResource> GetResourcesByType(ResourceType restype)
        {
            return _resources.Where(res => res.ResType == restype).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:385-394
        // Original: def try_get_resource(self, resref: str | ResRef, restype: ResourceType) -> tuple[bool, BIFResource | None]:
        public (bool found, BIFResource resource) TryGetResource(string resref, ResourceType restype)
        {
            string lowerResref = resref?.ToLowerInvariant() ?? "";
            ResourceIdentifier key = new ResourceIdentifier(lowerResref, restype);
            bool found = _resourceDict.TryGetValue(key, out BIFResource resource);
            return (found, found ? resource : null);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:396-402
        // Original: def build_lookup_tables(self) -> None:
        public void BuildLookupTables()
        {
            _resourceDict.Clear();
            _idLookup.Clear();
            foreach (BIFResource resource in _resources)
            {
                _resourceDict[new ResourceIdentifier(resource.ResRef.ToString(), resource.ResType)] = resource;
                _idLookup[resource.ResnameKeyIndex] = resource;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:404-407
        // Original: @property def is_compressed(self) -> bool:
        public bool IsCompressed => BifType == BIFType.BZF;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:409-412
        // Original: @property def is_modified(self) -> bool:
        public bool IsModified => _modified;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:567-574
        // Original: def reorganize_by_type(self) -> None:
        public void ReorganizeByType()
        {
            _resources.Sort((r1, r2) =>
            {
                int typeCompare = r1.ResType.TypeId.CompareTo(r2.ResType.TypeId);
                return typeCompare != 0 ? typeCompare : r1.ResnameKeyIndex.CompareTo(r2.ResnameKeyIndex);
            });
            _modified = true;
            BuildLookupTables();
        }

        public IEnumerator<BIFResource> GetEnumerator() => _resources.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}


