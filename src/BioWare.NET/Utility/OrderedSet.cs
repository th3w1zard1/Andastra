using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace BioWare.NET.Utility
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:25-177
    // Original: class OrderedSet(MutableSet[T]):
    /// <summary>
    /// An ordered set that maintains insertion order while providing set semantics.
    /// </summary>
    public class OrderedSet<T> : ISet<T>, IList<T>, IReadOnlyList<T>
    {
        private readonly List<T> _list = new List<T>();
        private readonly HashSet<T> _set = new HashSet<T>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:26-33
        // Original: def __init__(self, iterable: Iterable[T] | None = None) -> None:
        public OrderedSet()
        {
        }

        public OrderedSet(IEnumerable<T> iterable)
        {
            if (iterable != null)
            {
                Extend(iterable);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:35-36
        // Original: def __contains__(self, value: object) -> bool:
        public bool Contains(T item)
        {
            return _set.Contains(item);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:38-39
        // Original: def __iter__(self) -> Iterator[T]:
        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:41-42
        // Original: def __len__(self) -> int:
        public int Count => _list.Count;

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:44-47
        // Original: def add(self, value: T) -> None:
        public bool Add(T item)
        {
            if (!_set.Contains(item))
            {
                _list.Add(item);
                _set.Add(item);
                return true;
            }
            return false;
        }

        void ICollection<T>.Add(T item)
        {
            Add(item);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:49-50
        // Original: def append(self, value: T) -> None:
        public void Append(T value)
        {
            Add(value);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:52-54
        // Original: def discard(self, value: T) -> None:
        public bool Remove(T item)
        {
            if (_set.Remove(item))
            {
                _list.Remove(item);
                return true;
            }
            return false;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:56-57
        // Original: def remove(self, value: T) -> None:
        public void Discard(T value)
        {
            Remove(value);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:59-62
        // Original: def insert(self, index: int, value: T) -> None:
        public void Insert(int index, T item)
        {
            if (!_set.Contains(item))
            {
                _list.Insert(index, item);
                _set.Add(item);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:64-66
        // Original: def update(self, other: Iterable[T]):
        public void Update(IEnumerable<T> other)
        {
            foreach (var item in other)
            {
                Append(item);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:68-69
        // Original: def extend(self, iterable: Iterable[T]) -> None:
        public void Extend(IEnumerable<T> iterable)
        {
            Update(iterable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:71-74
        // Original: def pop(self, index: SupportsIndex = -1) -> T:
        public T Pop(int index = -1)
        {
            if (index < 0)
            {
                index = _list.Count + index;
            }
            T value = _list[index];
            _list.RemoveAt(index);
            _set.Remove(value);
            return value;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:82-83
        // Original: def __getitem__(self, index: SupportsIndex | slice) -> T | list[T]:
        public T this[int index]
        {
            get => _list[index];
            set
            {
                // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:85-91
                // Original: def __setitem__(self, index: SupportsIndex, value: T) -> None:
                T oldValue = _list[index];
                _list[index] = value;
                _set.Remove(oldValue);
                _set.Add(value);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:93-95
        // Original: def clear(self) -> None:
        public void Clear()
        {
            _list.Clear();
            _set.Clear();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:97-101
        // Original: def copy(self) -> Self:
        public OrderedSet<T> Copy()
        {
            OrderedSet<T> newSet = new OrderedSet<T>();
            newSet._list.AddRange(_list);
            foreach (var item in _set)
            {
                newSet._set.Add(item);
            }
            return newSet;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:109-121
        // Original: def __delitem__(self, index: SupportsIndex | slice) -> None:
        public void RemoveAt(int index)
        {
            T value = _list[index];
            _list.RemoveAt(index);
            _set.Remove(value);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:123-124
        // Original: def __repr__(self) -> str:
        public override string ToString()
        {
            return $"{GetType().Name}([{string.Join(", ", _list)}])";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:126-127
        // Original: def count(self, value: T) -> int:
        public int CountItem(T value)
        {
            return _list.Count(x => EqualityComparer<T>.Default.Equals(x, value));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:129-130
        // Original: def index(self, value: T, start: SupportsIndex = 0, stop: SupportsIndex = sys.maxsize) -> int:
        public int IndexOf(T item)
        {
            return _list.IndexOf(item);
        }

        public int IndexOf(T item, int startIndex)
        {
            for (int i = startIndex; i < _list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_list[i], item))
                {
                    return i;
                }
            }
            return -1;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:132-135
        // Original: def __eq__(self, other: object) -> bool:
        public override bool Equals(object obj)
        {
            if (obj is OrderedSet<T> other)
            {
                return _list.SequenceEqual(other._list) && _set.SetEquals(other._set);
            }
            if (obj is IList<T> list)
            {
                return _list.SequenceEqual(list);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return _list.GetHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:149-155
        // Original: def sort(self, *, key = None, reverse: bool = False) -> None:
        public void Sort()
        {
            _list.Sort();
        }

        public void Sort(IComparer<T> comparer)
        {
            _list.Sort(comparer);
        }

        public void Sort(Comparison<T> comparison)
        {
            _list.Sort(comparison);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:157-158
        // Original: def reverse(self) -> None:
        public void Reverse()
        {
            _list.Reverse();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/more_collections.py:160-163
        // Original: def __add__(self, other: Iterable[T]) -> OrderedSet[T]:
        public static OrderedSet<T> operator +(OrderedSet<T> left, IEnumerable<T> right)
        {
            OrderedSet<T> newSet = left.Copy();
            newSet.Extend(right);
            return newSet;
        }

        // ISet<T> implementation
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return _set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return _set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return _set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return _set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return _set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return _set.SetEquals(other);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            var otherSet = other as ISet<T> ?? new HashSet<T>(other);
            var toRemove = _set.Intersect(otherSet).ToList();
            var toAdd = otherSet.Except(_set).ToList();

            foreach (var item in toRemove)
            {
                Remove(item);
            }
            foreach (var item in toAdd)
            {
                Add(item);
            }
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (var item in other)
            {
                Add(item);
            }
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            var otherSet = other as ISet<T> ?? new HashSet<T>(other);
            var toRemove = _set.Except(otherSet).ToList();
            foreach (var item in toRemove)
            {
                Remove(item);
            }
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            foreach (var item in other)
            {
                Remove(item);
            }
        }

        // ICollection<T> implementation
        public bool IsReadOnly => false;

        public void CopyTo(T[] array, int arrayIndex)
        {
            _list.CopyTo(array, arrayIndex);
        }

        // IList<T> implementation
        public int IndexOf(T item, int index, int count)
        {
            for (int i = index; i < index + count && i < _list.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_list[i], item))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
