using System;

namespace TSLPatcher.Core.Common;

/// <summary>
/// Represents a 2-dimensional vector.
/// </summary>
public struct Vector2 : IEquatable<Vector2>
{
    public float X { get; set; }
    public float Y { get; set; }

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => new(0f, 0f);

    public static Vector2 FromNull() => Zero;

    public static Vector2 FromAngle(float angle)
    {
        float x = MathF.Cos(angle);
        float y = MathF.Sin(angle);
        return new Vector2(x, y).Normalize();
    }

    public void SetData(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float Magnitude()
    {
        return MathF.Sqrt(X * X + Y * Y);
    }

    public Vector2 Normalize()
    {
        float magnitude = Magnitude();
        if (magnitude == 0)
        {
            X = 0;
            Y = 0;
        }
        else
        {
            X /= magnitude;
            Y /= magnitude;
        }
        return this;
    }

    public Vector2 Normal()
    {
        Vector2 result = new(X, Y);
        return result.Normalize();
    }

    public float Dot(Vector2 other)
    {
        return X * other.X + Y * other.Y;
    }

    public float Distance(Vector2 other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public float Angle()
    {
        return MathF.Atan2(Y, X);
    }

    public static Vector2 operator +(Vector2 a, Vector2 b)
    {
        return new Vector2(a.X + b.X, a.Y + b.Y);
    }

    public static Vector2 operator -(Vector2 a, Vector2 b)
    {
        return new Vector2(a.X - b.X, a.Y - b.Y);
    }

    public static Vector2 operator *(Vector2 a, float scalar)
    {
        return new Vector2(a.X * scalar, a.Y * scalar);
    }

    public static Vector2 operator /(Vector2 a, float scalar)
    {
        return new Vector2(a.X / scalar, a.Y / scalar);
    }

    public bool Equals(Vector2 other)
    {
        return Math.Abs(X - other.X) < float.Epsilon
            && Math.Abs(Y - other.Y) < float.Epsilon;
    }

    public override bool Equals(object? obj)
    {
        return obj is Vector2 vector && Equals(vector);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public override string ToString()
    {
        return $"Vector2({X}, {Y})";
    }

    public static bool operator ==(Vector2 left, Vector2 right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Vector2 left, Vector2 right)
    {
        return !left.Equals(right);
    }
}

