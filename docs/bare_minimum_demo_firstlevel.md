# Bare Minimum Demo: First Level Playthrough

## Overview

This document specifies the **bare minimum implementation** required to achieve a playable demo of KOTOR's first level (Endar Spire, module `end_m01aa`). Items marked ✅ are implemented, items marked ❌ require implementation.

**Target Module**: `end_m01aa` (Endar Spire - Command Module)

**Definition of Done**: Player can boot game, load module, see area rendered, move character, interact with doors, complete at least one dialogue, and transition to next area.

---

## 1. Content Pipeline

### 1.1 Texture Converter (TPC/TGA → Stride Texture) ✅

**Status**: Implemented in `Odyssey.Stride/Converters/TpcToStrideTextureConverter.cs`

**Implemented**:

- Convert TPC pixel data to `Stride.Graphics.Texture`
- Handle DXT1/DXT3/DXT5 compressed formats
- Support mipmaps
- Handle grayscale, RGB, RGBA formats

**Acceptance**: Textures display correctly on room meshes without corruption.

### 1.2 Model Converter (MDL/MDX → Stride Model) ✅

**Status**: Implemented in `Odyssey.Stride/Converters/MdlToStrideModelConverter.cs`

**Implemented**:

- Convert MDL vertex data to `Stride.Graphics.MeshDraw`
- Support trimesh (static geometry) nodes
- Handle UV mapping for texture coordinates
- Support multiple mesh nodes per model
- Basic material binding to textures

**Deferred** (not required for first demo):

- Skeletal animation
- Skinning/bones
- Animations keyframes
- Attachment nodes

**Acceptance**: Room meshes render with correct geometry and textures.

### 1.3 Content Cache Integration ❌

**Status**: `ContentCache.cs` exists but lacks Stride integration.

**Required**:

- Wire texture converter to cache
- Wire model converter to cache
- Implement cache key generation (game + resref + hash)
- Add background loading support

---

## 2. Rendering

### 2.1 Room Scene Assembly ✅

**Status**: Implemented in `Odyssey.Stride/Scene/SceneBuilder.cs`

**Implemented**:

- `SceneBuilder.cs` builds Stride scene from KOTOR module data
- Instantiate `Stride.Engine.Entity` per room from LYT
- Attach `ModelComponent` with converted room models
- Position rooms according to LYT offsets
- Apply materials with lightmaps
- VIS-based room visibility culling

**Acceptance**: All rooms in `end_m01aa` display in Stride window.

### 2.2 Basic Material System ✅

**Status**: Implemented in `Odyssey.Stride/Materials/KotorMaterialFactory.cs`

**Implemented**:

- Create basic Stride material with diffuse texture
- Handle alpha cutout (for transparent surfaces)
- Handle additive blending (for emissive surfaces)
- Lightmap support

### 2.3 VIS Culling ✅

**Status**: Implemented in `SceneBuilder.cs`

**Implemented**:

- Track current room based on player position
- Enable/disable room entity visibility based on VIS data
- Update on player room transitions

---

## 3. Camera System

### 3.1 Chase Camera ✅

**Status**: Implemented in `Odyssey.Stride/Camera/ChaseCamera.cs`

**Implemented**:

- KOTOR-style follow camera
- Parameters: distance, height, pitch, lag factor, zoom limits
- Follow target entity (player)
- Smooth interpolation on movement
- Camera collision with walkmesh (prevent clipping through walls)
- Orbit mode (right-click drag)
- Zoom controls (mouse wheel)

**Acceptance**: Camera follows player smoothly through corridors.

---

## 4. Player Controller

### 4.1 Click-to-Move ✅

**Status**: Implemented in `Odyssey.Kotor/Input/PlayerController.cs`

**Implemented**:

- `PlayerController.cs` handles player input and movement
- Raycast from mouse position to walkmesh
- Find clicked position on navigation mesh
- Generate path using `NavigationMesh.FindPath()`
- Move player entity along path
- Walk/Run speed toggle
- Destination reached event

### 4.2 Movement Projection ✅

**Status**: Implemented in `PlayerController.cs` and `NavigationMesh.cs`

**Implemented**:

- Project player position onto walkmesh surface
- Clamp movement to walkable faces
- Prevent walking through blocked faces
- Handle face transitions smoothly

**Acceptance**: Player moves to clicked locations, stays on walkmesh.

---

## 5. Entity Spawning

### 5.1 GIT Loading Integration ❌

**Status**: `EntityFactory` creates entities from GIT, but not integrated with scene.

