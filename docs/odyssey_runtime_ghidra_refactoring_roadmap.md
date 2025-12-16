# OdysseyRuntime Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: IN PROGRESS
**Started**: 2025-01-15
**Current Phase**: Systematic file-by-file review with Ghidra MCP verification
**Total Files**: 414

## Refactoring Strategy

1. Search Ghidra for relevant functions using string searches and function name searches
2. Decompile relevant functions to understand original implementation
3. Add detailed comments with Ghidra function addresses and context
4. Update implementation to match original behavior where possible
5. Document any deviations or improvements

## Update Instructions

When processing a file:

- Mark as - [/] when starting work
- Mark as - [x] when complete with Ghidra references added
- Add notes about function addresses, string references, and implementation details
- Use format: - [x] FileName.cs - Function addresses, string references, key findings

## Files to Process

### Odyssey.Content (18 files)

- [x] Cache\ContentCache.cs - CACHE @ 0x007c6848, z:\cache @ 0x007c6850, CExoKeyTable resource management
- [x] Converters\BwmToNavigationMeshConverter.cs - nwsareapathfind.cpp @ 0x007be3ff, pathfinding errors @ 0x007be510, 0x007c03c0, 0x007c0408
- [x] Interfaces\IContentCache.cs - CACHE @ 0x007c6848, z:\cache @ 0x007c6850, CExoKeyTable resource management
- [x] Interfaces\IContentConverter.cs - Resource @ 0x007c14d4, Loading @ 0x007c7e40, CExoKeyTable resource loading
- [x] Interfaces\IGameResourceProvider.cs - Resource @ 0x007c14d4, FUN_00633270 @ 0x00633270, CExoKeyTable resource management
- [x] Interfaces\IResourceProvider.cs - Resource @ 0x007c14d4, CExoKeyTable @ 0x007b6078, FUN_00633270 @ 0x00633270
- [x] Loaders\GITLoader.cs - Already has comprehensive Ghidra references (31 matches)
- [x] Loaders\TemplateLoader.cs - Already has comprehensive Ghidra references (16 matches)
- [x] MDL\MDLBulkReader.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLCache.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLConstants.cs - DoubleMdlVar @ 0x007d05d8, ShortMdlVar @ 0x007d05e8, LongMdlVar @ 0x007d05f4, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLDataTypes.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLFastReader.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLLoader.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] MDL\MDLOptimizedReader.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] ResourceProviders\GameResourceProvider.cs - Resource @ 0x007c14d4, CExoKeyTable @ 0x007b6078, 0x007b6124
- [x] Save\SaveDataProvider.cs - Already has comprehensive Ghidra references (26 matches: FUN_004eb750, FUN_00708990, savenfo @ 0x007be1f0, SAVES: @ 0x007be284)
- [x] Save\SaveSerializer.cs - Already has comprehensive Ghidra references (48 matches: FUN_004eb750, FUN_005ac670, FUN_0057bd70, savenfo @ 0x007be1f0)

### Odyssey.Core (99 files)

