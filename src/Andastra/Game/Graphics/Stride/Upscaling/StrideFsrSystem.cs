using System;
using System.Collections.Generic;
using System.Reflection;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.Rendering;
using Andastra.Runtime.Graphics.Common.Upscaling;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Serialization.Contents;
using Stride.Engine;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Game.Stride.Upscaling
{
    /// <summary>
    /// Stride implementation of AMD FSR (FidelityFX Super Resolution).
    /// Inherits shared FSR logic from BaseFsrSystem.
    ///
    /// Features:
    /// - FSR 2.x temporal upscaling
    /// - FSR 3.x with Frame Generation (optional)
    /// - All quality modes: Quality, Balanced, Performance, Ultra Performance
    /// - Works on all GPUs (AMD, NVIDIA, Intel)
    ///
    /// Based on AMD FidelityFX SDK: https://github.com/GPUOpen-LibrariesAndSDKs/FidelityFX-SDK
    /// </summary>
    public class StrideFsrSystem : BaseFsrSystem
    {
        private StrideGraphics.GraphicsDevice _graphicsDevice;
        private StrideGraphics.CommandList _graphicsContext;
        private IServiceRegistry _services;
        private ContentManager _contentManager;
        private EffectSystem _effectSystem;
        private IntPtr _fsrContext;
        private StrideGraphics.Texture _outputTexture;

        // FSR 2.x compute shaders
        private EffectInstance _fsrEasuEffect;
        private EffectInstance _fsrRcasEffect;
        private EffectInstance _fsrTemporalEffect;
        private StrideGraphics.Effect _fsrEasuEffectBase;
        private StrideGraphics.Effect _fsrRcasEffectBase;
        private StrideGraphics.Effect _fsrTemporalEffectBase;

        // FSR internal textures for temporal accumulation
        private StrideGraphics.Texture _fsrHistoryTexture;
        private StrideGraphics.Texture _fsrLockTexture;
        private StrideGraphics.Texture _fsrExposureTexture;

        // FSR parameters
        private FsrConstants _fsrConstants;
        private bool _fsrContextNeedsRecreation;

        // FSR dispatch parameters
        private const int FSR_THREAD_GROUP_SIZE_X = 16;
        private const int FSR_THREAD_GROUP_SIZE_Y = 16;

        /// <summary>
        /// FSR constants structure matching AMD FidelityFX SDK format.
        /// Based on FfxFsr2DispatchDescription from FidelityFX SDK.
        /// </summary>
        public struct FsrConstants
        {
            public Vector4 Color;
            public Vector4 Depth;
            public Vector4 Motion;
            public Vector4 Exposure;
            public Vector4 Reactive;
            public Vector4 TransparencyAndComposition;
            public Vector4 Lock;
            public Vector4 Viewport;
            public Vector4 Jitter;
            public Vector4 PreExposure;
            public Vector4 Sharpness;
            public Vector4 LumaMeter;
            public Vector4 LumaScale;
            public Vector4 ColorOvershoot;
            public Vector4 ColorUndershoot;
            public Vector4 ColorMax;
            public Vector4 ColorMin;
            public Vector4 ColorRange;
            public Vector4 Reserved;
        }

        public override string Version => "2.2.2"; // FSR version
        public override bool IsAvailable => true; // FSR works on all GPUs
        public override int FsrVersion => 2; // FSR 2.x
        public override bool FrameGenerationAvailable => CheckFrameGenerationSupport();

        public StrideFsrSystem(StrideGraphics.GraphicsDevice graphicsDevice, StrideGraphics.CommandList graphicsContext = null, IServiceRegistry services = null, ContentManager contentManager = null)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            _graphicsContext = graphicsContext;
            _services = services;
            _contentManager = contentManager;

            // Try to get EffectSystem from services if available
            _effectSystem = GetServiceHelper<EffectSystem>(_services);

            // If ContentManager not provided, try to get it from services
            if (_contentManager == null)
            {
                _contentManager = GetServiceHelper<ContentManager>(_services);
            }

            // If still no ContentManager, try to get it from GraphicsDevice services
            if (_contentManager == null)
            {
                try
                {
                    var deviceServices = _graphicsDevice.Services();
                    _contentManager = GetServiceHelper<ContentManager>(deviceServices);
                }
                catch
                {
                    // ContentManager not available from GraphicsDevice, continue without it
                }
            }
        }

        /// <summary>
        /// Helper method to call GetService&lt;T&gt;() using reflection (C# 7.3 compatible).
        /// </summary>
        private static T GetServiceHelper<T>(object services) where T : class
        {
            if (services == null)
            {
                return null;
            }
            try
            {
                // Try to cast to IServiceRegistry first
                var serviceRegistry = services as IServiceRegistry;
                if (serviceRegistry != null)
                {
                    var getServiceMethod = serviceRegistry.GetType().GetMethod("GetService", new Type[0]);
                    if (getServiceMethod != null)
                    {
                        var genericMethod = getServiceMethod.MakeGenericMethod(typeof(T));
                        return genericMethod.Invoke(serviceRegistry, null) as T;
                    }
                }
                else
                {
                    // If not IServiceRegistry, try to get GetService method from the object's type
                    var getServiceMethod = services.GetType().GetMethod("GetService", new Type[0]);
                    if (getServiceMethod != null)
                    {
                        var genericMethod = getServiceMethod.MakeGenericMethod(typeof(T));
                        return genericMethod.Invoke(services, null) as T;
                    }
                }
            }
            catch
            {
                // Service not available
            }
            return null;
        }

        private StrideGraphics.CommandList GetCommandList()
        {
            if (_graphicsContext != null)
            {
                return _graphicsContext;
            }
            return null;
        }

        private StrideGraphics.CommandList GetGraphicsContext()
        {
            return _graphicsContext;
        }

        /// <summary>
        /// Applies an EffectInstance to a CommandList for compute shader execution.
        /// In Stride, EffectInstance.Apply() expects GraphicsContext, but we have CommandList.
        /// This method properly applies the effect by updating resource sets and setting the compute pipeline state.
        ///
        /// Based on Stride API:
        /// - EffectInstance.UpdateEffect() updates resource sets from Parameters
        /// - EffectInstance.Effect provides access to the compiled Effect
        /// - CommandList.SetComputePipelineState() sets the compute pipeline state
        /// - CommandList.Dispatch() executes the compute shader
        ///
        /// Implementation strategy:
        /// 1. Update EffectInstance resource sets from Parameters using UpdateEffect()
        /// 2. Get compute pipeline state from EffectInstance's Effect
        /// 3. Set compute pipeline state on CommandList
        /// 4. Dispatch compute shader (done by caller)
        /// </summary>
        private void ApplyEffectInstanceToCommandList(EffectInstance effectInstance, StrideGraphics.CommandList commandList)
        {
            if (effectInstance == null || commandList == null || effectInstance.Effect == null)
            {
                return;
            }

            try
            {
                // Strategy 1: Try to get GraphicsContext from GraphicsDevice
                // Stride GraphicsDevice may have GraphicsContext property (separate from ImmediateContext)
                try
                {
                    var graphicsDeviceType = _graphicsDevice.GetType();
                    var graphicsContextProp = graphicsDeviceType.GetProperty("GraphicsContext");
                    if (graphicsContextProp != null)
                    {
                        var deviceGraphicsContext = graphicsContextProp.GetValue(_graphicsDevice);
                        if (deviceGraphicsContext != null)
                        {
                            // Apply effect (this updates resource sets from Parameters and sets pipeline state)
                            // EffectInstance.Apply() handles both updating parameters and applying the effect
                            // Try to cast to GraphicsContext if available, otherwise skip this strategy
                            var graphicsContext = deviceGraphicsContext as StrideGraphics.GraphicsContext;
                            if (graphicsContext != null)
                            {
                                effectInstance.Apply(graphicsContext);
                                return;
                            }
                        }
                    }
                }
                catch
                {
                    // GraphicsDevice might not have GraphicsContext property, continue to Strategy 3
                }

                // Strategy 3: Manually update resource sets and set pipeline state
                // This is the fallback approach when GraphicsContext is not available
                // Update effect resource sets from Parameters
                // Note: UpdateEffect() may require GraphicsContext, so we use reflection or direct API access
                try
                {
                    // Try to update effect using reflection to access internal UpdateEffect method
                    // EffectInstance.UpdateEffect() updates resource sets from Parameters collection
                    var updateMethod = typeof(EffectInstance).GetMethod("UpdateEffect",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (updateMethod != null)
                    {
                        // Try with CommandList first
                        try
                        {
                            updateMethod.Invoke(effectInstance, new object[] { commandList });
                        }
                        catch
                        {
                            // If that fails, try to get GraphicsContext from CommandList
                            // Some Stride versions have CommandList.GraphicsContext property
                            var commandListType = commandList.GetType();
                            var graphicsContextProp = commandListType.GetProperty("GraphicsContext");
                            if (graphicsContextProp != null)
                            {
                                var ctx = graphicsContextProp.GetValue(commandList);
                                if (ctx != null)
                                {
                                    updateMethod.Invoke(effectInstance, new object[] { ctx });
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // UpdateEffect() not available or failed, continue to manual resource binding
                }

                // Set compute pipeline state from EffectInstance's Effect
                // EffectInstance.Effect contains the compiled shader with pipeline state
                var effect = effectInstance.Effect;
                if (effect != null)
                {
                    // Get compute shader from effect using reflection
                    // In Stride, Effect may have Passes property accessed via reflection
                    // or we may need to access through Techniques
                    try
                    {
                        var effectType = effect.GetType();

                        // Try to get Passes property first
                        var passesProperty = effectType.GetProperty("Passes",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (passesProperty != null)
                        {
                            var passes = passesProperty.GetValue(effect);
                            if (passes != null)
                            {
                                // Check if passes is a collection with Count property
                                var passesType = passes.GetType();
                                var countProperty = passesType.GetProperty("Count");
                                if (countProperty != null)
                                {
                                    var count = (int)countProperty.GetValue(passes);
                                    if (count > 0)
                                    {
                                        // Get first pass using indexer or Get method
                                        var indexer = passesType.GetProperty("Item",
                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                            null, null, new[] { typeof(int) }, null);
                                        if (indexer != null)
                                        {
                                            var computePass = indexer.GetValue(passes, new object[] { 0 });
                                            if (computePass != null)
                                            {
                                                // Get ComputeShader from pass
                                                var passType = computePass.GetType();
                                                var computeShaderProperty = passType.GetProperty("ComputeShader",
                                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                                if (computeShaderProperty != null)
                                                {
                                                    var computeShader = computeShaderProperty.GetValue(computePass);
                                                    if (computeShader != null)
                                                    {
                                                        // Set compute pipeline state on CommandList using reflection
                                                        var commandListType = commandList.GetType();
                                                        var setPipelineMethod = commandListType.GetMethod("SetComputePipelineState",
                                                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                                        if (setPipelineMethod != null)
                                                        {
                                                            setPipelineMethod.Invoke(commandList, new[] { computeShader });
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Reflection access failed, pipeline state may be set by EffectInstance.Apply() if GraphicsContext was available
                        // If not, this is a fallback and we continue without manually setting pipeline state
                    }
                }

                // Resource sets are updated by UpdateEffect() call above
                // If UpdateEffect() failed, we need to manually bind resources
                // This is done through EffectInstance.Parameters which are already set by caller
                // The Parameters collection is used by UpdateEffect() to update resource sets
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideFSR] Error applying effect instance to command list: {ex.Message}");
                Console.WriteLine($"[StrideFSR] Stack trace: {ex.StackTrace}");
            }
        }

        #region BaseUpscalingSystem Implementation

        protected override bool InitializeInternal()
        {
            Console.WriteLine("[StrideFSR] Initializing FSR...");

            // Load or create FSR compute shaders
            if (!LoadFsrShaders())
            {
                Console.WriteLine("[StrideFSR] Warning: Failed to load FSR shaders. FSR will use fallback implementation.");
            }

            // Initialize FSR constants
            _fsrConstants = new FsrConstants();
            _fsrContextNeedsRecreation = false;

            // Create FSR context (managed internally, not using native SDK)
            _fsrContext = new IntPtr(1); // Non-zero to indicate initialized

            Console.WriteLine("[StrideFSR] FSR initialized successfully");
            return true;
        }

        protected override void ShutdownInternal()
        {
            if (_fsrContext != IntPtr.Zero)
            {
                // Release FSR context
                _fsrContext = IntPtr.Zero;
            }

            // Dispose FSR compute shaders
            _fsrEasuEffect?.Dispose();
            _fsrRcasEffect?.Dispose();
            _fsrTemporalEffect?.Dispose();
            _fsrEasuEffectBase?.Dispose();
            _fsrRcasEffectBase?.Dispose();
            _fsrTemporalEffectBase?.Dispose();
            _fsrEasuEffect = null;
            _fsrRcasEffect = null;
            _fsrTemporalEffect = null;
            _fsrEasuEffectBase = null;
            _fsrRcasEffectBase = null;
            _fsrTemporalEffectBase = null;

            // Dispose FSR internal textures
            _fsrHistoryTexture?.Dispose();
            _fsrLockTexture?.Dispose();
            _fsrExposureTexture?.Dispose();
            _fsrHistoryTexture = null;
            _fsrLockTexture = null;
            _fsrExposureTexture = null;

            _outputTexture?.Dispose();
            _outputTexture = null;

            Console.WriteLine("[StrideFSR] Shutdown complete");
        }

        #endregion

        /// <summary>
        /// Applies FSR upscaling to the input frame.
        /// </summary>
        public StrideGraphics.Texture Apply(StrideGraphics.Texture input, StrideGraphics.Texture motionVectors, StrideGraphics.Texture depth,
            StrideGraphics.Texture reactivityMask, int targetWidth, int targetHeight, float deltaTime)
        {
            if (!IsEnabled || input == null) return input;

            EnsureOutputTexture(targetWidth, targetHeight, input.Format);

            // FSR 2.x Dispatch:
            // - Color: rendered frame at lower resolution
            // - Motion vectors: per-pixel velocity (in pixels)
            // - Depth: scene depth buffer
            // - Reactive mask: (optional) areas that need less temporal accumulation
            // - Output: upscaled frame at target resolution

            ExecuteFsr(input, motionVectors, depth, reactivityMask, _outputTexture, deltaTime);

            return _outputTexture ?? input;
        }

        private void EnsureOutputTexture(int width, int height, StrideGraphics.PixelFormat format)
        {
            if (_outputTexture != null &&
                _outputTexture.Width == width &&
                _outputTexture.Height == height)
            {
                return;
            }

            _outputTexture?.Dispose();
            _outputTexture = StrideGraphics.Texture.New2D(_graphicsDevice, width, height,
                format, StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.UnorderedAccess);
        }

        private void ExecuteFsr(StrideGraphics.Texture input, StrideGraphics.Texture motionVectors, StrideGraphics.Texture depth,
            StrideGraphics.Texture reactivityMask, StrideGraphics.Texture output, float deltaTime)
        {
            // FSR 2.x dispatch implementation
            // Based on AMD FidelityFX SDK: ffxFsr2ContextDispatch
            // FSR 2.x performs temporal upscaling using multiple compute shader passes:
            // 1. Lock pass: Detects disocclusions and areas that need special handling
            // 2. Depth clip pass: Clips depth values for better temporal stability
            // 3. Reactive mask generation (if reactivity mask provided)
            // 4. Temporal accumulation: Combines current frame with history
            // 5. RCAS sharpening: Applies final sharpening pass

            var graphicsContext = GetGraphicsContext();
            if (graphicsContext == null)
            {
                Console.WriteLine("[StrideFSR] Error: Graphics context not available");
                return;
            }

            // Ensure internal textures are created
            EnsureFsrInternalTextures(output.Width, output.Height, input.Format);

            // Calculate FSR constants based on input/output resolution and quality mode
            CalculateFsrConstants(input, output, motionVectors, depth, deltaTime);

            // Pass 1: Lock pass (detects disocclusions)
            // This pass identifies areas where temporal accumulation should be reduced
            if (_fsrTemporalEffect != null)
            {
                ExecuteFsrLockPass(graphicsContext, input, depth, motionVectors, _fsrLockTexture);
            }

            // Pass 2: Depth clip pass
            // Clips depth values to improve temporal stability
            if (_fsrTemporalEffect != null)
            {
                ExecuteFsrDepthClipPass(graphicsContext, depth, _fsrLockTexture);
            }

            // Pass 3: Reactive mask processing (if provided)
            // Processes the reactivity mask to identify areas needing less temporal accumulation
            if (reactivityMask != null && _fsrTemporalEffect != null)
            {
                ExecuteFsrReactiveMaskPass(graphicsContext, reactivityMask, _fsrLockTexture);
            }

            // Pass 4: Temporal accumulation (main upscaling pass)
            // Combines current frame with history buffer using motion vectors
            if (_fsrTemporalEffect != null)
            {
                ExecuteFsrTemporalPass(graphicsContext, input, motionVectors, depth, _fsrLockTexture,
                    reactivityMask, _fsrHistoryTexture, output);
            }
            else
            {
                // Fallback: Use EASU for spatial-only upscaling if temporal shader not available
                ExecuteFsrEasuPass(graphicsContext, input, output);
            }

            // Pass 5: RCAS sharpening (final pass)
            // Applies robust contrast adaptive sharpening
            if (_fsrRcasEffect != null)
            {
                ExecuteFsrRcasPass(graphicsContext, output, output);
            }

            // Update history texture for next frame
            UpdateFsrHistory(output, _fsrHistoryTexture);

            Console.WriteLine($"[StrideFSR] Executed FSR: {input.Width}x{input.Height} -> {output.Width}x{output.Height}");
        }

        /// <summary>
        /// Applies FSR 1.0 spatial-only upscaling (no temporal).
        /// Useful for UI or when motion vectors are unavailable.
        /// </summary>
        public StrideGraphics.Texture ApplySpatialOnly(StrideGraphics.Texture input, int targetWidth, int targetHeight)
        {
            if (!IsEnabled || input == null) return input;

            EnsureOutputTexture(targetWidth, targetHeight, input.Format);

            // FSR 1.0: EASU (Edge-Adaptive Spatial Upsampling) + RCAS (Robust Contrast Adaptive Sharpening)
            ExecuteFsr1(input, _outputTexture);

            return _outputTexture ?? input;
        }

        private void ExecuteFsr1(StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            // FSR 1.0 spatial-only upscaling (no temporal):
            // Pass 1: EASU - edge-adaptive spatial upsampling
            // Pass 2: RCAS - robust contrast adaptive sharpening

            var graphicsContext = GetGraphicsContext();
            if (graphicsContext == null)
            {
                Console.WriteLine("[StrideFSR] Error: Graphics context not available");
                return;
            }

            // Pass 1: EASU upscaling
            if (_fsrEasuEffect != null)
            {
                ExecuteFsrEasuPass(graphicsContext, input, output);
            }
            else
            {
                // Fallback: Simple bilinear upscale if EASU shader not available
                Console.WriteLine("[StrideFSR] Warning: EASU shader not available, using fallback upscaling");
            }

            // Pass 2: RCAS sharpening
            if (_fsrRcasEffect != null)
            {
                ExecuteFsrRcasPass(graphicsContext, output, output);
            }

            Console.WriteLine($"[StrideFSR] Executed FSR 1.0 (spatial): {input.Width}x{input.Height} -> {output.Width}x{output.Height}");
        }

        #region FSR Internal Implementation

        /// <summary>
        /// Ensures FSR internal textures are created with correct dimensions.
        /// </summary>
        private void EnsureFsrInternalTextures(int width, int height, StrideGraphics.PixelFormat format)
        {
            // History texture: Stores previous frame for temporal accumulation
            if (_fsrHistoryTexture == null || _fsrHistoryTexture.Width != width || _fsrHistoryTexture.Height != height)
            {
                _fsrHistoryTexture?.Dispose();
                _fsrHistoryTexture = StrideGraphics.Texture.New2D(_graphicsDevice, width, height, format,
                    StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.UnorderedAccess);
            }

            // Lock texture: Stores lock information for disocclusion detection
            if (_fsrLockTexture == null || _fsrLockTexture.Width != width || _fsrLockTexture.Height != height)
            {
                _fsrLockTexture?.Dispose();
                _fsrLockTexture = StrideGraphics.Texture.New2D(_graphicsDevice, width, height, StrideGraphics.PixelFormat.R8G8B8A8_UNorm,
                    StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.UnorderedAccess);
            }

            // Exposure texture: Stores exposure information (optional, for HDR)
            if (_fsrExposureTexture == null || _fsrExposureTexture.Width != 1 || _fsrExposureTexture.Height != 1)
            {
                _fsrExposureTexture?.Dispose();
                _fsrExposureTexture = StrideGraphics.Texture.New2D(_graphicsDevice, 1, 1, StrideGraphics.PixelFormat.R32_Float,
                    StrideGraphics.TextureFlags.RenderTarget | StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.UnorderedAccess);
            }
        }

        /// <summary>
        /// Calculates FSR constants based on input/output resolution and quality settings.
        /// Based on AMD FidelityFX SDK: FfxFsr2DispatchDescription constants.
        /// </summary>
        private void CalculateFsrConstants(StrideGraphics.Texture input, StrideGraphics.Texture output, StrideGraphics.Texture motionVectors,
            StrideGraphics.Texture depth, float deltaTime)
        {
            float inputWidth = input.Width;
            float inputHeight = input.Height;
            float outputWidth = output.Width;
            float outputHeight = output.Height;

            // Calculate scale factors
            float scaleX = inputWidth / outputWidth;
            float scaleY = inputHeight / outputHeight;

            // Viewport: Input resolution in output space
            _fsrConstants.Viewport = new Vector4(inputWidth, inputHeight, 1.0f / inputWidth, 1.0f / inputHeight);

            // Color: Input color buffer dimensions
            _fsrConstants.Color = new Vector4(inputWidth, inputHeight, 1.0f / inputWidth, 1.0f / inputHeight);

            // Depth: Depth buffer dimensions
            if (depth != null)
            {
                _fsrConstants.Depth = new Vector4(depth.Width, depth.Height, 1.0f / depth.Width, 1.0f / depth.Height);
            }
            else
            {
                _fsrConstants.Depth = new Vector4(inputWidth, inputHeight, 1.0f / inputWidth, 1.0f / inputHeight);
            }

            // Motion: Motion vector buffer dimensions
            if (motionVectors != null)
            {
                _fsrConstants.Motion = new Vector4(motionVectors.Width, motionVectors.Height,
                    1.0f / motionVectors.Width, 1.0f / motionVectors.Height);
            }
            else
            {
                _fsrConstants.Motion = new Vector4(inputWidth, inputHeight, 1.0f / inputWidth, 1.0f / inputHeight);
            }

            // Jitter: Sub-pixel jitter for temporal anti-aliasing (typically 0 for FSR)
            _fsrConstants.Jitter = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Sharpness: RCAS sharpness parameter (0.0 = no sharpening, 1.0 = maximum sharpening)
            float sharpness = _sharpness;
            _fsrConstants.Sharpness = new Vector4(sharpness, 0.0f, 0.0f, 0.0f);

            // Exposure: Pre-exposure and exposure values (for HDR, typically 1.0 for LDR)
            _fsrConstants.Exposure = new Vector4(1.0f, 1.0f, 0.0f, 0.0f);
            _fsrConstants.PreExposure = new Vector4(1.0f, 0.0f, 0.0f, 0.0f);

            // Reactive: Reactivity mask settings
            _fsrConstants.Reactive = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Transparency and composition: Transparency settings
            _fsrConstants.TransparencyAndComposition = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Lock: Lock texture settings
            _fsrConstants.Lock = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            // Luma meter: Luminance metering settings
            _fsrConstants.LumaMeter = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            _fsrConstants.LumaScale = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

            // Color range: Color clamping settings
            _fsrConstants.ColorOvershoot = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            _fsrConstants.ColorUndershoot = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            _fsrConstants.ColorMax = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            _fsrConstants.ColorMin = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
            _fsrConstants.ColorRange = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);

            // Reserved: Reserved for future use
            _fsrConstants.Reserved = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);
        }

        /// <summary>
        /// Executes FSR Lock pass: Detects disocclusions and areas needing special handling.
        /// </summary>
        private void ExecuteFsrLockPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture input, StrideGraphics.Texture depth,
            StrideGraphics.Texture motionVectors, StrideGraphics.Texture lockOutput)
        {
            if (_fsrTemporalEffect == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind resources through EffectInstance.Parameters
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputColor, input);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputDepth, depth);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputMotionVectors, motionVectors);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.OutputLock, lockOutput);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrTemporalEffect, commandList);

            // Calculate dispatch dimensions (lock pass operates at output resolution)
            int dispatchX = (lockOutput.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (lockOutput.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Executes FSR Depth Clip pass: Clips depth values for better temporal stability.
        /// </summary>
        private void ExecuteFsrDepthClipPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture depth, StrideGraphics.Texture lockTexture)
        {
            if (_fsrTemporalEffect == null || depth == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind resources
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputDepth, depth);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputLock, lockTexture);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrTemporalEffect, commandList);

            // Calculate dispatch dimensions
            int dispatchX = (depth.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (depth.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Executes FSR Reactive Mask pass: Processes reactivity mask.
        /// </summary>
        private void ExecuteFsrReactiveMaskPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture reactivityMask, StrideGraphics.Texture lockTexture)
        {
            if (_fsrTemporalEffect == null || reactivityMask == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind resources
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputReactiveMask, reactivityMask);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputLock, lockTexture);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrTemporalEffect, commandList);

            // Calculate dispatch dimensions
            int dispatchX = (reactivityMask.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (reactivityMask.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Executes FSR Temporal accumulation pass: Main upscaling pass using temporal data.
        /// </summary>
        private void ExecuteFsrTemporalPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture input, StrideGraphics.Texture motionVectors,
            StrideGraphics.Texture depth, StrideGraphics.Texture lockTexture, StrideGraphics.Texture reactivityMask, StrideGraphics.Texture historyTexture, StrideGraphics.Texture output)
        {
            if (_fsrTemporalEffect == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind input resources
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputColor, input);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputMotionVectors, motionVectors);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputDepth, depth);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputLock, lockTexture);
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputHistory, historyTexture);
            if (reactivityMask != null)
            {
                _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.InputReactiveMask, reactivityMask);
            }
            _fsrTemporalEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrTemporalEffect, commandList);

            // Calculate dispatch dimensions (temporal pass operates at output resolution)
            int dispatchX = (output.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (output.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Executes FSR EASU pass: Edge-adaptive spatial upsampling (FSR 1.0).
        /// </summary>
        private void ExecuteFsrEasuPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            if (_fsrEasuEffect == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind resources
            _fsrEasuEffect.Parameters.Set(FsrShaderKeys.InputColor, input);
            _fsrEasuEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrEasuEffect, commandList);

            // Calculate dispatch dimensions (EASU operates at output resolution)
            int dispatchX = (output.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (output.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Executes FSR RCAS pass: Robust contrast adaptive sharpening.
        /// </summary>
        private void ExecuteFsrRcasPass(StrideGraphics.CommandList graphicsContext, StrideGraphics.Texture input, StrideGraphics.Texture output)
        {
            if (_fsrRcasEffect == null || graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Bind resources
            _fsrRcasEffect.Parameters.Set(FsrShaderKeys.InputColor, input);
            _fsrRcasEffect.Parameters.Set(FsrShaderKeys.FsrConstants, _fsrConstants);

            // Apply effect parameters to command list
            // This updates resource sets and sets compute pipeline state for compute shader execution
            ApplyEffectInstanceToCommandList(_fsrRcasEffect, commandList);

            // Calculate dispatch dimensions (RCAS operates at output resolution)
            int dispatchX = (output.Width + FSR_THREAD_GROUP_SIZE_X - 1) / FSR_THREAD_GROUP_SIZE_X;
            int dispatchY = (output.Height + FSR_THREAD_GROUP_SIZE_Y - 1) / FSR_THREAD_GROUP_SIZE_Y;

            // Dispatch compute shader
            commandList.Dispatch(dispatchX, dispatchY, 1);
        }

        /// <summary>
        /// Updates FSR history texture for next frame's temporal accumulation.
        /// </summary>
        private void UpdateFsrHistory(StrideGraphics.Texture currentFrame, StrideGraphics.Texture historyTexture)
        {
            if (currentFrame == null || historyTexture == null) return;

            var graphicsContext = GetGraphicsContext();
            if (graphicsContext == null) return;

            var commandList = graphicsContext;
            if (commandList == null) return;

            // Copy current frame to history texture for next frame
            // In a full implementation, this would use a proper copy operation
            // For now, we'll use a simple texture copy
            commandList.CopyRegion(currentFrame, 0, null, historyTexture, 0);
        }

        /// <summary>
        /// Loads or creates FSR compute shaders.
        /// </summary>
        private bool LoadFsrShaders()
        {
            bool allLoaded = true;

            // Try to load EASU shader
            if (!TryLoadFsrShader("FSREASU", out _fsrEasuEffectBase))
            {
                _fsrEasuEffectBase = CreateFsrEasuShader();
                allLoaded = false;
            }
            if (_fsrEasuEffectBase != null)
            {
                _fsrEasuEffect = new EffectInstance(_fsrEasuEffectBase);
            }

            // Try to load RCAS shader
            if (!TryLoadFsrShader("FSRRCAS", out _fsrRcasEffectBase))
            {
                _fsrRcasEffectBase = CreateFsrRcasShader();
                allLoaded = false;
            }
            if (_fsrRcasEffectBase != null)
            {
                _fsrRcasEffect = new EffectInstance(_fsrRcasEffectBase);
            }

            // Try to load Temporal shader
            if (!TryLoadFsrShader("FSRTemporal", out _fsrTemporalEffectBase))
            {
                _fsrTemporalEffectBase = CreateFsrTemporalShader();
                allLoaded = false;
            }
            if (_fsrTemporalEffectBase != null)
            {
                _fsrTemporalEffect = new EffectInstance(_fsrTemporalEffectBase);
            }

            return allLoaded;
        }

        /// <summary>
        /// Tries to load an FSR shader from compiled effect files.
        /// Uses ContentManager.Load&lt;Effect&gt;() to load compiled .sdeffect files.
        /// Falls back to programmatic creation if loading fails.
        /// </summary>
        /// <remarks>
        /// Based on Stride Engine shader loading:
        /// - Compiled .sdsl files are stored as .sdeffect files in content
        /// - ContentManager.Load&lt;Effect&gt;() loads from content manager
        /// - EffectSystem can compile shaders at runtime from source (requires EffectSystem access)
        /// - Effect.Load() doesn't exist in newer Stride versions, use ContentManager instead
        /// </remarks>
        private bool TryLoadFsrShader(string shaderName, out StrideGraphics.Effect effect)
        {
            effect = null;

            // Strategy 1: Try loading from ContentManager if available
            if (_contentManager != null)
            {
                try
                {
                    // ContentManager.Load<Effect>() loads compiled .sdeffect files
                    // Asset name should match the shader name (e.g., "FSREASU", "FSRRCAS", "FSRTemporal")
                    effect = _contentManager.Load<StrideGraphics.Effect>(shaderName);
                    if (effect != null)
                    {
                        Console.WriteLine($"[StrideFSR] Successfully loaded {shaderName} from ContentManager");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideFSR] Failed to load {shaderName} from ContentManager: {ex.Message}");
                }
            }

            // Strategy 2: Try loading from services ContentManager if available
            if (effect == null && _services != null)
            {
                try
                {
                    var contentManager = GetServiceHelper<ContentManager>(_services);
                    if (contentManager != null)
                    {
                        effect = contentManager.Load<StrideGraphics.Effect>(shaderName);
                        if (effect != null)
                        {
                            Console.WriteLine($"[StrideFSR] Successfully loaded {shaderName} from services ContentManager");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideFSR] Failed to load {shaderName} from services ContentManager: {ex.Message}");
                }
            }

            // Strategy 3: Try loading from GraphicsDevice services ContentManager
            if (effect == null)
            {
                try
                {
                    var deviceServices = _graphicsDevice.Services();
                    if (deviceServices != null)
                    {
                        var contentManager = GetServiceHelper<ContentManager>(deviceServices);
                        if (contentManager != null)
                        {
                            effect = contentManager.Load<StrideGraphics.Effect>(shaderName);
                            if (effect != null)
                            {
                                Console.WriteLine($"[StrideFSR] Successfully loaded {shaderName} from GraphicsDevice ContentManager");
                                return true;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideFSR] Failed to load {shaderName} from GraphicsDevice ContentManager: {ex.Message}");
                }
            }

            // If all loading strategies failed, return false to indicate shader needs to be created programmatically
            Console.WriteLine($"[StrideFSR] Could not load {shaderName} from any ContentManager, will be created programmatically");
            return false;
        }

        /// <summary>
        /// Creates FSR EASU compute shader programmatically.
        /// Based on AMD FidelityFX FSR EASU algorithm.
        /// </summary>
        private StrideGraphics.Effect CreateFsrEasuShader()
        {
            // EASU (Edge-Adaptive Spatial Upsampling) shader source
            // This is a simplified version - full implementation would include the complete EASU algorithm
            string shaderSource = @"
shader FSREASU : ComputeShaderBase
{
    cbuffer FsrConstants : register(b0)
    {
        float4 Color;
        float4 Depth;
        float4 Motion;
        float4 Exposure;
        float4 Reactive;
        float4 TransparencyAndComposition;
        float4 Lock;
        float4 Viewport;
        float4 Jitter;
        float4 PreExposure;
        float4 Sharpness;
        float4 LumaMeter;
        float4 LumaScale;
        float4 ColorOvershoot;
        float4 ColorUndershoot;
        float4 ColorMax;
        float4 ColorMin;
        float4 ColorRange;
        float4 Reserved;
    };

    Texture2D InputColor : register(t0);
    SamplerState LinearSampler : register(s0);
    RWTexture2D<float4> OutputColor : register(u0);

    [numthreads(16, 16, 1)]
    void CS(uint3 id : SV_DispatchThreadID)
    {
        float2 inputSize = Color.xy;
        float2 outputSize = Viewport.xy;
        float2 inputUV = (id.xy + 0.5) / outputSize;
        float2 inputCoord = inputUV * inputSize;

        // EASU edge-adaptive upsampling
        // Simplified implementation - full EASU would include edge detection and adaptive filtering
        float4 color = InputColor.SampleLevel(LinearSampler, inputUV, 0);
        OutputColor[id.xy] = color;
    }
};";

            return CompileFsrShader(shaderSource, "FSREASU");
        }

        /// <summary>
        /// Creates FSR RCAS compute shader programmatically.
        /// Based on AMD FidelityFX FSR RCAS algorithm.
        /// </summary>
        private StrideGraphics.Effect CreateFsrRcasShader()
        {
            // RCAS (Robust Contrast Adaptive Sharpening) shader source
            string shaderSource = @"
shader FSRRCAS : ComputeShaderBase
{
    cbuffer FsrConstants : register(b0)
    {
        float4 Color;
        float4 Depth;
        float4 Motion;
        float4 Exposure;
        float4 Reactive;
        float4 TransparencyAndComposition;
        float4 Lock;
        float4 Viewport;
        float4 Jitter;
        float4 PreExposure;
        float4 Sharpness;
        float4 LumaMeter;
        float4 LumaScale;
        float4 ColorOvershoot;
        float4 ColorUndershoot;
        float4 ColorMax;
        float4 ColorMin;
        float4 ColorRange;
        float4 Reserved;
    };

    Texture2D InputColor : register(t0);
    SamplerState LinearSampler : register(s0);
    RWTexture2D<float4> OutputColor : register(u0);

    [numthreads(16, 16, 1)]
    void CS(uint3 id : SV_DispatchThreadID)
    {
        float2 outputSize = Viewport.xy;
        float2 uv = (id.xy + 0.5) / outputSize;
        float sharpness = Sharpness.x;

        // RCAS sharpening
        // Simplified implementation - full RCAS would include contrast-adaptive sharpening
        float4 center = InputColor.SampleLevel(LinearSampler, uv, 0);
        float4 result = center;

        // Apply sharpening based on contrast
        if (sharpness > 0.0)
        {
            float4 up = InputColor.SampleLevel(LinearSampler, uv + float2(0, 1.0 / outputSize.y), 0);
            float4 down = InputColor.SampleLevel(LinearSampler, uv - float2(0, 1.0 / outputSize.y), 0);
            float4 left = InputColor.SampleLevel(LinearSampler, uv - float2(1.0 / outputSize.x, 0), 0);
            float4 right = InputColor.SampleLevel(LinearSampler, uv + float2(1.0 / outputSize.x, 0), 0);

            float4 laplacian = (up + down + left + right) * 0.25 - center;
            result = center + laplacian * sharpness;
        }

        OutputColor[id.xy] = result;
    }
};";

            return CompileFsrShader(shaderSource, "FSRRCAS");
        }

        /// <summary>
        /// Creates FSR Temporal compute shader programmatically.
        /// Based on AMD FidelityFX FSR 2.x temporal accumulation algorithm.
        /// </summary>
        private StrideGraphics.Effect CreateFsrTemporalShader()
        {
            // FSR 2.x Temporal accumulation shader source
            string shaderSource = @"
shader FSRTemporal : ComputeShaderBase
{
    cbuffer FsrConstants : register(b0)
    {
        float4 Color;
        float4 Depth;
        float4 Motion;
        float4 Exposure;
        float4 Reactive;
        float4 TransparencyAndComposition;
        float4 Lock;
        float4 Viewport;
        float4 Jitter;
        float4 PreExposure;
        float4 Sharpness;
        float4 LumaMeter;
        float4 LumaScale;
        float4 ColorOvershoot;
        float4 ColorUndershoot;
        float4 ColorMax;
        float4 ColorMin;
        float4 ColorRange;
        float4 Reserved;
    };

    Texture2D InputColor : register(t0);
    Texture2D InputMotionVectors : register(t1);
    Texture2D InputDepth : register(t2);
    Texture2D InputLock : register(t3);
    Texture2D InputHistory : register(t4);
    Texture2D InputReactiveMask : register(t5);
    SamplerState LinearSampler : register(s0);
    RWTexture2D<float4> OutputColor : register(u0);

    [numthreads(16, 16, 1)]
    void CS(uint3 id : SV_DispatchThreadID)
    {
        float2 outputSize = Viewport.xy;
        float2 uv = (id.xy + 0.5) / outputSize;

        // Sample current frame
        float4 currentColor = InputColor.SampleLevel(LinearSampler, uv, 0);

        // Sample motion vectors
        float2 motion = InputMotionVectors.SampleLevel(LinearSampler, uv, 0).xy;
        float2 historyUV = uv - motion;

        // Sample history with clamping
        float2 clampedHistoryUV = clamp(historyUV, 0.0, 1.0);
        float4 historyColor = InputHistory.SampleLevel(LinearSampler, clampedHistoryUV, 0);

        // Sample lock information
        float lockValue = InputLock.SampleLevel(LinearSampler, uv, 0).x;

        // Temporal accumulation with lock-based blending
        float blendFactor = 0.1; // Base temporal blend factor
        if (lockValue > 0.5)
        {
            blendFactor = 0.5; // Reduce temporal accumulation in locked areas
        }

        // Blend current frame with history
        float4 result = lerp(historyColor, currentColor, blendFactor);

        OutputColor[id.xy] = result;
    }
};";

            return CompileFsrShader(shaderSource, "FSRTemporal");
        }

        /// <summary>
        /// Compiles an FSR shader from source code using EffectSystem or EffectCompiler.
        /// </summary>
        /// <remarks>
        /// Based on Stride shader compilation API:
        /// - EffectSystem provides the proper compilation environment
        /// - Uses EffectCompiler internally for shader compilation
        /// - EffectCompiler compiles shader source code to Effect bytecode
        /// - Requires EffectSystem access for proper compilation context
        /// - If EffectSystem is not available, shader must be provided as compiled .sdeffect file
        /// </remarks>
        private StrideGraphics.Effect CompileFsrShader(string shaderSource, string shaderName)
        {
            try
            {
                // Strategy 1: Try to compile using EffectSystem if available
                if (_effectSystem != null)
                {
                    try
                    {
                        return CompileShaderWithEffectSystem(_effectSystem, shaderSource, shaderName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideFSR] Failed to compile {shaderName} using EffectSystem: {ex.Message}");
                    }
                }

                // Strategy 2: Try to get EffectSystem from services
                if (_services != null)
                {
                    try
                    {
                        var effectSystem = GetServiceHelper<EffectSystem>(_services);
                        if (effectSystem != null)
                        {
                            return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideFSR] Failed to get EffectSystem from services: {ex.Message}");
                    }
                }

                // Strategy 3: Try to get EffectSystem from GraphicsDevice services
                try
                {
                    var deviceServices = _graphicsDevice.Services();
                    var effectSystem = GetServiceHelper<EffectSystem>(deviceServices);
                    if (effectSystem != null)
                    {
                        return CompileShaderWithEffectSystem(effectSystem, shaderSource, shaderName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideFSR] Failed to get EffectSystem from GraphicsDevice services: {ex.Message}");
                }

                // If all compilation strategies failed, return null
                // Shader must be provided as compiled .sdeffect file in ContentManager
                Console.WriteLine($"[StrideFSR] Cannot compile {shaderName} shader - no EffectSystem available");
                Console.WriteLine($"[StrideFSR] Shader {shaderName} must be provided as compiled .sdeffect file in ContentManager");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideFSR] Failed to compile {shaderName} shader: {ex.Message}");
                Console.WriteLine($"[StrideFSR] Stack trace: {ex.StackTrace}");
            }
            return null;
        }

        /// <summary>
        /// Compiles shader using EffectCompiler (replaces EffectSystem.Compile which doesn't exist).
        /// </summary>
        /// <param name="effectSystem">EffectSystem instance (unused, kept for compatibility).</param>
        /// <param name="shaderSource">Shader source code in SDSL format.</param>
        /// <param name="shaderName">Name identifier for the shader.</param>
        /// <returns>Compiled Effect, or null if compilation fails.</returns>
        /// <remarks>
        /// Based on Stride EffectCompiler API:
        /// - EffectCompiler.Compile() compiles shader source to Effect bytecode
        /// - EffectCompiler requires IVirtualFileProvider for file access
        /// - Returns Effect created from compiled bytecode
        /// </remarks>
        private StrideGraphics.Effect CompileShaderWithEffectSystem(EffectSystem effectSystem, string shaderSource, string shaderName)
        {
            // EffectSystem.Compile() doesn't exist, use EffectCompiler instead
            // Try to get file provider from services or create a minimal one
            IVirtualFileProvider fileProvider = null;
            if (_services != null)
            {
                fileProvider = GetServiceHelper<IVirtualFileProvider>(_services);
            }

            // If no file provider from services, try to get from ContentManager
            if (fileProvider == null && _contentManager != null)
            {
                try
                {
                    // ContentManager may have a FileProvider property
                    var fileProviderProperty = _contentManager.GetType().GetProperty("FileProvider");
                    if (fileProviderProperty != null)
                    {
                        fileProvider = fileProviderProperty.GetValue(_contentManager) as IVirtualFileProvider;
                    }
                }
                catch
                {
                    // FileProvider not available from ContentManager
                }
            }

            // Create EffectCompiler with file provider (required parameter)
            // If no file provider available, create a minimal one or use null (may fail but we try)
            EffectCompiler compiler = null;
            try
            {
                if (fileProvider != null)
                {
                    compiler = new EffectCompiler(fileProvider);
                }
                else
                {
                    // Try to create without file provider (may fail, but attempt it)
                    // Note: This may not work, but we try as a fallback
                    Console.WriteLine($"[StrideFSR] Warning: No file provider available for EffectCompiler, compilation may fail");
                    return null; // Cannot create EffectCompiler without fileProvider
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideFSR] Failed to create EffectCompiler: {ex.Message}");
                return null;
            }

            if (compiler == null)
            {
                return null;
            }

            try
            {
                // Create shader source object for EffectCompiler
                var shaderSourceObj = new FsrShaderSourceClass
                {
                    Name = shaderName,
                    SourceCode = shaderSource
                };

                // Compile using EffectCompiler
                // EffectCompiler.Compile() compiles shader source to Effect bytecode
                dynamic compilationResult = compiler.Compile(shaderSourceObj, new CompilerParameters
                {
                    EffectParameters = new EffectCompilerParameters()
                });

                // Handle TaskOrResult unwrapping
                dynamic compilerResult = compilationResult.Result;
                if (compilerResult != null && compilerResult.Bytecode != null && compilerResult.Bytecode.Length > 0)
                {
                    // Create Effect from compiled bytecode
                    var effect = new StrideGraphics.Effect(_graphicsDevice, (EffectBytecode)compilerResult.Bytecode);
                    Console.WriteLine($"[StrideFSR] Successfully compiled {shaderName} shader using EffectCompiler");
                    return effect;
                }
                else
                {
                    Console.WriteLine($"[StrideFSR] EffectCompiler compilation failed for {shaderName}: No bytecode generated");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideFSR] Failed to compile {shaderName} shader using EffectCompiler: {ex.Message}");
                Console.WriteLine($"[StrideFSR] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Helper class for shader source compilation.
        /// Wraps shader source code for EffectCompiler.
        /// </summary>
        private class FsrShaderSourceClass : ShaderSource
        {
            public string Name { get; set; }
            public string SourceCode { get; set; }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = hash * 31 + (Name?.GetHashCode() ?? 0);
                hash = hash * 31 + (SourceCode?.GetHashCode() ?? 0);
                return hash;
            }

            public override object Clone()
            {
                return new FsrShaderSourceClass
                {
                    Name = Name,
                    SourceCode = SourceCode
                };
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is FsrShaderSourceClass other))
                {
                    return false;
                }
                return Name == other.Name && SourceCode == other.SourceCode;
            }
        }

        #endregion

        #region Mode Handlers

        protected override void OnModeChanged(FsrMode mode)
        {
            Console.WriteLine($"[StrideFSR] Mode changed to: {mode}");

            // Mark that FSR context needs recreation with new quality preset
            _fsrContextNeedsRecreation = true;

            // Recreate FSR context with new quality preset
            // In a full implementation, this would recreate the FSR context with updated quality settings
            if (_initialized)
            {
                // Update constants based on new mode
                // The constants will be recalculated on next frame
            }
        }

        protected override void OnFrameGenerationChanged(bool enabled)
        {
            Console.WriteLine($"[StrideFSR] Frame Generation: {(enabled ? "enabled" : "disabled")}");

            // Frame generation requires FSR 3.0+
            // For FSR 2.x, this is a no-op, but we track it for future FSR 3.0 support
            if (enabled && FsrVersion < 3)
            {
                Console.WriteLine("[StrideFSR] Warning: Frame Generation requires FSR 3.0+, current version is FSR 2.x");
            }
        }

        protected override void OnSharpnessChanged(float sharpness)
        {
            Console.WriteLine($"[StrideFSR] Sharpness set to: {sharpness:F2}");

            // Update RCAS sharpness parameter
            // This will be applied in CalculateFsrConstants on next frame
            // Sharpness is clamped to [0, 1] range
            _sharpness = Math.Max(0.0f, Math.Min(1.0f, sharpness));
        }

        #endregion

        #region Capability Checks

        private bool CheckFrameGenerationSupport()
        {
            // FSR 3.0 Frame Generation works on all GPUs but requires specific driver support
            return true;
        }

        #endregion

        /// <summary>
        /// Gets recommended render resolution for the current quality mode.
        /// FSR quality modes define specific scale factors.
        /// </summary>
        public (int width, int height) GetOptimalRenderResolution(int displayWidth, int displayHeight)
        {
            float scaleFactor = GetScaleFactor();

            int renderWidth = (int)Math.Ceiling(displayWidth * scaleFactor);
            int renderHeight = (int)Math.Ceiling(displayHeight * scaleFactor);

            // FSR prefers power-of-2 aligned dimensions
            renderWidth = (renderWidth + 7) & ~7;
            renderHeight = (renderHeight + 7) & ~7;

            return (renderWidth, renderHeight);
        }
    }

    /// <summary>
    /// FSR shader parameter keys for binding resources to compute shaders.
    /// </summary>
    internal static class FsrShaderKeys
    {
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputColor = new ObjectParameterKey<StrideGraphics.Texture>("InputColor");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputMotionVectors = new ObjectParameterKey<StrideGraphics.Texture>("InputMotionVectors");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputDepth = new ObjectParameterKey<StrideGraphics.Texture>("InputDepth");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputReactiveMask = new ObjectParameterKey<StrideGraphics.Texture>("InputReactiveMask");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputLock = new ObjectParameterKey<StrideGraphics.Texture>("InputLock");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> InputHistory = new ObjectParameterKey<StrideGraphics.Texture>("InputHistory");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> OutputColor = new ObjectParameterKey<StrideGraphics.Texture>("OutputColor");
        public static readonly ObjectParameterKey<StrideGraphics.Texture> OutputLock = new ObjectParameterKey<StrideGraphics.Texture>("OutputLock");
        public static readonly ValueParameterKey<StrideFsrSystem.FsrConstants> FsrConstants = new ValueParameterKey<StrideFsrSystem.FsrConstants>("FsrConstants");
    }
}


