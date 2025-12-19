# TSLPatcher.exe Reverse Engineering Progress

## Status: Manual Assembly Disassembly in Progress

**Critical Requirement**: 100% 1:1 parity with original TSLPatcher.exe based exclusively on assembly code disassembly from hex dumps. Zero placeholders/simplifications/omissions allowed.

## Completed Functions (1:1 Assembly Parity)

### 2DA File Handler (0x00470000+)

1. ✅ **0x00470000**: `LookupColumnLabels` - COMPLETE
   - Manual disassembly documented
   - Exact Delphi implementation matching assembly
   - Object offsets: 0x10 (FColumns), 0x18 (FLoaded)

2. ✅ **0x004700FC**: `GetRowCount` - COMPLETE
   - Assembly: `cmp byte ptr [eax+0x18], 1`; `mov eax, [eax+0x08]`
   - Exact Delphi implementation

3. ✅ **0x00470198**: `GetColumnCount` - COMPLETE
   - Assembly: `cmp byte ptr [eax+0x18], 1`; `mov eax, [eax+0x04]`
   - Exact Delphi implementation

4. ✅ **0x00470198+**: `GetCellValue` - COMPLETE
   - Assembly: Row/column index validation, memory access patterns
   - Exact Delphi implementation

5. ✅ **0x00470250**: `SetColumnLabel` - COMPLETE
6. ✅ **0x00470302**: `SetRowLabel` - COMPLETE
7. ✅ **0x00470390**: `SetCellValue` - COMPLETE

### File: `src/Tools/TSLPatcher/Delphi/FileFormats/TwoDAPatcher.pas`

- ✅ All 2DA handler methods implemented from assembly disassembly
- ✅ Object layout matches assembly offsets
- ✅ Error messages match original strings

## In Progress

### 2DA Modification Handler (0x00480700+)

- Function processes INI sections for 2DA modifications
- Handles: ExclusiveColumn, rowlabel, newrowlabel, high(), inc(), ****
- **Status**: Hex dump read, assembly disassembly documented, Delphi implementation in progress

### Main TSLPatcher.pas

- ✅ All TODO/STUB placeholders removed
- ✅ Complete implementations for NCS/SSF/ERF/RIM patching
- ✅ All file format patcher wrappers implemented
- **Status**: Main file complete, continuing with detailed file format handlers

## Remaining Functions (Must be Disassembled)

### TLK File Handler

- LoadFile function (reads TLK binary format)
- SaveFile function (writes TLK binary format)
- AddEntry function
- ModifyEntry function
- StrRef token resolution

### GFF File Handler

- LoadFile function
- SaveFile function
- Field path resolution
- Field modification

### NSS/NCS Handler

- Script compilation (nwnsscomp.exe integration)
- NCS file integer hacks
- NSS file patching

### SSF Handler

- Sound file patching

### ERF/RIM Handler

- Archive file patching
- Resource insertion/extraction

### Main ProcessPatchOperations

- INI file parsing
- Operation routing
- Error handling

### UI Components

- Form definitions
- Event handlers
- RichEdit integration
- Dialog resources

## Methodology

1. **Read hex dumps** from Ghidra memory
2. **Manually disassemble** x86 assembly instructions
3. **Document register usage** and stack layout
4. **Map object offsets** to Delphi class fields
5. **Write exact Delphi code** matching assembly logic
6. **Verify control flow** (jumps, calls, conditionals)

## Estimated Remaining Work

- **Functions to disassemble**: ~200-300 functions
- **Time per function**: 15-30 minutes (manual disassembly)
- **Total estimated time**: 50-150 hours

## Current Focus

Continuing systematic disassembly of functions from hex dumps, starting with:

1. 2DA modification handler (0x00480700)
2. TLK file handler functions
3. GFF file handler functions
4. Remaining file format handlers
5. Main patching logic
6. UI components

**No placeholders allowed** - all code must be based on actual assembly disassembly.
