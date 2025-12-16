using System;
using System.Numerics;
using BioWareCSharp.Common;

namespace BioWareCSharp.Common.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1426-1531
    // Original: class BWMFace(Face, ComparableMixin)
    public class BWMFace : Face
    {
        public int? Trans1 { get; set; }
        public int? Trans2 { get; set; }
        public int? Trans3 { get; set; }

        public BWMFace(Vector3 v1, Vector3 v2, Vector3 v3) : base(v1, v2, v3)
        {
            Trans1 = null;
            Trans2 = null;
            Trans3 = null;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMFace other))
            {
                return false;
            }
            bool parentEq = base.Equals(other);
            return parentEq && Trans1 == other.Trans1 && Trans2 == other.Trans2 && Trans3 == other.Trans3;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), Trans1, Trans2, Trans3);
        }
    }
}

