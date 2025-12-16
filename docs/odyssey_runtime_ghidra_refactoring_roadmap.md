# OdysseyRuntime Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: IN PROGRESS
**Started**: 2025-01-15
**Total Files**: 442
**Completed**: Checking systematically
**Remaining**: Checking systematically

## Update Instructions

When processing a file:
- Mark as `- [/]` when starting work
- Mark as `- [x]` when complete with Ghidra references added
- Add notes about function addresses, string references, and implementation details
- Use format: `- [x] FileName.cs - Function addresses, string references, key findings`

## Refactoring Strategy

1. Search Ghidra for relevant functions using string searches and function name searches
2. Decompile relevant functions to understand original implementation
3. Add detailed comments with Ghidra function addresses and context
4. Update implementation to match original behavior where possible
5. Document any deviations or improvements

## Files to Process

### Odyssey.Content (18 files)

- [ ] Cache\ContentCache.cs
- [ ] Converters\BwmToNavigationMeshConverter.cs
- [ ] Interfaces\IContentCache.cs
- [ ] Interfaces\IContentConverter.cs
- [ ] Interfaces\IGameResourceProvider.cs
- [ ] Interfaces\IResourceProvider.cs
- [ ] Loaders\GITLoader.cs
- [ ] Loaders\TemplateLoader.cs
- [ ] MDL\MDLBulkReader.cs
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

### Odyssey.Engines.Aurora (1 file)

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

### Odyssey.Engines.Eclipse (1 file)

- [ ] EclipseEngine.cs

### Odyssey.Engines.Odyssey (9 files)

- [ ] EngineApi\OdysseyK1EngineApi.cs
- [ ] EngineApi\OdysseyK2EngineApi.cs
- [ ] OdysseyEngine.cs
- [ ] OdysseyGameSession.cs
- [ ] OdysseyModuleLoader.cs
- [ ] Profiles\OdysseyK1GameProfile.cs
- [ ] Profiles\OdysseyK2GameProfile.cs
- [ ] Templates\UTC.cs
- [ ] Templates\UTCHelpers.cs

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

- [x] GraphicsBackend.cs - Graphics Options @ 0x007b56a8, BTN_GRAPHICS @ 0x007d0d8c, optgraphics_p @ 0x007d2064, 2D3DBias @ 0x007c612c, 2D3D Bias @ 0x007c71f8
- [x] IContentManager.cs - Resource @ 0x007c14d4, Loading @ 0x007c7e40, CExoKeyTable @ 0x007b6078, FUN_00633270 @ 0x00633270
- [x] IDepthStencilBuffer.cs - GL_ARB_depth_texture @ 0x007b8848, m_sDepthTextureName @ 0x007baaa8, depth_texture @ 0x007bab5c, glDepthMask @ 0x0080aa38, glDepthFunc @ 0x0080ad96, glStencilOp @ 0x0080a9f0, glStencilMask @ 0x0080aa0c, glStencilFunc @ 0x0080aa68, glClearStencil @ 0x0080ada4, GL_EXT_stencil_two_side @ 0x007b8a68
- [x] IEffect.cs - Vertex program for skinned animations @ 0x0081c228, 0x0081fe20, DirectX 8/9 fixed-function pipeline
- [x] IEntityModelRenderer.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, VisibleModel @ 0x007c1c98, ModelType @ 0x007c4568, MODELTYPE @ 0x007c036c, ModelVariation @ 0x007c0990, ModelPart @ 0x007bd42c, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] IFont.cs - dialogfont16x16 @ 0x007b6380, fontheight @ 0x007b6eb8, Use Small Fonts @ 0x007c8538
- [x] IGraphicsBackend.cs - Graphics Options @ 0x007b56a8, BTN_GRAPHICS @ 0x007d0d8c, optgraphics_p @ 0x007d2064, Render Window @ 0x007b5680, render @ 0x007bab34, renderorder @ 0x007bab50, FUN_00404250 @ 0x00404250
- [x] IGraphicsDevice.cs - Render Window @ 0x007b5680, render @ 0x007bab34, WGL_NV_render_texture_rectangle @ 0x007b880c, WGL_ARB_render_texture @ 0x007b8890, DirectX 8/9 device
- [x] IIndexBuffer.cs - GetNextIndex: Duplicate triangle @ 0x007bb308, 0x007bb330, GetNextIndex: Triangle doesn't have all of its vertices @ 0x007bb36c
- [x] IInputManager.cs - Already has comprehensive Ghidra references
- [x] IModel.cs - ModelName @ 0x007c1c8c, Model @ 0x007c1ca8, VisibleModel @ 0x007c1c98, ModelType @ 0x007c4568, MODELTYPE @ 0x007c036c, ModelVariation @ 0x007c0990, ModelPart @ 0x007bd42c, CSWCCreature::LoadModel @ 0x007c82fc, FUN_005261b0 @ 0x005261b0
- [x] IRenderState.cs - GL_ARB_depth_texture @ 0x007b8848, glDepthMask @ 0x0080aa38, glDepthFunc @ 0x0080ad96, glStencilOp @ 0x0080a9f0, glStencilMask @ 0x0080aa0c, glStencilFunc @ 0x0080aa68, glClearStencil @ 0x0080ada4, GL_EXT_stencil_two_side @ 0x007b8a68
- [x] IRenderTarget.cs - WGL_NV_render_texture_rectangle @ 0x007b880c, WGL_ARB_render_texture @ 0x007b8890, m_sDepthTextureName @ 0x007baaa8, depth_texture @ 0x007bab5c
- [x] IRoomMeshRenderer.cs - roomcount @ 0x007b96c0, RoomName @ 0x007bd484, Rooms @ 0x007bd490, trimesh @ 0x007bac30, animmesh @ 0x007bac24, danglymesh @ 0x007bac18, VISIBLEVALUE @ 0x007b6a58, %s/%s.VIS @ 0x007b972c, VisibleModel @ 0x007c1c98, render @ 0x007bab34, renderorder @ 0x007bab50, AREANAME @ 0x007be1dc, AreaName @ 0x007be340
- [x] ISpatialAudio.cs - EnvAudio @ 0x007bd478, EAX2 room rolloff @ 0x007c5f24, EAX3 room LF @ 0x007c6010, EAX2 room HF @ 0x007c6040, EAX2 room @ 0x007c6050, EAX3 modulation depth @ 0x007c5f74, EAX3 echo depth @ 0x007c5fa4, _AIL_set_digital_master_room_type@8 @ 0x0080a0f6, _AIL_set_3D_room_type@8 @ 0x0080a11c, _AIL_3D_room_type@4 @ 0x0080a1ec
- [x] ISpriteBatch.cs - gui3D_room @ 0x007cc144, Render Window @ 0x007b5680, DirectX 8/9 sprite rendering
- [x] ITexture2D.cs - texturewidth @ 0x007b6e98, GL_ARB_texture_compression @ 0x007b88fc, GL_EXT_texture_compression_s3tc @ 0x007b88dc, GL_EXT_texture_filter_anisotropic @ 0x007b8974, GL_EXT_texture_cube_map @ 0x007b89dc, GL_EXT_texture_env_combine @ 0x007b8a2c, GL_ARB_multitexture @ 0x007b8a48, glActiveTextureARB @ 0x007b8738, glClientActiveTextureARB @ 0x007b871c, glBindTextureUnitParameterEXT @ 0x007b7774
- [x] IVertexBuffer.cs - Disable Vertex Buffer Objects @ 0x007b56bc, glVertexArrayRangeNV @ 0x007b7ce8, glVertexAttrib4fvNV @ 0x007b7d24, glVertexAttrib3fvNV @ 0x007b7d38, glVertexAttrib2fvNV @ 0x007b7d4c, glDeleteVertexShadersEXT @ 0x007b7974, glGenVertexShadersEXT @ 0x007b7990, glBindVertexShaderEXT @ 0x007b79a8
- [x] IVertexDeclaration.cs - Disable Vertex Buffer Objects @ 0x007b56bc, glVertexAttrib4fvNV @ 0x007b7d24, glVertexAttrib3fvNV @ 0x007b7d38, glVertexAttrib2fvNV @ 0x007b7d4c, DirectX 8/9 FVF
- [x] IWindow.cs - Render Window @ 0x007b5680, SW Movie Player Window @ 0x007b57dc, SWMovieWindow @ 0x007b57f4, Exo Base Window @ 0x007b74a0, AllowWindowedMode @ 0x007c75d0, GetProcessWindowStation @ 0x007d95f4, GetActiveWindow @ 0x007d963c, SetWindowTextA @ 0x00809e1a, DestroyWindow @ 0x00809e8a, ShowWindow @ 0x00809e9a
- [x] MatrixHelper.cs - DirectX 8/9 matrix operations (D3DXMatrix* functions)
- [x] VertexPositionColor.cs - Disable Vertex Buffer Objects @ 0x007b56bc, DirectX 8/9 FVF D3DFVF_XYZ | D3DFVF_DIFFUSE

