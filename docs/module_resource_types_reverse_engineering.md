# Module Resource Types - Reverse Engineering Findings

## Executive Summary

This document details the reverse engineering findings for module file discovery and resource type support in `swkotor.exe` and `swkotor2.exe`.

**Key Findings:**

1. **Both executables support `_s.rim` files** - confirmed via string references in both binaries
2. **No subfolder support** - ResRef is a flat 16-byte ASCII string, no path separators
3. **Container formats don't filter** - RIM/ERF/MOD containers accept any resource type ID
4. **Engine resource loading** - The engine's resource manager likely accepts any resource type stored in modules, as long as the type ID is valid and the data can be parsed

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

**Key Finding**: `_dlg.erf` is **HARDCODED** in K2. The engine explicitly checks for the literal string `"_dlg"` (swkotor2.exe: `FUN_004096b0` line 128). There is **NO wildcard support** for `_*.erf` patterns.

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

**However**, the **convention** (not a hard requirement) is:

- **`.rim` (MAIN)**: ARE, IFO, GIT only
- **`_s.rim` (DATA)**: FAC, LYT, NCS, PTH, UTC, UTD, UTE, UTI, UTM, UTP, UTS, UTT, UTW, DLG (K1)
- **`_dlg.erf` (K2_DLG)**: DLG only (K2)
- **`.mod` (MOD)**: Everything EXCEPT TwoDA (TwoDA files must be in override or chitin)

**Reverse Engineering Evidence**:

- Module loading code (`FUN_004094a0` swkotor.exe: 0x004094a0 / `FUN_004096b0` swkotor2.exe: 0x004096b0) opens RIM files without type filtering
- Resource extraction code reads all entries from container regardless of type
- No type validation found in module resource loading path
- Container format allows any resource type ID to be stored

### Resource Types: What CAN and CANNOT Be Packed

**PROOF: Engine loads ANY resource type from modules - NO filtering**

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

4. **2DA Loader** (`FUN_00413b40` swkotor.exe: 0x00413b40):
   - Line 43: `puVar3 = (undefined4 *)FUN_004074d0(DAT_007a39e8,param_1,0x7e1);`
   - Searches resource table for type `0x7e1` (2017 = TwoDA)
   - **Does NOT check source** - will load from modules, override, or chitin
   - **PROVES 2DA CAN be loaded from modules**

**Conclusion**: The engine loads **ALL resource types** from modules with **ZERO filtering**. Any resource type stored in a RIM/ERF/MOD container will be:

1. Registered in the resource table (via `FUN_0040e990`)
2. Available for loading by type-specific loaders (e.g., `FUN_00413b40` for 2DA)
3. Loaded regardless of type (TPC, TGA, 2DA, etc.)

**CAN be packed into modules** (PROVEN by code analysis):

- **TwoDA (0x7e1)**: ✅ **YES** - `FUN_00413b40` loads from resource table, no source filtering
- **TPC/TGA textures**: ✅ **YES** - No type filtering in RIM/MOD loaders
- **All GFF-based types**: ✅ **YES** - No type filtering
- **All binary formats**: ✅ **YES** - No type filtering
- **Media files**: ✅ **YES** - No type filtering
- **Any resource type**: ✅ **YES** - Engine accepts any type ID stored in containers

**CANNOT or SHOULD NOT be packed** (engine limitations or conventions):

- **TwoDA**: Convention says use override/chitin, but engine WILL load from modules (see proof above)
- **TLK**: Talk tables are global, not module-specific (but engine would load if stored)
- **KEY/BIF**: Chitin key/archive files (not module containers)
- **MOD/RIM/ERF/SAV**: Nested containers not supported
- **HAK/NWM**: Aurora/NWN formats, not KotOR
- **RES**: Save data format, not module content
- **DDS**: Not found in texture loading code - likely not supported or uses different path

**Note on TwoDA**: **YES, you CAN drop a 2DA into a module.** The proof:

- `FUN_0040f990` (RIM loader) and `FUN_0040f3c0` (MOD loader) register ALL entries with `FUN_0040e990`
- `FUN_00413b40` (2DA loader) searches the resource table for type `0x7e1` - it doesn't care where it came from
- Convention says use override/chitin, but the engine will load 2DA from modules

**Note on TPC/TGA**: **YES, these CAN be containerized** in modules. The proof:

- RIM/MOD loaders (`FUN_0040f990` / `FUN_0040f3c0`) iterate through ALL entries
- No type filtering - every entry is registered in the resource table
- Texture loaders (`FUN_004b8300` line 187-190) search through resource table:
  - First tries TGA (type 3) via `FUN_00408bc0`
  - Then tries TPC (type 0xbbf = 3007) via `FUN_00408bc0`
  - `FUN_00408bc0` calls `FUN_00407230` which searches all locations including modules
- **Texture Priority**: TGA → TPC (no DDS support in this code path)
- **DDS**: Not found in texture loading code - likely not supported or uses different path

### Known Resource Types from Andastra

Based on `ResourceType.cs`, the following resource types are defined:

**Core Game Resources** (likely supported in modules):

