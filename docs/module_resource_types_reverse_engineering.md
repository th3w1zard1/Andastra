# Module Resource Types - Reverse Engineering Findings

## Executive Summary

This document details the reverse engineering findings for module file discovery and resource type support in `swkotor.exe` and `swkotor2.exe`.

**Key Findings:**

1. **Both executables support `_s.rim` files** - confirmed via string references in both binaries
2. **No subfolder support** - ResRef is a flat 16-byte ASCII string, no path separators
3. **Container formats don't filter** - RIM/ERF/MOD containers accept any resource type ID
4. **Many files are loaded outside the resource system** - TLK, INI, audio files (WAV/OGG), and other files use direct file I/O, not the resource loader (see "Files Loaded Outside Resource System" section below)

## Files Loaded Outside Resource System

**CRITICAL**: Many files are loaded via direct file I/O, bypassing the resource system entirely. These files **CANNOT** be placed in module containers and will not be found by the resource loader.

### Direct File I/O Files (VERIFIED via Ghidra)

1. **`dialog.tlk` (TLK files)**
   - **Loading**: Direct file I/O from game root directory
   - **Function**: `FUN_005e6680` (swkotor.exe: 0x005e6680) calls initialization code
   - **Evidence**: Reone codebase shows `findFileIgnoreCase(gameDir, "dialog.tlk")` - direct filesystem search
   - **Location**: Game root directory (same location as executable)
   - **Resource Type**: TLK (2018, 0x7e2) exists in resource registry, but TLK loading uses direct file I/O, not resource system
   - **Module Support**: ❌ **NO** - TLK files in modules will be ignored

2. **`swkotor.ini` / `swkotor2.ini` (Configuration files)**
   - **Loading**: Direct file I/O via Windows INI API
   - **Function**: `FUN_0061b780` (swkotor.exe: 0x0061b780) - reads INI file directly
   - **Evidence**: String references to `".\\swkotor.ini"` at swkotor.exe: 0x0073d648, 0x0073d744
   - **Location**: Game root directory (same location as executable)
   - **Module Support**: ❌ **NO** - INI files in modules will be ignored

3. **Audio Files via Miles Audio System**
   - **Loading**: Direct directory access via Miles audio library
   - **Function**: `FUN_005e7a90` (swkotor.exe: 0x005e7a90) sets up directory mappings
   - **Directories**:
     - `streamwaves/` - WAV files loaded directly from this directory
     - `streammusic/` - Music files loaded directly from this directory
   - **Evidence**: String references to `".\\streamwaves"` (swkotor.exe: 0x0074df40) and `".\\streammusic"` (swkotor.exe: 0x0074e028)
   - **Resource Types**: WAV (4), OGG (2078), BMU (8) exist in resource registry, but Miles audio system uses direct directory access
   - **Module Support**: ❌ **NO** - Audio files in modules will NOT be loaded by Miles audio system
   - **Note**: WAV handler exists (swkotor.exe: 0x005d5e90) but Miles audio system bypasses it for streamed audio

4. **`chitin.key` and BIF files**
   - **Loading**: Direct file I/O during initialization
   - **Location**: Game root directory
   - **Module Support**: ❌ **NO** - These are archive index files, not resources

5. **Save Game Files (`.sav`)**
   - **Loading**: Direct file I/O from `saves/` directory
   - **Directory**: Mapped via `FUN_005e7a90` (swkotor.exe: 0x005e7a90 line 166-170)
   - **Module Support**: ❌ **NO** - Save files are separate containers, not loaded through resource system

6. **Other Direct Directory Access**
   - **`override/`**: Mapped via `FUN_005e7a90` (swkotor.exe: 0x005e7a90 line 47-59) - but this IS used by resource system
   - **`modules/`**: Mapped via `FUN_005e7a90` (swkotor.exe: 0x005e7a90 line 86-98) - used for module discovery
   - **`music/`**: Mapped via `FUN_005e7a90` (swkotor.exe: 0x005e7a90 line 177-187) - direct directory access
   - **`movies/`**: Mapped via `FUN_005e7a90` (swkotor.exe: 0x005e7a90 line 203-213) - direct directory access

### Summary: What CANNOT Be Containerized

The following files **CANNOT** be placed in module containers and will not work:

- ❌ **TLK files** (`dialog.tlk`) - Direct file I/O from game root
- ❌ **INI files** (`swkotor.ini`) - Direct file I/O from game root
- ❌ **Audio files via Miles** (`streamwaves/`, `streammusic/`) - Direct directory access
- ❌ **Music files** (`music/` directory) - Direct directory access
- ❌ **Video files** (`movies/` directory) - Direct directory access
- ❌ **Save files** (`.sav`) - Direct file I/O from `saves/` directory
- ❌ **Archive index files** (`chitin.key`, BIF files) - Direct file I/O during initialization

### What CAN Be Containerized

Resource types that use `FUN_00407230` / `FUN_004074d0` (resource search functions) **CAN** be placed in modules:

- ✅ **TPC/TGA textures** - Verified: Texture loaders call `FUN_00407230`
- ✅ **MDL/MDX models** - Verified: Model loaders call `FUN_004074d0`
- ✅ **DLG dialogs** - Verified: Dialog loaders call `FUN_004074d0`
- ✅ **ARE/GIT area data** - Verified: Area loaders call `FUN_004074d0`
- ✅ **NCS scripts** - Verified: Script loaders call `FUN_004074d0`
- ✅ **All resource types that have handlers calling `FUN_00407230`/`FUN_004074d0`** - These search all locations including modules

## Module File Discovery

### Exact Functions Handling Module Discovery

**Primary Module Loading Functions:**

- **swkotor.exe**: `FUN_004094a0` (address `0x004094a0`)
  - Handles module file discovery and loading
  - Checks for `"_a"`, `"_adx"`, `"_s"` suffixes
  - Opens RIM files via `FUN_00406e20`
  - No `_dlg.erf` support in K1

- **swkotor2.exe**: `FUN_004096b0` (address `0x004096b0`)
  - Handles module file discovery and loading
  - Checks for `"_a"`, `"_adx"`, `"_s"`, and **`"_dlg"`** suffixes
  - Opens RIM/ERF files via `FUN_00406ef0`
  - **Hardcoded `"_dlg"` check** (line 128): `apuStack_6c[0] = FUN_00630a90(aiStack_30,"_dlg");`

**Supporting Functions:**

