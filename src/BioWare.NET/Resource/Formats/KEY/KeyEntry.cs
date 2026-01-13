using System;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/key_data.py:154-279
    // Original: class KeyEntry
    public class KeyEntry : IEquatable<KeyEntry>
    {
        public ResRef ResRef { get; set; }
        public ResourceType ResType { get; set; }
        public uint ResourceId { get; set; }

        public KeyEntry()
        {
            ResRef = ResRef.FromBlank();
            ResType = ResourceType.INVALID;
            ResourceId = 0;
        }

        public int BifIndex
        {
            get { return (int)(ResourceId >> 20); }
        }

        public int ResIndex
        {
            get { return (int)(ResourceId & 0xFFFFF); }
        }

        public override bool Equals(object obj)
        {
            return obj is KeyEntry other && Equals(other);
        }

        public bool Equals(KeyEntry other)
        {
            if (other == null)
            {
                return false;
            }
            return string.Equals(ResRef.ToString(), other.ResRef.ToString(), StringComparison.OrdinalIgnoreCase) &&
                   ResType == other.ResType &&
                   ResourceId == other.ResourceId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ResRef.ToString().ToLowerInvariant(), ResType, ResourceId);
        }

        public override string ToString()
        {
            return string.Concat(ResRef, ":", ResType.Name, "@", BifIndex, ":", ResIndex);
        }
    }
}

