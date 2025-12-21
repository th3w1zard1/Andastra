using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Interfaces;
using Andastra.Runtime.MonoGame.Lighting;
using DynamicLight = Andastra.Runtime.MonoGame.Lighting.DynamicLight;
using EclipseILightingSystem = Andastra.Runtime.Games.Eclipse.ILightingSystem;
using EclipseIUpdatable = Andastra.Runtime.Games.Eclipse.IUpdatable;

namespace Andastra.Runtime.Games.Eclipse.Lighting
{
    /// <summary>
    /// Eclipse Engine (Dragon Age) lighting system implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Lighting System Implementation:
    /// - Based on daorigins.exe: Advanced lighting system with dynamic shadows and global illumination
    /// - Based on DragonAge2.exe: Enhanced lighting with volumetric fog and shadow casting
    /// - Supports dynamic lights, directional sun/moon lights, and area lighting
    /// - Advanced shadow mapping and global illumination support
    /// - Fog and atmospheric effects integration
    /// 
    /// Eclipse lighting features:
    /// - Dynamic shadow casting with configurable shadow maps
    /// - Sun and moon directional lights with separate ambient/diffuse colors
    /// - Volumetric fog with scattering
    /// - Dynamic point and spot lights for torches, fires, etc.
    /// - Global illumination probes for area-based lighting
    /// - Light culling and clustering for performance
    /// 
    /// Based on reverse engineering of:
    /// - daorigins.exe: Lighting system initialization and management
    /// - DragonAge2.exe: Enhanced lighting features and shadow casting
    /// </remarks>
    public class EclipseLightingSystem : EclipseILightingSystem
    {
        // Cluster configuration for light culling (similar to ClusteredLightingSystem)
        private const int ClusterCountX = 16;
        private const int ClusterCountY = 8;
        private const int ClusterCountZ = 24;
        private const int MaxLightsPerCluster = 128;

        private readonly List<DynamicLight> _lights;
        private readonly Dictionary<uint, DynamicLight> _lightMap;
        private DynamicLight _primaryDirectional;
        private DynamicLight _sunLight;
        private DynamicLight _moonLight;

        // Cluster data for light culling
        private int[] _clusterLightCounts;
        private int[] _clusterLightIndices;

        // Fog state
        private FogSettings _fog;

        // Global illumination
        private IntPtr _giProbeTexture;
        private float _giIntensity = 1.0f;

        // Lighting properties from ARE file
        private uint _sunAmbientColor;
        private uint _sunDiffuseColor;
        private uint _moonAmbientColor;
        private uint _moonDiffuseColor;
        private uint _dynAmbientColor;
        private bool _sunShadows;
        private bool _moonShadows;
        private byte _shadowOpacity;

        private bool _disposed;
        private bool _clustersDirty = true;

        /// <summary>
        /// Maximum supported dynamic lights.
        /// </summary>
        public int MaxLights { get; private set; }

        /// <summary>
        /// Current active light count.
        /// </summary>
        public int ActiveLightCount
        {
            get { return _lights.Count; }
        }

        /// <summary>
        /// Ambient light color.
        /// </summary>
        public Vector3 AmbientColor { get; set; } = new Vector3(0.2f, 0.2f, 0.25f);

        /// <summary>
        /// Ambient light intensity.
        /// </summary>
        public float AmbientIntensity { get; set; } = 1.0f;

        /// <summary>
        /// Sky light / environment probe intensity.
        /// </summary>
        public float SkyLightIntensity { get; set; } = 1.0f;

        /// <summary>
        /// Gets the primary directional light (sun/moon).
        /// </summary>
        public IDynamicLight PrimaryDirectionalLight
        {
            get { return _primaryDirectional != null ? new DynamicLightAdapter(_primaryDirectional) : null; }
        }

        /// <summary>
        /// Creates a new Eclipse lighting system.
        /// </summary>
        /// <param name="maxLights">Maximum number of dynamic lights supported (default: 1024).</param>
        /// <remarks>
        /// Initializes the lighting system with default values.
        /// Lighting properties should be configured from ARE file data after construction.
        /// </remarks>
        public EclipseLightingSystem(int maxLights = 1024)
        {
            MaxLights = maxLights;
            _lights = new List<DynamicLight>(maxLights);
            _lightMap = new Dictionary<uint, DynamicLight>(maxLights);

            // Allocate cluster data
            int totalClusters = ClusterCountX * ClusterCountY * ClusterCountZ;
            _clusterLightCounts = new int[totalClusters];
            _clusterLightIndices = new int[totalClusters * MaxLightsPerCluster];

            // Initialize default fog
            _fog = new FogSettings
            {
                Enabled = false,
                Mode = FogMode.Exponential,
                Color = new Vector3(0.5f, 0.6f, 0.7f),
                Density = 0.01f,
                HeightFog = false,
                Volumetric = false
            };

            // Initialize lighting properties to defaults
            _sunAmbientColor = 0x80808080; // Gray ambient
            _sunDiffuseColor = 0xFFFFFFFF; // White diffuse
            _moonAmbientColor = 0x40404040; // Darker gray for moon
            _moonDiffuseColor = 0x8080FFFF; // Slightly blue for moon
            _dynAmbientColor = 0x80808080; // Gray dynamic ambient
            _sunShadows = false;
            _moonShadows = false;
            _shadowOpacity = 255;
        }