- [ ] Actions\ActionAttack.cs
- [ ] Actions\ActionBase.cs
- [ ] Actions\ActionCastSpellAtLocation.cs
- [ ] Actions\ActionCastSpellAtObject.cs
- [ ] Actions\ActionCloseDoor.cs
- [ ] Actions\ActionDestroyObject.cs
- [ ] Actions\ActionDoCommand.cs
- [ ] Actions\ActionEquipItem.cs
- [ ] Actions\ActionFollowObject.cs
- [ ] Actions\ActionJumpToLocation.cs
- [ ] Actions\ActionJumpToObject.cs
- [ ] Actions\ActionMoveAwayFromObject.cs
- [ ] Actions\ActionMoveToLocation.cs
- [ ] Actions\ActionMoveToObject.cs
- [ ] Actions\ActionOpenDoor.cs
- [ ] Actions\ActionPickUpItem.cs
- [ ] Actions\ActionPlayAnimation.cs
- [ ] Actions\ActionPutDownItem.cs
- [ ] Actions\ActionQueue.cs
- [ ] Actions\ActionRandomWalk.cs
- [ ] Actions\ActionSpeakString.cs
- [ ] Actions\ActionUnequipItem.cs
- [ ] Actions\ActionUseItem.cs
- [ ] Actions\ActionUseObject.cs
- [ ] Actions\ActionWait.cs
- [ ] Actions\DelayScheduler.cs
- [ ] AI\AIController.cs
- [ ] Animation\AnimationSystem.cs
- [ ] Audio\ISoundPlayer.cs
- [ ] Camera\CameraController.cs
- [ ] Combat\CombatSystem.cs
- [ ] Combat\CombatTypes.cs
- [ ] Combat\EffectSystem.cs
- [ ] Dialogue\DialogueInterfaces.cs
- [ ] Dialogue\DialogueSystem.cs
- [ ] Dialogue\LipSyncController.cs
- [ ] Dialogue\RuntimeDialogue.cs
- [ ] Entities\Entity.cs
- [ ] Entities\EventBus.cs
- [ ] Entities\TimeManager.cs
- [ ] Entities\World.cs
- [ ] Enums\Ability.cs
- [ ] Enums\ActionStatus.cs
- [ ] Enums\ActionType.cs
- [ ] Enums\ObjectType.cs
- [ ] Enums\ScriptEvent.cs
- [ ] GameLoop\FixedTimestepGameLoop.cs
- [ ] GameSettings.cs
- [ ] Interfaces\Components\IActionQueueComponent.cs
- [ ] Interfaces\Components\IAnimationComponent.cs
- [ ] Interfaces\Components\IDoorComponent.cs
- [ ] Interfaces\Components\IFactionComponent.cs
- [ ] Interfaces\Components\IInventoryComponent.cs
- [ ] Interfaces\Components\IItemComponent.cs
- [ ] Interfaces\Components\IPerceptionComponent.cs
- [ ] Interfaces\Components\IPlaceableComponent.cs
- [ ] Interfaces\Components\IQuickSlotComponent.cs
- [ ] Interfaces\Components\IRenderableComponent.cs
- [ ] Interfaces\Components\IScriptHooksComponent.cs
- [ ] Interfaces\Components\IStatsComponent.cs
- [ ] Interfaces\Components\ITransformComponent.cs
- [ ] Interfaces\Components\ITriggerComponent.cs
- [ ] Interfaces\IAction.cs
- [ ] Interfaces\IActionQueue.cs
- [ ] Interfaces\IArea.cs
- [ ] Interfaces\IComponent.cs
- [x] Interfaces\IDelayScheduler.cs - DelayCommand @ 0x007be900, Delay @ 0x007c35b0, DelayReply @ 0x007c38f0, STORE_STATE opcode
- [ ] Interfaces\IEntity.cs
- [ ] Interfaces\IEventBus.cs
- [ ] Interfaces\IGameServicesContext.cs
- [ ] Interfaces\IModule.cs
- [ ] Interfaces\INavigationMesh.cs
- [ ] Interfaces\ITimeManager.cs
- [ ] Interfaces\IWorld.cs
- [ ] Journal\JournalSystem.cs
- [ ] Module\ModuleTransitionSystem.cs
- [ ] Module\RuntimeArea.cs
- [ ] Module\RuntimeModule.cs
- [ ] Movement\CharacterController.cs
- [ ] Movement\PlayerInputHandler.cs
- [ ] Navigation\NavigationMesh.cs
- [ ] Navigation\NavigationMeshFactory.cs
- [ ] Party\PartyInventory.cs
- [ ] Party\PartyMember.cs
- [ ] Party\PartySystem.cs
- [ ] Perception\PerceptionSystem.cs
- [ ] Save\AreaState.cs
- [ ] Save\SaveGameData.cs
- [ ] Save\SaveSystem.cs
- [ ] Templates\CreatureTemplate.cs
- [ ] Templates\DoorTemplate.cs
- [ ] Templates\EncounterTemplate.cs
- [ ] Templates\IEntityTemplate.cs
- [ ] Templates\PlaceableTemplate.cs
- [ ] Templates\SoundTemplate.cs
- [ ] Templates\StoreTemplate.cs
- [ ] Templates\TriggerTemplate.cs
- [ ] Templates\WaypointTemplate.cs
- [ ] Triggers\TriggerSystem.cs

### Odyssey.Engines.Aurora (1 files)

- [x] AuroraEngine.cs - Note: Different game engine (NWN/NWN2), no swkotor2.exe references needed

### Odyssey.Engines.Common (8 files)

- [x] BaseEngine.cs - FUN_00404250 @ 0x00404250, CExoKeyTable resource management
- [x] BaseEngineGame.cs - ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, FUN_006caab0 @ 0x006caab0
- [x] BaseEngineModule.cs - ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, FUN_006caab0 @ 0x006caab0
- [x] BaseEngineProfile.cs - Game profile system, resource config, table config
- [x] IEngine.cs - FUN_00404250 @ 0x00404250, CExoKeyTable resource management
- [x] IEngineGame.cs - ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, FUN_006caab0 @ 0x006caab0
- [x] IEngineModule.cs - ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, FUN_006caab0 @ 0x006caab0
- [x] IEngineProfile.cs - Game profile system, resource config, table config

