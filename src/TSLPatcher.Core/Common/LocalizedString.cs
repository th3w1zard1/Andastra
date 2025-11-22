using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TSLPatcher.Core.Common;

/// <summary>
/// Localized strings are a way of the game handling strings that need to be catered to a specific language or gender.
/// This is achieved through either referencing a entry in the 'dialog.tlk' or by directly providing strings for each language.
/// </summary>
public class LocalizedString : IEquatable<LocalizedString>, IEnumerable<(Language, Gender, string)>
{
    /// <summary>
    /// An index into the 'dialog.tlk' file. If this value is -1 the game will use the stored substrings.
    /// </summary>
    public int StringRef { get; set; }

    private readonly Dictionary<int, string> _substrings = new();

    public LocalizedString(int stringRef, Dictionary<int, string>? substrings = null)
    {
        StringRef = stringRef;
        if (substrings != null)
        {
            foreach (var (key, value) in substrings)
            {
                _substrings[key] = value;
            }
        }
    }

    public static LocalizedString FromInvalid() => new(-1);

    public static LocalizedString FromEnglish(string text)
    {
        var locString = new LocalizedString(-1);
        locString.SetData(Language.English, Gender.Male, text);
        return locString;
    }

    /// <summary>
    /// Computes the substring ID from language and gender.
    /// </summary>
    public static int SubstringId(Language language, Gender gender)
    {
        return (int)language * 2 + (int)gender;
    }

    /// <summary>
    /// Extracts language and gender from a substring ID.
    /// </summary>
    public static (Language, Gender) SubstringPair(int substringId)
    {
        var language = (Language)(substringId / 2);
        var gender = (Gender)(substringId % 2);
        return (language, gender);
    }

    public bool Exists(Language language, Gender gender)
    {
        return _substrings.ContainsKey(SubstringId(language, gender));
    }

    public string Get(Language language, Gender gender, string defaultValue = "")
    {
        int id = SubstringId(language, gender);
        return _substrings.TryGetValue(id, out string? value) ? value : defaultValue;
    }

    public void SetData(Language language, Gender gender, string text)
    {
        _substrings[SubstringId(language, gender)] = text;
    }

    public void Remove(Language language, Gender gender)
    {
        _substrings.Remove(SubstringId(language, gender));
    }

    public void Clear()
    {
        _substrings.Clear();
    }

    public int Count => _substrings.Count;

    public IEnumerator<(Language, Gender, string)> GetEnumerator()
    {
        foreach (var (substringId, text) in _substrings)
        {
            var (language, gender) = SubstringPair(substringId);
            yield return (language, gender, text);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString()
    {
        if (StringRef >= 0)
        {
            return StringRef.ToString();
        }

        // Default to English if available
        if (Exists(Language.English, Gender.Male))
        {
            return Get(Language.English, Gender.Male);
        }

        // Return first available substring
        foreach (var (_, _, text) in this)
        {
            return text;
        }

        return "-1";
    }

    public override int GetHashCode() => StringRef;

    public override bool Equals(object? obj)
    {
        return obj is LocalizedString other && Equals(other);
    }

    public bool Equals(LocalizedString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (StringRef != other.StringRef)
        {
            return false;
        }

        if (_substrings.Count != other._substrings.Count)
        {
            return false;
        }

        return _substrings.All(kvp =>
            other._substrings.TryGetValue(kvp.Key, out string? value) && value == kvp.Value);
    }

    public static bool operator ==(LocalizedString? left, LocalizedString? right)
    {
        if (left is null)
        {
            return right is null;
        }

        return left.Equals(right);
    }

    public static bool operator !=(LocalizedString? left, LocalizedString? right)
    {
        return !(left == right);
    }

    public Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            ["stringref"] = StringRef,
            ["substrings"] = new Dictionary<int, string>(_substrings)
        };
    }

    public static LocalizedString FromDictionary(Dictionary<string, object> data)
    {
        int stringRef = Convert.ToInt32(data["stringref"]);
        var substrings = data.TryGetValue("substrings", out object? subsObj) && subsObj is Dictionary<int, string> subs
            ? subs
            : null;
        return new LocalizedString(stringRef, substrings);
    }
}

