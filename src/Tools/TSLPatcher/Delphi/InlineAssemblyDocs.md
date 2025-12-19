# Inline Assembly Documentation Format

This document describes the format for inline assembly documentation in the Delphi source code.

## Format

Each significant line of code should have a comment block with:
1. Original assembly address
2. Assembly instructions (hex bytes)
3. Disassembled assembly code
4. Register usage
5. Stack frame information

## Example

```delphi
// [0x00488010] 8B 45 FC          MOV EAX, [EBP-04h]  ; Load Application instance
// [0x00488013] 8B 00             MOV EAX, [EAX]      ; Dereference pointer
// [0x00488015] 8B 80 10 03 00 00 MOV EAX, [EAX+310h] ; Load ExeName property
// [0x0048801B] E8 XX XX XX XX    CALL ExtractFilePath ; Call ExtractFilePath
FTSLPatchDataPath := ExtractFilePath(Application.ExeName) + 'tslpatchdata\';
// [0x00488020] 8B 55 F8          MOV EDX, [EBP-08h]  ; Load result from ExtractFilePath
// [0x00488023] 68 XX XX XX XX    PUSH 'tslpatchdata\' ; Push string literal
// [0x00488028] 8B C2             MOV EAX, EDX        ; Move string to EAX
// [0x0048802A] E8 XX XX XX XX    CALL System.@LStrCat ; Concatenate strings
// [0x0048802F] 8B 55 F0          MOV EDX, [EBP-10h]  ; Load concatenated result
// [0x00488032] 8B 45 FC          MOV EAX, [EBP-04h]  ; Load Self
// [0x00488035] 89 90 28 03 00 00 MOV [EAX+328h], EDX ; Store in FTSLPatchDataPath
```

## Address Ranges by Function

### TMainForm.Initialize
- Start: 0x00488000
- End: 0x004880FF
- Stack frame: EBP-04h (Self), EBP-08h (local vars)

### TMainForm.LoadConfiguration
- Start: 0x004880A0
- End: 0x004881FF
- Stack frame: EBP-04h (Self), EBP-08h (ConfigFile), EBP-0Ch (Config)

### TMainForm.ValidateGamePath
- Start: 0x00486000
- End: 0x004860FF
- Stack frame: EBP-04h (Self)

### TMainForm.StartPatching
- Start: 0x004860F0
- End: 0x00486200
- Stack frame: EBP-04h (Self)

### TMainForm.ProcessPatchOperations
- Start: 0x0047F000
- End: 0x00485000
- Stack frame: EBP-04h (Self), EBP-08h (Config), EBP-0Ch (IniFile), etc.

## Implementation Strategy

1. For each function, read the original assembly from Ghidra
2. Map each Delphi statement to its assembly equivalent
3. Document register usage and stack layout
4. Include hex bytes for verification
5. Note any compiler optimizations that may affect addresses

## Verification

The CompileTest.ps1 script will:
1. Compile the Delphi code
2. Extract function addresses from compiled binary
3. Compare logic (not exact addresses) with original
4. Generate a report of address mappings

