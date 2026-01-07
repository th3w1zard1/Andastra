using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;

namespace Andastra.Runtime.MonoGame.Lighting
{
    /// <summary>
    /// Clustered forward+ lighting system.
    ///
    /// Divides the view frustum into 3D clusters and assigns lights to clusters.
    /// This allows efficient per-pixel lighting with hundreds of dynamic lights.
    ///
    /// Features:
    /// - 3D clustered light culling
    /// - Support for point, spot, directional, and area lights
    /// - Volumetric lighting integration
    /// - Environment probe blending
    /// </summary>
    public class ClusteredLightingSystem : ILightingSystem
    {
        // Cluster configuration
        private const int ClusterCountX = 16;
        private const int ClusterCountY = 8;
        private const int ClusterCountZ = 24;
        private const int MaxLightsPerCluster = 128;

        private readonly List<DynamicLight> _lights;
        private readonly Dictionary<uint, DynamicLight> _lightMap;
        private DynamicLight _primaryDirectional;

        // Cluster data
        private int[] _clusterLightCounts;
        private int[] _clusterLightIndices;
        private IntPtr _clusterBuffer;
        private IntPtr _lightBuffer;

        // Fog state
        private FogSettings _fog;

        // Global illumination
        private IntPtr _giProbeTexture;
        private float _giIntensity = 1.0f;

        private bool _disposed;
        private bool _clustersDirty = true;

        public int MaxLights { get; private set; }

        public int ActiveLightCount
        {
            get { return _lights.Count; }
        }

        public Vector3 AmbientColor { get; set; } = new Vector3(0.03f, 0.03f, 0.05f);
        public float AmbientIntensity { get; set; } = 1.0f;
        public float SkyLightIntensity { get; set; } = 1.0f;

        public IDynamicLight PrimaryDirectionalLight
        {
            get { return _primaryDirectional; }
        }

        public ClusteredLightingSystem(int maxLights = 1024)
        {
            MaxLights = maxLights;
            _lights = new List<DynamicLight>(maxLights);
            _lightMap = new Dictionary<uint, DynamicLight>(maxLights);

            // Allocate cluster data
            int totalClusters = ClusterCountX * ClusterCountY * ClusterCountZ;
            _clusterLightCounts = new int[totalClusters];
            _clusterLightIndices = new int[totalClusters * MaxLightsPerCluster];

            // Create default fog
            _fog = new FogSettings
            {
                Enabled = false,
                Mode = FogMode.Exponential,
                Color = new Vector3(0.5f, 0.6f, 0.7f),
                Density = 0.01f
            };
        }

        public IDynamicLight CreateLight(LightType type)
        {
            if (_lights.Count >= MaxLights)
            {
                Console.WriteLine("[ClusteredLighting] Max lights reached: " + MaxLights);
                return null;
            }

            var light = new DynamicLight(type);
            _lights.Add(light);
            _lightMap[light.LightId] = light;
            _clustersDirty = true;

            return light;
        }

        public void RemoveLight(IDynamicLight light)
        {
            if (light == null)
            {
                return;
            }

            var dynamicLight = light as DynamicLight;
            if (dynamicLight != null)
            {
                _lights.Remove(dynamicLight);
                _lightMap.Remove(dynamicLight.LightId);

                if (_primaryDirectional == dynamicLight)
                {
                    _primaryDirectional = null;
                }

                dynamicLight.Dispose();
                _clustersDirty = true;
            }
        }

        public void SetPrimaryDirectionalLight(IDynamicLight light)
        {
            _primaryDirectional = light as DynamicLight;
        }

        public IDynamicLight[] GetActiveLights()
        {
            var active = new List<IDynamicLight>();
            foreach (DynamicLight light in _lights)
            {
                if (light.Enabled)
                {
                    active.Add(light);
                }
            }
            return active.ToArray();
        }

        public IDynamicLight[] GetLightsAffectingPoint(Vector3 position, float radius)
        {
            var affecting = new List<IDynamicLight>();

            foreach (DynamicLight light in _lights)
            {
                if (!light.Enabled)
                {
                    continue;
                }

                switch (light.Type)
                {
                    case LightType.Directional:
                        // Directional lights affect everything
                        affecting.Add(light);
                        break;

                    case LightType.Point:
                    case LightType.Spot:
                    case LightType.Area:
                        // Check distance
                        float dist = Vector3.Distance(light.Position, position);
                        if (dist <= light.Radius + radius)
                        {
                            affecting.Add(light);
                        }
                        break;
                }
            }

            return affecting.ToArray();
        }

