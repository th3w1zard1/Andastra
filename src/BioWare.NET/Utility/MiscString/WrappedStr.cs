using System;
using System.Reflection;
using JetBrains.Annotations;

namespace BioWare.NET.Utility.MiscString
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:9-15
    // Original: def is_string_like(obj: Any) -> bool:
    public static class StringUtil
    {
        public static bool IsStringLike(object obj)
        {
            try
            {
                var _ = obj + "";
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:18-94
    // Original: class WrappedStr(str):
    public class WrappedStr
    {
        protected string _content;

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:21-30
        // Original: def __init__(self, content: Self | str = ""):
        public WrappedStr(string content = "")
        {
            if (content == null)
            {
                throw new Exception($"Cannot initialize {GetType().Name}(None), expected a str-like argument");
            }
            _content = content;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:21-30
        // Original: def __init__(self, content: Self | str = ""):
        // Additional constructor to handle WrappedStr instances
        public WrappedStr(WrappedStr content)
        {
            if (content == null)
            {
                throw new Exception($"Cannot initialize {GetType().Name}(None), expected a str-like argument");
            }
            _content = content._content;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:32-33
        // Original: def __repr__(self):
        public override string ToString()
        {
            return $"WrappedStr({_content})";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:35-36
        // Original: def __reduce__(self):
        public object GetReduce()
        {
            return new object[] { GetType(), new object[] { _content } };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:59-69
        // Original: @classmethod def _assert_str_or_none_type(cls: type[Self], var: Any) -> str:
        protected static string AssertStrOrNoneType(Type cls, object var)
        {
            if (var == null)
            {
                return null;
            }
            if (!(var is string) && !(var != null && var.GetType() == cls))
            {
                throw new Exception($"Expected str-like, got '{var}' of type {var?.GetType()}");
            }
            return var?.ToString();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:72-80
        // Original: def removeprefix(self, __prefix: WrappedStr | str) -> Self:
        public WrappedStr RemovePrefix(WrappedStr prefix)
        {
            return RemovePrefix(prefix?._content ?? prefix?.ToString() ?? "");
        }

        public WrappedStr RemovePrefix(string prefix)
        {
            string parsedPrefix = AssertStrOrNoneType(GetType(), prefix);
            if (_content.StartsWith(parsedPrefix))
            {
                return (WrappedStr)Activator.CreateInstance(GetType(), _content.Substring(parsedPrefix.Length));
            }
            return (WrappedStr)Activator.CreateInstance(GetType(), _content);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:83-91
        // Original: def removesuffix(self, __suffix: WrappedStr | str) -> Self:
        public WrappedStr RemoveSuffix(WrappedStr suffix)
        {
            return RemoveSuffix(suffix?._content ?? suffix?.ToString() ?? "");
        }

        public WrappedStr RemoveSuffix(string suffix)
        {
            string parsedSuffix = AssertStrOrNoneType(GetType(), suffix);
            if (_content.EndsWith(parsedSuffix))
            {
                return (WrappedStr)Activator.CreateInstance(GetType(), _content.Substring(0, _content.Length - parsedSuffix.Length));
            }
            return (WrappedStr)Activator.CreateInstance(GetType(), _content);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/misc_string/mutable_str.py:92-93
        // Original: def __getstate__(self) -> str:
        public string GetState()
        {
            return _content;
        }

        // Forward all string operations to _content
        public static implicit operator string(WrappedStr wrapped)
        {
            return wrapped?._content ?? "";
        }

        public static implicit operator WrappedStr(string str)
        {
            return new WrappedStr(str);
        }

        public int Length => _content.Length;
        public char this[int index] => _content[index];
    }
}

