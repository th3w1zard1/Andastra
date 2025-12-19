# NCS Decompiler/Compiler Low-Level Analysis

## Executive Summary

The decompiler converts NCS (NWScript Compiled Script) bytecode to NSS (NWScript Source) text. The compiler does the reverse. The PRIMARY issue preventing 1:1 bytecode parity in roundtrip tests is that **instructions are being truncated** - files with 121 instructions are only processing 88, causing missing NEGI operations and incorrect constant values.

## Architecture Overview

### Flow: NCS → NSS (Decompilation)

```
1. NCSBinaryReader.Load()
   ├─ Reads NCS file header ("NCS ", "V1.0", magic byte 0x42, size field)
   ├─ Reads instructions from offset 13 to end of file
   ├─ Parses each instruction (opcode + qualifier + args)
   ├─ Stores in Dictionary<int offset, NCSInstruction>
   └─ Returns NCS object with List<NCSInstruction> (sorted by offset)

2. FileDecompiler.DecompileNcsObjectFromFile()
   ├─ Creates NCSBinaryReader
   ├─ Calls reader.Load() → gets NCS with instructions
   └─ Calls DecompileNcsObject(ncs)

3. FileDecompiler.DecompileNcsObject()
   ├─ Validates NCS has instructions
   ├─ Calls NcsToAstConverter.ConvertNcsToAst(ncs) → AST
   ├─ Applies SetPositions → adds position metadata
   ├─ Applies SetDestinations → resolves jump targets
   ├─ Applies SetDeadCode → marks unreachable code
   ├─ Applies DoGlobalVars → processes globals subroutine
   ├─ Applies DoTypes → type inference
   ├─ Applies MainPass → processes main and other subroutines
   └─ Generates NSS code via state.ToStringGlobals() and state.ToString()

4. NcsToAstConverter.ConvertNcsToAst()
   ├─ Analyzes instruction list to find:
   │  ├─ SAVEBP instructions (globals boundary)
   │  ├─ Entry stub pattern (JSR+RETN or JSR+RESTOREBP)
   │  ├─ Subroutine starts (JSR targets)
   │  └─ Main function start/end
   ├─ Creates ASubroutine nodes:
   │  ├─ Globals subroutine (0 to SAVEBP+1)
   │  ├─ Main subroutine (mainStart to mainEnd)
   │  └─ Other subroutines (from JSR targets)
   └─ Returns Start(AProgram, EOF)

5. DoGlobalVars (visitor pattern)
   ├─ Visits globals subroutine AST
   ├─ Processes instructions:
   │  ├─ RSADDI → creates variable declaration
   │  ├─ CONSTI → pushes constant value
   │  ├─ CPDOWNSP → assigns to variable
   │  ├─ MOVSP → adjusts stack
   │  └─ NEGI → negates constant (creates AUnaryExp)
   └─ Generates: "int intGLOB_1 = value;"

6. MainPass (visitor pattern)
   ├─ Visits main subroutine AST
   ├─ Processes instructions similarly to DoGlobalVars
   └─ Generates: "void main() { ... }"

7. ExpressionFormatter.Format()
   ├─ Formats AUnaryExp as: op + inner
   ├─ For NEGI: "-" + constant → "-6"
   └─ Returns formatted expression string
```

### Flow: NSS → NCS (Compilation)

```
1. NCSAuto.CompileNss()
   ├─ Parses NSS source to AST
   ├─ Generates NCS instructions
   └─ Returns NCS object

2. NCSBinaryWriter.Write()
   ├─ Writes NCS header
   ├─ Writes instructions (opcode + qualifier + args)
   └─ Updates size field
```

## Critical Problems Identified

### Problem 1: mainEnd Truncation (FIXED)

**Location**: `NcsToAstConverter.cs` line 666-671

**Issue**: When `isAlternativeMainStart` is true, `mainEnd` was set to `savebpIndex` (82) instead of `instructions.Count` (121). This caused the main subroutine to only process instructions 83-88 instead of 83-120.

**Root Cause**: The code assumed alternative main starts would be updated by split logic, but if split logic didn't run (no ACTION instructions), `mainEnd` stayed at 82.

**Fix**: Always initialize `mainEnd = instructions.Count`, then let split logic update it if needed.

**Impact**: Missing 33 instructions (88-120), including 5 NEGI operations that generate negative constants.

### Problem 2: Empty Main Case mainEnd (FIXED)

**Location**: `NcsToAstConverter.cs` line 787-816

**Issue**: The empty main case correctly sets `mainEnd = instructions.Count`, but the comment and logic didn't emphasize that "empty" main still contains ALL instructions from SAVEBP+1 to end (entry stub, cleanup, RETN).