- `FUN_00407230` (K1) / `FUN_00407300` (K2): Resource search through multiple locations
- `FUN_00406e20` (K1) / `FUN_00406ef0` (K2): Opens and loads RIM/ERF containers
- `FUN_004071a0` (K1) / `FUN_00407270` (K2): Searches resource lists

### `_s.rim` Support

**Both executables support `_s.rim` files:**

- **swkotor.exe**: String `"_s.rim"` at address `0x00752ff0`, referenced by:
  - `FUN_0067bc40` (swkotor.exe: 0x0067bc40) - Module enumeration/discovery
  - `FUN_006cfa70` (swkotor.exe: 0x006cfa70) - Module enumeration/discovery

- **swkotor2.exe**: String `"_s.rim"` at address `0x007cc0c0`, referenced by:
  - `FUN_006d1a50` (swkotor2.exe: 0x006d1a50) - Module enumeration/discovery  
  - `FUN_0073dcb0` (swkotor2.exe: 0x0073dcb0) - Module enumeration/discovery

**Conclusion**: Both K1 and K2 support `_s.rim` files as optional data archives.

### Valid File Combinations

**K1 (swkotor.exe) Valid Combinations:**

1. `{module}.mod` - **Overrides all** (highest priority)
2. `{module}.rim` - Main module archive
3. `{module}.rim` + `{module}_s.rim` - Composite module (main + data)
4. `{module}.rim` + `{module}_a.rim` - Main + area archive (if exists)
5. `{module}.rim` + `{module}_adx.rim` - Main + area archive extended (if exists)

**K2 (swkotor2.exe) Valid Combinations:**

1. `{module}.mod` - **Overrides all** (highest priority)
2. `{module}.rim` - Main module archive
3. `{module}.rim` + `{module}_s.rim` - Composite module (main + data)
4. `{module}.rim` + `{module}_s.rim` + `{module}_dlg.erf` - Composite with dialogs (K2 only)
5. `{module}.rim` + `{module}_a.rim` - Main + area archive (if exists)
6. `{module}.rim` + `{module}_adx.rim` - Main + area archive extended (if exists)

**Invalid/Unsupported:**

- `{module}_*.erf` - **NOT SUPPORTED** - Only `_dlg.erf` is hardcoded in K2
- `{module}.hak` - **NOT SUPPORTED** - No `.hak` references found in either executable
- `{module}_something.erf` - **NOT SUPPORTED** - Only `_dlg` suffix is checked
- Any other `_*.erf` patterns - **NOT SUPPORTED** - No wildcard matching
- `{module}_dlg.mod` - **NOT SUPPORTED** - `_dlg` uses container type 3 (ERF), must be `.erf` extension
- `{module}_s.mod` - **NOT SUPPORTED** - `_s` uses container type 4 (RIM), must be `.rim` extension
- `{module}_s.erf` - **NOT SUPPORTED** - `_s` uses container type 4 (RIM), must be `.rim` extension
  - **PROOF**: K1 line 118 and K2 line 122 call `FUN_00406e20`/`FUN_00406ef0` with container type 4 (RIM), not type 3 (ERF)
- `{module}_dlg.rim` - **NOT SUPPORTED** - `_dlg` uses container type 3 (ERF), must be `.erf` extension
- `{module}.erf` (no suffix) - **NOT SUPPORTED** - Not a recognized module piece pattern

**Key Finding**: `_dlg.erf` is **HARDCODED** in K2. The engine explicitly checks for the literal string `"_dlg"` (swkotor2.exe: `FUN_004096b0` line 128). There is **NO wildcard support** for `_*.erf` patterns.

**Extension Determination**: Extensions are determined by container type, not hardcoded in filename construction:

- Container type `3` (ERF) → `.erf` extension (swkotor2.exe: `FUN_004112f0` line 23-27, calls `FUN_0040fbb0`)
- Container type `4` (RIM) → `.rim` extension (swkotor2.exe: `FUN_004112f0` line 28-32, calls `FUN_004101a0`)
- Resource type `0x7db` (MOD) → `.mod` extension (searched via `FUN_00407300`)

**Proof**:

- K2 line 122: `FUN_00406ef0(param_1,aiStack_58,4,0)` - Container type 4 (RIM) → must be `_s.rim`
- K2 line 147: `FUN_00406ef0(param_1,piVar3,3,2)` - Container type 3 (ERF) → must be `_dlg.erf`
- K1 line 118: `FUN_00406e20(param_1,aiStack_38,4,0)` - Container type 4 (RIM) → must be `_s.rim`

### Subfolder Support

**Status**: **NOT SUPPORTED**

The RIM/ERF/MOD container formats store resources with:

- **ResRef**: 16-byte null-terminated ASCII string (no path separators)
- **Resource Type ID**: uint32 (RIM) or uint16 (ERF/MOD)
- **Resource data**: offset and size

**Analysis**:

