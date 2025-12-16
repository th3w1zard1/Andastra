using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BioWareCSharp.Common.Utility
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:180-383
    // Original: class CaseInsensitiveDict(Generic[T]):
    /// <summary>
    /// A class exactly like the builtin dict[str, Any], but provides case-insensitive key lookups.
    /// The case-sensitivity of the keys themselves are always preserved.
    /// </summary>
    public class CaseInsensitiveDict<T> : IDictionary<string, T>
    {
        private readonly Dictionary<string, T> _dictionary = new Dictionary<string, T>();
        private readonly Dictionary<string, string> _caseMap = new Dictionary<string, string>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:186-209
        // Original: def __init__(self, initial: Mapping[str, T] | Iterable[tuple[str, T]] | ItemsView[str, T] | None = None):
        public CaseInsensitiveDict()
        {
        }

        public CaseInsensitiveDict(IDictionary<string, T> initial)
        {
            if (initial != null)
            {
                foreach (var kvp in initial)
                {
                    this[kvp.Key] = kvp.Value;
                }
            }
        }

        public CaseInsensitiveDict(IEnumerable<KeyValuePair<string, T>> initial)
        {
            if (initial != null)
            {
                foreach (var kvp in initial)
                {
                    this[kvp.Key] = kvp.Value;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:211-229
        // Original: @classmethod def from_dict(cls, initial: dict[str, T]) -> CaseInsensitiveDict[T]:
        public static CaseInsensitiveDict<T> FromDict(IDictionary<string, T> initial)
        {
            var result = new CaseInsensitiveDict<T>();
            if (initial != null)
            {
                foreach (var kvp in initial)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:235-256
        // Original: def __eq__(self, other: object) -> bool:
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is CaseInsensitiveDict<T> other)
            {
                if (other._caseMap.Count != _caseMap.Count)
                {
                    return false;
                }

                foreach (var kvp in _dictionary)
                {
                    if (!other.TryGetValue(kvp.Key, out T otherValue) || !Equals(otherValue, kvp.Value))
                    {
                        return false;
                    }
                }
                return true;
            }

            if (obj is IDictionary<string, T> dict)
            {
                if (dict.Count != _dictionary.Count)
                {
                    return false;
                }

                foreach (var kvp in _dictionary)
                {
                    string lowerKey = kvp.Key.ToLowerInvariant();
                    T otherValue = default(T);
                    bool found = false;
                    foreach (var otherKvp in dict)
                    {
                        if (otherKvp.Key.ToLowerInvariant() == lowerKey)
                        {
                            otherValue = otherKvp.Value;
                            found = true;
                            break;
                        }
                    }
                    if (!found || !Equals(otherValue, kvp.Value))
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _dictionary.GetHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:258-259
        // Original: def __iter__(self) -> Iterator[str]:
        public IEnumerator<KeyValuePair<string, T>> GetEnumerator()
        {
            return _dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:261-265
        // Original: def __getitem__(self, key: str) -> T:
        public T this[string key]
        {
            get
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }
                string lowerKey = key.ToLowerInvariant();
                if (!_caseMap.TryGetValue(lowerKey, out string actualKey))
                {
                    throw new KeyNotFoundException($"Key '{key}' not found");
                }
                return _dictionary[actualKey];
            }
            set
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }
                if (ContainsKey(key))
                {
                    Remove(key);
                }
                string lowerKey = key.ToLowerInvariant();
                _caseMap[lowerKey] = key;
                _dictionary[key] = value;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:267-274
        // Original: def __setitem__(self, key: str, value: T):
        // (Handled in indexer setter above)

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:276-282
        // Original: def __delitem__(self, key: str):
        public bool Remove(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            string lowerKey = key.ToLowerInvariant();
            if (!_caseMap.TryGetValue(lowerKey, out string actualKey))
            {
                return false;
            }
            _dictionary.Remove(actualKey);
            _caseMap.Remove(lowerKey);
            return true;
        }

        bool IDictionary<string, T>.Remove(string key)
        {
            return Remove(key);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:284-285
        // Original: def __contains__(self, key: str) -> bool:
        public bool ContainsKey(string key)
        {
            if (key == null)
            {
                return false;
            }
            return _caseMap.ContainsKey(key.ToLowerInvariant());
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:287-288
        // Original: def __len__(self) -> int:
        public int Count => _dictionary.Count;

        public bool IsReadOnly => false;

        public ICollection<string> Keys => _dictionary.Keys;

        public ICollection<T> Values => _dictionary.Values;

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:290-291
        // Original: def __repr__(self) -> str:
        public override string ToString()
        {
            return $"CaseInsensitiveDict.from_dict({_dictionary})";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:332-353
        // Original: def update(self, other):
        public void Add(KeyValuePair<string, T> item)
        {
            this[item.Key] = item.Value;
        }

        public void Clear()
        {
            _dictionary.Clear();
            _caseMap.Clear();
        }

        public bool Contains(KeyValuePair<string, T> item)
        {
            return TryGetValue(item.Key, out T value) && Equals(value, item.Value);
        }

        public void CopyTo(KeyValuePair<string, T>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<string, T>>)_dictionary).CopyTo(array, arrayIndex);
        }

        public bool Remove(KeyValuePair<string, T> item)
        {
            if (Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public void Add(string key, T value)
        {
            this[key] = value;
        }

        public bool TryGetValue(string key, out T value)
        {
            if (key == null)
            {
                value = default(T);
                return false;
            }
            string lowerKey = key.ToLowerInvariant();
            if (!_caseMap.TryGetValue(lowerKey, out string actualKey))
            {
                value = default(T);
                return false;
            }
            return _dictionary.TryGetValue(actualKey, out value);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:332-353
        // Original: def update(self, other):
        public void Update(IDictionary<string, T> other)
        {
            if (other != null)
            {
                foreach (var kvp in other)
                {
                    this[kvp.Key] = kvp.Value;
                }
            }
        }

        public void Update(IEnumerable<KeyValuePair<string, T>> other)
        {
            if (other != null)
            {
                foreach (var kvp in other)
                {
                    this[kvp.Key] = kvp.Value;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:355-366
        // Original: def get(self, __key: str, __default: VT = None) -> VT | T:
        public T Get(string key, T defaultValue = default(T))
        {
            return TryGetValue(key, out T value) ? value : defaultValue;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:368-375
        // Original: def items(self), def values(self), def keys(self):
        public IEnumerable<KeyValuePair<string, T>> Items()
        {
            return _dictionary;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:377-378
        // Original: def copy(self) -> CaseInsensitiveDict[T]:
        public CaseInsensitiveDict<T> Copy()
        {
            return FromDict(_dictionary);
        }
    }
}

