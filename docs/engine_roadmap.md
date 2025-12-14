# Odyssey Engine Roadmap

This document tracks the implementation progress of the Odyssey engine reimplementation using Stride Game Engine and C#.

## Primary Goal

Create a 100% faithful recreation of the Odyssey engine (KotOR 1/2), with future extensibility for other Aurora/Eclipse engines (unified abstraction similar to xoreos).

## Architecture Overview

### Engine Family Abstraction

```
Odyssey Engine Family
├── Aurora Engine (NWN)
├── Odyssey Engine (KotOR 1/2)
├── Eclipse Engine (Jade Empire)
└── Unreal 3 Aurora (Mass Effect)
```

### Project Structure

```
src/OdysseyRuntime/
├── Odyssey.Core/          # Pure domain, no Stride dependency
│   ├── Entities/          # Entity/component system
│   ├── Actions/           # Action queue, delay scheduler
│   ├── Navigation/        # Walkmesh pathfinding
│   ├── Module/            # Runtime area/module abstractions
│   └── Interfaces/        # Core contracts
├── Odyssey.Content/       # Asset conversion/caching pipeline
│   ├── Cache/             # Content caching with hash keys
│   ├── Converters/        # TPC, MDL, BWM converters
│   └── ResourceProviders/ # Virtual file system
├── Odyssey.Scripting/     # NCS VM + NWScript engine API
│   ├── VM/                # Stack-based bytecode VM
│   ├── EngineApi/         # Engine function dispatch
│   └── Interfaces/        # Script contracts
├── Odyssey.Kotor/         # K1/K2 rule modules, gameplay systems
│   ├── Rules/             # D20 combat, feats, force powers
│   ├── Dialogue/          # DLG traversal, TLK lookup
│   └── Save/              # Save/load system
├── Odyssey.Stride/        # Stride adapters (rendering, physics, audio, UI)
│   ├── Rendering/         # Scene assembly, materials
│   ├── Materials/         # Lightmaps, transparency
│   └── Lighting/          # Dynamic lights, effects
├── Odyssey.Game/          # Stride executable/launcher
├── Odyssey.Tests/         # Deterministic tests
└── Odyssey.Tooling/       # Headless import/validation commands
```

### Layered Architecture

1. **Data/Formats Layer (CSharpKOTOR)**: File format parsing, installation scanning, resource management
2. **Runtime Domain Layer (Odyssey.Core)**: Game-agnostic runtime concepts (entities, components, world state, events)
3. **Content Pipeline Layer (Odyssey.Content)**: Asset conversion/caching for runtime
4. **Scripting Layer (Odyssey.Scripting)**: NCS VM + NWScript engine API
5. **Stride Integration Layer (Odyssey.Stride)**: Rendering, physics, audio, UI adapters
6. **Game Rules Layer (Odyssey.Kotor)**: K1/K2-specific rulesets, 2DA-driven data

---

```
src/OdysseyRuntime/
├── Odyssey.Core/          # Pure domain, no Stride dependency
│   ├── Actions/           # Action queue implementations
│   ├── Entities/          # Entity, World, EventBus, TimeManager
│   ├── Enums/             # ActionType, ObjectType, ScriptEvent, etc.
│   ├── Interfaces/        # IWorld, IEntity, IAction, INavigationMesh
│   ├── Module/            # RuntimeModule, RuntimeArea
│   └── Navigation/        # NavigationMesh, pathfinding
├── Odyssey.Content/       # Asset conversion/caching pipeline
│   ├── Cache/             # ContentCache
│   ├── Interfaces/        # IContentConverter, IResourceProvider
│   └── ResourceProviders/ # GameResourceProvider
├── Odyssey.Scripting/     # NCS VM + NWScript engine API
│   ├── EngineApi/         # BaseEngineApi, K1EngineApi, K2EngineApi
│   ├── Interfaces/        # INcsVm, IEngineApi, IExecutionContext
│   └── VM/                # NcsVm, ExecutionContext, ScriptGlobals
├── Odyssey.Kotor/         # K1/K2 rule modules, gameplay systems
├── Odyssey.Stride/        # Stride adapters (rendering, physics, audio, UI)
│   ├── Backends/          # Direct3D12, Vulkan backends
│   ├── Lighting/          # Clustered lighting, dynamic lights
│   ├── Materials/         # KOTOR material conversion
│   ├── Raytracing/        # RTX effects (optional)
│   ├── Remix/             # RTX Remix integration (optional)
│   └── Rendering/         # OdysseyRenderer, RenderSettings
├── Odyssey.Game/          # Stride executable/launcher
├── Odyssey.Tests/         # Deterministic tests
└── Odyssey.Tooling/       # Headless import/validation commands
```

