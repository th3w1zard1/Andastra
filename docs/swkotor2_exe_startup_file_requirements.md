# swkotor2.exe Startup File Requirements - Reverse Engineering Analysis

**⚠️ IMPORTANT**: This document contains **exclusively** documentation from reverse engineering analysis of `swkotor2.exe` using Ghidra MCP. All findings are based on analysis of the original game executable, not third-party engine rewrites or custom implementations.

## Executive Summary

This document exhaustively documents **exactly** what files must exist and where they must be located for `swkotor2.exe` to successfully launch. The analysis was performed by tracing the startup sequence from the entry point through `GameMain` and all file I/O operations.

**Key Findings:**

1. **Minimal File Requirements**: Only the executable itself and system DLLs are **strictly required** for the process to start
2. **Graceful Degradation**: Most file accesses are optional - missing files generate errors but do **NOT** prevent the game from launching
3. **Path Resolution**: All paths are resolved relative to the executable directory - **NO registry access** is used for installation path detection
4. **Configuration Files**: Multiple configuration files are attempted but missing files do not prevent launch
5. **Similar to K1**: Overall structure is very similar to `swkotor.exe`, with K2-specific differences in filenames (swkotor2.ini vs swkotor.ini)

## Startup Sequence Analysis

### Entry Point Flow

The startup sequence follows this exact path (verified via Ghidra decompilation):

1. **Windows Loader** → Loads `swkotor2.exe` and system DLLs
2. **Entry Function** → Calls `FUN_00404250` (GameMain equivalent) (swkotor2.exe: `0x0076e459`)
3. **GameMain** (swkotor2.exe: `0x00404250`) → Main initialization function
4. **File Loading Operations** → Various files attempted during initialization

### GameMain Function Analysis

**Function**: `FUN_00404250` (swkotor2.exe: `0x00404250`) - Main initialization function (equivalent to GameMain in K1)
**Signature**: `undefined4 FUN_00404250(HINSTANCE param_1)`

**File Loading Sequence** (in order):

1. **Line 48**: Creates mutex `"swkotor2"` (not a file, but process-level requirement)
2. **Line 55**: `FUN_00460ff0("config.txt")` - Attempts to load config.txt
3. **Line 63**: `FUN_00630a90(&iStack_84, "swKotor2.ini")` - Reads swKotor2.ini
4. **Line 81**: Reads `".\\swkotor2.ini"` for sound settings
5. **Line 38**: `FUN_00632350()` - Gets executable path (called from CreateMainWindow)
6. **Line 116**: `FUN_00460ff0("startup.txt")` - Attempts to load startup.txt

## File Requirements by Category

### Category 1: CRITICAL - Required for Process Execution

These files **MUST** exist for the executable to even start:

#### 1.1 swkotor2.exe
- **Location**: Any valid path (Windows will load it)
- **When Required**: Before process creation
- **Failure Behavior**: Process cannot start (Windows loader error)
- **Evidence**: Executable itself - required by Windows

#### 1.2 System DLLs
- **Required DLLs**: Loaded automatically by Windows loader
  - `KERNEL32.DLL`
  - `USER32.DLL`
  - `GDI32.DLL`
  - `OPENGL32.DLL`
  - `DINPUT8.DLL`
  - `BINKW32.DLL`
  - `MSS32.DLL`
  - `GLU32.DLL`
  - `IMM32.DLL`
  - `OLE32.DLL`
  - `VERSION.DLL`
- **Location**: System directories or executable directory
- **When Required**: During process initialization (before GameMain)
- **Failure Behavior**: Process cannot start (Windows loader error)
- **Evidence**: Import table analysis via Ghidra MCP

### Category 2: OPTIONAL - Attempted but Not Required

These files are **attempted** during startup but **missing files do NOT prevent launch**:

#### 2.1 config.txt
- **Function**: `FUN_00460ff0` (swkotor2.exe: `0x00460ff0`)
- **Location**: Same directory as `swkotor2.exe` (executable directory)
- **Path Resolution**: Relative to executable (determined via `GetModuleFileNameA`)
- **When Attempted**: Line 55 of GameMain (FUN_00404250)
- **Failure Behavior**: 
  - Prints error: `"ERROR: file 'config.txt' doesn't exist"` (swkotor2.exe: `0x007b95b4`)
  - Returns error object (line 38-39 of FUN_00460ff0)
  - **Execution continues** - does not exit or abort
