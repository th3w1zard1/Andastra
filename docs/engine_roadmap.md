# Odyssey Engine Roadmap

This document tracks the implementation progress of the Odyssey engine reimplementation using Stride Game Engine and C#.

## Primary Goal

Create a 100% faithful recreation of the Odyssey engine (KotOR 1/2), with future extensibility for Aurora/Eclipse engines (unified abstraction similar to xoreos).

## Architecture Overview

### Layered Architecture

1. **Data/Formats Layer (CSharpKOTOR)**: File format parsing, installation scanning, resource management
2. **Runtime Domain Layer (Odyssey.Core)**: Game-agnostic runtime concepts (entities, components, world state, events)
3. **Stride Integration Layer (Odyssey.Stride)**: Rendering, physics, audio, UI adapters
4. **Game Rules Layer (Odyssey.Kotor)**: K1/K2-specific rulesets, 2DA-driven data

### Project Structure

```
src/OdysseyRuntime/
├── Odyssey.Core/          # Pure domain, no Stride dependency
├── Odyssey.Content/       # Asset conversion/caching pipeline
├── Odyssey.Scripting/     # NCS VM + NWScript engine API
├── Odyssey.Kotor/         # K1/K2 rule modules, gameplay systems
├── Odyssey.Stride/        # Stride adapters (rendering, physics, audio, UI)
├── Odyssey.Game/          # Stride executable/launcher
├── Odyssey.Tests/         # Deterministic tests
└── Odyssey.Tooling/       # Headless import/validation commands
```

## Implementation Status

### Phase 0: Foundation ✅

- [x] Project structure created
- [x] C# 7.3 language version enforced
- [x] Core interfaces defined (IWorld, IEntity, INavigationMesh, etc.)
- [x] Entity/component system basics

### Phase 1: NCS Virtual Machine ✅

- [x] NCS bytecode parser with header validation
- [x] Stack-based VM with 4-byte alignment
- [x] All core opcodes implemented (arithmetic, comparisons, jumps, calls)
- [x] Engine function dispatch interface (ACTION opcode)
- [x] K1 Engine API with correct routine IDs from nwscript.nss
- [x] Script globals/locals persistence (ScriptGlobals class)
- [x] KOTOR-specific local variables (index-based, not name-based)
- [x] Action queue integration

### Phase 2: Resource System 🔄

- [x] Resource provider interface (IGameResourceProvider)
- [x] Resource identifier system
- [ ] Full precedence chain: override → module → save → chitin
- [ ] Async resource streaming
- [ ] Resource caching with LRU eviction

### Phase 3: Navigation & Walkmesh ✅

- [x] INavigationMesh interface defined
- [x] BWM file parsing integration (via NavigationMeshFactory)
- [x] AABB tree support (BWM built-in structure)
- [x] Adjacency-based A* pathfinding
- [x] Surface material walkability rules
- [x] Raycast for click-to-move and line-of-sight

### Phase 4: World & Areas ✅

- [x] Module loading (RuntimeModule with IFO properties)
- [x] Area loading (RuntimeArea with ARE/GIT properties)
- [x] Room layout from LYT files (RoomInfo class)
- [ ] Visibility culling from VIS files
- [x] Entity spawning by type (creatures, placeables, doors, triggers, etc.)
- [ ] Area transitions

### Phase 5: Rendering 📋

- [ ] MDL/MDX model loading and conversion to Stride
- [ ] TPC/TGA texture loading
- [ ] TXI material metadata
- [ ] Lightmap application
- [ ] Transparency sorting
- [ ] Skeletal animation

### Phase 6: Gameplay Systems 📋

- [ ] Dialogue system (DLG traversal)
- [ ] Combat system (D20 resolution)
- [ ] Party management
- [ ] Faction/hostility system
- [ ] Save/load system

## Key Resources

### Documentation

- `vendor/PyKotor/wiki/` - Comprehensive file format documentation
- `vendor/PyKotor/vendor/xoreos-docs/` - Official BioWare specifications
- `.cursor/plans/stride_odyssey_engine_e8927e4a.plan.md` - Detailed implementation plan

### Ghidra MCP Integration

Engine-related code MUST use Ghidra MCP server with `swkotor2.exe` loaded for:

- Understanding original engine mechanics
- Verifying faithful recreation
- Discovering undocumented behavior

### Reference Implementations

- `vendor/PyKotor/` - Python reference for format parsing
- `vendor/reone/` - C++ engine reimplementation
- `vendor/KotOR.js/` - TypeScript engine reimplementation
- `vendor/xoreos/` - Multi-Aurora engine project

## Design Principles

1. **Faithfulness**: Match original engine behavior exactly
2. **Modernization**: Fix bugs, improve performance where safe
3. **Modularity**: Clean separation for future Aurora/Eclipse support
4. **Clean-Room**: Behavioral specs, not code copying
5. **C# 7.3**: Maintain .NET Framework 4.x compatibility

## Game Loop Architecture

```
Input Phase     → Collect input, update camera, handle click-to-move
Script Phase    → Process delay wheel, fire heartbeats, execute actions
Simulation Phase → Update positions, perception checks, combat rounds
Animation Phase  → Skeletal animations, particles, lip sync
Scene Sync Phase → Sync runtime transforms → Stride scene graph
Render Phase     → VIS culling, transparency sort, draw calls
Audio Phase      → Spatial audio, trigger one-shots
```

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

---

*Last updated: Dec 2024*
