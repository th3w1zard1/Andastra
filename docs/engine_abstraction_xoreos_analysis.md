# Engine Abstraction: xoreos Pattern Analysis & Implementation Plan

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Purpose**: Deep analysis of xoreos's architecture pattern and comprehensive implementation plan for maximum code reuse

## xoreos Architecture Deep Dive

### xoreos Structure (from source code analysis)

```
src/engines/
├── engine.h (base Engine class)
├── kotorbase/ (113 files - shared KOTOR 1/2 code)
│   ├── game.h/game.cpp (Game class - module management, music, party)
│   ├── module.h/module.cpp (Module class - area management, entities, scripting)
│   ├── object.h/object.cpp (Object base class - all game objects)
│   ├── area.h/area.cpp (Area class - rooms, walkmesh, entities)
│   ├── creature.h/creature.cpp (Creature class - NPCs, PC, stats, combat)
│   ├── door.h/door.cpp (Door class)
│   ├── placeable.h/placeable.cpp (Placeable class)
│   ├── item.h/item.cpp (Item class)
│   ├── trigger.h/trigger.cpp (Trigger class)
│   ├── waypoint.h/waypoint.cpp (Waypoint class)
│   ├── sound.h/sound.cpp (Sound class)
│   ├── situated.h/situated.cpp (Situated base - doors, placeables)
│   ├── objectcontainer.h/objectcontainer.cpp (Object container - manages objects in area)
│   ├── action.h/action.cpp (Action system)
│   ├── actionqueue.h/actionqueue.cpp (Action queue)
│   ├── actionexecutor.h/actionexecutor.cpp (Action executor)
│   ├── partycontroller.h/partycontroller.cpp (Party management)
│   ├── partyleader.h/partyleader.cpp (Party leader management)
│   ├── cameracontroller.h/cameracontroller.cpp (Camera control)
│   ├── inventory.h/inventory.cpp (Inventory system)
│   ├── round.h/round.cpp (Combat rounds)
│   ├── savedgame.h/savedgame.cpp (Save game handling)
│   ├── room.h/room.cpp (Room - area subdivision)
│   ├── location.h/location.cpp (Location - position + orientation)
│   ├── creatureinfo.h/creatureinfo.cpp (Creature info/stats)
│   ├── creaturesearch.h/creaturesearch.cpp (Creature search)
│   ├── path/ (Pathfinding system)
│   │   ├── pathfinding.h/pathfinding.cpp
│   │   ├── walkmeshloader.h/walkmeshloader.cpp
│   │   ├── objectwalkmesh.h/objectwalkmesh.cpp
│   │   └── doorwalkmesh.h/doorwalkmesh.cpp
│   ├── gui/ (GUI components - 17 files)
│   │   ├── ingame.h/ingame.cpp (In-game GUI)
│   │   ├── dialog.h/dialog.cpp (Dialogue GUI)
│   │   ├── partyselection.h/partyselection.cpp (Party selection)
│   │   ├── loadscreen.h/loadscreen.cpp (Loading screen)
│   │   ├── hud.h/hud.cpp (HUD)
│   │   ├── inventoryitem.h/inventoryitem.cpp (Inventory items)
│   │   └── ...
│   └── script/ (NWScript functions - 20+ files)
│       ├── functions.h/functions.cpp (Main function dispatcher)
│       ├── functions_creatures.cpp
│       ├── functions_object.cpp
│       ├── functions_module.cpp
│       ├── functions_party.cpp
│       ├── functions_action.cpp
│       └── ...
├── kotor/ (KOTOR 1 specific - minimal, mostly profiles)
│   ├── game.h/game.cpp (inherits KotORBase::Game)
│   ├── module.h/module.cpp (inherits KotORBase::Module)
│   ├── creature.h/creature.cpp (inherits KotORBase::Creature)
│   └── script/ (K1-specific NWScript functions)
├── kotor2/ (KOTOR 2 specific - minimal, mostly profiles)
│   ├── game.h/game.cpp (inherits KotORBase::Game)
│   ├── module.h/module.cpp (inherits KotORBase::Module)
│   ├── creature.h/creature.cpp (inherits KotORBase::Creature)
│   └── script/ (K2-specific NWScript functions)
├── aurora/ (Shared Aurora utilities - pathfinding, GUI, etc.)
├── nwn/ (Neverwinter Nights)
├── nwn2/ (Neverwinter Nights 2)
├── eclipse/ (Eclipse engine base)
├── dragonage/ (Dragon Age)
└── dragonage2/ (Dragon Age 2)
```

