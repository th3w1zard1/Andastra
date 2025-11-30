using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using TSLPatcher.Core.Common;

namespace TSLPatcher.Core.Formats.NCS
{

    /// <summary>
    /// Reads NCS (NWScript Compiled Script) files.
    ///
    /// NCS files contain compiled bytecode for NWScript, the scripting language used in KotOR.
    /// Instructions include operations, constants, function calls, jumps, and control flow.
    ///
    /// References:
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:28-40 (NCS header reading)
    ///     vendor/reone/src/libs/script/format/ncsreader.cpp:42-195 (instruction reading)
    ///     vendor/xoreos-tools/src/nwscript/decompiler.cpp (NCS decompilation)
    ///     vendor/xoreos-docs/specs/torlack/ncs.html (NCS format specification)
    /// </summary>
    public class NCSBinaryReader : IDisposable
    {
        private const byte NCS_HEADER_MAGIC_BYTE = 0x42;
        private const int NCS_HEADER_SIZE = 13; // "NCS " (4) + "V1.0" (4) + magic_byte (1) + size (4)

        private readonly RawBinaryReader _reader;
        [CanBeNull]
        private NCS _ncs;
        private readonly Dictionary<int, NCSInstruction> _instructions = new Dictionary<int, NCSInstruction>();
        private readonly List<(NCSInstruction instruction, int jumpToOffset)> _jumps = new List<(NCSInstruction instruction, int jumpToOffset)>();
        public bool AutoClose { get; set; } = true;

        public NCSBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
        }