## Implementation Phases

### Phase 0: Foundation ✅ COMPLETE

- [x] Project structure created
- [x] C# 7.3 language version enforced
- [x] Core interfaces defined (IWorld, IEntity, INavigationMesh, etc.)
- [x] Entity/component system basics
- [x] Action system (ActionQueue, ActionBase, concrete actions)
- [x] Event bus for inter-system communication
- [x] Time manager for game time tracking
- [x] Core interfaces defined (IWorld, IEntity, INavigationMesh)
- [x] Entity/component system basics (Entity, World, EventBus)
- [x] Action system (ActionQueue, ActionBase, DelayScheduler)
- [x] Basic Stride project scaffolding

### Phase 1: NCS Virtual Machine ✅ COMPLETE

- [x] NCS bytecode parser with header validation (`"NCS V1.0"`, `0x42` marker)
- [x] Stack-based VM with 4-byte alignment (big-endian)
- [x] All core opcodes implemented:
  - [x] Stack operations (RSADD*, CONST*, CPTOPSP, CPDOWNSP)
  - [x] Arithmetic (ADD/SUB/MUL/DIV/MOD for II/IF/FI/FF/VV/VF/FV)
  - Base pointer (SAVEBP/RESTOREBP/CPTOPBP/CPDOWNBP)
  - Bitwise (INCOR/EXCOR/BOOLAND/SHLEFT/SHRIGHT)
  - [x] Comparisons (EQ/NEQ/GT/LT/GEQ/LEQ for II/FF/SS/OO)
  - Constants (CONSTI/CONSTF/CONSTS/CONSTO)
  - Flow control (JMP/JSR/JZ/JNZ/RETN)
  - [x] Logical (LOGAND, LOGOR, INCOR, EXCOR, BOOLAND, NOT)
  - [x] Jumps (JMP, JSR, JZ, JNZ, RETN)
  - Reserve space (RSADDI/RSADDF/RSADDS/RSADDO)
  - [x] Stack frame (SAVEBP, RESTOREBP, MOVSP, DESTRUCT)
  - Stack operations (CPDOWNSP/CPTOPSP/MOVSP/DESTRUCT)
  - [x] Variables (CPDOWNBP, CPTOPBP, DECISP, INCISP, DECIBP, INCIBP)
  - [x] STORE_STATE for deferred actions
- [x] Engine function dispatch interface (ACTION opcode)
- [x] Base engine API structure (K1EngineApi)
- [ ] Complete engine function surface (~850 K1, ~950 K2)
- [ ] Script globals/locals persistence
- [ ] Action queue integration with STORE_STATE

### Phase 2: Navigation & Walkmesh ✅ COMPLETE

- [x] Resource provider interface (IGameResourceProvider)
- [x] Resource identifier system
- [x] GameResourceProvider implementation
- [x] NavigationMesh with full A* pathfinding
- [x] AABB tree for spatial queries
- [x] Adjacency-based pathfinding (face index * 3 + edge encoding)
- [x] Surface material walkability rules (from surfacemat.2da semantics)
- [x] Raycast for click-to-move
- [x] Line-of-sight testing
- [x] Surface projection (height interpolation)
- [x] Path smoothing
- [ ] Integration with CSharpKOTOR BWM parser

### Phase 3: Navigation & Walkmesh ✅ COMPLETE

- [x] Resource provider interface (IGameResourceProvider)
- [x] Resource identifier system
- [x] Content cache structure
- [ ] Full precedence chain: override → module → save → chitin
- [ ] Async resource streaming with cancellation
- [ ] Resource caching with LRU eviction
- [ ] Texture pack integration (swpc_tex_*.erf)
- [x] INavigationMesh interface defined
- [x] NavigationMesh implementation with:
  - [x] A* pathfinding over adjacency graph
  - [x] Surface material walkability rules
  - [x] AABB tree for spatial queries
  - [x] Raycasting for click-to-move
  - [x] Line-of-sight testing
  - [x] Surface projection
