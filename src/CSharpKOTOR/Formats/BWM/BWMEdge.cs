using System;

namespace AuroraEngine.Common.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1765-1843
    // Original: class BWMEdge(ComparableMixin)
    public class BWMEdge
    {
        public BWMFace Face { get; set; }
        public int Index { get; set; }
        public int Transition { get; set; }
        public bool Final { get; set; }

        public BWMEdge(BWMFace face, int index, int transition, bool final = false)
        {
            Face = face;
            Index = index;
            Transition = transition;
            Final = final;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMEdge other))
            {
                return false;
            }
            return Face == other.Face && Index == other.Index && Transition == other.Transition && Final == other.Final;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Face, Index, Transition, Final);
        }
    }
}

