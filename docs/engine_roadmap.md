# Odyssey Engine Roadmap

A clean-room Odyssey engine reimplementation using Stride Game Engine and C#. This document outlines the implementation roadmap for a 100% faithful recreation of the KOTOR 1/2 engine with extensibility for other Aurora/Eclipse family games.

## Overview

**Goal**: A playable, faithful KOTOR1/KOTOR2 runtime built on **Stride**, with modular libraries reusable by tooling and future "Odyssey-family" games.

**Core Deliverables**:
- Area loading & rendering with correct materials, lightmaps, and visibility
- Walkmesh + movement with correct collision/nav on BWM and door/trigger volumes
- Actors: creatures, placeables, doors with correct animations and basic AI
- Dialogue: DLG playback with VO and lip sync, script hooks
- NWScript: NCS VM execution + engine function surface sufficient for core gameplay
- Save/Load: functional K1/K2 saves
- Mod compatibility: override/module precedence consistent with KOTOR

## Architecture

### Project Structure

```
src/OdysseyRuntime/
├── Odyssey.Core/       - Pure domain, no Stride dependency
├── Odyssey.Content/    - Asset conversion/caching pipeline
├── Odyssey.Scripting/  - NCS VM + NWScript host surface
├── Odyssey.Kotor/      - K1/K2 rule modules, data tables
├── Odyssey.Stride/     - Stride adapters: rendering, physics, audio, UI
├── Odyssey.Game/       - Stride executable/launcher
├── Odyssey.Tests/      - Deterministic tests
└── Odyssey.Tooling/    - Headless import/validation commands
```

### Layering (strict)

1. **Data/Formats layer (existing)**: `CSharpKOTOR` - Installation lookup, precedence, all file formats
2. **Runtime domain layer**: Game-agnostic runtime concepts (entity, components, world state, time, events)
3. **Stride integration layer**: Mapping runtime entities → Stride entities/components
4. **Game rules layer**: K1/K2-specific rulesets and data-driven interpretation

## Phase 1: Foundation (Current)

### 1.1 Repository + Build Foundation ✅
- [x] Add OdysseyRuntime projects to HoloPatcher.sln
- [x] C# 7.3 language version enforced
- [x] Nullable reference types disabled
- [x] Project structure created

### 1.2 Installation + Resource Virtualization ⏳
- [x] `IGameResourceProvider` interface defined
- [x] Basic resource provider implementation
- [ ] Full precedence chain: override → module archives → save overlay → chitin
- [ ] Async read APIs + caching
- [ ] Deterministic precedence tests

### 1.3 Core Entity System ✅
- [x] `World` class with entity registry
- [x] Entity registry (ID/tag/type lookup)
- [x] Component system (`IComponent`, `IEntity`)
- [x] Time management (`TimeManager`)
- [x] Event bus (`EventBus`)
- [x] Basic component interfaces (Transform, Renderable, Stats, etc.)

## Phase 2: Content Pipeline

### 2.1 Asset Pipeline (KOTOR → Stride)
- [ ] **Textures**: TPC/TGA → Stride.Texture
  - [ ] Alpha handling, normal maps, mipmaps
  - [ ] sRGB/linear rules from TXI
- [ ] **Models**: MDL/MDX → Stride.Model
  - [ ] Geometry, skinning, animations
  - [ ] Attachment nodes
- [x] **Walkmesh**: BWM → NavigationMesh (`BwmToNavigationMeshConverter`)
  - [x] Triangle adjacency decoding
  - [x] AABB tree for spatial queries
  - [x] Surface materials from surfacemat.2da
- [ ] **Audio**: WAV decode → Stride.AudioClip

### 2.2 Content Cache
- [ ] Cache key generation (game, resref, hash, converter version)
- [ ] Storage path strategy (user profile, not install)
- [ ] LRU eviction policy
- [ ] Cache validation and self-healing

## Phase 3: Rendering