### Odyssey.Engines.Eclipse (1 files)

- [x] EclipseEngine.cs - Note: Different game engine (DA/ME), no swkotor2.exe references needed

### Odyssey.Engines.Odyssey (7 files)

- [ ] EngineApi\OdysseyK1EngineApi.cs
- [ ] EngineApi\OdysseyK2EngineApi.cs
- [x] OdysseyEngine.cs - FUN_00404250 @ 0x00404250, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, FUN_006caab0 @ 0x006caab0, FUN_00633270 @ 0x00633270
- [ ] OdysseyGameSession.cs
- [ ] OdysseyModuleLoader.cs
- [ ] Profiles\OdysseyK1GameProfile.cs
- [ ] Profiles\OdysseyK2GameProfile.cs

### Odyssey.Game (8 files)

- [x] Core\GamePathDetector.cs - Registry access patterns, chitin.key validation, executable validation
- [x] Core\GameSettings.cs - FUN_00633270 @ 0x00633270, FUN_00630a90 @ 0x00630a90, FUN_00631ea0 @ 0x00631ea0, swkotor2.ini @ 0x007b5740
- [x] Core\GameState.cs - FUN_006caab0 @ 0x006caab0, ModuleLoaded @ 0x007bdd70, ModuleRunning @ 0x007bdd58, GameState @ 0x007c15d0
- [x] Core\GraphicsBackendFactory.cs - Abstraction layer factory, no Ghidra references needed
- [x] Core\OdysseyGame.cs - FUN_00404250 @ 0x00404250, UpdateScenes @ 0x007b8b54, frameStart @ 0x007ba698, frameEnd @ 0x007ba668
- [x] GUI\MenuRenderer.cs - RIMS:MAINMENU @ 0x007b6044, mainmenu_p @ 0x007cc000, MAINMENU @ 0x007cc030, mainmenu01-05 variants
- [x] GUI\SaveLoadMenu.cs - savenfo @ 0x007be1f0, FUN_004eb750 @ 0x004eb750, FUN_00708990 @ 0x00708990, FUN_0070a020 @ 0x0070a020
- [x] Program.cs - FUN_00404250 @ 0x00404250, mutex "swkotor2" @ 0x007b575c, config.txt loading FUN_00460ff0, INI loading FUN_00630a90/FUN_00631ea0

### Odyssey.Graphics (22 files)

- [ ] GraphicsBackend.cs
- [ ] IContentManager.cs
- [ ] IDepthStencilBuffer.cs
- [ ] IEffect.cs
- [ ] IEntityModelRenderer.cs
- [ ] IFont.cs
- [ ] IGraphicsBackend.cs
- [ ] IGraphicsDevice.cs
- [ ] IIndexBuffer.cs
- [ ] IInputManager.cs
- [ ] IModel.cs
- [ ] IRenderState.cs
- [ ] IRenderTarget.cs
- [ ] IRoomMeshRenderer.cs
- [ ] ISpatialAudio.cs
- [ ] ISpriteBatch.cs
- [ ] ITexture2D.cs
- [ ] IVertexBuffer.cs
- [ ] IVertexDeclaration.cs
- [ ] IWindow.cs
- [ ] MatrixHelper.cs
- [ ] VertexPositionColor.cs

### Odyssey.Kotor (56 files)