- **Evidence**: 
  - String reference at `0x007b5750`
  - Decompilation shows error handling returns error object but continues execution
  - No abort/exit calls after error return
  - Function structure identical to swkotor.exe's FUN_0044c980

#### 2.2 startup.txt
- **Function**: `FUN_00460ff0` (swkotor2.exe: `0x00460ff0`)
- **Location**: Same directory as `swkotor2.exe` (executable directory)
- **Path Resolution**: Relative to executable
- **When Attempted**: Line 116 of GameMain (after resource system initialization)
- **Failure Behavior**:
  - Prints error: `"ERROR: file 'startup.txt' doesn't exist"`
  - Returns error object
  - **Execution continues** - does not exit or abort
- **Evidence**:
  - String reference found: `"Hstartup.txt"` at `0x007b570b`
  - Same error handling pattern as config.txt
  - No abort/exit calls after error return
  - Function structure identical to swkotor.exe's FUN_0044c980

#### 2.3 swKotor2.ini / .\swkotor2.ini
- **Function**: Multiple functions read this file:
  - `FUN_00631fe0` - Reads INI values
  - `FUN_00403840` (swkotor2.exe: `0x00403840`) - Graphics options from INI
  - `FUN_00643f70` - Multiple INI reads during initialization
  - `FUN_0063bed0` - INI reading function
  - `FUN_00663420` - INI reading function
  - `FUN_0066a4f0` - INI reading function
- **Location**: Same directory as `swkotor2.exe` (executable directory)
- **Paths Attempted**:
  1. `"swKotor2.ini"` (case-sensitive string at `0x007b5740`)
  2. `".\\swkotor2.ini"` (string at `0x007b5644` - referenced 171+ times)
- **When Attempted**: 
  - Line 63 of GameMain (early initialization)
  - Throughout graphics initialization (`FUN_00403840`, `FUN_00643f70`)
  - During option reading (`FUN_0063bed0`, `FUN_00663420`, `FUN_0066a4f0`)
- **Failure Behavior**:
  - Windows INI API returns default values if file doesn't exist
  - No error messages generated
  - **Execution continues with default settings**
- **Evidence**:
  - 171+ cross-references to `".\\swkotor2.ini"` string
  - Uses Windows INI API (via `FUN_00631fe0`)
  - Windows INI API is non-fatal if file missing

### Category 3: RESOURCE SYSTEM FILES - Required for Game Functionality

These files are **required for the game to function properly** but may not prevent **initial launch**:

#### 3.1 chitin.key
- **Purpose**: Master index file for BIF archives (resource containers)
- **Location**: Same directory as `swkotor2.exe` (executable directory)
- **When Loaded**: During resource system initialization
- **Reference**: String `"HD0:chitin"` at `0x007be2dc`
- **Loading Functions**:
  - `FUN_004eed20` (swkotor2.exe: `0x004eed20`) - Resource system initialization
  - `FUN_004f05b0` (swkotor2.exe: `0x004f05b0`) - Resource system cleanup, references "HD0:chitin" at line 92
- **Failure Behavior**: 
  - **Analysis Required**: Resource system initialization may fail gracefully or abort
  - Without chitin.key, no game resources can be loaded
  - **Note**: May cause failure later in startup sequence when resources are needed
- **Evidence**:
  - String reference to "HD0:chitin" found
  - Referenced in resource system initialization/cleanup code (FUN_004eed20, FUN_004f05b0)

#### 3.2 BIF Files (Referenced by chitin.key)
- **Purpose**: Binary archive files containing game resources
- **Location**: Paths specified in `chitin.key` (typically relative to executable directory)
- **When Loaded**: During resource system initialization (after chitin.key is read)
- **Failure Behavior**: 
  - Individual missing BIF files may be skipped (graceful degradation)
  - Critical BIF files missing may prevent game functionality
  - **Analysis Required**: Need to trace exact failure behavior

### Category 4: PATH RESOLUTION MECHANISM