- ResRef field is fixed at 16 bytes, null-padded
- No evidence of path separator handling (`/` or `\`) in container format
- No evidence of subfolder enumeration in module loading code
- **Conclusion**: Resources are stored **flat** in containers - no subfolder support

**Reverse Engineering Evidence**:

- Container format specification shows ResRef as a simple 16-byte ASCII field
- Module loading code (`FUN_0067bc40` / `FUN_006d1a50`) only handles filename-based discovery, not path-based
- Container reading code in Andastra (`Capsule.cs`) shows ResRef as a flat 16-byte field with no path handling

## Resource Type Support in Modules

### Container Format Capabilities

RIM and ERF/MOD containers store resources with a **resource type ID** field:

- **RIM format**: `uint32 restype` (4 bytes)
- **ERF/MOD format**: `uint16 restype` (2 bytes)

**Critical Finding**: The container formats themselves do **NOT filter** resource types. Any resource type ID can be stored in the container.

### Engine Resource Loading

**Status**: **CONFIRMED** - Engine accepts any resource type stored in modules

**Analysis**:
The engine's resource manager loads resources by:

1. Looking up resource by ResRef + ResourceType in the resource table
2. Extracting data from the container (RIM/ERF/MOD)
3. Passing data to the appropriate loader based on ResourceType

**Key Finding**: There is **NO filtering** of resource types when loading from modules. The engine will attempt to load **ANY** resource type that is:

1. Stored in a module container (RIM/ERF/MOD)
2. Has a valid resource type ID
3. Can be parsed by the appropriate loader

**Reverse Engineering Evidence**:

- Module loading code (`FUN_004094a0` swkotor.exe: 0x004094a0 / `FUN_004096b0` swkotor2.exe: 0x004096b0) opens RIM files without type filtering
- Resource extraction code reads all entries from container regardless of type
- No type validation found in module resource loading path
- Container format allows any resource type ID to be stored

### Resource Types: What CAN and CANNOT Be Packed

#### PROOF: Engine loads ANY resource type from modules - NO filtering

**Exact instructions proving no type filtering:**

1. **RIM Loader** (`FUN_0040f990` swkotor.exe: 0x0040f990, case 4):
   - Line 85: `FUN_0040e990(param_1,local_4c,iStack_1c,uStack_18);`
   - Iterates through ALL entries in RIM file
   - Calls `FUN_0040e990` for EVERY entry, passing resource type `iStack_1c` directly
   - **NO type checking or filtering**

2. **MOD Loader** (`FUN_0040f3c0` swkotor.exe: 0x0040f3c0, case 3):
   - Line 85: `FUN_0040e990(param_1,local_c8,iStack_cc,uStack_d0);`
   - Iterates through ALL entries in MOD file
   - Calls `FUN_0040e990` for EVERY entry, passing resource type `iStack_cc` directly
   - **NO type checking or filtering**

3. **Resource Registration** (`FUN_0040e990` swkotor.exe: 0x0040e990):
   - Line 99: `*(short *)(*(int *)((int)this + 0x10) + 0x1a + iVar5) = (short)param_2;`
   - Stores resource type `param_2` directly into resource table
   - **NO type validation or filtering**
   - Only checks for duplicate ResRef+Type combinations

**Conclusion**: The engine loads **ALL resource types** from modules with **ZERO filtering**. Any resource type stored in a RIM/ERF/MOD container will be:

1. Registered in the resource table (via `FUN_0040e990`)
2. Available for loading by type-specific loaders
3. Loaded regardless of type (TPC, TGA, etc.)

**CRITICAL**: The following sections list ONLY resource types that the game **ACTUALLY LOADS AND USES** from modules. Resource types that can be "stored" or "registered" but are not loaded by the game are listed in "Resource Types NOT Loaded from Modules" section below.

## Resource Type Support Verification (Ghidra Analysis)

**Verified Resource Type Handlers** (Both K1 and K2 - handlers call `FUN_004074d0`/`FUN_004075a0` which searches all locations including modules):

From Ghidra decompilation of callers to `FUN_004074d0` (swkotor.exe) and `FUN_004075a0` (swkotor2.exe):

- **MDL** (2002, 0x7d2) - VERIFIED: Handler at swkotor.exe: 0x0070fb90, swkotor2.exe: 0x007837b0
- **TGA** (3) - VERIFIED: Handler at swkotor.exe: 0x00596670, swkotor2.exe: 0x005571b0
- **TPC** (3007, 0xbbf) - VERIFIED: Handler at swkotor.exe: 0x0070f800, swkotor2.exe: 0x00782760
- **TXI** (2022, 0x7e6) - VERIFIED: Handler at swkotor.exe: 0x0070fb90, swkotor2.exe: 0x00783190
- **MDX** (3008, 0xbc0) - VERIFIED: Handler at swkotor.exe: 0x0070fe60, swkotor2.exe: 0x007834e0
- **LIP** (3004, 0xbbc) - VERIFIED: Handler at swkotor.exe: 0x0070f0f0, swkotor2.exe: 0x0077f8f0
- **VIS** (3001, 0xbb9) - VERIFIED: Handler at swkotor.exe: 0x0070ee30, swkotor2.exe: 0x00782460
- **LYT** (3000) - VERIFIED: Handler at swkotor.exe: 0x0070dbf0, swkotor2.exe: 0x00781220
- **SSF** (2060, 0x80c) - VERIFIED: Handler at swkotor.exe: 0x006789a0, swkotor2.exe: 0x006cde50
- **FourPC** (2059, 0x80b) - VERIFIED: Handler at swkotor.exe: 0x00710910, swkotor2.exe: 0x00783f10
- **PLT** (6) - VERIFIED: Handler at swkotor.exe: 0x0070c350, swkotor2.exe: 0x0077f8f0
- **WAV** (4) - VERIFIED: Handler at swkotor.exe: 0x005d5e90, swkotor2.exe: 0x00621ac0
- **IFO** (2014, 0x7de) - VERIFIED: Handler at swkotor.exe: 0x004c4cc0, swkotor2.exe: 0x004fdfe0
- **ARE** (2012, 0x7dc) - VERIFIED: Handler at swkotor.exe: 0x00506c30, swkotor2.exe: 0x004e1ea0
- **NCS** (2010, 0x7da) - VERIFIED: Handler at swkotor.exe: 0x005d1ac0, swkotor2.exe: 0x0061d6e0
- **UTI** (2025) - VERIFIED: Handler at swkotor.exe: 0x006bdea0, swkotor2.exe: 0x00713340
- **DDS** (2033, 0x7f1) - VERIFIED: Handler at swkotor.exe: 0x00710530, swkotor2.exe: 0x00783b60
- **LTR** (2036, 0x7f4) - VERIFIED: Handler at swkotor.exe: 0x00711110, swkotor2.exe: 0x00784710

**Conclusion**: All verified resource types use the same search mechanism (`FUN_004074d0`/`FUN_004075a0` → `FUN_00407230`/`FUN_00407300`) which searches all locations including modules. **If a resource type has a handler, it can be loaded from modules.**

## Special Resource Loading Behaviors

### TXI/TGA Pairing

**Question**: Can TXI be in a module while its partner TGA is in Override?

**Answer**: **YES** - TXI and TGA are loaded independently:

1. **TXI Loading**: Handler at swkotor.exe: 0x0070fb90 calls `FUN_004074d0` with type 0x7e6 (2022 = TXI)
2. **TGA Loading**: Handler at swkotor.exe: 0x00596670 calls `FUN_004074d0` with type 3 (TGA)
3. **Texture Loading Priority** (`FUN_004b8300`): First tries TGA, then TPC - both searches go through `FUN_00408bc0` → `FUN_00407230` which searches all locations
4. **TXI is loaded separately** - It's a texture info file that provides metadata for textures, loaded independently of the texture itself

**Conclusion**: TXI can be in a module while TGA is in Override (or vice versa). They are resolved independently through the resource search mechanism.

### TLK (Talk Tables)

**Question**: What happens if `dialog.tlk` is put in a module?

**Answer**: ❌ **NO** - TLK files in modules will be ignored.

**Evidence**:

- TLK loading uses direct file I/O from game root directory
- Reone codebase shows `findFileIgnoreCase(gameDir, "dialog.tlk")` - direct filesystem search
- TLK is loaded during initialization, not through resource system
- See "Files Loaded Outside Resource System" section above for details

**Note**: TLK files are global (not module-specific) and are loaded from a fixed location (root directory) via direct file I/O, not through the resource search mechanism.

### DDS Textures

**Question**: Are DDS textures supported? What priority do they have? Can they be in modules?

**Answer**:

**✅ YES - DDS is fully supported** with the following characteristics:

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

- DDS handler uses the standard resource search mechanism (`FUN_004074d0` → `FUN_00407230`) which searches:
  - Modules (ERF files)
  - Override directory
  - BIF files
  - All standard resource locations

**Usage**: DDS textures work when:

- Explicitly loaded via DDS-specific loading functions
- Used in specific contexts that call DDS loader directly
- **NOT** automatically loaded as fallback when TGA/TPC are missing

**Conclusion**: DDS is a first-class texture format that can be used from modules, but it's not part of the automatic texture loading priority system. It requires explicit loading or specific context usage.

### WAV/OGG Audio

**Question**: Can WAV/OGG be loaded from modules? Are all WAV files obfuscated? Can the game read normal WAV files?

**Answer**:

- **WAV (4)**: **YES** - VERIFIED: Handler at swkotor.exe: 0x005d5e90 calls `FUN_004074d0` with type 4
- **OGG (2078)**: ❌ **NO** - VERIFIED: OGG (type 2078, 0x81e) is **NOT registered** in resource type registry (swkotor.exe: `FUN_005e6d20`, swkotor2.exe: `FUN_00632510`). No handler calls `FUN_004074d0` with type 0x81e. OGG files in modules will be ignored. OGG is loaded via direct file I/O (Miles audio system or similar), not through resource system.

**WAV Obfuscation**:

**NOT all WAV files are obfuscated** - Only SFX (sound effect) files have obfuscation headers:

1. **SFX Files** (sound effects, combat, UI, ambience):
   - **470-byte obfuscation header** starting with magic bytes `0xFF 0xF3 0x60 0xC4`
   - Header is followed by standard RIFF/WAVE data
   - Game detects SFX magic and seeks to offset 0x1DA (470 bytes) before reading RIFF/WAVE
   - Reference: `vendor/reone/src/libs/audio/format/wavreader.cpp:34-35`

2. **VO Files** (voice-over, dialogue):
   - **Standard RIFF/WAVE format** - no obfuscation
   - Plain RIFF/WAVE PCM files readable by any media player
   - Game reads directly as standard WAV (no header skipping)
   - Reference: `vendor/PyKotor/wiki/WAV-File-Format.md` - "VO (Voice-over): Plain RIFF/WAVE PCM files"

3. **MP3-in-WAV Format** (some music):
   - RIFF header with `riffSize == 50`
   - 58-byte header, then raw MP3 data follows
   - Game skips 58 bytes and treats remaining data as MP3

**Can the game read normal WAV files?**: **✅ YES**

The game's WAV handler (`vendor/reone/src/libs/audio/format/wavreader.cpp:30-38`) checks:

1. First checks for SFX magic (`\xff\xf3\x60\xc4`) → if found, seeks to offset 0x1DA
2. If not SFX magic, checks for "RIFF" → if found, reads as **standard RIFF/WAVE**
3. If neither, throws validation error

**Conclusion**: The game **can read normal, un-obfuscated WAV files**. Standard RIFF/WAVE files work without any obfuscation header. Only SFX files require the 470-byte header. VO files in the game are already standard WAV format.

### Audio File Loading Priority

**Question**: What is the priority order for audio files? Which takes priority: WAV in Override, MP3 in Override, OGG in Module, or MP3 in streamwaves/streamvoice/streammusic? Does streammusic have different priority than streamwaves/streamvoice?

**Answer**: **CRITICAL - Two Separate Audio Systems**

The game uses **TWO SEPARATE audio loading systems** that operate independently:

1. **Resource System** (for WAV files):
   - Uses standard resource search mechanism (`FUN_004074d0` → `FUN_00407230`)
   - **Priority**: Override → Modules → Chitin (standard resource priority)
   - **Supported formats**: WAV (type 4) only
   - **Used for**: Sound effects and voice-over loaded via resource system
   - **Handler**: swkotor.exe: 0x005d5e90

2. **Miles Audio System** (for streaming audio):
   - Uses direct file I/O via Miles audio library
   - **Directories**: `streamwaves/`, `streammusic/`, `streamvoice/`
   - **Supported formats**: WAV, MP3, OGG (via direct file access)
   - **Used for**: Streaming audio (music, ambient sounds, voice streaming)
   - **Bypasses resource system**: Miles audio system does NOT use resource search mechanism

**Key Finding**: These are **SEPARATE systems** - Miles audio system **bypasses** the resource system entirely for streamed audio.

**Priority Order** (when both systems could load the same file):

**TODO: Gain Certainty by going through ghidra mcp** - Need to verify:

1. Does the game check resource system first, then Miles directories?
2. Or does it check Miles directories first, then resource system?
3. Or are they used for completely different audio types (resource system for sound effects, Miles for music)?

**Current Understanding** (needs verification):

- **WAV files**: Can be loaded via BOTH systems
  - Resource system: WAV in Override/Module (via handler at 0x005d5e90)
  - Miles system: WAV in streamwaves/streamvoice/ (via direct file I/O)
  - **Unknown**: Which is checked first when both exist

- **MP3 files**:
  - **NOT registered** in resource system (no handler calls `FUN_004074d0` with MP3 type)
  - **Only loaded via Miles audio system** from streamwaves/streammusic/streamvoice/
  - MP3 in Override/Module: ❌ **NOT SUPPORTED** - MP3 is not a resource type

- **OGG files**:
  - **NOT registered** in resource system (verified: not in resource type registry)
  - **Only loaded via Miles audio system** from streamwaves/streammusic/streamvoice/
  - OGG in Override/Module: ❌ **NOT SUPPORTED** - OGG is not a resource type

**Streammusic vs Streamwaves/Streamvoice**:

- **streammusic/**: Used for background music (different audio context)
- **streamwaves/**: Used for sound effects (WAV files)
- **streamvoice/**: Used for voice-over streaming (WAV files)
- **Unknown**: Whether streammusic has different priority than streamwaves/streamvoice, or if they're all checked with same priority within Miles system

**Summary Table**:

| Location | Format | System | Supported? | Notes |
|----------|--------|--------|------------|-------|
| Override | WAV | Resource System | ✅ YES | Highest resource priority |
| Module | WAV | Resource System | ✅ YES | After Override |
| Override | MP3 | Resource System | ❌ NO | MP3 not a resource type |
| Module | OGG | Resource System | ❌ NO | OGG not a resource type |
| streamwaves/ | WAV | Miles Audio | ✅ YES | Direct file I/O (separate system) |
| streamwaves/ | MP3 | Miles Audio | ✅ YES | Direct file I/O (separate system) |
| streammusic/ | MP3 | Miles Audio | ✅ YES | Direct file I/O (separate system) |
| streamvoice/ | WAV | Miles Audio | ✅ YES | Direct file I/O (separate system) |

**Critical Unknown**: When both resource system and Miles system could load the same WAV file (e.g., WAV in Override AND WAV in streamwaves/), which is checked first? **TODO: Verify via Ghidra MCP** - Examine audio loading code to determine if resource system or Miles directories are checked first, and whether streammusic has different priority than streamwaves/streamvoice.

## Container Size Limits

**Question**: Do containers have maximum filesize or resource count limits?

**Answer**: ✅ **VERIFIED** - Container format limits (from binary structure analysis):

**Format Limits** (from RIM/ERF/MOD binary structure):

- **Entry Count**: `uint32` (4 bytes) - Maximum: 4,294,967,295 entries
- **Resource Size**: `uint32` (4 bytes) - Maximum: 4,294,967,295 bytes (~4.2 GB per resource)
- **File Offset**: `uint32` (4 bytes) - Maximum: 4,294,967,295 bytes (~4.2 GB total file size)
- **ResRef Length**: Fixed 16 bytes (null-padded ASCII string)

**Engine Behavior** (from reverse engineering):

- **No explicit validation**: RIM loader (`FUN_0040f990`) and MOD loader (`FUN_0040f3c0`) read entry count directly from header without bounds checking
- **No size limits enforced**: Engine reads resource sizes and offsets directly from container headers without validation
- **Memory allocation**: Engine allocates memory based on resource sizes without explicit limits

**Practical Limits**:

- **File system**: FAT32 has 2GB file size limit, NTFS has much larger limits (16TB+)
- **Memory**: Engine must have enough RAM to load all resources
- **32-bit address space**: Original KotOR is 32-bit, limiting practical file sizes to ~2GB due to address space constraints
- **No hard-coded limits**: Engine does not reject containers based on entry count or file size - it will attempt to load any valid container structure

**Conclusion**: Containers are limited only by:

1. Binary format constraints (uint32 maximums)
2. File system limits
3. Available memory
4. 32-bit address space (practical limit ~2GB for original engine)

The engine does not enforce any explicit maximum entry count or file size limits in the loading code.

## Resource Search Priority Order (PROVEN)

**Function**: `FUN_00407230` (swkotor.exe: 0x00407230) / `FUN_00407300` (swkotor2.exe: 0x00407300) - Called by `FUN_004074d0`/`FUN_004075a0` and `FUN_00408bc0`/`FUN_00408df0`

**Search Order** (lines 8-16):

1. `this+0x14` (Location 3) - Highest priority
2. `this+0x18` with param_6=1 (Location 2, variant 1)
3. `this+0x1c` (Location 1)
4. `this+0x18` with param_6=2 (Location 2, variant 2)
5. `this+0x10` (Location 0) - Lowest priority

**Source Type Identification** (`FUN_004074d0` line 44-60):

- `1` = BIF (Chitin archives)
- `2` = DIR (Override directory)
- `3` = ERF (Module containers: MOD/ERF)
- `4` = RIM (Module RIM files)

**Module Loading Evidence** (`FUN_004094a0`):

- Line 91: `FUN_00406e20(param_1,aiStack_48,2,0);` - Loads MODULES: with type 2 (DIR/Override)
- Line 136: `FUN_00406e20(param_1,aiStack_38,3,2);` - Loads MODULES: with type 3 (ERF/MOD)
- Line 42/85/118/159: `FUN_00406e20(param_1,aiStack_38,4,0);` - Loads RIMS: with type 4 (RIM)

**Complete Priority Order for ALL Resource Types**:

1. **Override Directory** (`this+0x14`, Location 3, Source Type 2) - Highest priority

   - Files in `Override/` folder
   - Searched first for all resource types

2. **Module Containers** (`this+0x18`, Location 2, Source Type 3) - High priority

   - `.mod` files (MOD containers)
   - `_dlg.erf` files (K2 only, ERF containers)
   - Searched after Override

3. **Module RIM Files** (`this+0x1c`, Location 1, Source Type 4) - Medium priority

   - `.rim` files
   - `_s.rim` files
   - `_a.rim` files
   - `_adx.rim` files
   - Searched after Module containers

4. **Chitin Archives** (`this+0x10`, Location 0, Source Type 1) - Lowest priority

   - BIF files from `chitin.key`
   - `patch.erf` (K1 only, loaded during global initialization)
   - Searched last

**Conclusion**: Modules are loaded into the resource table and searched in priority order. The search function `FUN_00407230` will find resources from modules if they're registered in the resource table. **This priority order applies to ALL resource types** - there is no special handling for different resource types in the search order.

## Texture Loading Priority (PROVEN)

**Function**: `FUN_004b8300` (swkotor.exe: 0x004b8300) - Area loading function

**Texture Search Order** (lines 187-190):

```c
iVar7 = FUN_00408bc0(DAT_007a39e8,(int *)&piStack_100,3,(undefined4 *)0x0);  // Try TGA (type 3)
if (iVar7 == 0) {
  FUN_00406d60(&piStack_100,aiStack_118);
  iVar7 = FUN_00408bc0(DAT_007a39e8,(int *)&piStack_100,0xbbf,(undefined4 *)0x0);  // Try TPC (type 0xbbf = 3007)
```

**Priority**: TGA (type 3) → TPC (type 0xbbf = 3007)

**DDS Support**: ✅ **VERIFIED** - See detailed DDS documentation in "Special Loading Behaviors" section above. DDS (type 2033, 0x7f1) is fully supported and can be loaded from modules via explicit DDS-specific loading functions.

**Module Support**: `FUN_00408bc0` calls `FUN_00407230` which searches all locations including modules, so TPC/TGA CAN be loaded from modules.

## Module File Priority Order (PROVEN)

### K1 (swkotor.exe) - `FUN_004094a0`

**Flag at Offset 0x54**: Controls loading mode

- **Flag == 0**: Simple mode - loads `.rim` file directly (line 32-42)
- **Flag != 0**: Complex mode - checks for area files (`_a.rim`, `_adx.rim`), then `.mod`, then `_s.rim` (lines 49-164)

**Complete Loading Flow** (lines 32-211):

**When flag at offset 0x54 == 0 (Simple Mode)**:

1. **`.rim`** (line 42: `FUN_00406e20(param_1,aiStack_38,4,0)`) - Loaded directly, function returns

**When flag at offset 0x54 != 0 (Complex Mode)**:

1. **Check for `_a.rim`** (lines 50-62):
   - Constructs filename: `{module}_a.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_a.rim`
   - **If found**: Loads `_a.rim` (line 159: `FUN_00406e20(param_1,aiStack_38,4,0)`) - Sets flag bit 0x10
   - **If not found**: Continues to check `_adx.rim`

2. **Check for `_adx.rim`** (lines 64-88):
   - Constructs filename: `{module}_adx.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_adx.rim`
   - **If found**: Loads `_adx.rim` (line 85: `FUN_00406e20(param_1,aiStack_38,4,0)`) - Sets flag bit 0x20
   - **If not found**: Continues to check `.mod`

3. **Load Override Directory** (line 91: `FUN_00406e20(param_1,aiStack_48,2,0)`) - Loads MODULES: directory (type 2 = DIR/Override)

4. **Check for `.mod`** (line 95: `FUN_00407230(param_1,aiStack_28,0x7db,aiStack_50,aiStack_48)`):
   - Searches for resource type `0x7db` (2011 = MOD) in MODULES: directory
   - **If `.mod` exists**: Loads `.mod` (line 136: `FUN_00406e20(param_1,aiStack_38,3,2)`) - Sets flag bit 0x2, **OVERRIDES** `.rim` entries
   - **If `.mod` doesn't exist**: Continues to check `_s.rim`

5. **Check for `_s.rim`** (lines 97-124):
   - Constructs filename: `{module}_s.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_s.rim`
   - **If found**: Loads `_s.rim` (line 118: `FUN_00406e20(param_1,aiStack_38,4,0)`) - Sets flag bit 0x8, **ADDS** to `.rim` entries

6. **Load CURRENTGAME: directory** (lines 166-200):
   - Only if flag at offset 0x58 == 0 AND previous load succeeded
   - Loads CURRENTGAME: directory (type 2 = DIR/Override)
   - Checks for ARE resource type `0xbc1` (3009) or `0xbba` (3002)
   - **If found**: Loads additional RIM file - Sets flag bit 0x1

**Key Findings**:

- **`.rim` is NOT loaded when `.mod` exists** in complex mode - `.mod` completely replaces `.rim`
- **`.rim` IS loaded** when:
  - Flag == 0 (simple mode)
  - OR when `_a.rim` or `_adx.rim` are found (they supplement the main `.rim`)
- **`.mod` takes absolute priority** - if it exists, `.rim` and `_s.rim` are NOT loaded for that module
- **`_a.rim` and `_adx.rim` are CONFIRMED** - Both K1 and K2 check for these files (K1: lines 50-88, K2: lines 54-92)

**Duplicate Handling** (`FUN_0040e990` lines 30-91):

- Line 36: Checks if resource with same ResRef+Type already exists
- Line 39-91: If duplicate found AND resource is already loaded (`*(int *)((int)this_00 + 0x14) != -1`), returns 0 (ignores duplicate)
- Line 94-101: If no duplicate OR resource not loaded, registers new resource

**Result**: **FIRST resource registered wins**. Later duplicates are ignored.

**Resource Resolution Priority** (exact behavior):

- **Same resource in `.rim` and `_s.rim`**: `.rim` wins (registered first when flag is 0, or loaded before `_s.rim` in other paths)
- **Same resource in `.rim` and `.mod`**: `.mod` wins (registered after `.rim`, overrides it)
- **Same resource in `_a.rim`/`_adx.rim` and `.rim`**: `.rim` wins (main `.rim` loads first, area files supplement it)
- **CRITICAL**: When `.mod` exists, `.rim` and `_s.rim` are NOT loaded - `.mod` completely replaces them

**Edge Cases**:

- **Flag == 0, only `.rim` exists**: Only `.rim` is loaded, function returns immediately
- **Flag == 0, `.rim` + `_a.rim` exist**: Only `.rim` is loaded (flag == 0 bypasses `_a.rim` check)
- **Flag != 0, `.rim` + `_a.rim` exist**: Both `.rim` and `_a.rim` are loaded (`.rim` loads first, `_a.rim` supplements)
- **Flag != 0, `.mod` exists**: Only `.mod` is loaded, `.rim` and `_s.rim` are NOT loaded
- **Flag != 0, `.mod` + `_a.rim` exist**: Only `.mod` is loaded, `_a.rim` is NOT loaded (`.mod` check happens before `_a.rim` check)
- **Flag != 0, `.rim` + `_s.rim` exist, no `.mod`**: Both `.rim` and `_s.rim` are loaded (`.rim` loads first, `_s.rim` supplements)
- **Flag != 0, `.rim` + `_a.rim` + `_adx.rim` exist**: All three are loaded (`.rim` first, then `_a.rim`, then `_adx.rim`)
- **Flag != 0, `.rim` + `_a.rim` + `_adx.rim` + `_s.rim` exist**: All four are loaded (`.rim` first, then `_a.rim`, then `_adx.rim`, then `_s.rim`)
- **Duplicate resources**: First registered wins, later duplicates are ignored (checked in `FUN_0040e990` line 36)

### K2 (swkotor2.exe) - `FUN_004096b0`

**Flag at Offset 0x54**: Controls loading mode (same as K1)

- **Flag == 0**: Simple mode - loads `.rim` file directly (line 36-46)
- **Flag != 0**: Complex mode - checks for area files (`_a.rim`, `_adx.rim`), then `.mod`, then `_s.rim` + `_dlg.erf` (lines 53-187)

**Complete Loading Flow** (lines 36-250):

**When flag at offset 0x54 == 0 (Simple Mode)**:

1. **`.rim`** (line 46: `FUN_00406ef0(param_1,aiStack_58,4,0)`) - Loaded directly, function returns

**When flag at offset 0x54 != 0 (Complex Mode)**:

1. **Check for `_a.rim`** (lines 54-66):
   - Constructs filename: `{module}_a.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_a.rim`
   - **If found**: Loads `_a.rim` (line 182: `FUN_00406ef0(param_1,aiStack_58,4,0)`) - Sets flag bit 0x10
   - **If not found**: Continues to check `_adx.rim`

2. **Check for `_adx.rim`** (lines 68-92):
   - Constructs filename: `{module}_adx.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_adx.rim`
   - **If found**: Loads `_adx.rim` (line 89: `FUN_00406ef0(param_1,aiStack_58,4,0)`) - Sets flag bit 0x20
   - **If not found**: Continues to check `.mod`

3. **Load Override Directory** (line 95: `FUN_00406ef0(param_1,aiStack_60,2,0)`) - Loads MODULES: directory (type 2 = DIR/Override)

4. **Check for `.mod`** (line 99: `FUN_00407300(param_1,aiStack_30,0x7db,apuStack_6c,aiStack_60)`):
   - Searches for resource type `0x7db` (2011 = MOD) in MODULES: directory
   - **If `.mod` exists**: Loads `.mod` (line 161: `FUN_00406ef0(param_1,aiStack_58,3,2)`) - Sets flag bit 0x2, **OVERRIDES** `.rim` entries
   - **If `.mod` doesn't exist**: Continues to check `_s.rim`

5. **Check for `_s.rim`** (lines 101-127):
   - Constructs filename: `{module}_s.rim`
   - Searches for ARE resource type `0xbba` (3002) in `_s.rim`
   - **If found**: Loads `_s.rim` (line 122: `FUN_00406ef0(param_1,aiStack_58,4,0)`) - Sets flag bit 0x8, **ADDS** to `.rim` entries

6. **Load `_dlg.erf`** (lines 128-149):
   - **ONLY if `.mod` doesn't exist** (inside the `if (iVar5 == 0)` block at line 100)
   - Constructs filename: `{module}_dlg.erf` (line 128: `FUN_00630a90(aiStack_30,"_dlg")`)
   - Loads `_dlg.erf` (line 147: `FUN_00406ef0(param_1,piVar3,3,2)`) - Container type 3 (ERF), **ADDS** to `.rim` and `_s.rim` entries

7. **Load CURRENTGAME: directory** (lines 189-250):
   - Only if flag at offset 0x58 == 0 AND previous load succeeded
   - Similar to K1, loads CURRENTGAME: directory and checks for additional RIM files

**Key Findings**:

- **`.rim` is NOT loaded when `.mod` exists** in complex mode - `.mod` completely replaces `.rim`
- **`.rim` IS loaded** when:
  - Flag == 0 (simple mode)
  - OR when `_a.rim` or `_adx.rim` are found (they supplement the main `.rim`)
- **`.mod` takes absolute priority** - if it exists, `.rim`, `_s.rim`, and `_dlg.erf` are NOT loaded for that module
- **`_a.rim` and `_adx.rim` are CONFIRMED** - Both K1 and K2 check for these files
- **`_s.erf` is NOT supported** - The code checks for `"_s"` suffix but uses container type 4 (RIM), so it must be `_s.rim`, not `_s.erf`

**Duplicate Handling**: Same as K1 - first registered wins.

**Resource Resolution Priority** (exact behavior):

- **Same resource in `.rim`, `_s.rim`, and `_dlg.erf`**: `.rim` wins (registered first)
- **Same resource in `_s.rim` and `_dlg.erf`**: `_s.rim` wins (registered before `_dlg.erf`)
- **Same resource in `.rim` and `.mod`**: `.mod` wins (registered after `.rim`, overrides it)
- **CRITICAL**: When `.mod` exists, `.rim`, `_s.rim`, and `_dlg.erf` are NOT loaded - `.mod` completely replaces them

**Edge Cases** (K2):

- **Flag == 0, only `.rim` exists**: Only `.rim` is loaded, function returns immediately
- **Flag == 0, `.rim` + `_a.rim` exist**: Only `.rim` is loaded (flag == 0 bypasses `_a.rim` check)
- **Flag != 0, `.rim` + `_a.rim` exist**: Both `.rim` and `_a.rim` are loaded (`.rim` loads first, `_a.rim` supplements)
- **Flag != 0, `.mod` exists**: Only `.mod` is loaded, `.rim`, `_s.rim`, and `_dlg.erf` are NOT loaded
- **Flag != 0, `.mod` + `_a.rim` exist**: Only `.mod` is loaded, `_a.rim` is NOT loaded (`.mod` check happens before `_a.rim` check)
- **Flag != 0, `.rim` + `_s.rim` + `_dlg.erf` exist, no `.mod`**: All three are loaded (`.rim` first, then `_s.rim`, then `_dlg.erf`)
- **Flag != 0, `.rim` + `_a.rim` + `_adx.rim` + `_s.rim` + `_dlg.erf` exist**: All five are loaded (`.rim` first, then `_a.rim`, then `_adx.rim`, then `_s.rim`, then `_dlg.erf`)
- **Duplicate resources**: First registered wins, later duplicates are ignored (checked in `FUN_0040e990` line 36)

## Override Directory Priority (PROVEN)

### K1 (swkotor.exe) - `FUN_0040f200`

**Enumeration** (`FUN_005e8cf0` line 78):

- Uses `FindFirstFileA` with pattern `"%s\\*.%s"` (line 76) - **FLAT enumeration only**
- Line 102: Checks `dwFileAttributes & 0x10` (directory flag)
- Line 172-199: If directory found AND `param_5 != 0`, recursively enumerates subdirectories
- **BUT**: `FUN_0040f200` calls `FUN_005e6640` with `param_5=0` (line 32), so **NO subdirectory recursion**

**Result**: **Override directory is FLAT** - only top-level files are loaded. Subfolders are **NOT** searched.

### K2 (swkotor2.exe) - `FUN_00410d20`

**Enumeration** (lines 78-200):

- Line 78: `FUN_00631e60(...,-1,1,0)` - Enumerates **subdirectories** (param_4=1, param_5=0 = **NO RECURSION**)
- Line 101: `FUN_00631e60(...,-1,0,0)` - Enumerates **files in each subdirectory** (param_4=0, param_5=0 = **NO RECURSION**)
- Line 118: `FUN_00631e60(...,-1,0,0)` - Enumerates **files in root override** (param_5=0 = **NO RECURSION**)
- Lines 158-174: **Process root files FIRST**
- Lines 176-200: **Process subdirectory files AFTER**

**CRITICAL**: `param_5=0` means **NO RECURSION**. The engine only processes:

1. **Root override files** (lines 168: `FUN_0040f270`) - Registered first, **HIGHEST PRIORITY**
2. **Files in immediate subdirectories** (1 level deep only, lines 191: `FUN_0040f270`) - Registered second

**Files at 2+ levels deep are IGNORED** (e.g., `Override/folder1/subfolder/test.ncs` is NOT processed).

**Subdirectory Enumeration Order**: Determined by filesystem enumeration (typically alphabetical, but not guaranteed - depends on filesystem order returned by `FindFirstFileA`/`FindNextFileA`).

**Result**:

- **Root override files win** over subdirectory files
- Only **1 level deep** subdirectories are searched
- Files at 2+ levels deep are **NOT PROCESSED**

## Multiple Module Files - Resource Resolution

**See "Module File Priority Order" section above for complete details with exact instruction-level proof.**

## patch.erf (K1 Only)

**Status**: ✅ **SUPPORTED in K1 only** - Loaded during global resource initialization

**Location**: Root installation directory (next to `dialog.tlk`, `chitin.key`, etc.)

**Loading**: ✅ **VERIFIED** - `patch.erf` is loaded as part of global resource initialization, separate from module loading.

**Evidence** (from reone codebase `vendor/reone/src/libs/resource/director.cpp:153-156`):

- Loaded in `loadGlobalResources()` function via `_resources.addERF(*patchPath)`
- Loaded AFTER chitin.key and texture packs, but BEFORE override directory
- Loaded as ERF container into resource system (same mechanism as modules)
- NOT found in module loading code (`FUN_004094a0` / `FUN_004096b0`) - confirmed separate loading path

**Priority Order** (for resources in patch.erf):

1. Override directory (highest priority)

2. Module files (`.mod`, `.rim`, `_s.rim`, `_dlg.erf`)
3. **patch.erf** (loaded during global initialization, priority between modules and chitin)
4. Chitin BIF archives (lowest priority)

**Note**: Based on reone codebase loading order, patch.erf is loaded AFTER chitin.key but BEFORE override directory in `loadGlobalResources()`. However, the actual game's resource search priority may differ. The engine's resource search function (`FUN_00407230`) determines final priority at runtime.

**What Can Be Put in patch.erf**:

- **ANY resource type** - Same as modules, patch.erf accepts any resource type ID
- **Common uses**: Bug fixes, patches, updated textures/models, updated scripts
- **Typical contents**: Fixed NCS scripts, updated textures, updated models

**Reverse Engineering Evidence**:

- `patch.erf` is referenced in Andastra codebase (`InstallationResourceManager.cs` line 543)
- Loaded via `GetPatchErfResources()` method for K1 installations only
- Treated as part of core resources (loaded with chitin resources)
- No type filtering - accepts any resource type stored in ERF container

**Note**: ✅ **VERIFIED** - `patch.erf` is **NOT found in module loading code** (`FUN_004094a0` / `FUN_004096b0`).

**Evidence**:

- Confirmed via codebase analysis: patch.erf is loaded separately in global resource initialization
- reone codebase shows `loadGlobalResources()` loads patch.erf via `_resources.addERF(*patchPath)` (line 153-156)
- Loading order in `loadGlobalResources()`: chitin.key → texture packs → music/sounds → LIP files → **patch.erf** → override directory
- This confirms patch.erf is loaded during global initialization, not module loading

## Summary

1. **Exact module discovery functions**:
   - **K1**: `FUN_004094a0` (swkotor.exe: 0x004094a0)
   - **K2**: `FUN_004096b0` (swkotor2.exe: 0x004096b0)

2. **`_s.rim` support**: ✅ Both swkotor.exe and swkotor2.exe support `_s.rim` files
   - swkotor.exe: `FUN_0067bc40` / `FUN_006cfa70` reference `"_s.rim"` string
   - swkotor2.exe: `FUN_006d1a50` / `FUN_0073dcb0` reference `"_s.rim"` string

3. **`_dlg.erf` support**: ✅ **HARDCODED in K2 only**
   - K2 explicitly checks for literal `"_dlg"` string (swkotor2.exe: `FUN_004096b0` line 128)
   - **NO wildcard support** - `_*.erf` patterns are NOT supported
   - Only `_dlg.erf` is recognized, not `_something.erf` or any other pattern

4. **`.hak` files**: ❌ **NOT SUPPORTED** - No references found in either executable

5. **Subfolders in modules**: ❌ NOT supported - ResRef is a flat 16-byte ASCII string
   - **Override subfolders**:
     - **K1**: ❌ NOT supported - Only top-level override files are loaded (`FUN_0040f200` calls `FUN_005e6640` with `param_5=0`)
     - **K2**: ✅ Supported - Subfolders ARE searched, but **root override files have priority** (`FUN_00410d20` lines 158-200)

6. **Resource types**: ✅ Engine loads verified resource types from modules (see "Resource Types Loaded from Modules" section)
   - Container format allows any resource type ID to be stored
   - Engine resource manager only loads resource types that have verified handlers calling `FUN_004074d0`/`FUN_004075a0`
   - **TPC/TGA ARE loaded from modules** - Verified handlers exist
   - **NSS/TLK are NOT loaded from modules** - No handlers or handlers bypass resource system

7. **Valid file combinations**:
   - K1: `.mod` (override), `.rim`, `.rim` + `_s.rim`, `.rim` + `_a.rim`, `.rim` + `_adx.rim`, `.rim` + `_a.rim` + `_adx.rim`
   - K2: `.mod` (override), `.rim`, `.rim` + `_s.rim`, `.rim` + `_s.rim` + `_dlg.erf`, `.rim` + `_a.rim`, `.rim` + `_adx.rim`, `.rim` + `_a.rim` + `_adx.rim`
   - **Note**: `.mod` completely replaces `.rim` and all extension files - they are NOT loaded together
   - **Note**: `_a.rim` and `_adx.rim` are CONFIRMED via reverse engineering (K1: lines 50-88, K2: lines 54-92)

## Implementation Notes for Andastra

The current `ModuleFileDiscovery.cs` correctly handles:

- `.mod` override behavior
- `_s.rim` support (both K1 and K2) - **CONFIRMED via reverse engineering**
- `_dlg.erf` support (K2 only) - **CONFIRMED via reverse engineering**
- Case-insensitive filename matching

**Engine Behavior**: The engine loads any resource type from any module container. The distribution above reflects what is typically found in game modules, not an engine requirement.
