# Odyssey Engine Roadmap

A clean-room reimplementation of BioWare's Odyssey Engine (KOTOR 1/2) using Stride Game Engine and C#.

## Project Vision

Create a 100% faithful recreation of the Odyssey engine capable of running KOTOR 1 and KOTOR 2, with an architecture that supports future expansion to other Aurora/Eclipse family engines (NWN, Jade Empire, Dragon Age).

## Architecture Overview

### Project Structure

```
src/OdysseyRuntime/
├── Odyssey.Core/       # Pure domain logic, no rendering dependencies
├── Odyssey.Content/    # Asset conversion/caching pipeline
├── Odyssey.Scripting/  # NCS VM + NWScript engine API
├── Odyssey.Kotor/      # K1/K2-specific rules and data
├── Odyssey.Stride/     # Stride rendering/audio/input adapters
├── Odyssey.Game/       # Executable launcher
├── Odyssey.Tooling/    # CLI tools for validation/import
└── Odyssey.Tests/      # Unit and integration tests
```

### Engine Abstraction (Future Aurora/Eclipse Support)

The architecture separates engine-agnostic systems from game-specific implementations:

| Layer | Description | Examples |
|-------|-------------|----------|
| `Odyssey.Core` | Abstract engine interfaces | `IWorld`, `IEntity`, `INcsVm`, `IResourceProvider` |
| `Odyssey.Kotor` | KOTOR-specific implementations | Combat rules, 2DA bindings, game constants |
| `Odyssey.Nwn` (future) | NWN-specific implementations | Different combat rules, tileset system |
| `Odyssey.JadeEmpire` (future) | Jade Empire specifics | Real-time combat, chi system |

## Implementation Phases

### Phase 0: Foundation (Complete)
- [x] Project structure setup
- [x] Entity-component system (`World`, `Entity`, components)
- [x] NCS VM core (instruction dispatch, stack operations)
- [x] Navigation mesh with A* pathfinding
- [x] Resource provider interfaces
- [x] Action queue system

### Phase 1: Core Runtime (In Progress)
Current focus for achieving first playable module.

#### 1.1 Resource System
- [x] `IGameResourceProvider` interface with `ResourceIdentifier` and `SearchLocation`
- [ ] Full precedence chain implementation:
  - Override → Module → Save → TexturePacks → Chitin
- [ ] Archive caching (LRU eviction for ERF/RIM/BIF handles)
- [ ] Async resource streaming
- [ ] Save overlay integration

#### 1.2 Module Loading Pipeline
- [x] `ModuleLoader` orchestration class
- [x] `TemplateLoader` for entity templates
- [x] IFO integration (RuntimeModule with metadata)
- [x] ARE integration (RuntimeArea with properties)
- [x] GIT integration (entity spawning)
- [ ] LYT integration (room instantiation)
- [ ] VIS integration (culling groups)
- [ ] PTH parser (path points)

#### 1.3 Entity Templates
- [x] `IEntityTemplate` interface and implementations
- [x] UTC → `CreatureTemplate`
- [x] UTP → `PlaceableTemplate`
- [x] UTD → `DoorTemplate`
- [x] UTT → `TriggerTemplate`
- [x] UTW → `WaypointTemplate`
- [x] UTS → `SoundTemplate`
- [x] UTE → `EncounterTemplate`
- [x] UTM → `StoreTemplate`

#### 1.4 Scene Assembly
- [ ] Room instantiation from LYT
- [ ] Entity spawning from GIT
- [ ] Doorhook placement
- [ ] VIS culling groups
- [ ] Material assignment

### Phase 2: Player Interaction (In Progress)

#### 2.1 Character Controller
- [x] `CharacterController` with pathfinding integration
- [x] Walkmesh-constrained movement
- [x] Click-to-move input handling (`PlayerInputHandler`)
- [x] Pathfinding integration (with `NavigationMesh`)
- [x] Trigger intersection detection
- [x] Smooth turning and speed transitions
- [ ] Ledge and ramp handling

#### 2.2 Camera System
- [x] `CameraController` with mode switching
- [x] Chase camera (KOTOR-style) with smooth follow
- [x] Free camera (debug)
- [x] Dialogue camera with multiple angles
- [x] Cinematic camera scripting with interpolation
- [ ] Camera collision avoidance (integration pending)

#### 2.3 Object Interaction
- [x] Click-to-select entities
- [x] Cursor mode detection (attack, talk, use, etc.)
- [x] Context-sensitive right-click actions
- [ ] Use/interact actions (execution)
- [ ] Door open/close states
- [ ] Placeable activation
- [x] Trigger enter/exit events

### Phase 3: Scripting Completion

#### 3.1 NCS VM Enhancements
- [ ] Full string handling (proper string pool)
- [ ] STORE_STATE for delayed actions
- [ ] Script debugging and tracing
- [ ] Error isolation (per-script)