- [ ] Combat\CombatManager.cs
- [ ] Combat\CombatRound.cs
- [ ] Combat\DamageCalculator.cs
- [ ] Combat\WeaponDamageCalculator.cs
- [ ] Components\ActionQueueComponent.cs
- [ ] Components\CreatureComponent.cs
- [ ] Components\DoorComponent.cs
- [ ] Components\EncounterComponent.cs
- [ ] Components\FactionComponent.cs
- [ ] Components\InventoryComponent.cs
- [ ] Components\ItemComponent.cs
- [ ] Components\PerceptionComponent.cs
- [ ] Components\PlaceableComponent.cs
- [x] Components\QuickSlotComponent.cs - FUN_005226d0 @ 0x005226d0, FUN_005223a0 @ 0x005223a0, QuickSlot_0-11 fields in UTC GFF
- [ ] Components\RenderableComponent.cs
- [ ] Components\ScriptHooksComponent.cs
- [ ] Components\SoundComponent.cs
- [ ] Components\StatsComponent.cs
- [ ] Components\StoreComponent.cs
- [ ] Components\TransformComponent.cs
- [ ] Components\TriggerComponent.cs
- [ ] Components\WaypointComponent.cs
- [ ] Data\GameDataManager.cs
- [ ] Data\TwoDATableManager.cs
- [ ] Dialogue\ConversationContext.cs
- [ ] Dialogue\DialogueManager.cs
- [ ] Dialogue\DialogueState.cs
- [ ] Dialogue\KotorDialogueLoader.cs
- [ ] Dialogue\KotorLipDataLoader.cs
- [ ] EngineApi\K1EngineApi.cs
- [ ] EngineApi\K2EngineApi.cs
- [ ] Game\GameSession.cs
- [ ] Game\ModuleLoader.cs
- [ ] Game\ModuleTransitionSystem.cs
- [ ] Game\PlayerController.cs
- [ ] Game\ScriptExecutor.cs
- [ ] Input\PlayerController.cs
- [ ] Loading\EntityFactory.cs
- [ ] Loading\KotorModuleLoader.cs
- [ ] Loading\ModuleLoader.cs
- [ ] Loading\NavigationMeshFactory.cs
- [ ] Profiles\GameProfileFactory.cs
- [ ] Profiles\IGameProfile.cs
- [ ] Profiles\K1GameProfile.cs
- [ ] Profiles\K2GameProfile.cs
- [ ] Save\SaveGameManager.cs
- [ ] Systems\AIController.cs
- [ ] Systems\ComponentInitializer.cs
- [ ] Systems\EncounterSystem.cs
- [ ] Systems\FactionManager.cs
- [ ] Systems\HeartbeatSystem.cs
- [ ] Systems\ModelResolver.cs
- [ ] Systems\PartyManager.cs
- [ ] Systems\PerceptionManager.cs
- [ ] Systems\StoreSystem.cs
- [ ] Systems\TriggerSystem.cs

### Odyssey.MonoGame (159 files)