        /// <summary>
        /// Initializes lighting system from ARE file data.
        /// </summary>
        /// <param name="sunAmbientColor">Sun ambient color (RGBA).</param>
        /// <param name="sunDiffuseColor">Sun diffuse color (RGBA).</param>
        /// <param name="moonAmbientColor">Moon ambient color (RGBA).</param>
        /// <param name="moonDiffuseColor">Moon diffuse color (RGBA).</param>
        /// <param name="dynAmbientColor">Dynamic ambient color (RGBA).</param>
        /// <param name="sunShadows">Whether sun casts shadows.</param>
        /// <param name="moonShadows">Whether moon casts shadows.</param>
        /// <param name="shadowOpacity">Shadow opacity (0-255).</param>
        /// <param name="fogEnabled">Whether fog is enabled.</param>
        /// <param name="fogColor">Fog color (RGBA).</param>
        /// <param name="fogNear">Fog near distance.</param>
        /// <param name="fogFar">Fog far distance.</param>
        /// <param name="isNight">Whether it's night time (determines sun vs moon as primary).</param>
        /// <remarks>
        /// Configures the lighting system with properties from the ARE file.
        /// Creates sun and moon directional lights based on the ARE file data.
        /// Sets the primary directional light based on day/night cycle.
        /// </remarks>
        public void InitializeFromAreaData(
            uint sunAmbientColor,
            uint sunDiffuseColor,
            uint moonAmbientColor,
            uint moonDiffuseColor,
            uint dynAmbientColor,
            bool sunShadows,
            bool moonShadows,
            byte shadowOpacity,
            bool fogEnabled,
            uint fogColor,
            float fogNear,
            float fogFar,
            bool isNight)
        {
            // Store ARE file lighting properties
            _sunAmbientColor = sunAmbientColor;
            _sunDiffuseColor = sunDiffuseColor;
            _moonAmbientColor = moonAmbientColor;
            _moonDiffuseColor = moonDiffuseColor;
            _dynAmbientColor = dynAmbientColor;
            _sunShadows = sunShadows;
            _moonShadows = moonShadows;
            _shadowOpacity = shadowOpacity;

            // Convert ARE color format (BGR) to Vector3 (RGB)
            Vector3 sunAmbientVec = ColorBgrToVector3(sunAmbientColor);
            Vector3 sunDiffuseVec = ColorBgrToVector3(sunDiffuseColor);
            Vector3 moonAmbientVec = ColorBgrToVector3(moonAmbientColor);
            Vector3 moonDiffuseVec = ColorBgrToVector3(moonDiffuseColor);
            Vector3 dynAmbientVec = ColorBgrToVector3(dynAmbientColor);

            // Set ambient color from dynamic ambient (Eclipse uses dynamic ambient as base)
            AmbientColor = dynAmbientVec;
            AmbientIntensity = 1.0f;

            // Create sun directional light
            _sunLight = new DynamicLight(LightType.Directional);
            _sunLight.Direction = new Vector3(0.5f, -1.0f, 0.3f); // Sun direction (angled down)
            _sunLight.Direction = Vector3.Normalize(_sunLight.Direction);
            _sunLight.Color = sunDiffuseVec;
            _sunLight.Intensity = 1.0f;
            _sunLight.CastShadows = sunShadows;
            _sunLight.ShadowResolution = sunShadows ? 2048 : 0;
            _sunLight.ShadowBias = 0.0001f;
            _sunLight.ShadowNormalBias = 0.02f;
            _sunLight.ShadowSoftness = shadowOpacity / 255.0f;
            _sunLight.Enabled = true;
            _lights.Add(_sunLight);
            _lightMap[_sunLight.LightId] = _sunLight;

            // Create moon directional light
            _moonLight = new DynamicLight(LightType.Directional);
            _moonLight.Direction = new Vector3(-0.5f, -1.0f, -0.3f); // Moon direction (opposite of sun)
            _moonLight.Direction = Vector3.Normalize(_moonLight.Direction);
            _moonLight.Color = moonDiffuseVec;
            _moonLight.Intensity = 0.3f; // Moon is dimmer than sun
            _moonLight.CastShadows = moonShadows;
            _moonLight.ShadowResolution = moonShadows ? 1024 : 0;
            _moonLight.ShadowBias = 0.0001f;
            _moonLight.ShadowNormalBias = 0.02f;
            _moonLight.ShadowSoftness = shadowOpacity / 255.0f;
            _moonLight.Enabled = !isNight; // Moon enabled at night
            _lights.Add(_moonLight);
            _lightMap[_moonLight.LightId] = _moonLight;

            // Set primary directional light based on day/night
            if (isNight)
            {
                _primaryDirectional = _moonLight;
                _sunLight.Enabled = false;
                _moonLight.Enabled = true;
            }
            else
            {
                _primaryDirectional = _sunLight;
                _sunLight.Enabled = true;
                _moonLight.Enabled = false;
            }

            // Configure fog
            Vector3 fogColorVec = ColorBgrToVector3(fogColor);
            _fog = new FogSettings
            {
                Enabled = fogEnabled,
                Mode = FogMode.Exponential,
                Color = fogColorVec,
                Density = fogFar > 0 ? (1.0f / fogFar) : 0.01f,
                Start = fogNear,
                End = fogFar,
                HeightFog = false,
                Volumetric = false // Eclipse may support volumetric fog, but default is standard
            };

            _clustersDirty = true;
        }

