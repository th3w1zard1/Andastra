# OdysseyRuntime Ghidra Refactoring Roadmap

This document tracks the progress of refactoring the `OdysseyRuntime` engine to be more faithful to the original game, using Ghidra's decompiled output from `swkotor2.exe`.

**Status**: In Progress
**Started**: 2025-01-XX
**Current File**: None (starting)

## Refactoring Strategy

1. **Search Ghidra** for relevant functions using string searches and function name searches
2. **Decompile** relevant functions to understand original implementation
3. **Add detailed comments** with Ghidra function addresses and context
4. **Update implementation** to match original behavior where possible
5. **Document** any deviations or improvements

## Files to Process

### Odyssey.Game (Entry Point & Launcher)
- [x] Program.cs
- [x] Core/GameState.cs
- [x] Core/GamePathDetector.cs
- [x] Core/GameSettings.cs
- [x] GUI/MenuRenderer.cs
- [x] GUI/SaveLoadMenu.cs

### Odyssey.Core (Core Domain Logic)
#### Entities
- [x] Entities/Entity.cs - Already has comprehensive Ghidra references
- [x] Entities/World.cs - Already has comprehensive Ghidra references
- [x] Entities/EventBus.cs - Already has comprehensive Ghidra references (FUN_004dcfb0 @ 0x004dcfb0)
- [x] Entities/TimeManager.cs - Already has comprehensive Ghidra references (TIMEPLAYED @ 0x007be1c4, frame timing strings)