- [ ] Animation\AnimationCompression.cs
- [ ] Animation\SkeletalAnimationBatching.cs
- [ ] Assets\AssetHotReload.cs
- [ ] Assets\AssetValidator.cs
- [ ] Audio\MonoGameSoundPlayer.cs
- [ ] Audio\MonoGameVoicePlayer.cs
- [ ] Audio\SpatialAudio.cs
- [ ] Backends\BackendFactory.cs
- [ ] Backends\Direct3D10Backend.cs
- [ ] Backends\Direct3D11Backend.cs
- [ ] Backends\Direct3D12Backend.cs
- [ ] Backends\OpenGLBackend.cs
- [ ] Backends\VulkanBackend.cs
- [ ] Camera\ChaseCamera.cs
- [ ] Camera\MonoGameDialogueCameraController.cs
- [ ] Compute\ComputeShaderFramework.cs
- [ ] Converters\MdlToMonoGameModelConverter.cs
- [ ] Converters\RoomMeshRenderer.cs
- [ ] Converters\TpcToMonoGameTextureConverter.cs
- [ ] Culling\DistanceCuller.cs
- [ ] Culling\Frustum.cs
- [ ] Culling\GPUCulling.cs
- [ ] Culling\OcclusionCuller.cs
- [ ] Debug\DebugRendering.cs
- [ ] Debug\RenderStatistics.cs
- [ ] Enums\GraphicsBackend.cs
- [ ] Enums\MaterialType.cs
- [ ] Graphics\MonoGameBasicEffect.cs
- [ ] Graphics\MonoGameContentManager.cs
- [ ] Graphics\MonoGameDepthStencilBuffer.cs
- [ ] Graphics\MonoGameEntityModelRenderer.cs
- [ ] Graphics\MonoGameFont.cs
- [ ] Graphics\MonoGameGraphicsBackend.cs
- [ ] Graphics\MonoGameGraphicsDevice.cs
- [ ] Graphics\MonoGameIndexBuffer.cs
- [ ] Graphics\MonoGameInputManager.cs
- [ ] Graphics\MonoGameRenderState.cs
- [ ] Graphics\MonoGameRenderTarget.cs
- [ ] Graphics\MonoGameRoomMeshRenderer.cs
- [ ] Graphics\MonoGameSpatialAudio.cs
- [ ] Graphics\MonoGameSpriteBatch.cs
- [ ] Graphics\MonoGameTexture2D.cs
- [ ] Graphics\MonoGameVertexBuffer.cs
- [ ] Graphics\MonoGameWindow.cs
- [ ] GUI\KotorGuiManager.cs
- [ ] GUI\MyraMenuRenderer.cs
- [ ] Interfaces\ICommandList.cs
- [ ] Interfaces\IDevice.cs
- [ ] Interfaces\IDynamicLight.cs
- [ ] Interfaces\IGraphicsBackend.cs
- [ ] Interfaces\IPbrMaterial.cs
- [ ] Interfaces\IRaytracingSystem.cs
- [ ] Lighting\ClusteredLightCulling.cs
- [ ] Lighting\ClusteredLightingSystem.cs
- [ ] Lighting\DynamicLight.cs
- [ ] Lighting\LightProbeSystem.cs
- [ ] Lighting\VolumetricLighting.cs
- [ ] Loading\AsyncResourceLoader.cs
- [ ] LOD\LODFadeSystem.cs
- [ ] LOD\LODSystem.cs
- [ ] Materials\KotorMaterialConverter.cs
- [ ] Materials\KotorMaterialFactory.cs
- [ ] Materials\MaterialInstancing.cs
- [ ] Materials\PbrMaterial.cs
- [ ] Memory\GPUMemoryPool.cs
- [ ] Memory\MemoryTracker.cs
- [ ] Memory\ObjectPool.cs
- [ ] Models\MDLModelConverter.cs
- [ ] Particles\GPUParticleSystem.cs
- [ ] Particles\ParticleSorter.cs
- [ ] Performance\FramePacing.cs
- [ ] Performance\FrameTimeBudget.cs
- [ ] Performance\GPUTimestamps.cs
- [ ] Performance\Telemetry.cs
- [ ] PostProcessing\Bloom.cs
- [ ] PostProcessing\ColorGrading.cs
- [ ] PostProcessing\ExposureAdaptation.cs
- [ ] PostProcessing\MotionBlur.cs
- [ ] PostProcessing\SSAO.cs
- [ ] PostProcessing\SSR.cs
- [ ] PostProcessing\TemporalAA.cs
- [ ] PostProcessing\ToneMapping.cs
- [ ] Raytracing\NativeRaytracingSystem.cs
- [ ] Raytracing\RaytracedEffects.cs
- [ ] Remix\Direct3D9Wrapper.cs
- [ ] Remix\RemixBridge.cs
- [ ] Remix\RemixMaterialExporter.cs
- [ ] Rendering\AdaptiveQuality.cs
- [ ] Rendering\BatchOptimizer.cs
- [ ] Rendering\BindlessTextures.cs
- [ ] Rendering\CommandBuffer.cs
- [ ] Rendering\CommandListOptimizer.cs
- [ ] Rendering\ContactShadows.cs
- [ ] Rendering\DecalSystem.cs
- [ ] Rendering\DeferredRenderer.cs
- [ ] Rendering\DepthPrePass.cs
- [ ] Rendering\DrawCallSorter.cs
- [ ] Rendering\DynamicBatching.cs
- [ ] Rendering\DynamicResolution.cs
- [ ] Rendering\EntityModelRenderer.cs
- [ ] Rendering\FrameGraph.cs
- [ ] Rendering\GeometryCache.cs
- [ ] Rendering\GeometryStreaming.cs
- [ ] Rendering\GPUInstancing.cs
- [ ] Rendering\GPUMemoryBudget.cs
- [ ] Rendering\GPUMemoryDefragmentation.cs
- [ ] Rendering\GPUSynchronization.cs
- [ ] Rendering\HDRPipeline.cs
- [ ] Rendering\IndirectRenderer.cs
- [ ] Rendering\MemoryAliasing.cs
- [ ] Rendering\MeshCompression.cs
- [ ] Rendering\ModernRenderer.cs
- [ ] Rendering\MultiThreadedRenderer.cs
- [ ] Rendering\MultiThreadedRendering.cs
- [ ] Rendering\OcclusionQueries.cs
- [ ] Rendering\OdysseyRenderer.cs
- [ ] Rendering\PipelineStateCache.cs
- [ ] Rendering\QualityPresets.cs
- [ ] Rendering\RenderBatchManager.cs
- [ ] Rendering\RenderGraph.cs
- [ ] Rendering\RenderOptimizer.cs
- [ ] Rendering\RenderPipeline.cs
- [ ] Rendering\RenderProfiler.cs
- [ ] Rendering\RenderQueue.cs
- [ ] Rendering\RenderSettings.cs
- [ ] Rendering\RenderTargetCache.cs
- [ ] Rendering\RenderTargetChain.cs
- [ ] Rendering\RenderTargetManager.cs
- [ ] Rendering\RenderTargetPool.cs
- [ ] Rendering\RenderTargetScaling.cs
- [ ] Rendering\ResourceBarriers.cs
- [ ] Rendering\ResourcePreloader.cs
- [ ] Rendering\SceneGraph.cs
- [ ] Rendering\ShaderCache.cs
- [ ] Rendering\StateCache.cs
- [ ] Rendering\SubsurfaceScattering.cs
- [ ] Rendering\TemporalReprojection.cs
- [ ] Rendering\TextureAtlas.cs
- [ ] Rendering\TextureCompression.cs
- [ ] Rendering\TriangleStripGenerator.cs
- [ ] Rendering\Upscaling\DLSS.cs
- [ ] Rendering\Upscaling\FSR.cs
- [ ] Rendering\VariableRateShading.cs
- [ ] Rendering\VertexCacheOptimizer.cs
- [ ] Rendering\VisibilityBuffer.cs
- [ ] Save\AsyncSaveSystem.cs
- [ ] Scene\SceneBuilder.cs
- [ ] Shaders\ShaderCache.cs
- [ ] Shaders\ShaderPermutationSystem.cs
- [ ] Shadows\CascadedShadowMaps.cs
- [ ] Spatial\Octree.cs
- [ ] Textures\TextureFormatConverter.cs
- [ ] Textures\TextureStreamingManager.cs
- [ ] UI\BasicHUD.cs
- [ ] UI\DialoguePanel.cs
- [ ] UI\LoadingScreen.cs
- [ ] UI\MainMenu.cs
- [ ] UI\PauseMenu.cs
- [ ] UI\ScreenFade.cs

