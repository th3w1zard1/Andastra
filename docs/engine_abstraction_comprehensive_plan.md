# Engine Abstraction Comprehensive Plan

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Purpose**: Internal tracking document - DO NOT COMMIT

## Overview

Refactor the codebase to support multiple BioWare engine families (Odyssey, Aurora, Eclipse) through a comprehensive abstraction layer, following xoreos's modular pattern but with cleaner design. Maximize code reuse through base classes, minimize duplication, and ensure 1:1 parity with original KOTOR 2 engine behavior (verified via Ghidra).

## Architecture Pattern (Inspired by xoreos, but cleaner)

```
AuroraEngine.Common (pure file formats, installation, resource management)
    ↑
Odyssey.Engines.Common (base engine abstraction)
    ↑
├── Odyssey.Engines.Odyssey (KOTOR 1/2 shared code - like xoreos's kotorbase)
│   ├── Odyssey.Engines.Odyssey.K1 (KOTOR 1 specific - optional, can use profiles)
│   └── Odyssey.Engines.Odyssey.K2 (KOTOR 2 specific - optional, can use profiles)
├── Odyssey.Engines.Aurora (NWN/NWN2 base - like xoreos's aurora)
│   ├── Odyssey.Engines.Aurora.NWN (NWN specific)
│   └── Odyssey.Engines.Aurora.NWN2 (NWN2 specific)
└── Odyssey.Engines.Eclipse (Dragon Age/Mass Effect base - like xoreos's eclipse)
    ├── Odyssey.Engines.Eclipse.DragonAge (Dragon Age specific)
    └── Odyssey.Engines.Eclipse.MassEffect (Mass Effect specific)
```

## Key Principles

1. **AuroraEngine.Common** must be engine-agnostic:
   - File format parsers (GFF, 2DA, TLK, MDL, TPC, etc.) - ✅ KEEP
   - Installation detection and resource management - ✅ KEEP
   - NO game-specific enums (Game.K1, Game.K2) - ❌ MOVE
   - NO engine-specific module structures (KModuleType) - ❌ MOVE
   - NO engine-specific GFF templates (UTC, UTD, etc.) - ❌ MOVE (or make generic)

2. **Odyssey.Engines.Common** provides base abstraction:
   - IEngine, IEngineGame, IEngineModule, IEngineProfile interfaces
   - BaseEngine, BaseEngineGame, BaseEngineModule, BaseEngineProfile classes
   - Common engine logic shared across all engines

3. **Odyssey.Engines.Odyssey** contains KOTOR 1/2 shared code:
   - Game enum (K1, K2, etc.) - moved from AuroraEngine.Common
   - KModuleType enum and Module class - moved from AuroraEngine.Common
   - GFF template classes (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, etc.) - moved from AuroraEngine.Common
   - Shared KOTOR game logic

4. **Engine-specific projects** contain game-specific implementations:
   - Game profiles (K1GameProfile, K2GameProfile, etc.)
   - Engine API implementations (K1EngineApi, K2EngineApi, etc.)
   - Game-specific systems and components

## Files to Move from AuroraEngine.Common

### KOTOR-Specific Code (Move to Odyssey.Engines.Odyssey)

1. **Common/Game.cs** - Game enum (K1, K2, etc.) and extensions
   - Status: [ ] Identify all usages
   - Status: [ ] Move to Odyssey.Engines.Odyssey.Common
   - Status: [ ] Update all references

2. **Common/Module.cs** - KModuleType enum and Module class
   - Status: [ ] Identify all usages
   - Status: [ ] Move to Odyssey.Engines.Odyssey.Module
   - Status: [ ] Update all references

3. **Common/ModuleDataLoader.cs** - KOTOR-specific module data loading
   - Status: [ ] Review if KOTOR-specific
   - Status: [ ] Move if needed