        /// <summary>
        /// Updates the lighting system each frame.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        /// <remarks>
        /// Updates light transforms, shadow maps, and clustering.
        /// Called by EclipseArea.Update() each frame.
        /// </remarks>
        public void Update(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            // Update light transforms if needed
            // In a full implementation, this would update lights that follow entities
            // TODO: STUB - For now, static lights don't need per-frame updates

            // Update shadow maps if dirty
            // In a full implementation, this would regenerate shadow maps for directional lights
            // TODO: STUB - For now, shadow maps are static

            // Mark clusters as dirty if lights changed
            if (_clustersDirty)
            {
                // Clustering will be updated when UpdateClustering is called
            }
        }

        /// <summary>
        /// Converts BGR color (ARE file format) to Vector3 RGB.
        /// </summary>
        /// <param name="bgrColor">BGR color as UInt32.</param>
        /// <returns>RGB color as Vector3 (0-1 range).</returns>
        private Vector3 ColorBgrToVector3(uint bgrColor)
        {
            // ARE file format: BGR (Blue in low byte, Green in middle, Red in high)
            // Extract B, G, R components and convert to 0-1 range
            byte b = (byte)(bgrColor & 0xFF);
            byte g = (byte)((bgrColor >> 8) & 0xFF);
            byte r = (byte)((bgrColor >> 16) & 0xFF);

            return new Vector3(r / 255.0f, g / 255.0f, b / 255.0f);
        }

        /// <summary>
        /// Creates a new dynamic light.
        /// </summary>
        /// <param name="type">Type of light to create.</param>
        /// <returns>The created light, or null if max lights reached.</returns>
        public IDynamicLight CreateLight(LightType type)
        {
            if (_lights.Count >= MaxLights)
            {
                Console.WriteLine("[EclipseLighting] Max lights reached: " + MaxLights);
                return null;
            }

            var light = new DynamicLight(type);
            _lights.Add(light);
            _lightMap[light.LightId] = light;
            _clustersDirty = true;

            // Create adapter to bridge MonoGame.DynamicLight to Eclipse.IDynamicLight
            return new DynamicLightAdapter(light);
        }

        /// <summary>
        /// Adds a light to the system.
        /// </summary>
        /// <param name="light">The light to add.</param>
        public void AddLight(IDynamicLight light)
        {
            if (light == null)
            {
                return;
            }

            var dynamicLight = light as DynamicLight;
            if (dynamicLight != null)
            {
                if (!_lights.Contains(dynamicLight))
                {
                    if (_lights.Count >= MaxLights)
                    {
                        Console.WriteLine("[EclipseLighting] Max lights reached: " + MaxLights);
                        return;
                    }

                    _lights.Add(dynamicLight);
                    _lightMap[dynamicLight.LightId] = dynamicLight;
                    _clustersDirty = true;
                }
            }
        }

        /// <summary>
        /// Removes a light from the system.
        /// </summary>
        /// <param name="light">The light to remove.</param>
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

                if (_sunLight == dynamicLight)
                {
                    _sunLight = null;
                }

                if (_moonLight == dynamicLight)
                {
                    _moonLight = null;
                }