### Odyssey.Scripting (11 files)

- [ ] EngineApi\BaseEngineApi.cs
- [ ] Interfaces\IEngineApi.cs
- [ ] Interfaces\IExecutionContext.cs
- [ ] Interfaces\INcsVm.cs
- [ ] Interfaces\IScriptGlobals.cs
- [ ] Interfaces\Variable.cs
- [ ] ScriptExecutor.cs
- [ ] Types\Location.cs
- [ ] VM\ExecutionContext.cs
- [ ] VM\NcsVm.cs
- [ ] VM\ScriptGlobals.cs

### Odyssey.Stride (20 files)

- [ ] Audio\StrideSoundPlayer.cs
- [ ] Audio\StrideVoicePlayer.cs
- [ ] Camera\StrideDialogueCameraController.cs
- [ ] Graphics\StrideBasicEffect.cs
- [ ] Graphics\StrideContentManager.cs
- [ ] Graphics\StrideDepthStencilBuffer.cs
- [ ] Graphics\StrideEntityModelRenderer.cs
- [ ] Graphics\StrideFont.cs
- [ ] Graphics\StrideGraphicsBackend.cs
- [ ] Graphics\StrideGraphicsDevice.cs
- [ ] Graphics\StrideIndexBuffer.cs
- [ ] Graphics\StrideInputManager.cs
- [ ] Graphics\StrideRenderState.cs
- [ ] Graphics\StrideRenderTarget.cs
- [ ] Graphics\StrideRoomMeshRenderer.cs
- [ ] Graphics\StrideSpatialAudio.cs
- [ ] Graphics\StrideSpriteBatch.cs
- [ ] Graphics\StrideTexture2D.cs
- [ ] Graphics\StrideVertexBuffer.cs
- [ ] Graphics\StrideWindow.cs

### Odyssey.Tests (3 files)

- [ ] UI\FallbackUITests.cs
- [ ] UI\KotorGuiManagerTests.cs
- [ ] VM\NcsVmTests.cs

### Odyssey.Tooling (1 files)

- [ ] Program.cs

---

## Engine Abstraction Refactoring

**Status**: IN PROGRESS  
**Started**: 2025-01-15  
**Current Phase**: Identifying KOTOR-specific code in AuroraEngine.Common  
**Goal**: Move all KOTOR/Odyssey-specific code from AuroraEngine.Common to Odyssey.Engines.Odyssey

### Strategy

1. Identify KOTOR-specific code in AuroraEngine.Common
2. Move to Odyssey.Engines.Odyssey (shared KOTOR code)
3. Create engine-specific projects following xoreos pattern
4. Maximize code in base classes, minimize duplication
5. Ensure 1:1 parity with original KOTOR 2 engine (Ghidra verification)

### Files to Move from AuroraEngine.Common

#### Common/ (KOTOR-Specific)

- [ ] Common\Game.cs - Game enum (K1, K2, etc.) and extensions
  - Target: Odyssey.Engines.Odyssey.Common\Game.cs
  - Status: [ ] Identify all usages, [ ] Move, [ ] Update references

