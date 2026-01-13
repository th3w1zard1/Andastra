using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.PostProcessing
{
    /// <summary>
    /// Temporal Anti-Aliasing (TAA) implementation.
    /// 
    /// TAA uses information from previous frames to reduce aliasing,
    /// providing high-quality anti-aliasing with minimal performance cost.
    /// 
    /// Features:
    /// - History buffer for previous frame
    /// - Motion vector reprojection
    /// - Clipping to prevent ghosting
    /// - Jittered sampling
    /// </summary>
    public class TemporalAA : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _historyBuffer;
        private RenderTarget2D _currentFrame;
        private RenderTarget2D _outputBuffer;
        private SpriteBatch _spriteBatch;
        private int _width;
        private int _height;
        private int _frameNumber;
        private Vector2 _jitterOffset;
        private bool _isFirstFrame;

        /// <summary>
        /// Gets or sets the TAA strength (0-1).
        /// Higher values blend more history (better AA, more ghosting risk).
        /// Typical range: 0.05-0.1 for good balance.
        /// </summary>
        public float Strength { get; set; } = 0.05f;

        /// <summary>
        /// Gets or sets the blend factor for temporal accumulation (0-1).
        /// Lower values = more history (better AA), higher values = more current frame (less ghosting).
        /// Default: 0.05 (95% history, 5% current frame).
        /// </summary>
        public float BlendFactor { get; set; } = 0.05f;

        /// <summary>
        /// Gets or sets the neighborhood clamp factor for ghosting reduction (0-1).
        /// Higher values = more aggressive clamping (less ghosting, potentially less AA quality).
        /// Default: 0.5 (moderate clamping).
        /// </summary>
        public float ClampFactor { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets whether TAA is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets the current jitter offset for this frame.
        /// </summary>
        public Vector2 JitterOffset
        {
            get { return _jitterOffset; }
        }

        /// <summary>
        /// Initializes a new TAA system.
        /// </summary>
        public TemporalAA(GraphicsDevice graphicsDevice, int width, int height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _width = width;
            _height = height;
            _isFirstFrame = true;

            // Initialize SpriteBatch for fullscreen quad rendering
            _spriteBatch = new SpriteBatch(graphicsDevice);

            CreateBuffers();
        }

        /// <summary>
        /// Begins TAA frame, calculating jitter offset.
        /// </summary>
        public void BeginFrame()
        {
            if (!Enabled)
            {
                return;
            }

            // Calculate Halton sequence jitter
            _jitterOffset = CalculateJitter(_frameNumber);
            _frameNumber++;
        }

        /// <summary>
        /// Applies TAA to the current frame.
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="motionVectors">Motion vector buffer (can be null for first frame or if motion vectors unavailable).</param>
        /// <param name="depthBuffer">Depth buffer (can be null, used for depth-based rejection).</param>
        /// <param name="effect">Optional TAA shader effect. If null, uses CPU fallback implementation.</param>
        /// <returns>TAA-processed frame.</returns>
        /// <remarks>
        /// TAA Algorithm (Temporal Anti-Aliasing):
        /// Based on industry-standard TAA implementation used in modern game engines.
        /// Algorithm steps:
        /// 1. Reproject previous frame using motion vectors to current frame's camera position
        /// 2. Sample neighborhood of current pixel (3x3 or 5-tap cross pattern)
        /// 3. Compute color AABB (Axis-Aligned Bounding Box) from neighborhood samples
        /// 4. Clamp reprojected history color to neighborhood AABB bounds (reduces ghosting)
        /// 5. Blend current frame with clamped history using temporal blend factor
        /// 6. Store result in history buffer for next frame
        /// 
        /// Blending formula:
        /// result = lerp(clampedHistory, current, blendFactor)
        /// where blendFactor typically ranges from 0.05-0.1 (5-10% current, 90-95% history)
        /// 
        /// Ghosting reduction:
        /// - Neighborhood clamping prevents history from contributing colors outside current frame's variance
        /// - Depth-based rejection discards history samples with large depth differences
        /// - Motion vector validation rejects samples with invalid or excessive motion
        /// 
        /// Performance considerations:
        /// - Shader-based implementation (recommended): Full GPU acceleration, minimal CPU overhead
        /// - CPU fallback: Works without shaders but significantly slower, suitable for testing/fallback
        /// 
        /// Based on implementations from:
        /// - Unreal Engine 4/5 TAA implementation
        /// - Unity HDRP Temporal Anti-Aliasing
        /// - CryEngine TAA implementation
        /// - Industry-standard TAA research papers (Karis 2014, Karis 2016)
        /// </remarks>
        public RenderTarget2D ApplyTAA(RenderTarget2D currentFrame, RenderTarget2D motionVectors, RenderTarget2D depthBuffer, Effect effect = null)
        {
            if (currentFrame == null)
            {
                throw new ArgumentNullException(nameof(currentFrame));
            }

            if (!Enabled)
            {
                return currentFrame;
            }

            // Ensure buffers match current frame dimensions
            int width = currentFrame.Width;
            int height = currentFrame.Height;
            if (_outputBuffer == null || _outputBuffer.Width != width || _outputBuffer.Height != height)
            {
                Resize(width, height);
            }

            // Save previous render target
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                // Set render target to output buffer
                _graphicsDevice.SetRenderTarget(_outputBuffer);
                _graphicsDevice.Clear(Color.Black);

                Rectangle fullscreenRect = new Rectangle(0, 0, width, height);

                if (effect != null)
                {
                    // Shader-based TAA implementation (GPU-accelerated)
                    ApplyTAAShader(currentFrame, motionVectors, depthBuffer, fullscreenRect, effect, width, height);
                }
                else
                {
                    // CPU fallback implementation (works without shaders)
                    ApplyTAACpu(currentFrame, motionVectors, depthBuffer, fullscreenRect, width, height);
                }

                // Swap buffers: output becomes history for next frame
                RenderTarget2D temp = _historyBuffer;
                _historyBuffer = _outputBuffer;
                _outputBuffer = temp;

                // Mark that we've processed at least one frame
                _isFirstFrame = false;
            }
            finally
            {
                // Always restore previous render target
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _historyBuffer;
        }

        /// <summary>
        /// Shader-based TAA implementation using GPU acceleration.
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="motionVectors">Motion vector buffer.</param>
        /// <param name="depthBuffer">Depth buffer.</param>
        /// <param name="fullscreenRect">Fullscreen rectangle for rendering.</param>
        /// <param name="effect">TAA shader effect.</param>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        private void ApplyTAAShader(RenderTarget2D currentFrame, RenderTarget2D motionVectors, RenderTarget2D depthBuffer,
            Rectangle fullscreenRect, Effect effect, int width, int height)
        {
            // Set shader parameters for TAA
            // Based on industry-standard TAA shader parameter layout
            EffectParameter currentFrameParam = effect.Parameters["CurrentFrame"];
            if (currentFrameParam != null)
            {
                currentFrameParam.SetValue(currentFrame);
            }

            EffectParameter historyBufferParam = effect.Parameters["HistoryBuffer"];
            if (historyBufferParam != null)
            {
                historyBufferParam.SetValue(_historyBuffer);
            }

            EffectParameter motionVectorsParam = effect.Parameters["MotionVectors"];
            if (motionVectorsParam != null && motionVectors != null)
            {
                motionVectorsParam.SetValue(motionVectors);
            }

            EffectParameter depthBufferParam = effect.Parameters["DepthBuffer"];
            if (depthBufferParam != null && depthBuffer != null)
            {
                depthBufferParam.SetValue(depthBuffer);
            }

            EffectParameter screenSizeParam = effect.Parameters["ScreenSize"];
            if (screenSizeParam != null)
            {
                screenSizeParam.SetValue(new Vector2(width, height));
            }

            EffectParameter screenSizeInvParam = effect.Parameters["ScreenSizeInv"];
            if (screenSizeInvParam != null)
            {
                screenSizeInvParam.SetValue(new Vector2(1.0f / width, 1.0f / height));
            }

            EffectParameter jitterOffsetParam = effect.Parameters["JitterOffset"];
            if (jitterOffsetParam != null)
            {
                jitterOffsetParam.SetValue(_jitterOffset);
            }

            EffectParameter blendFactorParam = effect.Parameters["BlendFactor"];
            if (blendFactorParam != null)
            {
                blendFactorParam.SetValue(BlendFactor);
            }

            EffectParameter clampFactorParam = effect.Parameters["ClampFactor"];
            if (clampFactorParam != null)
            {
                clampFactorParam.SetValue(ClampFactor);
            }

            EffectParameter strengthParam = effect.Parameters["Strength"];
            if (strengthParam != null)
            {
                strengthParam.SetValue(Strength);
            }

            EffectParameter isFirstFrameParam = effect.Parameters["IsFirstFrame"];
            if (isFirstFrameParam != null)
            {
                isFirstFrameParam.SetValue(_isFirstFrame ? 1.0f : 0.0f);
            }

            // Render fullscreen quad with TAA shader
            _spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Opaque,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect);
            _spriteBatch.Draw(currentFrame, fullscreenRect, Color.White);
            _spriteBatch.End();
        }

        /// <summary>
        /// CPU fallback TAA implementation.
        /// This is a simplified approximation that works without shaders.
        /// For production use, a shader-based implementation is strongly recommended.
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="motionVectors">Motion vector buffer (optional).</param>
        /// <param name="depthBuffer">Depth buffer (optional).</param>
        /// <param name="fullscreenRect">Fullscreen rectangle for rendering.</param>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        private void ApplyTAACpu(RenderTarget2D currentFrame, RenderTarget2D motionVectors, RenderTarget2D depthBuffer,
            Rectangle fullscreenRect, int width, int height)
        {
            // CPU fallback: Simplified temporal blending
            // This implementation provides basic TAA functionality without requiring shaders
            // For a full implementation, we would need to:
            // 1. Read pixel data from currentFrame and historyBuffer
            // 2. Reproject history using motion vectors
            // 3. Sample neighborhood and compute AABB
            // 4. Clamp history to AABB bounds
            // 5. Blend and write result
            // 
            // However, reading/writing pixel data on CPU is very slow, so we use a simplified approach:
            // - First frame: Just copy current frame to history
            // - Subsequent frames: Blend current frame with history using SpriteBatch

            if (_isFirstFrame)
            {
                // First frame: Copy current frame to history buffer (no temporal data yet)
                _graphicsDevice.SetRenderTarget(_historyBuffer);
                _graphicsDevice.Clear(Color.Black);

                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                _spriteBatch.Draw(currentFrame, fullscreenRect, Color.White);
                _spriteBatch.End();

                // Copy to output
                _graphicsDevice.SetRenderTarget(_outputBuffer);
                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                _spriteBatch.Draw(currentFrame, fullscreenRect, Color.White);
                _spriteBatch.End();
            }
            else
            {
                // Subsequent frames: Temporal blending
                // Draw current frame first
                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                _spriteBatch.Draw(currentFrame, fullscreenRect, Color.White);
                _spriteBatch.End();

                // Blend history buffer on top with temporal blend factor
                // BlendFactor controls how much history vs current frame
                // Lower BlendFactor = more history (better AA, more ghosting risk)
                // Higher BlendFactor = more current frame (less AA, less ghosting)
                float historyWeight = 1.0f - BlendFactor;
                Color blendColor = new Color(historyWeight, historyWeight, historyWeight, historyWeight);

                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.LinearClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone);
                _spriteBatch.Draw(_historyBuffer, fullscreenRect, blendColor);
                _spriteBatch.End();

                // Note: Full CPU implementation would require:
                // - Reading pixel data from textures (GetData/SetData - very slow)
                // - Reprojecting history using motion vectors
                // - Computing neighborhood AABB and clamping
                // - This is too slow for real-time rendering, hence the simplified approach
                // For production use, shader-based implementation is essential
            }
        }

        /// <summary>
        /// Calculates Halton sequence jitter for this frame.
        /// </summary>
        private Vector2 CalculateJitter(int frame)
        {
            // Halton(2, 3) sequence for jitter
            float x = Halton(frame, 2);
            float y = Halton(frame, 3);

            // Scale to pixel offset
            return new Vector2(
                (x - 0.5f) / _width,
                (y - 0.5f) / _height
            );
        }

        /// <summary>
        /// Calculates Halton sequence value.
        /// </summary>
        private float Halton(int index, int baseNum)
        {
            float result = 0.0f;
            float f = 1.0f / baseNum;
            int i = index;

            while (i > 0)
            {
                result += f * (i % baseNum);
                i = i / baseNum;
                f /= baseNum;
            }

            return result;
        }

        /// <summary>
        /// Resizes TAA buffers.
        /// </summary>
        public void Resize(int width, int height)
        {
            _width = width;
            _height = height;
            DisposeBuffers();
            CreateBuffers();
        }

        private void CreateBuffers()
        {
            // History buffer stores previous frame's TAA result
            // Used for temporal accumulation and reprojection
            _historyBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.HalfVector4, // HDR format for high-quality color storage
                DepthFormat.None
            );

            // Current frame buffer (temporary, used for buffer swapping)
            _currentFrame = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None
            );

            // Output buffer for TAA-processed result
            _outputBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.HalfVector4,
                DepthFormat.None
            );
        }

        private void DisposeBuffers()
        {
            _historyBuffer?.Dispose();
            _currentFrame?.Dispose();
            _outputBuffer?.Dispose();
        }

        public void Dispose()
        {
            _spriteBatch?.Dispose();
            _spriteBatch = null;
            DisposeBuffers();
        }
    }
}

