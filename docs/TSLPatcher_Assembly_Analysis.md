# TSLPatcher.exe Assembly Code Analysis

## Critical Finding: Manual Assembly Analysis Required

Ghidra's auto-analysis does not properly disassemble Delphi binaries. To achieve 1:1 parity, we must manually analyze the assembly code from hex dumps.

## Function at 0x00470000 - 2DA Column Label Lookup

### Hex Dump Analysis:
```
00470000: 55 8B EC          push ebp; mov ebp, esp  (function prologue)
00470003: 33 C9             xor ecx, ecx
00470005: 51 51 51 51       push ecx (4 times - local variables)
00470009: 53                push ebx
0047000A: 56                push esi
0047000B: 57                push edi
0047000C: 89 55 FC          mov [ebp-4], edx  (save parameter 2)
0047000F: 8B F8             mov edi, eax     (save 'this' pointer)
00470011: 8B 45 FC          mov eax, [ebp-4]  (load parameter 2)
00470014: E8 9B 50 F9 FF     call <string function>
```

### Assembly Logic:
1. Standard Delphi function prologue (push ebp; mov ebp, esp)
2. Saves 'this' pointer (eax) to edi
3. Saves second parameter (edx) to [ebp-4]
4. Calls string function on parameter 2
5. Checks offset 0x18 in object: `80 7F 18 01` = `cmp byte ptr [edi+0x18], 1`
6. If not loaded, raises exception with string at 0x00470064: "No 2da file has been loaded. Unable to look up column labels."

### Delphi Equivalent:
```delphi
function TTwoDAFile.LookupColumnLabels(const ColumnName: string): Integer;
begin
  if FLoaded <> 1 then  // Check offset 0x18
    raise Exception.Create('No 2da file has been loaded. Unable to look up column labels.');
  // ... rest of logic
end;
```

## Next Steps for 1:1 Parity:

1. **Systematically disassemble each function** from hex dumps
2. **Document exact register usage** and stack layout
3. **Map Delphi object offsets** to assembly memory accesses
4. **Write Delphi code** that matches exact assembly logic
5. **Verify control flow** matches (jumps, calls, conditionals)

## Functions Identified:

- 0x00470000: LookupColumnLabels (2DA file)
- 0x00470064: String "No 2da file has been loaded..."
- 0x00470094: GetRowCount function
- 0x004700FC: GetColumnCount function
- 0x00470198: GetCellValue function
- 0x00470250: SetColumnLabel function
- 0x00470302: SetRowLabel function
- 0x00470390: SetCellValue function

Each function must be manually reverse engineered from assembly to achieve 1:1 parity.

