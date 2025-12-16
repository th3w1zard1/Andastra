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
- [ ] Converters\BwmToNavigationMeshConverter.cs
- [x] Interfaces\IContentCache.cs - CACHE @ 0x007c6848, z:\cache @ 0x007c6850, CExoKeyTable resource management
- [x] Interfaces\IContentConverter.cs - Resource @ 0x007c14d4, Loading @ 0x007c7e40, CExoKeyTable resource loading
- [ ] Interfaces\IGameResourceProvider.cs
- [ ] Interfaces\IResourceProvider.cs
- [ ] Loaders\GITLoader.cs
- [ ] Loaders\TemplateLoader.cs
- [x] MDL\MDLBulkReader.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [ ] MDL\MDLCache.cs
- [ ] MDL\MDLConstants.cs
- [ ] MDL\MDLDataTypes.cs
- [ ] MDL\MDLFastReader.cs
- [ ] MDL\MDLLoader.cs
- [ ] MDL\MDLOptimizedReader.cs
- [ ] ResourceProviders\GameResourceProvider.cs
- [ ] Save\SaveDataProvider.cs
- [ ] Save\SaveSerializer.cs

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
- [ ] Interfaces\IDelayScheduler.cs
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

- [ ] AuroraEngine.cs

### Odyssey.Engines.Common (8 files)

- [ ] BaseEngine.cs
- [ ] BaseEngineGame.cs
- [ ] BaseEngineModule.cs
- [ ] BaseEngineProfile.cs
- [ ] IEngine.cs
- [ ] IEngineGame.cs
- [ ] IEngineModule.cs
- [ ] IEngineProfile.cs

### Odyssey.Engines.Eclipse (1 files)

- [ ] EclipseEngine.cs

### Odyssey.Engines.Odyssey (7 files)

- [ ] EngineApi\OdysseyK1EngineApi.cs
- [ ] EngineApi\OdysseyK2EngineApi.cs
- [ ] OdysseyEngine.cs
- [ ] OdysseyGameSession.cs
- [ ] OdysseyModuleLoader.cs
- [ ] Profiles\OdysseyK1GameProfile.cs
- [ ] Profiles\OdysseyK2GameProfile.cs

### Odyssey.Game (8 files)

- [ ] Core\GamePathDetector.cs
- [ ] Core\GameSettings.cs
- [ ] Core\GameState.cs
- [ ] Core\GraphicsBackendFactory.cs
- [ ] Core\OdysseyGame.cs
- [ ] GUI\MenuRenderer.cs
- [ ] GUI\SaveLoadMenu.cs
- [ ] Program.cs

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
