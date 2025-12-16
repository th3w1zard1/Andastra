# Engine Abstraction Layer Refactoring Roadmap

**Status**: In Progress  
**Last Updated**: 2025-01-15  
**Purpose**: Internal tracking document - DO NOT COMMIT

## Current Progress Summary

### ✅ Completed
1. **Phase 2**: Created `Odyssey.Engines.Common` with complete base abstraction layer
   - All base interfaces and classes implemented
   - Project compiles successfully
   - C# 7.3 compatibility maintained

2. **Phase 3 (Partial)**: Created `Odyssey.Engines.Odyssey` project structure
   - Project created and configured
   - Started `OdysseyK1GameProfile` implementation (inherits from BaseEngineProfile)
   - Project references configured

### ⏳ In Progress
- Refactoring `Odyssey.Kotor` code to use base classes
- Moving KOTOR-specific implementations to `Odyssey.Engines.Odyssey`

### ✅ Additional Progress
3. **Phase 3 (Continued)**: Odyssey Engine Implementation
   - Created `OdysseyK2GameProfile` (inherits from BaseEngineProfile)
   - Created `OdysseyEngine` class (inherits from BaseEngine)
   - Created `OdysseyGameSession` class (inherits from BaseEngineGame)
   - Project structure in place

4. **Phase 4 & 5**: Created Aurora and Eclipse Engine Projects
   - Created `Odyssey.Engines.Aurora` project structure
   - Created `Odyssey.Engines.Eclipse` project structure
   - Created `AuroraEngine` and `EclipseEngine` base classes
   - Projects configured with proper dependencies

### 📋 Remaining Work
- Complete Odyssey engine refactoring:
  - Move EngineApi classes to Odyssey.Engines.Odyssey
  - Complete OdysseyGameSession implementation (move code from Odyssey.Kotor.Game.GameSession)
  - Refactor ModuleLoader to inherit from BaseEngineModule
  - Move all KOTOR-specific components/systems to Odyssey.Engines.Odyssey
- Implement Aurora engine:
  - Create Aurora game profiles (NWN, NWN2)
  - Implement Aurora-specific resource provider
  - Implement AuroraGameSession
  - Implement Aurora-specific systems
- Implement Eclipse engine:
  - Create Eclipse game profiles (Dragon Age, Mass Effect)
  - Implement Eclipse-specific resource provider
  - Implement EclipseGameSession
  - Implement Eclipse-specific systems
- Rename CSharpKOTOR to AuroraEngine.Common (large find/replace operation)
- Update solution file with new projects
- Update all project references throughout codebase

## Goal

Refactor OdysseyRuntime to support multiple BioWare engine families (Odyssey, Aurora, Eclipse) through a comprehensive abstraction layer. Each engine should be a separate project with minimal code duplication, maximizing base class usage.

## Current State

- `CSharpKOTOR` - Common shared library (file formats, installation, no MonoGame/Stride dependencies)
- `OdysseyRuntime` projects:
  - `Odyssey.Core` - Pure domain, no rendering dependencies
  - `Odyssey.Content` - Asset conversion/caching
  - `Odyssey.Scripting` - NCS VM + NWScript
  - `Odyssey.Kotor` - K1/K2 specific rules
  - `Odyssey.MonoGame` - MonoGame rendering adapters
  - `Odyssey.Stride` - Stride rendering adapters
  - `Odyssey.Graphics` - Abstraction interfaces
  - `Odyssey.Game` - Executable launcher

## Target Architecture

```
AuroraEngine.Common (renamed from CSharpKOTOR)
    ↑
Odyssey.Engines.Common (base engine classes)
    ↑
├── Odyssey.Engines.Odyssey (KOTOR 1/2) - refactored from Odyssey.Kotor
├── Odyssey.Engines.Aurora (NWN, NWN2)
└── Odyssey.Engines.Eclipse (Dragon Age, Mass Effect)
    ↑
Odyssey.Core (domain layer)
    ↑
Odyssey.Content (asset pipeline)
    ↑
Odyssey.Scripting (NCS VM)
    ↑
Odyssey.Graphics (abstraction interfaces)
    ↑
├── Odyssey.MonoGame (MonoGame implementations)
└── Odyssey.Stride (Stride implementations)
    ↑
Odyssey.Game (executable)
```

## Phase 1: Rename CSharpKOTOR ✅

- [x] Create roadmap document
- [ ] Rename `CSharpKOTOR` project to `AuroraEngine.Common`
- [ ] Update namespace from `CSharpKOTOR.*` to `AuroraEngine.Common.*`
- [ ] Update all project references
- [ ] Update solution file
- [ ] Update NuGet package metadata
- [ ] Verify compilation

## Phase 2: Create Engine Abstraction Layer ✅

- [x] Create `Odyssey.Engines.Common` project
- [x] Define base engine interfaces:
  - [x] `IEngine` - Base engine interface
  - [x] `IEngineGame` - Game session interface
  - [x] `IEngineModule` - Module management interface
  - [x] `IEngineProfile` - Game profile interface (refactor from IGameProfile)
- [x] Create base implementations:
  - [x] `BaseEngine` - Abstract base engine class
  - [x] `BaseEngineGame` - Abstract base game class
  - [x] `BaseEngineModule` - Abstract base module class
  - [x] `BaseEngineProfile` - Abstract base profile class
