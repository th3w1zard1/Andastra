using System;

namespace TSLPatcher.Core.Common;

/// <summary>
/// Represents a 3 dimensional vector.
/// </summary>
public struct Vector3 : IEquatable<Vector3>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => new(0f, 0f, 0f);
    public static Vector3 FromNull() => Zero;

    public static Vector3 FromVector2(Vector2 v)
    {
        return new Vector3(v.X, v.Y, 0f);
    }

    public void SetVectorCoords(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float Magnitude()
    {
        return MathF.Sqrt(X * X + Y * Y + Z * Z);
    }

    public Vector3 Normalize()
    {
        float magnitude = Magnitude();
        if (magnitude == 0)
        {
            X = 0;
            Y = 0;
            Z = 0;
        }
        else
        {
            X /= magnitude;
            Y /= magnitude;
            Z /= magnitude;
        }
        return this;
    }

    public Vector3 Normal()
    {
        Vector3 result = new(X, Y, Z);
        return result.Normalize();
    }

    public float Dot(Vector3 other)
    {
        return X * other.X + Y * other.Y + Z * other.Z;
    }

    public float Distance(Vector3 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public override string ToString() => $"{X} {Y} {Z}";

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
    }

    public bool Equals(Vector3 other)
    {
        const float epsilon = 1e-9f;
        return Math.Abs(X - other.X) < epsilon &&
               Math.Abs(Y - other.Y) < epsilon &&
               Math.Abs(Z - other.Z) < epsilon;
    }

    public static Vector3 operator +(Vector3 a, Vector3 b)
        => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b)
        => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 v, float scalar)
        => new(v.X * scalar, v.Y * scalar, v.Z * scalar);

    public static Vector3 operator /(Vector3 v, float scalar)
        => new(v.X / scalar, v.Y / scalar, v.Z / scalar);

    public static bool operator ==(Vector3 left, Vector3 right)
        => left.Equals(right);

    public static bool operator !=(Vector3 left, Vector3 right)
        => !left.Equals(right);
}

