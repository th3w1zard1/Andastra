using System;
using System.Numerics;
using AuroraEngine.Common.Common;

namespace AuroraEngine.Common.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1544-1707
    // Original: class BWMNodeAABB(ComparableMixin)
    public class BWMNodeAABB
    {
        public Vector3 BbMin { get; set; }
        public Vector3 BbMax { get; set; }
        public BWMFace Face { get; set; }
        public BWMMostSignificantPlane Sigplane { get; set; }
        public BWMNodeAABB Left { get; set; }
        public BWMNodeAABB Right { get; set; }

        public BWMNodeAABB(Vector3 bbMin, Vector3 bbMax, BWMFace face, int sigplane, BWMNodeAABB left, BWMNodeAABB right)
        {
            BbMin = bbMin;
            BbMax = bbMax;
            Face = face;
            Sigplane = (BWMMostSignificantPlane)sigplane;
            Left = left;
            Right = right;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is BWMNodeAABB other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return BbMin.Equals(other.BbMin) &&
                   BbMax.Equals(other.BbMax) &&
                   Face == other.Face &&
                   Sigplane == other.Sigplane &&
                   Left == other.Left &&
                   Right == other.Right;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BbMin, BbMax, Face, Sigplane, Left, Right);
        }
    }
}