        public NCSBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
        }

        public NCSBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
        }

        /// <summary>
        /// Loads an NCS file from the reader.
        ///
        /// Returns:
        ///     NCS: The loaded NCS object
        ///
        /// Raises:
        ///     ValueError - Corrupt NCS.
        ///     IOException - some operating system issue occurred.
        ///
        /// Processing Logic:
        ///     - Reads the file type and version headers
        ///     - Reads each instruction from the file into a dictionary
        ///     - Resolves jump offsets to reference the target instructions
        ///     - Adds the instructions to the NCS object
        ///     - Optionally closes the reader.
        /// </summary>
        public NCS Load()
        {
            _ncs = new NCS();

            string fileType = _reader.ReadString(4, "ascii");
            string fileVersion = _reader.ReadString(4, "ascii");

            if (fileType != "NCS ")
            {
                throw new InvalidDataException("The file type that was loaded is invalid.");
            }

            if (fileVersion != "V1.0")
            {
                throw new InvalidDataException("The NCS version that was loaded is not supported.");
            }

            _instructions.Clear();
            _jumps.Clear();

            // Read the header fields
            // vendor/reone/src/libs/script/format/ncsreader.cpp:31-32
            byte magicByte = _reader.ReadUInt8(); // Position 8
            uint totalSize = _reader.ReadUInt32(bigEndian: true); // Positions 9-12: Total file size

            // Validate header
            if (magicByte != NCS_HEADER_MAGIC_BYTE)
            {
                throw new InvalidDataException(
                    $"Invalid NCS header magic byte: expected 0x{NCS_HEADER_MAGIC_BYTE:X2}, got 0x{magicByte:X2}");
            }

            // Validate size field
            int actualFileSize = _reader.Size;
            if (totalSize > actualFileSize)
            {
                throw new InvalidDataException(
                    $"NCS size field ({totalSize}) is larger than actual file size ({actualFileSize}). " +
                    "File may be corrupted or truncated.");
            }

            // Check for empty or minimal NCS files
            if (totalSize <= NCS_HEADER_SIZE)
            {
                // File has only a header, no instructions
                // This is technically valid but unusual
                _ncs.Instructions = new List<NCSInstruction>();
                return _ncs;
            }

            // Now at position 13, read instructions until we reach total_size
            // total_size is the end position (includes the header)
            int codeEndPosition = (int)totalSize;

            // Safety: don't read beyond actual file size
            int safeEndPosition = Math.Min(codeEndPosition, actualFileSize);

            while (_reader.Position < safeEndPosition && _reader.Remaining > 0)
            {
                int offset = _reader.Position;

                try
                {
                    _instructions[offset] = ReadInstruction();
                }
                catch (InvalidDataException e)
                {
                    string errorMsg = e.Message;

                    // Check if this is zero-padding that slipped through due to incorrect size field
                    if (errorMsg.Contains("Unknown NCS bytecode 0x00"))
                    {
                        // Peek ahead to confirm this is just padding
                        _reader.Seek(offset);

                        // Read remaining bytes up to the safe end position
                        int bytesToCheck = Math.Min(_reader.Remaining, safeEndPosition - offset);
                        byte[] remainingData = _reader.ReadBytes(bytesToCheck);

                        bool allZeros = true;
                        foreach (byte b in remainingData)
                        {
                            if (b != 0)
                            {
                                allZeros = false;
                                break;
                            }
                        }

                        if (allZeros)
                        {
                            // This is zero-padding - the size field incorrectly includes padding
                            Console.Error.WriteLine(
                                $"Warning: NCS file has incorrect size field (includes zero-padding). " +
                                $"Size field: {totalSize}, actual code ends at: {offset}, " +
                                $"found {remainingData.Length} bytes of padding");
                            break;
                        }

                        // Not all zeros - this is genuinely corrupted data
                        // Show diagnostic information
                        _reader.Seek(offset);
                        int diagnosticSize = Math.Min(32, _reader.Remaining);
                        byte[] diagnosticBytes = _reader.ReadBytes(diagnosticSize);
                        string diagnosticHex = string.Join(" ", Array.ConvertAll(diagnosticBytes, b => $"{b:X2}"));

                        string enhancedMsg =
                            $"{errorMsg}\n" +
                            $"  File size field: {totalSize}, current offset: {offset}\n" +
                            $"  Next 32 bytes (hex): {diagnosticHex}\n" +
                            "  This indicates the NCS file is genuinely corrupted or uses an unknown format variant.";

                        throw new InvalidDataException(enhancedMsg, e);
                    }

                    // Re-raise other errors with additional context
                    string enhancedErrorMsg = $"Failed to parse NCS instruction at offset {offset}: {errorMsg}";
                    throw new InvalidDataException(enhancedErrorMsg, e);
                }
            }

            foreach ((NCSInstruction instruction, int jumpToOffset) in _jumps)
            {
                // Can be null if target instruction not found
                if (!_instructions.TryGetValue(jumpToOffset, out NCSInstruction targetInstruction))
                {
                    // Try to find the closest instruction offset (in case of calculation errors)
                    int? closestOffset = null;
                    int minDiff = int.MaxValue;
                    foreach (int offset in _instructions.Keys)
                    {
                        int diff = Math.Abs(offset - jumpToOffset);
                        if (diff < minDiff && diff < 16) // Only consider if within 16 bytes
                        {
                            minDiff = diff;
                            closestOffset = offset;
                        }
                    }

                    if (closestOffset.HasValue && _instructions.TryGetValue(closestOffset.Value, out targetInstruction))
                    {
                        // Use the closest match as a workaround
                        Console.Error.WriteLine(
                            $"Warning: Jump target 0x{jumpToOffset:X} not found for instruction at 0x{instruction.Offset:X}. " +
                            $"Using closest match at 0x{closestOffset.Value:X} (difference: {minDiff} bytes)");
                        instruction.Jump = targetInstruction;
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Jump target 0x{jumpToOffset:X} not found for instruction at 0x{instruction.Offset:X}. " +
                            $"Available instruction offsets: {string.Join(", ", _instructions.Keys.Select(k => $"0x{k:X}").Take(10))}...");
                    }
                }
                else
                {
                    instruction.Jump = targetInstruction;
                }
            }

            _ncs.Instructions = new List<NCSInstruction>(_instructions.Values);

            return _ncs;
        }

        private NCSInstruction ReadInstruction()
        {
            int instructionOffset = _reader.Position;
            byte byteCodeValue = _reader.ReadUInt8();
            byte qualifier = _reader.ReadUInt8();

            // Try to convert to NCSByteCode enum
            if (!Enum.IsDefined(typeof(NCSByteCode), byteCodeValue))
            {
                // Provide detailed diagnostic information for unknown bytecodes
                // Read some context bytes around the error position
                int contextSize = 16;
                int contextStart = Math.Max(0, instructionOffset - contextSize);
                int currentPos = _reader.Position;

                _reader.Seek(contextStart);
                int contextBytesToRead = Math.Min(contextSize * 2, _reader.Size - contextStart);
                byte[] contextBytes = _reader.ReadBytes(contextBytesToRead);
                string contextHex = string.Join(" ", Array.ConvertAll(contextBytes, b => $"{b:X2}"));

                _reader.Seek(currentPos); // Restore position

                string msg =
                    $"Unknown NCS bytecode 0x{byteCodeValue:X2} with qualifier 0x{qualifier:X2} at offset {instructionOffset}.\n" +
                    $"  Context (hex): {contextHex}\n" +
                    "  This NCS file may be corrupted, from an unsupported NCS variant, or have an incorrect size field.";

                throw new InvalidDataException(msg);
            }

            NCSByteCode byteCode = (NCSByteCode)byteCodeValue;

            NCSInstructionType instructionType;
            try
            {
                instructionType = NCSInstructionTypeExtensions.FromBytecode(byteCode, qualifier);
            }
            catch (ArgumentException e)
            {
                // Unknown bytecode/qualifier combination
                string msg =
                    $"Unknown NCS instruction type combination: " +
                    $"bytecode=0x{byteCodeValue:X2} ({byteCode}), " +
                    $"qualifier=0x{qualifier:X2} at offset {instructionOffset}.\n" +
                    "  The bytecode is recognized but this qualifier combination is not supported.";

                throw new InvalidDataException(msg, e);
            }

            NCSInstruction instruction = new NCSInstruction
            {
                InsType = instructionType,
                Offset = instructionOffset  // Store the offset where this instruction starts (bytecode position)
            };

            // Special handling for RESERVED opcode - it appears with various qualifiers
            // Treat all RESERVED variants as simple 2-byte no-ops
            if (byteCode == NCSByteCode.RESERVED)
            {
                // Use RESERVED (0x00, 0x00) as the canonical type regardless of actual qualifier
                instruction.InsType = NCSInstructionType.RESERVED;
            }

            // Read instruction arguments based on type
            if (instruction.InsType == NCSInstructionType.CPDOWNSP || instruction.InsType == NCSInstructionType.CPTOPSP
                || instruction.InsType == NCSInstructionType.CPDOWNBP || instruction.InsType == NCSInstructionType.CPTOPBP)
            {
                instruction.Args.Add(_reader.ReadInt32(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTI)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTF)
            {
                instruction.Args.Add(_reader.ReadSingle(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTS)
            {
                ushort length = _reader.ReadUInt16(bigEndian: true);
                instruction.Args.Add(_reader.ReadString(length, "ascii"));
            }
            else if (instruction.InsType == NCSInstructionType.CONSTO)
            {
                // Object constants are stored as signed 32-bit integers, not 16-bit
                // See DeNCS Decoder.java case 4, subcase 6 (OBJECT type uses readSignedInt)
                instruction.Args.Add(_reader.ReadInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.ACTION)
            {
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt8());
            }
            else if (instruction.InsType == NCSInstructionType.MOVSP)
            {
                instruction.Args.Add(_reader.ReadInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.JMP || instruction.InsType == NCSInstructionType.JSR
                || instruction.InsType == NCSInstructionType.JZ || instruction.InsType == NCSInstructionType.JNZ)
            {
                int jumpOffset = _reader.ReadInt32(bigEndian: true) + _reader.Position - 6;
                _jumps.Add((instruction, jumpOffset));
            }
            else if (instruction.InsType == NCSInstructionType.DESTRUCT)
            {
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadInt16(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.DECxSP || instruction.InsType == NCSInstructionType.INCxSP
                || instruction.InsType == NCSInstructionType.DECxBP || instruction.InsType == NCSInstructionType.INCxBP)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.STORE_STATE)
            {
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
                instruction.Args.Add(_reader.ReadUInt32(bigEndian: true));
            }
            else if (instruction.InsType == NCSInstructionType.EQUALTT || instruction.InsType == NCSInstructionType.NEQUALTT)
            {
                // Struct equality comparisons include a size field
                // See DeNCS Decoder.java case 11/12 with qualifier 0x24 (36 = StructStruct)
                instruction.Args.Add(_reader.ReadUInt16(bigEndian: true));
            }
            // All other instructions have no arguments (just opcode + qualifier)

            return instruction;
        }

        public void Dispose()
        {
            if (AutoClose)
            {
                _reader?.Dispose();
            }
        }
    }
}

