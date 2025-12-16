# Engine Abstraction Plan - Following xoreos Architecture

**Status**: PLANNING  
**Date**: 2025-12-16  
**Reference**: `vendor/PyKotor/vendor/xoreos/`  
**Goal**: Abstract CSharpKOTOR to support multiple BioWare engines (Aurora, Eclipse, Infinity, Odyssey) with maximum code in base classes, following xoreos canonical structure.

## xoreos Architecture Analysis

### Directory Structure

```sh
xoreos/src/
├── aurora/                    # Engine-agnostic file format parsers
│   ├── 2dafile.cpp/h         # 2DA parser
│   ├── gff3file.cpp/h        # GFF parser
│   ├── talktable.cpp/h       # TLK parser
│   ├── erffile.cpp/h         # ERF parser
│   ├── keyfile.cpp/h         # KEY parser
│   └── ...                    # Other format parsers
│
├── engines/
│   ├── engine.h/cpp           # Base engine interface (like our BioWareEngines.Common)
│   ├── kotorbase/             # Shared KOTOR code (like our BioWareEngines.Odyssey)
│   │   ├── engine.h/cpp       # KotOREngine : public Engine
│   │   ├── game.h/cpp         # Game session
│   │   ├── module.h/cpp       # Module loader
│   │   ├── area.h/cpp         # Area management
│   │   ├── object.h/cpp       # GameObject base
│   │   ├── creature.h/cpp     # Creature entity
│   │   ├── door.h/cpp         # Door entity
│   │   ├── placeable.h/cpp    # Placeable entity
│   │   ├── item.h/cpp         # Item entity
│   │   ├── trigger.h/cpp      # Trigger entity
│   │   ├── waypoint.h/cpp     # Waypoint entity
│   │   ├── action.h/cpp       # Action system
│   │   ├── actionqueue.h/cpp  # Action queue
│   │   ├── round.h/cpp        # Combat rounds
│   │   ├── savedgame.h/cpp    # Save/load
│   │   ├── script/            # NWScript API functions
│   │   │   ├── functions.h/cpp
│   │   │   ├── functions_action.cpp
│   │   │   ├── functions_creatures.cpp
│   │   │   ├── functions_events.cpp
│   │   │   ├── functions_global.cpp
│   │   │   ├── functions_local.cpp
│   │   │   ├── functions_math.cpp
│   │   │   ├── functions_module.cpp
│   │   │   ├── functions_object.cpp
│   │   │   ├── functions_party.cpp
│   │   │   ├── functions_situated.cpp
│   │   │   ├── functions_sound.cpp
│   │   │   ├── functions_string.cpp
│   │   │   └── functions_time.cpp
│   │   └── path/              # Pathfinding/walkmesh
│   │       ├── walkmeshloader.h/cpp
│   │       ├── pathfinding.h/cpp
│   │       └── ...
│   │
│   ├── kotor/                 # KOTOR 1 specific
│   │   ├── engine.h/cpp       # KOTOREngine : public KotORBase::KotOREngine
│   │   └── ...
│   │
│   ├── kotor2/                # KOTOR 2 specific
│   │   ├── engine.h/cpp       # KOTOR2Engine : public KotORBase::KotOREngine
│   │   └── ...
│   │
│   ├── aurora/                # Aurora engine base (NWN/NWN2)
│   │   └── ...
│   │
│   ├── nwn/                   # Neverwinter Nights
│   │   └── ...
│   │
│   ├── nwn2/                  # Neverwinter Nights 2
│   │   └── ...
│   │
│   ├── dragonage/             # Dragon Age: Origins
│   │   └── ...
│   │
│   ├── dragonage2/            # Dragon Age II
│   │   └── ...
│   │
│   ├── eclipse/               # Eclipse engine base
│   │   └── ...
│   │
│   └── jade/                  # Jade Empire
│       └── ...
│
└── common/                    # Common utilities (not engine-specific)
    └── ...
```

### Key Patterns

1. **Base Engine Interface** (`engines/engine.h`):
   - `class Engine` - Abstract base for all engines
   - Virtual methods: `run()`, `detectLanguages()`, `getLanguage()`, `changeLanguage()`
   - Common functionality: console, FPS display, language management