#### Actions
- [x] Actions/ActionBase.cs - Already has comprehensive Ghidra references (FUN_00508260 @ 0x00508260, FUN_00505bc0 @ 0x00505bc0)
- [ ] Actions/ActionQueue.cs
- [ ] Actions/ActionMoveToLocation.cs
- [ ] Actions/ActionJumpToLocation.cs
- [ ] Actions/ActionJumpToObject.cs
- [ ] Actions/ActionAttack.cs
- [ ] Actions/ActionDoCommand.cs
- [ ] Actions/DelayScheduler.cs
- [ ] Actions/*.cs (all other action files)

#### Combat
- [ ] Combat/CombatSystem.cs
- [ ] Combat/CombatTypes.cs
- [ ] Combat/EffectSystem.cs

#### Dialogue
- [ ] Dialogue/DialogueSystem.cs
- [ ] Dialogue/DialogueInterfaces.cs
- [ ] Dialogue/RuntimeDialogue.cs
- [ ] Dialogue/LipSyncController.cs

#### Movement & Navigation
- [ ] Movement/CharacterController.cs
- [ ] Movement/PlayerInputHandler.cs
- [ ] Navigation/NavigationMesh.cs
- [ ] Navigation/NavigationMeshFactory.cs

#### Party
- [ ] Party/*.cs (all party files)

#### Perception
- [ ] Perception/PerceptionSystem.cs

#### Save
- [ ] Save/*.cs (all save files)

#### Scripting
- [ ] Scripting/*.cs (all scripting files)

#### Templates
- [ ] Templates/*.cs (all template files)

#### Triggers
- [ ] Triggers/*.cs (all trigger files)

#### Interfaces
- [ ] Interfaces/*.cs (all interface files)

#### Enums
- [ ] Enums/*.cs (all enum files)

#### Other Core
- [ ] GameSettings.cs
- [ ] Journal/JournalSystem.cs
- [ ] Module/*.cs (all module files)
- [ ] AI/AIController.cs
- [ ] Audio/ISoundPlayer.cs
- [ ] Camera/CameraController.cs

### Odyssey.Content (Asset Pipeline)
- [ ] Cache/ContentCache.cs
- [ ] Converters/BwmToNavigationMeshConverter.cs
- [ ] Loaders/GITLoader.cs
- [ ] Loaders/TemplateLoader.cs
- [ ] MDL/*.cs (all MDL files)
- [ ] ResourceProviders/GameResourceProvider.cs
- [ ] Save/SaveDataProvider.cs
- [ ] Save/SaveSerializer.cs
- [ ] Interfaces/*.cs (all interface files)

### Odyssey.Scripting (NCS VM & Engine API)
- [ ] VM/ScriptGlobals.cs
- [ ] VM/ExecutionContext.cs
- [ ] VM/*.cs (all other VM files)
- [ ] EngineApi/*.cs (all engine API files)
- [ ] ScriptExecutor.cs
- [ ] Types/*.cs (all type files)
- [ ] Interfaces/*.cs (all interface files)

### Odyssey.Kotor (KOTOR-Specific Rules)
#### Components
- [ ] Components/TransformComponent.cs
- [ ] Components/StatsComponent.cs
- [ ] Components/ScriptHooksComponent.cs
- [ ] Components/FactionComponent.cs
- [ ] Components/WaypointComponent.cs
- [ ] Components/SoundComponent.cs
- [ ] Components/StoreComponent.cs
- [ ] Components/EncounterComponent.cs
- [ ] Components/*.cs (all other component files)

#### Combat
- [ ] Combat/*.cs (all combat files)

#### Dialogue
- [ ] Dialogue/DialogueState.cs
- [ ] Dialogue/ConversationContext.cs
- [ ] Dialogue/*.cs (all other dialogue files)

#### Game
- [ ] Game/GameSession.cs
- [ ] Game/PlayerController.cs
- [ ] Game/ScriptExecutor.cs
- [ ] Game/*.cs (all other game files)

#### Input
- [ ] Input/*.cs (all input files)

#### Loading
- [ ] Loading/EntityFactory.cs
- [ ] Loading/NavigationMeshFactory.cs
- [ ] Loading/*.cs (all other loading files)

#### Profiles
- [ ] Profiles/IGameProfile.cs
- [ ] Profiles/*.cs (all other profile files)

#### Save
- [ ] Save/SaveGameManager.cs
- [ ] Save/*.cs (all other save files)

#### Systems
- [ ] Systems/EncounterSystem.cs
- [ ] Systems/*.cs (all other system files)

#### Data
- [ ] Data/*.cs (all data files)

### Odyssey.MonoGame (MonoGame Adapters)
#### Animation
- [ ] Animation/*.cs (all animation files)

#### Assets
- [ ] Assets/*.cs (all asset files)

#### Audio
- [ ] Audio/MonoGameSoundPlayer.cs
- [ ] Audio/MonoGameVoicePlayer.cs
- [ ] Audio/*.cs (all other audio files)

#### Backends
- [ ] Backends/*.cs (all backend files)

#### Camera
- [ ] Camera/*.cs (all camera files)

#### Compute
- [ ] Compute/*.cs (all compute files)

#### Converters
- [ ] Converters/RoomMeshRenderer.cs
- [ ] Converters/MdlToMonoGameModelConverter.cs
- [ ] Converters/*.cs (all other converter files)

#### Culling
- [ ] Culling/*.cs (all culling files)

#### Graphics
- [ ] Graphics/*.cs (all graphics files)

#### GUI
- [ ] GUI/*.cs (all GUI files)

#### Lighting
- [ ] Lighting/ClusteredLightCulling.cs
- [ ] Lighting/*.cs (all other lighting files)

#### Loading
- [ ] Loading/*.cs (all loading files)

#### LOD
- [ ] LOD/LODSystem.cs
- [ ] LOD/*.cs (all other LOD files)

#### Materials
- [ ] Materials/KotorMaterialConverter.cs
- [ ] Materials/*.cs (all other material files)

#### Memory
- [ ] Memory/*.cs (all memory files)

#### Models
- [ ] Models/MDLModelConverter.cs
- [ ] Models/*.cs (all other model files)

#### Particles
- [ ] Particles/*.cs (all particle files)

#### Performance
- [ ] Performance/*.cs (all performance files)

#### PostProcessing
- [ ] PostProcessing/*.cs (all post-processing files)

#### Raytracing
- [ ] Raytracing/*.cs (all raytracing files)

#### Remix
- [ ] Remix/*.cs (all remix files)

#### Rendering
- [ ] Rendering/RenderTargetManager.cs
- [ ] Rendering/EntityModelRenderer.cs
- [ ] Rendering/MemoryAliasing.cs
- [ ] Rendering/RenderProfiler.cs
- [ ] Rendering/*.cs (all other rendering files - 58 total)

#### Save
- [ ] Save/*.cs (all save files)

#### Scene
- [ ] Scene/*.cs (all scene files)

#### Shaders
- [ ] Shaders/ShaderCache.cs
- [ ] Shaders/*.cs (all other shader files)

#### Shadows
- [ ] Shadows/*.cs (all shadow files)

#### Spatial
- [ ] Spatial/Octree.cs
- [ ] Spatial/*.cs (all other spatial files)

#### Textures
- [ ] Textures/*.cs (all texture files)

#### UI
- [ ] UI/PauseMenu.cs
- [ ] UI/*.cs (all other UI files)

#### Interfaces
- [ ] Interfaces/*.cs (all interface files)

#### Enums
- [ ] Enums/*.cs (all enum files)

#### Debug
- [ ] Debug/*.cs (all debug files)

### Odyssey.Graphics (Graphics Abstraction)
- [ ] GraphicsBackend.cs
- [ ] IGraphicsBackend.cs
- [ ] IGraphicsDevice.cs
- [ ] IContentManager.cs
- [ ] IFont.cs
- [ ] IInputManager.cs
- [ ] IIndexBuffer.cs
- [ ] IRenderTarget.cs
- [ ] ISpriteBatch.cs
- [ ] ITexture2D.cs
- [ ] IVertexBuffer.cs
- [ ] IDepthStencilBuffer.cs
- [ ] IWindow.cs

### Odyssey.Stride (Stride Backend - Optional)
- [ ] Graphics/*.cs (all Stride graphics files)

### Odyssey.Tests (Test Files - Lower Priority)
- [ ] Tests/UI/*.cs (all test files)
- [ ] Tests/VM/*.cs (all test files)

### Odyssey.Tooling (Tooling - Lower Priority)
- [ ] Tooling/Program.cs
- [ ] Tooling/*.cs (all other tooling files)

## Completed Files
- [x] Program.cs - Verified and confirmed Ghidra references (FUN_00404250 @ 0x00404250, string references at 0x007b575c, 0x0080c210, 0x007b5644)
- [x] GamePathDetector.cs - Already has comprehensive Ghidra references for registry access patterns
- [x] GameState.cs - Updated with accurate FUN_006caab0 @ 0x006caab0 module state management function details
- [x] GameSettings.cs - Verified Ghidra references (FUN_00633270 @ 0x00633270 for directory aliases, FUN_00630a90, FUN_00631ea0 for INI loading)
- [x] GUI/MenuRenderer.cs - Verified Ghidra references (main menu strings at 0x007b6044, 0x007cc030, 0x007cc000, etc.)
- [x] GUI/SaveLoadMenu.cs - Verified Ghidra references (savenfo @ 0x007be1f0, FUN_004eb750 @ 0x004eb750 for save creation, FUN_00708990 @ 0x00708990 for load menu, FUN_0070a020 @ 0x0070a020 for save enumeration)

## Current File Being Processed
- Odyssey.Core/Actions/ActionBase.cs (next - action system)

## Notes

- Focus on core game logic first (Odyssey.Core, Odyssey.Kotor, Odyssey.Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented

## Common Ghidra Search Strings

- "GLOBALVARS" - Global variable system
- "PARTYTABLE" - Party management
- "savenfo" - Save game info
- "BWM" - Walkmesh
- "pathfind" - Pathfinding
- "walkmesh" - Walkmesh operations
- "MODULE" - Module loading
- "AREA" - Area management
- "DIALOG" - Dialogue system
- "COMBAT" - Combat system
- "SCRIPT" - Script execution
- "ACTION" - Action system
- "EFFECT" - Effect system
- "TRIGGER" - Trigger system
- "DOOR" - Door system
- "CREATURE" - Creature management
- "INVENTORY" - Inventory system
- "SAVE" - Save/load system
- "LOAD" - Load system

