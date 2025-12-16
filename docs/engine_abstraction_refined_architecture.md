# Engine Abstraction Refined Architecture

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Purpose**: Comprehensive architecture plan for multi-engine abstraction following xoreos pattern with maximum code in base classes

## Executive Summary

Refactor Andastra to support multiple BioWare engine families (specifically: Odyssey, Aurora, Eclipse, Infinity) through a comprehensive abstraction layer. Follow xoreos's proven modular pattern but with cleaner design, maximizing code reuse through base classes and ensuring 1:1 parity with original KOTOR 2 engine (Ghidra-verified).

## Architecture Pattern (Following xoreos, but cleaner)

### xoreos Structure Analysis

xoreos uses this hierarchy:

```sh
Engine (base engine.h)
├── kotorbase/ (shared KOTOR 1/2 code)
│   ├── kotor/ (KotOR 1 specific)
│   └── kotor2/ (KotOR 2 specific)
├── aurora/ (shared Aurora utilities)
│   ├── nwn/ (NeverWinter Nights)
│   └── nwn2/ (NeverWinter Nights 2)
├── eclipse/ (Eclipse engine base)
│   ├── dragonage/ (Dragon Age)
│   └── dragonage2/ (Dragon Age 2)
└── jade/ (Jade Empire)
```

### Our Refined Structure

```sh
AuroraEngine.Common (pure file formats, installation, resource management)
    ↑
Odyssey.Engines.Common (base engine abstraction - like xoreos's Engine)
    ↑
├── Odyssey.Engines.Odyssey (KOTOR 1/2 shared - like xoreos's kotorbase)
│   ├── Common/ (Game enum, shared utilities)
│   ├── Module/ (Module class, KModuleType, ModuleDataLoader)
│   ├── Templates/ (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM)
│   ├── Profiles/ (OdysseyK1GameProfile, OdysseyK2GameProfile)
│   ├── EngineApi/ (OdysseyK1EngineApi, OdysseyK2EngineApi)
│   ├── OdysseyEngine.cs (engine implementation)
│   ├── OdysseyGameSession.cs (game session)
│   └── OdysseyModuleLoader.cs (module loader)
│
├── Odyssey.Engines.Aurora (NWN/NWN2 base - like xoreos's aurora)
│   ├── Common/ (Aurora-specific utilities)
│   ├── Module/ (Aurora module structures)
│   ├── Profiles/ (Aurora game profiles)
│   ├── EngineApi/ (Aurora engine API)
│   ├── AuroraEngine.cs
│   ├── AuroraGameSession.cs
│   └── AuroraModuleLoader.cs
│
└── Odyssey.Engines.Eclipse (Dragon Age/Mass Effect base - like xoreos's eclipse)
    ├── Common/ (Eclipse-specific utilities)
    ├── Module/ (Eclipse module structures)
    ├── Profiles/ (Eclipse game profiles)
    ├── EngineApi/ (Eclipse engine API)
    ├── EclipseEngine.cs
    ├── EclipseGameSession.cs
    └── EclipseModuleLoader.cs
```

## Key Design Principles

### 1. Maximum Code in Base Classes

**Principle**: All shared logic goes in `Odyssey.Engines.Common` base classes. Engine-specific projects should only contain what differs.

**Implementation**:

- `BaseEngine` - All common engine initialization, shutdown, resource management
- `BaseEngineGame` - All common game session logic (module loading, world management, update loop)
- `BaseEngineModule` - All common module loading logic (IFO parsing, area loading, entity spawning)
- `BaseEngineProfile` - All common profile logic (resource config, table config, feature detection)

**Example**: Module loading logic should be 90% in `BaseEngineModule`, with only KOTOR-specific file formats in `OdysseyModuleLoader`.

### 2. AuroraEngine.Common Must Be Engine-Agnostic

**Keep in AuroraEngine.Common**:

- ✅ File format parsers (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM)
- ✅ Installation detection (generic structure, engine-specific detection via plugins)
- ✅ Resource management (resource loading, precedence, caching)
- ✅ Utility classes (ResRef, LocalizedString, BinaryReader/Writer)

**Move from AuroraEngine.Common**:

- ❌ Game enum (K1, K2, etc.) → `Odyssey.Engines.Odyssey.Common`
- ❌ KModuleType enum → `Odyssey.Engines.Odyssey.Module`
- ❌ Module class → `Odyssey.Engines.Odyssey.Module`
- ❌ GFF templates (UTC, UTD, etc.) → `Odyssey.Engines.Odyssey.Templates`
- ❌ Module/Area structures (IFO, ARE, GIT, JRL, PTH) if KOTOR-specific

