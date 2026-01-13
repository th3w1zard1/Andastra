using System;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource
{
    /// <summary>
    /// Represents a resource stored within a BioWare archive (ERF, RIM, BIF).
    ///
    /// Contains resource reference, type, and data. Used as the base resource type
    /// for archive-based resource storage.
    /// </summary>
    [PublicAPI]
    public class ArchiveResource
    {
        public ResRef ResRef { get; protected set; }
        public ResourceType ResType { get; protected set; }
        public byte[] Data { get; protected set; }

        public ArchiveResource(ResRef resref, ResourceType restype, byte[] data)
        {
            ResRef = resref ?? throw new ArgumentNullException(nameof(resref));
            ResType = restype ?? throw new ArgumentNullException(nameof(restype));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public ResourceIdentifier Identifier => new ResourceIdentifier(ResRef, ResType);

        public override bool Equals(object obj)
        {
            if (obj is ArchiveResource other)
            {
                return ResRef.Equals(other.ResRef) && ResType.Equals(other.ResType);
            }
            return false;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (ResRef.GetHashCode() * 397) ^ ResType.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{ResRef}.{ResType.Extension}";
        }
    }
}