**Required**:

- Wire `EntityFactory` into module loading pipeline
- Spawn entities into `World` on module load
- Create Stride entities for visual representation
- Link runtime entities to Stride scene entities

### 5.2 Door Rendering ❌

**Status**: Door entities created but no visual representation.

**Required**:

- Load door model from `genericdoors.2da` → model resref
- Position door at doorhook from LYT
- Handle door open/close animation state
- Create collider for door walkmesh (DWK)

### 5.3 Placeable Rendering ❌

**Status**: Placeable entities created but no visual representation.

**Required**:

- Load placeable model from `placeables.2da` → model resref
- Position at GIT instance location
- Handle static vs interactive placeables

### 5.4 Creature Rendering ❌

**Status**: Creature entities created but no visual representation.

**Required**:

- Load creature model from `appearance.2da` → model resref
- Position at GIT instance location
- Basic idle animation (optional for demo)

---

## 6. Interaction System

### 6.1 Object Selection ❌

**Status**: Not implemented.

**Required**:

- Raycast from cursor to scene objects
- Highlight selected object (outline shader or tint)
- Show selection indicator
- Store currently selected object

### 6.2 Door Interaction ❌

**Status**: `ActionOpenDoor` exists but not wired to input.

**Required**:

- On click/use: check if door is locked
- If unlocked: play open animation, update walkmesh
- Fire `OnOpen` script event
- Handle door transitions (module change)

### 6.3 Use Object ❌

**Status**: `OnUsed` scripts exist in entities.

**Required**:

- Generic "use" action for placeables
- Fire `OnUsed` script
- Handle container inventory (optional for demo)

---

## 7. Scripting Integration

### 7.1 Script Loading ❌

**Status**: NCS VM implemented, but script loading from resources not wired.

**Required**:

- Load NCS bytes from module resources
- Create `ExecutionContext` with proper world/entity references
- Execute OnSpawn scripts when entities spawn
- Execute OnEnter scripts for triggers

### 7.2 Essential Engine Functions

**Status**: ~120 functions implemented (stubs), ~50 working.

**Required for Demo** (must be functional):

- ✅ `PrintString`, `Random`, `IntToString`, `FloatToString`
- ✅ `GetTag`, `GetObjectByTag`
- ✅ `GetPosition`, `GetFacing`, `SetFacing`
- ✅ `GetLocalInt/Float/String`, `SetLocalInt/Float/String`
- ✅ `GetGlobalNumber/Boolean`, `SetGlobalNumber/Boolean`
- ❌ `ActionMoveToLocation` - needs location type support
- ❌ `ActionMoveToObject` - needs pathfinding integration
- ❌ `ActionOpenDoor` - needs door state management
- ❌ `GetIsObjectValid` - needs proper entity validation
- ❌ `GetDistanceToObject` - implemented but needs verification
- ❌ `SetLocked`, `GetLocked` - not implemented
- ❌ `ActionStartConversation` - needs dialogue system wire-up
- ❌ `GetEnteringObject` - needs trigger system integration
- ❌ `ApplyEffectToObject` - stub only

### 7.3 Trigger System ❌

**Status**: Triggers created from GIT but no collision detection.

**Required**:

- Detect entity entering/exiting trigger polygon
- Fire `OnEnter`/`OnExit` scripts
- Track triggered triggers (one-shot support)

### 7.4 Heartbeat System ❌

**Status**: Not implemented.

**Required**:

- 6-second interval timer per entity
- Fire `OnHeartbeat` script
- Budget-limited script execution per frame

---

## 8. Dialogue System

### 8.1 Dialogue Initiation ❌

**Status**: `DialogueManager` implemented with DLG traversal.

**Required**:

- Wire `ActionStartConversation` to DialogueManager
- Load TLK for text lookup
- Pause gameplay during conversation
- Set up camera for dialogue

### 8.2 Dialogue UI ❌

**Status**: Not implemented.

**Required**:

- Create basic dialogue panel (speaker text + replies)
- Display localized text from TLK
- Show clickable reply options (1-9 numbered)
- Handle reply selection

### 8.3 Voice Over Playback ❌

**Status**: WAV decoder exists, no audio playback integration.

**Required**:

- Load VO WAV from module resources
- Play through Stride audio system
- Sync with dialogue text display
- Handle VO completion callback

---

## 9. Module Transitions

### 9.1 Door Transitions ❌

**Status**: Door entities have `LinkedToModule` data, but no transition logic.

