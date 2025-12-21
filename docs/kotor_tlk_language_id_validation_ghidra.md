# KOTOR TLK Language ID Validation - Ghidra Reverse Engineering Analysis

**⚠️ IMPORTANT**: This document contains **exclusively** reverse engineering analysis from Ghidra MCP of `swkotor.exe` and `swkotor2.exe` regarding TLK language ID validation.

## Question

**Will the game self-destruct or perform differently if a TLK file uses an unsupported language identifier (e.g., Russian = 61, or a completely custom value)?**

## TLK File Format Context

The TLK (Talk Table) file header structure:

| Offset | Size | Type | Name | Description |
|--------|------|------|------|-------------|
| 0x00 | 4 | char[4] | FileType | `"TLK "` (4 bytes, space-padded) |
| 0x04 | 4 | char[4] | FileVersion | `"V3.0"` (4 bytes, space-padded) |
| 0x08 | 4 | uint32 | LanguageID | **Language identifier** |
| 0x0C | 4 | uint32 | StringCount | Number of string entries |
| 0x10 | 4 | uint32 | EntriesOffset | Offset to entry table |

**Language ID Location**: Offset 8 (0x08) in the TLK file header, stored as a 32-bit unsigned integer (uint32).

## Analysis Status

**⚠️ INCOMPLETE**: Ghidra MCP session terminated during analysis. The following findings are based on available decompilation results:

### swkotor.exe Analysis

**TLK Loading Path** (from previous documentation):
- `FUN_005e6680` (0x005e6680) → `FUN_005e7a90` (0x005e7a90) → Initializes directory aliases
- TLK loading function not fully analyzed due to session termination
- String reference: `"HD0:dialog"` found at 0x007454fc, referenced by `FUN_004b63e0` (0x004b63e0)

**Note**: The actual TLK file reading function that extracts the language ID from offset 8 was not fully analyzed before session termination.

### swkotor2.exe Analysis

**TLK Loading Path**:
- Similar structure to swkotor.exe
- String reference: `"HD0:dialog"` found at 0x007be2b4, referenced by `FUN_004eed20` (0x004eed20)
- Function analysis incomplete due to session termination

## Expected Behavior (Based on Binary Structure)

Based on the TLK file format and typical binary file reading patterns:

### Language ID Reading

The language ID is read as a **raw uint32** from offset 8:
- No format validation beyond uint32 type
- No range checking expected
- No whitelist validation expected

### Validation Expectations

**Expected**: The game reads the language ID directly without validation:
- Language ID stored as uint32
- Used for encoding selection (if encoding lookup exists)
- Unknown language IDs likely use default encoding

**⚠️ NEEDS VERIFICATION**: Full decompilation of TLK loading function required to confirm:
1. Whether language ID is validated against a known list
2. Whether bounds checking occurs
3. What happens with unknown language IDs (encoding fallback behavior)
4. Whether invalid language IDs cause errors or crashes

## Conclusion

**Analysis Status**: ⚠️ **INCOMPLETE** - Ghidra MCP session terminated before complete analysis

**Next Steps Required**:
1. Locate exact TLK file reading function in both executables
2. Decompile function that reads TLK header (offset 8 = language ID)
3. Trace language ID usage after reading
4. Analyze encoding selection logic
5. Verify error handling for unknown language IDs

**Current Evidence**:
- Language ID stored as uint32 (no inherent validation)
- TLK loading uses direct file I/O (not resource system)
- No evidence of language ID validation found in analyzed code paths

---

**Document Generated**: Based on partial Ghidra MCP analysis (session terminated before completion)  
**Analysis Date**: Incomplete - requires re-analysis  
**Methodology**: Ghidra MCP decompilation (interrupted)