### Key Insights from xoreos

1. **kotorbase contains 95%+ of KOTOR code**: All shared logic (objects, areas, modules, combat, GUI, scripting) is in `kotorbase/`
2. **kotor/kotor2 are minimal**: They only contain game-specific profiles, module lists, and minor differences
3. **Maximum code reuse**: `kotorbase::Game`, `kotorbase::Module`, `kotorbase::Object` contain almost everything
4. **Inheritance hierarchy**: `kotor::Game` inherits `kotorbase::Game`, only overrides `hasModule()` and `run()`

## Our Current Implementation vs. xoreos

### What We Have ✅

```
Odyssey.Engines.Common/
├── IEngine.cs ✅
├── IEngineGame.cs ✅
├── IEngineModule.cs ✅
├── IEngineProfile.cs ✅
├── BaseEngine.cs ✅ (minimal - needs expansion)
├── BaseEngineGame.cs ✅ (minimal - needs expansion)
├── BaseEngineModule.cs ✅ (minimal - needs expansion)
└── BaseEngineProfile.cs ✅

Odyssey.Engines.Odyssey/
├── Templates/ ✅ (All 9 GFF templates)
├── Profiles/ ✅ (K1/K2 profiles)
├── EngineApi/ ✅ (K1/K2 engine API)
├── OdysseyEngine.cs ✅
├── OdysseyGameSession.cs ✅ (minimal - delegates to ModuleLoader)
└── OdysseyModuleLoader.cs ✅ (uses old Odyssey.Kotor.Loading.ModuleLoader)
```

### What We're Missing (Compared to xoreos)

1. **Base class maximization**: Our base classes are too minimal
   - `BaseEngineGame` only has basic module loading
   - `BaseEngineModule` only has basic state management
   - Need to move ALL common logic to base classes

2. **Odyssey.Engines.Odyssey structure**: Missing shared KOTOR components
   - No `Object` base class (like `kotorbase::Object`)
   - No `Area` class (like `kotorbase::Area`)
   - No `Creature`, `Door`, `Placeable`, `Item` classes
   - No action system, party management, GUI components
   - No script function implementations (only EngineApi)

3. **Still using old code**: `OdysseyModuleLoader` uses `Odyssey.Kotor.Loading.ModuleLoader`
   - Need to move module loading logic to `BaseEngineModule`
   - Only KOTOR-specific file formats should be in `OdysseyModuleLoader`

## Comprehensive Implementation Plan

### Phase 1: Maximize Base Class Code (CRITICAL)

**Goal**: Move 90%+ of common logic to base classes, following xoreos pattern.

#### BaseEngine Enhancements

**Current**: Minimal initialization, resource provider creation
**Target**: All common engine logic

**Move to BaseEngine**:
- [ ] Engine state management (initialized, running, etc.)
- [ ] Resource provider lifecycle management
- [ ] World lifecycle management
- [ ] Engine API lifecycle management
- [ ] Common initialization patterns
- [ ] Common shutdown patterns
- [ ] Error handling and validation

#### BaseEngineGame Enhancements

**Current**: Basic module loading, world updates
**Target**: All common game session logic (like `kotorbase::Game`)

