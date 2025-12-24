using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;

namespace Andastra.Parsing.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/key_data.py:281-462
    // Original: class KEY
    public class KEY : IEquatable<KEY>
    {
        public const string FileTypeConst = "KEY ";
        public const string FileVersionConst = "V1  ";
        public const int HeaderSize = 64;
        public const int BifEntrySize = 12;
        public const int KeyEntrySize = 22;

        public string FileType { get; set; }
        public string FileVersion { get; set; }
        public int BuildYear { get; set; }
        public int BuildDay { get; set; }
        public List<BifEntry> BifEntries { get; set; }
        public List<KeyEntry> KeyEntries { get; set; }

        private readonly Dictionary<Tuple<string, ResourceType>, KeyEntry> _resourceLookup;
        private readonly Dictionary<string, BifEntry> _bifLookup;
        private bool _modified;

        public KEY()
        {
            FileType = FileTypeConst;
            FileVersion = FileVersionConst;
            BuildYear = 0;
            BuildDay = 0;
            BifEntries = new List<BifEntry>();
            KeyEntries = new List<KeyEntry>();
            _resourceLookup = new Dictionary<Tuple<string, ResourceType>, KeyEntry>();
            _bifLookup = new Dictionary<string, BifEntry>();
            _modified = false;
        }

        public int CalculateFileTableOffset()
        {
            return HeaderSize;
        }

        public int CalculateFilenameTableOffset()
        {
            return CalculateFileTableOffset() + (BifEntries.Count * BifEntrySize);
        }

        public int CalculateKeyTableOffset()
        {
            return CalculateFilenameTableOffset() + CalculateTotalFilenameSize();
        }

        public int CalculateFilenameOffset(int bifIndex)
        {
            if (bifIndex < 0 || bifIndex >= BifEntries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bifIndex));
            }

            int offset = CalculateFilenameTableOffset();
            for (int i = 0; i < bifIndex; i++)
            {
                offset += BifEntries[i].Filename.Length + 1;
            }
            return offset;
        }

        private int CalculateTotalFilenameSize()
        {
            int total = 0;
            foreach (var bif in BifEntries)
            {
                total += bif.Filename.Length + 1;
            }
            return total;
        }

        public int CalculateResourceId(int bifIndex, int resIndex)
        {
            if (bifIndex < 0)
            {
                throw new ArgumentException("BIF index cannot be negative", nameof(bifIndex));
            }
            if (bifIndex >= BifEntries.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bifIndex));
            }
            return (bifIndex << 20) | (resIndex & 0xFFFFF);
        }

        public KeyEntry GetResource(string resref, ResourceType restype)
        {
            string key = resref.ToLowerInvariant();
            KeyEntry entry;
            _resourceLookup.TryGetValue(Tuple.Create(key, restype), out entry);
            return entry;
        }

        public BifEntry GetBif(string filename)
        {
            string key = filename.ToLowerInvariant();
            BifEntry bif;
            _bifLookup.TryGetValue(key, out bif);
            return bif;
        }

        public BifEntry AddBif(string filename, int filesize = 0, ushort drives = 0)
        {
            var bif = new BifEntry();
            bif.Filename = filename.Replace("\\", "/").TrimStart('/');
            bif.Filesize = filesize;
            bif.Drives = drives;
            BifEntries.Add(bif);
            _bifLookup[bif.Filename.ToLowerInvariant()] = bif;
            _modified = true;
            return bif;
        }

        public KeyEntry AddKeyEntry(string resref, ResourceType restype, int bifIndex, int resIndex)
        {
            var entry = new KeyEntry();
            entry.ResRef = new ResRef(resref);
            entry.ResType = restype;
            entry.ResourceId = (uint)CalculateResourceId(bifIndex, resIndex);
            KeyEntries.Add(entry);
            _resourceLookup[Tuple.Create(resref.ToLowerInvariant(), restype)] = entry;
            _modified = true;
            return entry;
        }

        public void RemoveBif(BifEntry bif)
        {
            if (bif == null)
            {
                return;
            }
            BifEntries.Remove(bif);
            BuildLookupTables();
            _modified = true;
        }

        public void BuildLookupTables()
        {
            _resourceLookup.Clear();
            _bifLookup.Clear();
            foreach (var entry in KeyEntries)
            {
                _resourceLookup[Tuple.Create(entry.ResRef.ToString().ToLowerInvariant(), entry.ResType)] = entry;
            }
            foreach (var bif in BifEntries)
            {
                _bifLookup[bif.Filename.ToLowerInvariant()] = bif;
            }
        }

        public bool IsModified
        {
            get { return _modified; }
        }

        public override bool Equals(object obj)
        {
            return obj is KEY other && Equals(other);
        }

        public bool Equals(KEY other)
        {
            if (other == null)
            {
                return false;
            }
            return FileType == other.FileType &&
                   FileVersion == other.FileVersion &&
                   BuildYear == other.BuildYear &&
                   BuildDay == other.BuildDay &&
                   BifEntries.SequenceEqual(other.BifEntries) &&
                   KeyEntries.SequenceEqual(other.KeyEntries);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(FileType);
            hash.Add(FileVersion);
            hash.Add(BuildYear);
            hash.Add(BuildDay);
            foreach (var bif in BifEntries)
            {
                hash.Add(bif);
            }
            foreach (var entry in KeyEntries)
            {
                hash.Add(entry);
            }
            return hash.ToHashCode();
        }
    }
}