2. **Shared Engine Base** (`engines/kotorbase/`):
   - `class KotOREngine : public Engine` - Base for KOTOR 1/2
   - Shared KOTOR logic: Game, Module, Area, Objects, Actions, Scripts, Pathfinding
   - All common KOTOR functionality goes here

3. **Game-Specific Engines** (`engines/kotor/`, `engines/kotor2/`):
   - `class KOTOREngine : public KotORBase::KotOREngine` - KOTOR 1
   - `class KOTOR2Engine : public KotORBase::KotOREngine` - KOTOR 2
   - Only game-specific differences

4. **File Format Parsers** (`aurora/`):
   - Engine-agnostic parsers (GFF, 2DA, TLK, ERF, KEY, etc.)
   - Used by all engines

## Target Architecture for Andastra

### Current Structure (Before Refactoring)

```sh
src/
├── CSharpKOTOR/               # File format parsers (engine-agnostic)
│   ├── Formats/               # GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM
│   ├── Installation/         # Installation detection (KOTOR-specific currently)
│   └── Resource/Generics/    # GFF templates (UTC, UTD, etc.) - KOTOR-specific
│
└── OdysseyRuntime/            # Runtime engine (KOTOR-specific)
    ├── Odyssey.Core/          # Domain logic
    ├── Odyssey.Content/       # Asset pipeline
    ├── Odyssey.Scripting/     # NCS VM + NWScript API
    ├── Odyssey.Kotor/        # KOTOR-specific game rules
    ├── Odyssey.Engines.Common/ # Base engine abstraction
    ├── Odyssey.Engines.Odyssey/ # KOTOR engine
    ├── Odyssey.Engines.Aurora/  # Aurora engine (placeholder)
    ├── Odyssey.Engines.Eclipse/ # Eclipse engine (placeholder)
    ├── Odyssey.Graphics/      # Graphics abstractions
    ├── Odyssey.MonoGame/      # MonoGame adapters
    └── Odyssey.Game/          # Executable launcher
```

### Target Structure (After Refactoring)

```sh
src/
├── CSharpKOTOR/               # Engine-agnostic file format parsers (like xoreos's aurora/)
│   ├── Formats/               # GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM
│   ├── Installation/          # Installation detection (will be abstracted)
│   └── Resources/             # Resource management (engine-agnostic)
│
└── BioWareEngines/            # Runtime engines (like xoreos's engines/)
    ├── Common/                # Base engine abstraction (like xoreos's engines/engine.h)
    │   ├── IEngine.cs         # Base engine interface
    │   ├── BaseEngine.cs      # Base engine implementation
    │   ├── IEngineGame.cs     # Game session interface
    │   ├── BaseEngineGame.cs  # Base game session
    │   ├── IEngineModule.cs   # Module interface
    │   ├── BaseEngineModule.cs # Base module loader
    │   ├── IEngineProfile.cs  # Engine profile interface
    │   └── BaseEngineProfile.cs # Base profile
    │
    ├── Odyssey/               # Shared KOTOR code (like xoreos's kotorbase/)
    │   ├── OdysseyEngine.cs   # OdysseyEngine : BaseEngine
    │   ├── OdysseyGameSession.cs # Game session
    │   ├── OdysseyModuleLoader.cs # Module loader
    │   ├── Templates/         # All 9 GFF templates (UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM) ✅
    │   ├── Profiles/          # OdysseyK1GameProfile, OdysseyK2GameProfile ✅
    │   ├── EngineApi/         # OdysseyK1EngineApi, OdysseyK2EngineApi ✅
    │   └── Module/            # KOTOR-specific module structures (Module.cs, etc.)
    │
    ├── Aurora/                 # Aurora engine (NWN/NWN2) - like xoreos's aurora base
    │   └── AuroraEngine.cs    # AuroraEngine : BaseEngine
    │
    ├── Eclipse/               # Eclipse engine (Dragon Age/Mass Effect)
    │   └── EclipseEngine.cs   # EclipseEngine : BaseEngine
    │
    ├── Infinity/              # Infinity engine (Baldur's Gate/Icewind Dale) - future
    │   └── InfinityEngine.cs # InfinityEngine : BaseEngine
    │
    ├── Core/                  # Shared domain logic (no engine-specific code)
    │   ├── Entities/          # Entity, World, EventBus
    │   ├── Actions/           # Action system
    │   ├── Combat/            # Combat system
    │   ├── Dialogue/         # Dialogue system
    │   ├── Navigation/       # Navigation mesh
    │   └── ...
    │
    ├── Content/               # Asset pipeline
    ├── Scripting/             # NCS VM + NWScript API (engine-agnostic)
    ├── Kotor/                # KOTOR-specific game rules (combat, stats, etc.)
    ├── Graphics/              # Graphics abstractions
    ├── Graphics.Common/      # Shared graphics base
    ├── MonoGame/             # MonoGame rendering adapters
    ├── Stride/               # Stride rendering adapters
    └── Game/                 # Executable launcher
```

