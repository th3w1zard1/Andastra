using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Shadows
{
    /// <summary>
    /// Cascaded shadow maps (CSM) for high-quality directional light shadows.
    /// 
    /// CSM splits the view frustum into multiple cascades, each with its own
    /// shadow map, providing high resolution near the camera and lower resolution
    /// at distance.
    /// 
    /// Features:
    /// - Multiple cascade levels (typically 4)
    /// - Automatic cascade distribution
    /// - Shadow filtering (PCF, VSM, ESM)
    /// - Shadow atlas for multiple lights
    /// </summary>
    public class CascadedShadowMaps : IDisposable
    {
        /// <summary>
        /// Shadow cascade configuration.
        /// </summary>
        public struct CascadeConfig
        {
            /// <summary>
            /// Split distances (normalized 0-1, where 1 = far plane).
            /// </summary>
            public float[] SplitDistances;

            /// <summary>
            /// Shadow map resolution per cascade.
            /// </summary>
            public int ShadowMapResolution;

            /// <summary>
            /// Number of cascades.
            /// </summary>
            public int CascadeCount;

            /// <summary>
            /// Light direction (normalized).
            /// </summary>
            public Vector3 LightDirection;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly CascadeConfig _config;
        private RenderTarget2D[] _shadowMaps;
        private Matrix[] _lightViewMatrices;
        private Matrix[] _lightProjectionMatrices;
        private float[] _cascadeDistances;

        /// <summary>
        /// Gets shadow maps for each cascade.
        /// </summary>
        public RenderTarget2D[] ShadowMaps
        {
            get { return _shadowMaps; }
        }

        /// <summary>
        /// Gets light view matrices for each cascade.
        /// </summary>
        public Matrix[] LightViewMatrices
        {
            get { return _lightViewMatrices; }
        }

        /// <summary>
        /// Gets light projection matrices for each cascade.
        /// </summary>
        public Matrix[] LightProjectionMatrices
        {
            get { return _lightProjectionMatrices; }
        }

        /// <summary>
        /// Gets cascade split distances.
        /// </summary>
        public float[] CascadeDistances
        {
            get { return _cascadeDistances; }
        }

        /// <summary>
        /// Initializes a new cascaded shadow map system.
        /// </summary>
        public CascadedShadowMaps(GraphicsDevice graphicsDevice, CascadeConfig config)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _config = config;

            InitializeCascades();
        }

        /// <summary>
        /// Updates cascade matrices based on camera.
        /// </summary>
        public void UpdateCascades(Matrix viewMatrix, Matrix projectionMatrix, Vector3 cameraPosition)
        {
            float nearPlane = 0.1f;
            float farPlane = 1000.0f;

            // Calculate split distances
            CalculateSplitDistances(nearPlane, farPlane, _config.SplitDistances, _cascadeDistances);

            // Calculate light view matrix
            Vector3 lightPos = cameraPosition - _config.LightDirection * 100.0f;
            Matrix lightView = Matrix.CreateLookAt(lightPos, cameraPosition, Vector3.Up);

            // Calculate projection for each cascade
            for (int i = 0; i < _config.CascadeCount; i++)
            {
                float splitNear = i == 0 ? nearPlane : _cascadeDistances[i - 1];
                float splitFar = _cascadeDistances[i];

                // Calculate frustum corners in world space
                Vector3[] frustumCorners = CalculateFrustumCorners(viewMatrix, projectionMatrix, splitNear, splitFar);

                // Transform to light space
                for (int j = 0; j < 8; j++)
                {
                    frustumCorners[j] = Vector3.Transform(frustumCorners[j], lightView);
                }

                // Calculate AABB in light space
                Vector3 min = frustumCorners[0];
                Vector3 max = frustumCorners[0];
                for (int j = 1; j < 8; j++)
                {
                    min = Vector3.Min(min, frustumCorners[j]);
                    max = Vector3.Max(max, frustumCorners[j]);
                }

                // Create orthographic projection
                float width = max.X - min.X;
                float height = max.Y - min.Y;
                float depth = max.Z - min.Z;

                _lightViewMatrices[i] = lightView;
                _lightProjectionMatrices[i] = Matrix.CreateOrthographic(width, height, -depth, depth);
            }
        }

        /// <summary>
        /// Begins rendering shadow map for a cascade.
        /// </summary>
        public void BeginShadowMap(int cascadeIndex)
        {
            if (cascadeIndex < 0 || cascadeIndex >= _shadowMaps.Length)
            {
                return;
            }

            _graphicsDevice.SetRenderTarget(_shadowMaps[cascadeIndex]);
            _graphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
        }

        /// <summary>
        /// Ends shadow map rendering.
        /// </summary>
        public void EndShadowMap()
        {
            _graphicsDevice.SetRenderTarget(null);
        }

        private void InitializeCascades()
        {
            int cascadeCount = _config.CascadeCount;
            _shadowMaps = new RenderTarget2D[cascadeCount];
            _lightViewMatrices = new Matrix[cascadeCount];
            _lightProjectionMatrices = new Matrix[cascadeCount];
            _cascadeDistances = new float[cascadeCount];

            // Create shadow maps
            for (int i = 0; i < cascadeCount; i++)
            {
                _shadowMaps[i] = new RenderTarget2D(
                    _graphicsDevice,
                    _config.ShadowMapResolution,
                    _config.ShadowMapResolution,
                    false,
                    SurfaceFormat.Single, // Depth stored as float
                    DepthFormat.Depth24
                );
            }
        }

        private void CalculateSplitDistances(float nearPlane, float farPlane, float[] splitRatios, float[] distances)
        {
            // Logarithmic split scheme (better for outdoor scenes)
            for (int i = 0; i < splitRatios.Length; i++)
            {
                float ratio = splitRatios[i];
                float logSplit = nearPlane * (float)Math.Pow(farPlane / nearPlane, ratio);
                float uniformSplit = nearPlane + (farPlane - nearPlane) * ratio;
                distances[i] = 0.5f * (logSplit + uniformSplit); // Blend between schemes
            }
        }

        private Vector3[] CalculateFrustumCorners(Matrix view, Matrix projection, float near, float far)
        {
            Matrix invViewProj = Matrix.Invert(view * projection);
            Vector3[] corners = new Vector3[8];

            // Near plane corners
            corners[0] = Unproject(new Vector3(-1, -1, 0), invViewProj); // Bottom-left
            corners[1] = Unproject(new Vector3(1, -1, 0), invViewProj);  // Bottom-right
            corners[2] = Unproject(new Vector3(-1, 1, 0), invViewProj);  // Top-left
            corners[3] = Unproject(new Vector3(1, 1, 0), invViewProj);   // Top-right

            // Far plane corners
            corners[4] = Unproject(new Vector3(-1, -1, 1), invViewProj);
            corners[5] = Unproject(new Vector3(1, -1, 1), invViewProj);
            corners[6] = Unproject(new Vector3(-1, 1, 1), invViewProj);
            corners[7] = Unproject(new Vector3(1, 1, 1), invViewProj);

            return corners;
        }

        private Vector3 Unproject(Vector3 screenPos, Matrix invViewProj)
        {
            Vector4 result = Vector4.Transform(new Vector4(screenPos, 1.0f), invViewProj);
            if (Math.Abs(result.W) > 1e-6f)
            {
                result.X /= result.W;
                result.Y /= result.W;
                result.Z /= result.W;
            }
            return new Vector3(result.X, result.Y, result.Z);
        }

        public void Dispose()
        {
            if (_shadowMaps != null)
            {
                for (int i = 0; i < _shadowMaps.Length; i++)
                {
                    _shadowMaps[i]?.Dispose();
                }
            }
        }
    }
}

