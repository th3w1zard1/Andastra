# Engine Abstraction Strategy

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Purpose**: Internal tracking document - DO NOT COMMIT

## Overview

Refactor the codebase to support multiple BioWare engine families (Odyssey, Aurora, Eclipse) through a comprehensive abstraction layer, following xoreos's modular pattern but with cleaner design. Maximize code reuse through base classes, minimize duplication, and ensure 1:1 parity with original KOTOR 2 engine behavior (verified via Ghidra).

## Architecture Pattern (Inspired by xoreos)

### Xoreos Structure

```
src/aurora/              - File format parsers (engine-agnostic)
src/engines/engine.h     - Base engine class
src/engines/kotorbase/   - Shared KOTOR 1/2 code
src/engines/kotor/       - KOTOR 1 specific (inherits from KotORBase)
src/engines/kotor2/      - KOTOR 2 specific (inherits from KotORBase)
src/engines/aurora/      - Aurora engine utilities
src/engines/nwn/         - NWN specific
src/engines/eclipse/      - Eclipse base
```

### Our Structure

```
AuroraEngine.Common (src/CSharpKOTOR/)
    - File format parsers (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM, etc.)
    - Installation detection (currently KOTOR-specific, but structure allows expansion)
    - Resource management
    - Game enum (KOTOR-specific, kept for patcher tools compatibility)
    - NO rendering dependencies (MonoGame/Stride)

Odyssey.Engines.Common
    - Base engine interfaces (IEngine, IEngineGame, IEngineModule, IEngineProfile)
    - Base engine classes (BaseEngine, BaseEngineGame, BaseEngineModule, BaseEngineProfile)
    - EngineFamily enum

Odyssey.Engines.Odyssey
    - KOTOR 1/2 shared runtime code (like xoreos's kotorbase)
    - Game enum wrapper (references AuroraEngine.Common.Game)
    - Module class (KOTOR-specific module structure)
    - GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW)
    - OdysseyEngine, OdysseyGameSession, OdysseyModuleLoader
    - Profiles (OdysseyK1GameProfile, OdysseyK2GameProfile)
    - EngineApi (OdysseyK1EngineApi, OdysseyK2EngineApi)

Odyssey.Engines.Aurora (future)
    - NWN/NWN2 shared code
    - AuroraEngine, AuroraGameSession, AuroraModuleLoader

Odyssey.Engines.Eclipse (future)
    - Dragon Age/Mass Effect shared code
    - EclipseEngine, EclipseGameSession, EclipseModuleLoader
```

## Key Design Decisions

### 1. Game Enum Location

**Decision**: Keep `Game` enum in `AuroraEngine.Common` for now.

**Rationale**:
- Used by patcher tools (Andastra, HolocronToolset, NCSDecomp, KotorDiff)
- Creating a project reference from `AuroraEngine.Common` to `Odyssey.Engines.Odyssey` would create a circular dependency
- `AuroraEngine.Common` is currently KOTOR-specific, but the structure allows for future expansion
- Document it as KOTOR-specific and create a wrapper in `Odyssey.Engines.Odyssey` if needed

**Future**: When Aurora/Eclipse engines are implemented, we can:
- Create an engine-agnostic `IEngineInstallation` interface
- Move KOTOR-specific `Installation` to `Odyssey.Engines.Odyssey`
- Create Aurora/Eclipse-specific installation classes

### 2. Installation Class

**Decision**: Keep `Installation` in `AuroraEngine.Common` for now.

**Rationale**:
- Used by patcher tools
- KOTOR-specific (checks for swkotor.exe, swkotor2.exe)
- Can be refactored later when other engines are implemented

**Future**: Create `IEngineInstallation` interface and move KOTOR-specific implementation to `Odyssey.Engines.Odyssey`.

### 3. GFF Templates

**Decision**: Move all GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW) to `Odyssey.Engines.Odyssey.Templates`.

**Rationale**:
- These are KOTOR/Odyssey-specific GFF structures
- Not used by Aurora or Eclipse engines
- Keeps `AuroraEngine.Common` focused on file format parsers

### 4. Module Class

**Decision**: Move `Module` class and `KModuleType` enum to `Odyssey.Engines.Odyssey.Module`.

**Rationale**:
- KOTOR-specific module structure (.rim, _s.rim, _dlg.erf, .mod)
- Aurora and Eclipse engines have different module structures
- Keeps `AuroraEngine.Common` focused on file format parsers

