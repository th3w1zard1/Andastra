using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Temporal reprojection for upsampling and anti-aliasing.
    ///
    /// Uses temporal history from previous frames to improve image quality
    /// through upsampling, denoising, and anti-aliasing.
    ///
    /// Features:
    /// - Jittered sampling for TAA
    /// - Motion vector reprojection
    /// - History buffer management
    /// - Clipping and rejection
    /// - Upsampling support
    /// </summary>
    public class TemporalReprojection : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _historyBuffer;
        private RenderTarget2D _outputBuffer;
        private RenderTarget2D _velocityBuffer;
        private SpriteBatch _spriteBatch;
        private Matrix _previousViewProjection;
        private Matrix _currentViewProjection;
        private int _sampleIndex;
        private bool _enabled;
        private bool _isFirstFrame;

        /// <summary>
        /// Gets or sets whether temporal reprojection is enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets the current sample index (for jittering).
        /// </summary>
        public int SampleIndex
        {
            get { return _sampleIndex; }
        }

        /// <summary>
        /// Gets or sets the temporal blend factor (0-1).
        /// Lower values = more history (better quality, more ghosting risk).
        /// Higher values = more current frame (less ghosting, potentially less quality).
        /// Default: 0.1 (90% history, 10% current frame).
        /// </summary>
        public float BlendFactor { get; set; } = 0.1f;

        /// <summary>
        /// Gets or sets the neighborhood clamp factor for ghosting reduction (0-1).
        /// Higher values = more aggressive clamping (less ghosting, potentially less quality).
        /// Default: 0.5 (moderate clamping).
        /// </summary>
        public float ClampFactor { get; set; } = 0.5f;

        /// <summary>
        /// Initializes a new temporal reprojection system.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public TemporalReprojection(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _sampleIndex = 0;
            _enabled = true;
            _isFirstFrame = true;
            _spriteBatch = new SpriteBatch(graphicsDevice);
        }

        /// <summary>
        /// Gets the jitter offset for the current frame.
        /// </summary>
        /// <param name="width">Render target width in pixels.</param>
        /// <param name="height">Render target height in pixels.</param>
        /// <returns>Jitter offset in normalized device coordinates.</returns>
        public Vector2 GetJitterOffset(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return Vector2.Zero;
            }

            // Halton sequence for jittering (low-discrepancy sequence for better TAA)
            float[] haltonX = { 0.0f, 0.5f, 0.25f, 0.75f, 0.125f, 0.625f, 0.375f, 0.875f };
            float[] haltonY = { 0.0f, 0.333333f, 0.666667f, 0.111111f, 0.444444f, 0.777778f, 0.222222f, 0.555556f };

            int index = _sampleIndex % haltonX.Length;
            return new Vector2(
                (haltonX[index] - 0.5f) / width,
                (haltonY[index] - 0.5f) / height
            );
        }

        /// <summary>
        /// Updates reprojection with current frame data.
        /// </summary>
        /// <param name="viewProjection">Current view-projection matrix.</param>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="velocityBuffer">Velocity buffer for motion vectors.</param>
        public void Update(Matrix viewProjection, RenderTarget2D currentFrame, RenderTarget2D velocityBuffer)
        {
            if (!_enabled)
            {
                return;
            }

            // Store previous frame data for reprojection (will be used in next Reproject call)
            _currentViewProjection = viewProjection;
            _velocityBuffer = velocityBuffer;

            // Create or resize history and output buffers if needed
            if (currentFrame != null)
            {
                int width = currentFrame.Width;
                int height = currentFrame.Height;
                SurfaceFormat format = currentFrame.Format;

                if (_historyBuffer == null || _historyBuffer.Width != width || _historyBuffer.Height != height)
                {
                    _historyBuffer?.Dispose();
                    _outputBuffer?.Dispose();

                    _historyBuffer = new RenderTarget2D(
                        _graphicsDevice,
                        width,
                        height,
                        false,
                        format,
                        DepthFormat.None
                    );

                    _outputBuffer = new RenderTarget2D(
                        _graphicsDevice,
                        width,
                        height,
                        false,
                        format,
                        DepthFormat.None
                    );
                }
            }

            // Advance to next sample in jitter pattern
            _sampleIndex = (_sampleIndex + 1) % 8; // 8 sample pattern
        }

        /// <summary>
        /// Applies temporal reprojection to combine current frame with history.
        ///
        /// Temporal Reprojection Algorithm:
        /// Based on industry-standard temporal reprojection used in modern game engines for
        /// upsampling, anti-aliasing, and denoising. Algorithm steps:
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
        /// - Depth-based rejection discards history samples with large depth differences (if depth buffer available)
        /// - Motion vector validation rejects samples with invalid or excessive motion
        ///
        /// Based on implementations from:
        /// - Unreal Engine 4/5 temporal upsampling and TAA
        /// - Unity HDRP Temporal Anti-Aliasing and upsampling
        /// - Industry-standard temporal reprojection research (Karis 2014, Karis 2016)
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="effect">Effect/shader for temporal reprojection. Can be null for CPU fallback.</param>
        /// <returns>Reprojected frame, or original if disabled.</returns>
        public RenderTarget2D Reproject(RenderTarget2D currentFrame, Effect effect)
        {
            if (!_enabled || currentFrame == null || _historyBuffer == null || _outputBuffer == null)
            {
                return currentFrame;
            }

            // Ensure buffers match current frame dimensions
            int width = currentFrame.Width;
            int height = currentFrame.Height;
            if (_outputBuffer.Width != width || _outputBuffer.Height != height)
            {
                // Buffers will be resized in Update() call, so just return current frame
                return currentFrame;
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

                if (effect != null && _velocityBuffer != null)
                {
                    // Shader-based temporal reprojection implementation (GPU-accelerated)
                    ApplyReprojectionShader(currentFrame, fullscreenRect, effect, width, height);
                }
                else
                {
                    // CPU fallback implementation (works without shaders)
                    ApplyReprojectionCpu(currentFrame, fullscreenRect, width, height);
                }

                // Swap buffers: output becomes history for next frame
                RenderTarget2D temp = _historyBuffer;
                _historyBuffer = _outputBuffer;
                _outputBuffer = temp;

                // Update previous view projection for next frame
                _previousViewProjection = _currentViewProjection;

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
        /// Shader-based temporal reprojection implementation using GPU acceleration.
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="fullscreenRect">Fullscreen rectangle for rendering.</param>
        /// <param name="effect">Temporal reprojection shader effect.</param>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        private void ApplyReprojectionShader(RenderTarget2D currentFrame, Rectangle fullscreenRect,
            Effect effect, int width, int height)
        {
            // Set shader parameters for temporal reprojection
            // Based on industry-standard temporal reprojection shader parameter layout
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

            EffectParameter velocityBufferParam = effect.Parameters["VelocityBuffer"];
            if (velocityBufferParam != null && _velocityBuffer != null)
            {
                velocityBufferParam.SetValue(_velocityBuffer);
            }

            EffectParameter previousViewProjectionParam = effect.Parameters["PreviousViewProjection"];
            if (previousViewProjectionParam != null)
            {
                previousViewProjectionParam.SetValue(_previousViewProjection);
            }

            EffectParameter currentViewProjectionParam = effect.Parameters["CurrentViewProjection"];
            if (currentViewProjectionParam != null)
            {
                currentViewProjectionParam.SetValue(_currentViewProjection);
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
                Vector2 jitterOffset = GetJitterOffset(width, height);
                jitterOffsetParam.SetValue(jitterOffset);
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

            EffectParameter isFirstFrameParam = effect.Parameters["IsFirstFrame"];
            if (isFirstFrameParam != null)
            {
                isFirstFrameParam.SetValue(_isFirstFrame ? 1.0f : 0.0f);
            }

            // Render fullscreen quad with temporal reprojection shader
            // Shader should perform:
            // 1. Sample velocity buffer to get motion vector for current pixel
            // 2. Reproject previous pixel position: prevUV = currentUV - motionVector
            // 3. Sample history buffer at reprojected UV coordinates
            // 4. Sample 3x3 or 5-tap cross neighborhood around current pixel
            // 5. Compute color AABB from neighborhood samples
            // 6. Clamp history color to AABB bounds: clampedHistory = clamp(history, aabbMin, aabbMax)
            // 7. Blend: result = lerp(clampedHistory, current, blendFactor)
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
        /// CPU fallback temporal reprojection implementation.
        /// This is a simplified approximation that works without shaders.
        /// For production use, a shader-based implementation is strongly recommended.
        /// </summary>
        /// <param name="currentFrame">Current frame render target.</param>
        /// <param name="fullscreenRect">Fullscreen rectangle for rendering.</param>
        /// <param name="width">Frame width.</param>
        /// <param name="height">Frame height.</param>
        private void ApplyReprojectionCpu(RenderTarget2D currentFrame, Rectangle fullscreenRect,
            int width, int height)
        {
            // CPU fallback: Simplified temporal blending
            // Full CPU implementation would require:
            // 1. Reading pixel data from currentFrame and historyBuffer (GetData - very slow)
            // 2. Reprojecting history using motion vectors (requires velocity buffer data)
            // 3. Sampling neighborhood and computing AABB (requires pixel reads)
            // 4. Clamping history to AABB bounds
            // 5. Blending and writing result (SetData - very slow)
            //
            // However, reading/writing pixel data on CPU is very slow for real-time rendering,
            // so we use a simplified approach with SpriteBatch blending:

            if (_isFirstFrame)
            {
                // First frame: Copy current frame to history buffer (no temporal data yet)
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
                // Lower BlendFactor = more history (better quality, more ghosting risk)
                // Higher BlendFactor = more current frame (less quality, less ghosting)
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

                // Note: Full CPU implementation with proper reprojection, neighborhood clamping,
                // and motion vector sampling would require GetData/SetData operations which are
                // too slow for real-time rendering. For production use, shader-based implementation
                // is essential for acceptable performance.
            }
        }

        /// <summary>
        /// Disposes of all resources used by this temporal reprojection system.
        /// </summary>
        public void Dispose()
        {
            _spriteBatch?.Dispose();
            _spriteBatch = null;
            _historyBuffer?.Dispose();
            _historyBuffer = null;
            _outputBuffer?.Dispose();
            _outputBuffer = null;
            _velocityBuffer = null; // Not owned, just referenced
        }
    }
}