#### 3.2 Engine API Surface
- [ ] Core object functions (~100 functions)
- [ ] Area/module functions
- [ ] Effect system functions
- [ ] Combat functions
- [ ] Dialogue functions
- [ ] 2DA lookup functions
- [ ] K1 profile (~850 total)
- [ ] K2 profile (~950 total)

#### 3.3 Script Events
- [ ] OnSpawn
- [ ] OnHeartbeat
- [ ] OnPerception
- [ ] OnUserDefined
- [ ] OnDamaged
- [ ] OnDeath
- [ ] OnConversation
- [ ] OnUsed
- [ ] OnEnter/OnExit

### Phase 4: Dialogue System (Foundation Complete)

#### 4.1 DLG Playback
- [x] `DialogueSystem` core with state machine
- [x] `RuntimeDialogue`, `DialogueEntry`, `DialogueReply` data structures
- [x] Conversation graph traversal
- [x] Conditional script evaluation integration
- [x] Entry/reply script hooks
- [ ] Full node script execution

#### 4.2 Localization
- [ ] TLK string lookup
- [ ] StrRef resolution
- [ ] Language support

#### 4.3 Voice/Lipsync
- [x] `IVoicePlayer` and `ILipSyncController` interfaces
- [x] `LipSyncController` with phoneme interpolation
- [ ] VO playback from StreamVoice/StreamWaves
- [ ] Facial animation blending
- [ ] Camera transitions (speaker/listener)

### Phase 5: Combat System (Foundation Complete)

#### 5.1 Combat Resolution
- [x] `CombatSystem` with D20 attack rolls
- [x] Defense calculations
- [x] Damage application with reduction
- [x] Critical hits (threat + confirmation)
- [x] Saving throws (Fortitude, Reflex, Will)
- [x] `AttackResult`, `DamageResult`, `SavingThrowResult` types

#### 5.2 Combat Round System
- [x] 3-second round timing
- [x] Combat encounter tracking
- [ ] Attack queue management
- [ ] Animation synchronization
- [ ] Projectile handling

#### 5.3 Effect System
- [x] `EffectSystem` with effect application/removal
- [x] Duration tracking (Permanent, Temporary, Instant)
- [x] Round-based expiration
- [ ] Effect stacking rules
- [ ] Visual effect spawning

#### 5.4 AI System
- [ ] Perception (sight/hearing)
- [ ] Faction hostility checks
- [ ] Combat AI patterns
- [ ] Heartbeat AI decisions

### Phase 6: Party System (Foundation Complete)

#### 6.1 Party Management
- [x] `PartySystem` core with PC and NPC management
- [x] Party member addition/removal
- [x] Available/selectable member tracking
- [x] Party member selection (max 3 active)
- [x] Leader switching/cycling
- [x] Gold and XP management with distribution
- [x] `PartyInventory` with stacking and events
- [x] Influence system (K2)

#### 6.2 Follower AI
- [x] `PartyMember` with AI mode settings
- [x] Formation positioning (basic)
- [ ] Follow behavior
- [ ] Combat assistance
- [ ] Script hooks

### Phase 7: Save/Load System (Foundation Complete)

#### 7.1 State Serialization
- [x] `SaveSystem` with save/load orchestration
- [x] `SaveGameData` with complete game state structures
- [x] Global variables (`GlobalVariableState`)
- [x] Local variables per object (`LocalVariableSet`)
- [x] Party state (`PartyState`, `PartyMemberState`)
- [x] Creature state (equipment, inventory, powers, skills)
- [x] `AreaState` with entity states
- [x] `EntityState` (positions, door states, HP, effects)
- [x] Active effects serialization (`SavedEffect`)

#### 7.2 SAV Format
- [x] `SaveDataProvider` for file I/O
- [x] `SaveSerializer` with GFF/ERF structure (placeholder)
- [x] Save folder structure (savenfo.res, savegame.sav, screen.tga)
- [x] Manual, auto, and quick save support
- [x] Slot management
- [ ] Full CSharpKOTOR GFF/ERF integration
- [ ] Resource overlay integration

### Phase 8: Content Pipeline

#### 8.1 Texture Conversion
- [ ] TPC → Stride.Texture
- [ ] TGA → Stride.Texture
- [ ] TXI material metadata
- [ ] Mipmap generation
- [ ] Normal map handling

#### 8.2 Model Conversion
- [ ] MDL/MDX → Stride.Model
- [ ] Geometry and skinning
- [ ] Bone hierarchy
- [ ] Animation keyframes
- [ ] Attachment points

#### 8.3 Audio Conversion
- [ ] WAV decoding
- [ ] Spatial audio positioning
- [ ] Music streaming
- [ ] Ambient sound zones

#### 8.4 Caching
- [ ] Hash-keyed content cache
- [ ] Dependency tracking
- [ ] Cache invalidation
- [ ] Memory pressure management

