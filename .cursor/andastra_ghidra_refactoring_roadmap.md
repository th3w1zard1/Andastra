# Andastra Ghidra Refactoring Roadmap

Internal tracking document for AI agents. Not public-facing. Do not commit to repository.

**Status**: IN PROGRESS
**Started**: 2025-01-16
**Current Phase**: Initial file inventory and systematic review

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

## Notes

- Focus on core game logic first (Runtime/Core, Runtime/Games/Odyssey, Runtime/Scripting)
- Graphics/MonoGame adapters can be lower priority unless they affect gameplay
- Use Ghidra string searches to locate functions (e.g., "GLOBALVARS", "PARTYTABLE", "savenfo")
- Document all Ghidra function addresses and string references in comments
- Match original engine behavior exactly where documented
- Modern graphics enhancements (DLSS, FSR, RTX Remix, raytracing) are not in original game - note as enhancements