**Exception for Patcher Tools**:

- Keep deprecated aliases in `AuroraEngine.Common` for backward compatibility
- Patcher tools (Andastra, HolocronToolset, NCSDecomp, KotorDiff) can continue using old namespaces
- Document as DEPRECATED, encourage migration to new namespaces

### 3. Engine Family Base Classes (Like xoreos's kotorbase)

**Odyssey.Engines.Odyssey** (like xoreos's `kotorbase`):

- Contains ALL shared KOTOR 1/2 code
- Game enum, Module class, GFF templates
- Shared game logic, combat systems, dialogue systems
- Profiles and EngineApi for both K1 and K2

**Odyssey.Engines.Aurora** (like xoreos's `aurora`):

- Contains ALL shared NWN/NWN2 code
- Aurora-specific structures (tilesets, etc.)
- Shared Aurora game logic

**Odyssey.Engines.Eclipse** (like xoreos's `eclipse`):

- Contains ALL shared Dragon Age/Mass Effect code
- Eclipse-specific structures
- Shared Eclipse game logic

### 4. Game-Specific Layers (Optional, Use Profiles When Possible)

**Preference**: Use profiles (`IEngineProfile`) instead of separate projects when possible.

**Only create separate projects if**:

- Significant code differences (>50% different)
- Different file formats
- Different game loop structures

**For KOTOR 1/2**: Use profiles (`OdysseyK1GameProfile`, `OdysseyK2GameProfile`) instead of separate projects, as they share 95% of code.

## Detailed Component Breakdown

### AuroraEngine.Common

**Purpose**: Pure file formats and resource management, engine-agnostic.

**Contents**:

```sh
AuroraEngine.Common/
├── Formats/ (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM, etc.)
├── Installation/ (Installation detection - generic structure)
├── Resources/ (Resource management, loading, caching)
├── Common/ (ResRef, LocalizedString, BinaryReader/Writer, etc.)
├── Logger/ (Logging infrastructure)
└── Utility/ (Utility classes)
```

**Key Files**:

- `Formats/GFF/GFF.cs` - Generic GFF parser (engine-agnostic)
- `Formats/TwoDA/TwoDA.cs` - Generic 2DA parser
- `Formats/TLK/TLK.cs` - Generic TLK parser
- `Installation/Installation.cs` - Generic installation detection (with engine-specific plugins)
- `Resources/ResourceType.cs` - Resource type enumeration

**Deprecated (for patcher tools)**:

- `Resource/Generics/UTC.cs` → Alias to `Odyssey.Engines.Odyssey.Templates.UTC`
- `Resource/Generics/UTD.cs` → Alias to `Odyssey.Engines.Odyssey.Templates.UTD`
- (All other GFF templates)

### Odyssey.Engines.Common

**Purpose**: Base engine abstraction, maximum shared code.

**Contents**:

```sh
Odyssey.Engines.Common/
├── IEngine.cs (interface)
├── IEngineGame.cs (interface)
├── IEngineModule.cs (interface)
├── IEngineProfile.cs (interface)
├── BaseEngine.cs (abstract base - MAXIMIZE CODE HERE)
├── BaseEngineGame.cs (abstract base - MAXIMIZE CODE HERE)
├── BaseEngineModule.cs (abstract base - MAXIMIZE CODE HERE)
└── BaseEngineProfile.cs (abstract base - MAXIMIZE CODE HERE)
```

**Key Responsibilities**:

- **BaseEngine**: Engine initialization, shutdown, resource provider creation, world creation, engine API creation
- **BaseEngineGame**: Game session lifecycle, module loading coordination, world updates, entity management
- **BaseEngineModule**: Module loading logic, IFO parsing, area loading, entity spawning, navigation mesh loading
- **BaseEngineProfile**: Resource configuration, table configuration, feature detection

**Maximization Strategy**:

- Move ALL common logic from engine-specific classes to base classes
- Engine-specific classes should only override what differs
- Use template method pattern for extensibility

### Odyssey.Engines.Odyssey

**Purpose**: KOTOR 1/2 shared code (like xoreos's `kotorbase`).

**Contents**:

```sh
Odyssey.Engines.Odyssey/
├── Common/
│   ├── Game.cs (Game enum: K1, K2, etc.)
│   └── GameExtensions.cs
├── Module/
│   ├── Module.cs (KModuleType enum, Module class)
│   ├── ModuleDataLoader.cs
│   └── ModuleHelpers.cs
├── Templates/ (All 9 GFF templates - ✅ COMPLETED)
│   ├── UTC.cs, UTCHelpers.cs
│   ├── UTD.cs, UTDHelpers.cs
│   ├── UTE.cs, UTEHelpers.cs
│   ├── UTI.cs, UTIHelpers.cs
│   ├── UTP.cs, UTPHelpers.cs
│   ├── UTS.cs, UTSHelpers.cs
│   ├── UTT.cs, UTTHelpers.cs
│   ├── UTW.cs, UTWHelpers.cs
│   └── UTM.cs, UTMHelpers.cs
├── Profiles/
│   ├── OdysseyK1GameProfile.cs
│   └── OdysseyK2GameProfile.cs
├── EngineApi/
│   ├── OdysseyK1EngineApi.cs
│   └── OdysseyK2EngineApi.cs
├── OdysseyEngine.cs (inherits BaseEngine)
├── OdysseyGameSession.cs (inherits BaseEngineGame)
└── OdysseyModuleLoader.cs (inherits BaseEngineModule)
```

**Key Responsibilities**:

- **OdysseyEngine**: KOTOR-specific resource provider creation (uses `AuroraEngine.Common.Installation`)
- **OdysseyGameSession**: KOTOR-specific game logic (combat, dialogue, etc.)
- **OdysseyModuleLoader**: KOTOR-specific module loading (MOD, ARE, GIT, etc.)

### Odyssey.Engines.Aurora (Future)

**Purpose**: NWN/NWN2 shared code (like xoreos's `aurora`).

**Contents**:

```sh
Odyssey.Engines.Aurora/
├── Common/ (Aurora-specific utilities)
├── Module/ (Aurora module structures - tilesets, etc.)
├── Profiles/ (Aurora game profiles)
├── EngineApi/ (Aurora engine API)
├── AuroraEngine.cs (inherits BaseEngine)
├── AuroraGameSession.cs (inherits BaseEngineGame)
└── AuroraModuleLoader.cs (inherits BaseEngineModule)
```

### Odyssey.Engines.Eclipse (Future)

**Purpose**: Dragon Age/Mass Effect shared code (like xoreos's `eclipse`).

**Contents**:

```sh
Odyssey.Engines.Eclipse/
├── Common/ (Eclipse-specific utilities)
├── Module/ (Eclipse module structures)
├── Profiles/ (Eclipse game profiles)
├── EngineApi/ (Eclipse engine API)
├── EclipseEngine.cs (inherits BaseEngine)
├── EclipseGameSession.cs (inherits BaseEngineGame)
└── EclipseModuleLoader.cs (inherits BaseEngineModule)
```

## Migration Strategy

### Phase 1: Base Class Maximization (CURRENT)

**Goal**: Move maximum code to `Odyssey.Engines.Common` base classes.

**Tasks**:

- [ ] Review `BaseEngine` - move all common initialization logic
- [ ] Review `BaseEngineGame` - move all common game session logic
- [ ] Review `BaseEngineModule` - move all common module loading logic
- [ ] Review `BaseEngineProfile` - move all common profile logic
- [ ] Ensure engine-specific classes only override what differs

### Phase 2: KOTOR-Specific Code Migration

**Goal**: Move all KOTOR-specific code from `AuroraEngine.Common` to `Odyssey.Engines.Odyssey`.

**Tasks**:

- [x] Move GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM) ✅
- [ ] Move Game enum and extensions
- [ ] Move Module class and KModuleType enum
- [ ] Move ModuleDataLoader
- [ ] Review and move IFO, ARE, GIT, JRL, PTH if KOTOR-specific
- [ ] Review and move DLG structures if KOTOR-specific
- [ ] Review and move GUI structures if KOTOR-specific
- [ ] Create backward compatibility aliases for patcher tools

### Phase 3: Patcher Tool Compatibility

**Goal**: Ensure patcher tools continue working with deprecated namespaces.

**Tasks**:

- [ ] Create type aliases in `AuroraEngine.Common` for moved classes
- [ ] Document deprecated namespaces
- [ ] Create migration guide for patcher tools
- [ ] Test HoloPatcher.NET, HolocronToolset, NCSDecomp, KotorDiff

### Phase 4: Aurora Engine Implementation (Future)

**Goal**: Implement Aurora engine support.

**Tasks**:

- [ ] Create `Odyssey.Engines.Aurora` project
- [ ] Implement Aurora-specific structures
- [ ] Implement Aurora game profiles
- [ ] Implement Aurora engine API

### Phase 5: Eclipse Engine Implementation (Future)

**Goal**: Implement Eclipse engine support.

**Tasks**:

- [ ] Create `Odyssey.Engines.Eclipse` project
- [ ] Implement Eclipse-specific structures
- [ ] Implement Eclipse game profiles
- [ ] Implement Eclipse engine API

## Patcher Tool Compatibility Strategy

### Problem

Patcher tools (HoloPatcher.NET, HolocronToolset, NCSDecomp, KotorDiff) directly reference `AuroraEngine.Common` and expect:

- `AuroraEngine.Common.Game` enum
- `AuroraEngine.Common.Resource.Generics.UTC` (and other templates)
- `AuroraEngine.Common.Common.Module` class

### Solution: Backward Compatibility Aliases

**Approach**: Keep deprecated type aliases in `AuroraEngine.Common` that forward to new locations.

**Example**:

```csharp
// AuroraEngine.Common/Resource/Generics/UTC.cs (DEPRECATED)
namespace AuroraEngine.Common.Resource.Generics
{
    // DEPRECATED: Use Odyssey.Engines.Odyssey.Templates.UTC instead
    using UTC = Odyssey.Engines.Odyssey.Templates.UTC;
    using UTCClass = Odyssey.Engines.Odyssey.Templates.UTCClass;
}
```

**For Classes (C# 7.3 limitation)**:
Since C# 7.3 doesn't support type aliases for classes, we need forwarding implementations:

```csharp
// AuroraEngine.Common/Resource/Generics/UTC.cs (DEPRECATED)
namespace AuroraEngine.Common.Resource.Generics
{
    // DEPRECATED: This class forwards to Odyssey.Engines.Odyssey.Templates.UTC
    // Patcher tools can continue using this namespace
    public sealed class UTC
    {
        private readonly Odyssey.Engines.Odyssey.Templates.UTC _impl;
        
        // Forward all properties and methods to _impl
        // ...
    }
}
```

**Better Approach**: Keep original class definition but mark as DEPRECATED, add using alias for new namespace.

## 1:1 Parity with KOTOR 2 Engine

### Ghidra Integration

**Requirement**: All KOTOR 2 implementations must achieve 1:1 parity with original engine (verified via Ghidra).

**Process**:

1. Search Ghidra for relevant functions using string searches
2. Decompile functions to understand original implementation
3. Add detailed comments with Ghidra function addresses
4. Match original behavior exactly
5. Document any deviations or improvements

**Example Comment Format**:

```csharp
/// <summary>
/// Loads a module by name.
/// </summary>
/// <remarks>
/// Based on swkotor2.exe: FUN_006caab0 @ 0x006caab0
/// Located via string reference: "ModuleLoaded" @ 0x007bdd70
/// Original implementation: Sets module state flags, loads IFO, ARE, GIT files
/// </remarks>
```

## File Migration Checklist

### Completed ✅

- [x] GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM) → `Odyssey.Engines.Odyssey.Templates`
- [x] Profiles (OdysseyK1GameProfile, OdysseyK2GameProfile) → `Odyssey.Engines.Odyssey.Profiles`
- [x] EngineApi (OdysseyK1EngineApi, OdysseyK2EngineApi) → `Odyssey.Engines.Odyssey.EngineApi`
- [x] Base engine abstraction → `Odyssey.Engines.Common`

### In Progress [/]

- [/] Game enum → `Odyssey.Engines.Odyssey.Common` (deferred due to patcher tools)
- [/] Module class → `Odyssey.Engines.Odyssey.Module` (deferred due to patcher tools)
- [/] Base class maximization (reviewing common logic)

### Pending [ ]

- [ ] ModuleDataLoader → `Odyssey.Engines.Odyssey.Module`
- [ ] IFO, ARE, GIT, JRL, PTH structures (review if KOTOR-specific)
- [ ] DLG structures (review if KOTOR-specific)
- [ ] GUI structures (review if KOTOR-specific)
- [ ] Backward compatibility aliases for patcher tools
- [ ] Aurora engine implementation
- [ ] Eclipse engine implementation

## Notes

- Follow xoreos's proven pattern but avoid their design issues
- Maximize code in base classes (90%+ in base, <10% in engine-specific)
- Minimize duplication across engine families
- Ensure C# 7.3 compatibility
- Maintain 1:1 parity with original KOTOR 2 engine (Ghidra verification)
- Consider patcher tool dependencies (backward compatibility)
- Document all Ghidra-derived implementations