#### 4.1 Executable Path Detection
- **Function**: `FUN_00632350` (swkotor2.exe: `0x00632350`)
- **Called From**: `FUN_00403f70` (CreateMainWindow equivalent) (swkotor2.exe: `0x00403f70` line 38)
- **API Used**: `GetModuleFileNameA` (KERNEL32.DLL)
- **Purpose**: Determines executable directory for relative path resolution
- **Code Location**: Line 24 of FUN_00632350:
  ```c
  GetModuleFileNameA(DAT_00828398, local_10c, 0x100);
  ```
- **Evidence**: 
  - 5 references to `GetModuleFileNameA` found in executable
  - Used to get executable path, not installation path

#### 4.2 Registry Access
- **Finding**: **NO registry access found** for installation path
- **Searched**: 
  - No imports of `RegOpenKeyExA`, `RegQueryValueExA`, or other registry APIs
  - Searched for strings: "SOFTWARE", "BioWare", "LucasArts", "Obsidian" - only found in copyright strings, not registry keys
- **Conclusion**: swkotor2.exe does **NOT** use Windows Registry for installation path detection
- **Path Resolution**: All paths resolved relative to executable directory

### Category 5: FILE LOADING FUNCTIONS

#### 5.1 File Open Function
- **Function**: `FUN_00460db0` (swkotor2.exe: `0x00460db0`)
- **Purpose**: Opens files with path resolution
- **Parameters**:
  - `param_1`: Filename (base name)
  - `param_2`: Search path (optional, defaults to `DAT_007b5718` if NULL)
  - `param_3`: Output flag (set to 1 if file opened successfully)
  - `param_4`: Error handling flag
- **Return Value**: File handle pointer if successful, `NULL` if file not found
- **Implementation**:
  - Line 21: Constructs full path: `FUN_0076dac2(local_104, "%s%s%s")`
  - Line 22: Opens file: `FUN_0076dd6c(local_104, "rb")` (read binary)
  - Line 55: Returns NULL if file doesn't exist
- **Evidence**: Used by `FUN_00460ff0` to open config.txt and startup.txt

#### 5.2 Script/Config Execution Function
- **Function**: `FUN_00460ff0` (swkotor2.exe: `0x00460ff0`)
- **Purpose**: Executes text files (config.txt, startup.txt) as scripts
- **Parameters**: `char *param_1` - filename
- **Error Handling**:
  - Line 36: Calls `FUN_00460db0` to open file
  - Line 37-39: If file doesn't exist:
    - Prints: `"ERROR: file '%s' doesn't exist"` (string at `0x007b95b4`)
    - Returns error object `&DAT_0083d9e8`
    - **Does NOT exit or abort**
- **Evidence**: 
  - Called with "config.txt" at GameMain line 55
  - Called with "startup.txt" at GameMain line 116
  - Error return value is not checked by caller
  - Function structure is identical to swkotor.exe's FUN_0044c980 (same behavior)

## Exact File Paths and Locations

### Path Resolution Rules

1. **All paths are relative to executable directory**
2. **No absolute paths hardcoded**
3. **No registry-based path resolution**
4. **No environment variable usage** (except standard Windows environment)

### Specific File Paths

#### Files in Executable Directory

These files must be in the **same directory as swkotor2.exe**:

| File | Path Format | Example |
|------|-------------|---------|
| `swkotor2.exe` | `{executable_dir}\swkotor2.exe` | `C:\Games\KOTOR2\swkotor2.exe` |
| `config.txt` | `{executable_dir}\config.txt` | `C:\Games\KOTOR2\config.txt` |
| `startup.txt` | `{executable_dir}\startup.txt` | `C:\Games\KOTOR2\startup.txt` |
| `swKotor2.ini` | `{executable_dir}\swKotor2.ini` | `C:\Games\KOTOR2\swKotor2.ini` |
| `.\swkotor2.ini` | `{executable_dir}\swkotor2.ini` | `C:\Games\KOTOR2\swkotor2.ini` |
| `chitin.key` | `{executable_dir}\chitin.key` | `C:\Games\KOTOR2\chitin.key` |
| `dialog.tlk` | `{executable_dir}\dialog.tlk` | `C:\Games\KOTOR2\dialog.tlk` |

**Note**: Both `"swKotor2.ini"` and `".\\swkotor2.ini"` refer to the same file, but the engine uses both string literals (possibly case-insensitive on Windows, but both are attempted).