- [x] NavigationMeshFactory for building from BWM data
- [ ] Integration with BWM parsing from CSharpKOTOR
- [ ] Door/placeable walkmesh (DWK/PWK) handling

### Phase 4: World & Module Loading ✅ COMPLETE

- [x] Module loading pipeline:
  - [x] IFO parsing (module metadata)
  - [x] ARE parsing (area properties)
  - [x] GIT parsing (instance spawning)
- [x] Room layout from LYT files
- [x] Visibility culling from VIS files
- [x] Entity spawning from GIT templates:
  - [x] UTC → Creature (CreatureComponent)
  - [x] UTP → Placeable (PlaceableComponent)
  - [x] UTD → Door (DoorComponent)
  - [x] UTT → Trigger (TriggerComponent)
  - [x] UTW → Waypoint (WaypointComponent)
  - [x] UTS → Sound (SoundComponent)
  - [x] UTE → Encounter (EncounterComponent)
  - [x] Store → Store (StoreComponent)
- [ ] Area transitions between modules
- [ ] Save overlay integration

### Phase 5: Rendering 📋 PLANNED

- [ ] MDL/MDX model loading and conversion to Stride
- [ ] TPC/TGA texture loading
- [ ] TXI material metadata interpretation
- [ ] Material system:
  - [ ] Lightmap application
  - [ ] Environment maps
  - [ ] Transparency (alpha/additive)
  - [ ] Self-illumination
  - [ ] Cutout (alpha test)
- [ ] Transparency sorting
- [ ] Skeletal animation
- [ ] Particle systems
- [ ] VIS-based room culling

### Phase 6: Dialogue System 🔄 IN PROGRESS

- [x] DLG file structure support
- [x] Entry/reply node navigation
- [x] Conditional script evaluation (framework)
- [ ] TLK text lookup integration
- [ ] Voice-over playback
- [ ] LIP sync animation
- [ ] Camera cuts/shots
- [x] Skippable entries
- [x] Paused conversations

### Phase 7: Combat System 🔄 IN PROGRESS

- [x] Combat round structure (~3 second rounds)
- [x] D20 attack resolution:
  - [x] Attack roll (d20 + modifiers vs AC)
  - [x] Critical hit confirmation
  - [x] Damage calculation
- [x] Two-weapon fighting support
- [ ] Force powers
- [ ] Combat animations
- [ ] Effect system (60+ effect types)

### Phase 8: AI & Perception ✅ COMPLETE

- [x] Perception system:
  - [x] Sight range checks
  - [x] Hearing range checks
  - [x] Line-of-sight queries
  - [x] OnPerception events
- [x] Faction system:
  - [x] Hostility checks
  - [x] Reputation tracking (faction + personal)
- [ ] AI behavior:
  - [ ] Heartbeat scripts
  - [ ] Combat AI
  - [ ] Follow behavior

### Phase 9: Save/Load System 📋 PLANNED

- [ ] SAV file format reading/writing
- [ ] State serialization:
  - [ ] Script globals
  - [ ] Party state
  - [ ] Inventory
  - [ ] Module state
- [ ] Resource overlay from saves

### Phase 10: UI & Input 📋 PLANNED

- [ ] Stride UI integration
- [ ] Dialogue panel
- [ ] HUD (health, party)
- [ ] Pause menu
- [ ] Loading screens
- [ ] Click-to-move controls
- [ ] Camera controllers (chase, free, dialogue)

### Phase 11: Audio 📋 PLANNED

- [ ] WAV decoding
- [ ] Voice-over playback
- [ ] Sound effects
- [ ] Music with combat transitions
- [ ] Spatial audio
- [ ] Ambient sounds

## File Format Support Matrix

### Fully Supported in CSharpKOTOR ✅