**Move to BaseEngineGame**:
- [ ] Module list management (like `kotorbase::Game::_modules`)
- [ ] Module loading coordination (delegate to `BaseEngineModule`)
- [ ] Player entity management
- [ ] World update loop (already have, but expand)
- [ ] Module state tracking (loaded, running, etc.)
- [ ] Common module transition logic
- [ ] Error handling for module operations

**Keep in OdysseyGameSession** (KOTOR-specific):
- [ ] KOTOR-specific module list collection
- [ ] KOTOR-specific entry point logic
- [ ] KOTOR-specific save game handling

#### BaseEngineModule Enhancements

**Current**: Basic state management, abstract LoadModuleAsync
**Target**: All common module loading logic (like `kotorbase::Module`)

**Move to BaseEngineModule**:
- [ ] Module state management (loaded, running, etc.)
- [ ] Area management (current area, area list)
- [ ] Navigation mesh management
- [ ] Entity spawning coordination (delegate to engine-specific)
- [ ] Common IFO parsing (if IFO is shared across engines)
- [ ] Common area loading patterns
- [ ] Module validation and error handling
- [ ] Progress callback handling

**Keep in OdysseyModuleLoader** (KOTOR-specific):
- [ ] KOTOR-specific file formats (MOD, ARE, GIT parsing)
- [ ] KOTOR-specific module structure (KModuleType)
- [ ] KOTOR-specific area loading (LYT, VIS, BWM)
- [ ] KOTOR-specific entity spawning (GIT parsing)

### Phase 2: Create Odyssey.Engines.Odyssey Shared Components

**Goal**: Create shared KOTOR components (like `kotorbase/`)

#### Object System (Like kotorbase::Object)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `OdysseyObject.cs` - Base object class (like `kotorbase::Object`)
- [ ] `OdysseyCreature.cs` - Creature class (like `kotorbase::Creature`)
- [ ] `OdysseyDoor.cs` - Door class (like `kotorbase::Door`)
- [ ] `OdysseyPlaceable.cs` - Placeable class (like `kotorbase::Placeable`)
- [ ] `OdysseyItem.cs` - Item class (like `kotorbase::Item`)
- [ ] `OdysseyTrigger.cs` - Trigger class (like `kotorbase::Trigger`)
- [ ] `OdysseyWaypoint.cs` - Waypoint class (like `kotorbase::Waypoint`)
- [ ] `OdysseySound.cs` - Sound class (like `kotorbase::Sound`)

**Move from Odyssey.Kotor**:
- [ ] All entity classes
- [ ] All object management code

#### Area System (Like kotorbase::Area)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `OdysseyArea.cs` - Area class (like `kotorbase::Area`)
- [ ] `OdysseyRoom.cs` - Room class (like `kotorbase::Room`)
- [ ] `OdysseyObjectContainer.cs` - Object container (like `kotorbase::ObjectContainer`)

**Move from Odyssey.Kotor**:
- [ ] Area loading and management
- [ ] Room management
- [ ] Object container logic

#### Action System (Like kotorbase::Action)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `OdysseyAction.cs` - Action base class (like `kotorbase::Action`)
- [ ] `OdysseyActionQueue.cs` - Action queue (like `kotorbase::ActionQueue`)
- [ ] `OdysseyActionExecutor.cs` - Action executor (like `kotorbase::ActionExecutor`)

**Move from Odyssey.Core**:
- [ ] Action system (if KOTOR-specific)
- [ ] Action queue management

#### Party System (Like kotorbase::PartyController)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `OdysseyPartyController.cs` - Party controller (like `kotorbase::PartyController`)
- [ ] `OdysseyPartyLeader.cs` - Party leader (like `kotorbase::PartyLeader`)

**Move from Odyssey.Kotor**:
- [ ] Party management code
- [ ] Party leader management

#### Combat System (Like kotorbase::Round)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `OdysseyRound.cs` - Combat round (like `kotorbase::Round`)
- [ ] `OdysseyCombatSystem.cs` - Combat system