- [ ] Common\Module.cs - KModuleType enum and Module class
  - Target: Odyssey.Engines.Odyssey.Module\Module.cs
  - Status: [ ] Identify all usages, [ ] Move, [ ] Update references

- [ ] Common\ModuleDataLoader.cs - KOTOR-specific module data loading
  - Target: Odyssey.Engines.Odyssey.Module\ModuleDataLoader.cs
  - Status: [ ] Review if KOTOR-specific, [ ] Move if needed

#### Resource/Generics/ (KOTOR/Odyssey GFF Templates)

**Entity Templates (KOTOR-specific - Move to Odyssey.Engines.Odyssey.Templates\):**

- [ ] Resource\Generics\UTC.cs - Creature template
  - Target: Odyssey.Engines.Odyssey.Templates\UTC.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTCHelpers.cs - Creature template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTCHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTD.cs - Door template
  - Target: Odyssey.Engines.Odyssey.Templates\UTD.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTDHelpers.cs - Door template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTDHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTE.cs - Encounter template
  - Target: Odyssey.Engines.Odyssey.Templates\UTE.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTEHelpers.cs - Encounter template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTEHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTI.cs - Item template
  - Target: Odyssey.Engines.Odyssey.Templates\UTI.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTIHelpers.cs - Item template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTIHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTM.cs - Merchant template
  - Target: Odyssey.Engines.Odyssey.Templates\UTM.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTMHelpers.cs - Merchant template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTMHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTP.cs - Placeable template
  - Target: Odyssey.Engines.Odyssey.Templates\UTP.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTPHelpers.cs - Placeable template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTPHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTS.cs - Sound template
  - Target: Odyssey.Engines.Odyssey.Templates\UTS.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTSHelpers.cs - Sound template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTSHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTT.cs - Trigger template
  - Target: Odyssey.Engines.Odyssey.Templates\UTT.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTTAuto.cs - Trigger template auto-generated code
  - Target: Odyssey.Engines.Odyssey.Templates\UTTAuto.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTTHelpers.cs - Trigger template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTTHelpers.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTW.cs - Waypoint template
  - Target: Odyssey.Engines.Odyssey.Templates\UTW.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTWAuto.cs - Waypoint template auto-generated code
  - Target: Odyssey.Engines.Odyssey.Templates\UTWAuto.cs
  - Status: [ ] Move, [ ] Update references

- [ ] Resource\Generics\UTWHelpers.cs - Waypoint template helpers
  - Target: Odyssey.Engines.Odyssey.Templates\UTWHelpers.cs
  - Status: [ ] Move, [ ] Update references

**Module/Area Structures (Review - May be KOTOR-specific):**

- [ ] Resource\Generics\IFO.cs - Module info (review if KOTOR-specific)
  - Target: Odyssey.Engines.Odyssey.Module\IFO.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\IFOHelpers.cs - Module info helpers
  - Target: Odyssey.Engines.Odyssey.Module\IFOHelpers.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\ARE.cs - Area (review if KOTOR-specific)
  - Target: Odyssey.Engines.Odyssey.Module\ARE.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\AREHelpers.cs - Area helpers
  - Target: Odyssey.Engines.Odyssey.Module\AREHelpers.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GIT.cs - Game instance template (review if KOTOR-specific)
  - Target: Odyssey.Engines.Odyssey.Module\GIT.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GITHelpers.cs - Game instance template helpers
  - Target: Odyssey.Engines.Odyssey.Module\GITHelpers.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\JRL.cs - Journal (review if KOTOR-specific)
  - Target: Odyssey.Engines.Odyssey.Module\JRL.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\PTH.cs - Path (review if KOTOR-specific)
  - Target: Odyssey.Engines.Odyssey.Module\PTH.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\PTHAuto.cs - Path auto-generated code
  - Target: Odyssey.Engines.Odyssey.Module\PTHAuto.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\PTHHelpers.cs - Path helpers
  - Target: Odyssey.Engines.Odyssey.Module\PTHHelpers.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

**Dialogue (Review - May be KOTOR-specific):**

- [ ] Resource\Generics\DLG\DLG.cs - Dialogue structure
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLG.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\DLG\DLGAnimation.cs - Dialogue animation
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLGAnimation.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\DLG\DLGHelper.cs - Dialogue helpers
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLGHelper.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\DLG\DLGLink.cs - Dialogue link
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLGLink.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\DLG\DLGNode.cs - Dialogue node
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLGNode.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\DLG\DLGStunt.cs - Dialogue stunt
  - Target: Odyssey.Engines.Odyssey.Dialogue\DLGStunt.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

**GUI (Review - May be KOTOR-specific):**