4. **Resource/Generics/** - GFF template classes (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, etc.)
   - Status: [ ] Identify all template classes
   - Status: [ ] Move to Odyssey.Engines.Odyssey.Templates
   - Status: [ ] Update all references
   - Note: These are KOTOR/Odyssey-specific GFF structures

5. **Resource/Generics/IFO.cs** - Module info (IFO) structure
   - Status: [ ] Review if KOTOR-specific or shared
   - Status: [ ] Move if KOTOR-specific

6. **Resource/Generics/ARE.cs** - Area (ARE) structure
   - Status: [ ] Review if KOTOR-specific or shared
   - Status: [ ] Move if KOTOR-specific

7. **Resource/Generics/GIT.cs** - Game instance template (GIT) structure
   - Status: [ ] Review if KOTOR-specific or shared
   - Status: [ ] Move if KOTOR-specific

8. **Resource/Generics/JRL.cs** - Journal (JRL) structure
   - Status: [ ] Review if KOTOR-specific or shared
   - Status: [ ] Move if KOTOR-specific

9. **Resource/Generics/PTH.cs** - Path (PTH) structure
   - Status: [ ] Review if KOTOR-specific or shared
   - Status: [ ] Move if KOTOR-specific

10. **Tools/Module.cs** - KOTOR-specific module tools
    - Status: [ ] Review if KOTOR-specific
    - Status: [ ] Move if needed

11. **Uninstall/UninstallHelpers.cs** - References Game enum
    - Status: [ ] Update to use engine-agnostic approach
    - Status: [ ] Keep in AuroraEngine.Common but make generic

### Engine-Agnostic Code (Keep in AuroraEngine.Common)

1. **Common/GameObject.cs** - ObjectType enum and GameObject class (shared across engines)
2. **Common/ResRef.cs** - Resource reference (shared)
3. **Formats/** - All file format parsers (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, etc.)
4. **Installation/** - Installation detection and resource management
5. **Resources/** - Resource management and loading
6. **Logger/** - Logging infrastructure
7. **Utility/** - Utility classes

## Project Structure

### AuroraEngine.Common
- Pure file formats (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM, etc.)
- Installation detection (determine game type, find installation paths)
- Resource management (resource loading, precedence, caching)
- NO game-specific code
- NO engine-specific code

### Odyssey.Engines.Common
- Base engine interfaces (IEngine, IEngineGame, IEngineModule, IEngineProfile)
- Base engine classes (BaseEngine, BaseEngineGame, BaseEngineModule, BaseEngineProfile)
- Common engine logic shared across all engines

### Odyssey.Engines.Odyssey
- KOTOR 1/2 shared code
- Game enum (K1, K2, etc.)
- KModuleType enum and Module class
- GFF template classes (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, etc.)
- Shared KOTOR game logic
- OdysseyEngine, OdysseyGameSession, OdysseyModuleLoader

### Odyssey.Engines.Odyssey.K1 (Optional)
- KOTOR 1 specific implementations
- K1GameProfile
- K1EngineApi
- K1-specific systems

### Odyssey.Engines.Odyssey.K2 (Optional)
- KOTOR 2 specific implementations
- K2GameProfile
- K2EngineApi
- K2-specific systems

### Odyssey.Engines.Aurora
- NWN/NWN2 base code
- AuroraEngine, AuroraGameSession, AuroraModuleLoader
- NWN-specific structures (tilesets, etc.)

### Odyssey.Engines.Eclipse
- Dragon Age/Mass Effect base code
- EclipseEngine, EclipseGameSession, EclipseModuleLoader
- Eclipse-specific structures

## Migration Strategy

### Phase 1: Identify and Catalog
- [ ] List all files in AuroraEngine.Common
- [ ] Identify KOTOR-specific code
- [ ] Identify engine-specific code
- [ ] Create migration checklist

### Phase 2: Create Target Structure
- [x] Create Odyssey.Engines.Common
- [x] Create Odyssey.Engines.Odyssey
- [x] Create Odyssey.Engines.Aurora (placeholder)
- [x] Create Odyssey.Engines.Eclipse (placeholder)

### Phase 3: Move KOTOR-Specific Code
- [ ] Move Game.cs to Odyssey.Engines.Odyssey.Common
- [ ] Move Module.cs to Odyssey.Engines.Odyssey.Module
- [ ] Move GFF templates to Odyssey.Engines.Odyssey.Templates
- [ ] Update all references
- [ ] Verify compilation

### Phase 4: Refactor Dependencies
- [ ] Update AuroraEngine.Common to remove KOTOR dependencies
- [ ] Update Odyssey.Engines.Odyssey to use moved code
- [ ] Update Odyssey.Kotor to use Odyssey.Engines.Odyssey
- [ ] Update all project references

### Phase 5: Verify and Test
- [ ] Compile all projects
- [ ] Fix compilation errors
- [ ] Run existing tests
- [ ] Verify no regressions

## Notes

- Follow xoreos's pattern but avoid their design issues
- Maximize code in base classes
- Minimize duplication
- Ensure C# 7.3 compatibility
- Maintain 1:1 parity with original KOTOR 2 engine (Ghidra verification)
- Consider Andastra, HolocronToolset, NCSDecomp, and KotorDiff dependencies

