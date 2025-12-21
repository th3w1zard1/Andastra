# swkotor.exe Startup File Requirements - Reverse Engineering Analysis

**⚠️ IMPORTANT**: This document contains **exclusively** documentation from reverse engineering analysis of `swkotor.exe` using Ghidra MCP. All findings are based on analysis of the original game executable, not third-party engine rewrites or custom implementations.

## Executive Summary

This document exhaustively documents **exactly** what files must exist and where they must be located for `swkotor.exe` to successfully launch. The analysis was performed by tracing the startup sequence from the entry point through `GameMain` and all file I/O operations.

**Key Findings:**

1. **Minimal File Requirements**: Only the executable itself and system DLLs are **strictly required** for the process to start
2. **Graceful Degradation**: Most file accesses are optional - missing files generate errors but do **NOT** prevent the game from launching
3. **Path Resolution**: All paths are resolved relative to the executable directory - **NO registry access** is used for installation path detection
4. **Configuration Files**: Multiple configuration files are attempted but missing files do not prevent launch

## Startup Sequence Analysis

### Entry Point Flow

The startup sequence follows this exact path (verified via Ghidra decompilation):

1. **Windows Loader** → Loads `swkotor.exe` and system DLLs
2. **Entry Function** → Calls `GameMain` (swkotor.exe: `0x006fb509`)
3. **GameMain** (swkotor.exe: `0x004041f0`) → Main initialization function
4. **File Loading Operations** → Various files attempted during initialization

### GameMain Function Analysis

**Function**: `GameMain` (swkotor.exe: `0x004041f0`)
**Signature**: `UINT GameMain(HINSTANCE hInstance)`

**File Loading Sequence** (in order):

1. **Line 58**: `FUN_0044c980("config.txt")` - Attempts to load config.txt
2. **Line 66**: `FUN_005e5a90(&iStack_84, "swKotor.ini")` - Reads swKotor.ini
3. **Line 84-85**: Reads `".\\swkotor.ini"` for sound settings
4. **Line 116**: `FUN_0044c980("startup.txt")` - Attempts to load startup.txt

## File Requirements by Category

### Category 1: CRITICAL - Required for Process Execution

These files **MUST** exist for the executable to even start:

#### 1.1 swkotor.exe
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
- **Function**: `FUN_0044c980` (swkotor.exe: `0x0044c980`)
- **Location**: Same directory as `swkotor.exe` (executable directory)
- **Path Resolution**: Relative to executable (determined via `GetModuleFileNameA`)
- **When Attempted**: Line 58 of GameMain
- **Failure Behavior**: 
  - Prints error: `"ERROR: file 'config.txt' doesn't exist"` (swkotor.exe: `0x007414a0`)
  - Returns error object (line 39 of FUN_0044c980)
  - **Execution continues** - does not exit or abort
- **Evidence**: 
  - String reference at `0x0073d750`
  - Decompilation shows error handling returns error object but continues execution
  - No abort/exit calls after error return

#### 2.2 startup.txt
- **Function**: `FUN_0044c980` (swkotor.exe: `0x0044c980`)
- **Location**: Same directory as `swkotor.exe` (executable directory)
- **Path Resolution**: Relative to executable
- **When Attempted**: Line 116 of GameMain (after resource system initialization)
- **Failure Behavior**:
  - Prints error: `"ERROR: file 'startup.txt' doesn't exist"`
  - Returns error object
  - **Execution continues** - does not exit or abort
- **Evidence**:
  - String reference found: `"Hstartup.txt"` at `0x0073d70f`
  - Same error handling pattern as config.txt
  - No abort/exit calls after error return

#### 2.3 swKotor.ini / .\swkotor.ini
- **Function**: Multiple functions read this file:
  - `FUN_0061b780` (swkotor.exe: `0x0061b780`) - Main INI reader
  - `FUN_005e67c0` - Reads INI values
  - `FUN_00403800` (swkotor.exe: `0x00403800`) - Graphics options from INI
  - `FUN_005f8550` (swkotor.exe: `0x005f8550`) - Multiple INI reads during initialization