        public void UpdateClustering(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (!_clustersDirty)
            {
                return;
            }

            // Clear cluster data
            Array.Clear(_clusterLightCounts, 0, _clusterLightCounts.Length);

            // Invert matrices for world-space calculations
            Matrix4x4.Invert(viewMatrix, out Matrix4x4 invView);
            Matrix4x4.Invert(projectionMatrix, out Matrix4x4 invProj);

            // Build frustum planes for each cluster
            // Assign lights to clusters based on AABB intersection

            foreach (DynamicLight light in _lights)
            {
                if (!light.Enabled)
                {
                    continue;
                }

                // Transform light to view space
                Vector3 viewPos = Vector3.Transform(light.Position, viewMatrix);

                // Determine which clusters this light affects
                AssignLightToClusters(light, viewPos, projectionMatrix);
            }

            _clustersDirty = false;
        }

        public void SubmitLightData()
        {
            // Update GPU buffers with current light data
            foreach (DynamicLight light in _lights)
            {
                light.UpdateGpuData();
            }

            // Upload cluster assignment data
            // Upload light data buffer
        }

        public void SetGlobalIlluminationProbe(IntPtr probeTexture, float intensity)
        {
            _giProbeTexture = probeTexture;
            _giIntensity = intensity;
        }

        public void SetFog(FogSettings fog)
        {
            _fog = fog;
        }

        public FogSettings GetFog()
        {
            return _fog;
        }

        private void AssignLightToClusters(DynamicLight light, Vector3 viewPos, Matrix4x4 projection)
        {
            // Calculate light bounds in clip space
            float lightRadius = light.Radius;

            // For directional lights, affect all clusters
            if (light.Type == LightType.Directional)
            {
                for (int z = 0; z < ClusterCountZ; z++)
                {
                    for (int y = 0; y < ClusterCountY; y++)
                    {
                        for (int x = 0; x < ClusterCountX; x++)
                        {
                            int clusterIdx = x + y * ClusterCountX + z * ClusterCountX * ClusterCountY;
                            AddLightToCluster(clusterIdx, light);
                        }
                    }
                }
                return;
            }

            // For local lights, calculate affected cluster range
            // Using conservative AABB in view space

            // Project light bounds to determine X/Y cluster range
            Vector4 clipPos = Vector4.Transform(new Vector4(viewPos, 1), projection);

            if (clipPos.W <= 0)
            {
                return; // Behind camera
            }

            // NDC position
            float ndcX = clipPos.X / clipPos.W;
            float ndcY = clipPos.Y / clipPos.W;

            // Convert to cluster coordinates
            int centerX = (int)((ndcX * 0.5f + 0.5f) * ClusterCountX);
            int centerY = (int)((ndcY * 0.5f + 0.5f) * ClusterCountY);

            // Calculate Z cluster from depth
            float depth = -viewPos.Z;
            int centerZ = DepthToClusterZ(depth);

            // Calculate cluster radius based on light radius in screen space
            float screenRadius = lightRadius / depth * ClusterCountX * 0.5f;
            int clusterRadius = (int)Math.Ceiling(screenRadius) + 1;

            // Assign to affected clusters
            int minX = Math.Max(0, centerX - clusterRadius);
            int maxX = Math.Min(ClusterCountX - 1, centerX + clusterRadius);
            int minY = Math.Max(0, centerY - clusterRadius);
            int maxY = Math.Min(ClusterCountY - 1, centerY + clusterRadius);
            int minZ = Math.Max(0, centerZ - 2);
            int maxZ = Math.Min(ClusterCountZ - 1, centerZ + 2);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        int clusterIdx = x + y * ClusterCountX + z * ClusterCountX * ClusterCountY;
                        AddLightToCluster(clusterIdx, light);
                    }
                }
            }
        }

        private void AddLightToCluster(int clusterIdx, DynamicLight light)
        {
            int count = _clusterLightCounts[clusterIdx];
            if (count >= MaxLightsPerCluster)
            {
                return;
            }

            int offset = clusterIdx * MaxLightsPerCluster + count;
            _clusterLightIndices[offset] = (int)light.LightId;
            _clusterLightCounts[clusterIdx] = count + 1;
        }

        private int DepthToClusterZ(float depth)
        {
            // Logarithmic depth distribution for better near/far coverage
            float near = 0.1f;
            float far = 1000f;

            if (depth <= near) return 0;
            if (depth >= far) return ClusterCountZ - 1;

            float logDepth = (float)(Math.Log(depth / near) / Math.Log(far / near));
            return (int)(logDepth * ClusterCountZ);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (DynamicLight light in _lights)
            {
                light.Dispose();
            }
            _lights.Clear();
            _lightMap.Clear();

            // Release GPU buffers

            _disposed = true;
        }
    }
}