- `ARE` (2012) - Area data
- `IFO` (2014) - Module info
- `GIT` (2023) - Area instance data
- `DLG` (2029) - Dialog trees
- `UTI` (2025) - Item templates
- `UTC` (2027) - Creature templates
- `UTD` (2042) - Door templates
- `UTP` (2044) - Placeable templates
- `UTS` (2035) - Sound templates
- `UTT` (2032) - Trigger templates
- `UTW` (2058) - Waypoint templates
- `UTM` (2051) - Merchant templates
- `JRL` (2056) - Journal entries
- `PTH` (3003) - Pathfinding data
- `TwoDA` (2017) - 2D array data
- `WOK` (2016) - Walkmesh data
- `DWK` (2052) - Door walkmesh
- `PWK` (2053) - Placeable walkmesh
- `MDL` (2002) - 3D models
- `MDX` (3008) - Model animations
- `TPC` (3007) - Textures
- `TGA` (3) - Texture images
- `TXI` (2022) - Texture info
- `NCS` (2010) - Compiled scripts
- `NSS` (2009) - Script source (unlikely in modules)
- `SSF` (2060) - Soundset files
- `LIP` (3004) - Lip sync data
- `VIS` (3001) - Visibility data
- `LYT` (3000) - Layout data
- `FAC` (2038) - Faction data
- `GUI` (2047) - GUI definitions
- `CUT` (2074) - Cutscene data

**Unlikely/Unsupported in Modules**:

- `RES` (0) - Save data (SAV containers only)
- `SAV` (2057) - Save game containers
- `KEY` (9999) - Chitin key files
- `BIF` (9998) - BIF archives
- `MOD` (2011) - Module containers (nested modules not supported)
- `RIM` (3002) - RIM containers (nested RIMs not supported)
- `ERF` (9997) - ERF containers (nested ERFs not supported)
- `HAK` (2061) - HAK archives (Aurora/NWN only, not KotOR)
- `NWM` (2062) - NWM modules (Aurora/NWN only)

**Media Files** (may be supported):

- `WAV` (4) - Audio files
- `BMU` (8) - Obfuscated MP3 audio
- `OGG` (2078) - OGG audio
- `MVE` (2) - Video files
- `MPG` (9) - MPEG video
- `BIK` (2063) - Bink video

## Resource Search Priority Order (PROVEN)

**Function**: `FUN_00407230` (swkotor.exe: 0x00407230) - Called by `FUN_004074d0` and `FUN_00408bc0`

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

**Conclusion**: Modules are loaded into the resource table and searched in priority order. The search function `FUN_00407230` will find resources from modules if they're registered in the resource table.

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

**DDS Support**: NOT found in texture loading code - likely not supported or uses different path

**Module Support**: `FUN_00408bc0` calls `FUN_00407230` which searches all locations including modules, so TPC/TGA CAN be loaded from modules.

## Multiple Module Files - Resource Resolution

When the same resource exists in multiple module files (e.g., `module.rim` and `module_s.rim`), the engine uses the **first match** found in the search order:

1. Resources are registered in order: `.mod` → `.rim` → `_s.rim` → `_dlg.erf` (K2)
2. `FUN_00407230` searches locations in priority order (see above)
3. **First match wins** - later registrations are ignored if resource already exists

**Example**: If `appearances.2da` exists in both `module.rim` and `module_s.rim`:
- First file loaded registers the resource
- Second file's entry is skipped (duplicate ResRef+Type)
- Engine uses the first one found

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

5. **Subfolders**: ❌ NOT supported - ResRef is a flat 16-byte ASCII string

6. **Resource types**: ✅ Engine accepts ANY resource type in modules (no filtering)
   - Container format allows any resource type ID
   - Engine resource manager loads any type stored in containers
   - **TPC/TGA CAN be containerized** - This is a "game changer" for modding
   - **TwoDA CAN be containerized** - Though convention says otherwise
   - Convention: Follow `KModuleType.Contains()` for compatibility, but engine is permissive

7. **Valid file combinations**:
   - K1: `.mod` (override), `.rim`, `.rim` + `_s.rim`, `.rim` + `_a.rim`, `.rim` + `_adx.rim`
   - K2: `.mod` (override), `.rim`, `.rim` + `_s.rim`, `.rim` + `_s.rim` + `_dlg.erf`, `.rim` + `_a.rim`, `.rim` + `_adx.rim`

## Implementation Notes for Andastra

The current `ModuleFileDiscovery.cs` correctly handles:

- `.mod` override behavior
- `_s.rim` support (both K1 and K2) - **CONFIRMED via reverse engineering**
- `_dlg.erf` support (K2 only) - **CONFIRMED via reverse engineering**
- Case-insensitive filename matching

The `KModuleType.Contains()` method in `Module.cs` implements the **conventional** resource type distribution (not a hard engine requirement):

- **MAIN (.rim)**: ARE, IFO, GIT only
- **DATA (_s.rim)**: FAC, LYT, NCS, PTH, UTC, UTD, UTE, UTI, UTM, UTP, UTS, UTT, UTW, DLG (K1)
- **K2_DLG (_dlg.erf)**: DLG only (K2)
- **MOD (.mod)**: Everything EXCEPT TwoDA

**Important**: The engine is permissive - it will load any resource type from any module container. Following the convention ensures compatibility with tooling and modding practices.