### Odyssey.Graphics.Common (15 files)

- [ ] Backends\BaseDirect3D11Backend.cs
- [ ] Backends\BaseDirect3D12Backend.cs
- [ ] Backends\BaseGraphicsBackend.cs
- [ ] Backends\BaseVulkanBackend.cs
- [ ] Enums\GraphicsBackendType.cs
- [ ] Interfaces\ILowLevelBackend.cs
- [ ] Interfaces\IPostProcessingEffect.cs
- [ ] Interfaces\IRaytracingSystem.cs
- [ ] Interfaces\IUpscalingSystem.cs
- [ ] PostProcessing\BasePostProcessingEffect.cs
- [ ] Raytracing\BaseRaytracingSystem.cs
- [ ] Remix\BaseRemixBridge.cs
- [ ] Rendering\RenderSettings.cs
- [ ] Structs\GraphicsStructs.cs
- [ ] Upscaling\BaseUpscalingSystem.cs

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
- [ ] Components\QuickSlotComponent.cs
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

### Odyssey.Stride (31 files)

- [ ] Audio\StrideSoundPlayer.cs
- [ ] Audio\StrideVoicePlayer.cs
- [ ] Backends\StrideBackendFactory.cs
- [ ] Backends\StrideDirect3D11Backend.cs
- [ ] Backends\StrideDirect3D12Backend.cs
- [ ] Backends\StrideVulkanBackend.cs
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
- [ ] PostProcessing\StrideBloomEffect.cs
- [ ] PostProcessing\StrideSsaoEffect.cs
- [ ] PostProcessing\StrideTemporalAaEffect.cs
- [ ] Raytracing\StrideRaytracingSystem.cs
- [ ] Remix\StrideRemixBridge.cs
- [ ] Upscaling\StrideDlssSystem.cs
- [ ] Upscaling\StrideFsrSystem.cs

### Odyssey.Tests (3 files)

- [ ] UI\FallbackUITests.cs
- [ ] UI\KotorGuiManagerTests.cs
- [ ] VM\NcsVmTests.cs

### Odyssey.Tooling (1 file)

- [ ] Program.cs

## Notes

- Focus on core game logic first (Odyssey.Core, Odyssey.Kotor, Odyssey.Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented
- Modern graphics enhancements (DLSS, FSR, RTX Remix, raytracing) are not in original game - note as enhancements
