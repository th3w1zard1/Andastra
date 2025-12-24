using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Andastra.Parsing.Common
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:440-799
    // Original: class LocalizedString:
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

        /// <summary>
        /// Returns true if this LocalizedString is invalid (StringRef == -1 and no substrings).
        /// </summary>
        public bool IsInvalid
        {
            get { return StringRef == -1 && _substringsInternal.Count == 0; }
        }

        private Dictionary<int, string> _substringsInternal;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:451-454
        // Original: def __init__(self, stringref: int, substrings: dict[int, str] | None = None):
        public LocalizedString(int stringRef, [CanBeNull] IDictionary<int, string> substrings = null)
        {
            StringRef = stringRef;
            _substringsInternal = new Dictionary<int, string>();
            if (substrings != null)
            {
                SetSubstrings(substrings);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:455-470
        // Original: def _substrings(self) -> dict[int, str]:
        private Dictionary<int, string> Substrings
        {
            get { return _substringsInternal; }
            set { _substringsInternal = value ?? new Dictionary<int, string>(); }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:471-476
        // Original: def __iter__(self) -> Generator[tuple[Language, Gender, str], Any, None]:
        public IEnumerator<(Language, Gender, string)> GetEnumerator()
        {
            foreach (KeyValuePair<int, string> kvp in _substringsInternal)
            {
                Language language;
                Gender gender;
                SubstringPair(kvp.Key, out language, out gender);
                yield return (language, gender, kvp.Value);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:477-479
        // Original: def __len__(self):
        public int Count
        {
            get { return _substringsInternal.Count; }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:481-482
        // Original: def __hash__(self):
        public override int GetHashCode()
        {
            return StringRef;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:484-498
        // Original: def __str__(self):
        public override string ToString()
        {
            if (StringRef >= 0)
            {
                return StringRef.ToString();
            }

            if (Exists(Language.English, Gender.Male))
            {
                string english = Get(Language.English, Gender.Male, false);
                if (english != null)
                {
                    return english;
                }
            }

            foreach ((Language _, Gender _, string text) in this)
            {
                return text;
            }

            return "-1";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:499-507
        // Original: def __eq__(self, other) -> bool:
        public override bool Equals([CanBeNull] object obj)
        {
            LocalizedString other = obj as LocalizedString;
            return Equals(other);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:499-507
        // Original: def __eq__(self, other) -> bool:
        public bool Equals([CanBeNull] LocalizedString other)
        {
            if (other == null)
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

            if (_substringsInternal.Count != other._substringsInternal.Count)
            {
                return false;
            }

            return _substringsInternal.All(kvp =>
                other._substringsInternal.TryGetValue(kvp.Key, out string value) && value == kvp.Value);
        }

        public static bool operator ==([CanBeNull] LocalizedString left, [CanBeNull] LocalizedString right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=([CanBeNull] LocalizedString left, [CanBeNull] LocalizedString right)
        {
            return !(left == right);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:508-512
        // Original: def to_dict(self) -> dict:
        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "stringref", StringRef },
                { "substrings", new Dictionary<int, string>(_substringsInternal) }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:514-518
        // Original: def from_dict(cls, data: dict) -> Self:
        public static LocalizedString FromDictionary(Dictionary<string, object> data)
        {
            int stringRef = Convert.ToInt32(data["stringref"]);
            Dictionary<int, string> substrings = null;
            object subsObj;
            if (data.TryGetValue("substrings", out subsObj) && subsObj is Dictionary<int, string>)
            {
                substrings = (Dictionary<int, string>)subsObj;
            }

            return new LocalizedString(stringRef, substrings);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:521-522
        // Original: def from_invalid(cls) -> Self:
        public static LocalizedString FromInvalid()
        {
            return new LocalizedString(-1);
        }

        // Convenience method for creating LocalizedString from string ID (string reference)
        public static LocalizedString FromStringId(int stringId)
        {
            return new LocalizedString(stringId);
        }

        // Alias for FromStringId - string reference
        public static LocalizedString FromStrRef(int stringRef)
        {
            return new LocalizedString(stringRef);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:525-538
        // Original: def from_english(cls, text: str) -> Self:
        public static LocalizedString FromEnglish(string text)
        {
            LocalizedString locstring = new LocalizedString(-1);
            locstring.SetData(Language.English, Gender.Male, text);
            return locstring;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:548-575
        // Original: def substring_id(language: Language | int, gender: Gender | int) -> int:
        public static int SubstringId(Language language, Gender gender)
        {
            return (int)language * 2 + (int)gender;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:548-575
        // Original: def substring_id(language: Language | int, gender: Gender | int) -> int:
        public static int SubstringId(int language, int gender)
        {
            Language languageEnum = LanguageExtensions.FromValue(language);
            Gender genderEnum = ToGenderEnum(gender);
            return SubstringId(languageEnum, genderEnum);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:577-598
        // Original: def substring_pair(substring_id: int | str) -> tuple[Language, Gender]:
        public static void SubstringPair(int substringId, out Language language, out Gender gender)
        {
            language = LanguageExtensions.FromValue(substringId / 2);
            gender = (Gender)(substringId % 2);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:600-648
        // Original: def set_data(self, language: Language | int, gender: Gender | int, string: str) -> None:
        public void SetData(Language language, Gender gender, string text)
        {
            int substringId = SubstringId(language, gender);
            _substringsInternal[substringId] = text;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:600-648
        // Original: def set_data(self, language: Language | int, gender: Gender | int, string: str) -> None:
        public void SetData(int language, int gender, string text)
        {
            SetData(LanguageExtensions.FromValue(language), ToGenderEnum(gender), text);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:651-654
        // Original: def set_string(self, substring_id: int | str, string: str) -> None:
        public void SetString(int substringId, string text)
        {
            Language language;
            Gender gender;
            SubstringPair(substringId, out language, out gender);
            SetData(language, gender, text);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:656-702
        // Original: def get(self, language: Language | int, gender: Gender | int | None = None, *, use_fallback: bool = False) -> str | None:
        public string Get(Language language, Gender gender, bool useFallback = false)
        {
            int substringId = SubstringId(language, gender);
            string value;
            if (_substringsInternal.TryGetValue(substringId, out value))
            {
                return value;
            }

            if (useFallback)
            {
                return _substringsInternal.Values.FirstOrDefault();
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:656-702
        // Original: def get(self, language: Language | int, gender: Gender | int | None = None, *, use_fallback: bool = False) -> str | None:
        public string Get(int language, int gender, bool useFallback = false)
        {
            return Get(LanguageExtensions.FromValue(language), ToGenderEnum(gender), useFallback);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:656-702
        // Original: def get(self, language: Language | int, gender: Gender | int | None = None, *, use_fallback: bool = False) -> str | None:
        public string Get(int language, bool useFallback = false)
        {
            return Get(LanguageExtensions.FromValue(language), Gender.Male, useFallback);
        }

        // Convenience method for tests - alias for Get(Language, Gender)
        public string GetString(Language language, Gender gender, bool useFallback = false)
        {
            return Get(language, gender, useFallback);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:704-749
        // Original: def remove(self, language: Language | int, gender: Gender | int) -> None:
        public void Remove(Language language, Gender gender)
        {
            int substringId = SubstringId(language, gender);
            _substringsInternal.Remove(substringId);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:704-749
        // Original: def remove(self, language: Language | int, gender: Gender | int) -> None:
        public void Remove(int language, int gender)
        {
            Remove(LanguageExtensions.FromValue(language), ToGenderEnum(gender));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:751-798
        // Original: def exists(self, language: Language | int, gender: Gender | int) -> bool:
        public bool Exists(Language language, Gender gender)
        {
            int substringId = SubstringId(language, gender);
            return _substringsInternal.ContainsKey(substringId);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:751-798
        // Original: def exists(self, language: Language | int, gender: Gender | int) -> bool:
        public bool Exists(int language, int gender)
        {
            return Exists(LanguageExtensions.FromValue(language), ToGenderEnum(gender));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/language.py:451-470
        // Original: def _substrings(self, value: dict[int, str]):
        private void SetSubstrings(IEnumerable<KeyValuePair<int, string>> substrings)
        {
            _substringsInternal.Clear();
            foreach (KeyValuePair<int, string> pair in substrings)
            {
                _substringsInternal[Convert.ToInt32(pair.Key)] = pair.Value;
            }
        }

        private static Gender ToGenderEnum(int gender)
        {
            if (!Enum.IsDefined(typeof(Gender), gender))
            {
                throw new ArgumentException("Invalid gender value: " + gender);
            }

            return (Gender)gender;
        }
    }
}
