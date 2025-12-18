# Andastra Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: IN PROGRESS
**Started**: 2025-01-16
**Current Phase**: Phase 1 - Core Systems (Save/Load, Walkmesh, Module Loading)
**Ghidra Project**: `C:\Users\boden\test.gpr` (20 programs loaded)

## Progress Summary

### ✅ Completed Systems

- **Save/Load System**:
  - `swkotor2.exe`: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70, SaveModuleState @ 0x004f0c50, SaveModuleIFO @ 0x005018b0
  - **Inheritance**: Base class `SaveSerializer` (Runtime.Games.Common), `OdysseySaveSerializer : SaveSerializer` (Runtime.Games.Odyssey)
  - **Cross-engine**: TODO - Search swkotor.exe, nwmain.exe, daorigins.exe for similar functions
- **Walkmesh System**:
  - `swkotor2.exe`: WriteBWMFile @ 0x0055aef0, ValidateBWMHeader @ 0x006160c0
  - **Inheritance**: Base class `WalkmeshSystem` (Runtime.Games.Common), `OdysseyWalkmeshSystem : WalkmeshSystem` (Runtime.Games.Odyssey)
  - **Cross-engine**: TODO - Search swkotor.exe, nwmain.exe, daorigins.exe for similar functions
- **Module Loading**:
  - `swkotor2.exe`: LoadModule @ 0x004f20d0, LoadModuleFromPath @ 0x004f3460
  - **Inheritance**: Base class `ModuleLoader` (Runtime.Games.Common), `OdysseyModuleLoader : ModuleLoader` (Runtime.Games.Odyssey)
  - **Cross-engine**: TODO - Search swkotor.exe, nwmain.exe, daorigins.exe for similar functions

### 🔄 In Progress

- **NCS VM Execution**:
  - `swkotor2.exe`: DispatchScriptEvent @ 0x004dd730 - Dispatches script events to registered handlers
  - `swkotor2.exe`: LogScriptEvent @ 0x004dcfb0 - Logs script events for debugging
  - `swkotor2.exe`: LoadScriptHooks @ 0x0050c510 - Loads script hook references from GFF templates
  - **Inheritance**: TODO - Establish base class `ScriptExecutor` (Runtime.Games.Common), engine-specific implementations
  - **Cross-engine**: TODO - Search swkotor.exe, nwmain.exe, daorigins.exe for similar functions
  - Searching for actual NCS bytecode execution functions

### 📋 Pending Systems

- Dialogue System (DLG, TLK, VO)
- Combat System
- Entity Spawning
- Animation System
- Audio System
- Trigger System
- Encounter System
- Store System
- Party Management
- Perception System

## Class Inheritance Structure

### Script Execution System

