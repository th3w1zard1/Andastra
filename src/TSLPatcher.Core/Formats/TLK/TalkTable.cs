using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Formats.TLK;

/// <summary>
/// Read-only accessor for dialog.tlk files.
/// Files are only opened when accessing a string, ensuring strings are always up to date.
/// For full TLK manipulation, use the TLK class instead.
/// </summary>
/// <remarks>
/// Python Reference: g:/GitHub/PyKotor/Libraries/PyKotor/src/pykotor/extract/talktable.py
/// </remarks>
public class TalkTable
{
    private readonly string _path;

    public string Path => _path;

    public TalkTable(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
    }

    /// <summary>
    /// Access a string from the tlk file.
    /// </summary>
    public string GetString(int stringref)
    {
        if (stringref == -1)
        {
            return "";
        }

        if (!File.Exists(_path))
        {
            return "";
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(12);
        uint entriesCount = reader.ReadUInt32();
        uint textsOffset = reader.ReadUInt32(); // Offset to TEXT DATA section (not entry headers!)

        if (stringref >= entriesCount)
        {
            return "";
        }

        // Python's talktable.py: texts_offset points to TEXT DATA, text_offset is relative to it
        TLKData tlkData = ExtractCommonTlkData(reader, stringref);
        // Calculate absolute position, ensuring no overflow
        long absolutePosition = (long)textsOffset + (long)tlkData.TextOffset;
        if (absolutePosition > int.MaxValue)
        {
            throw new IOException($"Text offset too large: {absolutePosition}");
        }
        reader.Seek((int)absolutePosition);
        // ReadString reads bytes, TextLength is character count (for cp1252, they're equal)
        // Get encoding for the language
        Encoding encoding = GetEncodingForLanguage(GetLanguage());
        byte[] textBytes = reader.ReadBytes(tlkData.TextLength);
        string text = encoding.GetString(textBytes);
        // Trim at first null byte (Python's read_string does this)
        int nullIndex = text.IndexOf('\0');
        if (nullIndex >= 0)
        {
            text = text.Substring(0, nullIndex);
        }
        return text.Replace("\0", "");
    }

    /// <summary>
    /// Access the sound ResRef from the tlk file.
    /// </summary>
    public ResRef GetSound(int stringref)
    {
        if (stringref == -1)
        {
            return ResRef.FromBlank();
        }

        if (!File.Exists(_path))
        {
            return ResRef.FromBlank();
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(12);
        uint entriesCount = reader.ReadUInt32();
        reader.Skip(4);

        if (stringref >= entriesCount)
        {
            return ResRef.FromBlank();
        }

        TLKData tlkData = ExtractCommonTlkData(reader, stringref);
        return new ResRef(tlkData.Voiceover);
    }

    /// <summary>
    /// Gets both string and sound in one call.
    /// </summary>
    public StringResult GetStringResult(int stringref)
    {
        if (stringref == -1)
        {
            return new StringResult("", ResRef.FromBlank());
        }

        if (!File.Exists(_path))
        {
            return new StringResult("", ResRef.FromBlank());
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(12);
        uint entriesCount = reader.ReadUInt32();
        uint textsOffset = reader.ReadUInt32(); // Offset to TEXT DATA section

        if (stringref >= entriesCount)
        {
            return new StringResult("", ResRef.FromBlank());
        }

        TLKData tlkData = ExtractCommonTlkData(reader, stringref);
        long absolutePosition = (long)textsOffset + (long)tlkData.TextOffset;
        if (absolutePosition > int.MaxValue)
        {
            throw new IOException($"Text offset too large: {absolutePosition}");
        }
        reader.Seek((int)absolutePosition);
        string text = reader.ReadString(tlkData.TextLength);
        ResRef sound = new(tlkData.Voiceover);

        return new StringResult(text, sound);
    }

    /// <summary>
    /// Loads a list of strings and sound ResRefs from the specified list.
    /// This uses a single file handle and should be used when loading multiple strings.
    /// </summary>
    public Dictionary<int, StringResult> Batch(List<int> stringrefs)
    {
        var batch = new Dictionary<int, StringResult>();

        if (!File.Exists(_path))
        {
            foreach (int stringref in stringrefs)
            {
                batch[stringref] = new StringResult("", ResRef.FromBlank());
            }
            return batch;
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(8);
        uint languageId = reader.ReadUInt32();
        var language = (Language)languageId;
        uint entriesCount = reader.ReadUInt32();
        uint textsOffset = reader.ReadUInt32(); // Offset to TEXT DATA section

        foreach (int stringref in stringrefs)
        {
            if (stringref == -1 || stringref >= entriesCount)
            {
                batch[stringref] = new StringResult("", ResRef.FromBlank());
                continue;
            }

            TLKData tlkData = ExtractCommonTlkData(reader, stringref);
            long absolutePosition = (long)textsOffset + (long)tlkData.TextOffset;
            if (absolutePosition > int.MaxValue)
            {
                throw new IOException($"Text offset too large: {absolutePosition}");
            }
            reader.Seek((int)absolutePosition);
            string text = reader.ReadString(tlkData.TextLength);
            ResRef sound = new(tlkData.Voiceover);

            batch[stringref] = new StringResult(text, sound);
        }

        return batch;
    }

    /// <summary>
    /// Returns the number of entries in the talk table.
    /// </summary>
    public int Size()
    {
        if (!File.Exists(_path))
        {
            return 0;
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(12);
        return (int)reader.ReadUInt32();
    }

    /// <summary>
    /// Returns the language of the TLK file.
    /// </summary>
    public Language GetLanguage()
    {
        if (!File.Exists(_path))
        {
            return Language.English;
        }

        using var reader = RawBinaryReader.FromFile(_path);
        reader.Seek(8);
        uint languageId = reader.ReadUInt32();
        return (Language)languageId;
    }

    private static TLKData ExtractCommonTlkData(RawBinaryReader reader, int stringref)
    {
        // Entry offset calculation: header (20 bytes) + entry_size (40 bytes) * stringref
        reader.Seek(20 + 40 * stringref);

        return new TLKData(
            Flags: reader.ReadUInt32(),
            Voiceover: reader.ReadString(16),
            VolumeVariance: reader.ReadUInt32(),
            PitchVariance: reader.ReadUInt32(),
            TextOffset: reader.ReadUInt32(),
            TextLength: (int)reader.ReadUInt32(),
            SoundLength: reader.ReadSingle()
        );
    }

    private record TLKData(
        uint Flags,
        string Voiceover,
        uint VolumeVariance,
        uint PitchVariance,
        uint TextOffset,
        int TextLength,
        float SoundLength
    );

    private static Encoding GetEncodingForLanguage(Language language)
    {
        // Match Python's Language.get_encoding() method
        return language switch
        {
            Language.English or Language.French or Language.German or Language.Italian or Language.Spanish => Encoding.GetEncoding("windows-1252"), // cp1252
            Language.Polish => Encoding.GetEncoding("windows-1250"), // cp1250
            Language.Korean => Encoding.GetEncoding("euc-kr"),
            Language.Chinese_Traditional => Encoding.GetEncoding("big5"),
            Language.Chinese_Simplified => Encoding.GetEncoding("gb2312"),
            Language.Japanese => Encoding.GetEncoding("shift_jis"),
            _ => Encoding.GetEncoding("windows-1252"), // default to cp1252
        };
    }
}

/// <summary>
/// Result containing both text and sound from a TLK entry.
/// </summary>
public record StringResult(string Text, ResRef Sound);