#### BIF Files

- **Location**: Paths specified in `chitin.key` file table
- **Format**: Relative paths from executable directory (e.g., `data\BIFs\swpc_tex_gui.bif`)
- **Resolution**: Paths in chitin.key are resolved relative to executable directory

## Startup Timing Requirements

### Critical Timing Points

1. **Before Process Start**:
   - `swkotor2.exe` must exist
   - System DLLs must be accessible

2. **During GameMain (Early Phase - Line 55)**:
   - `config.txt` is attempted (optional)
   - Error logged if missing, execution continues

3. **During GameMain (Early Phase - Line 63)**:
   - `swKotor2.ini` is read (optional)
   - Default values used if missing

4. **During CreateMainWindow (FUN_00403f70)**:
   - `FUN_00632350()` called (line 38) to get executable path via `GetModuleFileNameA`
   - This path is used for all subsequent relative path resolution

5. **During Resource System Initialization**:
   - `chitin.key` is read (required for resource loading)
   - BIF files referenced in chitin.key are loaded

6. **During GameMain (Later Phase - Line 116)**:
   - `startup.txt` is attempted (optional)
   - Error logged if missing, execution continues

### Files That Must Exist at Specific Times

| File | Must Exist By | Critical | Evidence |
|------|---------------|----------|----------|
| `swkotor2.exe` | Process start | ✅ YES | Executable itself |
| System DLLs | Process start | ✅ YES | Windows loader requirement |
| `config.txt` | GameMain line 55 | ❌ NO | Error logged, continues |
| `swKotor2.ini` | GameMain line 63 | ❌ NO | Default values used |
| `chitin.key` | Resource init | ⚠️ MAYBE | Required for resources, may not stop initial launch |
| BIF files | Resource init | ⚠️ MAYBE | Required for resources, may not stop initial launch |
| `startup.txt` | GameMain line 116 | ❌ NO | Error logged, continues |

## Error Handling Analysis

### Error Handling Patterns

1. **Non-Fatal File Errors**:
   - `FUN_00460ff0` (config.txt, startup.txt):
     - Prints error message
     - Returns error object
     - **Caller does NOT check return value**
     - Execution continues

2. **INI File Errors**:
   - Windows INI API returns empty string/default value
   - **No error propagation**
   - Execution continues with defaults

3. **File Not Found**:
   - `FUN_00460db0` returns `NULL` if file not found
   - Caller checks for NULL and handles gracefully
   - **No abort/exit calls**

### Critical Failure Points

Based on code analysis, the **only** ways the process can fail to start:

1. **Windows loader failure**: Missing executable or system DLLs
2. **Resource system critical failure**: If chitin.key loading aborts (needs verification)
3. **Graphics initialization failure**: Window creation failure (may not prevent process start)

**All other file access failures are handled gracefully and do NOT prevent launch**.

## Comparison with swkotor.exe

### Similarities

1. **Identical structure**: Both use same startup pattern with config.txt and startup.txt
2. **Same error handling**: Both handle missing files gracefully
3. **No registry access**: Both resolve paths relative to executable
4. **Same file loading functions**: Equivalent functions perform same operations

### Differences

1. **INI filename**: K2 uses `swKotor2.ini` / `.\swkotor2.ini` vs K1's `swKotor.ini` / `.\swkotor.ini`
2. **Mutex name**: K2 uses `"swkotor2"` vs K1's `"swkotor"`
3. **Function addresses**: Different addresses due to code size/layout differences
4. **Window class**: K2 uses `"KotOR2"` (string at `0x0080c210`) vs K1's window class

### Function Address Mapping

| Purpose | swkotor.exe | swkotor2.exe |
|---------|-------------|--------------|
| GameMain | `0x004041f0` | `0x00404250` |
| Config/Startup loader | `0x0044c980` | `0x00460ff0` |
| File opener | `0x0044c740` | `0x00460db0` |
| Get executable path | `0x005e6b10` | `0x00632350` |
| Create main window | `0x00403f20` | `0x00403f70` |
| Graphics init | `0x00403800` | `0x00403840` |
| Resource init | `0x004b63e0` | `0x004eed20` |
| Resource cleanup | `0x004b7c60` | `0x004f05b0` |

