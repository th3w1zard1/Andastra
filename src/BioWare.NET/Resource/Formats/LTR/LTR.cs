using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.LTR
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:57-355
    // Original: class LTR(ComparableMixin)
    public class LTR : IEquatable<LTR>
    {
        public const string CharacterSet = "abcdefghijklmnopqrstuvwxyz'-";
        public const int NumCharacters = 28;

        public static readonly ResourceType BinaryType = ResourceType.LTR;

        private readonly LTRBlock _singles;
        private readonly List<LTRBlock> _doubles;
        private readonly List<List<LTRBlock>> _triples;

        public LTR()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:115-142
            // Original: def __init__(self)
            _singles = new LTRBlock(NumCharacters);
            _doubles = new List<LTRBlock>();
            for (int i = 0; i < NumCharacters; i++)
            {
                _doubles.Add(new LTRBlock(NumCharacters));
            }
            _triples = new List<List<LTRBlock>>();
            for (int i = 0; i < NumCharacters; i++)
            {
                List<LTRBlock> row = new List<LTRBlock>();
                for (int j = 0; j < NumCharacters; j++)
                {
                    row.Add(new LTRBlock(NumCharacters));
                }
                _triples.Add(row);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:156-164
        // Original: @staticmethod def _chance() -> float
        private static float Chance(Random random)
        {
            return random.Next(0, 1001) / 1000.0f;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:166-282
        // Original: def generate(self, seed: int | None = None) -> str
        public string Generate(int? seed = null)
        {
            Random random = seed.HasValue ? new Random(seed.Value) : new Random();

            bool done = false;

            while (!done)
            {
                int attempts = 0;
                StringBuilder name = new StringBuilder();

                // Generate first character using single-letter start probabilities
                bool found = false;
                foreach (char c in CharacterSet)
                {
                    if (Chance(random) < _singles.GetStart(c.ToString()))
                    {
                        name.Append(c);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    continue;
                }

                // Generate second character using double-letter start probabilities
                found = false;
                foreach (char c in CharacterSet)
                {
                    int index = CharacterSet.IndexOf(name[name.Length - 1]);
                    if (Chance(random) < _doubles[index].GetStart(c.ToString()))
                    {
                        name.Append(c);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    continue;
                }

                // Generate third character using triple-letter start probabilities
                found = false;
                foreach (char c in CharacterSet)
                {
                    int index1 = CharacterSet.IndexOf(name[name.Length - 2]);
                    int index2 = CharacterSet.IndexOf(name[name.Length - 1]);
                    if (Chance(random) < _triples[index1][index2].GetStart(c.ToString()))
                    {
                        name.Append(c);
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    continue;
                }

                // Generate subsequent characters using triple-letter middle/end probabilities
                while (true)
                {
                    float prob = Chance(random);

                    // Check if name should end (probability increases with name length)
                    if ((random.Next(0, 12) % 12) <= name.Length)
                    {
                        // Select final character using triple-letter end probabilities
                        foreach (char c in CharacterSet)
                        {
                            int index1 = CharacterSet.IndexOf(name[name.Length - 2]);
                            int index2 = CharacterSet.IndexOf(name[name.Length - 1]);
                            if (prob < _triples[index1][index2].GetEnd(c.ToString()))
                            {
                                name.Append(c);
                                string result = name.ToString();
                                if (result.Length > 0)
                                {
                                    return char.ToUpper(result[0]) + result.Substring(1);
                                }
                                return result;
                            }
                        }
                    }

                    // Generate next character using triple-letter middle probabilities
                    found = false;
                    foreach (char c in CharacterSet)
                    {
                        int index1 = CharacterSet.IndexOf(name[name.Length - 2]);
                        int index2 = CharacterSet.IndexOf(name[name.Length - 1]);
                        if (prob < _triples[index1][index2].GetMiddle(c.ToString()))
                        {
                            name.Append(c);
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        // No valid character found - increment attempts and check termination
                        attempts++;
                        if (name.Length < 4 || attempts > 100)
                        {
                            break;
                        }
                    }
                }
            }

            throw new InvalidOperationException($"Unknown problem generating LTR from seed {seed}");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:284-303
        // Original: def set_singles_start/middle/end
        public void SetSinglesStart(string char_, float chance)
        {
            _singles.SetStart(char_, chance);
        }

        public void SetSinglesMiddle(string char_, float chance)
        {
            _singles.SetMiddle(char_, chance);
        }

        public void SetSinglesEnd(string char_, float chance)
        {
            _singles.SetEnd(char_, chance);
        }

        // Public getters for editor access
        public float GetSinglesStart(string char_)
        {
            return _singles.GetStart(char_);
        }

        public float GetSinglesMiddle(string char_)
        {
            return _singles.GetMiddle(char_);
        }

        public float GetSinglesEnd(string char_)
        {
            return _singles.GetEnd(char_);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:305-327
        // Original: def set_doubles_start/middle/end
        public void SetDoublesStart(string previous1, string char_, float chance)
        {
            _doubles[CharacterSet.IndexOf(previous1)].SetStart(char_, chance);
        }

        public void SetDoublesMiddle(string previous1, string char_, float chance)
        {
            _doubles[CharacterSet.IndexOf(previous1)].SetMiddle(char_, chance);
        }

        public void SetDoublesEnd(string previous1, string char_, float chance)
        {
            _doubles[CharacterSet.IndexOf(previous1)].SetEnd(char_, chance);
        }

        // Public getters for editor access
        public float GetDoublesStart(string previous1, string char_)
        {
            return _doubles[CharacterSet.IndexOf(previous1)].GetStart(char_);
        }

        public float GetDoublesMiddle(string previous1, string char_)
        {
            return _doubles[CharacterSet.IndexOf(previous1)].GetMiddle(char_);
        }

        public float GetDoublesEnd(string previous1, string char_)
        {
            return _doubles[CharacterSet.IndexOf(previous1)].GetEnd(char_);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_data.py:329-354
        // Original: def set_triples_start/middle/end
        public void SetTriplesStart(string previous2, string previous1, string char_, float chance)
        {
            _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].SetStart(char_, chance);
        }

        public void SetTriplesMiddle(string previous2, string previous1, string char_, float chance)
        {
            _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].SetMiddle(char_, chance);
        }

        public void SetTriplesEnd(string previous2, string previous1, string char_, float chance)
        {
            _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].SetEnd(char_, chance);
        }

        // Public getters for editor access
        public float GetTriplesStart(string previous2, string previous1, string char_)
        {
            return _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].GetStart(char_);
        }

        public float GetTriplesMiddle(string previous2, string previous1, string char_)
        {
            return _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].GetMiddle(char_);
        }

        public float GetTriplesEnd(string previous2, string previous1, string char_)
        {
            return _triples[CharacterSet.IndexOf(previous2)][CharacterSet.IndexOf(previous1)].GetEnd(char_);
        }

        // Internal access for reader/writer
        internal LTRBlock Singles => _singles;
        internal List<LTRBlock> Doubles => _doubles;
        internal List<List<LTRBlock>> Triples => _triples;

        public override bool Equals(object obj)
        {
            return obj is LTR other && Equals(other);
        }

        public bool Equals(LTR other)
        {
            if (other == null)
            {
                return false;
            }
            return _singles.Equals(other._singles) &&
                   _doubles.SequenceEqual(other._doubles) &&
                   _triples.SelectMany(row => row).SequenceEqual(other._triples.SelectMany(row => row));
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(_singles);
            foreach (var block in _doubles)
            {
                hash.Add(block);
            }
            foreach (var row in _triples)
            {
                foreach (var block in row)
                {
                    hash.Add(block);
                }
            }
            return hash.ToHashCode();
        }
    }
}