- [ ] Move common engine logic from `Odyssey.Kotor` to base classes
- [x] Ensure C# 7.3 compatibility

## Phase 3: Refactor Odyssey.Kotor to Odyssey.Engines.Odyssey ⏳

- [x] Create `Odyssey.Engines.Odyssey` project
- [x] Create `OdysseyK1GameProfile` (inherit from BaseEngineProfile)
- [ ] Create `OdysseyK2GameProfile` (inherit from BaseEngineProfile)
- [ ] Move KOTOR-specific code from `Odyssey.Kotor`:
  - [ ] `K1EngineApi`, `K2EngineApi` → `OdysseyEngineApi` (move to Odyssey.Engines.Odyssey)
  - [ ] `GameSession` → `OdysseyGameSession` (inherit from BaseEngineGame)
  - [ ] `ModuleLoader` → `OdysseyModuleLoader` (inherit from BaseEngineModule)
  - [ ] `EntityFactory` → `OdysseyEntityFactory`
  - [ ] KOTOR-specific components and systems (move to Odyssey.Engines.Odyssey)
- [ ] Update all references
- [ ] Ensure inheritance from base classes
- [ ] Minimize code duplication

## Phase 4: Create Odyssey.Engines.Aurora

- [ ] Create `Odyssey.Engines.Aurora` project structure
- [ ] Implement Aurora-specific classes:
  - [ ] `AuroraGameProfile` (NWN, NWN2)
  - [ ] `AuroraEngineApi` (NWScript functions)
  - [ ] `AuroraGameSession` (inherit from BaseEngineGame)
  - [ ] `AuroraModuleLoader` (inherit from BaseEngineModule)
  - [ ] `AuroraEntityFactory`
- [ ] Implement NWN-specific features:
  - [ ] Tileset system
  - [ ] NWN-specific walkmesh handling
  - [ ] NWN-specific scripting
- [ ] Implement NWN2-specific features:
  - [ ] NWN2-specific modules
  - [ ] NWN2-specific scripting

## Phase 5: Create Odyssey.Engines.Eclipse

- [ ] Create `Odyssey.Engines.Eclipse` project structure
- [ ] Implement Eclipse-specific classes:
  - [ ] `EclipseGameProfile` (Dragon Age, Mass Effect)
  - [ ] `EclipseEngineApi` (Eclipse scripting)
  - [ ] `EclipseGameSession` (inherit from BaseEngineGame)
  - [ ] `EclipseModuleLoader` (inherit from BaseEngineModule)
  - [ ] `EclipseEntityFactory`
- [ ] Implement Dragon Age-specific features:
  - [ ] Campaign system
  - [ ] Dragon Age-specific scripting
- [ ] Implement Mass Effect-specific features:
  - [ ] Mass Effect-specific modules
  - [ ] Mass Effect-specific scripting

## Phase 6: Update Dependencies and References

- [ ] Update all project references:
  - [ ] `Odyssey.Core` → reference `AuroraEngine.Common`
  - [ ] `Odyssey.Content` → reference `AuroraEngine.Common`
  - [ ] `Odyssey.Scripting` → reference `AuroraEngine.Common`
  - [ ] `Odyssey.Engines.Common` → reference `AuroraEngine.Common`, `Odyssey.Core`
  - [ ] `Odyssey.Engines.Odyssey` → reference `Odyssey.Engines.Common`, `Odyssey.Content`, `Odyssey.Scripting`
  - [ ] `Odyssey.Engines.Aurora` → reference `Odyssey.Engines.Common`, `Odyssey.Content`, `Odyssey.Scripting`
  - [ ] `Odyssey.Engines.Eclipse` → reference `Odyssey.Engines.Common`, `Odyssey.Content`, `Odyssey.Scripting`
  - [ ] `Odyssey.MonoGame` → reference engine projects as needed
  - [ ] `Odyssey.Stride` → reference engine projects as needed
  - [ ] `Odyssey.Game` → reference engine projects
- [ ] Update solution file
- [ ] Update all using statements

## Phase 7: Code Deduplication

- [ ] Identify duplicated code across engines
- [ ] Move common logic to base classes:
  - [ ] Entity creation patterns
  - [ ] Module loading patterns
  - [ ] Area management patterns
  - [ ] Script execution patterns
  - [ ] Save/load patterns
- [ ] Ensure engine-specific code is minimal
- [ ] Verify inheritance hierarchy is correct

## Phase 8: Testing and Verification

- [ ] Compile all projects
- [ ] Fix compilation errors
- [ ] Fix namespace issues
- [ ] Verify no broken references
- [ ] Run existing tests
- [ ] Update tests for new structure

## Design Principles

1. **Minimize Duplication**: All common code in base classes
2. **Maximize Inheritance**: Engine-specific projects inherit from base
3. **Clean Separation**: Each engine is a separate project
4. **C# 7.3 Compatibility**: All code must be C# 7.3 compatible
5. **No External Dependencies**: `AuroraEngine.Common` has no MonoGame/Stride dependencies
6. **Abstraction First**: Interfaces define contracts, base classes provide implementations

## Notes

- Do not reference xoreos directly in code or comments
- Keep abstraction layer generic and extensible
- Ensure backward compatibility where possible
- Follow existing code patterns and conventions

