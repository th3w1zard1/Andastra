using System;
using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;

namespace TSLPatcher.Core.Mods.NSS;

/// <summary>
/// Represents binary patching modifications for NCS (compiled NWScript) files.
/// Used by [HACKList] in TSLPatcher INI files.
/// </summary>
public class ModificationsNCS : PatcherModifications
{
    public const string DefaultDestination = "Override";

    /// <summary>
    /// List of hack data: (TokenType, Offset, TokenIdOrValue)
    /// TokenType: "StrRef", "2DAMEMORY", "UINT8", "UINT16", "UINT32"
    /// </summary>
    public List<(string TokenType, int Offset, int TokenIdOrValue)> HackData { get; } = new();

    public ModificationsNCS(string filename, bool replace = false)
        : base(filename, replace)
    {
        Action = "Hack ";
    }

    public override object PatchResource(
        byte[] source,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        Apply(source, memory, logger, game);
        return source;
    }

    public override void Apply(
        object mutableData,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        if (mutableData is byte[] ncsBytes)
        {
            ApplyBinaryPatches(ncsBytes, memory, logger, game);
        }
        else
        {
            logger.AddError($"Expected byte[] for ModificationsNCS, but got {mutableData.GetType().Name}");
        }
    }

    /// <summary>
    /// Apply binary patches to NCS byte array.
    /// </summary>
    private void ApplyBinaryPatches(
        byte[] ncsBytes,
        PatcherMemory memory,
        PatchLogger logger,
        Game game)
    {
        using var stream = new System.IO.MemoryStream(ncsBytes);
        using var writer = new System.IO.BinaryWriter(stream);

        foreach (var (tokenType, offset, tokenIdOrValue) in HackData)
        {
            logger.AddVerbose($"HACKList {SourceFile}: seeking to offset {offset:#X}");
            writer.Seek(offset, System.IO.SeekOrigin.Begin);

            int value;
            string lowerTokenType = tokenType.ToLower();

            if (lowerTokenType == "strref")
            {
                if (!memory.MemoryStr.TryGetValue(tokenIdOrValue, out int memoryStrVal))
                {
                    throw new KeyNotFoundException($"StrRef{tokenIdOrValue} was not defined before use");
                }
                value = memoryStrVal;
                logger.AddVerbose($"HACKList {SourceFile}: writing unsigned WORD {value} at offset {offset:#X}");
                WriteUInt16BigEndian(writer, (ushort)value);
            }
            else if (lowerTokenType == "2damemory")
            {
                if (!memory.Memory2DA.TryGetValue(tokenIdOrValue, out string? memoryVal))
                {
                    throw new KeyNotFoundException($"2DAMEMORY{tokenIdOrValue} was not defined before use");
                }

                // Memory value cannot be a path in HACKList
                if (memoryVal is string stringVal)
                {
                    value = int.Parse(stringVal);
                    logger.AddVerbose($"HACKList {SourceFile}: writing unsigned WORD {value} at offset {offset:#X}");
                    WriteUInt16BigEndian(writer, (ushort)value);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Memory value cannot be !FieldPath in [HACKList] patches, got '{memoryVal}'");
                }
            }
            else
            {
                value = tokenIdOrValue;

                if (lowerTokenType == "uint32")
                {
                    logger.AddVerbose($"HACKList {SourceFile}: writing unsigned DWORD (32-bit) {value} at offset {offset:#X}");
                    WriteUInt32BigEndian(writer, (uint)value);
                }
                else if (lowerTokenType == "uint16")
                {
                    logger.AddVerbose($"HACKList {SourceFile}: writing unsigned WORD (16-bit) {value} at offset {offset:#X}");
                    WriteUInt16BigEndian(writer, (ushort)value);
                }
                else if (lowerTokenType == "uint8")
                {
                    logger.AddVerbose($"HACKList {SourceFile}: writing unsigned BYTE (8-bit) {value} at offset {offset:#X}");
                    writer.Write((byte)value);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown token type '{tokenType}' in HACKList patch");
                }
            }
        }
    }

    /// <summary>
    /// Writes a UInt16 in big-endian format (network byte order).
    /// </summary>
    private static void WriteUInt16BigEndian(System.IO.BinaryWriter writer, ushort value)
    {
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }

    /// <summary>
    /// Writes a UInt32 in big-endian format (network byte order).
    /// </summary>
    private static void WriteUInt32BigEndian(System.IO.BinaryWriter writer, uint value)
    {
        writer.Write((byte)((value >> 24) & 0xFF));
        writer.Write((byte)((value >> 16) & 0xFF));
        writer.Write((byte)((value >> 8) & 0xFF));
        writer.Write((byte)(value & 0xFF));
    }
}