### 3.1 Area Scene Assembly ⏳
- [x] LYT parsing (room layout, doorhooks) - via `ModuleLoader`
- [x] VIS parsing (room visibility) - via `ModuleLoader`
- [ ] Room mesh instantiation
- [ ] VIS culling groups
- [ ] Doorhook placement

### 3.2 Material System
- [ ] Lightmap support
- [ ] Environment mapping
- [ ] Transparency ordering
- [ ] Material variants: Opaque, AlphaCutout, AlphaBlend, Additive
- [ ] Lightmapped variants

### 3.3 Camera System
- [ ] Chase camera (KOTOR-style)
- [ ] Free camera (debugging)
- [ ] Dialog camera (speaker/listener focus)
- [ ] Camera collision with walkmesh

## Phase 4: Navigation + Movement

### 4.1 Walkmesh System ✅
- [x] `NavigationMesh` class structure
- [x] `NavigationMeshFactory` interface
- [x] BWM file parsing and loading (via CSharpKOTOR)
- [x] `BwmToNavigationMeshConverter` for conversion
- [x] Triangle adjacency for navigation
- [x] AABB tree for spatial queries
- [x] Surface walkability from surfacemat.2da

### 4.2 Pathfinding ✅
- [x] A* over walkmesh adjacency graph (`NavigationMesh.FindPath`)
- [x] Path smoothing (line-of-sight simplification)
- [ ] Dynamic obstacle handling

### 4.3 Player Controller
- [ ] Click-to-move input handling
- [ ] Movement projection onto walkmesh
- [ ] Ledge/ramp handling
- [ ] Door/trigger volume intersection

## Phase 5: Entity System

### 5.1 Object Spawning ✅
- [x] GIT parsing (instance spawning) - `GITLoader`
- [x] Template loaders: UTC, UTP, UTD, UTT, UTW, UTS, UTE, UTM (`TemplateLoader`)
- [x] Entity factory from templates (`EntityFactory`)
- [x] Entity registry integration (via `World.AddEntity`)

