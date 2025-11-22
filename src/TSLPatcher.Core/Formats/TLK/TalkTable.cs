using System;
using System.Collections.Generic;
using System.IO;
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
        uint textsOffset = reader.ReadUInt32();

        if (stringref >= entriesCount)
        {
            return "";
        }

        var tlkData = ExtractCommonTlkData(reader, stringref);
        reader.Seek((int)(textsOffset + tlkData.TextOffset));
        return reader.ReadString(tlkData.TextLength);
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

        var tlkData = ExtractCommonTlkData(reader, stringref);
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
        uint textsOffset = reader.ReadUInt32();

        if (stringref >= entriesCount)
        {
            return new StringResult("", ResRef.FromBlank());
        }

        var tlkData = ExtractCommonTlkData(reader, stringref);
        reader.Seek((int)(textsOffset + tlkData.TextOffset));
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
        uint textsOffset = reader.ReadUInt32();

        foreach (int stringref in stringrefs)
        {
            if (stringref == -1 || stringref >= entriesCount)
            {
                batch[stringref] = new StringResult("", ResRef.FromBlank());
                continue;
            }

            var tlkData = ExtractCommonTlkData(reader, stringref);
            reader.Seek((int)(textsOffset + tlkData.TextOffset));
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

    private TLKData ExtractCommonTlkData(RawBinaryReader reader, int stringref)
    {
        // Entry offset calculation: header (20 bytes) + entry_size (40 bytes) * stringref
        reader.Seek(20 + 40 * stringref);

        return new TLKData(
            Flags: reader.ReadUInt32(),
            Voiceover: reader.ReadString(16),
            VolumeVariance: reader.ReadUInt32(),
            PitchVariance: reader.ReadUInt32(),
            TextOffset: reader.ReadUInt32(),
            TextLength: reader.ReadInt32(),
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
}

/// <summary>
/// Result containing both text and sound from a TLK entry.
/// </summary>
public record StringResult(string Text, ResRef Sound);