**Fix**: Added explicit comments and debug logging to ensure `mainEnd` is always `instructions.Count` for empty main case.

### Problem 3: Instructions.Count Mismatch (INVESTIGATING)

**Location**: Unknown - NCS object has 88 instructions instead of 121

**Issue**: `NCSBinaryReader.Load()` reports "Created 121 instructions", but `DecompileNcsObject()` receives NCS with only 88 instructions.

**Possible Causes**:
1. Multiple NCS objects being created (one with 121, one with 88)
2. Instructions being filtered somewhere between Load() and ConvertNcsToAst()
3. Different code path using a different reader

**Investigation**: Added debug logging to trace instruction count at each stage.

### Problem 4: NEGI Instruction Processing (PENDING)

**Location**: `DoGlobalVars.cs` and `SubScriptState.cs`

**Issue**: NEGI instructions at indices 88, 93, 98, 103, 108 are not being processed because they're beyond the truncated mainEnd=88.

**Expected Behavior**:
- NEGI at index 88: negates constant at index 87 → should generate "-6"
- Currently: constant at index 87 generates "6" (positive)

**Fix Status**: Will be fixed automatically once Problem 1 is resolved (instructions 88-120 will be processed).

## Instruction Processing Details

### NCS Instruction Structure

```
Offset 0-12: Header
  - "NCS " (4 bytes)
  - "V1.0" (4 bytes)
  - Magic byte: 0x42 (1 byte)
  - Size field: total file size (4 bytes, big-endian)

Offset 13+: Instructions
  Each instruction:
    - Opcode (1 byte)
    - Qualifier (1 byte)
    - Arguments (variable length)
```

### Key Instruction Types

1. **RSADDI** (0x02, qualifier 0x03): Reserve stack space for integer variable
2. **CONSTI** (0x04, qualifier 0x03): Push integer constant
3. **CPDOWNSP** (0x01): Copy value from stack top to stack offset
4. **MOVSP** (0x1B): Move stack pointer
5. **NEGI** (0x19, qualifier 0x03): Negate integer on stack
6. **SAVEBP** (0x2A): Save base pointer (globals boundary)
7. **JSR** (0x1E): Jump to subroutine
8. **RETN** (0x20): Return from function

### Global Variable Initialization Pattern

```
RSADDI          // Reserve stack space
CONSTI <value>  // Push constant value
CPDOWNSP        // Copy to variable location
MOVSP -4        // Adjust stack
[NEGI]          // Optional: negate value (for negative constants)
CPDOWNSP        // Copy negated value
MOVSP -4        // Adjust stack
```

### Example: `int SW_CONSTANT_DARK_HIT_HIGH = -6;`

```
Instruction 86: RSADDI          // Reserve space for variable
Instruction 87: CONSTI 6       // Push 6
Instruction 88: NEGI           // Negate → -6
Instruction 89: CPDOWNSP        // Copy -6 to variable
Instruction 90: MOVSP -4        // Adjust stack
```

## Debug Logging Added

### NCSBinaryReader
- Logs instruction count and offset range after reading
- Logs ACTION bytecode when encountered
- Logs file size vs size field discrepancies

### NcsToAstConverter
- Logs total instruction count at start
- Logs SAVEBP detection and indices
- Logs entry stub detection
- Logs mainStart and mainEnd calculations
- Logs split logic decisions
- Logs final mainEnd value before creating main subroutine

### FileDecompiler
- Logs instruction count when NCS is loaded
- Logs instruction count when passed to ConvertNcsToAst
- Logs instruction offset ranges

### SubScriptState
- Logs TransformUnary calls
- Logs unary expression creation

## Testing Strategy

1. **Unit Tests**: Test NCSBinaryReader with k_act_com41.ncs to verify 121 instructions are read
2. **Integration Tests**: Test full decompilation flow to verify all 121 instructions are processed
3. **Roundtrip Tests**: Verify decompiled NSS compiles to identical bytecode

## Next Steps

1. ✅ Fix mainEnd truncation bug
2. ✅ Add comprehensive debug logging
3. 🔄 Investigate why instructions.Count is 88 instead of 121
4. 🔄 Verify NEGI instructions are processed correctly once all instructions are included
5. 🔄 Run full test suite and fix remaining issues

## Files Modified

1. `src/Andastra/Parsing/Resource/Formats/NCS/NCSDecomp/Utils/NcsToAstConverter.cs`
   - Fixed mainEnd initialization (always set to instructions.Count)
   - Added debug logging for mainEnd calculations
   - Enhanced empty main case comments

2. `src/Andastra/Parsing/Resource/Formats/NCS/NCSDecomp/FileDecompiler.cs`
   - Added debug logging for instruction counts at each stage
   - Added instruction offset range logging