- **Location**: Same directory as `swkotor.exe` (executable directory)
- **Paths Attempted**:
  1. `"swKotor.ini"` (case-sensitive string at `0x0073d744`)
  2. `".\\swkotor.ini"` (string at `0x0073d648` - referenced 144+ times)
- **When Attempted**: 
  - Line 66 of GameMain (early initialization)
  - Throughout graphics initialization (`FUN_00403800`, `FUN_005f8550`)
  - During option reading (`FUN_0061b780`)
- **Failure Behavior**:
  - Windows INI API returns default values if file doesn't exist
  - No error messages generated
  - **Execution continues with default settings**
- **Evidence**:
  - 144+ cross-references to `".\\swkotor.ini"` string
  - Uses Windows `GetPrivateProfileStringA` API (via `FUN_005e67c0`)
  - Windows INI API is non-fatal if file missing

### Category 3: RESOURCE SYSTEM FILES - Required for Game Functionality

These files are **required for the game to function properly** but may not prevent **initial launch**:

#### 3.1 chitin.key
- **Purpose**: Master index file for BIF archives (resource containers)
- **Location**: Same directory as `swkotor.exe` (executable directory)
- **When Loaded**: During resource system initialization
- **Reference**: String `"HD0:chitin"` at `0x00745524`
- **Loading Functions**:
  - `FUN_004b63e0` (swkotor.exe: `0x004b63e0`) - References "HD0:chitin"
  - `FUN_004b7c60` (swkotor.exe: `0x004b7c60`) - Cleans up resource system, references "HD0:chitin" at line 92
- **Failure Behavior**: 
  - **Analysis Required**: Resource system initialization may fail gracefully or abort
  - Without chitin.key, no game resources can be loaded
  - **Note**: May cause failure later in startup sequence when resources are needed
- **Evidence**:
  - String reference to "HD0:chitin" found
  - Referenced in resource system initialization/cleanup code

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
- **Function**: `FUN_005e6b10` (swkotor.exe: `0x005e6b10`)
- **Called From**: `CreateMainWindow` (swkotor.exe: `0x00403f20` line 39)
- **API Used**: `GetModuleFileNameA` (KERNEL32.DLL)
- **Purpose**: Determines executable directory for relative path resolution
- **Code Location**: Line 24 of FUN_005e6b10:
  ```c
  GetModuleFileNameA(DAT_007a39c0, local_10c, 0x100);
  ```
- **Evidence**: 
  - 5 references to `GetModuleFileNameA` found in executable
  - Used to get executable path, not installation path

#### 4.2 Registry Access
- **Finding**: **NO registry access found** for installation path
- **Searched**: 
  - No imports of `RegOpenKeyExA`, `RegQueryValueExA`, or other registry APIs
  - Searched for strings: "SOFTWARE", "BioWare", "LucasArts" - only found in copyright strings, not registry keys
- **Conclusion**: swkotor.exe does **NOT** use Windows Registry for installation path detection
- **Path Resolution**: All paths resolved relative to executable directory

### Category 5: FILE LOADING FUNCTIONS

#### 5.1 File Open Function
- **Function**: `FUN_0044c740` (swkotor.exe: `0x0044c740`)
- **Purpose**: Opens files with path resolution
- **Parameters**:
  - `param_1`: Filename (base name)
  - `param_2`: Search path (optional, defaults to `DAT_0073d71c` if NULL)
  - `param_3`: Output flag (set to 1 if file opened successfully)
  - `param_4`: Error handling flag
- **Return Value**: File handle pointer if successful, `NULL` if file not found
- **Implementation**:
  - Line 21: Constructs full path: `FUN_006fadb0(local_104, "%s%s%s")`
  - Line 22: Opens file: `FUN_006fad9d(local_104, "rb")` (read binary)
  - Line 37: Returns NULL if file doesn't exist
- **Evidence**: Used by `FUN_0044c980` to open config.txt and startup.txt

