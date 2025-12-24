using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.PCC
{
    /// <summary>
    /// Represents the data of a PCC/UPK (Unreal Engine 3 Package) file.
    /// </summary>
    /// <remarks>
    /// PCC/UPK Package Format:
    /// - Based on Unreal Engine 3 package format used by Eclipse Engine games (Dragon Age, )
    /// - PCC: Package file containing cooked content (textures, meshes, scripts, etc.)
    /// - UPK: Unreal Package file (same format as PCC, different extension)
    /// - Package structure: Header, Name Table, Import Table, Export Table, Data Chunks
    /// - Resources are stored as exports in the Export Table
    /// - Each export has a name, type, and data offset/size
    /// - Based on Unreal Engine 3 package format specification
    /// - Used by Dragon Age: Origins, Dragon Age 2, ,  2
    /// </remarks>
    public class PCC : IEnumerable<PCCResource>
    {
        private readonly List<PCCResource> _resources = new List<PCCResource>();
        private readonly Dictionary<ResourceIdentifier, PCCResource> _resourceDict = new Dictionary<ResourceIdentifier, PCCResource>();

        public PCCType PackageType { get; set; }
        public int PackageVersion { get; set; }
        public int LicenseeVersion { get; set; }
        public int EngineVersion { get; set; }
        public int CookerVersion { get; set; }

        public PCC(PCCType packageType = PCCType.PCC)
        {
            PackageType = packageType;
        }

        public int Count => _resources.Count;

        /// <summary>
        /// Gets a resource by index or ResourceIdentifier.
        /// </summary>
        public PCCResource this[int index] => _resources[index];

        /// <summary>
        /// Gets a resource by ResourceIdentifier or resname string.
        /// </summary>
        public PCCResource this[string resname]
        {
            get
            {
                string lowerResname = resname.ToLowerInvariant();
                ResourceIdentifier key = _resourceDict.Keys.FirstOrDefault(k =>
                    k.ResName.ToLowerInvariant() == lowerResname);

                if (key != null && _resourceDict.TryGetValue(key, out PCCResource resource))
                {
                    return resource;
                }

                throw new KeyNotFoundException($"{resname} not found in PCC");
            }
        }

        /// <summary>
        /// Gets a resource by ResourceIdentifier.
        /// </summary>
        public PCCResource this[ResourceIdentifier identifier]
        {
            get
            {
                if (_resourceDict.TryGetValue(identifier, out PCCResource resource))
                {
                    return resource;
                }
                throw new KeyNotFoundException($"{identifier} not found in PCC");
            }
        }

        public void SetData(string resname, ResourceType restype, byte[] data)
        {
            var ident = new ResourceIdentifier(resname, restype);
            var resref = new ResRef(ident.ResName);

            if (_resourceDict.TryGetValue(ident, out PCCResource resource))
            {
                // Update existing resource
                resource.ResRef = resref;
                resource.ResType = restype;
                resource.Data = data;
            }
            else
            {
                // Create new resource
                resource = new PCCResource(resref, restype, data);
                _resources.Add(resource);
                _resourceDict[ident] = resource;
            }
        }

        [CanBeNull]
        public byte[] Get(string resname, ResourceType restype)
        {
            var ident = new ResourceIdentifier(resname, restype);
            return _resourceDict.TryGetValue(ident, out PCCResource resource) ? resource.Data : null;
        }

        public void Remove(string resname, ResourceType restype)
        {
            var ident = new ResourceIdentifier(resname, restype);
            if (_resourceDict.TryGetValue(ident, out PCCResource resource))
            {
                _resources.Remove(resource);
                _resourceDict.Remove(ident);
            }
        }

        public List<PCCResource> GetResources() => _resources.ToList();

        public IEnumerator<PCCResource> GetEnumerator()
        {
            foreach (PCCResource resource in _resources)
            {
                yield return new PCCResource(resource.ResRef, resource.ResType, resource.Data);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override bool Equals(object obj)
        {
            if (obj is PCC other)
            {
                return _resources.ToHashSet().SetEquals(other._resources);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (PCCResource resource in _resources.OrderBy(r => r.GetHashCode()))
            {
                hash.Add(resource);
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Represents a single resource within a PCC/UPK archive.
    /// </summary>
    public class PCCResource
    {
        public ResRef ResRef { get; set; }
        public ResourceType ResType { get; set; }
        public byte[] Data { get; set; }

        public PCCResource(ResRef resref, ResourceType restype, byte[] data)
        {
            ResRef = resref;
            ResType = restype;
            Data = data is byte[]? data : data.ToArray();
        }

        public ResourceIdentifier Identifier() => new ResourceIdentifier(ResRef.ToString(), ResType);

        public override bool Equals(object obj)
        {
            if (obj is PCCResource other)
            {
                return ResRef.Equals(other.ResRef) &&
                       ResType == other.ResType &&
                       Data.SequenceEqual(other.Data);
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

    /// <summary>
    /// The type of PCC/UPK package file.
    /// </summary>
    public enum PCCType
    {
        /// <summary>
        /// PCC package file (cooked content)
        /// </summary>
        PCC,

        /// <summary>
        /// UPK package file (Unreal Package)
        /// </summary>
        UPK
    }
}