## Abstraction Strategy

### Phase 1: Rename and Restructure (Current)

1. ✅ Rename `OdysseyRuntime` → `BioWareEngines`
2. ✅ Rename `Odyssey.*` namespaces → `BioWareEngines.*`
3. ⏳ Fix build errors and project references
4. ⏳ Update solution file

### Phase 2: Abstract CSharpKOTOR (Next)

**Goal**: Identify and abstract KOTOR-specific code from CSharpKOTOR to BioWareEngines.Odyssey

#### Files to Review in CSharpKOTOR

1. **Module.cs** (`Common/Module.cs`):
   - Contains `KModuleType` enum (`.rim`, `_s.rim`, `_dlg.erf`, `.mod`) - KOTOR-specific
   - **Action**: Move to `BioWareEngines.Odyssey.Module.Module.cs`
   - **Keep in CSharpKOTOR**: Deprecated copy for patcher tools compatibility

2. **ModuleDataLoader.cs** (`Common/ModuleDataLoader.cs`):
   - KOTOR-specific module data loading
   - **Action**: Move to `BioWareEngines.Odyssey.Module.ModuleDataLoader.cs`

3. **GFF Templates** (`Resource/Generics/UTC.cs`, etc.):
   - ✅ Already migrated to `BioWareEngines.Odyssey.Templates`
   - **Status**: Complete, kept in CSharpKOTOR for compatibility

4. **IFO, ARE, GIT, JRL, PTH** (`Resource/Generics/`):
   - **Review**: Check if KOTOR-specific or shared across engines
   - **Decision**: If KOTOR-specific → Move to `BioWareEngines.Odyssey`
   - **Decision**: If shared → Keep in CSharpKOTOR

5. **DLG, GUI** (`Resource/Generics/DLG/`, `Resource/Generics/GUI/`):
   - **Review**: Check if KOTOR-specific or shared
   - **Decision**: Based on xoreos analysis

6. **Installation.cs** (`Installation/Installation.cs`):
   - Currently KOTOR-specific (checks for `swkotor.exe`, `swkotor2.exe`)
   - **Action**: Create `IEngineInstallation` interface in `BioWareEngines.Common`
   - **Action**: Keep `Installation.cs` in CSharpKOTOR for patcher tools
   - **Action**: Create `OdysseyInstallation` in `BioWareEngines.Odyssey` implementing `IEngineInstallation`

### Phase 3: Maximize Base Classes

**Goal**: Move all shared logic to `BioWareEngines.Common` base classes

#### BaseEngine (BioWareEngines.Common)

Following xoreos's `engines/engine.h` pattern:

```csharp
namespace BioWareEngines.Common
{
    // Base engine interface
    public interface IEngine
    {
        void Start(GameID game, string target, Platform platform);
        void Run();
        bool DetectLanguages(out List<Language> languages);
        bool GetLanguage(out Language language);
        bool ChangeLanguage();
    }

    // Base engine implementation
    public abstract class BaseEngine : IEngine
    {
        protected GameID _game;
        protected Platform _platform;
        protected string _target;
        protected Console _console;
        protected FPSDisplay _fps;

        public virtual void Start(GameID game, string target, Platform platform) { }
        public abstract void Run(); // Must be implemented by derived engines
        public virtual bool DetectLanguages(out List<Language> languages) { }
        public virtual bool GetLanguage(out Language language) { }
        public virtual bool ChangeLanguage() { }
    }
}
```

#### BaseEngineGame (BioWareEngines.Common)

Following xoreos's `kotorbase/game.h` pattern:

```csharp
namespace BioWareEngines.Common
{
    public interface IEngineGame
    {
        void LoadModule(string moduleName);
        void UnloadModule();
        IArea GetCurrentArea();
        IEntity GetPlayer();
    }

    public abstract class BaseEngineGame : IEngineGame
    {
        protected IModule _currentModule;
        protected IArea _currentArea;
        protected IEntity _player;

        public virtual void LoadModule(string moduleName) { }
        public virtual void UnloadModule() { }
        public virtual IArea GetCurrentArea() { }
        public virtual IEntity GetPlayer() { }
    }
}
```

#### OdysseyEngine (BioWareEngines.Odyssey)

Following xoreos's `kotorbase/engine.h` pattern:

```csharp
namespace BioWareEngines.Odyssey
{
    public class OdysseyEngine : BaseEngine
    {
        private OdysseyGameSession _game;

        public OdysseyGameSession GetGame() => _game;

        public override void Run()
        {
            // KOTOR-specific game loop
        }
    }
}
```

### Phase 4: Review File Formats

**Goal**: Determine which file formats are engine-agnostic vs engine-specific

#### Engine-Agnostic (Keep in CSharpKOTOR)

- **GFF** - Used by all engines (Aurora, Odyssey, Eclipse)
- **2DA** - Used by all engines
- **TLK** - Used by all engines
- **ERF** - Used by all engines
- **KEY/BIF** - Used by all engines
- **MDL/MDX** - Used by all engines (with variations)
- **TPC** - Used by all engines
- **LYT/VIS** - Used by all engines (with variations)

#### KOTOR/Odyssey-Specific (Move to BioWareEngines.Odyssey)

- **UTC, UTD, UTE, UTI, UTP, UTS, UTT, UTW, UTM** - ✅ Already moved
- **IFO** - KOTOR module info (check if Aurora uses similar)
- **ARE** - KOTOR area structure (check if Aurora uses similar)
- **GIT** - KOTOR instance template (check if Aurora uses similar)
- **JRL** - KOTOR journal (check if Aurora uses similar)
- **PTH** - KOTOR pathfinding (check if Aurora uses similar)
- **DLG** - KOTOR dialogue (check if Aurora uses similar)
- **GUI** - KOTOR GUI format (check if Aurora uses similar)

**Action**: Review xoreos source to determine which formats are shared vs KOTOR-specific.

## Implementation Order

1. **Rename Phase** (Current):
   - ✅ Execute `EngineNamespaceRenamer.ps1` to rename `OdysseyRuntime` → `BioWareEngines`
   - ✅ Rename all `Odyssey.*` namespaces → `BioWareEngines.*`
   - ⏳ Fix build errors
   - ⏳ Update solution file references

2. **Abstraction Phase**:
   - Review CSharpKOTOR files for KOTOR-specific code
   - Move KOTOR-specific code to `BioWareEngines.Odyssey`
   - Create base classes in `BioWareEngines.Common`
   - Update references

3. **Base Class Maximization**:
   - Move shared logic to `BaseEngine`, `BaseEngineGame`, `BaseEngineModule`
   - Minimize code in engine-specific projects
   - Follow xoreos pattern: base classes contain all common logic

4. **Ghidra Documentation**:
   - For all KOTOR-specific code, add Ghidra function addresses
   - Document original engine behavior
   - Ensure 1:1 parity with original engine

## Key Principles

1. **Maximum Code in Base Classes**: All common logic goes in `BioWareEngines.Common` base classes
2. **Engine-Specific = Minimal**: Engine-specific projects should only contain what differs
3. **File Formats = Engine-Agnostic**: Keep format parsers in CSharpKOTOR unless proven engine-specific
4. **xoreos as Reference**: Follow xoreos architecture patterns but adapt to C#/.NET idioms
5. **1:1 Parity for KOTOR2**: Use Ghidra to ensure exact match with original engine behavior

## Files Requiring Review

See `docs/odyssey_runtime_ghidra_refactoring_roadmap.md` for comprehensive file list (671 files total).

## Next Steps

1. Fix build errors from renaming
2. Review CSharpKOTOR files systematically
3. Move KOTOR-specific code to BioWareEngines.Odyssey
4. Maximize base class implementations
5. Add Ghidra documentation