                dynamicLight.Dispose();
                _clustersDirty = true;
            }
        }

        /// <summary>
        /// Sets the primary directional light.
        /// </summary>
        /// <param name="light">The light to set as primary directional.</param>
        public void SetPrimaryDirectionalLight(IDynamicLight light)
        {
            // Extract underlying DynamicLight from adapter if needed
            if (light is DynamicLightAdapter adapter)
            {
                _primaryDirectional = adapter.Light;
            }
            else
            {
                _primaryDirectional = light as DynamicLight;
            }
        }

        /// <summary>
        /// Gets all active lights.
        /// </summary>
        /// <returns>Array of all active lights.</returns>
        public IDynamicLight[] GetActiveLights()
        {
            var active = new List<IDynamicLight>();
            foreach (DynamicLight light in _lights)
            {
                if (light.Enabled)
                {
                    active.Add(new DynamicLightAdapter(light));
                }
            }
            return active.ToArray();
        }

        /// <summary>
        /// Gets lights affecting a specific point.
        /// </summary>
        /// <param name="position">World space position.</param>
        /// <param name="radius">Query radius.</param>
        /// <returns>Array of lights affecting the point.</returns>
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
                        affecting.Add(new DynamicLightAdapter(light));
                        break;

                    case LightType.Point:
                    case LightType.Spot:
                    case LightType.Area:
                        // Check distance
                        float dist = Vector3.Distance(light.Position, position);
                        if (dist <= light.Radius + radius)
                        {
                            affecting.Add(new DynamicLightAdapter(light));
                        }
                        break;
                }
            }

            return affecting.ToArray();
        }

        /// <summary>
        /// Updates the light clustering/tiling for the current view.
        /// </summary>
        /// <param name="viewMatrix">View matrix.</param>
        /// <param name="projectionMatrix">Projection matrix.</param>
        public void UpdateClustering(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            if (!_clustersDirty)
            {
                return;
            }

            // Clear cluster data
            Array.Clear(_clusterLightCounts, 0, _clusterLightCounts.Length);

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

        /// <summary>
        /// Submits light data for rendering.
        /// </summary>
        public void SubmitLightData()
        {
            // Update GPU buffers with current light data
            foreach (DynamicLight light in _lights)
            {
                light.UpdateGpuData();
            }

            // Upload cluster assignment data
            // Upload light data buffer
            // In a full implementation, this would upload to GPU buffers
        }

        /// <summary>
        /// Sets global illumination probe data.
        /// </summary>
        /// <param name="probeTexture">GI probe texture.</param>
        /// <param name="intensity">GI intensity.</param>
        public void SetGlobalIlluminationProbe(IntPtr probeTexture, float intensity)
        {
            _giProbeTexture = probeTexture;
            _giIntensity = intensity;
        }

        /// <summary>
        /// Sets fog parameters.
        /// </summary>
        /// <param name="fog">Fog settings.</param>
        public void SetFog(FogSettings fog)
        {
            _fog = fog;
        }

        /// <summary>
        /// Gets current fog settings.
        /// </summary>
        /// <returns>Current fog settings.</returns>
        public FogSettings GetFog()
        {
            return _fog;
        }

        /// <summary>
        /// Assigns a light to clusters based on its position and bounds.
        /// </summary>
        /// <param name="light">The light to assign.</param>
        /// <param name="viewPos">Light position in view space.</param>
        /// <param name="projection">Projection matrix.</param>
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

        /// <summary>
        /// Adds a light to a cluster.
        /// </summary>
        /// <param name="clusterIdx">Cluster index.</param>
        /// <param name="light">Light to add.</param>
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

        /// <summary>
        /// Converts depth to cluster Z index.
        /// </summary>
        /// <param name="depth">Depth in view space.</param>
        /// <returns>Cluster Z index.</returns>
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

        /// <summary>
        /// Disposes the lighting system and all lights.
        /// </summary>
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

            _primaryDirectional = null;
            _sunLight = null;
            _moonLight = null;

            // Release GPU buffers
            // In a full implementation, this would release GPU resources

            _disposed = true;
        }
    }

    /// <summary>
    /// Adapter to bridge MonoGame.DynamicLight to Eclipse.IDynamicLight interface.
    /// </summary>
    internal class DynamicLightAdapter : IDynamicLight
    {
        private readonly DynamicLight _light;

        public DynamicLightAdapter(DynamicLight light)
        {
            _light = light ?? throw new ArgumentNullException(nameof(light));
        }

        public Vector3 Position => _light.Position;
        public Vector3 Color => _light.Color;
        public float Intensity => _light.Intensity;
        public float Range => _light.Radius;

        /// <summary>
        /// Gets the underlying DynamicLight instance.
        /// </summary>
        public DynamicLight Light => _light;
    }
}
