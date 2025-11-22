using System;
using System.IO;
using System.Text;
using TSLPatcher.Core.Common;

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

        // Write all entry headers
        foreach (var entry in _tlk.Entries)
        {
            WriteEntry(writer, entry, ref textOffset, encoding);
        }

        // Write all entry texts
        foreach (var entry in _tlk.Entries)
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

    private void WriteEntry(BinaryWriter writer, TLKEntry entry, ref int textOffset, Encoding encoding)
    {
        string soundResref = entry.Voiceover.ToString();
        uint currentTextOffset = (uint)textOffset;
        uint textLength = (uint)encoding.GetByteCount(entry.Text);

        // Calculate entry flags
        uint entryFlags = 0;
        if (entry.TextPresent)
        {
            entryFlags |= 0x0001;  // TEXT_PRESENT
        }

        if (entry.SoundPresent)
        {
            entryFlags |= 0x0002;  // SND_PRESENT
        }

        if (entry.SoundLengthPresent)
        {
            entryFlags |= 0x0004;  // SND_LENGTH
        }

        writer.Write(entryFlags);

        // Write sound resref (16 bytes, null-padded)
        byte[] resrefBytes = new byte[16];
        byte[] sourceBytes = Encoding.ASCII.GetBytes(soundResref);
        Array.Copy(sourceBytes, resrefBytes, Math.Min(sourceBytes.Length, 16));
        writer.Write(resrefBytes);

        writer.Write((uint)0);  // volume variance (unused)
        writer.Write((uint)0);  // pitch variance (unused)
        writer.Write(currentTextOffset);
        writer.Write(textLength);
        writer.Write((uint)0);  // sound length (unused - note: Python writes uint32 here, not float)

        textOffset += (int)textLength;
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