- [ ] Resource\Generics\GUI\GUI.cs - GUI structure
  - Target: Odyssey.Engines.Odyssey.GUI\GUI.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIBorder.cs - GUI border
  - Target: Odyssey.Engines.Odyssey.GUI\GUIBorder.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIControl.cs - GUI control
  - Target: Odyssey.Engines.Odyssey.GUI\GUIControl.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIEnums.cs - GUI enums
  - Target: Odyssey.Engines.Odyssey.GUI\GUIEnums.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIMoveTo.cs - GUI move to
  - Target: Odyssey.Engines.Odyssey.GUI\GUIMoveTo.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIProgress.cs - GUI progress
  - Target: Odyssey.Engines.Odyssey.GUI\GUIProgress.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIReader.cs - GUI reader
  - Target: Odyssey.Engines.Odyssey.GUI\GUIReader.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIScrollbar.cs - GUI scrollbar
  - Target: Odyssey.Engines.Odyssey.GUI\GUIScrollbar.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

- [ ] Resource\Generics\GUI\GUIText.cs - GUI text
  - Target: Odyssey.Engines.Odyssey.GUI\GUIText.cs (if KOTOR-specific)
  - Status: [ ] Review, [ ] Move if needed

#### Tools/ (KOTOR-Specific)

- [ ] Tools\Module.cs - KOTOR-specific module tools
  - Target: Odyssey.Engines.Odyssey.Tools\Module.cs
  - Status: [ ] Review if KOTOR-specific, [ ] Move if needed

#### Uninstall/ (Update to be engine-agnostic)

- [ ] Uninstall\UninstallHelpers.cs - References Game enum
  - Target: Keep in AuroraEngine.Common, make engine-agnostic
  - Status: [ ] Update to use IEngineProfile instead of Game enum

### Files to Keep in AuroraEngine.Common (Engine-Agnostic)

- [x] Common\GameObject.cs - ObjectType enum (shared across engines)
- [x] Common\ResRef.cs - Resource reference (shared)
- [x] Formats\** - All file format parsers (GFF, 2DA, TLK, MDL, TPC, BWM, LYT, VIS, KEY, BIF, ERF, RIM, etc.)
- [x] Installation\** - Installation detection and resource management
- [x] Resources\** - Resource management and loading
- [x] Logger\** - Logging infrastructure
- [x] Utility\** - Utility classes

### Odyssey.Engines.Odyssey (New Structure)

- [x] OdysseyEngine.cs - Base Odyssey engine implementation
- [x] OdysseyGameSession.cs - Game session implementation
- [x] OdysseyModuleLoader.cs - Module loader implementation
- [x] Profiles\OdysseyK1GameProfile.cs - KOTOR 1 profile
- [x] Profiles\OdysseyK2GameProfile.cs - KOTOR 2 profile
- [x] EngineApi\OdysseyK1EngineApi.cs - KOTOR 1 engine API
- [x] EngineApi\OdysseyK2EngineApi.cs - KOTOR 2 engine API
- [ ] Common\Game.cs - Game enum (moved from AuroraEngine.Common)
- [ ] Module\Module.cs - Module class (moved from AuroraEngine.Common)
- [ ] Module\ModuleDataLoader.cs - Module data loader (moved from AuroraEngine.Common)
- [ ] Templates\UTC.cs - Creature template (moved from AuroraEngine.Common)
- [ ] Templates\UTD.cs - Door template (moved from AuroraEngine.Common)
- [ ] Templates\UTE.cs - Encounter template (moved from AuroraEngine.Common)
- [ ] Templates\UTI.cs - Item template (moved from AuroraEngine.Common)
- [ ] Templates\UTM.cs - Merchant template (moved from AuroraEngine.Common)
- [ ] Templates\UTP.cs - Placeable template (moved from AuroraEngine.Common)
- [ ] Templates\UTS.cs - Sound template (moved from AuroraEngine.Common)
- [ ] Templates\UTT.cs - Trigger template (moved from AuroraEngine.Common)
- [ ] Templates\UTW.cs - Waypoint template (moved from AuroraEngine.Common)

### Migration Tasks

- [ ] Phase 1: Identify all KOTOR-specific code
- [ ] Phase 2: Create target directory structure in Odyssey.Engines.Odyssey
- [ ] Phase 3: Move Game.cs and update all references
- [ ] Phase 4: Move Module.cs and update all references
- [ ] Phase 5: Move GFF templates and update all references
- [ ] Phase 6: Update UninstallHelpers.cs to be engine-agnostic
- [ ] Phase 7: Verify compilation
- [ ] Phase 8: Run tests and verify no regressions