## Migration Phases

### Phase 1: Foundation ✅
- [x] Create `Odyssey.Engines.Common` with base interfaces and classes
- [x] Create `Odyssey.Engines.Odyssey` project structure
- [x] Create `Odyssey.Engines.Aurora` placeholder
- [x] Create `Odyssey.Engines.Eclipse` placeholder

### Phase 2: Game Enum (Deferred)
- [ ] Keep `Game` enum in `AuroraEngine.Common` (used by patcher tools)
- [ ] Document as KOTOR-specific
- [ ] Create wrapper in `Odyssey.Engines.Odyssey` if needed
- [ ] Future: Create engine-agnostic abstraction when other engines are implemented

### Phase 3: Module Class
- [ ] Move `Module.cs` to `Odyssey.Engines.Odyssey.Module`
- [ ] Move `ModuleDataLoader.cs` to `Odyssey.Engines.Odyssey.Module`
- [ ] Update all references
- [ ] Verify compilation

### Phase 4: GFF Templates
- [ ] Move entity templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW) to `Odyssey.Engines.Odyssey.Templates`
- [ ] Move template helpers to `Odyssey.Engines.Odyssey.Templates`
- [ ] Review module/area structures (IFO, ARE, GIT, JRL, PTH) - move if KOTOR-specific
- [ ] Review dialogue structures (DLG) - move if KOTOR-specific
- [ ] Review GUI structures - move if KOTOR-specific
- [ ] Update all references
- [ ] Verify compilation

### Phase 5: Installation Class (Future)
- [ ] Create `IEngineInstallation` interface in `AuroraEngine.Common`
- [ ] Move KOTOR-specific `Installation` to `Odyssey.Engines.Odyssey`
- [ ] Update patcher tools to use new structure
- [ ] Create Aurora/Eclipse installation classes when those engines are implemented

## File Organization

### AuroraEngine.Common (Keep Engine-Agnostic)
- ✅ Formats/** - All file format parsers
- ✅ Installation/** - Installation detection (currently KOTOR-specific, but structure allows expansion)
- ✅ Resources/** - Resource management
- ✅ Logger/** - Logging infrastructure
- ✅ Utility/** - Utility classes
- ⚠️ Common/Game.cs - KOTOR-specific, but kept for patcher tools compatibility
- ⚠️ Common/GameObject.cs - Engine-agnostic (ObjectType is shared)
- ⚠️ Common/ResRef.cs - Engine-agnostic

### Odyssey.Engines.Odyssey (KOTOR-Specific Runtime)
- ✅ OdysseyEngine.cs
- ✅ OdysseyGameSession.cs
- ✅ OdysseyModuleLoader.cs
- ✅ Profiles/OdysseyK1GameProfile.cs
- ✅ Profiles/OdysseyK2GameProfile.cs
- ✅ EngineApi/OdysseyK1EngineApi.cs
- ✅ EngineApi/OdysseyK2EngineApi.cs
- [ ] Common/Game.cs - Wrapper for AuroraEngine.Common.Game
- [ ] Module/Module.cs - Moved from AuroraEngine.Common
- [ ] Module/ModuleDataLoader.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTC.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTD.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTE.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTI.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTM.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTP.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTS.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTT.cs - Moved from AuroraEngine.Common
- [ ] Templates/UTW.cs - Moved from AuroraEngine.Common

## Dependencies

### Current
- `Odyssey.Engines.Odyssey` → `AuroraEngine.Common` ✅
- `AuroraEngine.Common` → (no engine dependencies) ✅

### Future (When Other Engines Are Implemented)
- `AuroraEngine.Common` → `IEngineInstallation` interface (engine-agnostic)
- `Odyssey.Engines.Odyssey` → `OdysseyInstallation` (KOTOR-specific implementation)
- `Odyssey.Engines.Aurora` → `AuroraInstallation` (NWN/NWN2-specific implementation)
- `Odyssey.Engines.Eclipse` → `EclipseInstallation` (Dragon Age/Mass Effect-specific implementation)

## Notes

- Follow xoreos's pattern but avoid their design issues
- Maximize code in base classes
- Minimize duplication
- Ensure C# 7.3 compatibility
- Maintain 1:1 parity with original KOTOR 2 engine (Ghidra verification)
- Consider Andastra, HolocronToolset, NCSDecomp, and KotorDiff dependencies
- `AuroraEngine.Common` is currently KOTOR-specific, but the structure allows for future expansion to other engines