**Move from Odyssey.Core**:
- [ ] Combat system (if KOTOR-specific)
- [ ] Round-based combat logic

#### GUI System (Like kotorbase::gui)

**Create in Odyssey.Engines.Odyssey**:
- [ ] `GUI/OdysseyInGameGUI.cs` - In-game GUI (like `kotorbase::gui::ingame`)
- [ ] `GUI/OdysseyDialogGUI.cs` - Dialogue GUI (like `kotorbase::gui::dialog`)
- [ ] `GUI/OdysseyPartySelectionGUI.cs` - Party selection (like `kotorbase::gui::partyselection`)
- [ ] `GUI/OdysseyLoadScreen.cs` - Loading screen (like `kotorbase::gui::loadscreen`)
- [ ] `GUI/OdysseyHUD.cs` - HUD (like `kotorbase::gui::hud`)

**Move from Odyssey.Kotor**:
- [ ] All GUI components
- [ ] GUI management code

#### Script Functions (Like kotorbase::script)

**Current**: `OdysseyK1EngineApi` and `OdysseyK2EngineApi` contain script functions
**Target**: Organize like `kotorbase::script::functions_*.cpp`

**Reorganize in Odyssey.Engines.Odyssey**:
- [ ] `Script/OdysseyFunctions.cs` - Main function dispatcher
- [ ] `Script/OdysseyFunctionsCreatures.cs` - Creature functions
- [ ] `Script/OdysseyFunctionsObject.cs` - Object functions
- [ ] `Script/OdysseyFunctionsModule.cs` - Module functions
- [ ] `Script/OdysseyFunctionsParty.cs` - Party functions
- [ ] `Script/OdysseyFunctionsAction.cs` - Action functions
- [ ] `Script/OdysseyFunctionsEvents.cs` - Event functions
- [ ] `Script/OdysseyFunctionsGlobal.cs` - Global variable functions
- [ ] `Script/OdysseyFunctionsString.cs` - String functions
- [ ] `Script/OdysseyFunctionsTime.cs` - Time functions
- [ ] `Script/OdysseyFunctionsSound.cs` - Sound functions
- [ ] `Script/OdysseyFunctionsMath.cs` - Math functions

**Move from EngineApi**:
- [ ] All script function implementations (organize by category)

### Phase 3: Move KOTOR-Specific Code from AuroraEngine.Common

**Goal**: Complete migration of KOTOR-specific code to `Odyssey.Engines.Odyssey`

#### Common/ Directory

- [ ] `Common/Game.cs` → `Odyssey.Engines.Odyssey.Common/Game.cs`
  - [ ] Move Game enum and extensions
  - [ ] Create backward compatibility alias in AuroraEngine.Common
  - [ ] Update all references

- [ ] `Common/Module.cs` → `Odyssey.Engines.Odyssey.Module/Module.cs`
  - [ ] Move Module class, KModuleType enum
  - [ ] Create backward compatibility alias
  - [ ] Update all references

- [ ] `Common/ModuleDataLoader.cs` → `Odyssey.Engines.Odyssey.Module/ModuleDataLoader.cs`
  - [ ] Move if KOTOR-specific
  - [ ] Update references

#### Resource/Generics/ Directory

**Module/Area Structures** (Review if KOTOR-specific):
- [ ] `Resource/Generics/IFO.cs` → Review, move if KOTOR-specific
- [ ] `Resource/Generics/ARE.cs` → Review, move if KOTOR-specific
- [ ] `Resource/Generics/GIT.cs` → Review, move if KOTOR-specific
- [ ] `Resource/Generics/JRL.cs` → Review, move if KOTOR-specific
- [ ] `Resource/Generics/PTH.cs` → Review, move if KOTOR-specific

**Dialogue Structures** (Review if KOTOR-specific):
- [ ] `Resource/Generics/DLG/` → Review, move if KOTOR-specific

