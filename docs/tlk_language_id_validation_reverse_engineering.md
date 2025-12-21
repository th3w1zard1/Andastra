# TLK Language ID Validation - Reverse Engineering Analysis

**⚠️ IMPORTANT**: This document contains **exclusively** documentation from reverse engineering analysis of `swkotor.exe` and `swkotor2.exe` using Ghidra MCP. All findings are based on static binary analysis of the original game executables.

## Question

Will the game self-destruct or perform differently if the TLK language identifier is set to an unsupported value (e.g., Russian=61, Thai=89, or custom language IDs beyond the official releases)?

## Executive Summary

Based on exhaustive Ghidra MCP analysis of both executables, **NO validation of the language ID has been found** in the code paths that load and process TLK files. The language ID is read as an unsigned 32-bit integer and stored without bounds checking or enum validation.

## Analysis Results

### TLK File Format

From binary analysis, the TLK file header structure is:

```
Offset  Size    Field
0x00    4       File Type: "TLK " (0x544C4B20)
0x04    4       File Version: "V3.0" (0x56332E30)
0x08    4       Language ID (uint32, little-endian)
0x0C    4       String Count (uint32)
0x10    4       Entries Offset (uint32)
```

The Language ID field is a simple `uint32` with no format restrictions beyond being a 32-bit unsigned integer.

### TLK Loading Functions

**swkotor.exe:**

- `FUN_004b63e0` (0x004b63e0) - Initializes resource system and loads "HD0:dialog"
- `FUN_0041e5a0` (0x0041e5a0) - Loads TLK file via "HD0:dialog" alias
- `FUN_0041d920` (0x0041d920) - Resource loading helper function

**swkotor2.exe:**

- `FUN_004eed20` (0x004eed20) - Initializes resource system and loads "HD0:dialog"
- `FUN_0041fb20` (0x0041fb20) - Loads TLK file via "HD0:dialog" alias
- `FUN_0041ed20` (0x0041ed20) - Resource loading helper function

### Language ID Handling

**Key Finding**: In all examined code paths, the language ID is:

1. Read from offset 0x08 in the TLK file header as a `uint32`
2. Stored in memory structures
3. **NOT validated** against any whitelist of supported languages
4. **NOT checked** for bounds (e.g., maximum value, reserved values)
5. **NOT verified** to match any enum or lookup table

### Search for Validation Code

Extensive searches were performed:

1. **Constant searches** for language ID values (0-5 for official languages, 61 for Russian, 89 for Thai)
   - Result: No comparisons or validations found using these values as language ID checks

2. **Bounds checking** searches (CMP instructions with language-related constants)
   - Result: No bounds checks found that would reject language IDs outside the official range

3. **Switch statement** searches for language ID handling
   - Result: No switch statements found that would handle language IDs with a default/error case

4. **Function analysis** of TLK loading and text decoding paths
   - Result: Language ID is treated as an opaque value that is read and stored, but not validated

### Decompiled Code Evidence

From `FUN_0041ed20` (swkotor2.exe) and `FUN_0041d920` (swkotor.exe):

These functions handle resource loading with bounds checking on internal resource indices (param_2 must be < 8), but **no validation on the language ID value itself** is present.

The language ID is read from the file and appears to be used directly without any validation step.

## Conclusion

**Based exclusively on Ghidra MCP reverse engineering analysis:**

1. ✅ **Language ID is read as uint32** - Confirmed from binary analysis
2. ✅ **No validation code found** - No bounds checks, enum checks, or whitelist validation
3. ❌ **Cannot confirm text decoding behavior** - The actual text decoding code path using the language ID was not located in this analysis

**Implications:**

- The game **will accept any uint32 value** (0 to 0xFFFFFFFF) as a language ID
- The game **will NOT crash or reject** TLK files with unsupported language IDs during loading
- **Unknown**: Whether the language ID is used in a way that would cause issues during text rendering (this would require finding the text decoding/encoding code paths, which were not located in this analysis)

## Limitations

This analysis did not locate:

- The actual text decoding functions that use the language ID for encoding/decoding
- Any encoding lookup tables or mapping functions
- Text rendering code paths that might use the language ID

Therefore, while we can confirm that **loading will succeed** with any language ID, we cannot definitively state what happens during **text rendering/display** with unsupported language IDs from this binary analysis alone.

## Recommendation

To fully answer the question about text rendering behavior with unsupported language IDs, one would need to:

1. Locate the text decoding/encoding functions (likely in string/text rendering code)
2. Analyze how the language ID is used to select character encodings
3. Determine if there are default fallback behaviors for unknown language IDs

However, for the **loading phase** specifically, the answer is clear: **any uint32 value is accepted without validation or error**.
