using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats;

namespace TSLPatcher.Core.Formats.TLK;

/// <summary>
/// Reads TLK (Talk Table) binary data.
/// 1:1 port of Python TLKBinaryReader from pykotor/resource/formats/tlk/io_tlk.py
/// </summary>
public class TLKBinaryReader : BinaryFormatReaderBase
{
    private const int FileHeaderSize = 20;
    private const int EntrySize = 40;

    private readonly Language? _language;

    private TLK? _tlk;
    private int _textsOffset;
    private readonly List<(int offset, int length)> _textHeaders = new();

    static TLKBinaryReader()
    {
        // Register CodePages encoding provider for Windows encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TLKBinaryReader(byte[] data, Language? language = null) : base(data)
    {
        _language = language;
    }

    public TLKBinaryReader(string filepath, Language? language = null) : base(filepath)
    {
        _language = language;
    }

    public TLKBinaryReader(Stream source, Language? language = null) : base(source)
    {
        _language = language;
    }

    public TLK Load()
    {
        try
        {
            _tlk = new TLK();
            _textsOffset = 0;
            _textHeaders.Clear();

            Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            LoadFileHeader();

            // Load all entry headers
            for (int stringref = 0; stringref < _tlk.Count; stringref++)
            {
                LoadEntry(stringref);
            }

            // Load all entry texts
            for (int stringref = 0; stringref < _tlk.Count; stringref++)
            {
                LoadText(stringref);
            }

            return _tlk;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Invalid TLK file format - unexpected end of file.");
        }
    }

    private void LoadFileHeader()
    {
        string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
        string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));
        uint languageId = Reader.ReadUInt32();
        uint stringCount = Reader.ReadUInt32();
        uint entriesOffset = Reader.ReadUInt32();

        if (fileType != "TLK ")
        {
            throw new InvalidDataException("Attempted to load an invalid TLK file.");
        }

        if (fileVersion != "V3.0")
        {
            throw new InvalidDataException("Attempted to load an invalid TLK file.");
        }

        _tlk!.Language = _language ?? (Language)languageId;
        _tlk.Resize((int)stringCount);

        _textsOffset = (int)entriesOffset;
    }

    private void LoadEntry(int stringref)
    {
        var entry = _tlk!.Entries[stringref];

        uint entryFlags = Reader.ReadUInt32();
        string soundResref = Encoding.ASCII.GetString(Reader.ReadBytes(16)).TrimEnd('\0');
        uint volumeVariance = Reader.ReadUInt32();  // unused
        uint pitchVariance = Reader.ReadUInt32();   // unused
        uint textOffset = Reader.ReadUInt32();
        uint textLength = Reader.ReadUInt32();
        float soundLength = Reader.ReadSingle();

        entry.TextPresent = (entryFlags & 0x0001) != 0;
        entry.SoundPresent = (entryFlags & 0x0002) != 0;
        entry.SoundLengthPresent = (entryFlags & 0x0004) != 0;
        entry.Voiceover = new ResRef(soundResref);
        entry.SoundLength = soundLength;

        _textHeaders.Add(((int)textOffset, (int)textLength));
    }

    private void LoadText(int stringref)
    {
        var textHeader = _textHeaders[stringref];
        var entry = _tlk!.Entries[stringref];

        Reader.BaseStream.Seek(textHeader.offset + _textsOffset, SeekOrigin.Begin);

        // Get encoding for the language
        Encoding encoding = GetEncodingForLanguage(_tlk.Language);
        byte[] textBytes = Reader.ReadBytes(textHeader.length);
        string text = encoding.GetString(textBytes);

        entry.Text = text;
    }

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

