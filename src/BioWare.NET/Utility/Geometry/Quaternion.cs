using System;
using JetBrains.Annotations;


namespace BioWare.NET.Utility.Geometry
{
    /// <summary>
    /// Represents a quaternion for 3D rotations.
    /// </summary>
    [PublicAPI]
    public struct Quaternion : IEquatable<Quaternion>
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Quaternion Identity => new Quaternion(0f, 0f, 0f, 1f);
        public static Quaternion FromNull() => Identity;

        public override bool Equals(object obj)
        {
            return obj is Quaternion quaternion && Equals(quaternion);
        }

        public bool Equals(Quaternion other)
        {
            return Math.Abs(X - other.X) < float.Epsilon
                   && Math.Abs(Y - other.Y) < float.Epsilon
                   && Math.Abs(Z - other.Z) < float.Epsilon
                   && Math.Abs(W - other.W) < float.Epsilon;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = X.GetHashCode();
                hashCode = (hashCode * 397) ^ Y.GetHashCode();
                hashCode = (hashCode * 397) ^ Z.GetHashCode();
                hashCode = (hashCode * 397) ^ W.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(Quaternion left, Quaternion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Quaternion left, Quaternion right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"Quaternion(X={X}, Y={Y}, Z={Z}, W={W})";
        }
    }
}
