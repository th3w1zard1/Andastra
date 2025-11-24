using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TSLPatcher.Core.Formats.NCS;

/// <summary>
/// Writes NCS (NWScript Compiled Script) files.
///
/// References:
///     vendor/reone/src/libs/script/format/ncsreader.cpp:28-40 (NCS header writing)
///     vendor/xoreos-tools/src/nwscript/compiler.cpp (NCS compilation)
///     vendor/xoreos-docs/specs/torlack/ncs.html (NCS format specification)
/// </summary>
public class NCSBinaryWriter
{
    private const byte NCS_HEADER_MAGIC_BYTE = 0x42;
    private const int NCS_HEADER_SIZE = 13;

    private readonly NCS _ncs;
    private readonly Dictionary<int, int> _offsets = new();
    private readonly Dictionary<int, int> _sizes = new();

    public NCSBinaryWriter(NCS ncs)
    {
        _ncs = ncs ?? throw new ArgumentNullException(nameof(ncs));
    }

    public byte[] Write()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        int offset = NCS_HEADER_SIZE;
        foreach (NCSInstruction instruction in _ncs.Instructions)
        {
            int instId = instruction.GetHashCode();
            int instructionSize = DetermineSize(instruction);
            _sizes[instId] = instructionSize;
            _offsets[instId] = offset;
            offset += instructionSize;
        }

        writer.Write(Encoding.ASCII.GetBytes("NCS "));
        writer.Write(Encoding.ASCII.GetBytes("V1.0"));
        writer.Write(NCS_HEADER_MAGIC_BYTE);
        WriteBigEndianUInt32(writer, (uint)offset);

        foreach (NCSInstruction instruction in _ncs.Instructions)
        {
            WriteInstruction(writer, instruction);
        }

        return ms.ToArray();
    }

    private void WriteInstruction(BinaryWriter writer, NCSInstruction instruction)
    {
        (NCSByteCode byteCode, byte qualifier) = instruction.InsType.GetValue();
        writer.Write((byte)byteCode);
        writer.Write(qualifier);

        switch (instruction.InsType)
        {
            case NCSInstructionType.CPDOWNSP:
            case NCSInstructionType.CPTOPSP:
            case NCSInstructionType.CPDOWNBP:
            case NCSInstructionType.CPTOPBP:
                WriteBigEndianInt32(writer, Convert.ToInt32(instruction.Args[0]));
                WriteBigEndianUInt16(writer, Convert.ToUInt16(instruction.Args[1]));
                break;

            case NCSInstructionType.CONSTI:
                WriteBigEndianUInt32(writer, Convert.ToUInt32(instruction.Args[0]));
                break;

            case NCSInstructionType.CONSTF:
                WriteBigEndianSingle(writer, Convert.ToSingle(instruction.Args[0]));
                break;

            case NCSInstructionType.CONSTS:
                {
                    string str = instruction.Args[0].ToString() ?? "";
                    WriteBigEndianUInt16(writer, (ushort)str.Length);
                    writer.Write(Encoding.ASCII.GetBytes(str));
                }
                break;

            case NCSInstructionType.CONSTO:
                WriteBigEndianInt32(writer, Convert.ToInt32(instruction.Args[0]));
                break;

            case NCSInstructionType.ACTION:
                WriteBigEndianUInt16(writer, Convert.ToUInt16(instruction.Args[0]));
                writer.Write(Convert.ToByte(instruction.Args[1]));
                break;

            case NCSInstructionType.MOVSP:
                WriteBigEndianInt32(writer, Convert.ToInt32(instruction.Args[0]));
                break;

            case NCSInstructionType.JMP:
            case NCSInstructionType.JSR:
            case NCSInstructionType.JZ:
            case NCSInstructionType.JNZ:
                {
                    if (instruction.Jump == null)
                    {
                        throw new InvalidOperationException($"Jump instruction {instruction.InsType} has no jump target");
                    }
                    int instId = instruction.GetHashCode();
                    int jumpId = instruction.Jump.GetHashCode();
                    int currentOffset = _offsets[instId];
                    int jumpOffset = _offsets[jumpId];
                    int relativeOffset = jumpOffset - (currentOffset + 6);
                    WriteBigEndianInt32(writer, relativeOffset);
                }
                break;

            case NCSInstructionType.DESTRUCT:
                WriteBigEndianUInt16(writer, Convert.ToUInt16(instruction.Args[0]));
                WriteBigEndianInt16(writer, Convert.ToInt16(instruction.Args[1]));
                WriteBigEndianUInt16(writer, Convert.ToUInt16(instruction.Args[2]));
                break;

            case NCSInstructionType.DECxSP:
            case NCSInstructionType.INCxSP:
            case NCSInstructionType.DECxBP:
            case NCSInstructionType.INCxBP:
                WriteBigEndianUInt32(writer, Convert.ToUInt32(instruction.Args[0]));
                break;

            case NCSInstructionType.STORE_STATE:
                WriteBigEndianUInt32(writer, Convert.ToUInt32(instruction.Args[0]));
                WriteBigEndianUInt32(writer, Convert.ToUInt32(instruction.Args[1]));
                break;

            case NCSInstructionType.EQUALTT:
            case NCSInstructionType.NEQUALTT:
                WriteBigEndianUInt16(writer, Convert.ToUInt16(instruction.Args[0]));
                break;
        }
    }

    private static int DetermineSize(NCSInstruction instruction)
    {
        int size = 2;

        switch (instruction.InsType)
        {
            case NCSInstructionType.CPDOWNSP:
            case NCSInstructionType.CPTOPSP:
            case NCSInstructionType.CPDOWNBP:
            case NCSInstructionType.CPTOPBP:
                size += 6;
                break;

            case NCSInstructionType.CONSTI:
                size += 4;
                break;

            case NCSInstructionType.CONSTF:
                size += 4;
                break;

            case NCSInstructionType.CONSTS:
                {
                    string str = instruction.Args[0].ToString() ?? "";
                    size += 2 + str.Length;
                }
                break;

            case NCSInstructionType.CONSTO:
                size += 4;
                break;

            case NCSInstructionType.ACTION:
                size += 3;
                break;

            case NCSInstructionType.MOVSP:
                size += 4;
                break;

            case NCSInstructionType.JMP:
            case NCSInstructionType.JSR:
            case NCSInstructionType.JZ:
            case NCSInstructionType.JNZ:
                size += 4;
                break;

            case NCSInstructionType.DESTRUCT:
                size += 6;
                break;

            case NCSInstructionType.DECxSP:
            case NCSInstructionType.INCxSP:
            case NCSInstructionType.DECxBP:
            case NCSInstructionType.INCxBP:
                size += 4;
                break;

            case NCSInstructionType.STORE_STATE:
                size += 8;
                break;

            case NCSInstructionType.EQUALTT:
            case NCSInstructionType.NEQUALTT:
                size += 2;
                break;
        }

        return size;
    }

    private static void WriteBigEndianInt16(BinaryWriter writer, short value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndianUInt16(BinaryWriter writer, ushort value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndianInt32(BinaryWriter writer, int value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndianUInt32(BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    private static void WriteBigEndianSingle(BinaryWriter writer, float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }
}