**Required**:

- Detect door use with transition flag
- Save current module state (optional for demo)
- Unload current module
- Load target module
- Position player at target waypoint

### 9.2 Loading Screen ❌

**Status**: Not implemented.

**Required**:

- Display loading screen during module load
- Show progress indicator
- Display area name

---

## 10. UI Essentials

### 10.1 Basic HUD ❌

**Status**: Not implemented.

**Required for Demo**:

- Health bar (player HP)
- Simple overlay for debug info

### 10.2 Pause Menu ❌

**Status**: Not implemented.

**Required**:

- ESC key opens pause
- Resume/Exit options
- Basic menu navigation

---

## Implementation Priority Order

1. **Content Pipeline** - Without this, nothing renders
   - Texture converter
   - Model converter

2. **Room Rendering** - See the environment
   - Scene builder
   - Basic materials

3. **Player Movement** - Navigate the space
   - Click-to-move
   - Chase camera

4. **Entity Visuals** - See objects in world
   - Door models
   - Placeable models

5. **Interaction** - Interact with world
   - Object selection
   - Door open/close

6. **Dialogue** - Complete conversations
   - Dialogue UI
   - TLK text display

7. **Module Transitions** - Move between areas
   - Door transitions
   - Loading screen

---

## Estimated Scope

| Category | Items | Estimated Effort |
|----------|-------|------------------|
| Content Pipeline | 2 converters + cache | High (complex formats) |
| Rendering | Scene builder + materials | Medium |
| Camera | Chase camera | Low |
| Movement | Player controller | Medium |
| Entities | 3 entity types visual | Medium |
| Interaction | Selection + doors | Medium |
| Scripting | Wire-up + triggers | Medium |
| Dialogue | UI + playback | Medium |
| Transitions | Module loading | Medium |
| UI | HUD + menu | Low |

**Total**: Significant effort, but achievable with focused work on critical path.

---

## Files to Create/Modify

### New Files Required

```
src/OdysseyRuntime/Odyssey.Content/Converters/
  - TpcToStrideTextureConverter.cs
  - MdlToStrideModelConverter.cs

src/OdysseyRuntime/Odyssey.Stride/
  - Scene/
    - SceneBuilder.cs
    - RoomSceneNode.cs
    - EntitySceneSync.cs
  - Materials/
    - KotorMaterialFactory.cs
  - UI/
    - DialoguePanel.cs
    - BasicHUD.cs
    - PauseMenu.cs
    - LoadingScreen.cs

src/OdysseyRuntime/Odyssey.Kotor/
  - Input/
    - PlayerController.cs
  - Systems/
    - TriggerSystem.cs
    - HeartbeatSystem.cs
  - Game/
    - ModuleTransitionSystem.cs
```

### Files to Modify

```
src/OdysseyRuntime/Odyssey.Game/Core/OdysseyGame.cs
  - Integrate scene builder
  - Wire player controller
  - Add UI systems

src/OdysseyRuntime/Odyssey.Kotor/Game/GameSession.cs
  - Wire module loading to scene
  - Add entity spawning to scene

src/OdysseyRuntime/Odyssey.Scripting/EngineApi/K1EngineApi.cs
  - Fix ActionMoveToLocation/Object
  - Add location type support
  - Wire trigger integration
```

---

## Testing Checklist

### Milestone 1: Render

- [ ] Load `end_m01aa` module
- [ ] See room geometry in window
- [ ] Textures display correctly
- [ ] All rooms visible

### Milestone 2: Move

- [ ] Camera follows player
- [ ] Click on floor moves player
- [ ] Player stays on walkmesh
- [ ] Can navigate corridors

### Milestone 3: Interact

- [ ] See door models
- [ ] Click on door opens it
- [ ] Door blocks movement when closed
- [ ] Scripts fire on door open

### Milestone 4: Dialogue

- [ ] Talk to NPC shows dialogue
- [ ] Text displays from TLK
- [ ] Can select replies
- [ ] Conversation ends properly

### Milestone 5: Transition

- [ ] Use transition door
- [ ] Loading screen appears
- [ ] New module loads
- [ ] Player at correct position

---

## References

- **Format Documentation**: `vendor/PyKotor/wiki/`
- **Implementation Plan**: `.cursor/plans/stride_odyssey_engine_e8927e4a.plan.md`
- **Engine Roadmap**: `docs/engine_roadmap.md`
- **CSharpKOTOR Formats**: `src/CSharpKOTOR/Formats/`
