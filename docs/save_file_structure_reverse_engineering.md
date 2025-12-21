# Save File Structure - Reverse Engineering Findings

**⚠️ IMPORTANT**: This document contains **exclusively** documentation from reverse engineered components of `swkotor.exe` and `swkotor2.exe` using Ghidra MCP. All findings are based on analysis of the original game executables, not third-party engine rewrites or custom implementations.

## Executive Summary

This document details the reverse engineering findings for save file structure, cached entities, resource priority, and nested module support in `swkotor.exe` and `swkotor2.exe`.

**Key Findings:**

1. **Save files are ERF archives** containing game state (GLOBALVARS.res, PARTYTABLE.res, savenfo.res, cached modules)
2. **RES, PT, NFO priority**: RES and NFO bypass resource system (direct file I/O), PT uses resource system
3. **Nested modules**: Save files contain cached module ERF/RIM files for previously visited areas
4. **Cached entities**: Party members (AVAILNPC*.utc), inventory (INVENTORY.res), reputation (REPUTE.fac), and module states are cached

## Save File Structure

### Save File Format

**File**: `savegame.sav` (ERF archive with "MOD V1.0" signature)

**Location**: `SAVES:\{saveName}\savegame.sav`

**Contents**:

1. **savenfo.res** (GFF with "NFO " signature) - Save metadata
2. **GLOBALVARS.res** (GFF with "GLOB" signature) - Global variable state
3. **PARTYTABLE.res** (GFF with "PT  " signature) - Party state
4. **INVENTORY.res** (GFF) - Player inventory items
5. **REPUTE.fac** (GFF with "FAC " signature) - Faction reputation data
6. **AVAILNPC*.utc** (up to 12 files, indices 0-11) - Cached companion character data
7. **Cached module ERF/RIM files** - Previously visited areas with full state preserved

### Save File Loading

**Function**: `FUN_006ca250` (swkotor.exe: 0x006ca250) - Load save game

**Evidence**:
- swkotor.exe: `FUN_006ca250` line 110: Creates "GAMEINPROGRESS:" directory alias
- swkotor.exe: `FUN_006ca250` line 145: Loads override directory (`FUN_00408800`)
- swkotor.exe: `FUN_006ca250` line 147: Loads PARTYTABLE (`FUN_00565d20`)
- swkotor.exe: `FUN_004b8300` line 136-155: Loads "savenfo.res" directly via `FUN_00411260`

## Resource Priority: RES, PT, NFO

### RES Files (Resource Type 0)

**Loading Mechanism**: **Direct file I/O**, bypasses resource system

**Evidence**:
- swkotor.exe: `FUN_004b8300` line 136-155 loads "savenfo.res" directly via `FUN_00411260` (GFF loader)
- No RES handler found that calls `FUN_004074d0` (resource system)
- RES files are accessed directly from save file path, bypassing `FUN_00407230` entirely

**Priority**: ❌ **Module/Override/Patch.erf RES files are IGNORED**

- RES files in modules, override, or patch.erf will **NOT** override save file RES files
- RES loader bypasses resource system, so resource priority chain does not apply
- Only RES files in save files are loaded

### PT Files (PARTYTABLE.res)

**Loading Mechanism**: **Resource system** (`FUN_00407230`)

**Evidence**:
- swkotor.exe: `FUN_00565d20` (0x00565d20) loads PARTYTABLE
- swkotor.exe: `FUN_00565d20` line 51: Calls `FUN_00410630` with "PARTYTABLE" and "PT  " signature
- swkotor.exe: `FUN_00410630` line 48: Calls `FUN_00407680` with resource type
- swkotor.exe: `FUN_00407680` line 12: Calls `FUN_00407230` (resource system search function)

**Priority**: ✅ **Module/Override/Patch.erf PT files WILL override save file PT files**

**Complete Priority Order** (from `FUN_00407230`):
1. **Override Directory** (Location 3, Source Type 2) - Highest priority
2. **Module Containers** (Location 2, Source Type 3) - `.mod` files
3. **Module RIM Files** (Location 1, Source Type 4) - `.rim`, `_s.rim`, `_a.rim`, `_adx.rim`
4. **Chitin Archives** (Location 0, Source Type 1) - **Includes patch.erf** (K1 only)
5. **Save Files** (GAMEINPROGRESS: directory) - Lowest priority

**Conclusion**: If PARTYTABLE.res exists in override, modules, or patch.erf, it will be loaded **BEFORE** the save file version.

### NFO Files (savenfo.res)

**Loading Mechanism**: **Direct file I/O**, bypasses resource system

**Evidence**:
- swkotor.exe: `FUN_004b8300` line 136-155 loads "savenfo.res" directly via `FUN_00411260` (GFF loader)
- `FUN_00411260` does not call `FUN_00407230` (resource system)
- NFO files are accessed directly from save file path, bypassing resource system

**Priority**: ❌ **Module/Override/Patch.erf NFO files are IGNORED**

- NFO files in modules, override, or patch.erf will **NOT** override save file NFO files
- NFO loader bypasses resource system, so resource priority chain does not apply
- Only NFO files in save files are loaded

## Cached Entities in Save Files

### What IS Cached

**1. Party Members (AVAILNPC*.utc)**
- **Files**: AVAILNPC0.utc, AVAILNPC1.utc, ..., AVAILNPC11.utc (up to 12 files)
- **Contents**: Complete companion character data (stats, equipment, feats, powers, skills, inventory)
- **Updated**: Whenever companion stats/equipment changes
- **Indexes**: Match PT_AVAIL_NPCS in PARTYTABLE.res

