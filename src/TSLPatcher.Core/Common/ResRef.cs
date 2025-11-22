using System;
using System.Linq;
using System.Text;

namespace TSLPatcher.Core.Common;

/// <summary>
/// A string reference to a game resource.
/// ResRefs are the names of resources without the extension (the file stem).
/// Restrictions: ASCII only, max 16 characters, case-insensitive.
/// </summary>
public class ResRef : IEquatable<ResRef>
{
    public const int MaxLength = 16;
    private const string InvalidCharacters = "<>:\"/\\|?*";

    private string _value = string.Empty;

    public ResRef(string text)
    {
        SetData(text);
    }

    public static ResRef FromBlank() => new(string.Empty);

    public static ResRef FromPath(string filePath)
    {
        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        return new ResRef(fileName);
    }

    public static bool IsValid(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text != text.Trim())
        {
            return false;
        }

        if (text.Length > MaxLength)
        {
            return false;
        }

        if (!IsAscii(text))
        {
            return false;
        }

        return !InvalidCharacters.Any(c => text.Contains(c));
    }

    private static bool IsAscii(string text)
    {
        return text.All(c => c < 128);
    }

    public void SetData(string text, bool truncate = false)
    {
        text = text?.Trim() ?? string.Empty;

        if (!IsAscii(text))
        {
            throw new InvalidEncodingException($"'{text}' must only contain ASCII characters.");
        }

        if (text.Length > MaxLength)
        {
            if (truncate)
            {
                text = text.Substring(0, MaxLength);
            }
            else
            {
                throw new ExceedsMaxLengthException($"Length of '{text}' ({text.Length} characters) exceeds the maximum allowed length ({MaxLength})");
            }
        }

        if (InvalidCharacters.Any(c => text.Contains(c)))
        {
            throw new InvalidFormatException("ResRefs must conform to Windows filename requirements.");
        }

        _value = text;
    }

    public override string ToString() => _value;

    public override int GetHashCode() => _value.ToLower().GetHashCode();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj switch
        {
            ResRef other => Equals(other),
            string str => _value.Equals(str, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    public bool Equals(ResRef? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return _value.Equals(other._value, StringComparison.OrdinalIgnoreCase);
    }

    public static bool operator ==(ResRef? left, ResRef? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(ResRef? left, ResRef? right)
    {
        return !(left == right);
    }

    public static implicit operator string(ResRef resRef) => resRef.ToString();

    // Exception classes
    public class InvalidFormatException : ArgumentException
    {
        public InvalidFormatException(string message) : base(message) { }
    }

    public class InvalidEncodingException : ArgumentException
    {
        public InvalidEncodingException(string message) : base(message) { }
    }

    public class ExceedsMaxLengthException : ArgumentException
    {
        public ExceedsMaxLengthException(string message) : base(message) { }
    }
}