#### 5.2 Script/Config Execution Function
- **Function**: `FUN_0044c980` (swkotor.exe: `0x0044c980`)
- **Purpose**: Executes text files (config.txt, startup.txt) as scripts
- **Parameters**: `char *param_1` - filename
- **Error Handling**:
  - Line 36: Calls `FUN_0044c740` to open file
  - Line 37-39: If file doesn't exist:
    - Prints: `"ERROR: file '%s' doesn't exist"` (string at `0x007414a0`)
    - Returns error object `&DAT_007b8fe8`
    - **Does NOT exit or abort**
- **Evidence**: 
  - Called with "config.txt" at GameMain line 58
  - Called with "startup.txt" at GameMain line 116
  - Error return value is not checked by caller

## Exact File Paths and Locations

### Path Resolution Rules

1. **All paths are relative to executable directory**
2. **No absolute paths hardcoded**
3. **No registry-based path resolution**
4. **No environment variable usage** (except standard Windows environment)

### Specific File Paths

#### Files in Executable Directory

These files must be in the **same directory as swkotor.exe**:

| File | Path Format | Example |
|------|-------------|---------|
| `swkotor.exe` | `{executable_dir}\swkotor.exe` | `C:\Games\KOTOR\swkotor.exe` |
| `config.txt` | `{executable_dir}\config.txt` | `C:\Games\KOTOR\config.txt` |
| `startup.txt` | `{executable_dir}\startup.txt` | `C:\Games\KOTOR\startup.txt` |
| `swKotor.ini` | `{executable_dir}\swKotor.ini` | `C:\Games\KOTOR\swKotor.ini` |
| `.\swkotor.ini` | `{executable_dir}\swkotor.ini` | `C:\Games\KOTOR\swkotor.ini` |
| `chitin.key` | `{executable_dir}\chitin.key` | `C:\Games\KOTOR\chitin.key` |
| `dialog.tlk` | `{executable_dir}\dialog.tlk` | `C:\Games\KOTOR\dialog.tlk` |

**Note**: Both `"swKotor.ini"` and `".\\swkotor.ini"` refer to the same file, but the engine uses both string literals (possibly case-insensitive on Windows, but both are attempted).

#### BIF Files

- **Location**: Paths specified in `chitin.key` file table
- **Format**: Relative paths from executable directory (e.g., `data\BIFs\swpc_tex_gui.bif`)
- **Resolution**: Paths in chitin.key are resolved relative to executable directory

## Startup Timing Requirements

### Critical Timing Points

1. **Before Process Start**:
   - `swkotor.exe` must exist
   - System DLLs must be accessible

2. **During GameMain (Early Phase - Line 58)**:
   - `config.txt` is attempted (optional)
   - Error logged if missing, execution continues

3. **During GameMain (Early Phase - Line 66)**:
   - `swKotor.ini` is read (optional)
   - Default values used if missing

4. **During CreateMainWindow**:
   - `GetModuleFileNameA` called to determine executable path
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
| `swkotor.exe` | Process start | ✅ YES | Executable itself |
| System DLLs | Process start | ✅ YES | Windows loader requirement |
| `config.txt` | GameMain line 58 | ❌ NO | Error logged, continues |
| `swKotor.ini` | GameMain line 66 | ❌ NO | Default values used |
| `chitin.key` | Resource init | ⚠️ MAYBE | Required for resources, may not stop initial launch |
| BIF files | Resource init | ⚠️ MAYBE | Required for resources, may not stop initial launch |
| `startup.txt` | GameMain line 116 | ❌ NO | Error logged, continues |

## Error Handling Analysis

### Error Handling Patterns

1. **Non-Fatal File Errors**:
   - `FUN_0044c980` (config.txt, startup.txt):
     - Prints error message
     - Returns error object
     - **Caller does NOT check return value**
     - Execution continues

2. **INI File Errors**:
   - Windows INI API (`GetPrivateProfileStringA`) returns empty string/default value
   - **No error propagation**
   - Execution continues with defaults

3. **File Not Found**:
   - `FUN_0044c740` returns `NULL` if file not found
   - Caller checks for NULL and handles gracefully
   - **No abort/exit calls**

