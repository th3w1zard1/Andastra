using System;
using System.Collections.Generic;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Extract
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/talktable.py
    public struct StringResult
    {
        public string Text { get; }
        public ResRef Sound { get; }

        public StringResult(string text, ResRef sound)
        {
            Text = text;
            Sound = sound;
        }
    }

    public struct TlkEntry
    {
        public uint Flags { get; }
        public string Voiceover { get; }
        public uint VolumeVariance { get; }
        public uint PitchVariance { get; }
        public uint TextOffset { get; }
        public uint TextLength { get; }
        public float SoundLength { get; }

        public TlkEntry(uint flags, string voiceover, uint volumeVariance, uint pitchVariance, uint textOffset, uint textLength, float soundLength)
        {
            Flags = flags;
            Voiceover = voiceover;
            VolumeVariance = volumeVariance;
            PitchVariance = pitchVariance;
            TextOffset = textOffset;
            TextLength = textLength;
            SoundLength = soundLength;
        }
    }

    /// <summary>
    /// Read-only talk table access (dialog.tlk).
    /// </summary>
    public class TalkTable
    {
        private readonly string _path;

        public TalkTable(string path)
        {
            _path = path;
        }

        public string Path => _path;

        public string GetString(int stringref)
        {
            if (stringref == -1)
            {
                return string.Empty;
            }

            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_path))
            {
                reader.Seek(12);
                uint entries = reader.ReadUInt32();
                uint textsOffset = reader.ReadUInt32();

                if (stringref < 0 || stringref >= entries)
                {
                    return string.Empty;
                }

                TlkEntry entry = ExtractEntry(reader, stringref);
                reader.Seek((int)(textsOffset + entry.TextOffset));
                return reader.ReadString((int)entry.TextLength);
            }
        }

        public ResRef GetSound(int stringref)
        {
            if (stringref == -1)
            {
                return ResRef.FromBlank();
            }

            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_path))
            {
                reader.Seek(12);
                uint entries = reader.ReadUInt32();
                reader.Skip(4);

                if (stringref < 0 || stringref >= entries)
                {
                    return ResRef.FromBlank();
                }

                TlkEntry entry = ExtractEntry(reader, stringref);
                return new ResRef(entry.Voiceover);
            }
        }

        public Dictionary<int, StringResult> Batch(IList<int> stringrefs)
        {
            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_path))
            {
                reader.Seek(8);
                Language language = LanguageExtensions.FromValue((int)reader.ReadUInt32());
                string encoding = language.GetEncoding();
                uint entries = reader.ReadUInt32();
                uint textsOffset = reader.ReadUInt32();

                var result = new Dictionary<int, StringResult>();
                foreach (int sref in stringrefs)
                {
                    if (sref == -1 || sref < 0 || sref >= entries)
                    {
                        result[sref] = new StringResult(string.Empty, ResRef.FromBlank());
                        continue;
                    }

                    TlkEntry entry = ExtractEntry(reader, sref);
                    reader.Seek((int)(textsOffset + entry.TextOffset));
                    string text = reader.ReadString((int)entry.TextLength, encoding);
                    ResRef sound = new ResRef(entry.Voiceover);
                    result[sref] = new StringResult(text, sound);
                }

                return result;
            }
        }

        public int Size()
        {
            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_path))
            {
                reader.Seek(12);
                return (int)reader.ReadUInt32();
            }
        }

        public Language GetLanguage()
        {
            using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(_path))
            {
                reader.Seek(8);
                return LanguageExtensions.FromValue((int)reader.ReadUInt32());
            }
        }

        private TlkEntry ExtractEntry(BioWare.NET.Common.RawBinaryReader reader, int stringref)
        {
            reader.Seek(20 + 40 * stringref);
            uint flags = reader.ReadUInt32();
            string voiceover = reader.ReadString(16);
            uint volVar = reader.ReadUInt32();
            uint pitchVar = reader.ReadUInt32();
            uint textOffset = reader.ReadUInt32();
            uint textLength = reader.ReadUInt32();
            float soundLength = reader.ReadSingle();
            return new TlkEntry(flags, voiceover, volVar, pitchVar, textOffset, textLength, soundLength);
        }
    }
}