**GUI Structures** (Review if KOTOR-specific):
- [ ] `Resource/Generics/GUI/` → Review, move if KOTOR-specific

**GFF Templates** (✅ COMPLETED):
- [x] All 9 templates moved to `Odyssey.Engines.Odyssey.Templates/`

### Phase 4: Refactor Odyssey.Kotor to Use New Structure

**Goal**: Move all code from `Odyssey.Kotor` to `Odyssey.Engines.Odyssey`

**Current Structure**:
```
Odyssey.Kotor/
├── Game/GameSession.cs (uses ModuleLoader)
├── Loading/ModuleLoader.cs (KOTOR-specific module loading)
├── Entities/ (Creature, Door, Placeable, etc.)
├── Areas/ (Area, Room, etc.)
├── GUI/ (InGameGUI, DialogGUI, etc.)
├── Combat/ (CombatSystem, Round, etc.)
└── ...
```

**Target Structure**:
```
Odyssey.Engines.Odyssey/
├── OdysseyGameSession.cs (inherits BaseEngineGame)
├── OdysseyModuleLoader.cs (inherits BaseEngineModule)
├── Objects/ (OdysseyObject, OdysseyCreature, etc.)
├── Areas/ (OdysseyArea, OdysseyRoom, etc.)
├── Actions/ (OdysseyAction, OdysseyActionQueue, etc.)
├── Party/ (OdysseyPartyController, etc.)
├── Combat/ (OdysseyRound, etc.)
├── GUI/ (OdysseyInGameGUI, etc.)
└── Script/ (OdysseyFunctions*.cs)
```

**Migration Tasks**:
- [ ] Move all entity classes to `Odyssey.Engines.Odyssey.Objects/`
- [ ] Move all area classes to `Odyssey.Engines.Odyssey.Areas/`
- [ ] Move all GUI classes to `Odyssey.Engines.Odyssey.GUI/`
- [ ] Move all combat classes to `Odyssey.Engines.Odyssey.Combat/`
- [ ] Move all action classes to `Odyssey.Engines.Odyssey.Actions/`
- [ ] Move all party classes to `Odyssey.Engines.Odyssey.Party/`
- [ ] Reorganize script functions to `Odyssey.Engines.Odyssey.Script/`
- [ ] Update all references
- [ ] Delete `Odyssey.Kotor` project (or keep as thin wrapper)

### Phase 5: Backward Compatibility for Patcher Tools

**Goal**: Ensure patcher tools continue working

