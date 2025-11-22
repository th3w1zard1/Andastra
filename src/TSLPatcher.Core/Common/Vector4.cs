using System;

namespace TSLPatcher.Core.Common;

/// <summary>
/// Represents a 4 dimensional vector.
/// </summary>
public struct Vector4 : IEquatable<Vector4>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Vector4 Identity => new(0, 0, 0, 1);
    public static Vector4 FromNull() => new(0f, 0f, 0f, 0f);

    public static Vector4 FromVector2(Vector2 v)
    {
        return new Vector4(v.X, v.Y, 0f, 0f);
    }

    public static Vector4 FromVector3(Vector3 v)
    {
        return new Vector4(v.X, v.Y, v.Z, 0f);
    }

    /// <summary>
    /// Decompresses a packed quaternion from a 32-bit integer.
    /// KotOR uses compressed quaternions for orientation controllers.
    /// </summary>
    public static Vector4 FromCompressed(uint data)
    {
        // X component: bits 0-10 (11 bits, mask 0x7FF = 2047)
        float x = ((data & 0x7FF) / 1023.0f) - 1.0f;

        // Y component: bits 11-21 (11 bits)
        float y = (((data >> 11) & 0x7FF) / 1023.0f) - 1.0f;

        // Z component: bits 22-31 (10 bits)
        float z = ((data >> 22) / 511.0f) - 1.0f;

        // Calculate W from quaternion unit constraint
        float temp = x * x + y * y + z * z;
        float w;
        if (temp < 1.0f)
        {
            w = MathF.Sqrt(1.0f - temp);
        }
        else
        {
            float sqrtTemp = MathF.Sqrt(temp);
            x /= sqrtTemp;
            y /= sqrtTemp;
            z /= sqrtTemp;
            w = 0;
        }

        return new Vector4(x, y, z, w);
    }

    /// <summary>
    /// Compresses this quaternion into a 32-bit integer.
    /// </summary>
    public uint ToCompressed()
    {
        // Clamp values to valid range
        float x = Math.Clamp(X, -1.0f, 1.0f);
        float y = Math.Clamp(Y, -1.0f, 1.0f);
        float z = Math.Clamp(Z, -1.0f, 1.0f);

        // Map from [-1, 1] to integer ranges and pack
        uint xPacked = (uint)((x + 1.0f) * 1023.0f) & 0x7FF;
        uint yPacked = (uint)((y + 1.0f) * 1023.0f) & 0x7FF;
        uint zPacked = (uint)((z + 1.0f) * 511.0f) & 0x3FF;

        return xPacked | (yPacked << 11) | (zPacked << 22);
    }

    /// <summary>
    /// Creates a Vector4 quaternion from Euler angles (in radians).
    /// </summary>
    public static Vector4 FromEuler(float roll, float pitch, float yaw)
    {
        float qx = MathF.Sin(roll / 2) * MathF.Cos(pitch / 2) * MathF.Cos(yaw / 2)
                 - MathF.Cos(roll / 2) * MathF.Sin(pitch / 2) * MathF.Sin(yaw / 2);
        float qy = MathF.Cos(roll / 2) * MathF.Sin(pitch / 2) * MathF.Cos(yaw / 2)
                 + MathF.Sin(roll / 2) * MathF.Cos(pitch / 2) * MathF.Sin(yaw / 2);
        float qz = MathF.Cos(roll / 2) * MathF.Cos(pitch / 2) * MathF.Sin(yaw / 2)
                 - MathF.Sin(roll / 2) * MathF.Sin(pitch / 2) * MathF.Cos(yaw / 2);
        float qw = MathF.Cos(roll / 2) * MathF.Cos(pitch / 2) * MathF.Cos(yaw / 2)
                 + MathF.Sin(roll / 2) * MathF.Sin(pitch / 2) * MathF.Sin(yaw / 2);

        return new Vector4(qx, qy, qz, qw);
    }

    /// <summary>
    /// Converts this quaternion to Euler angles.
    /// </summary>
    public Vector3 ToEuler()
    {
        float t0 = 2.0f * (W * X + Y * Z);
        float t1 = 1 - 2 * (X * X + Y * Y);
        float roll = MathF.Atan2(t0, t1);

        float t2 = 2 * (W * Y - Z * X);
        t2 = Math.Clamp(t2, -1.0f, 1.0f);
        float pitch = MathF.Asin(t2);

        float t3 = 2 * (W * Z + X * Y);
        float t4 = 1 - 2 * (Y * Y + Z * Z);
        float yaw = MathF.Atan2(t3, t4);

        return new Vector3(roll, pitch, yaw);
    }

    public void SetVectorCoords(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public float Magnitude()
    {
        return MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);
    }

    public Vector4 Normalize()
    {
        float magnitude = Magnitude();
        if (magnitude == 0)
        {
            X = 0;
            Y = 0;
            Z = 0;
            W = 0;
        }
        else
        {
            X /= magnitude;
            Y /= magnitude;
            Z /= magnitude;
            W /= magnitude;
        }
        return this;
    }

    public override string ToString() => $"{X} {Y} {Z} {W}";

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override bool Equals(object? obj)
    {
        return obj is Vector4 other && Equals(other);
    }

    public bool Equals(Vector4 other)
    {
        const float epsilon = 1e-9f;
        return Math.Abs(X - other.X) < epsilon &&
               Math.Abs(Y - other.Y) < epsilon &&
               Math.Abs(Z - other.Z) < epsilon &&
               Math.Abs(W - other.W) < epsilon;
    }

    public static Vector4 operator +(Vector4 a, Vector4 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Vector4 operator -(Vector4 a, Vector4 b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Vector4 operator *(Vector4 v, float scalar)
        => new(v.X * scalar, v.Y * scalar, v.Z * scalar, v.W * scalar);

    public static Vector4 operator /(Vector4 v, float scalar)
        => new(v.X / scalar, v.Y / scalar, v.Z / scalar, v.W / scalar);

    public static bool operator ==(Vector4 left, Vector4 right)
        => left.Equals(right);

    public static bool operator !=(Vector4 left, Vector4 right)
        => !left.Equals(right);
}