**Base Class**: `ScriptExecutor` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseyScriptExecutor : ScriptExecutor` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: DispatchScriptEvent @ 0x004dd730, LogScriptEvent @ 0x004dcfb0, LoadScriptHooks @ 0x0050c510
  - `swkotor.exe`: TODO - Search for similar functions
- **Aurora Implementation**: `AuroraScriptExecutor : ScriptExecutor` (Runtime.Games.Aurora)
  - `nwmain.exe`: CScriptEvent @ 0x1404c6490, ExecuteCommandExecuteScript @ 0x14051d5c0
- **Eclipse Implementation**: `EclipseScriptExecutor : ScriptExecutor` (Runtime.Games.Eclipse)
  - `daorigins.exe`: COMMAND_EXECUTESCRIPT @ 0x00af4aac (string reference)
  - `DragonAge2.exe`: TODO - Search for similar functions

### Save/Load System

**Base Class**: `SaveSerializer` (Runtime.Games.Common)

- **Odyssey Implementation**: `OdysseySaveSerializer : SaveSerializer` (Runtime.Games.Odyssey)
  - `swkotor2.exe`: SerializeSaveNfo @ 0x004eb750, SaveGlobalVariables @ 0x005ac670, SavePartyTable @ 0x0057bd70
  - `swkotor.exe`: TODO - Search for similar functions
- **Aurora Implementation**: `AuroraSaveSerializer : SaveSerializer` (Runtime.Games.Aurora)
  - `nwmain.exe`: TODO - Search for similar functions
- **Eclipse Implementation**: `EclipseSaveSerializer : SaveSerializer` (Runtime.Games.Eclipse)
  - `daorigins.exe`: TODO - Search for similar functions
  - `DragonAge2.exe`: TODO - Search for similar functions

## Ghidra Executables Inventory

### ✅ PRIMARY TARGET: Odyssey Engine (KotOR 1 & 2)

**Most Relevant for Current Project Goals** - This is the primary focus for implementation/unification with class inheritance.

- **swkotor.exe** (KotOR 1)
  - Path: `/swkotor.exe`
  - Functions: **12,045** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **HIGH** - Primary reference for KotOR 1 engine behavior

- **swkotor2.exe** (KotOR 2: The Sith Lords)
  - Path: `/swkotor2.exe`
  - Functions: **13,782** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **HIGHEST** - Most complete Odyssey implementation, primary reference for KotOR 2

### Aurora Engine (Neverwinter Nights)

- **nwmain.exe** (Neverwinter Nights main executable)
  - Path: `/nwmain.exe`
  - Functions: **52,644** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Aurora engine architecture

- **nwnnsscomp.exe** (NWN Script Compiler)
  - Path: `/nwnnsscomp.exe`
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Tool, not runtime engine

- **nwnnsscomp_kscript.exe** (NWN Script Compiler - KScript variant)
  - Path: `/nwnnsscomp_kscript.exe`
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Tool, not runtime engine

### Eclipse Engine (Dragon Age Origins)

- **daorigins.exe** (Dragon Age: Origins)
  - Path: `/daorigins.exe`
  - Functions: **8,420** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Eclipse engine architecture

- **DragonAge2.exe** (Dragon Age II)
  - Path: `/DragonAge2.exe`
  - Functions: **12,069** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **MEDIUM** - Reference for Eclipse engine evolution

### Mass Effect Series (Eclipse-based/Custom)

- **MassEffect.exe** (Mass Effect 1)
  - Path: `/MassEffect.exe`
  - Functions: **12,558** (filtered, excluding default names)
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Eclipse-derived, may have unique implementations

- **MassEffect2.exe** (Mass Effect 2)
  - Path: `/MassEffect2.exe`
  - Functions: **3** (filtered, appears to be a launcher/stub)
  - Status: ✅ Loaded and available
  - Priority: **LOW** - Minimal executable, likely launcher

### Windows System DLLs (Not Game Executables)

- USER32.DLL, KERNEL32.DLL, GDI32.DLL, IMM32.DLL, VERSION.DLL, OLE32.DLL
- DINPUT8.DLL, OPENGL32.DLL, GLU32.DLL, MSS32.DLL, BINKW32.DLL
- Status: ✅ Loaded (for cross-reference analysis)
- Priority: **LOW** - System libraries, not engine code

### Total Executables Available: 20

- Game Executables: 8 (swkotor.exe, swkotor2.exe, nwmain.exe, daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe, nwnnsscomp.exe, nwnnsscomp_kscript.exe)
- System DLLs: 11
- **Total Functions Available**: ~120,000+ across all game executables

## Reverse Engineering Strategy by Engine

### Odyssey Engine (PRIMARY FOCUS)

**Target Files**: `swkotor.exe` and `swkotor2.exe`
**Goal**: Achieve 1:1 parity with original engine behavior for all systems:

- Module loading (LYT, VIS, GIT, ARE)
- Walkmesh navigation (BWM parsing and pathfinding)
- Entity spawning and management
- Script execution (NCS VM)
- Combat system
- Dialogue system (DLG, TLK, VO)
- Save/Load system (GFF serialization)
- Party management
- Perception system
- Trigger system
- Encounter system
- Store system
- Animation system
- Audio system

**Key String References to Search**:

- "GLOBALVARS", "PARTYTABLE", "savenfo", "SAVES", "MODULES", "AREAS"
- "BWM", "walkmesh", "pathfind", "navigation"
- "DLG", "TLK", "dialogue", "conversation"
- "NCS", "script", "ACTION", "STORE_STATE"
- "UTC", "UTD", "UTI", "UTP", "UTS", "UTT", "UTW", "UTE", "UTM" (template types)

### Aurora Engine (SECONDARY)

**Target File**: `nwmain.exe`
**Goal**: Understand Aurora architecture for future unification:

- Server/client architecture
- Script compilation differences
- Module system differences
- Combat system differences

### Eclipse Engine (SECONDARY)

**Target Files**: `daorigins.exe`, `DragonAge2.exe`
**Goal**: Understand Eclipse architecture for future unification:

- Dialogue system evolution
- Combat system evolution
- Save system differences

## Update Instructions

When processing a file:

- Mark as `- [/]` when starting work
- Mark as `- [x]` when complete with Ghidra references added and verified
- Add notes about function addresses, string references, and implementation details
- Use format: `- [x] FileName.cs - Function addresses, string references, key findings`

## Refactoring Strategy

1. Search Ghidra for relevant functions using string searches and function name searches
2. Decompile relevant functions to understand original implementation
3. Add detailed comments with Ghidra function addresses and context
4. Update implementation to match original behavior where possible
5. Document any deviations or improvements
6. Rename functions in Ghidra with descriptive names
7. Set function prototypes in Ghidra
8. Add comprehensive comments in Ghidra

## Files to Process

### Runtime/Core (99 files)

#### Entities (4 files)

- [ ] Entities/Entity.cs
- [ ] Entities/World.cs
- [ ] Entities/EventBus.cs
- [ ] Entities/TimeManager.cs

#### Actions (27 files)

- [ ] Actions/ActionAttack.cs
- [ ] Actions/ActionBase.cs
- [ ] Actions/ActionCastSpellAtLocation.cs
- [ ] Actions/ActionCastSpellAtObject.cs
- [ ] Actions/ActionCloseDoor.cs
- [ ] Actions/ActionDestroyObject.cs
- [ ] Actions/ActionDoCommand.cs
- [ ] Actions/ActionEquipItem.cs
- [ ] Actions/ActionFollowObject.cs
- [ ] Actions/ActionJumpToLocation.cs
- [ ] Actions/ActionJumpToObject.cs
- [ ] Actions/ActionMoveAwayFromObject.cs
- [ ] Actions/ActionMoveToLocation.cs
- [ ] Actions/ActionMoveToObject.cs
- [ ] Actions/ActionOpenDoor.cs
- [ ] Actions/ActionPickUpItem.cs
- [ ] Actions/ActionPlayAnimation.cs
- [ ] Actions/ActionPutDownItem.cs
- [ ] Actions/ActionQueue.cs
- [ ] Actions/ActionRandomWalk.cs
- [ ] Actions/ActionSpeakString.cs
- [ ] Actions/ActionUnequipItem.cs
- [ ] Actions/ActionUseItem.cs
- [ ] Actions/ActionUseObject.cs
- [ ] Actions/ActionWait.cs
- [ ] Actions/DelayScheduler.cs

#### AI (1 file)

- [ ] AI/AIController.cs

#### Animation (1 file)

- [ ] Animation/AnimationSystem.cs

#### Audio (1 file)

- [ ] Audio/ISoundPlayer.cs

#### Camera (1 file)

- [ ] Camera/CameraController.cs

#### Combat (3 files)

- [ ] Combat/CombatSystem.cs
- [ ] Combat/CombatTypes.cs
- [ ] Combat/EffectSystem.cs

#### Dialogue (4 files)

- [ ] Dialogue/DialogueInterfaces.cs
- [ ] Dialogue/DialogueSystem.cs
- [ ] Dialogue/LipSyncController.cs
- [ ] Dialogue/RuntimeDialogue.cs

#### Enums (5 files)

- [ ] Enums/Ability.cs
- [ ] Enums/ActionStatus.cs
- [ ] Enums/ActionType.cs
- [ ] Enums/ObjectType.cs
- [ ] Enums/ScriptEvent.cs

#### GameLoop (1 file)

- [ ] GameLoop/FixedTimestepGameLoop.cs

#### Interfaces (25 files)

- [ ] Interfaces/IAction.cs
- [ ] Interfaces/IActionQueue.cs
- [ ] Interfaces/IArea.cs
- [ ] Interfaces/IComponent.cs
- [ ] Interfaces/IDelayScheduler.cs
- [ ] Interfaces/IEntity.cs
- [ ] Interfaces/IEventBus.cs
- [ ] Interfaces/IGameServicesContext.cs
- [ ] Interfaces/IModule.cs
- [ ] Interfaces/INavigationMesh.cs
- [ ] Interfaces/ITimeManager.cs
- [ ] Interfaces/IWorld.cs
- [ ] Interfaces/Components/IActionQueueComponent.cs
- [ ] Interfaces/Components/IAnimationComponent.cs
- [ ] Interfaces/Components/IDoorComponent.cs
- [ ] Interfaces/Components/IFactionComponent.cs
- [ ] Interfaces/Components/IInventoryComponent.cs
- [ ] Interfaces/Components/IItemComponent.cs
- [ ] Interfaces/Components/IPerceptionComponent.cs
- [ ] Interfaces/Components/IPlaceableComponent.cs
- [ ] Interfaces/Components/IQuickSlotComponent.cs
- [ ] Interfaces/Components/IRenderableComponent.cs
- [ ] Interfaces/Components/IScriptHooksComponent.cs
- [ ] Interfaces/Components/IStatsComponent.cs
- [ ] Interfaces/Components/ITransformComponent.cs
- [ ] Interfaces/Components/ITriggerComponent.cs

#### Journal (1 file)

- [ ] Journal/JournalSystem.cs

#### Module (3 files)

- [ ] Module/ModuleTransitionSystem.cs
- [ ] Module/RuntimeArea.cs
- [ ] Module/RuntimeModule.cs

#### Movement (2 files)

- [ ] Movement/CharacterController.cs
- [ ] Movement/PlayerInputHandler.cs

#### Navigation (2 files)

- [ ] Navigation/NavigationMesh.cs
- [ ] Navigation/NavigationMeshFactory.cs

#### Party (3 files)

- [ ] Party/PartyInventory.cs
- [ ] Party/PartyMember.cs
- [ ] Party/PartySystem.cs

#### Perception (1 file)

- [ ] Perception/PerceptionSystem.cs

#### Save (3 files)

- [ ] Save/AreaState.cs
- [ ] Save/SaveGameData.cs
- [ ] Save/SaveSystem.cs

#### Templates (9 files)

- [ ] Templates/CreatureTemplate.cs
- [ ] Templates/DoorTemplate.cs
- [ ] Templates/EncounterTemplate.cs
- [ ] Templates/IEntityTemplate.cs
- [ ] Templates/PlaceableTemplate.cs
- [ ] Templates/SoundTemplate.cs
- [ ] Templates/StoreTemplate.cs
- [ ] Templates/TriggerTemplate.cs
- [ ] Templates/WaypointTemplate.cs

#### Triggers (1 file)

- [ ] Triggers/TriggerSystem.cs

#### Root (1 file)

- [ ] GameSettings.cs

### Runtime/Content (18 files)

- [ ] Cache/ContentCache.cs
- [ ] Converters/BwmToNavigationMeshConverter.cs
- [ ] Interfaces/IContentCache.cs
- [ ] Interfaces/IContentConverter.cs
- [ ] Interfaces/IGameResourceProvider.cs
- [ ] Interfaces/IResourceProvider.cs
- [ ] Loaders/GITLoader.cs
- [ ] Loaders/TemplateLoader.cs
- [ ] MDL/MDLBulkReader.cs
- [ ] MDL/MDLCache.cs
- [ ] MDL/MDLConstants.cs
- [ ] MDL/MDLDataTypes.cs
- [ ] MDL/MDLFastReader.cs
- [ ] MDL/MDLLoader.cs
- [ ] MDL/MDLOptimizedReader.cs
- [ ] ResourceProviders/GameResourceProvider.cs
- [ ] Save/SaveDataProvider.cs
- [ ] Save/SaveSerializer.cs

### Runtime/Scripting (11 files)

- [ ] EngineApi/BaseEngineApi.cs
- [ ] Interfaces/IEngineApi.cs
- [ ] Interfaces/IExecutionContext.cs
- [ ] Interfaces/INcsVm.cs
- [ ] Interfaces/IScriptGlobals.cs
- [ ] Interfaces/Variable.cs
- [ ] ScriptExecutor.cs
- [ ] Types/Location.cs
- [ ] VM/ExecutionContext.cs
- [ ] VM/NcsVm.cs
- [ ] VM/ScriptGlobals.cs

### Runtime/Games (99 files)

#### Common (8 files)

- [ ] Common/BaseEngine.cs
- [ ] Common/BaseEngineGame.cs
- [ ] Common/BaseEngineModule.cs
- [ ] Common/BaseEngineProfile.cs
- [ ] Common/IEngine.cs
- [ ] Common/IEngineGame.cs
- [ ] Common/IEngineModule.cs
- [ ] Common/IEngineProfile.cs

#### Odyssey (84 files)

- [ ] Odyssey/OdysseyEngine.cs
- [ ] Odyssey/OdysseyGameSession.cs
- [ ] Odyssey/OdysseyModuleLoader.cs
- [ ] Odyssey/Combat/CombatManager.cs
- [ ] Odyssey/Combat/CombatRound.cs
- [ ] Odyssey/Combat/DamageCalculator.cs
- [ ] Odyssey/Combat/WeaponDamageCalculator.cs
- [ ] Odyssey/Components/ActionQueueComponent.cs
- [ ] Odyssey/Components/CreatureComponent.cs
- [ ] Odyssey/Components/DoorComponent.cs
- [ ] Odyssey/Components/EncounterComponent.cs
- [ ] Odyssey/Components/FactionComponent.cs
- [ ] Odyssey/Components/InventoryComponent.cs
- [ ] Odyssey/Components/ItemComponent.cs
- [ ] Odyssey/Components/PerceptionComponent.cs
- [ ] Odyssey/Components/PlaceableComponent.cs
- [ ] Odyssey/Components/QuickSlotComponent.cs
- [ ] Odyssey/Components/RenderableComponent.cs
- [ ] Odyssey/Components/ScriptHooksComponent.cs
- [ ] Odyssey/Components/SoundComponent.cs
- [ ] Odyssey/Components/StatsComponent.cs
- [ ] Odyssey/Components/StoreComponent.cs
- [ ] Odyssey/Components/TransformComponent.cs
- [ ] Odyssey/Components/TriggerComponent.cs
- [ ] Odyssey/Components/WaypointComponent.cs
- [ ] Odyssey/Data/GameDataManager.cs
- [ ] Odyssey/Data/TwoDATableManager.cs
- [ ] Odyssey/Dialogue/ConversationContext.cs
- [ ] Odyssey/Dialogue/DialogueManager.cs
- [ ] Odyssey/Dialogue/DialogueState.cs
- [ ] Odyssey/Dialogue/KotorDialogueLoader.cs
- [ ] Odyssey/Dialogue/KotorLipDataLoader.cs
- [ ] Odyssey/EngineApi/K1EngineApi.cs
- [ ] Odyssey/EngineApi/K2EngineApi.cs
- [ ] Odyssey/EngineApi/OdysseyK1EngineApi.cs
- [ ] Odyssey/EngineApi/OdysseyK2EngineApi.cs
- [ ] Odyssey/Game/GameSession.cs
- [ ] Odyssey/Game/ModuleLoader.cs
- [ ] Odyssey/Game/ScriptExecutor.cs
- [ ] Odyssey/Input/PlayerController.cs
- [ ] Odyssey/Loading/EntityFactory.cs
- [ ] Odyssey/Loading/KotorModuleLoader.cs
- [ ] Odyssey/Loading/ModuleLoader.cs
- [ ] Odyssey/Loading/NavigationMeshFactory.cs
- [ ] Odyssey/Profiles/GameProfileFactory.cs
- [ ] Odyssey/Profiles/IGameProfile.cs
- [ ] Odyssey/Profiles/K1GameProfile.cs
- [ ] Odyssey/Profiles/K2GameProfile.cs
- [ ] Odyssey/Save/SaveGameManager.cs
- [ ] Odyssey/Systems/AIController.cs
- [ ] Odyssey/Systems/ComponentInitializer.cs
- [ ] Odyssey/Systems/EncounterSystem.cs
- [ ] Odyssey/Systems/FactionManager.cs
- [ ] Odyssey/Systems/HeartbeatSystem.cs
- [ ] Odyssey/Systems/ModelResolver.cs
- [ ] Odyssey/Systems/PartyManager.cs
- [ ] Odyssey/Systems/PerceptionManager.cs
- [ ] Odyssey/Systems/StoreSystem.cs
- [ ] Odyssey/Systems/TriggerSystem.cs
- [ ] Odyssey/Templates/UTC.cs
- [ ] Odyssey/Templates/UTCHelpers.cs
- [ ] Odyssey/Templates/UTD.cs
- [ ] Odyssey/Templates/UTDHelpers.cs
- [ ] Odyssey/Templates/UTE.cs
- [ ] Odyssey/Templates/UTEHelpers.cs
- [ ] Odyssey/Templates/UTI.cs
- [ ] Odyssey/Templates/UTIHelpers.cs
- [ ] Odyssey/Templates/UTM.cs
- [ ] Odyssey/Templates/UTMHelpers.cs
- [ ] Odyssey/Templates/UTP.cs
- [ ] Odyssey/Templates/UTPHelpers.cs
- [ ] Odyssey/Templates/UTS.cs
- [ ] Odyssey/Templates/UTSHelpers.cs
- [ ] Odyssey/Templates/UTT.cs
- [ ] Odyssey/Templates/UTTHelpers.cs
- [ ] Odyssey/Templates/UTW.cs
- [ ] Odyssey/Templates/UTWHelpers.cs

#### Aurora (1 file)

- [ ] Aurora/AuroraEngine.cs

#### Eclipse (1 file)

- [ ] Eclipse/EclipseEngine.cs

#### Infinity (1 file)

- [ ] Infinity/InfinityEngine.cs

### Runtime/Graphics (247 files)

#### Common (50 files)

- [ ] Common/Backends/BaseDirect3D11Backend.cs
- [ ] Common/Backends/BaseDirect3D12Backend.cs
- [ ] Common/Backends/BaseGraphicsBackend.cs
- [ ] Common/Backends/BaseVulkanBackend.cs
- [ ] Common/Enums/GraphicsBackendType.cs
- [ ] Common/Interfaces/ILowLevelBackend.cs
- [ ] Common/Interfaces/IPostProcessingEffect.cs
- [ ] Common/Interfaces/IRaytracingSystem.cs
- [ ] Common/Interfaces/IRoomMeshRenderer.cs
- [ ] Common/Interfaces/ISamplerFeedbackBackend.cs
- [ ] Common/Interfaces/IUpscalingSystem.cs
- [ ] Common/PostProcessing/BasePostProcessingEffect.cs
- [ ] Common/Raytracing/BaseRaytracingSystem.cs
- [ ] Common/Remix/BaseRemixBridge.cs
- [ ] Common/Rendering/RenderSettings.cs
- [ ] Common/Structs/GraphicsStructs.cs
- [ ] Common/Upscaling/BaseUpscalingSystem.cs
- [ ] Common/Interfaces/IContentManager.cs
- [ ] Common/Interfaces/IDepthStencilBuffer.cs
- [ ] Common/Interfaces/IEffect.cs
- [ ] Common/Interfaces/IEntityModelRenderer.cs
- [ ] Common/Interfaces/IFont.cs
- [ ] Common/Interfaces/IGraphicsBackend.cs
- [ ] Common/Interfaces/IGraphicsDevice.cs
- [ ] Common/Interfaces/IIndexBuffer.cs
- [ ] Common/Interfaces/IInputManager.cs
- [ ] Common/Interfaces/IModel.cs
- [ ] Common/Interfaces/IRenderState.cs
- [ ] Common/Interfaces/IRenderTarget.cs
- [ ] Common/Interfaces/ISamplerFeedbackBackend.cs
- [ ] Common/Interfaces/ISpatialAudio.cs
- [ ] Common/Interfaces/ISpriteBatch.cs
- [ ] Common/Interfaces/ITexture2D.cs
- [ ] Common/Interfaces/IVertexBuffer.cs
- [ ] Common/Interfaces/IVertexDeclaration.cs
- [ ] Common/Interfaces/IWindow.cs
- [ ] Common/MatrixHelper.cs
- [ ] Common/VertexPositionColor.cs
- [ ] GraphicsBackend.cs

#### MonoGame (158 files)

- [ ] MonoGame/Animation/AnimationCompression.cs
- [ ] MonoGame/Animation/SkeletalAnimationBatching.cs
- [ ] MonoGame/Assets/AssetHotReload.cs
- [ ] MonoGame/Assets/AssetValidator.cs
- [ ] MonoGame/Audio/MonoGameSoundPlayer.cs
- [ ] MonoGame/Audio/MonoGameVoicePlayer.cs
- [ ] MonoGame/Audio/SpatialAudio.cs
- [ ] MonoGame/Backends/BackendFactory.cs
- [ ] MonoGame/Backends/Direct3D10Backend.cs
- [ ] MonoGame/Backends/Direct3D11Backend.cs
- [ ] MonoGame/Backends/Direct3D12Backend.cs
- [ ] MonoGame/Backends/OpenGLBackend.cs
- [ ] MonoGame/Backends/VulkanBackend.cs
- [ ] MonoGame/Camera/ChaseCamera.cs
- [ ] MonoGame/Camera/MonoGameDialogueCameraController.cs
- [ ] MonoGame/Compute/ComputeShaderFramework.cs
- [ ] MonoGame/Converters/MdlToMonoGameModelConverter.cs
- [ ] MonoGame/Converters/RoomMeshRenderer.cs
- [ ] MonoGame/Converters/TpcToMonoGameTextureConverter.cs
- [ ] MonoGame/Culling/DistanceCuller.cs
- [ ] MonoGame/Culling/Frustum.cs
- [ ] MonoGame/Culling/GPUCulling.cs
- [ ] MonoGame/Culling/OcclusionCuller.cs
- [ ] MonoGame/Debug/DebugRendering.cs
- [ ] MonoGame/Debug/RenderStatistics.cs
- [ ] MonoGame/Enums/GraphicsBackend.cs
- [ ] MonoGame/Enums/MaterialType.cs
- [ ] MonoGame/Graphics/MonoGameBasicEffect.cs
- [ ] MonoGame/Graphics/MonoGameContentManager.cs
- [ ] MonoGame/Graphics/MonoGameDepthStencilBuffer.cs
- [ ] MonoGame/Graphics/MonoGameEntityModelRenderer.cs
- [ ] MonoGame/Graphics/MonoGameFont.cs
- [ ] MonoGame/Graphics/MonoGameGraphicsBackend.cs
- [ ] MonoGame/Graphics/MonoGameGraphicsDevice.cs
- [ ] MonoGame/Graphics/MonoGameIndexBuffer.cs
- [ ] MonoGame/Graphics/MonoGameInputManager.cs
- [ ] MonoGame/Graphics/MonoGameRenderState.cs
- [ ] MonoGame/Graphics/MonoGameRenderTarget.cs
- [ ] MonoGame/Graphics/MonoGameRoomMeshRenderer.cs
- [ ] MonoGame/Graphics/MonoGameSpatialAudio.cs
- [ ] MonoGame/Graphics/MonoGameSpriteBatch.cs
- [ ] MonoGame/Graphics/MonoGameTexture2D.cs
- [ ] MonoGame/Graphics/MonoGameVertexBuffer.cs
- [ ] MonoGame/Graphics/MonoGameWindow.cs
- [ ] MonoGame/GUI/KotorGuiManager.cs
- [ ] MonoGame/GUI/MyraMenuRenderer.cs
- [ ] MonoGame/Interfaces/ICommandList.cs
- [ ] MonoGame/Interfaces/IDevice.cs
- [ ] MonoGame/Interfaces/IDynamicLight.cs
- [ ] MonoGame/Interfaces/IGraphicsBackend.cs
- [ ] MonoGame/Interfaces/IPbrMaterial.cs
- [ ] MonoGame/Interfaces/IRaytracingSystem.cs
- [ ] MonoGame/Lighting/ClusteredLightCulling.cs
- [ ] MonoGame/Lighting/ClusteredLightingSystem.cs
- [ ] MonoGame/Lighting/DynamicLight.cs
- [ ] MonoGame/Lighting/LightProbeSystem.cs
- [ ] MonoGame/Lighting/VolumetricLighting.cs
- [ ] MonoGame/Loading/AsyncResourceLoader.cs
- [ ] MonoGame/LOD/LODFadeSystem.cs
- [ ] MonoGame/LOD/LODSystem.cs
- [ ] MonoGame/Materials/KotorMaterialConverter.cs
- [ ] MonoGame/Materials/KotorMaterialFactory.cs
- [ ] MonoGame/Materials/MaterialInstancing.cs
- [ ] MonoGame/Materials/PbrMaterial.cs
- [ ] MonoGame/Memory/GPUMemoryPool.cs
- [ ] MonoGame/Memory/MemoryTracker.cs
- [ ] MonoGame/Memory/ObjectPool.cs
- [ ] MonoGame/Models/MDLModelConverter.cs
- [ ] MonoGame/Particles/GPUParticleSystem.cs
- [ ] MonoGame/Particles/ParticleSorter.cs
- [ ] MonoGame/Performance/FramePacing.cs
- [ ] MonoGame/Performance/FrameTimeBudget.cs
- [ ] MonoGame/Performance/GPUTimestamps.cs
- [ ] MonoGame/Performance/Telemetry.cs
- [ ] MonoGame/PostProcessing/Bloom.cs
- [ ] MonoGame/PostProcessing/ColorGrading.cs
- [ ] MonoGame/PostProcessing/ExposureAdaptation.cs
- [ ] MonoGame/PostProcessing/MotionBlur.cs
- [ ] MonoGame/PostProcessing/SSAO.cs
- [ ] MonoGame/PostProcessing/SSR.cs
- [ ] MonoGame/PostProcessing/TemporalAA.cs
- [ ] MonoGame/PostProcessing/ToneMapping.cs
- [ ] MonoGame/Raytracing/NativeRaytracingSystem.cs
- [ ] MonoGame/Raytracing/RaytracedEffects.cs
- [ ] MonoGame/Remix/Direct3D9Wrapper.cs
- [ ] MonoGame/Remix/RemixBridge.cs
- [ ] MonoGame/Remix/RemixMaterialExporter.cs
- [ ] MonoGame/Rendering/AdaptiveQuality.cs
- [ ] MonoGame/Rendering/BatchOptimizer.cs
- [ ] MonoGame/Rendering/BindlessTextures.cs
- [ ] MonoGame/Rendering/CommandBuffer.cs
- [ ] MonoGame/Rendering/CommandListOptimizer.cs
- [ ] MonoGame/Rendering/ContactShadows.cs
- [ ] MonoGame/Rendering/DecalSystem.cs
- [ ] MonoGame/Rendering/DeferredRenderer.cs
- [ ] MonoGame/Rendering/DepthPrePass.cs
- [ ] MonoGame/Rendering/DrawCallSorter.cs
- [ ] MonoGame/Rendering/DynamicBatching.cs
- [ ] MonoGame/Rendering/DynamicResolution.cs
- [ ] MonoGame/Rendering/EntityModelRenderer.cs
- [ ] MonoGame/Rendering/FrameGraph.cs
- [ ] MonoGame/Rendering/GeometryCache.cs
- [ ] MonoGame/Rendering/GeometryStreaming.cs
- [ ] MonoGame/Rendering/GPUInstancing.cs
- [ ] MonoGame/Rendering/GPUMemoryBudget.cs
- [ ] MonoGame/Rendering/GPUMemoryDefragmentation.cs
- [ ] MonoGame/Rendering/GPUSynchronization.cs
- [ ] MonoGame/Rendering/HDRPipeline.cs
- [ ] MonoGame/Rendering/IndirectRenderer.cs
- [ ] MonoGame/Rendering/MemoryAliasing.cs
- [ ] MonoGame/Rendering/MeshCompression.cs
- [ ] MonoGame/Rendering/ModernRenderer.cs
- [ ] MonoGame/Rendering/MultiThreadedRenderer.cs
- [ ] MonoGame/Rendering/MultiThreadedRendering.cs
- [ ] MonoGame/Rendering/OcclusionQueries.cs
- [ ] MonoGame/Rendering/OdysseyRenderer.cs
- [ ] MonoGame/Rendering/PipelineStateCache.cs
- [ ] MonoGame/Rendering/QualityPresets.cs
- [ ] MonoGame/Rendering/RenderBatchManager.cs
- [ ] MonoGame/Rendering/RenderGraph.cs
- [ ] MonoGame/Rendering/RenderOptimizer.cs
- [ ] MonoGame/Rendering/RenderPipeline.cs
- [ ] MonoGame/Rendering/RenderProfiler.cs
- [ ] MonoGame/Rendering/RenderQueue.cs
- [ ] MonoGame/Rendering/RenderSettings.cs
- [ ] MonoGame/Rendering/RenderTargetCache.cs
- [ ] MonoGame/Rendering/RenderTargetChain.cs
- [ ] MonoGame/Rendering/RenderTargetManager.cs
- [ ] MonoGame/Rendering/RenderTargetPool.cs
- [ ] MonoGame/Rendering/RenderTargetScaling.cs
- [ ] MonoGame/Rendering/ResourceBarriers.cs
- [ ] MonoGame/Rendering/ResourcePreloader.cs
- [ ] MonoGame/Rendering/SceneGraph.cs
- [ ] MonoGame/Rendering/ShaderCache.cs
- [ ] MonoGame/Rendering/StateCache.cs
- [ ] MonoGame/Rendering/SubsurfaceScattering.cs
- [ ] MonoGame/Rendering/TemporalReprojection.cs
- [ ] MonoGame/Rendering/TextureAtlas.cs
- [ ] MonoGame/Rendering/TextureCompression.cs
- [ ] MonoGame/Rendering/TriangleStripGenerator.cs
- [ ] MonoGame/Rendering/Upscaling/DLSS.cs
- [ ] MonoGame/Rendering/Upscaling/FSR.cs
- [ ] MonoGame/Rendering/VariableRateShading.cs
- [ ] MonoGame/Rendering/VertexCacheOptimizer.cs
- [ ] MonoGame/Rendering/VisibilityBuffer.cs
- [ ] MonoGame/Save/AsyncSaveSystem.cs
- [ ] MonoGame/Scene/SceneBuilder.cs
- [ ] MonoGame/Shaders/ShaderCache.cs
- [ ] MonoGame/Shaders/ShaderPermutationSystem.cs
- [ ] MonoGame/Shadows/CascadedShadowMaps.cs
- [ ] MonoGame/Spatial/Octree.cs
- [ ] MonoGame/Textures/TextureFormatConverter.cs
- [ ] MonoGame/Textures/TextureStreamingManager.cs
- [ ] MonoGame/UI/BasicHUD.cs
- [ ] MonoGame/UI/DialoguePanel.cs
- [ ] MonoGame/UI/LoadingScreen.cs
- [ ] MonoGame/UI/MainMenu.cs
- [ ] MonoGame/UI/PauseMenu.cs
- [ ] MonoGame/UI/ScreenFade.cs

#### Stride (37 files)

- [ ] Stride/Audio/StrideSoundPlayer.cs
- [ ] Stride/Audio/StrideVoicePlayer.cs
- [ ] Stride/Backends/StrideBackendFactory.cs
- [ ] Stride/Backends/StrideDirect3D11Backend.cs
- [ ] Stride/Backends/StrideDirect3D12Backend.cs
- [ ] Stride/Backends/StrideVulkanBackend.cs
- [ ] Stride/Camera/StrideDialogueCameraController.cs
- [ ] Stride/Graphics/StrideBasicEffect.cs
- [ ] Stride/Graphics/StrideContentManager.cs
- [ ] Stride/Graphics/StrideDepthStencilBuffer.cs
- [ ] Stride/Graphics/StrideEntityModelRenderer.cs
- [ ] Stride/Graphics/StrideFont.cs
- [ ] Stride/Graphics/StrideGraphicsBackend.cs
- [ ] Stride/Graphics/StrideGraphicsDevice.cs
- [ ] Stride/Graphics/StrideIndexBuffer.cs
- [ ] Stride/Graphics/StrideInputManager.cs
- [ ] Stride/Graphics/StrideRenderState.cs
- [ ] Stride/Graphics/StrideRenderTarget.cs
- [ ] Stride/Graphics/StrideRoomMeshRenderer.cs
- [ ] Stride/Graphics/StrideSpatialAudio.cs
- [ ] Stride/Graphics/StrideSpriteBatch.cs
- [ ] Stride/Graphics/StrideTexture2D.cs
- [ ] Stride/Graphics/StrideVertexBuffer.cs
- [ ] Stride/Graphics/StrideWindow.cs
- [ ] Stride/PostProcessing/StrideBloomEffect.cs
- [ ] Stride/PostProcessing/StrideColorGradingEffect.cs
- [ ] Stride/PostProcessing/StrideMotionBlurEffect.cs
- [ ] Stride/PostProcessing/StrideSsaoEffect.cs
- [ ] Stride/PostProcessing/StrideSsrEffect.cs
- [ ] Stride/PostProcessing/StrideTemporalAaEffect.cs
- [ ] Stride/PostProcessing/StrideToneMappingEffect.cs
- [ ] Stride/Raytracing/StrideRaytracingSystem.cs
- [ ] Stride/Remix/StrideRemixBridge.cs
- [ ] Stride/Upscaling/StrideDlssSystem.cs
- [ ] Stride/Upscaling/StrideFsrSystem.cs
- [ ] Stride/Upscaling/StrideXeSSSystem.cs

#### Enums (1 file)

- [ ] Enums/GraphicsBackendType.cs

### Game (8 files)

- [ ] Program.cs
- [ ] Core/GamePathDetector.cs
- [ ] Core/GameSettings.cs
- [ ] Core/GameState.cs
- [ ] Core/GraphicsBackendFactory.cs
- [ ] Core/OdysseyGame.cs
- [ ] GUI/MenuRenderer.cs
- [ ] GUI/SaveLoadMenu.cs

### Parsing (600+ files)

**Note**: Parsing layer files typically don't need Ghidra references as they handle file format parsing, not engine behavior. However, some files that implement engine-specific logic may need references.

#### Common (18 files)

- [ ] Common/AlienSounds.cs
- [ ] Common/BinaryExtensions.cs
- [ ] Common/BinaryReader.cs
- [ ] Common/BinaryWriter.cs
- [ ] Common/CaseAwarePath.cs
- [ ] Common/Face.cs
- [ ] Common/Game.cs
- [ ] Common/GameObject.cs
- [ ] Common/Language.cs
- [ ] Common/LocalizedString.cs
- [ ] Common/Misc.cs
- [ ] Common/Module.cs
- [ ] Common/ModuleDataLoader.cs
- [ ] Common/Pathfinding.cs
- [ ] Common/ResRef.cs
- [ ] Common/SurfaceMaterial.cs
- [ ] Common/Script/DataType.cs
- [ ] Common/Script/DataTypeExtensions.cs
- [ ] Common/Script/NwscriptParser.cs
- [ ] Common/Script/ScriptConstant.cs
- [ ] Common/Script/ScriptDefs.cs
- [ ] Common/Script/ScriptFunction.cs
- [ ] Common/Script/ScriptLib.cs
- [ ] Common/Script/ScriptParam.cs

#### Extract (15 files)

- [ ] Extract/Capsule/Capsule.cs
- [ ] Extract/Capsule/LazyCapsule.cs
- [ ] Extract/Chitin/Chitin.cs
- [ ] Extract/ChitinWrapper.cs
- [ ] Extract/FileResource.cs
- [ ] Extract/FileResourceHelpers.cs
- [ ] Extract/InstallationWrapper.cs
- [ ] Extract/KeyFileWrapper.cs
- [ ] Extract/KeyWriterWrapper.cs
- [ ] Extract/SaveData/GlobalVars.cs
- [ ] Extract/SaveData/PartyTable.cs
- [ ] Extract/SaveData/SaveFolderEntry.cs
- [ ] Extract/SaveData/SaveInfo.cs
- [ ] Extract/SaveData/SaveNestedCapsule.cs
- [ ] Extract/TalkTable.cs
- [ ] Extract/TwoDAManager.cs
- [ ] Extract/TwoDARegistry.cs

#### Installation (4 files)

- [ ] Installation/Installation.cs
- [ ] Installation/InstallationResourceManager.cs
- [ ] Installation/ResourceResult.cs
- [ ] Installation/SearchLocation.cs

#### Merge (1 file)

- [ ] Merge/ModuleManager.cs

#### Resource/Formats (500+ files)

**BIF Format** (5 files)

- [ ] Resource/Formats/BIF/BIF.cs
- [ ] Resource/Formats/BIF/BIFBinaryReader.cs
- [ ] Resource/Formats/BIF/BIFBinaryWriter.cs
- [ ] Resource/Formats/BIF/BIFResource.cs
- [ ] Resource/Formats/BIF/BIFType.cs
- [ ] Resource/Formats/BIF/BZF.cs

**BWM Format** (9 files) - **HIGH PRIORITY** (walkmesh navigation)

- [ ] Resource/Formats/BWM/BWM.cs
- [ ] Resource/Formats/BWM/BWMAdjacency.cs
- [ ] Resource/Formats/BWM/BWMAuto.cs
- [ ] Resource/Formats/BWM/BWMBinaryReader.cs
- [ ] Resource/Formats/BWM/BWMBinaryWriter.cs
- [ ] Resource/Formats/BWM/BWMEdge.cs
- [ ] Resource/Formats/BWM/BWMFace.cs
- [ ] Resource/Formats/BWM/BWMMostSignificantPlane.cs
- [ ] Resource/Formats/BWM/BWMNodeAABB.cs
- [ ] Resource/Formats/BWM/BWMType.cs

**ERF Format** (4 files)

- [ ] Resource/Formats/ERF/ERF.cs
- [ ] Resource/Formats/ERF/ERFAuto.cs
- [ ] Resource/Formats/ERF/ERFBinaryReader.cs
- [ ] Resource/Formats/ERF/ERFBinaryWriter.cs
- [ ] Resource/Formats/ERF/ERFType.cs

**GFF Format** (50+ files) - **HIGH PRIORITY** (save/load, templates)

- [ ] Resource/Formats/GFF/GFF.cs
- [ ] Resource/Formats/GFF/GFFAuto.cs
- [ ] Resource/Formats/GFF/GFFBinaryReader.cs
- [ ] Resource/Formats/GFF/GFFBinaryWriter.cs
- [ ] Resource/Formats/GFF/GFFContent.cs
- [ ] Resource/Formats/GFF/GFFFieldType.cs
- [ ] Resource/Formats/GFF/GFFList.cs
- [ ] Resource/Formats/GFF/GFFStruct.cs
- [ ] Resource/Formats/GFF/Generics/* (46 files)

**KEY Format** (5 files)

- [ ] Resource/Formats/KEY/BifEntry.cs
- [ ] Resource/Formats/KEY/KEY.cs
- [ ] Resource/Formats/KEY/KEYAuto.cs
- [ ] Resource/Formats/KEY/KEYBinaryReader.cs
- [ ] Resource/Formats/KEY/KEYBinaryWriter.cs
- [ ] Resource/Formats/KEY/KeyEntry.cs

**LIP Format** (6 files) - **MEDIUM PRIORITY** (lip sync)

- [ ] Resource/Formats/LIP/LIP.cs
- [ ] Resource/Formats/LIP/LIPAuto.cs
- [ ] Resource/Formats/LIP/LIPBinaryReader.cs
- [ ] Resource/Formats/LIP/LIPBinaryWriter.cs
- [ ] Resource/Formats/LIP/LIPKeyFrame.cs
- [ ] Resource/Formats/LIP/LIPShape.cs

**LTR Format** (4 files)

- [ ] Resource/Formats/LTR/LTR.cs
- [ ] Resource/Formats/LTR/LTRAuto.cs
- [ ] Resource/Formats/LTR/LTRBinaryReader.cs
- [ ] Resource/Formats/LTR/LTRBinaryWriter.cs
- [ ] Resource/Formats/LTR/LTRBlock.cs

**LYT Format** (9 files) - **HIGH PRIORITY** (area layout)

- [ ] Resource/Formats/LYT/* (9 files)

**MDL Format** (7 files) - **HIGH PRIORITY** (3D models)

- [ ] Resource/Formats/MDL/* (7 files)

**NCS Format** (375+ files) - **HIGHEST PRIORITY** (script VM)

- [ ] Resource/Formats/NCS/* (375 files including compiler, decompiler, VM)

**RIM Format** (4 files)

- [ ] Resource/Formats/RIM/* (4 files)

**SSF Format** (5 files)

- [ ] Resource/Formats/SSF/* (5 files)

**TLK Format** (6 files) - **HIGH PRIORITY** (dialogue text)

- [ ] Resource/Formats/TLK/* (6 files)

**TPC Format** (12 files) - **HIGH PRIORITY** (textures)

- [ ] Resource/Formats/TPC/* (12 files)

**TwoDA Format** (5 files) - **HIGH PRIORITY** (game data tables)

- [ ] Resource/Formats/TwoDA/* (5 files)

**TXI Format** (7 files)

- [ ] Resource/Formats/TXI/* (7 files)

**VIS Format** (5 files) - **HIGH PRIORITY** (area visibility)

- [ ] Resource/Formats/VIS/* (5 files)

**WAV Format** (10 files)

- [ ] Resource/Formats/WAV/* (10 files)

#### Resource Core (6 files)

- [ ] Resource/ArchiveResource.cs
- [ ] Resource/ResourceAuto.cs
- [ ] Resource/ResourceAutoHelpers.cs
- [ ] Resource/ResourceFormat.cs
- [ ] Resource/ResourceIdentifier.cs
- [ ] Resource/ResourceType.cs
- [ ] Resource/Salvage.cs

#### Tools (24 files)

- [ ] Tools/Archives.cs
- [ ] Tools/Conversions.cs
- [ ] Tools/Creature.cs
- [ ] Tools/Door.cs
- [ ] Tools/Encoding.cs
- [ ] Tools/Heuristics.cs
- [ ] Tools/Kit.cs
- [ ] Tools/Misc.cs
- [ ] Tools/Model.cs
- [ ] Tools/Module.cs
- [ ] Tools/Patching.cs
- [ ] Tools/Path.cs
- [ ] Tools/Placeable.cs
- [ ] Tools/PlayPazaak.cs
- [ ] Tools/ReferenceCache.cs
- [ ] Tools/Registry.cs
- [ ] Tools/ResourceConversions.cs
- [ ] Tools/Scripts.cs
- [ ] Tools/StringUtils.cs
- [ ] Tools/Template.cs
- [ ] Tools/Utilities.cs
- [ ] Tools/Validation.cs

#### TSLPatcher (40+ files)

- [ ] TSLPatcher/Config/LogLevel.cs
- [ ] TSLPatcher/Config/PatcherConfig.cs
- [ ] TSLPatcher/Diff/DiffAnalyzerFactory.cs
- [ ] TSLPatcher/Diff/DiffEngine.cs
- [ ] TSLPatcher/Diff/DiffHelpers.cs
- [ ] TSLPatcher/Diff/GffDiff.cs
- [ ] TSLPatcher/Diff/GffDiffAnalyzer.cs
- [ ] TSLPatcher/Diff/Resolution.cs
- [ ] TSLPatcher/Diff/SsfDiff.cs
- [ ] TSLPatcher/Diff/TlkDiff.cs
- [ ] TSLPatcher/Diff/TwoDaDiff.cs
- [ ] TSLPatcher/Diff/TwoDaDiffAnalyzer.cs
- [ ] TSLPatcher/GeneratorValidation.cs
- [ ] TSLPatcher/IncrementalTSLPatchDataWriter.cs
- [ ] TSLPatcher/INIManager.cs
- [ ] TSLPatcher/InstallFolderDeterminer.cs
- [ ] TSLPatcher/Logger/InstallLogWriter.cs
- [ ] TSLPatcher/Logger/LogType.cs
- [ ] TSLPatcher/Logger/PatchLog.cs
- [ ] TSLPatcher/Logger/PatchLogger.cs
- [ ] TSLPatcher/Logger/RobustLogger.cs
- [ ] TSLPatcher/Memory/PatcherMemory.cs
- [ ] TSLPatcher/Memory/TokenUsage.cs
- [ ] TSLPatcher/ModInstaller.cs
- [ ] TSLPatcher/Mods/GFF/* (3 files)
- [ ] TSLPatcher/Mods/InstallFile.cs
- [ ] TSLPatcher/Mods/ModificationsByType.cs
- [ ] TSLPatcher/Mods/NCS/* (1 file)
- [ ] TSLPatcher/Mods/NSS/* (1 file)
- [ ] TSLPatcher/Mods/PatcherModifications.cs
- [ ] TSLPatcher/Mods/SSF/* (1 file)
- [ ] TSLPatcher/Mods/TLK/* (1 file)
- [ ] TSLPatcher/Mods/TSLPatcherINISerializer.cs
- [ ] TSLPatcher/Mods/TwoDA/* (4 files)
- [ ] TSLPatcher/PatcherNamespace.cs
- [ ] TSLPatcher/Reader/ConfigReader.cs
- [ ] TSLPatcher/Reader/NamespaceReader.cs
- [ ] TSLPatcher/TSLPatchDataGenerator.cs
- [ ] TSLPatcher/Uninstall/ModUninstaller.cs
- [ ] TSLPatcher/Uninstall/UninstallHelpers.cs

### Utility (14 files)

**Note**: Utility files typically don't need Ghidra references as they are helper/utility code.

- [ ] Utility/ArrayHead.cs
- [ ] Utility/CaseInsensitiveDict.cs
- [ ] Utility/ErrorHandling.cs
- [ ] Utility/Geometry/GeometryUtils.cs
- [ ] Utility/Geometry/Polygon2.cs
- [ ] Utility/Geometry/Polygon3.cs
- [ ] Utility/Geometry/Quaternion.cs
- [ ] Utility/KeyError.cs
- [ ] Utility/LZMA/LzmaHelper.cs
- [ ] Utility/Misc.cs
- [ ] Utility/MiscString/CaseInsensImmutableStr.cs
- [ ] Utility/MiscString/StringUtilFunctions.cs
- [ ] Utility/MiscString/WrappedStr.cs
- [ ] Utility/OrderedSet.cs
- [ ] Utility/System/OSHelper.cs
- [ ] Utility/SystemHelpers.cs

## Reverse Engineering Requirements

### Files Requiring Ghidra References

**MANDATORY**: All files in the following directories MUST have Ghidra references:

1. **Runtime/Core** - All files (99 files)
   - These implement core engine behavior
   - Must match original engine behavior exactly
   - Every function should reference Ghidra function addresses

2. **Runtime/Games/Odyssey** - All files (84 files)
   - Primary implementation target
   - Must achieve 1:1 parity with swkotor.exe/swkotor2.exe
   - Every system must be reverse engineered

3. **Runtime/Scripting** - All files (11 files)
   - NCS VM implementation
   - Engine API functions
   - Must match original script execution behavior

4. **Runtime/Content** - Selected files
   - Loaders/GITLoader.cs (entity spawning)
   - Loaders/TemplateLoader.cs (template loading)
   - Save/SaveSerializer.cs (save/load system)
   - Save/SaveDataProvider.cs (save data handling)
   - Converters/BwmToNavigationMeshConverter.cs (walkmesh)

5. **Parsing/Extract/SaveData** - Selected files
   - SaveData/GlobalVars.cs (global variable system)
   - SaveData/PartyTable.cs (party management)
   - SaveData/SaveInfo.cs (save metadata)

6. **Parsing/Resource/Formats** - Selected files
   - BWM/* (walkmesh format - must match engine pathfinding)
   - GFF/* (save format - must match engine serialization)
   - NCS/* (script format - must match VM behavior)

### Files NOT Requiring Ghidra References

These files are format parsers, utilities, or modern enhancements:

1. **Parsing/Resource/Formats** - Most format parsers
   - File format parsing doesn't need engine references
   - Exception: BWM, GFF, NCS (see above)

2. **Utility/** - All files
   - Helper/utility code
   - No engine behavior to match

3. **Runtime/Graphics/** - Most files
   - Modern MonoGame/Stride adapters
   - Original engines used DirectX/OpenGL
   - Note enhancements vs. original behavior

4. **TSLPatcher/** - All files
   - Modding tool, not engine code

5. **Parsing/Tools/** - All files
   - Utility tools for format manipulation

### Exhaustive Reverse Engineering Checklist

For each file requiring Ghidra references:

- [ ] **Search Ghidra** for relevant functions using:
  - String searches (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
  - Function name searches
  - Cross-references from known functions
  - Data references

- [ ] **Decompile** all relevant functions in:
  - swkotor.exe (KotOR 1 behavior)
  - swkotor2.exe (KotOR 2 behavior - PRIMARY)
  - nwmain.exe (Aurora reference, if applicable)
  - daorigins.exe (Eclipse reference, if applicable)

- [ ] **Document in Ghidra**:
  - Rename functions with descriptive names
  - Set accurate function prototypes
  - Rename variables and data labels
  - Add comprehensive comments
  - Track analysis status ([STATUS: ANALYZED], [STATUS: TODO], [C#: IMPLEMENTED])

- [ ] **Document in C# Code**:
  - Add comments with Ghidra executable name and function address
  - Include function name from Ghidra (descriptive, not FUN_xxxxx)
  - Include string references used to locate function
  - Include key implementation details from decompiled code
  - Note any deviations or improvements

- [ ] **Verify Implementation**:
  - Match original engine behavior exactly
  - Test with actual game assets
  - Verify edge cases and error handling
  - Ensure C# 7.3 compatibility

### Priority Order for Reverse Engineering

1. **Phase 1: Core Systems** (swkotor2.exe PRIMARY)
   - Module loading (LYT, VIS, GIT, ARE)
   - Walkmesh navigation (BWM parsing and pathfinding)
   - Entity spawning and management
   - Script execution (NCS VM)
   - Save/Load system (GFF serialization)

2. **Phase 2: Gameplay Systems** (swkotor2.exe PRIMARY)
   - Combat system
   - Dialogue system (DLG, TLK, VO)
   - Party management
   - Perception system
   - Trigger system
   - Encounter system
   - Store system

3. **Phase 3: Presentation Systems** (swkotor.exe + swkotor2.exe)
   - Animation system
   - Audio system
   - Camera system
   - Rendering (reference only, modern implementation)

4. **Phase 4: Cross-Engine Unification** (All engines)
   - Aurora engine (nwmain.exe) - architecture reference
   - Eclipse engine (daorigins.exe, DragonAge2.exe) - architecture reference
   - Infinity engine - architecture reference (if available)

## Notes

- **PRIMARY TARGET**: swkotor2.exe is the most complete Odyssey implementation - use as primary reference
- Focus on core game logic first (Runtime/Core, Runtime/Games/Odyssey, Runtime/Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo", "BWM", "walkmesh", "pathfind")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented
- Modern graphics enhancements (DLSS, FSR, RTX Remix, raytracing) are not in original game - note as enhancements
- **EXHAUSTIVE REQUIREMENT**: Every function in Runtime/Core, Runtime/Games/Odyssey, and Runtime/Scripting MUST have Ghidra references
- **GHIDRA DOCUMENTATION**: All analyzed functions MUST be documented in Ghidra with descriptive names, prototypes, and comments before documenting in C# code
