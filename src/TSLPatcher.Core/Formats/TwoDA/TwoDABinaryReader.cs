using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TSLPatcher.Core.Formats;

namespace TSLPatcher.Core.Formats.TwoDA;

/// <summary>
/// Reads TwoDA binary data.
/// 1:1 port of Python TwoDABinaryReader from pykotor/resource/formats/twoda/io_twoda.py
/// </summary>
public class TwoDABinaryReader : BinaryFormatReaderBase
{
    private TwoDA? _twoda;

    public TwoDABinaryReader(byte[] data) : base(data)
    {
    }

    public TwoDABinaryReader(string filepath) : base(filepath)
    {
    }

    public TwoDABinaryReader(Stream source) : base(source)
    {
    }

    public TwoDA Load()
    {
        try
        {
            _twoda = new TwoDA();

            Reader.BaseStream.Seek(0, SeekOrigin.Begin);

            // Read header
            string fileType = Encoding.ASCII.GetString(Reader.ReadBytes(4));
            string fileVersion = Encoding.ASCII.GetString(Reader.ReadBytes(4));

            if (fileType != "2DA ")
            {
                throw new InvalidDataException("The file type that was loaded is invalid.");
            }

            if (fileVersion != "V2.b")
            {
                throw new InvalidDataException("The 2DA version that was loaded is not supported.");
            }

            Reader.ReadByte(); // \n

            // Read column headers
            var columns = new List<string>();
            while (Peek() != 0)
            {
                string columnHeader = ReadTerminatedString('\t');
                _twoda.AddColumn(columnHeader);
                columns.Add(columnHeader);
            }

            Reader.ReadByte(); // \0

            // Read row count and row labels
            uint rowCount = Reader.ReadUInt32();
            int columnCount = _twoda.GetWidth();
            int cellCount = (int)(rowCount * columnCount);

            for (int i = 0; i < rowCount; i++)
            {
                string rowHeader = ReadTerminatedString('\t');
                _twoda.AddRow(rowHeader);
            }

            // Read cell offsets
            var cellOffsets = new List<int>();
            for (int i = 0; i < cellCount; i++)
            {
                cellOffsets.Add(Reader.ReadUInt16());
            }

            Reader.ReadUInt16(); // data size (not used during reading)
            long cellDataOffset = Reader.BaseStream.Position;

            // Read cell values
            for (int i = 0; i < cellCount; i++)
            {
                int columnId = i % columnCount;
                int rowId = i / columnCount;
                string columnHeader = columns[columnId];

                Reader.BaseStream.Seek(cellDataOffset + cellOffsets[i], SeekOrigin.Begin);
                string cellValue = ReadTerminatedString('\0');
                _twoda.SetCellString(rowId, columnHeader, cellValue);
            }

            return _twoda;
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException("Invalid 2DA file format - unexpected end of file.");
        }
    }

    private byte Peek()
    {
        long pos = Reader.BaseStream.Position;
        byte b = Reader.ReadByte();
        Reader.BaseStream.Seek(pos, SeekOrigin.Begin);
        return b;
    }

    private string ReadTerminatedString(char terminator)
    {
        var sb = new StringBuilder();
        while (true)
        {
            byte b = Reader.ReadByte();
            if (b == terminator)
            {
                break;
            }

            sb.Append((char)b);
        }
        return sb.ToString();
    }
}