| Format | Description | Status |
|--------|-------------|--------|
| GFF | Generic File Format (templates) | ✅ Read/Write |
| ERF | Encapsulated Resource File | ✅ Read/Write |
| RIM | Resource Image File | ✅ Read/Write |
| KEY | Key index file (chitin.key) | ✅ Read/Write |
| BIF | Resource archive | ✅ Read |
| TLK | Talk table (localization) | ✅ Read/Write |
| 2DA | Two-dimensional array (tables) | ✅ Read/Write |
| NCS | Compiled NWScript | ✅ Read/Write |
| TPC | Texture (DXT compressed) | ✅ Read/Write |
| TGA | Texture (Targa) | ✅ Read/Write |
| TXI | Texture info (material flags) | ✅ Read/Write |
| MDL/MDX | Model/Geometry | ✅ Read/Write |
| BWM | Binary Walkmesh | ✅ Read/Write |
| LYT | Layout (room positioning) | ✅ Read/Write |
| VIS | Visibility groups | ✅ Read/Write |
| LIP | Lip sync animation | ✅ Read/Write |
| LTR | Letter tree (name generation) | ✅ Read/Write |
| SSF | Sound set file | ✅ Read/Write |
| WAV | Audio (obfuscated in KOTOR) | ✅ Read/Write |

### GFF Template Types

| Extension | Object Type | Key Fields |
|-----------|-------------|------------|
| IFO | Module Info | Module name, entry points, scripts |
| ARE | Area | Tileset, lighting, weather |
| GIT | Game Instance | Creature/placeable/door/trigger instances |
| UTC | Creature | Appearance, faction, HP, attributes, scripts |
| UTP | Placeable | Appearance, useable, locked, scripts |
| UTD | Door | Generic type, locked, transition, scripts |
| UTT | Trigger | Geometry (polygon), scripts |
| UTW | Waypoint | Tag, position |
| UTS | Sound | Active, looping, positional, resref |
| UTE | Encounter | Creature list, spawn conditions |
| UTI | Item | Base item, properties, charges |
| DLG | Dialogue | Entries, replies, conditions, scripts |

## Key 2DA Tables

| Table | Purpose |
|-------|---------|
| appearance.2da | Model resref, walk/run speed, body type |
| heads.2da | Head model by race/gender |
| baseitems.2da | Item categories, damage, properties |
| feat.2da | Feat definitions, prerequisites |
| spells.2da | Force powers, ranges, effects |
| classes.2da | Class progression, hit dice, saves |
| skills.2da | Skill definitions |
| surfacemat.2da | Surface walkability, footstep sounds |
| portraits.2da | Portrait images |
| placeables.2da | Placeable appearance |
| genericdoors.2da | Door models |
| ambientmusic.2da | Music tracks |
| ambientsound.2da | Ambient sounds |
=======

- [ ] Save overlay integration

### Phase 4: Module Loading 📋 PLANNED

- [ ] IFO parsing (module metadata, scripts, variables)
- [ ] ARE parsing (area properties, lighting, weather)
- [ ] GIT parsing (instance spawning: creatures, placeables, doors, triggers)
- [ ] LYT parsing (room layout, doorhooks)
- [ ] VIS parsing (room visibility culling)
- [ ] PTH parsing (path network for AI)
- [ ] Module transition system

### Phase 5: Entity System 📋 PLANNED

**Object Type Hierarchy**:

```
Object (abstract base)
├── Creature
│   ├── PC (player-controlled)
│   └── NPC (AI-controlled)
├── Door
├── Placeable
├── Trigger (invisible volume)
├── Waypoint (invisible marker)
├── Sound (ambient emitter)
├── Store (merchant)
├── Encounter (spawn point)
├── Item (world-dropped or inventory)
└── AreaOfEffect (spell zones)
```

**GFF Template Loading**:

- [ ] UTC → Creature (Appearance_Type, Faction, HP, Attributes, Feats, Scripts)
- [ ] UTP → Placeable (Appearance, Useable, Locked, OnUsed)
- [ ] UTD → Door (GenericType, Locked, OnOpen, OnClose)
- [ ] UTT → Trigger (Geometry polygon, OnEnter, OnExit)
- [ ] UTW → Waypoint (Tag, position)
- [ ] UTS → Sound (Active, Looping, Positional, ResRef)
- [ ] UTE → Encounter (Creature list, spawn conditions)
- [ ] UTI → Item (BaseItem, Properties, Charges)

### Phase 6: Rendering 📋 PLANNED

- [ ] MDL/MDX model loading and conversion to Stride
- [ ] TPC/TGA texture loading with alpha handling
- [ ] TXI material metadata parsing
- [ ] Room scene assembly from LYT
- [ ] VIS-based culling groups
- [ ] Material system (Opaque, AlphaCutout, AlphaBlend, Additive, Lightmapped)
- [ ] Transparency sorting
- [ ] Skeletal animation
- [ ] Attachment nodes (weapons, effects)
- [ ] Environment mapping