**2. Inventory (INVENTORY.res)**
- **File**: INVENTORY.res (GFF with "ItemList" containing item structs)
- **Contents**: Player's inventory items (not equipped)
- **Updated**: Whenever items picked up, dropped, or used
- **Format**: GFF file with item template data (resref, stack size, etc.)

**3. Reputation (REPUTE.fac)**
- **File**: REPUTE.fac (GFF with "FAC " signature)
- **Contents**: Faction reputation data
- **Updated**: When reputation-affecting actions occur

**4. Cached Modules (ERF/RIM files)**
- **Files**: `{module}.sav` (ERF archives) for previously visited areas
- **Contents**: Complete module state (module.ifo, creatures, placeables, triggers, etc.)
- **Updated**: When area is visited and state changes
- **Common Issue**: EventQueue in module.ifo can corrupt → save won't load
- **Fix**: Clear EventQueue GFF list from each cached module's IFO

**5. Global Variables (GLOBALVARS.res)**
- **File**: GLOBALVARS.res (GFF with "GLOB" signature)
- **Contents**: All global int/bool/string variables
- **Updated**: When global variables are set

**6. Party State (PARTYTABLE.res)**
- **File**: PARTYTABLE.res (GFF with "PT  " signature)
- **Contents**: Party member list, selection, gold, XP pool, solo mode, cheat flags
- **Updated**: When party composition or state changes

**7. Save Metadata (savenfo.res)**
- **File**: savenfo.res (GFF with "NFO " signature)
- **Contents**: Save name, time, screenshot, current module, entry position
- **Updated**: When save is created

### What is NOT Cached

**1. Module Resources (TPC, TGA, MDL, MDX, etc.)**
- **Status**: NOT cached in save files
- **Reason**: Loaded from modules/BIF files on-demand
- **Exception**: Cached modules contain their own resources, but base module resources are not duplicated

**2. Scripts (NCS files)**
- **Status**: NOT cached in save files
- **Reason**: Loaded from modules/BIF files on-demand
- **Exception**: Script state (variables, execution state) may be cached in module state

**3. Dialog Trees (DLG files)**
- **Status**: NOT cached in save files
- **Reason**: Loaded from modules/BIF files on-demand
- **Exception**: Dialog state (selected entries, flags) may be cached in module state

**4. Area Layouts (ARE, GIT files)**
- **Status**: NOT cached in save files (base data)
- **Reason**: Loaded from modules/BIF files on-demand
- **Exception**: Area state (entity positions, door states) IS cached in module state

**5. Texture Packs (TPA, TPB, TPC files)**
- **Status**: NOT cached in save files
- **Reason**: Loaded from texture pack directories on-demand

## Nested Modules in Save Files

### Structure

**Cached modules are ERF/RIM files stored inside savegame.sav ERF archive**

**Evidence**:
- PyKotor `SaveNestedCapsule` class shows cached modules as ERF files with resource type SAV (2057)
- Each cached module is a complete ERF archive containing:
  - module.ifo (module info)
  - Area GFF files (creatures, placeables, triggers, etc.)
  - Entity states (positions, HP, door/placeable states)

**Loading**:
- swkotor.exe: `FUN_00409460` → `FUN_00408e90` loads GAMEINPROGRESS: directory
- Cached modules are loaded into resource table when save is loaded
- Module states are applied when areas are entered

**Common Issues**:
- **EventQueue corruption**: EventQueue in module.ifo can become corrupted → save won't load
- **Fix**: Clear EventQueue GFF list from each cached module's IFO
- **Missing modules**: If cached module references are missing, save may fail to load

## Mod Uninstall Issues

### Problem

When mods are uninstalled and saves are reused, the following issues occur:

1. **Missing Resources**: Cached modules may reference resources (UTI, UTC, etc.) that no longer exist
2. **Corrupted References**: Entity states may reference templates that are missing
3. **EventQueue Corruption**: Cached module IFO files may have corrupted EventQueue
4. **Inventory Items**: Inventory may contain items from uninstalled mods
5. **Party Members**: AVAILNPC*.utc files may reference templates that no longer exist

### Mitigation Strategies

**1. Validate Resources Before Loading**
- Check if referenced resources exist before loading
- Remove invalid references or replace with defaults

**2. Clean EventQueue**
- Clear EventQueue GFF list from cached module IFO files
- Prevents corruption-related save load failures

**3. Remove Invalid Inventory Items**
- Scan inventory for items with missing templates
- Remove or replace with default items

**4. Validate Party Members**
- Check if AVAILNPC*.utc templates exist
- Remove or replace with default companions

**5. Rebuild Cached Modules**
- Remove cached modules from save file
- Force game to rebuild module state from base modules

## Implementation Notes

### Save Serializer Requirements

1. **Support nested ERF/RIM files** in savegame.sav
2. **Preserve resource order** for byte-identical repacks
3. **Handle cached modules** as ERF archives
4. **Support all cached entity types** (AVAILNPC, INVENTORY, REPUTE, etc.)
5. **Validate resources** before saving/loading

### Save Editor Requirements

1. **Undo/Redo support** for all cached entity modifications
2. **Selective component removal** (remove cached modules, inventory items, etc.)
3. **Resource validation** (check for missing templates, etc.)
4. **EventQueue cleanup** (clear corrupted EventQueue from cached modules)
5. **Integration with other editors** (GITEditor, etc.) to handle cached data

