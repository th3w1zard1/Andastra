using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Resource.Formats.LTR;

namespace BioWare.NET.Resource.Formats.LTR
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:357-627
    // Original: class LTRBlock(ComparableMixin)
    public class LTRBlock : IEquatable<LTRBlock>
    {
        private readonly List<float> _start;
        private readonly List<float> _middle;
        private readonly List<float> _end;

        public LTRBlock(int numCharacters)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:412-435
            // Original: def __init__(self, num_characters: int)
            _start = new List<float>(new float[numCharacters]);
            _middle = new List<float>(new float[numCharacters]);
            _end = new List<float>(new float[numCharacters]);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:449-477
        // Original: def set_start(self, char: str, chance: float)
        public void SetStart(string char_, float chance)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }
            if (chance < 0.0f || chance > 1.0f)
            {
                throw new ArgumentException("The chance specified must be between 0.0 and 1.0 inclusive.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            _start[charId] = chance;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:479-507
        // Original: def set_middle(self, char: str, chance: float)
        public void SetMiddle(string char_, float chance)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }
            if (chance < 0.0f || chance > 1.0f)
            {
                throw new ArgumentException("The chance specified must be between 0.0 and 1.0 inclusive.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            _middle[charId] = chance;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:509-537
        // Original: def set_end(self, char: str, chance: float)
        public void SetEnd(string char_, float chance)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }
            if (chance < 0.0f || chance > 1.0f)
            {
                throw new ArgumentException("The chance specified must be between 0.0 and 1.0 inclusive.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            _end[charId] = chance;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:539-567
        // Original: def get_start(self, char: str) -> float
        public float GetStart(string char_)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            return _start[charId];
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:569-597
        // Original: def get_middle(self, char: str) -> float
        public float GetMiddle(string char_)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            return _middle[charId];
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:599-627
        // Original: def get_end(self, char: str) -> float
        public float GetEnd(string char_)
        {
            if (char_ == null || char_.Length != 1)
            {
                throw new ArgumentException("The character specified was not a real character.");
            }
            if (!LTR.CharacterSet.Contains(char_))
            {
                throw new IndexOutOfRangeException("The character specified was invalid.");
            }

            int charId = LTR.CharacterSet.IndexOf(char_);
            return _end[charId];
        }

        // Internal access for reader/writer
        internal List<float> Start => _start;
        internal List<float> Middle => _middle;
        internal List<float> End => _end;

        public override bool Equals(object obj)
        {
            return obj is LTRBlock other && Equals(other);
        }

        public bool Equals(LTRBlock other)
        {
            if (other == null)
            {
                return false;
            }
            return _start.SequenceEqual(other._start) &&
                   _middle.SequenceEqual(other._middle) &&
                   _end.SequenceEqual(other._end);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var val in _start)
            {
                hash.Add(val);
            }
            foreach (var val in _middle)
            {
                hash.Add(val);
            }
            foreach (var val in _end)
            {
                hash.Add(val);
            }
            return hash.ToHashCode();
        }
    }
}

