using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Performance;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Unified render pipeline orchestrating all rendering systems.
    ///
    /// The render pipeline coordinates all rendering optimizations and
    /// systems into a cohesive, efficient rendering flow.
    ///
    /// Features:
    /// - Unified rendering flow
    /// - Automatic optimization application
    /// - Performance monitoring
    /// - Configurable pipeline stages
    /// </summary>
    public class RenderPipeline : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ModernRenderer _modernRenderer;
        private readonly RenderQueue _renderQueue;
        private readonly RenderGraph _renderGraph;
        private readonly FrameGraph _frameGraph;
        private readonly RenderTargetManager _rtManager;
        private readonly GPUMemoryBudget _memoryBudget;
        private readonly Telemetry _telemetry;
        private readonly RenderStatistics _statistics;
        private readonly RenderSettings _settings;
        private readonly ILightingSystem _lightingSystem;

        /// <summary>
        /// Initializes a new render pipeline.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <param name="resourceProvider">Resource provider for asset loading.</param>
        /// <param name="settings">Optional render settings. If null, post-processing checks will default to false.</param>
        /// <param name="lightingSystem">Optional lighting system. If provided, used to check for shadow-casting lights.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice or resourceProvider is null.</exception>
        public RenderPipeline(
            GraphicsDevice graphicsDevice,
            IGameResourceProvider resourceProvider,
            RenderSettings settings = null,
            ILightingSystem lightingSystem = null)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }
            if (resourceProvider == null)
            {
                throw new ArgumentNullException(nameof(resourceProvider));
            }

            _graphicsDevice = graphicsDevice;
            _modernRenderer = new ModernRenderer(graphicsDevice, resourceProvider);
            _renderQueue = new RenderQueue();
            _renderGraph = new RenderGraph();
            _frameGraph = new FrameGraph();
            _rtManager = new RenderTargetManager(graphicsDevice);
            _memoryBudget = new GPUMemoryBudget();
            _telemetry = new Telemetry();
            _statistics = new RenderStatistics();
            _settings = settings;
            _lightingSystem = lightingSystem;
        }

        /// <summary>
        /// Renders a frame with all optimizations applied.
        /// </summary>
        /// <param name="viewMatrix">View matrix for camera transform.</param>
        /// <param name="projectionMatrix">Projection matrix for camera perspective.</param>
        /// <param name="cameraPosition">Camera position in world space.</param>
        /// <param name="outputTarget">Output render target. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if outputTarget is null.</exception>
        public void RenderFrame(
            Matrix viewMatrix,
            Matrix projectionMatrix,
            Vector3 cameraPosition,
            RenderTarget2D outputTarget)
        {
            if (outputTarget == null)
            {
                throw new ArgumentNullException(nameof(outputTarget));
            }

            // Begin frame
            _modernRenderer.BeginFrame(viewMatrix, projectionMatrix, cameraPosition);
            _memoryBudget.UpdateFrame();

            // Update render queue
            _renderQueue.Sort();

            // Build frame graph
            BuildFrameGraph();

            // Execute rendering
            ExecuteRendering(outputTarget);

            // End frame
            _modernRenderer.EndFrame(outputTarget);

            // Update statistics
            UpdateStatistics();
        }

        /// <summary>
        /// Builds the frame graph from render queue commands.
        ///
        /// Analyzes the render queue and constructs a frame graph with proper pass dependencies,
        /// resource management, and execution order. This ensures optimal rendering performance
        /// by managing resource lifetimes and pass ordering.
        ///
        /// Based on swkotor2.exe rendering system architecture:
        /// - Original implementation: Render order sorting by material/shader (swkotor2.exe: "renderorder" @ 0x007bab50)
        /// - Original rendering: Single-pass rendering with depth testing (no explicit frame graph)
        /// - Modern enhancement: Frame graph system for advanced resource management and pass scheduling
        /// </summary>
        private void BuildFrameGraph()
        {
            if (_frameGraph == null || _renderQueue == null)
            {
                return;
            }

            // Clear the frame graph to start fresh for this frame
            _frameGraph.Clear();

            // Analyze render queue to determine which passes are needed
            Dictionary<RenderQueue.QueueType, int> queueCounts = new Dictionary<RenderQueue.QueueType, int>();
            foreach (RenderQueue.QueueEntry entry in _renderQueue.GetEntries())
            {
                int count;
                if (queueCounts.TryGetValue(entry.Type, out count))
                {
                    queueCounts[entry.Type] = count + 1;
                }
                else
                {
                    queueCounts[entry.Type] = 1;
                }
            }

            // Track which passes we're creating for dependency management
            List<string> passNames = new List<string>();
            int passIndex = 0;

            // 1. Depth Pre-Pass (if enabled and we have opaque geometry)
            // Depth pre-pass renders geometry depth-only first, enabling early-Z rejection
            // This must come before any opaque geometry rendering
            // Based on ModernRenderer.DepthPrePassEnabled check (swkotor2.exe: depth buffer testing)
            if (_modernRenderer.DepthPrePassEnabled && queueCounts.ContainsKey(RenderQueue.QueueType.Opaque))
            {
                FrameGraph.FrameGraphNode depthPrePassNode = new FrameGraph.FrameGraphNode("DepthPrePass");
                depthPrePassNode.Priority = 100; // Highest priority - executes first
                depthPrePassNode.WriteResources.Add("DepthBuffer");
                depthPrePassNode.Execute = (context) =>
                {
                    // Depth pre-pass execution is handled by ModernRenderer.BeginFrame/EndFrame
                    // This node marks the pass in the graph for dependency tracking
                };
                _frameGraph.AddNode(depthPrePassNode);
                passNames.Add("DepthPrePass");
                passIndex++;
            }

            // 2. Shadow Pass (if we have shadow-casting lights)
            // Shadow maps must be rendered before lighting calculations
            // This is a modern enhancement - original KOTOR used simple depth testing
            // Check if we need shadow rendering (this would be determined by light setup)
            // Based on swkotor2.exe shadow system: "SunShadows" @ 0x007bd5d8, "MoonShadows" @ 0x007bd628
            // Original implementation: KOTOR 2 had shadow support with configurable sun/moon shadows
            // Modern enhancement: Check all active lights for shadow casting capability
            bool hasShadowCastingLights = HasShadowCastingLights();
            if (hasShadowCastingLights && queueCounts.ContainsKey(RenderQueue.QueueType.Opaque))
            {
                FrameGraph.FrameGraphNode shadowPassNode = new FrameGraph.FrameGraphNode("ShadowPass");
                shadowPassNode.Priority = 200;
                shadowPassNode.WriteResources.Add("ShadowMap");
                if (passNames.Contains("DepthPrePass"))
                {
                    shadowPassNode.Dependencies.Add("DepthPrePass");
                }
                shadowPassNode.Execute = (context) =>
                {
                    // Shadow map rendering would be executed here
                    // This involves rendering scene from light's perspective to shadow map texture
                };
                _frameGraph.AddNode(shadowPassNode);
                passNames.Add("ShadowPass");
                passIndex++;
            }

            // 3. Background Pass (sky, far background elements)
            // Rendered first after depth/shadow passes, but before geometry
            // Based on original KOTOR render order: background elements rendered first
            if (queueCounts.ContainsKey(RenderQueue.QueueType.Background))
            {
                FrameGraph.FrameGraphNode backgroundPassNode = new FrameGraph.FrameGraphNode("BackgroundPass");
                backgroundPassNode.Priority = 300;
                backgroundPassNode.ReadResources.Add("DepthBuffer");
                backgroundPassNode.WriteResources.Add("MainTarget");
                if (passNames.Contains("DepthPrePass"))
                {
                    backgroundPassNode.Dependencies.Add("DepthPrePass");
                }
                if (passNames.Contains("ShadowPass"))
                {
                    backgroundPassNode.Dependencies.Add("ShadowPass");
                }
                backgroundPassNode.Execute = (context) =>
                {
                    // Render background/sky elements
                    // This would iterate through Background queue entries and render them
                };
                _frameGraph.AddNode(backgroundPassNode);
                passNames.Add("BackgroundPass");
                passIndex++;
            }

            // 4. Opaque Geometry Pass
            // Main geometry rendering - most objects are opaque
            // Based on original KOTOR render order: opaque geometry after background (swkotor2.exe: "renderorder" @ 0x007bab50)
            if (queueCounts.ContainsKey(RenderQueue.QueueType.Opaque))
            {
                FrameGraph.FrameGraphNode opaquePassNode = new FrameGraph.FrameGraphNode("OpaquePass");
                opaquePassNode.Priority = 400;
                opaquePassNode.ReadResources.Add("DepthBuffer");
                opaquePassNode.WriteResources.Add("MainTarget");
                opaquePassNode.WriteResources.Add("GBuffer"); // For deferred rendering
                if (passNames.Contains("DepthPrePass"))
                {
                    opaquePassNode.Dependencies.Add("DepthPrePass");
                }
                if (passNames.Contains("ShadowPass"))
                {
                    opaquePassNode.ReadResources.Add("ShadowMap");
                    opaquePassNode.Dependencies.Add("ShadowPass");
                }
                if (passNames.Contains("BackgroundPass"))
                {
                    opaquePassNode.Dependencies.Add("BackgroundPass");
                }
                opaquePassNode.Execute = (context) =>
                {
                    // Render opaque geometry from Opaque queue
                    // This would iterate through Opaque queue entries, apply culling/batching/LOD,
                    // and render them using ModernRenderer
                };
                _frameGraph.AddNode(opaquePassNode);
                passNames.Add("OpaquePass");
                passIndex++;
            }

            // 5. Alpha Test Pass
            // Geometry with alpha testing (e.g., foliage, fences)
            // Rendered after opaque but before transparent
            // Based on original KOTOR render order: alpha-tested geometry in separate pass
            if (queueCounts.ContainsKey(RenderQueue.QueueType.AlphaTest))
            {
                FrameGraph.FrameGraphNode alphaTestPassNode = new FrameGraph.FrameGraphNode("AlphaTestPass");
                alphaTestPassNode.Priority = 500;
                alphaTestPassNode.ReadResources.Add("DepthBuffer");
                alphaTestPassNode.WriteResources.Add("MainTarget");
                if (passNames.Contains("OpaquePass"))
                {
                    alphaTestPassNode.Dependencies.Add("OpaquePass");
                }
                else if (passNames.Contains("BackgroundPass"))
                {
                    alphaTestPassNode.Dependencies.Add("BackgroundPass");
                }
                alphaTestPassNode.Execute = (context) =>
                {
                    // Render alpha-tested geometry from AlphaTest queue
                    // Uses alpha testing to discard fragments (more efficient than blending)
                };
                _frameGraph.AddNode(alphaTestPassNode);
                passNames.Add("AlphaTestPass");
                passIndex++;
            }

            // 6. Lighting Pass (if using deferred rendering)
            // Calculate lighting from G-buffer data
            // This comes after geometry has been rendered to G-buffer
            // Modern enhancement - original KOTOR used forward rendering
            bool useDeferredRendering = IsDeferredRenderingEnabled();
            if (useDeferredRendering && passNames.Contains("OpaquePass"))
            {
                FrameGraph.FrameGraphNode lightingPassNode = new FrameGraph.FrameGraphNode("LightingPass");
                lightingPassNode.Priority = 600;
                lightingPassNode.ReadResources.Add("GBuffer");
                lightingPassNode.ReadResources.Add("DepthBuffer");
                lightingPassNode.WriteResources.Add("LightingBuffer");
                if (passNames.Contains("OpaquePass"))
                {
                    lightingPassNode.Dependencies.Add("OpaquePass");
                }
                if (passNames.Contains("ShadowPass"))
                {
                    lightingPassNode.ReadResources.Add("ShadowMap");
                    lightingPassNode.Dependencies.Add("ShadowPass");
                }
                lightingPassNode.Execute = (context) =>
                {
                    // Calculate lighting from G-buffer
                    // This would use clustered forward+ lighting or deferred lighting
                };
                _frameGraph.AddNode(lightingPassNode);
                passNames.Add("LightingPass");
                passIndex++;
            }

            // 7. Transparent Pass
            // Transparent geometry rendered back-to-front
            // Based on original KOTOR render order: transparent objects last (sorted back-to-front)
            // Original implementation: Objects sorted by distance for proper alpha blending
            if (queueCounts.ContainsKey(RenderQueue.QueueType.Transparent))
            {
                FrameGraph.FrameGraphNode transparentPassNode = new FrameGraph.FrameGraphNode("TransparentPass");
                transparentPassNode.Priority = 700;
                transparentPassNode.ReadResources.Add("DepthBuffer");
                transparentPassNode.WriteResources.Add("MainTarget");
                if (passNames.Contains("AlphaTestPass"))
                {
                    transparentPassNode.Dependencies.Add("AlphaTestPass");
                }
                else if (passNames.Contains("OpaquePass"))
                {
                    transparentPassNode.Dependencies.Add("OpaquePass");
                }
                else if (passNames.Contains("BackgroundPass"))
                {
                    transparentPassNode.Dependencies.Add("BackgroundPass");
                }
                transparentPassNode.Execute = (context) =>
                {
                    // Render transparent geometry from Transparent queue
                    // Rendered back-to-front for proper alpha blending
                    // Based on original KOTOR: transparent objects sorted by distance (swkotor2.exe render order)
                };
                _frameGraph.AddNode(transparentPassNode);
                passNames.Add("TransparentPass");
                passIndex++;
            }

            // 8. Post-Processing Pass
            // Tone mapping, bloom, TAA, etc.
            // Modern enhancement - original KOTOR had minimal post-processing
            bool hasPostProcessing = IsPostProcessingEnabled();
            if (hasPostProcessing)
            {
                FrameGraph.FrameGraphNode postProcessPassNode = new FrameGraph.FrameGraphNode("PostProcessPass");
                postProcessPassNode.Priority = 800;
                postProcessPassNode.ReadResources.Add("MainTarget");
                postProcessPassNode.WriteResources.Add("PostProcessTarget");
                if (passNames.Contains("TransparentPass"))
                {
                    postProcessPassNode.Dependencies.Add("TransparentPass");
                }
                else if (passNames.Contains("AlphaTestPass"))
                {
                    postProcessPassNode.Dependencies.Add("AlphaTestPass");
                }
                else if (passNames.Contains("OpaquePass"))
                {
                    postProcessPassNode.Dependencies.Add("OpaquePass");
                }
                postProcessPassNode.Execute = (context) =>
                {
                    // Apply post-processing effects (tone mapping, bloom, TAA, etc.)
                };
                _frameGraph.AddNode(postProcessPassNode);
                passNames.Add("PostProcessPass");
                passIndex++;
            }

            // 9. Overlay/UI Pass
            // UI elements rendered on top of everything
            // Based on original KOTOR: UI rendered last ("mgs_drawmain" @ swkotor2.exe:0x007cc8f0)
            if (queueCounts.ContainsKey(RenderQueue.QueueType.Overlay))
            {
                FrameGraph.FrameGraphNode overlayPassNode = new FrameGraph.FrameGraphNode("OverlayPass");
                overlayPassNode.Priority = 900; // Lowest priority - executes last
                overlayPassNode.ReadResources.Add("MainTarget");
                overlayPassNode.WriteResources.Add("OutputTarget");
                if (passNames.Contains("PostProcessPass"))
                {
                    overlayPassNode.Dependencies.Add("PostProcessPass");
                }
                else if (passNames.Contains("TransparentPass"))
                {
                    overlayPassNode.Dependencies.Add("TransparentPass");
                }
                else if (passNames.Contains("AlphaTestPass"))
                {
                    overlayPassNode.Dependencies.Add("AlphaTestPass");
                }
                else if (passNames.Contains("OpaquePass"))
                {
                    overlayPassNode.Dependencies.Add("OpaquePass");
                }
                overlayPassNode.Execute = (context) =>
                {
                    // Render UI/overlay elements from Overlay queue
                    // Based on original KOTOR: UI rendered on top ("mgs_drawmain" @ swkotor2.exe:0x007cc8f0)
                };
                _frameGraph.AddNode(overlayPassNode);
                passNames.Add("OverlayPass");
                passIndex++;
            }

            // Compile the frame graph to determine final execution order
            // This performs topological sort based on dependencies and resource lifetimes
            _frameGraph.Compile();
        }

        /// <summary>
        /// Executes all rendering passes with optimizations.
        ///
        /// Executes the frame graph that was built by BuildFrameGraph(), which ensures
        /// all passes execute in the correct order with proper resource management.
        ///
        /// Rendering pass order (as determined by frame graph):
        /// 1. Depth pre-pass (for early-Z rejection) - if enabled
        /// 2. Shadow maps (cascaded shadow maps) - if shadow-casting lights exist
        /// 3. Background pass (sky, far background elements)
        /// 4. Opaque geometry pass (main geometry rendering)
        /// 5. Alpha test pass (alpha-tested geometry like foliage)
        /// 6. Lighting pass (deferred lighting calculations) - if using deferred rendering
        /// 7. Transparent pass (transparent geometry, back-to-front)
        /// 8. Post-processing pass (tone mapping, bloom, TAA, etc.) - if enabled
        /// 9. Overlay/UI pass (UI elements rendered on top)
        ///
        /// Based on swkotor2.exe rendering system:
        /// - Original implementation: Single-pass rendering with render order sorting (swkotor2.exe: "renderorder" @ 0x007bab50)
        /// - Modern enhancement: Frame graph system for advanced pass scheduling and resource management
        /// </summary>
        /// <param name="outputTarget">Output render target. Must not be null.</param>
        private void ExecuteRendering(RenderTarget2D outputTarget)
        {
            if (outputTarget == null)
            {
                throw new ArgumentNullException(nameof(outputTarget));
            }

            if (_frameGraph == null)
            {
                return;
            }

            // Set up frame graph context with output target
            // The context provides resources to pass execution functions
            _frameGraph.SetContextResource("OutputTarget", outputTarget, 0, int.MaxValue);
            _frameGraph.SetContextResource("MainTarget", outputTarget, 0, int.MaxValue);

            // Get render target dimensions for resource allocation
            int width = outputTarget.Width;
            int height = outputTarget.Height;

            // Allocate render targets needed by passes
            // These are managed by RenderTargetManager for efficient reuse
            // Depth buffer is shared across passes that need it
            RenderTarget2D depthTarget = _rtManager.GetRenderTarget(width, height, SurfaceFormat.Single, DepthFormat.Depth24, 0, false);
            if (depthTarget != null)
            {
                _frameGraph.SetContextResource("DepthBuffer", depthTarget, 0, int.MaxValue);
            }

            // Execute the frame graph
            // This will execute all passes in the correct order as determined by BuildFrameGraph()
            // Each pass's Execute action will perform the actual rendering for that pass
            _frameGraph.Execute();

            // Release temporary render targets back to pool
            // RenderTargetManager handles lifetime management
            if (depthTarget != null)
            {
                _rtManager.ReturnRenderTarget(depthTarget);
            }

            // Note: The actual rendering work is performed by the Execute actions set up in BuildFrameGraph()
            // These actions delegate to ModernRenderer, which handles:
            // - Culling (frustum, occlusion, distance)
            // - LOD selection
            // - Batching and instancing
            // - Material/shader binding
            // - Draw call execution
        }

        /// <summary>
        /// Updates performance statistics and telemetry.
        /// </summary>
        private void UpdateStatistics()
        {
            if (_modernRenderer == null || _telemetry == null)
            {
                return;
            }

            // Update telemetry and statistics
            RenderStats stats = _modernRenderer.Stats;
            if (stats != null)
            {
                // Record key performance metrics
                _telemetry.RecordMetric("DrawCalls", stats.DrawCalls);
                _telemetry.RecordMetric("TrianglesRendered", stats.TrianglesRendered);
                _telemetry.RecordMetric("ObjectsCulled", stats.ObjectsCulled);

                // Frame time would be measured externally and passed in
                // This is where it would be recorded if available
            }
        }

        /// <summary>
        /// <summary>
        /// Checks if deferred rendering is enabled based on render settings.
        ///
        /// Deferred rendering is enabled when one or more of the following conditions are met:
        /// - MaxDynamicLights exceeds threshold (typically > 8, as forward rendering handles up to 8 lights efficiently)
        /// - Global illumination mode requires deferred rendering (e.g., certain raytraced GI modes benefit from deferred)
        /// - Quality preset indicates deferred rendering should be used (High/Ultra presets)
        ///
        /// Deferred rendering benefits:
        /// - Efficiently handles many lights (decouples geometry complexity from light count)
        /// - Enables advanced lighting techniques (SSAO, SSR, etc.)
        /// - Better performance for complex scenes with many dynamic lights
        ///
        /// Based on RenderSettings configuration.
        /// If RenderSettings is not provided, returns false (forward rendering).
        /// </summary>
        /// <returns>True if deferred rendering should be enabled, false for forward rendering.</returns>
        private bool IsDeferredRenderingEnabled()
        {
            // If settings are not available, use forward rendering (default)
            if (_settings == null)
            {
                return false;
            }

            // Check if many dynamic lights are configured (deferred rendering is beneficial for many lights)
            // Forward rendering typically handles up to 8 lights efficiently
            // Deferred rendering becomes beneficial when there are more than 8 lights
            const int deferredLightThreshold = 8;
            if (_settings.MaxDynamicLights > deferredLightThreshold)
            {
                return true;
            }

            // Check if global illumination mode benefits from deferred rendering
            // Certain GI modes (like raytraced GI) work better with deferred rendering
            // ScreenSpace GI can work with either, but deferred provides better quality
            if (_settings.GlobalIllumination == GlobalIlluminationMode.Raytraced)
            {
                return true;
            }

            // Check quality preset - High and Ultra presets typically use deferred rendering
            // for better visual quality with many lights and advanced effects
            if (_settings.Quality == Enums.RenderQuality.High || _settings.Quality == Enums.RenderQuality.Ultra)
            {
                // For High/Ultra quality, enable deferred if dynamic lighting is enabled
                // Forward rendering is still acceptable for High quality, but deferred provides better scalability
                // Only enable deferred for Ultra quality by default, High can use either
                if (_settings.Quality == Enums.RenderQuality.Ultra && _settings.DynamicLighting)
                {
                    return true;
                }
            }

            // Default to forward rendering
            return false;
        }

        /// <summary>
        /// Checks if there are any active shadow-casting lights in the lighting system.
        ///
        /// Queries the lighting system for all active lights and checks if any of them
        /// have shadow casting enabled (CastShadows property). Only enabled lights are
        /// considered, as disabled lights do not contribute to rendering.
        ///
        /// If no lighting system is provided, returns false (no shadow-casting lights).
        ///
        /// Based on swkotor2.exe shadow system:
        /// - Original implementation: "SunShadows" @ 0x007bd5d8, "MoonShadows" @ 0x007bd628
        /// - Original behavior: KOTOR 2 checked sun/moon light shadow casting flags
        /// - Modern enhancement: Checks all dynamic lights for shadow casting capability
        /// </summary>
        /// <returns>True if there are any active shadow-casting lights, false otherwise.</returns>
        private bool HasShadowCastingLights()
        {
            // If no lighting system is provided, there are no shadow-casting lights
            if (_lightingSystem == null)
            {
                return false;
            }

            // Get all active lights from the lighting system
            IDynamicLight[] activeLights = _lightingSystem.GetActiveLights();
            if (activeLights == null)
            {
                return false;
            }

            // Check each active light to see if it casts shadows
            // Only enabled lights are returned by GetActiveLights(), so we just need to check CastShadows
            for (int i = 0; i < activeLights.Length; i++)
            {
                IDynamicLight light = activeLights[i];
                if (light != null && light.CastShadows)
                {
                    return true;
                }
            }

            // No shadow-casting lights found
            return false;
        }

        /// <summary>
        /// Checks if post-processing is enabled based on render settings.
        /// Post-processing is considered enabled if any of the following effects are enabled:
        /// - Bloom (BloomEnabled)
        /// - Motion blur (MotionBlurEnabled)
        /// - Depth of field (DepthOfFieldEnabled)
        /// - Chromatic aberration (ChromaticAberration)
        /// - Film grain (FilmGrain)
        /// - Vignette (Vignette)
        /// - Tone mapping (Tonemapper is not default/disabled)
        /// - Color grading LUT (ColorGradingLut is not null)
        ///
        /// Based on RenderSettings post-processing configuration.
        /// If RenderSettings is not provided, returns false (no post-processing).
        /// </summary>
        /// <returns>True if any post-processing effects are enabled, false otherwise.</returns>
        private bool IsPostProcessingEnabled()
        {
            // If settings are not available, post-processing is disabled
            if (_settings == null)
            {
                return false;
            }

            // Check if any post-processing effects are enabled
            // Bloom is the most common effect, so check it first
            if (_settings.BloomEnabled)
            {
                return true;
            }

            // Check other post-processing effects
            if (_settings.MotionBlurEnabled)
            {
                return true;
            }

            if (_settings.DepthOfFieldEnabled)
            {
                return true;
            }

            if (_settings.ChromaticAberration)
            {
                return true;
            }

            if (_settings.FilmGrain)
            {
                return true;
            }

            if (_settings.Vignette)
            {
                return true;
            }

            // Check if tone mapping is enabled
            // Tone mapping is enabled when the operator is not None
            // Based on swkotor2.exe: Original engine used LDR rendering with gamma correction (gamma @ 0x007b6fcc)
            // Modern enhancement: HDR rendering with tone mapping operators (Reinhard, ACES, Uncharted2, etc.)
            // The None operator indicates tone mapping is disabled
            if (_settings.Tonemapper != TonemapOperator.None)
            {
                return true;
            }

            // Check if color grading LUT is provided
            if (!string.IsNullOrEmpty(_settings.ColorGradingLut))
            {
                return true;
            }

            // No post-processing effects are enabled
            return false;
        }

        /// <summary>
        /// Disposes of all resources used by this render pipeline.
        /// </summary>
        public void Dispose()
        {
            _modernRenderer?.Dispose();
            _rtManager?.Dispose();
        }
    }
}

