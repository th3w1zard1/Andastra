using System;
using System.Collections.Generic;
using Andastra.Runtime.MonoGame.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Lighting
{
    /// <summary>
    /// Clustered light culling system for efficient handling of many lights.
    /// 
    /// Clustered shading divides view frustum into 3D clusters and assigns
    /// lights to clusters, enabling efficient rendering of hundreds/thousands
    /// of lights with minimal performance impact.
    /// 
    /// Based on modern AAA game clustered forward/deferred shading techniques.
    /// </summary>
    public class ClusteredLightCulling : IDisposable
    {
        /// <summary>
        /// Light data structure for GPU.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct LightData
        {
            /// <summary>
            /// Light position (XYZ) and type (W: 0=point, 1=spot, 2=directional).
            /// </summary>
            public Vector4 PositionType;

            /// <summary>
            /// Light direction (XYZ) and range (W).
            /// </summary>
            public Vector4 DirectionRange;

            /// <summary>
            /// Light color (RGB) and intensity (A).
            /// </summary>
            public Vector4 ColorIntensity;

            /// <summary>
            /// Spot light parameters (inner angle, outer angle, falloff, unused).
            /// </summary>
            public Vector4 SpotParams;
        }

        /// <summary>
        /// Light grid entry structure (offset + count per cluster).
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct LightGridEntry
        {
            /// <summary>
            /// Offset into the light index buffer where this cluster's lights start.
            /// </summary>
            public uint Offset;

            /// <summary>
            /// Number of lights in this cluster.
            /// </summary>
            public uint Count;
        }

        /// <summary>
        /// Cluster configuration.
        /// </summary>
        public struct ClusterConfig
        {
            /// <summary>
            /// Number of clusters in X, Y, Z dimensions.
            /// </summary>
            public Vector3 ClusterCounts;

            /// <summary>
            /// Near and far plane distances.
            /// </summary>
            public Vector2 DepthRange;

            /// <summary>
            /// Viewport dimensions.
            /// </summary>
            public Vector2 ViewportSize;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly ClusterConfig _config;
        private readonly List<LightData> _lights;
        private MonoGameGraphicsBuffer _lightBuffer;
        private MonoGameGraphicsBuffer _lightIndexBuffer;
        private MonoGameGraphicsBuffer _lightGridBuffer;
        private int _maxLightsPerCluster;
        private int _maxLights;
        private int _clusterCount;

        /// <summary>
        /// Gets or sets the maximum lights per cluster.
        /// </summary>
        public int MaxLightsPerCluster
        {
            get { return _maxLightsPerCluster; }
            set
            {
                _maxLightsPerCluster = Math.Max(1, value);
                RecreateBuffers();
            }
        }

        /// <summary>
        /// Gets the current light count.
        /// </summary>
        public int LightCount
        {
            get { return _lights.Count; }
        }

        /// <summary>
        /// Gets the maximum number of lights supported.
        /// </summary>
        public int MaxLights
        {
            get { return _maxLights; }
        }

        /// <summary>
        /// Gets the number of clusters.
        /// </summary>
        public int ClusterCount
        {
            get { return _clusterCount; }
        }

        /// <summary>
        /// Initializes a new clustered light culling system.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device.</param>
        /// <param name="config">Cluster configuration.</param>
        /// <param name="maxLightsPerCluster">Maximum lights per cluster (default: 255).</param>
        /// <param name="maxLights">Maximum total lights supported (default: 1024).</param>
        public ClusteredLightCulling(GraphicsDevice graphicsDevice, ClusterConfig config, int maxLightsPerCluster = 255, int maxLights = 1024)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _config = config;
            _maxLightsPerCluster = Math.Max(1, maxLightsPerCluster);
            _maxLights = Math.Max(1, maxLights);
            _lights = new List<LightData>();
            _clusterCount = (int)(_config.ClusterCounts.X * _config.ClusterCounts.Y * _config.ClusterCounts.Z);

            RecreateBuffers();
        }

        /// <summary>
        /// Adds a point light.
        /// </summary>
        public void AddPointLight(Vector3 position, Vector3 color, float intensity, float range)
        {
            _lights.Add(new LightData
            {
                PositionType = new Vector4(position, 0.0f), // Type 0 = point
                DirectionRange = new Vector4(0, 0, 0, range),
                ColorIntensity = new Vector4(color, intensity),
                SpotParams = Vector4.Zero
            });
        }

        /// <summary>
        /// Adds a spot light.
        /// </summary>
        public void AddSpotLight(Vector3 position, Vector3 direction, Vector3 color, float intensity, float range, float innerAngle, float outerAngle)
        {
            _lights.Add(new LightData
            {
                PositionType = new Vector4(position, 1.0f), // Type 1 = spot
                DirectionRange = new Vector4(direction, range),
                ColorIntensity = new Vector4(color, intensity),
                SpotParams = new Vector4(innerAngle, outerAngle, 0.0f, 0.0f)
            });
        }

        /// <summary>
        /// Adds a directional light.
        /// </summary>
        public void AddDirectionalLight(Vector3 direction, Vector3 color, float intensity)
        {
            _lights.Add(new LightData
            {
                PositionType = new Vector4(0, 0, 0, 2.0f), // Type 2 = directional
                DirectionRange = new Vector4(direction, 0.0f),
                ColorIntensity = new Vector4(color, intensity),
                SpotParams = Vector4.Zero
            });
        }

        /// <summary>
        /// Builds light clusters using compute shader.
        /// </summary>
        /// <param name="viewMatrix">View matrix.</param>
        /// <param name="projectionMatrix">Projection matrix.</param>
        public void BuildClusters(Matrix viewMatrix, Matrix projectionMatrix)
        {
            if (_lights.Count == 0)
            {
                return;
            }

            // Ensure buffers are large enough
            if (_lights.Count > _maxLights)
            {
                _maxLights = _lights.Count;
                RecreateBuffers();
            }

            // Upload lights to GPU
            LightData[] lightArray = _lights.ToArray();
            if (_lightBuffer != null && lightArray.Length > 0)
            {
                // Resize buffer if needed
                if (lightArray.Length > _lightBuffer.ElementCount)
                {
                    RecreateBuffers();
                }
                _lightBuffer.SetData(lightArray);
            }

            // Dispatch compute shader to:
            // 1. Assign lights to clusters based on AABB intersection
            // 2. Build light index lists per cluster
            // 3. Store in light grid buffer

            // Note: Actual compute shader dispatch would be implemented here
            // when compute shader support is available in MonoGame
            // _graphicsDevice.DispatchCompute(...);
        }

        /// <summary>
        /// Clears all lights.
        /// </summary>
        public void ClearLights()
        {
            _lights.Clear();
        }

        private void RecreateBuffers()
        {
            DisposeBuffers();

            _clusterCount = (int)(_config.ClusterCounts.X * _config.ClusterCounts.Y * _config.ClusterCounts.Z);
            if (_clusterCount <= 0)
            {
                _clusterCount = 1;
            }

            // Calculate buffer sizes
            int lightDataStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(LightData));
            int maxLightsForBuffer = Math.Max(_maxLights, _lights.Count);
            if (maxLightsForBuffer <= 0)
            {
                maxLightsForBuffer = 1;
            }

            // Light index buffer: stores light indices for all clusters
            // Each cluster can have up to _maxLightsPerCluster lights
            int maxLightIndices = _clusterCount * _maxLightsPerCluster;

            // Light grid buffer: stores offset + count for each cluster
            int lightGridStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(LightGridEntry));

            // Create light buffer (structured buffer for all lights)
            _lightBuffer = new MonoGameGraphicsBuffer(
                _graphicsDevice,
                maxLightsForBuffer,
                lightDataStride,
                isDynamic: true
            );

            // Create light index buffer (stores light indices per cluster)
            // Using uint (4 bytes) for light indices
            _lightIndexBuffer = new MonoGameGraphicsBuffer(
                _graphicsDevice,
                maxLightIndices,
                sizeof(uint),
                isDynamic: true
            );

            // Create light grid buffer (offset + count per cluster)
            _lightGridBuffer = new MonoGameGraphicsBuffer(
                _graphicsDevice,
                _clusterCount,
                lightGridStride,
                isDynamic: true
            );
        }

        private void DisposeBuffers()
        {
            if (_lightBuffer != null)
            {
                _lightBuffer.Dispose();
                _lightBuffer = null;
            }

            if (_lightIndexBuffer != null)
            {
                _lightIndexBuffer.Dispose();
                _lightIndexBuffer = null;
            }

            if (_lightGridBuffer != null)
            {
                _lightGridBuffer.Dispose();
                _lightGridBuffer = null;
            }
        }

        /// <summary>
        /// Gets the light buffer for binding to shaders.
        /// </summary>
        internal MonoGameGraphicsBuffer LightBuffer => _lightBuffer;

        /// <summary>
        /// Gets the light index buffer for binding to shaders.
        /// </summary>
        internal MonoGameGraphicsBuffer LightIndexBuffer => _lightIndexBuffer;

        /// <summary>
        /// Gets the light grid buffer for binding to shaders.
        /// </summary>
        internal MonoGameGraphicsBuffer LightGridBuffer => _lightGridBuffer;

        public void Dispose()
        {
            DisposeBuffers();
            _lights.Clear();
        }
    }
}

