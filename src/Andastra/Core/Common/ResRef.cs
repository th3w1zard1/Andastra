using System;
using System.Linq;
using System.Text;
using Andastra.Core.Common;
using JetBrains.Annotations;

namespace Andastra.Core.Common
{

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/misc.py:23-248
    // Original: class ResRef(str):
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

        public static ResRef FromBlank() => new ResRef(string.Empty);

        public static ResRef FromString(string text) => new ResRef(text);

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

            // Must be trimmed (no leading/trailing whitespace)
            if (text != text.Trim())
            {
                return false;
            }

            // Strict ASCII validation
            if (!IsAscii(text))
            {
                return false;
            }

            // Strict maximum length validation (16 characters)
            if (text.Length > MaxLength)
            {
                return false;
            }

            // Must not contain invalid characters
            return !InvalidCharacters.Any(c => text.Contains(c));
        }

        /// <summary>
        /// Validates that the string contains only ASCII characters (strict enforcement).
        /// ASCII characters have values 0-127.
        /// </summary>
        private static bool IsAscii(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return true;
            }
            // Strict ASCII: all characters must be in range 0-127
            return text.All(c => c >= 0 && c <= 127);
        }

        public void SetData(string text, bool truncate = false)
        {
            text = text?.Trim() ?? string.Empty;

            // Strict ASCII validation - must be checked first
            if (!IsAscii(text))
            {
                throw new InvalidEncodingException($"'{text}' must only contain ASCII characters.");
            }

            // Strict maximum length validation (16 characters)
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

            // Check for invalid characters (Windows filename restrictions)
            if (InvalidCharacters.Any(c => text.Contains(c)))
            {
                throw new InvalidFormatException("ResRefs must conform to Windows filename requirements.");
            }

            _value = text;
        }

        public override string ToString() => _value;

        public bool IsBlank() => string.IsNullOrEmpty(_value);

        public override int GetHashCode() => _value.ToLower().GetHashCode();

        public override bool Equals([CanBeNull] object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is ResRef other)
            {
                return Equals(other);
            }
            if (obj is string str)
            {
                return _value.Equals(str, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public bool Equals([CanBeNull] ResRef other)
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

        public static bool operator ==([CanBeNull] ResRef left, [CanBeNull] ResRef right)
        {
            if (left is null)
            {
                return right is null;
            }

            return left.Equals(right);
        }

        public static bool operator !=([CanBeNull] ResRef left, [CanBeNull] ResRef right)
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
}