### Phase 9: Rendering

#### 9.1 Material System
- [ ] Lightmap application
- [ ] Environment maps
- [ ] Alpha blending
- [ ] Additive transparency
- [ ] Two-sided materials
- [ ] Cutout/alpha test

#### 9.2 Scene Rendering
- [ ] Room mesh rendering
- [ ] VIS-based culling
- [ ] Transparency sorting
- [ ] Dynamic lighting
- [ ] Particle effects

#### 9.3 Character Rendering
- [ ] Skeletal animation
- [ ] Animation blending
- [ ] Equipment attachment
- [ ] Facial animation

### Phase 10: UI System

#### 10.1 In-Game UI
- [ ] Dialogue panel
- [ ] HUD (health, party)
- [ ] Action bar
- [ ] Target reticle

#### 10.2 Menus
- [ ] Pause menu
- [ ] Inventory
- [ ] Character sheet
- [ ] Journal
- [ ] Map
- [ ] Options

#### 10.3 Loading
- [ ] Loading screen
- [ ] Progress indication
- [ ] Area transition effects

## File Format Support

### Fully Implemented (in CSharpKOTOR)
| Format | Description | Status |
|--------|-------------|--------|
| GFF | Generic file format (templates) | ✅ Complete |
| ERF | Module/save archives | ✅ Complete |
| RIM | Read-only module archives | ✅ Complete |
| KEY/BIF | Installation archives | ✅ Complete |
| TLK | Talk table (localization) | ✅ Complete |
| 2DA | Data tables | ✅ Complete |
| TPC | Compressed textures | ✅ Complete |
| TGA | Uncompressed textures | ✅ Complete |
| TXI | Texture metadata | ✅ Complete |
| NCS | Compiled scripts | ✅ Complete |
| BWM | Walkmesh (binary) | ✅ Complete |
| LYT | Layout (ASCII) | ✅ Complete |
| VIS | Visibility (ASCII) | ✅ Complete |
| LIP | Lipsync animation | ✅ Complete |
| LTR | Letter combo probabilities | ✅ Complete |
| SSF | Sound set | ✅ Complete |
| WAV | Audio | ✅ Complete |

### Needs Runtime Integration
| Format | Description | Priority |
|--------|-------------|----------|
| MDL/MDX | Models/animations | High |
| DLG | Dialogue trees | High |
| UTC/UTP/UTD/etc | Entity templates | High |
| ARE | Area properties | High |
| GIT | Instance lists | High |
| IFO | Module info | High |
| PTH | Pathfinding points | Medium |
| GUI | User interface | Medium |
| JRL | Journal | Low |
| PTM/PTT | Plot manager | Low |

## Aurora/Eclipse Engine Family

### Supported (Current)
- **Odyssey Engine**: KOTOR 1 (2003), KOTOR 2 (2004)

### Planned (Future)
- **Aurora Engine**: Neverwinter Nights (2002)
- **Electron Engine**: Jade Empire (2005)
- **Eclipse Engine**: Dragon Age: Origins (2009), Dragon Age 2 (2011)

### Abstraction Points
| System | Odyssey | Aurora | Electron | Eclipse |
|--------|---------|--------|----------|---------|
| Combat | D20/turn-based | D20/real-time | Real-time martial | Real-time tactical |
| Scripting | NWScript | NWScript | NWScript | Lua-based |
| Resources | KEY/BIF/ERF | KEY/BIF/ERF | KEY/BIF/ERF | DADB |
| Models | MDL/MDX | MDL/MDX | MDL/MDX | MMH/MSH |

## Quality Targets

### Faithfulness
- Identical script behavior to original
- Matching resource precedence rules
- Compatible with existing mods
- Same visual appearance (materials, lighting)

### Performance
- 60 FPS on mid-range hardware
- Background asset streaming
- No loading stutters
- Memory-efficient caching

### Stability
- Graceful script error handling
- Save game integrity
- Clean shutdown/restart

## Reference Documentation

### Primary Sources
- `vendor/PyKotor/wiki/` - File format specifications
- `vendor/PyKotor/vendor/xoreos-docs/` - BioWare specifications
- In-game observation and testing

### Implementation References
- `.cursor/plans/stride_odyssey_engine_e8927e4a.plan.md` - Detailed implementation plan
- `src/CSharpKOTOR/` - Format readers/writers
- Original game behavior (via Ghidra analysis when needed)

## Development Guidelines

### Clean-Room Process
1. Derive behavior from specs and observation
2. Do not copy code from other implementations
3. Document behavioral decisions
4. Test against original game behavior

### Code Standards
- C# 7.3 compatible (no nullable reference types)
- Follow existing CSharpKOTOR conventions
- Add XML documentation for public APIs
- Unit test all critical paths

### Commit Guidelines
- Use conventional commits format
- Stage files individually (not `git add .`)
- Reference relevant spec sections in commits