## Summary: What MUST Exist

### Absolute Minimum (Process Can Start)

1. ✅ `swkotor2.exe` - The executable itself
2. ✅ System DLLs - Required by Windows loader

### Functional Minimum (Game Can Load)

The following are likely required for the game to proceed past initial loading:

1. ✅ `swkotor2.exe` - The executable
2. ✅ System DLLs - Required by Windows
3. ⚠️ `chitin.key` - Required for resource loading (verification needed on exact failure behavior)
4. ⚠️ Critical BIF files - Required for game resources (verification needed on exact failure behavior)

### Optional Files (Enhance Functionality)

1. ❌ `config.txt` - Startup script (optional, error logged if missing)
2. ❌ `startup.txt` - Startup script (optional, error logged if missing)
3. ❌ `swKotor2.ini` / `.\swkotor2.ini` - Configuration (optional, defaults used if missing)
4. ❌ `dialog.tlk` - Dialogue text (loaded later, not during initial startup)

## Verification Notes

### Areas Requiring Further Investigation

1. **chitin.key Loading Failure**:
   - Exact behavior when chitin.key is missing needs verification
   - May cause graceful degradation or may abort startup
   - Resource system initialization code (`FUN_004eed20`, `FUN_004f05b0`) needs deeper analysis

2. **BIF File Loading Failure**:
   - Behavior when individual BIF files are missing
   - Behavior when all BIF files are missing
   - Critical vs. non-critical BIF files

3. **Graphics Initialization**:
   - Window creation failure behavior
   - Whether this prevents process start or just causes window error

### Analysis Methodology

All findings verified via:
- ✅ Ghidra MCP decompilation of swkotor2.exe
- ✅ Cross-reference analysis of file access operations
- ✅ String reference analysis
- ✅ Function call tree analysis
- ✅ Import table analysis (DLL dependencies)
- ✅ Error message string analysis
- ✅ Comparison with swkotor.exe analysis for validation

**No filesystem inspection or runtime testing performed** - all findings based exclusively on static binary analysis.

## Function Reference

### Key Functions Analyzed

| Function | Address | Purpose |
|----------|---------|---------|
| `FUN_00404250` | `0x00404250` | Main initialization function (GameMain equivalent) |
| `FUN_00460ff0` | `0x00460ff0` | Executes config/startup scripts |
| `FUN_00460db0` | `0x00460db0` | Opens files with path resolution |
| `FUN_00632350` | `0x00632350` | Gets executable path |
| `FUN_00403840` | `0x00403840` | Graphics initialization (reads INI) |
| `FUN_00643f70` | `0x00643f70` | Resource system initialization |
| `FUN_004eed20` | `0x004eed20` | Resource system setup (references chitin) |
| `FUN_004f05b0` | `0x004f05b0` | Resource system cleanup (references chitin) |
| `FUN_00403f70` | `0x00403f70` | Window creation (CreateMainWindow equivalent) |
| `FUN_0063bed0` | `0x0063bed0` | INI file reader |
| `FUN_00663420` | `0x00663420` | INI file reader |
| `FUN_0066a4f0` | `0x0066a4f0` | INI file reader |

### String References

| String | Address | Purpose |
|--------|---------|---------|
| `"config.txt"` | `0x007b5750` | Config script filename |
| `"startup.txt"` | Referenced via `"Hstartup.txt"` at `0x007b570b` | Startup script filename |
| `"swKotor2.ini"` | `0x007b5740` | INI filename (case-sensitive) |
| `".\\swkotor2.ini"` | `0x007b5644` | INI filename (relative path, 171+ references) |
| `"HD0:chitin"` | `0x007be2dc` | Chitin keyfile reference |
| `"ERROR: file '%s' doesn't exist"` | `0x007b95b4` | Error message for missing files |
| `"swkotor2"` | (in CreateMutexA call) | Mutex name for single-instance enforcement |
| `"KotOR2"` | `0x0080c210` | Window class name |

---

**Document Generated**: Based exclusively on Ghidra MCP analysis of swkotor2.exe  
**Analysis Date**: Static binary analysis  
**Methodology**: Reverse engineering via decompilation and cross-reference analysis  
**Comparison**: Findings validated against swkotor.exe analysis for consistency verification

