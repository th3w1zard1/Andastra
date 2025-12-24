using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Rendering
{
    /// <summary>
    /// Contact hardening shadows for realistic shadow edges.
    /// 
    /// Provides additional detail in shadow penumbra regions, creating
    /// realistic soft shadow transitions that harden near contact points.
    /// 
    /// Features:
    /// - Screen-space contact shadow detection
    /// - Variable shadow softness
    /// - Performance optimized
    /// - Integration with cascaded shadow maps
    /// </summary>
    public class ContactShadows : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private RenderTarget2D _contactShadowTarget;
        private float _shadowDistance;
        private float _shadowThickness;
        private int _sampleCount;
        private bool _enabled;
        private int _lastWidth;
        private int _lastHeight;

        // Full-screen quad vertices for rendering
        private static VertexPositionTexture[] _fullScreenQuadVertices;
        private static short[] _fullScreenQuadIndices;
        // Shared black texture for when normal buffer is not provided
        private static Texture2D _blackTexture;

        // Static constructor to initialize full-screen quad geometry
        static ContactShadows()
        {
            // Initialize full-screen quad vertices (two triangles)
            // Positions in NDC space (-1 to 1), texture coordinates (0 to 1)
            _fullScreenQuadVertices = new VertexPositionTexture[4];
            _fullScreenQuadVertices[0] = new VertexPositionTexture(new Vector3(-1.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f)); // Bottom-left
            _fullScreenQuadVertices[1] = new VertexPositionTexture(new Vector3(1.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f));  // Bottom-right
            _fullScreenQuadVertices[2] = new VertexPositionTexture(new Vector3(-1.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f));  // Top-left
            _fullScreenQuadVertices[3] = new VertexPositionTexture(new Vector3(1.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f));   // Top-right

            // Initialize indices for two triangles forming the quad
            _fullScreenQuadIndices = new short[6];
            _fullScreenQuadIndices[0] = 0; // Bottom-left
            _fullScreenQuadIndices[1] = 2; // Top-left
            _fullScreenQuadIndices[2] = 1; // Bottom-right
            _fullScreenQuadIndices[3] = 1; // Bottom-right
            _fullScreenQuadIndices[4] = 2; // Top-left
            _fullScreenQuadIndices[5] = 3; // Top-right
        }

        /// <summary>
        /// Initializes the shared black texture for fallback normal buffer.
        /// Called once per graphics device to avoid recreating the texture every frame.
        /// </summary>
        private void InitializeBlackTexture()
        {
            if (_blackTexture == null)
            {
                _blackTexture = new Texture2D(_graphicsDevice, 1, 1, false, SurfaceFormat.Color);
                _blackTexture.SetData(new Color[] { Color.Black });
            }
        }

        /// <summary>
        /// Gets or sets whether contact shadows are enabled.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Gets or sets the maximum shadow distance.
        /// </summary>
        public float ShadowDistance
        {
            get { return _shadowDistance; }
            set { _shadowDistance = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the shadow thickness (penumbra size).
        /// </summary>
        public float ShadowThickness
        {
            get { return _shadowThickness; }
            set { _shadowThickness = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the number of shadow samples.
        /// </summary>
        public int SampleCount
        {
            get { return _sampleCount; }
            set { _sampleCount = Math.Max(1, Math.Min(32, value)); }
        }

        /// <summary>
        /// Initializes a new contact shadows system.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering operations.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        public ContactShadows(GraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _shadowDistance = 10.0f;
            _shadowThickness = 0.5f;
            _sampleCount = 8;
            _enabled = true;
            _lastWidth = 0;
            _lastHeight = 0;

            // Initialize shared black texture for fallback normal buffer
            InitializeBlackTexture();
        }

        /// <summary>
        /// Renders contact shadows using screen-space depth.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer for depth testing. Must not be null.</param>
        /// <param name="normalBuffer">Normal buffer for surface orientation. Can be null.</param>
        /// <param name="effect">Effect/shader for contact shadow rendering. Must not be null.</param>
        /// <param name="viewMatrix">View matrix for camera transformation. Used for screen-space calculations.</param>
        /// <param name="projectionMatrix">Projection matrix for camera perspective. Used for screen-space calculations.</param>
        /// <returns>Render target containing contact shadows, or null if disabled or invalid input.</returns>
        /// <exception cref="ArgumentNullException">Thrown if depthBuffer or effect is null.</exception>
        public RenderTarget2D Render(RenderTarget2D depthBuffer, RenderTarget2D normalBuffer, Effect effect, Matrix viewMatrix, Matrix projectionMatrix)
        {
            if (!_enabled)
            {
                return null;
            }
            if (depthBuffer == null)
            {
                throw new ArgumentNullException(nameof(depthBuffer));
            }
            if (effect == null)
            {
                throw new ArgumentNullException(nameof(effect));
            }

            // Create or resize render target if needed
            int width = depthBuffer.Width;
            int height = depthBuffer.Height;
            if (_contactShadowTarget == null || _lastWidth != width || _lastHeight != height)
            {
                _contactShadowTarget?.Dispose();
                _contactShadowTarget = new RenderTarget2D(
                    _graphicsDevice,
                    width,
                    height,
                    false,
                    SurfaceFormat.Single,
                    DepthFormat.None
                );
                _lastWidth = width;
                _lastHeight = height;
            }

            // Set render target
            RenderTarget2D previousTarget = _graphicsDevice.GetRenderTargets().Length > 0
                ? _graphicsDevice.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_contactShadowTarget);
                _graphicsDevice.Clear(Color.White); // White = no shadow

                // Calculate inverse view-projection matrix for depth reconstruction
                Matrix viewProjection = viewMatrix * projectionMatrix;
                Matrix inverseViewProjection = Matrix.Invert(viewProjection);

                // Set effect parameters for contact shadow rendering
                // DepthTexture: The depth buffer for ray-marching
                if (effect.Parameters["DepthTexture"] != null)
                {
                    effect.Parameters["DepthTexture"].SetValue(depthBuffer);
                }

                // NormalTexture: Surface normals for better shadow quality (optional)
                Texture2D normalTex = null;
                if (normalBuffer != null)
                {
                    normalTex = normalBuffer;
                }
                else
                {
                    // Use shared black texture if normal buffer is not provided
                    // This allows the shader to work without normals (though quality may be reduced)
                    normalTex = _blackTexture;
                }
                if (effect.Parameters["NormalTexture"] != null)
                {
                    effect.Parameters["NormalTexture"].SetValue(normalTex);
                }

                // ShadowDistance: Maximum distance to search for contact shadows
                if (effect.Parameters["ShadowDistance"] != null)
                {
                    effect.Parameters["ShadowDistance"].SetValue(_shadowDistance);
                }

                // ShadowThickness: Penumbra size (how soft the shadows are)
                if (effect.Parameters["ShadowThickness"] != null)
                {
                    effect.Parameters["ShadowThickness"].SetValue(_shadowThickness);
                }

                // SampleCount: Number of ray-march samples (more = better quality, slower)
                if (effect.Parameters["SampleCount"] != null)
                {
                    effect.Parameters["SampleCount"].SetValue((float)_sampleCount);
                }

                // ScreenSize: Resolution of the render target (used for pixel-perfect sampling)
                if (effect.Parameters["ScreenSize"] != null)
                {
                    effect.Parameters["ScreenSize"].SetValue(new Vector2(width, height));
                }

                // InverseViewProjection: For reconstructing world position from depth
                if (effect.Parameters["InverseViewProjection"] != null)
                {
                    effect.Parameters["InverseViewProjection"].SetValue(inverseViewProjection);
                }

                // ViewMatrix: For screen-space calculations
                if (effect.Parameters["ViewMatrix"] != null)
                {
                    effect.Parameters["ViewMatrix"].SetValue(viewMatrix);
                }

                // ProjectionMatrix: For screen-space calculations
                if (effect.Parameters["ProjectionMatrix"] != null)
                {
                    effect.Parameters["ProjectionMatrix"].SetValue(projectionMatrix);
                }

                // Render full-screen quad with contact shadow shader
                // The shader performs screen-space ray-marching to detect contact shadows
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    // Disable depth testing and writing for full-screen post-processing
                    DepthStencilState previousDepthState = _graphicsDevice.DepthStencilState;
                    _graphicsDevice.DepthStencilState = DepthStencilState.None;

                    // Disable culling for full-screen quad
                    RasterizerState previousRasterizerState = _graphicsDevice.RasterizerState;
                    _graphicsDevice.RasterizerState = RasterizerState.CullNone;

                    // Draw full-screen quad using DrawUserIndexedPrimitives
                    _graphicsDevice.DrawUserIndexedPrimitives<VertexPositionTexture>(
                        PrimitiveType.TriangleList,
                        _fullScreenQuadVertices,
                        0,
                        4,
                        _fullScreenQuadIndices,
                        0,
                        2
                    );

                    // Restore previous render states
                    _graphicsDevice.DepthStencilState = previousDepthState;
                    _graphicsDevice.RasterizerState = previousRasterizerState;
                }
            }
            finally
            {
                // Always restore previous render target
                _graphicsDevice.SetRenderTarget(previousTarget);
            }

            return _contactShadowTarget;
        }

        /// <summary>
        /// Renders contact shadows using screen-space depth (overload without view/projection matrices).
        /// Uses identity matrices as fallback - for full functionality, use the overload with matrices.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer for depth testing. Must not be null.</param>
        /// <param name="normalBuffer">Normal buffer for surface orientation. Can be null.</param>
        /// <param name="effect">Effect/shader for contact shadow rendering. Must not be null.</param>
        /// <returns>Render target containing contact shadows, or null if disabled or invalid input.</returns>
        /// <exception cref="ArgumentNullException">Thrown if depthBuffer or effect is null.</exception>
        public RenderTarget2D Render(RenderTarget2D depthBuffer, RenderTarget2D normalBuffer, Effect effect)
        {
            // Use identity matrices as fallback (less accurate but allows rendering)
            return Render(depthBuffer, normalBuffer, effect, Matrix.Identity, Matrix.Identity);
        }

        /// <summary>
        /// Disposes of all resources used by this contact shadows system.
        /// </summary>
        public void Dispose()
        {
            _contactShadowTarget?.Dispose();
            _contactShadowTarget = null;

            // Note: _blackTexture is static and shared, so we don't dispose it here
            // It will be disposed when the graphics device is disposed
        }
    }
}

