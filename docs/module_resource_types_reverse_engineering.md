# Module Resource Types - Reverse Engineering Documentation

**⚠️ IMPORTANT**: This document contains **exclusively** documentation from reverse engineered components of `swkotor.exe` and `swkotor2.exe` using Ghidra MCP. All findings are based on analysis of the original game executables, not third-party engine rewrites or custom implementations.

## Table of Contents

1. [Resource Loading System Overview](#resource-loading-system-overview)
2. [Module File Loading Priority](#module-file-loading-priority)
3. [Resource Type Support](#resource-type-support)
4. [Resource Search Priority Order](#resource-search-priority-order)
5. [Files Loaded Outside Resource System](#files-loaded-outside-resource-system)
6. [Special Resource Behaviors](#special-resource-behaviors)
7. [Implementation Reference](#implementation-reference)

---

## Resource Loading System Overview

### Core Functions

**Primary Resource Search Functions:**

- **swkotor.exe**: `FUN_00407230` (0x00407230) - Core resource search
- **swkotor2.exe**: `FUN_00407300` (0x00407300) - Core resource search (equivalent)

**Resource Lookup Wrappers:**

- **swkotor.exe**: `FUN_004074d0` (0x004074d0) - Calls `FUN_00407230`
- **swkotor2.exe**: `FUN_004075a0` (0x004075a0) - Calls `FUN_00407300`

**Module Loading Functions:**

- **swkotor.exe**: `FUN_004094a0` (0x004094a0) - Module file discovery and loading
- **swkotor2.exe**: `FUN_004096b0` (0x004096b0) - Module file discovery and loading

**Resource Registration:**

- **swkotor.exe**: `FUN_0040e990` (0x0040e990) - Registers resources in resource table
  - **Duplicate Handling**: Line 36 checks for existing ResRef+Type combination
  - **Behavior**: If duplicate found AND resource already loaded, returns 0 (ignores duplicate)
  - **Result**: **First resource registered wins** - later duplicates are silently ignored

### Resource Loading Mechanism

Resources are loaded through a **two-phase system**:

1. **Registration Phase**: Module files are opened and all resources are registered in the resource table (via `FUN_0040e990`)
2. **Lookup Phase**: When a resource is needed, the search function (`FUN_00407230`/`FUN_00407300`) searches through registered resources in priority order

**Key Principle**: Resources are registered **in the order files are loaded**. The first resource with a given ResRef+Type combination wins. Later duplicates are ignored.

---

## Module File Loading Priority

### Loading Modes

The game uses two loading modes controlled by a flag at offset 0x54 in the resource manager structure:

- **Simple Mode** (flag at offset 0x54 == 0): Loads `.rim` file directly, returns immediately
  - **When**: Used when flag is explicitly set to 0
  - **Behavior**: Only `.rim` file is loaded, function returns immediately after loading
  - **Evidence**: swkotor.exe: `FUN_004094a0` line 32: `if (*(int *)((int)param_1 + 0x54) == 0)`
  
- **Complex Mode** (flag at offset 0x54 != 0): Checks for area files (`_a.rim`, `_adx.rim`), then `.mod`, then `_s.rim`/`_dlg.erf` files
  - **When**: Used when flag is non-zero (default behavior)
  - **Behavior**: Checks for `_a.rim` first, then `_adx.rim`, then `.mod`, then `_s.rim`/`_dlg.erf`
  - **Evidence**: swkotor.exe: `FUN_004094a0` line 49-216 (else branch when flag != 0)

**Flag Control**: The flag at offset 0x54 is part of the resource manager structure (`param_1`).

**Initialization**: In `FUN_00409bf0` (resource manager creation, swkotor.exe: 0x00409bf0), offset 0x54 is NOT explicitly set, so it defaults to 0 (zero-initialized by `operator_new`). Analysis of FUN_00409bf0 confirms no writes to offset 0x54 occur during initialization.

**Setting the Flag**: The flag is set to 1 (enabling Complex Mode) by `FUN_004064f0` (swkotor.exe: 0x004064f0):

- **Function**: `void __thiscall FUN_004064f0(void *this, int *param_1, undefined4 param_2, undefined4 param_3)`
- **Line 9**: `*(undefined4 *)((int)this + 0x54) = param_2;`
- **Called from**: `FUN_004c4150` (swkotor.exe: 0x004c4150) line 90 with `param_2=1` (hardcoded literal)
- **When**: Called when loading a module/game (specifically during module localization loading)
- **Evidence**: swkotor.exe: `FUN_004c4150` line 90: `FUN_004064f0(DAT_007a39e8,local_40,1,*(undefined4 *)((int)this + 0x1c8));`
- **Critical Finding**: Cross-reference analysis confirms `FUN_004064f0` is the ONLY function that writes to offset 0x54 in the resource manager structure (DAT_007a39e8). No other functions write to this offset in the resource manager context.

**swkotor2.exe Equivalents**:

- Setting function: `FUN_004065e0` (swkotor2.exe: 0x004065e0) - identical behavior, line 9 sets offset 0x54
- Called from: `FUN_004fd2a0` (swkotor2.exe: 0x004fd2a0) line 98 with `param_2=1` (hardcoded literal)
- Checking function: `FUN_004096b0` (swkotor2.exe: 0x004096b0) line 36: `if (*(int *)((int)param_1 + 0x54) == 0)`
- **Critical Finding**: Cross-reference analysis confirms `FUN_004065e0` is the ONLY function that writes to offset 0x54 in the resource manager structure (DAT_008283c0). No other functions write to this offset in the resource manager context.

**Condition Summary - FULLY VERIFIED**:

- **Simple Mode (flag == 0)**:
  - **Initial State**: Default state when resource manager is created by `FUN_00409bf0`. Offset 0x54 is zero-initialized to 0 by `operator_new`.
  - **Persists if**: `FUN_004064f0`/`FUN_004065e0` is never called (e.g., during main menu or when not loading a module)
  - **No conditional logic**: There is NO condition that sets the flag to 0 - it simply remains 0 if not explicitly set to 1

- **Complex Mode (flag == 1)**:
  - **Trigger**: Flag becomes 1 when `FUN_004064f0`/`FUN_004065e0` is called during module/game loading
  - **Value is always 1**: `param_2` is ALWAYS 1 (hardcoded literal) - there is NO conditional logic that determines if it should be 0 or 1
  - **When called**: During module localization loading (`FUN_004c4150` in swkotor.exe, `FUN_004fd2a0` in swkotor2.exe)
  - **State transition**: Once set to 1, the flag remains 1 for the lifetime of the resource manager instance

**Conclusion**: The flag operates as a simple state indicator:

- **0 = Simple Mode**: Initial/unset state (zero-initialized)
- **1 = Complex Mode**: Set state (unconditionally set to 1 when module localization loads)
- **No conditional logic exists**: The value is never conditionally determined - it's either 0 (default) or 1 (unconditionally set). The only "condition" is whether `FUN_004064f0`/`FUN_004065e0` is called at all.

**Full Function Decompilation** (swkotor.exe: `FUN_004094a0`):

- **Total Size**: 1621 bytes (0x655 bytes)
- **Total Lines**: 227 lines of decompiled code
- **Entry Point**: 0x004094a0
- **Called From**: Background thread function `FUN_00409b90` (swkotor.exe: 0x00409b90 line 10)
- **Complete Decompilation**: Available via Ghidra MCP - all 227 lines analyzed

**When Each Mode Is Used**:

- **Simple Mode (flag == 0)**:
  - **Default state**: When the resource manager is first created by `FUN_00409bf0`, offset 0x54 is zero-initialized to 0
  - **Remains 0 if**: `FUN_004064f0`/`FUN_004065e0` is never called (e.g., during main menu or when not loading a module)
  - **Usage**: Used for simple resource loading scenarios, typically for main menu resources or when only `.rim` files need to be loaded

- **Complex Mode (flag == 1)**:
  - **Triggered when**: `FUN_004064f0` (swkotor.exe) / `FUN_004065e0` (swkotor2.exe) is called with `param_2=1`
  - **Called from**: Module loading functions (`FUN_004c4150` in swkotor.exe, `FUN_004fd2a0` in swkotor2.exe) during module/game loading, specifically when loading localization data
  - **Usage**: Required for full module loading with area files (`_a.rim`, `_adx.rim`), module files (`.mod`), and save state files (`_s.rim`/`_dlg.erf`)

**In practice**: The flag transitions from 0 (Simple Mode) to 1 (Complex Mode) when a module/game is loaded. Simple Mode is the initial state, Complex Mode is activated during gameplay.

**When `.mod` is loaded**: `.mod` files are loaded in **complex mode** (swkotor.exe: `FUN_004094a0` line 95-136). The check for `.mod` happens after checking for `_a.rim` and `_adx.rim`, but if `.mod` exists, it replaces all other files.

### K1 Module File Loading (swkotor.exe: `FUN_004094a0`)

#### Simple Mode (Flag == 0)

**Loading Sequence:**

1. **`.rim`** (line 42) - Loaded directly, function returns

**Evidence**: swkotor.exe: `FUN_004094a0` lines 32-42

#### Complex Mode (Flag != 0)

**Exact Loading Sequence** (from Ghidra decompilation):

```c
// Lines 50-62: Check for _a.rim
if (FUN_00407230(..., ARE type 0xbba) != 0) {
    // Line 159: Load _a.rim
    FUN_00406e20(param_1, aiStack_38, 4, 0);
    // Line 162: Continue to _adx.rim check
}

// Lines 64-88: Check for _adx.rim
if (FUN_00407230(..., ARE type 0xbba) != 0) {
    // Line 85: Load _adx.rim
    FUN_00406e20(param_1, aiStack_38, 4, 0);
}

// Line 91: Load MODULES: directory
FUN_00406e20(param_1, aiStack_48, 2, 0);

// Lines 95-142: Check for .mod
if (FUN_00407230(..., MOD type 0x7db) != 0) {
    // Line 136: Load .mod (REPLACES .rim and _s.rim)
    FUN_00406e20(param_1, aiStack_38, 3, 2);
} else {
    // Lines 97-124: Check for _s.rim
    if (FUN_00407230(..., ARE type 0xbba) != 0) {
        // Line 118: Load _s.rim (ADDS to .rim)
        FUN_00406e20(param_1, aiStack_38, 4, 0);
    }
}
```

**Complete Priority Order** (Low to High - Registration Order):

| Priority | File | Behavior | Condition | Evidence |
|----------|------|----------|-----------|----------|
| **1 (Lowest)** | `.rim` | **NOT LOADED** in complex mode | Only loaded in simple mode (flag == 0) | swkotor.exe: `FUN_004094a0` line 32-42 |
| **2** | `_a.rim` | **REPLACES** `.rim` | Loaded if ARE found in `_a.rim` | swkotor.exe: `FUN_004094a0` line 61, 159 |
| **3** | `_adx.rim` | **ADDS** to `_a.rim` | Loaded if `_a.rim` not found AND ARE found in `_adx.rim` | swkotor.exe: `FUN_004094a0` line 74, 85 |
| **4** | `_s.rim` | **ADDS** to `.rim`/`_a.rim`/`_adx.rim` | Loaded if `.mod` NOT found AND ARE found in `_s.rim` | swkotor.exe: `FUN_004094a0` line 107, 118 |
| **5 (Highest)** | `.mod` | **REPLACES** all other files | Loaded if found, **skips** `_s.rim` | swkotor.exe: `FUN_004094a0` line 95, 136 |

**Critical Findings:**

1. **`.rim` is NOT loaded in complex mode** - It's only loaded in simple mode (flag == 0)
2. **`_a.rim` REPLACES `.rim`** - If `_a.rim` exists, `.rim` is never loaded
3. **`_adx.rim` ADDS to `_a.rim`** - If `_a.rim` not found, `_adx.rim` can be loaded
4. **`.mod` REPLACES everything** - If `.mod` exists, `.rim`, `_s.rim`, `_a.rim`, and `_adx.rim` are NOT loaded
5. **`_s.rim` ADDS to base** - Only loaded if `.mod` doesn't exist

**Example: All Files Exist (`_a.rim` + `_adx.rim` + `_s.rim` + `.rim` + `.mod`)**

**Exact Ghidra Execution Flow** (swkotor.exe: `FUN_004094a0`):

```c
// Complex mode (flag != 0)
// Line 61: Check if _a.rim exists (search for ARE type 0xbba)
iVar6 = FUN_00407230(param_1, aiStack_28, 0xbba, aiStack_48, aiStack_50);
if (iVar6 != 0) {
    // Line 159: _a.rim FOUND - Load it
    iVar7 = FUN_00406e20(param_1, aiStack_38, 4, 0);
    // Line 162: Continue to _adx.rim check (goto LAB_00409646)
}

// Line 74: Check if _adx.rim exists
iVar6 = FUN_00407230(param_1, aiStack_28, 0xbba, aiStack_50, aiStack_48);
if (iVar6 != 0) {
    // Line 85: _adx.rim FOUND - Load it
    iVar7 = FUN_00406e20(param_1, aiStack_38, 4, 0);
}

// Line 95: Check if .mod exists
iVar6 = FUN_00407230(param_1, aiStack_28, 0x7db, aiStack_50, aiStack_48);
if (iVar6 != 0) {
    // Line 136: .mod FOUND - Load it (REPLACES everything)
    iVar7 = FUN_00406e20(param_1, aiStack_38, 3, 2);
    // SKIPS _s.rim check (line 96: if (iVar6 == 0) - false, so doesn't enter)
} else {
    // Line 107: Check if _s.rim exists (only if .mod NOT found)
    // NOT REACHED in this example because .mod exists
}
```

**Loading Order:**

1. Check `_a.rim` → **FOUND** → Load `_a.rim` (line 159)
2. Check `_adx.rim` → **FOUND** → Load `_adx.rim` (line 85)
3. Check `.mod` → **FOUND** → Load `.mod` (line 136) → **SKIPS** `_s.rim`
4. `.rim` is **NOT LOADED** (only loaded in simple mode, flag == 0)

**Result**: Only `_a.rim`, `_adx.rim`, and `.mod` are loaded. `.rim` and `_s.rim` are ignored.

**Resource Resolution** (if same ResRef+Type exists in multiple files):

- `_a.rim` resources registered first → **WIN** (for resources not in `.mod`)
- `_adx.rim` resources registered second → **IGNORED** if duplicate of `_a.rim`
- `.mod` resources registered third → **WIN** (overrides `_a.rim` and `_adx.rim` for all duplicates)

**Example: All Files Exist WITHOUT `.mod` (`_a.rim` + `_adx.rim` + `_s.rim` + `.rim`)**

**Exact Ghidra Execution Flow**:

```c
// Line 61: Check _a.rim → FOUND → Load _a.rim (line 159)
// Line 74: Check _adx.rim → FOUND → Load _adx.rim (line 85)
// Line 95: Check .mod → NOT FOUND (iVar6 == 0)
// Line 96: if (iVar6 == 0) → TRUE, enter else block
// Line 107: Check _s.rim → FOUND → Load _s.rim (line 118)
```

**Loading Order:**

1. `_a.rim` → Loaded (line 159)
2. `_adx.rim` → Loaded (line 85)
3. `_s.rim` → Loaded (line 118) - because `.mod` NOT found
4. `.rim` → **NOT LOADED** (only in simple mode)

**Result**: `_a.rim`, `_adx.rim`, and `_s.rim` are loaded. `.rim` is ignored.

**Resource Resolution**:

- `_a.rim` resources registered first → **WIN**
- `_adx.rim` resources registered second → **IGNORED** if duplicate of `_a.rim`
- `_s.rim` resources registered third → **IGNORED** if duplicate of `_a.rim` or `_adx.rim`

### K2 Module File Loading (swkotor2.exe: `FUN_004096b0`)

#### Simple Mode (Flag == 0)

**Loading Sequence:**

1. **`.rim`** (line 46) - Loaded directly, function returns

**Evidence**: swkotor2.exe: `FUN_004096b0` lines 36-46

#### Complex Mode (Flag != 0)

**Exact Loading Sequence** (from Ghidra decompilation):

```c
// Lines 54-66: Check for _a.rim
if (FUN_00407300(..., ARE type 0xbba) != 0) {
    // Line 182: Load _a.rim
    FUN_00406ef0(param_1, aiStack_58, 4, 0);
    // Line 185: Continue to _adx.rim check
}

// Lines 68-92: Check for _adx.rim
if (FUN_00407300(..., ARE type 0xbba) != 0) {
    // Line 89: Load _adx.rim
    FUN_00406ef0(param_1, aiStack_58, 4, 0);
}

// Line 95: Load MODULES: directory
FUN_00406ef0(param_1, aiStack_60, 2, 0);

// Lines 99-165: Check for .mod
if (FUN_00407300(..., MOD type 0x7db) != 0) {
    // Line 161: Load .mod (REPLACES .rim, _s.rim, _dlg.erf)
    FUN_00406ef0(param_1, aiStack_58, 3, 2);
} else {
    // Lines 101-127: Check for _s.rim
    if (FUN_00407300(..., ARE type 0xbba) != 0) {
        // Line 122: Load _s.rim (ADDS to .rim)
        FUN_00406ef0(param_1, aiStack_58, 4, 0);
    }
    
    // Lines 128-149: Load _dlg.erf (ONLY if .mod NOT found)
    // Line 128: Construct "_dlg" suffix
    // Line 147: Load _dlg.erf (ADDS to .rim and _s.rim)
    FUN_00406ef0(param_1, piVar3, 3, 2);
}
```

**Complete Priority Order** (Low to High - Registration Order):

| Priority | File | Behavior | Condition | Evidence |
|----------|------|----------|-----------|----------|
| **1 (Lowest)** | `.rim` | **NOT LOADED** in complex mode | Only loaded in simple mode (flag == 0) | swkotor2.exe: `FUN_004096b0` line 36-46 |
| **2** | `_a.rim` | **REPLACES** `.rim` | Loaded if ARE found in `_a.rim` | swkotor2.exe: `FUN_004096b0` line 65, 182 |
| **3** | `_adx.rim` | **ADDS** to `_a.rim` | Loaded if `_a.rim` not found AND ARE found in `_adx.rim` | swkotor2.exe: `FUN_004096b0` line 78, 89 |
| **4** | `_s.rim` | **ADDS** to `.rim`/`_a.rim`/`_adx.rim` | Loaded if `.mod` NOT found AND ARE found in `_s.rim` | swkotor2.exe: `FUN_004096b0` line 111, 122 |
| **5** | `_dlg.erf` | **ADDS** to `.rim`/`_s.rim` | Loaded if `.mod` NOT found (K2 only) | swkotor2.exe: `FUN_004096b0` line 128, 147 |
| **6 (Highest)** | `.mod` | **REPLACES** all other files | Loaded if found, **skips** `_s.rim` and `_dlg.erf` | swkotor2.exe: `FUN_004096b0` line 99, 161 |

**Critical Findings:**

1. **`_dlg.erf` is K2-only** - Hardcoded check at line 128: `FUN_00630a90(aiStack_30, "_dlg")`
2. **`_dlg.erf` only loads if `.mod` NOT found** - Inside the `if (iVar5 == 0)` block at line 100
3. **Same replacement rules as K1** - `.mod` replaces everything, `_a.rim` replaces `.rim`

### Module File Combination Matrix

**Question**: What happens when multiple files exist with the same resource (ResRef+Type)?

**Answer**: Files are loaded in priority order. Resources are registered as files are loaded. **First registration wins** - duplicates are ignored.

#### K1 Combinations

| Files Present | Files Loaded | Registration Order | Duplicate Resolution |
|---------------|-------------|-------------------|---------------------|
| `.rim` only | `.rim` | 1. `.rim` | N/A |
| `.rim` + `_s.rim` | `.rim` + `_s.rim` | 1. `.rim`<br>2. `_s.rim` | `.rim` wins (registered first) |
| `.rim` + `_a.rim` | `_a.rim` only | 1. `_a.rim` | `_a.rim` wins (`.rim` not loaded) |
| `.rim` + `_a.rim` + `_adx.rim` | `_a.rim` + `_adx.rim` | 1. `_a.rim`<br>2. `_adx.rim` | `_a.rim` wins (registered first) |
| `.rim` + `_a.rim` + `_adx.rim` + `_s.rim` | `_a.rim` + `_adx.rim` + `_s.rim` | 1
. `_a.rim`<br>2. `_adx.rim`<br>3. `_s.rim` | `_a.rim` wins (registered first) |
| `.rim` + `.mod` | `.mod` only | 1. `.mod` | `.mod` wins (`.rim` not loaded) |
| `.rim` + `_s.rim` + `.mod` | `.mod` only | 1. `.mod` | `.mod` wins (`.rim` and `_s.rim` not loaded) |
| `_a.rim` + `_adx.rim` + `_s.rim` + `.rim` + `.mod` | `_a.rim` + `_adx.rim` + `.mod` | 1. `_a.rim`<br>2. `_adx.rim`<br>3. `.mod` | `.mod` wins (registered last, overrides all) |

#### K2 Combinations

| Files Present | Files Loaded | Registration Order | Duplicate Resolution |
|---------------|-------------|-------------------|---------------------|
| `.rim` only | `.rim` | 1. `.rim` | N/A |
| `.rim` + `_s.rim` + `_dlg.erf` | `.rim` + `_s.rim` + `_dlg.erf` | 1. `.rim`<br>2. `_s.rim`<br>3. `_dlg.erf` | `.rim` wins (registered first) |
| `.rim` + `_a.rim` + `_adx.rim` + `_s.rim` + `_dlg.erf` | `_a.rim` + `_adx.rim` + `_s.rim` + `_dlg.erf` | 1. `_a.rim`<br>2. `_adx.rim`<br>3. `_s.rim`<br>4. `_dlg.erf` | `_a.rim` wins (registered first) |
| All files + `.mod` | `_a.rim` + `_adx.rim` + `.mod` | 1. `_a.rim`<br>2. `_adx.rim`<br>3. `.mod` | `.mod` wins (`.rim`, `_s.rim`, `_dlg.erf` not loaded) |

### File Behavior Summary

| File | Behavior | Replaces | Adds To | Evidence |
|------|----------|----------|---------|----------|
| `.rim` | Base module file | N/A | N/A | Simple mode only |
| `_a.rim` | Area-specific RIM | **REPLACES** `.rim` | N/A | swkotor.exe: `FUN_004094a0` line 159 |
| `_adx.rim` | Extended area RIM | N/A | **ADDS** to `_a.rim` or `.rim` | swkotor.exe: `FUN_004094a0` line 85 |
| `_s.rim` | Save state RIM | N/A | **ADDS** to `.rim`/`_a.rim`/`_adx.rim` | swkotor.exe: `FUN_004094a0` line 118 |
| `_dlg.erf` | Dialog ERF (K2 only) | N/A | **ADDS** to `.rim`/`_s.rim` | swkotor2.exe: `FUN_004096b0` line 147 |
| `.mod` | Module override | **REPLACES** all other files | N/A | swkotor.exe: `FUN_004094a0` line 136 |

---

## Resource Type Support

### Resource Types Using Resource System

Resources that use `FUN_004074d0`/`FUN_004075a0` (which calls `FUN_00407230`/`FUN_00407300`) **CAN** be placed in modules and will be found by the resource system.

**Complete List** (verified via Ghidra analysis of all callers to `FUN_004074d0`):

| Resource Type | ID (Hex) | ID (Dec) | Handler Function | Executable | Evidence |
|--------------|----------|----------|------------------|------------|----------|
| TGA | 3 | 3 | `FUN_0070ee30` | swkotor.exe: 0x0070ee30 | Line 43: `FUN_004074d0(..., 3)` |
| WAV | 4 | 4 | `FUN_005d5e90` | swkotor.exe: 0x005d5e90 | Line 43: `FUN_004074d0(..., 4)` |
| PLT | 6 | 6 | `FUN_0070dbf0` | swkotor.exe: 0x0070dbf0 | Line 43: `FUN_004074d0(..., 6)` |
| TPC | 0xbbf | 3007 | `FUN_0070f800` | swkotor.exe: 0x0070f800 | Line 43: `FUN_004074d0(..., 0xbbf)` |
| FourPC | 0x80b | 2059 | `FUN_00710910` | swkotor.exe: 0x00710910 | Line 43: `FUN_004074d0(..., 0x80b)` |
| DDS | 0x7f1 | 2033 | `FUN_00710530` | swkotor.exe: 0x00710530 | Line 43: `FUN_004074d0(..., 0x7f1)` |
| IFO | 0x7de | 2014 | `FUN_004c4cc0` | swkotor.exe: 0x004c4cc0 | Line 43: `FUN_004074d0(..., 0x7de)` |
| UTI | 0x7e9 | 2025 | `FUN_006bdea0` | swkotor.exe: 0x006bdea0 | Line 96: `FUN_004074d0(..., 0x7e9)` |
| LIP | 0xbbc | 3004 | `FUN_0070c350` | swkotor.exe: 0x0070c350 | Line 43: `FUN_004074d0(..., 0xbbc)` |
| MDL | 0x7d2 | 2002 | `FUN_00710180` | swkotor.exe: 0x00710180 | Line 43: `FUN_004074d0(..., 0x7d2)` |
| TXI | 0x7e6 | 2022 | `FUN_0070fb90` | swkotor.exe: 0x0070fb90 | Line 43: `FUN_004074d0(..., 0x7e6)` |
| LTR | 0x7f4 | 2036 | `FUN_00711110` | swkotor.exe: 0x00711110 | Line 43: `FUN_004074d0(..., 0x7f4)` |
| SSF | 0x80c | 2060 | `FUN_006789a0` | swkotor.exe: 0x006789a0 | Line 43: `FUN_004074d0(..., 0x80c)` |
| MDX | 0xbc0 | 3008 | `FUN_0070fe60` | swkotor.exe: 0x0070fe60 | Line 43: `FUN_004074d0(..., 0xbc0)` |
| ARE | 0x7dc | 2012 | `FUN_00506c30` | swkotor.exe: 0x00506c30 | Line 43: `FUN_004074d0(..., 0x7dc)` |
| NCS | 0x7da | 2010 | `FUN_005d1ac0` | swkotor.exe: 0x005d1ac0 | Line 43: `FUN_004074d0(..., 0x7da)` |
| VIS | 0xbb9 | 3001 | `FUN_0070f0f0` | swkotor.exe: 0x0070f0f0 | Line 43: `FUN_004074d0(..., 0xbb9)` |
| LYT | 3000 | 3000 | `FUN_005de5f0` | swkotor.exe: 0x005de5f0 | Line 43: `FUN_004074d0(..., 3000)` |
| Unknown | 0x7e1 | 2017 | `FUN_00413b40` | swkotor.exe: 0x00413b40 | Line 43: `FUN_004074d0(..., 0x7e1)` |

**Note**: This list was compiled by analyzing all callers of `FUN_004074d0` in swkotor.exe using Ghidra MCP. Each handler function calls `FUN_004074d0` with a specific resource type ID, proving that these resource types use the resource system and can be placed in modules.

### Resource Types NOT Using Resource System

| Resource Type | ID (Hex) | ID (Dec) | Loading Method | Evidence |
|--------------|----------|----------|----------------|----------|
| TLK | 0x7e2 | 2018 | Direct file I/O | swkotor.exe: `FUN_0041d810` (0x0041d810) opens file with "rb" mode, uses directory alias system, NOT resource system |
| RES | 0x0 | 0 | Direct file I/O | swkotor.exe: `FUN_004b8300` loads "savenfo.res" directly via `FUN_00411260` (GFF loader), bypasses `FUN_00407230` |
| NFO | N/A | N/A | Direct file I/O | Same as RES - loaded as "savenfo.res" via direct file I/O |
| MVE | 2 | 2 | Direct file I/O | swkotor.exe: `FUN_005e7a90` sets up "MOVIES:" directory alias, not resource system |
| MPG | 9 | 9 | Direct file I/O | Same as MVE - uses "MOVIES:" directory alias |
| BIK | 0x80f | 2063 | Direct file I/O | swkotor.exe: `FUN_005fbbf0` uses "MOVIES:" directory alias, also direct file I/O at `FUN_00602d40` |
| OGG | 0x81e | 2078 | ❌ Not supported | Not registered in resource type registry, no handler found |
| MP3 | 8 | 8 | ❌ Registered but not loaded | Registered in resource type registry but no handler calls `FUN_004074d0` |
| WMV | 12 | 12 | ❌ Unknown | Resource type exists but no handler found |
| NSS | 0x7d9 | 2009 | ❌ **NOT LOADED** | Registered in resource type registry (swkotor.exe: `FUN_005e6d20` line 42) but **NO handler found** that calls `FUN_004074d0`. **NSS files are source files** - they must be compiled to NCS (compiled scripts) before the game can use them. The game engine does NOT load NSS files directly from modules. |

---

## Resource Search Priority Order

### Global Resource Search Priority (VERIFIED via Ghidra)

**Function**: `FUN_00407230` (swkotor.exe: 0x00407230) / `FUN_00407300` (swkotor2.exe: 0x00407300)

**Complete Priority Order** (Highest to Lowest - Search Order):

**VERIFIED via Ghidra decompilation** (`FUN_00407230` lines 8-16):

1. **Override Directory** (`this+0x14`, Location 3, Source Type 2) - **HIGHEST PRIORITY**
   - Files in `Override/` folder
   - **Evidence**: Line 8: `FUN_004071a0((undefined4 *)((int)this + 0x14),...)` - searched FIRST

2. **Module Containers - First Variant** (`this+0x18`, Location 2, Source Type 3, param_6=1) - **HIGH PRIORITY**
   - `.mod` files (MOD containers)
   - **Evidence**: Line 10: `FUN_004071a0((undefined4 *)((int)this + 0x18),...,1)` - searched SECOND

3. **Module RIM Files** (`this+0x1c`, Location 1, Source Type 4) - **MEDIUM PRIORITY**
   - `.rim` files (simple mode only)
   - `_a.rim` files
   - `_adx.rim` files
   - `_s.rim` files
   - **Evidence**: Line 12: `FUN_004071a0((undefined4 *)((int)this + 0x1c),...)` - searched THIRD

4. **Module Containers - Second Variant** (`this+0x18`, Location 2, Source Type 3, param_6=2) - **MEDIUM-LOW PRIORITY**
   - `_dlg.erf` files (K2 only, ERF containers)
   - **Evidence**: Line 14: `FUN_004071a0((undefined4 *)((int)this + 0x18),...,2)` - searched FOURTH

5. **Chitin Archives** (`this+0x10`, Location 0, Source Type 1) - **LOWEST PRIORITY**
   - BIF files from `chitin.key`
   - `patch.erf` (K1 only, loaded during global initialization)
   - **Evidence**: Line 16: `FUN_004071a0((undefined4 *)((int)this + 0x10),...)` - searched LAST

**Note**: This priority order applies to **ALL resource types** that use the resource system. There is no special handling for different resource types in the search order.

### Module File Registration Order (VERIFIED via Ghidra)

**Critical**: Resources are registered in the order files are loaded. **First registration wins** - duplicates are ignored (verified in `FUN_0040e990` line 36).

#### K1 Complex Mode Registration Order (swkotor.exe: `FUN_004094a0`)

**VERIFIED via Ghidra decompilation**:

1. **`_a.rim`** (if found) - Line 159: `FUN_00406e20(param_1,aiStack_38,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: ARE type 0xbba found in `_a.rim` (line 61 check)

2. **`_adx.rim`** (if found) - Line 85: `FUN_00406e20(param_1,aiStack_38,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: `_a.rim` NOT found AND ARE type 0xbba found in `_adx.rim` (line 74 check)

3. **`.mod`** (if found) - Line 136: `FUN_00406e20(param_1,aiStack_38,3,2)`
   - Registered to Location 2 (`this+0x18`, Source Type 3, param_6=2)
   - **Condition**: MOD type 0x7db found (line 95 check)
   - **CRITICAL**: If `.mod` exists, `_s.rim` is NOT loaded (line 96: `if (iVar6 == 0)` - false when .mod found)

4. **`_s.rim`** (if `.mod` NOT found) - Line 118: `FUN_00406e20(param_1,aiStack_38,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: `.mod` NOT found AND ARE type 0xbba found in `_s.rim` (line 107 check)

5. **`.rim`** - **NOT LOADED in complex mode**
   - Only loaded in simple mode (flag == 0, line 32-42)

#### K2 Complex Mode Registration Order (swkotor2.exe: `FUN_004096b0`)

**VERIFIED via Ghidra decompilation**:

1. **`_a.rim`** (if found) - Line 182: `FUN_00406ef0(param_1,aiStack_58,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: ARE type 0xbba found in `_a.rim` (line 65 check)

2. **`_adx.rim`** (if found) - Line 89: `FUN_00406ef0(param_1,aiStack_58,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: `_a.rim` NOT found AND ARE type 0xbba found in `_adx.rim` (line 78 check)

3. **`.mod`** (if found) - Line 161: `FUN_00406ef0(param_1,aiStack_58,3,2)`
   - Registered to Location 2 (`this+0x18`, Source Type 3, param_6=2)
   - **Condition**: MOD type 0x7db found (line 99 check)
   - **CRITICAL**: If `.mod` exists, `_s.rim` and `_dlg.erf` are NOT loaded (line 100: `if (iVar5 == 0)` - false when .mod found)

4. **`_s.rim`** (if `.mod` NOT found) - Line 122: `FUN_00406ef0(param_1,aiStack_58,4,0)`
   - Registered to Location 1 (`this+0x1c`, Source Type 4)
   - **Condition**: `.mod` NOT found AND ARE type 0xbba found in `_s.rim` (line 111 check)

5. **`_dlg.erf`** (if `.mod` NOT found) - Line 147: `FUN_00406ef0(param_1,piVar3,3,2)`
   - Registered to Location 2 (`this+0x18`, Source Type 3, param_6=2)
   - **Condition**: `.mod` NOT found (line 128, inside `if (iVar5 == 0)` block)

6. **`.rim`** - **NOT LOADED in complex mode**
   - Only loaded in simple mode (flag == 0, line 36-46)

### Complete Resource Resolution Priority Chain

**Example**: Resource `test.ncs` exists in `.rim`, `_dlg.erf`, `_s.rim`, `_adx.rim`, and `_a.rim`.

**Resolution Order** (when resource is requested):

1. **Override Directory** - If `test.ncs` exists in `Override/`, it wins (HIGHEST PRIORITY)
2. **`.mod` file** - If `.mod` exists and contains `test.ncs`, it wins (Location 2, searched second)
3. **`_a.rim`** - If `_a.rim` exists and contains `test.ncs`, it wins (Location 1, searched third)
   - **Registration**: Registered FIRST in complex mode (if found)
   - **Search**: Searched THIRD in resource lookup
4. **`_adx.rim`** - If `_adx.rim` exists and contains `test.ncs`, it wins (Location 1, searched third)
   - **Registration**: Registered SECOND in complex mode (if `_a.rim` not found)
   - **Search**: Searched THIRD in resource lookup
   - **Duplicate Handling**: If `_a.rim` also contains `test.ncs`, `_a.rim` wins (registered first)
5. **`_s.rim`** - If `_s.rim` exists and contains `test.ncs`, it wins (Location 1, searched third)
   - **Registration**: Registered FOURTH in complex mode (if `.mod` not found)
   - **Search**: Searched THIRD in resource lookup
   - **Duplicate Handling**: If `_a.rim` or `_adx.rim` also contains `test.ncs`, earlier registered file wins
6. **`_dlg.erf`** - If `_dlg.erf` exists and contains `test.ncs`, it wins (Location 2, searched fourth)
   - **Registration**: Registered FIFTH in complex mode K2 (if `.mod` not found)
   - **Search**: Searched FOURTH in resource lookup
   - **Duplicate Handling**: If `.mod` also contains `test.ncs`, `.mod` wins (searched earlier)
7. **`.rim`** - If `.rim` exists and contains `test.ncs`, it wins (Location 1, searched third)
   - **Registration**: NOT LOADED in complex mode (only in simple mode)
   - **Search**: Searched THIRD in resource lookup (if registered)
8. **`patch.erf`** - If `patch.erf` exists and contains `test.ncs`, it wins (Location 0, searched last)
   - **Registration**: Loaded during global initialization (K1 only)
   - **Search**: Searched LAST (LOWEST PRIORITY)
9. **Chitin BIF files** - If BIF contains `test.ncs`, it wins (Location 0, searched last)
   - **Search**: Searched LAST (LOWEST PRIORITY)

**Key Principle**:


- **Registration order** determines which file wins when same resource exists in multiple files at the same location
- **Search order** determines which location wins when same resource exists in different locations
- **First registered wins** for duplicates at the same location
- **Higher search priority wins** for duplicates at different locations

### patch.erf Priority Position

**VERIFIED via Ghidra**:

- **Location**: `this+0x10` (Location 0, Source Type 1) - Same location as Chitin BIF files
- **Search Priority**: **LOWEST** - Searched LAST in `FUN_00407230` (line 16)
- **Priority Order**:
  1. Override Directory (Location 3) - HIGHEST
  2. Module Containers - First Variant (Location 2, param_6=1) - `.mod` files
  3. Module RIM Files (Location 1) - `_a.rim`, `_adx.rim`, `_s.rim`, `.rim`
  4. Module Containers - Second Variant (Location 2, param_6=2) - `_dlg.erf` files
  5. **Chitin Archives (Location 0) - LOWEST** - Includes `patch.erf` and BIF files

**Conclusion**: `patch.erf` has the **LOWEST priority** - it is searched AFTER override, modules, and all other locations. Resources in `patch.erf` will only be used if they don't exist in any other location.

### Override Directory Structure

#### K1 (swkotor.exe: `FUN_0040f200`)

- **Structure**: **FLAT** - Only top-level files are loaded
- **Subfolders**: **NOT searched**
- **Evidence**: `FUN_0040f200` calls `FUN_005e6640` with `param_5=0` (no recursion)

#### K2 (swkotor2.exe: `FUN_00410d20`)

- **Structure**: **1 level deep** - Root files + immediate subdirectories
- **Priority**: Root files registered **FIRST** (highest priority), then subdirectory files
- **Subfolders**: Only immediate subdirectories (1 level deep) are searched
- **Files at 2+ levels deep**: **IGNORED**
- **Evidence**: swkotor2.exe: `FUN_00410d20` lines 158-200

---

## Files Loaded Outside Resource System

**CRITICAL**: Many files are loaded via direct file I/O, bypassing the resource system entirely. These files **CANNOT** be placed in module containers and will not be found by the resource loader.

### Direct File I/O Files (VERIFIED via Ghidra)

1. **`dialog.tlk` (TLK files)**
   - **Loading**: Direct file I/O from game root directory
   - **Function**: swkotor.exe: `FUN_0041d810` (0x0041d810)
   - **Evidence**: Line 14: `FUN_005e5a90(local_14,"rb");` - opens file with "rb" mode, then calls `FUN_005e68d0` which uses directory alias system, NOT resource system
   - **Module Support**: ❌ **NO** - TLK files in modules will be ignored

2. **`swkotor.ini` / `swkotor2.ini` (Configuration files)**
   - **Loading**: Direct file I/O via Windows INI API
   - **Function**: swkotor.exe: `FUN_0061b780` (0x0061b780)
   - **Module Support**: ❌ **NO**

3. **Audio/Video Files via Direct Directory Access**
   - **MVE/MPG/BIK**: Direct file I/O via "MOVIES:" directory alias
   - **Function**: swkotor.exe: `FUN_005e7a90` (0x005e7a90) sets up directory mappings
   - **Module Support**: ❌ **NO** - Must be placed in `movies/` directory

4. **Save Game Files (`.sav`)**
   - **Loading**: Direct file I/O from `saves/` directory
   - **Module Support**: ❌ **NO**

---

## Special Resource Behaviors

### RES, PT, NFO Priority: Save Files vs Modules/Override/Patch.erf

**Question**: If the same `.res`, `PARTYTABLE.res`, or `savenfo.res` exists in `patch.erf`, `override`, or modules, how does the engine prioritize these sources relative to save file resources?

**Answer**: **It depends on the file type** - RES and NFO bypass the resource system, PT uses the resource system.

#### RES Files (Resource Type 0)

**Priority**: ❌ **Module/Override/Patch.erf RES files are IGNORED**

**Evidence**:

- swkotor.exe: `FUN_004b8300` line 136-155 loads "savenfo.res" directly via `FUN_00411260` (GFF loader)
- `FUN_00411260` does not call `FUN_00407230` (resource system)
- RES files are accessed directly from save file path, bypassing resource system entirely

**Conclusion**: RES files in modules, override, or patch.erf will **NOT** override save file RES files. Only RES files in save files are loaded.

#### PT Files (PARTYTABLE.res)

**Priority**: ✅ **Module/Override/Patch.erf PT files WILL override save file PT files**

**Evidence**:

- swkotor.exe: `FUN_00565d20` (0x00565d20) loads PARTYTABLE
- swkotor.exe: `FUN_00565d20` line 51: Calls `FUN_00410630` with "PARTYTABLE" and "PT  " signature
- swkotor.exe: `FUN_00410630` line 48: Calls `FUN_00407680` with resource type
- swkotor.exe: `FUN_00407680` line 12: Calls `FUN_00407230` (resource system search function)

**Complete Priority Order** (from `FUN_00407230`):

1. **Override Directory** (Location 3, Source Type 2) - Highest priority
2. **Module Containers** (Location 2, Source Type 3) - `.mod` files
3. **Module RIM Files** (Location 1, Source Type 4) - `.rim`, `_s.rim`, `_a.rim`, `_adx.rim`
4. **Chitin Archives** (Location 0, Source Type 1) - **Includes patch.erf** (K1 only)
5. **Save Files** (GAMEINPROGRESS: directory) - Lowest priority

**Conclusion**: If PARTYTABLE.res exists in override, modules, or patch.erf, it will be loaded **BEFORE** the save file version.

#### NFO Files (savenfo.res)

**Priority**: ❌ **Module/Override/Patch.erf NFO files are IGNORED**

**Evidence**:

- swkotor.exe: `FUN_004b8300` line 136-155 loads "savenfo.res" directly via `FUN_00411260` (GFF loader)
- `FUN_00411260` does not call `FUN_00407230` (resource system)
- NFO files are accessed directly from save file path, bypassing resource system entirely

**Conclusion**: NFO files in modules, override, or patch.erf will **NOT** override save file NFO files. Only NFO files in save files are loaded.

### WAV Audio Files

**Obfuscation Support**: ✅ **Supports BOTH obfuscated and unobfuscated**

- **Can the game load standard, non-obfuscated `.wav` files?** ✅ **YES** - Standard RIFF/WAVE files work without any obfuscation layer
- **Obfuscation is OPTIONAL, not required** - The game auto-detects format and handles both:
  - **Obfuscated SFX**: 470-byte header (magic: `0xFF 0xF3 0x60 0xC4`) - skip 470 bytes, then standard RIFF/WAVE follows
  - **Obfuscated VO**: 20-byte header (magic: `"RIFF"` at 0, `"RIFF"` at 20) - skip 20 bytes, then standard RIFF/WAVE follows
  - **MP3-in-WAV**: 58-byte header (RIFF size = 50) - skip 58 bytes, then raw MP3 data follows
  - **Standard WAV**: No obfuscation header - file starts directly with standard RIFF/WAVE format (bytes 0-3 = "RIFF", bytes 8-11 = "WAVE")

**Module Support**: ✅ **YES** - WAV handler uses resource system that searches modules

- **Handler**: swkotor.exe: `FUN_005d5e90` (0x005d5e90) line 43: `FUN_004074d0(..., 4)`

### DDS Textures

**Support**: ✅ **YES** - DDS is fully supported

- **Handler**: swkotor.exe: 0x00710530 (`LoadDDSTexture`), swkotor2.exe: 0x00783b60
- **Type ID**: 2033 (0x7f1)
- **Resource Search**: Uses `FUN_004074d0` with type 0x7f1, which searches **all locations including modules, override, and BIF files**

**Priority**: **DDS has NO priority in the automatic texture loading chain**

- **Main texture loader** (`FUN_00596670` at swkotor.exe: 0x00596670) only checks:
  1. **TGA** (type 3) - checked first
  2. **TPC** (type 0xbbf/3007) - checked if TGA not found
  3. **DDS is NOT checked** in this automatic priority chain

- **DDS loading**: DDS textures are loaded via separate explicit path (`FUN_0070e400` at swkotor.exe: 0x0070e400), not through the automatic TGA→TPC fallback system

**Module Support**: **✅ YES - DDS files CAN be placed in modules**

---

## Implementation Reference

### Function Addresses

| Function | K1 Address | K2 Address | Purpose |
|----------|------------|------------|---------|
| Core Resource Search | `FUN_00407230` (0x00407230) | `FUN_00407300` (0x00407300) | Searches all locations for resources |
| Resource Lookup Wrapper | `FUN_004074d0` (0x004074d0) | `FUN_004075a0` (0x004075a0) | Wrapper that calls core search |
| Module Loading | `FUN_004094a0` (0x004094a0) | `FUN_004096b0` (0x004096b0) | Module file discovery and loading |
| Resource Registration | `FUN_0040e990` (0x0040e990) | `FUN_0040e990` (0x0040e990) | Registers resources in resource table |

### Complete Resource Resolution Example

**Scenario**: Resource `test.ncs` exists in multiple locations: `.rim`, `_dlg.erf`, `_s.rim`, `_adx.rim`, and `_a.rim`.

**Resolution Process** (VERIFIED via Ghidra):

1. **Override Directory Check** (Location 3, searched FIRST)
   - If `test.ncs` exists in `Override/` → **WINS** (HIGHEST PRIORITY)
   - **Evidence**: `FUN_00407230` line 8 searches `this+0x14` first

2. **Module Containers - First Variant** (Location 2, param_6=1, searched SECOND)
   - If `.mod` exists and contains `test.ncs` → **WINS** (if not in Override)
   - **Evidence**: `FUN_00407230` line 10 searches `this+0x18` with param_6=1
   - **Registration**: `.mod` registered to Location 2 with param_3=2 (swkotor.exe: `FUN_004094a0` line 136)

3. **Module RIM Files** (Location 1, searched THIRD)
   - Files registered to Location 1 are searched in registration order:
     - **`_a.rim`** → If contains `test.ncs` → **WINS** (if not in Override or .mod)
       - **Registration**: Registered FIRST in complex mode (swkotor.exe: `FUN_004094a0` line 159)
       - **Evidence**: `FUN_00407230` line 12 searches `this+0x1c`
     - **`_adx.rim`** → If contains `test.ncs` → **WINS** (if `_a.rim` doesn't have it)
       - **Registration**: Registered SECOND in complex mode (swkotor.exe: `FUN_004094a0` line 85)
       - **Duplicate Handling**: If `_a.rim` also has `test.ncs`, `_a.rim` wins (registered first, `FUN_0040e990` line 36 ignores duplicates)
     - **`_s.rim`** → If contains `test.ncs` → **WINS** (if `_a.rim` and `_adx.rim` don't have it)
       - **Registration**: Registered FOURTH in complex mode (swkotor.exe: `FUN_004094a0` line 118)
       - **Condition**: Only registered if `.mod` NOT found
       - **Duplicate Handling**: If `_a.rim` or `_adx.rim` also has `test.ncs`, earlier registered file wins
     - **`.rim`** → If contains `test.ncs` → **WINS** (if others don't have it)
       - **Registration**: NOT LOADED in complex mode (only in simple mode)
       - **Note**: In complex mode, `.rim` is never loaded, so resources in `.rim` are never registered

4. **Module Containers - Second Variant** (Location 2, param_6=2, searched FOURTH)
   - **`_dlg.erf`** (K2 only) → If contains `test.ncs` → **WINS** (if not in earlier locations)
     - **Registration**: Registered FIFTH in complex mode K2 (swkotor2.exe: `FUN_004096b0` line 147)
     - **Condition**: Only registered if `.mod` NOT found
     - **Evidence**: `FUN_00407230` line 14 searches `this+0x18` with param_6=2
     - **Duplicate Handling**: If `.mod` also has `test.ncs`, `.mod` wins (searched earlier, line 10)

5. **Chitin Archives** (Location 0, searched LAST)
   - **`patch.erf`** (K1 only) → If contains `test.ncs` → **WINS** (if not in any earlier location)
     - **Registration**: Loaded during global initialization (not module loading)
     - **Evidence**: `FUN_00407230` line 16 searches `this+0x10` last
   - **BIF files** → If contains `test.ncs` → **WINS** (if not in any earlier location)
     - **Registration**: Loaded during global initialization
     - **Evidence**: `FUN_00407230` line 16 searches `this+0x10` last

**Complete Priority Chain** (Highest to Lowest):

1. **Override Directory** (Location 3) - HIGHEST PRIORITY
2. **`.mod` files** (Location 2, param_6=1) - HIGH PRIORITY
3. **`_a.rim`** (Location 1) - MEDIUM-HIGH PRIORITY (registered first at Location 1)
4. **`_adx.rim`** (Location 1) - MEDIUM PRIORITY (registered second at Location 1, loses to `_a.rim` if duplicate)
5. **`_s.rim`** (Location 1) - MEDIUM-LOW PRIORITY (registered fourth at Location 1, loses to `_a.rim`/`_adx.rim` if duplicate)
6. **`_dlg.erf`** (Location 2, param_6=2, K2 only) - LOW PRIORITY (searched after Location 1, loses to `.mod` if duplicate)
7. **`.rim`** (Location 1) - NOT LOADED in complex mode (only in simple mode)
8. **`patch.erf`** (Location 0, K1 only) - LOWEST PRIORITY (searched last)
9. **Chitin BIF files** (Location 0) - LOWEST PRIORITY (searched last)

**Key Principles**:

- **Search order** determines priority between different locations (Override > Module Containers > Module RIM > Chitin)
- **Registration order** determines priority within the same location (first registered wins for duplicates)
- **First registered wins** - `FUN_0040e990` line 36 checks for duplicates and ignores later registrations
- **Location priority** - Resources in higher-priority locations always win over lower-priority locations, regardless of registration order

### Key Findings Summary

1. **Module files are loaded in a specific order** - First file loaded wins for duplicate resources
2. **`.mod` files completely replace all other module files** - If `.mod` exists, `.rim`, `_s.rim`, `_a.rim`, `_adx.rim`, and `_dlg.erf` are NOT loaded
3. **`_a.rim` replaces `.rim`** - If `_a.rim` exists, `.rim` is NOT loaded in complex mode
4. **`_adx.rim` and `_s.rim` add to base** - They supplement the main module file
5. **Resource type is more important than location** - Different resource types can coexist, but same ResRef+Type combinations follow priority order
6. **Override directory has highest priority** - Files in Override win over all other locations
7. **Many files bypass the resource system** - TLK, RES, NFO, MVE, MPG, BIK use direct file I/O
8. **`patch.erf` has lowest priority** - Searched after all module files and override directory

---

**Document Version**: 2.0  
**Last Updated**: Based on Ghidra MCP analysis of swkotor.exe and swkotor2.exe  
**Analysis Date**: 2025