### Critical Failure Points

Based on code analysis, the **only** ways the process can fail to start:

1. **Windows loader failure**: Missing executable or system DLLs
2. **Resource system critical failure**: If chitin.key loading aborts (needs verification)
3. **Graphics initialization failure**: Window creation failure (may not prevent process start)

**All other file access failures are handled gracefully and do NOT prevent launch**.

## Summary: What MUST Exist

### Absolute Minimum (Process Can Start)

1. ✅ `swkotor.exe` - The executable itself
2. ✅ System DLLs - Required by Windows loader

### Functional Minimum (Game Can Load)

The following are likely required for the game to proceed past initial loading:

1. ✅ `swkotor.exe` - The executable
2. ✅ System DLLs - Required by Windows
3. ⚠️ `chitin.key` - Required for resource loading (verification needed on exact failure behavior)
4. ⚠️ Critical BIF files - Required for game resources (verification needed on exact failure behavior)

### Optional Files (Enhance Functionality)

1. ❌ `config.txt` - Startup script (optional, error logged if missing)
2. ❌ `startup.txt` - Startup script (optional, error logged if missing)
3. ❌ `swKotor.ini` / `.\swkotor.ini` - Configuration (optional, defaults used if missing)
4. ❌ `dialog.tlk` - Dialogue text (loaded later, not during initial startup)

## Verification Notes

### Areas Requiring Further Investigation

1. **chitin.key Loading Failure**:
   - Exact behavior when chitin.key is missing needs verification
   - May cause graceful degradation or may abort startup
   - Resource system initialization code (`FUN_004b63e0`, `FUN_004b7c60`) needs deeper analysis

2. **BIF File Loading Failure**:
   - Behavior when individual BIF files are missing
   - Behavior when all BIF files are missing
   - Critical vs. non-critical BIF files

3. **Graphics Initialization**:
   - Window creation failure behavior
   - Whether this prevents process start or just causes window error

### Analysis Methodology

All findings verified via:
- ✅ Ghidra MCP decompilation of swkotor.exe
- ✅ Cross-reference analysis of file access operations
- ✅ String reference analysis
- ✅ Function call tree analysis
- ✅ Import table analysis (DLL dependencies)
- ✅ Error message string analysis

**No filesystem inspection or runtime testing performed** - all findings based exclusively on static binary analysis.

## Function Reference

### Key Functions Analyzed

| Function | Address | Purpose |
|----------|---------|---------|
| `GameMain` | `0x004041f0` | Main initialization function |
| `FUN_0044c980` | `0x0044c980` | Executes config/startup scripts |
| `FUN_0044c740` | `0x0044c740` | Opens files with path resolution |
| `FUN_005e6b10` | `0x005e6b10` | Gets executable path |
| `FUN_00403800` | `0x00403800` | Graphics initialization (reads INI) |
| `FUN_005f8550` | `0x005f8550` | Resource system initialization |
| `FUN_004b63e0` | `0x004b63e0` | Resource system setup (references chitin) |
| `FUN_004b7c60` | `0x004b7c60` | Resource system cleanup (references chitin) |
| `CreateMainWindow` | `0x00403f20` | Window creation |
| `FUN_0061b780` | `0x0061b780` | INI file reader |

### String References

| String | Address | Purpose |
|--------|---------|---------|
| `"config.txt"` | `0x0073d750` | Config script filename |
| `"startup.txt"` | Referenced via `"Hstartup.txt"` at `0x0073d70f` | Startup script filename |
| `"swKotor.ini"` | `0x0073d744` | INI filename (case-sensitive) |
| `".\\swkotor.ini"` | `0x0073d648` | INI filename (relative path) |
| `"HD0:chitin"` | `0x00745524` | Chitin keyfile reference |
| `"ERROR: file '%s' doesn't exist"` | `0x007414a0` | Error message for missing files |

---

**Document Generated**: Based exclusively on Ghidra MCP analysis of swkotor.exe  
**Analysis Date**: Static binary analysis  
**Methodology**: Reverse engineering via decompilation and cross-reference analysis

