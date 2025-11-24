using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TSLPatcher.Core.Common;
using BinaryWriter = System.IO.BinaryWriter;

namespace TSLPatcher.Core.Formats.TLK;

/// <summary>
/// Writes TLK (Talk Table) binary data.
/// 1:1 port of Python TLKBinaryWriter from pykotor/resource/formats/tlk/io_tlk.py
/// </summary>
public class TLKBinaryWriter
{
    private const int FileHeaderSize = 20;
    private const int EntrySize = 40;

    private readonly TLK _tlk;

    static TLKBinaryWriter()
    {
        // Register CodePages encoding provider for Windows encodings
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TLKBinaryWriter(TLK tlk)
    {
        _tlk = tlk;
    }

    public byte[] Write()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        WriteFileHeader(writer);

        int textOffset = 0;
        Encoding encoding = GetEncodingForLanguage(_tlk.Language);

        // First pass: write entry headers with text offsets
        // Python's _write_entry writes text_offset starting at 0 (relative to text data start)
        // The file header's entries_offset points to the TEXT DATA section, not the entry headers
        // So text_offset in each entry header is relative to the start of text data (0, len1, len1+len2, etc.)
        foreach (TLKEntry entry in _tlk.Entries)
        {
            byte[] textBytes = encoding.GetBytes(entry.Text);
            // text_offset stored in entry header is relative to text data start (starts at 0)
            WriteEntry(writer, entry, textOffset, textBytes.Length);
            textOffset += textBytes.Length;
        }

        // Second pass: write text data
        foreach (TLKEntry entry in _tlk.Entries)
        {
            byte[] textBytes = encoding.GetBytes(entry.Text);
            writer.Write(textBytes);
        }

        return ms.ToArray();
    }

    private void WriteFileHeader(BinaryWriter writer)
    {
        uint languageId = (uint)_tlk.Language;
        uint stringCount = (uint)_tlk.Count;
        uint entriesOffset = (uint)CalculateEntriesOffset();

        writer.Write(Encoding.ASCII.GetBytes("TLK "));
        writer.Write(Encoding.ASCII.GetBytes("V3.0"));
        writer.Write(languageId);
        writer.Write(stringCount);
        writer.Write(entriesOffset);
    }

    private int CalculateEntriesOffset()
    {
        return FileHeaderSize + _tlk.Count * EntrySize;
    }

    private static void WriteEntry(BinaryWriter writer, TLKEntry entry, int textOffset, int textByteLength)
    {
        string soundResref = entry.Voiceover.ToString();
        uint currentTextOffset = (uint)textOffset;
        // Python uses len(entry.text) for text_length (character count)
        // For cp1252, character count == byte count, so this works
        uint textLength = (uint)textByteLength;

        uint entryFlags = 0;
        if (entry.TextPresent)
        {
            entryFlags |= 0x0001;
        }

        if (entry.SoundPresent)
        {
            entryFlags |= 0x0002;
        }

        if (entry.SoundLengthPresent)
        {
            entryFlags |= 0x0004;
        }

        writer.Write(entryFlags);

        byte[] resrefBytes = new byte[16];
        byte[] sourceBytes = Encoding.ASCII.GetBytes(soundResref);
        Array.Copy(sourceBytes, resrefBytes, Math.Min(sourceBytes.Length, 16));
        writer.Write(resrefBytes);

        writer.Write((uint)0);  // volume variance (unused)
        writer.Write((uint)0);  // pitch variance (unused)
        writer.Write(currentTextOffset);
        writer.Write(textLength);
        writer.Write((uint)0);  // sound length
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