### Phase 7: NWScript Engine API 📋 PLANNED

**Coverage Tiers**:

- **Tier 0**: Boot + area + movement + interaction + dialogue entry
- **Tier 1**: Combat core + inventory + party management essentials
- **Tier 2**: Quests, journals, influence, minigames, full AI
- **Tier 3**: Edge features and obscure calls

**Core Function Groups** (implement in order):

1. Object functions (GetObjectByTag, GetNearestCreature, etc.)
2. Position/location functions (GetPosition, Location, etc.)
3. Area/module functions (GetArea, GetModule, etc.)
4. Action functions (ActionMoveToLocation, ActionAttack, etc.)
5. Effect functions (EffectDamage, ApplyEffectToObject, etc.)
6. Conversation functions (ActionStartConversation, etc.)
7. Combat functions (GetAttackTarget, etc.)
8. Variable functions (GetLocalInt, SetGlobalBoolean, etc.)
9. Party functions (GetPartyMemberByIndex, etc.)
10. Utility functions (PrintString, Random, etc.)

### Phase 8: Dialogue System 📋 PLANNED

- [ ] DLG file traversal (entries, replies, conditions)
- [ ] TLK localized text lookup
- [ ] Conditional script evaluation
- [ ] VO playback with timing
- [ ] LIP sync (phoneme shapes to facial animation)
- [ ] Dialogue camera positioning
- [ ] Script hooks (OnEntry, OnReply)

### Phase 9: Combat System 📋 PLANNED

**Round-Based Combat (~3 second rounds)**:

```
Starting     → 0.0s - init animations
FirstAttack  → ~0.5s - primary attack
SecondAttack → ~1.5s - offhand/counter (if duel)
Cooldown     → ~2.5s - return to ready
Finished     → 3.0s - complete
```

- [ ] D20 attack resolution (roll + modifiers vs AC)
- [ ] Damage calculation with resistances
- [ ] Critical hits (threat range, confirmation)
- [ ] Effect system (~60+ effect types)
- [ ] Perception system (sight/hearing ranges)
- [ ] Faction hostility checks
- [ ] AI behavior (action queue population)

### Phase 10: Save/Load System 📋 PLANNED

- [ ] SaveModel definition (globals, party, inventory, quests)
- [ ] Module state serialization (positions, door states, triggers)
- [ ] Save file packaging (ERF/SAV compatible)
- [ ] Load with state restoration
- [ ] Module transition state preservation

---
>>>>>>> origin/cursor/odyssey-engine-documentation-integration-cc03

## Game Loop Architecture

The engine operates on a **fixed-timestep game loop** with the following per-frame phases:

```
┌─────────────────────────────────────────────────────────────────┐
│                       Fixed-Timestep Game Loop                  │
├─────────────────────────────────────────────────────────────────┤
│ 1. Input Phase     │ Collect input, update camera, click-to-move │
│ 2. Script Phase    │ Process delay wheel, heartbeats, actions    │
│ 3. Simulation      │ Update positions, perception, combat rounds │
│ 4. Animation       │ Skeletal animations, particles, lip sync    │
│ 5. Scene Sync      │ Sync runtime transforms → Stride scene      │
│ 6. Render Phase    │ VIS culling, transparency sort, draw calls  │
│ 7. Audio Phase     │ Spatial audio, trigger one-shots            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Recent Progress

### Action System Components (Dec 2024)

- ActionDoCommand for delayed script commands
- ActionFollowObject for NPC following behavior
- ActionAttack with simple combat calculation
- ActionOpenDoor/ActionCloseDoor with movement
- IDoorComponent, IPlaceableComponent, ITriggerComponent interfaces
- DamageEvent, DoorOpenedEvent, etc. for event system

### NWScript Engine API (Dec 2024)

- Corrected routine IDs to match k1_nwscript.nss
- KOTOR-specific local variables (GetLocalBoolean/Number with index)
- Global variable functions (GetGlobalNumber/Boolean/String)
- Core functions: GetTag(168), GetObjectByTag(200), GetModule(242)

### Playable Demo Foundation (Dec 2024)

- **Game Entry Point**: Program.cs with command line parsing (--k1/--k2, --path, --module)
- **GameSettings**: Configuration class for game/window/debug settings
- **GamePathDetector**: Automatic KOTOR installation detection (registry, Steam, GOG)
- **OdysseyGame**: Stride game integration with main loop
- **GameSession**: Module/save/party management orchestrator
- **ModuleLoader**: Placeholder module loading (TODO: CSharpKOTOR integration)
- **PlayerController**: Click-to-move and object interaction system
- **DialogueManager**: Placeholder dialogue with state machine
- **IActionQueueComponent**: Interface for entity action queues

## Critical TODOs for First Playable Level

### Module Loading (High Priority)

- [ ] Integrate CSharpKOTOR KEY/BIF resource loading
- [ ] Parse IFO file for module metadata
- [ ] Parse ARE file for area properties
- [ ] Parse GIT file for entity instances
- [ ] Load UTC/UTP/UTD templates for entities

### Rendering (High Priority)

- [ ] MDL model loading to Stride meshes
- [ ] Basic texture loading (TPC/TGA)
- [ ] Room rendering from LYT layout
- [ ] Entity rendering at spawn positions

### Gameplay (Medium Priority)

- [ ] DLG file parsing for dialogue
- [ ] Area transition triggers
- [ ] Door opening/closing mechanics
- [ ] Basic combat (for Endar Spire)

## Next Steps

1. Integrate CSharpKOTOR resource loading into ModuleLoader
2. Add MDL model conversion to Stride mesh format
3. Complete dialogue system with actual DLG parsing
4. Test with Endar Spire (end_m01aa) module
5. Add basic UI for dialogue display

## Engine API Function Categories

The NWScript engine API is divided into functional categories:

| Category | K1 Functions | K2 Functions | Description |
|----------|-------------|-------------|-------------|
| Core | ~100 | ~100 | Object access, variables, math |
| Actions | ~50 | ~55 | Movement, combat, interaction |
| Effects | ~60 | ~65 | Buffs, debuffs, visual effects |
| Events | ~30 | ~35 | Script events, triggers |
| Dialogue | ~20 | ~25 | Conversation control |
| Combat | ~40 | ~45 | Attack, damage, hit points |
| Items | ~30 | ~35 | Inventory, equipment |
| Spells | ~25 | ~30 | Force powers |
| Party | ~15 | ~20 | Party management |
| Game | ~20 | ~25 | Module, save, GUI |
| **Total** | **~850** | **~950** | |

## Future Engine Support (Aurora Family)

The architecture is designed to support future Aurora/Eclipse engine variants:

| Engine | Games | Status |
|--------|-------|--------|
| Odyssey | KotOR 1, KotOR 2: TSL | 🔄 Active Development |
| Aurora  | NWN                   | 📋 Future             |
| Electron| Jade Empire           | 📋 Future             |
| Eclipse | Dragon Age: Origins   | 📋 Future             |

### Abstraction Strategy

Following xoreos patterns:

- **Common**: Shared resource loading, GFF parsing, base VM
- **Game-specific**: Engine API implementations, 2DA interpretations, gameplay rules
- **Platform-specific**: Rendering backends, audio backends, input handling

## Design Principles

1. **Faithfulness**: Match original engine behavior exactly where documented
2. **Modernization**: Fix bugs, improve performance where safe
3. **Modularity**: Clean separation for future Aurora/Eclipse support
4. **Clean-Room**: Derive from behavioral specs and observation, not code copying
5. **C# 7.3**: Maintain .NET Framework 4.x compatibility

## Development Resources

### Documentation

- Primary: Spec documents derived from game behavior observation
- Secondary: Format documentation in wiki-style specs
- Implementation: `.cursor/plans/stride_odyssey_engine_e8927e4a.plan.md`

### Verification Tools

- Ghidra MCP server with `swkotor2.exe` loaded for engine behavior verification
- In-game testing for behavioral acceptance criteria

### Reference Projects (behavioral observation only)

- xoreos - Multi-Aurora engine project (behavioral reference)
- reone - C++ reimplementation (behavioral reference)
- KotOR.js - TypeScript reimplementation (behavioral reference)

---

*Document Version: 2.0*
*Last Updated: December 2024*
