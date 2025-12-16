using System;

namespace AuroraEngine.Common.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1710-1762
    // Original: class BWMAdjacency(ComparableMixin)
    public class BWMAdjacency
    {
        public BWMFace Face { get; set; }
        public int Edge { get; set; }

        public BWMAdjacency(BWMFace face, int index)
        {
            Face = face;
            Edge = index;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMAdjacency other))
            {
                return false;
            }
            return Face == other.Face && Edge == other.Edge;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Face, Edge);
        }
    }
}