### 5.2 Object Types (all must implement)
```
Object (abstract base)
├── Creature (PC, NPC)
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

### 5.3 Object Interaction
- [ ] Click-to-select
- [ ] Use object (doors, placeables)
- [ ] Door open/close animation
- [ ] Locked/unlocked state

## Phase 6: Scripting (Critical Path)

### 6.1 NCS VM ⏳
- [x] Basic VM structure (`NcsVm` class)
- [x] Header validation (NCS V1.0, 0x42 marker)
- [x] Stack operations (int, float, string, object, vector)
- [x] Arithmetic/comparison opcodes
- [x] Control flow (JMP, JSR, JZ, JNZ, RETN)
- [x] Base pointer operations (SAVEBP, RESTOREBP, CPTOPBP, CPDOWNBP)
- [x] ACTION opcode (engine function dispatch)
- [ ] Full STORE_STATE implementation for deferred actions
- [ ] String interning and management
- [ ] Effect/Event/Location/Talent types

### 6.2 Action System ⏳
- [x] `IAction` interface and base implementations
- [x] `ActionQueue` FIFO queue per entity
- [x] `DelayScheduler` for DelayCommand
- [x] Basic action types: Move, Attack, Wait, OpenDoor, PlayAnimation, SpeakString
- [ ] STORE_STATE integration for action parameters
- [ ] Action interruption handling

### 6.3 Engine API Surface
- [x] `IEngineApi` interface
- [x] `BaseEngineApi` with dispatch mechanism
- [x] `K1EngineApi` profile (core functions implemented)
- [x] `K2EngineApi` profile (TSL-specific functions: influence, forms, etc.)
- [x] Generated dispatch tables from ScriptDefs
- [x] Tier 0 functions (~50): Print*, Random, Get/SetLocal*, GetTag, etc.
- [x] Tier 1 functions (~80): ActionMove*, AssignCommand, DelayCommand, etc.
- [ ] Tier 2-6 function implementation in progress

### 6.4 Script Events
- [x] `ScriptEvent` enum (OnSpawn, OnHeartbeat, OnPerception, etc.)
- [x] `IScriptHooksComponent` interface
- [ ] Event firing from world systems
- [ ] Heartbeat integration (6-second interval)

## Phase 7: Dialogue System

### 7.1 DLG Playback
- [ ] Conversation graph traversal
- [ ] Conditional script evaluation
- [ ] Entry/Reply navigation
- [ ] Script hooks (OnStart, OnEnd)

### 7.2 Localization
- [ ] TLK integration
- [ ] Custom token support

### 7.3 Voice Over
- [ ] StreamVoice/StreamWaves playback
- [ ] VO synchronization with text

### 7.4 Lip Sync
- [ ] LIP file parsing
- [ ] Phoneme shape mapping
- [ ] Facial animation blending

## Phase 8: Game Systems

### 8.1 Perception System
- [ ] Sight range checks
- [ ] Hearing range checks
- [ ] Line-of-sight through walkmesh
- [ ] Perception events (Seen/NotSeen/Heard)

### 8.2 Faction System
- [ ] Faction ID management
- [ ] Hostility checks
- [ ] Faction change support

### 8.3 Combat System
- [ ] Combat round structure (~3 seconds)
- [ ] D20 attack resolution
- [ ] Damage calculation
- [ ] Critical hit confirmation
- [ ] Combat animations

### 8.4 Effect System
- [ ] Effect base class
- [ ] Duration types (Instant, Temporary, Permanent)
- [ ] Core effects: Damage, Heal, AbilityMod, ACMod, etc.
- [ ] Effect stacking rules

### 8.5 AI System
- [ ] Action queue population
- [ ] Default combat behavior
- [ ] Perception-driven reactions
- [ ] Script-driven overrides

## Phase 8.5: Area Management ✅

### 8.5.1 Area Loading Orchestrator
- [x] `AreaManager` - Complete area loading orchestrator
- [x] Progress callback support for loading screens
- [x] Entity spawning from GIT instances
- [x] Navigation mesh loading and pathfinding integration
- [x] Room visibility queries
- [x] Area unloading and cleanup

## Phase 9: Transitions + State

### 9.1 Door Transitions
- [ ] Module transition execution
- [ ] State persistence across transitions
- [ ] Loading screen display
- [ ] OnModuleLeave/OnModuleLoad scripts

### 9.2 Trigger System
- [ ] Trigger volume detection
- [ ] OnEnter/OnExit events
- [ ] One-shot trigger support

### 9.3 Module State
- [ ] Creature state serialization
- [ ] Placeable state (open, inventory)
- [ ] Door state (open, locked)
- [ ] Triggered triggers tracking

## Phase 10: Save/Load

### 10.1 Save System
- [ ] SaveModel definition
- [ ] Global/Campaign variables
- [ ] Party composition + inventory
- [ ] Current module/area/position
- [ ] Module state storage

### 10.2 Load System
- [ ] State deserialization
- [ ] Module restoration
- [ ] Entity state restoration

## Phase 11: UI + Input

### 11.1 In-Game UI
- [ ] Dialogue panel with replies
- [ ] Pause menu
- [ ] Loading screen with progress
- [ ] Debug overlay (FPS, module, entity)

### 11.2 Input Mapping
- [ ] Click-to-move
- [ ] Camera controls
- [ ] Rebindable inputs

## Phase 12: Audio

### 12.1 Audio System
- [ ] VO channel
- [ ] SFX channel
- [ ] Music channel
- [ ] Ambient channel

### 12.2 Spatial Audio
- [ ] 3D positioning
- [ ] Distance attenuation
- [ ] Reverb zones (future)

## Phase 13: Fidelity + Polish

### 13.1 Rendering Fidelity
- [ ] Correct lightmaps
- [ ] Transparency ordering
- [ ] Environment mapping
- [ ] Particle effects (spells, blasters)
- [ ] Projected lights

### 13.2 Animation
- [ ] Skeletal animation playback
- [ ] Animation blending
- [ ] Attachment point animations

## Extensibility: Aurora/Eclipse Engine Family

### Game Profiles ✅
The engine is designed for extensibility to other BioWare/Obsidian engines:

```csharp
public interface IGameProfile
{
    GameType GameType { get; }
    string Name { get; }
    EngineFamily EngineFamily { get; }
    IEngineApi CreateEngineApi();
    IResourceConfig ResourceConfig { get; }
    ITableConfig TableConfig { get; }
    bool SupportsFeature(GameFeature feature);
}
```

Implementation in `Odyssey.Kotor.Profiles`:
- `IGameProfile` - Core interface defining game profile contract
- `K1GameProfile` - KOTOR 1 specific configuration
- `K2GameProfile` - TSL specific configuration with influence system support
- `GameProfileFactory` - Factory for creating profiles by game type

### Supported Profiles (Implementation Status)
- **K1GameProfile** - KOTOR 1 primary target ✅
- **K2GameProfile** - TSL primary target ✅
- **Odyssey.Profiles.Placeholder.JadeEmpire** - Future placeholder
- **Odyssey.Profiles.Placeholder.NWN** - Future placeholder (Aurora)
- **Odyssey.Profiles.Placeholder.MassEffect** - Future placeholder (Eclipse/Unreal)

### Shared Paradigms
- GFF-based data structures
- Module archive systems (ERF/RIM/MOD)
- NWScript/NCS scripting
- 2DA-driven rule tables

## Definition of Done

The runtime is "playable KOTOR" when:

- [ ] **Boot**: User selects K1/K2 install; engine validates; loads chosen module
- [ ] **Render**: Module rooms render with correct lightmaps/materials at stable FPS
- [ ] **Move**: Player moves on walkmesh; cannot walk through blocked regions
- [ ] **Interact**: Click/use doors and placeables; triggers fire
- [ ] **Script**: NWScript VM runs common area scripts; major engine-call coverage
- [ ] **Dialogue**: Complete at least one full conversation with branching
- [ ] **Combat**: Basic combat encounter is playable end-to-end
- [ ] **Save/Load**: Save and reload into same module with core state
- [ ] **Mod Precedence**: Override/module/chitin precedence matches expectations

## NWScript Coverage Strategy

Implementation by observed usage tiers:

### Tier 0 - Boot/Basic (~50 functions)
PrintString, Random, Get/SetLocal*, GetTag, GetModule, GetArea, GetPosition, etc.

### Tier 1 - Movement/Actions (~80 functions)
ActionMoveToLocation, AssignCommand, DelayCommand, ActionOpenDoor, etc.

### Tier 2 - Creatures/Stats (~100 functions)
GetAbilityScore, GetCurrentHitPoints, GetIsPC, GetPerceptionSeen, etc.

### Tier 3 - Combat (~80 functions)
ActionAttack, EffectDamage, ApplyEffectToObject, GetIsInCombat, etc.

### Tier 4 - Dialogue (~40 functions)
SpeakString, GetPCSpeaker, BeginConversation, SetCustomToken, etc.

### Tier 5 - Inventory/Items (~60 functions)
GetItemInSlot, CreateItemOnObject, ActionEquipItem, etc.

### Tier 6 - Force Powers/Spells (~50 functions)
ActionCastSpellAtObject, GetHasSpell, Effect creators, etc.

## Performance Budgets

- **Module Load (cold)**: Acceptable with progress indicator
- **Module Load (warm)**: Seconds, not minutes
- **Frame Rate**: Stable 60fps on midrange GPU
- **Script Budget**: Capped instructions per frame
- **Memory**: Bounded caches with LRU eviction

## Testing Strategy

### Unit Tests (Fast)
- NCS VM instruction semantics
- Engine function tests
- Resource precedence tests

### Golden-File Tests
- Resource parsing validation
- Stable property assertions
- Synthetic test fixtures

### Integration Tests
- Module boot scripts
- Door transitions
- Dialogue flows
- Vertical slice checklist

## References

- **Specifications**: `.cursor/plans/stride_odyssey_engine_e8927e4a.plan.md`
- **Format Documentation**: CSharpKOTOR format parsers
- **Clean-Room Process**: Derived from behavioral observation and specifications only