**Strategy**:
- [ ] Create type aliases in `AuroraEngine.Common` for moved classes
- [ ] Create forwarding implementations for classes (C# 7.3 limitation)
- [ ] Document deprecated namespaces
- [ ] Test Andastra, HolocronToolset, NCSDecomp, KotorDiff
- [ ] Create migration guide

### Phase 6: Aurora Engine Implementation (Future)

**Goal**: Implement Aurora engine support

**Tasks**:
- [ ] Create `Odyssey.Engines.Aurora` project
- [ ] Implement Aurora-specific structures (tilesets, etc.)
- [ ] Implement Aurora game profiles
- [ ] Implement Aurora engine API
- [ ] Inherit from base classes, override only what differs

### Phase 7: Eclipse Engine Implementation (Future)

**Goal**: Implement Eclipse engine support

**Tasks**:
- [ ] Create `Odyssey.Engines.Eclipse` project
- [ ] Implement Eclipse-specific structures
- [ ] Implement Eclipse game profiles
- [ ] Implement Eclipse engine API
- [ ] Inherit from base classes, override only what differs

## Code Maximization Strategy

### Target: 90%+ Code in Base Classes

**Measurement**:
- Count lines of code in base classes vs. engine-specific classes
- Target: <10% code in engine-specific classes
- Example: If `BaseEngineModule` has 1000 lines, `OdysseyModuleLoader` should have <100 lines

**Implementation**:
1. Move ALL common logic to base classes
2. Use template method pattern for extensibility
3. Engine-specific classes only override abstract methods
4. Use composition over inheritance where appropriate

### Example: Module Loading Maximization

**Current** (OdysseyModuleLoader has most logic):
```csharp
public class OdysseyModuleLoader : BaseEngineModule
{
    public override async Task LoadModuleAsync(...)
    {
        // 50+ lines of module loading logic
        // IFO parsing, ARE loading, GIT parsing, etc.
    }
}
```

**Target** (BaseEngineModule has most logic):
```csharp
public abstract class BaseEngineModule
{
    public virtual async Task LoadModuleAsync(...)
    {
        // 90% of module loading logic here
        // Common IFO parsing, area loading, entity spawning
        // Only delegate engine-specific parts to abstract methods
        OnLoadModuleIFO(moduleName);
        OnLoadModuleAreas(moduleName);
        OnLoadModuleEntities(moduleName);
    }
    
    protected abstract void OnLoadModuleIFO(string moduleName);
    protected abstract void OnLoadModuleAreas(string moduleName);
    protected abstract void OnLoadModuleEntities(string moduleName);
}

public class OdysseyModuleLoader : BaseEngineModule
{
    protected override void OnLoadModuleIFO(string moduleName)
    {
        // Only KOTOR-specific IFO parsing (10 lines)
    }
    
    protected override void OnLoadModuleAreas(string moduleName)
    {
        // Only KOTOR-specific ARE/LYT/VIS parsing (10 lines)
    }
    
    protected override void OnLoadModuleEntities(string moduleName)
    {
        // Only KOTOR-specific GIT parsing (10 lines)
    }
}
```

## 1:1 Parity with KOTOR 2 Engine

### Ghidra Integration Requirements

**For ALL KOTOR 2 implementations**:
1. Search Ghidra for function addresses
2. Decompile to understand original behavior
3. Add detailed comments with addresses
4. Match behavior exactly
5. Document any deviations

**Comment Format**:
```csharp
/// <summary>
/// Loads a module by name.
/// </summary>
/// <remarks>
/// Based on swkotor2.exe: FUN_006caab0 @ 0x006caab0
/// Located via string reference: "ModuleLoaded" @ 0x007bdd70
/// Original implementation: Sets module state flags, loads IFO, ARE, GIT files
/// Module state: 0=Idle, 1=ModuleLoaded, 2=ModuleRunning (stored in DAT_008283d4)
/// </remarks>
```

## Roadmap Integration

**Update `docs/odyssey_runtime_ghidra_refactoring_roadmap.md`**:
- [ ] Add Phase 1 tasks (Base class maximization)
- [ ] Add Phase 2 tasks (Odyssey.Engines.Odyssey components)
- [ ] Add Phase 3 tasks (AuroraEngine.Common migration)
- [ ] Add Phase 4 tasks (Odyssey.Kotor refactoring)
- [ ] Add Phase 5 tasks (Backward compatibility)
- [ ] Track progress for each file/component
- [ ] Update status as work progresses

## Success Criteria

1. **Code Maximization**: 90%+ code in base classes, <10% in engine-specific
2. **xoreos Pattern**: Follow xoreos's proven architecture pattern
3. **1:1 Parity**: All KOTOR 2 code matches original engine (Ghidra-verified)
4. **Backward Compatibility**: Patcher tools continue working
5. **Modularity**: Clean separation between engine families
6. **Extensibility**: Easy to add Aurora/Eclipse engines

## Notes

- Follow xoreos's proven pattern but avoid their design issues
- Maximize code in base classes (90%+ target)
- Minimize duplication across engine families
- Ensure C# 7.3 compatibility
- Maintain 1:1 parity with original KOTOR 2 engine (Ghidra verification)
- Consider patcher tool dependencies (backward compatibility)
- Document all Ghidra-derived implementations
- Be comprehensive and exhaustive in implementation

