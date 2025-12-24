using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Enums;
using Andastra.Runtime.MonoGame.Graphics;
using Andastra.Runtime.MonoGame.Interfaces;
using Microsoft.Xna.Framework.Graphics;
using DynamicLight = Andastra.Runtime.MonoGame.Lighting.DynamicLight;
using IDynamicLight = Andastra.Runtime.MonoGame.Interfaces.IDynamicLight;

namespace Andastra.Runtime.Games.Eclipse.Lighting
{
    /// <summary>
    /// Light grid entry structure (offset + count per cluster) for GPU buffer.
    /// Matches ClusteredLightCulling.LightGridEntry for consistency.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct LightGridEntry
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
    public class EclipseLightingSystem : ILightingSystem
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

        // GPU buffers for cluster data (optional - only created if GraphicsDevice is provided)
        private GraphicsDevice _graphicsDevice;
        private MonoGameGraphicsBuffer _lightIndexBuffer;
        private MonoGameGraphicsBuffer _lightGridBuffer;
        private bool _buffersDirty = true;

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

        // Entity-light attachment tracking
        // Maps entity object ID to lights attached to that entity
        private readonly Dictionary<uint, List<DynamicLight>> _entityLightMap;
        // Maps light to its attached entity ID (for reverse lookup)
        private readonly Dictionary<DynamicLight, uint> _lightEntityMap;

        // Day/night cycle tracking
        private float _timeOfDay = 0.5f; // 0.0 = midnight, 0.5 = noon, 1.0 = midnight
        private bool _enableDayNightCycle = false;
        private float _dayNightCycleSpeed = 1.0f;

        // Shadow map update tracking
        private bool _shadowMapsDirty = false;
        private float _shadowMapUpdateInterval = 1.0f / 60.0f; // Update shadow maps at 60Hz
        private float _shadowMapUpdateTimer = 0.0f;

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
        /// Gets or sets the graphics device for GPU buffer support.
        /// Setting this will create GPU buffers if they don't exist.
        /// </summary>
        public GraphicsDevice GraphicsDevice
        {
            get { return _graphicsDevice; }
            set
            {
                if (_graphicsDevice != value)
                {
                    _graphicsDevice = value;
                    if (_graphicsDevice != null && (_lightIndexBuffer == null || _lightGridBuffer == null))
                    {
                        CreateGpuBuffers();
                        _buffersDirty = true;
                    }
                    else if (_graphicsDevice == null)
                    {
                        DisposeGpuBuffers();
                    }
                }
            }
        }

        /// <summary>
        /// Creates a new Eclipse lighting system.
        /// </summary>
        /// <param name="maxLights">Maximum number of dynamic lights supported (default: 1024).</param>
        /// <param name="graphicsDevice">Optional graphics device for GPU buffer support. If null, GPU buffers will not be created.</param>
        /// <remarks>
        /// Initializes the lighting system with default values.
        /// Lighting properties should be configured from ARE file data after construction.
        /// If graphicsDevice is provided, GPU buffers will be created for efficient cluster data upload.
        /// </remarks>
        public EclipseLightingSystem(int maxLights = 1024, GraphicsDevice graphicsDevice = null)
        {
            MaxLights = maxLights;
            _lights = new List<DynamicLight>(maxLights);
            _lightMap = new Dictionary<uint, DynamicLight>(maxLights);
            _entityLightMap = new Dictionary<uint, List<DynamicLight>>();
            _lightEntityMap = new Dictionary<DynamicLight, uint>();
            _graphicsDevice = graphicsDevice;

            // Allocate cluster data
            int totalClusters = ClusterCountX * ClusterCountY * ClusterCountZ;
            _clusterLightCounts = new int[totalClusters];
            _clusterLightIndices = new int[totalClusters * MaxLightsPerCluster];

            // Create GPU buffers if graphics device is available
            if (_graphicsDevice != null)
            {
                CreateGpuBuffers();
            }

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
        ///
        /// Per-frame updates include:
        /// - Day/night cycle updates for sun/moon directional lights
        /// - Shadow map updates for directional lights (periodic)
        /// - Marking clusters as dirty when lights change
        ///
        /// Note: Entity-attached light updates are handled separately via UpdateEntityLightTransforms()
        /// since they require entity system access.
        /// </remarks>
        public void Update(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            // Update day/night cycle if enabled
            if (_enableDayNightCycle)
            {
                _timeOfDay += deltaTime * _dayNightCycleSpeed;
                if (_timeOfDay >= 1.0f)
                {
                    _timeOfDay -= 1.0f; // Wrap around
                }
                if (_timeOfDay < 0.0f)
                {
                    _timeOfDay += 1.0f; // Wrap around (if speed is negative)
                }

                UpdateDayNightCycle();
            }

            // Update shadow maps periodically for directional lights
            _shadowMapUpdateTimer += deltaTime;
            if (_shadowMapUpdateTimer >= _shadowMapUpdateInterval || _shadowMapsDirty)
            {
                UpdateShadowMaps();
                _shadowMapUpdateTimer = 0.0f;
                _shadowMapsDirty = false;
            }

            // Mark clusters as dirty if lights changed (will be updated when UpdateClustering is called)
            // Clustering is updated separately to avoid per-frame cost
        }

        /// <summary>
        /// Updates entity-attached light transforms based on entity world matrices.
        /// This should be called from EclipseArea.Update() with entity transform data.
        /// </summary>
        /// <param name="entityTransforms">Dictionary mapping entity object ID to world transform matrix.</param>
        /// <remarks>
        /// Entity Light Transform Updates:
        /// - Updates lights that are attached to entities (torches, fires, etc.)
        /// - Lights follow entity position and rotation
        /// - Marks clusters as dirty when lights move
        /// - Based on Eclipse engine pattern: lights attached to placeables and creatures
        /// </remarks>
        public void UpdateEntityLightTransforms(Dictionary<uint, Matrix4x4> entityTransforms)
        {
            if (_disposed || entityTransforms == null)
            {
                return;
            }

            bool lightsMoved = false;

            // Update each entity's attached lights
            foreach (var entityEntry in _entityLightMap)
            {
                uint entityId = entityEntry.Key;
                List<DynamicLight> entityLights = entityEntry.Value;

                // Check if entity has a transform
                if (!entityTransforms.TryGetValue(entityId, out Matrix4x4 entityTransform))
                {
                    continue;
                }

                // Update all lights attached to this entity
                foreach (DynamicLight light in entityLights)
                {
                    if (light == null || !light.Enabled)
                    {
                        continue;
                    }

                    // Store previous position to detect movement
                    Vector3 oldPosition = light.Position;

                    // Update light transform from entity transform
                    light.UpdateTransform(entityTransform);

                    // Check if light moved (for cluster dirty marking)
                    if (Vector3.DistanceSquared(oldPosition, light.Position) > 0.0001f)
                    {
                        lightsMoved = true;
                    }
                }
            }

            // Mark clusters as dirty if any lights moved
            if (lightsMoved)
            {
                _clustersDirty = true;
                _shadowMapsDirty = true; // Shadow maps may need updates if lights moved significantly
            }
        }

        /// <summary>
        /// Updates sun/moon directional lights based on day/night cycle time.
        /// </summary>
        /// <remarks>
        /// Day/Night Cycle Updates:
        /// - Calculates sun/moon direction based on time of day
        /// - Transitions between sun and moon as primary directional light
        /// - Updates light colors and intensities based on time of day
        /// - Based on Eclipse engine day/night cycle system
        /// </remarks>
        private void UpdateDayNightCycle()
        {
            if (_sunLight == null || _moonLight == null)
            {
                return;
            }

            // Calculate sun angle based on time of day
            // 0.0 (midnight) = -90 degrees (below horizon), 0.5 (noon) = 90 degrees (above horizon)
            float sunAngle = (_timeOfDay - 0.5f) * 2.0f * (float)Math.PI; // Convert to radians
            float sunElevation = (float)Math.Sin(sunAngle); // -1 to 1 range

            // Sun direction: rotates around Y axis (east to west), elevates up/down
            float sunAzimuth = _timeOfDay * 2.0f * (float)Math.PI; // Full rotation over day
            float sunX = (float)Math.Cos(sunAzimuth) * (float)Math.Cos(sunElevation);
            float sunZ = (float)Math.Sin(sunAzimuth) * (float)Math.Cos(sunElevation);
            float sunY = sunElevation;

            Vector3 sunDirection = Vector3.Normalize(new Vector3(sunX, sunY, sunZ));
            Vector3 oldSunDirection = _sunLight.Direction;

            // Update sun direction
            _sunLight.Direction = sunDirection;

            // Moon is opposite of sun (180 degrees offset)
            Vector3 moonDirection = Vector3.Normalize(-sunDirection);
            _moonLight.Direction = moonDirection;

            // Determine if it's day or night
            bool isNight = sunElevation < 0.0f; // Below horizon = night

            // Update primary directional light
            if (isNight)
            {
                if (_primaryDirectional != _moonLight)
                {
                    _primaryDirectional = _moonLight;
                    _sunLight.Enabled = false;
                    _moonLight.Enabled = true;
                }
            }
            else
            {
                if (_primaryDirectional != _sunLight)
                {
                    _primaryDirectional = _sunLight;
                    _sunLight.Enabled = true;
                    _moonLight.Enabled = false;
                }

                // Adjust sun intensity based on elevation (fade near horizon)
                float sunIntensity = Math.Max(0.0f, sunElevation); // 0 at horizon, 1 at zenith
                _sunLight.Intensity = sunIntensity;
            }

            // Adjust ambient color based on time of day
            // Dawn/dusk: warmer colors, night: cooler colors
            float timeFactor = Math.Abs(sunElevation); // 0 at horizon, 1 at zenith/midnight
            Vector3 dayAmbient = ColorBgrToVector3(_sunAmbientColor);
            Vector3 nightAmbient = ColorBgrToVector3(_moonAmbientColor);
            AmbientColor = Vector3.Lerp(nightAmbient, dayAmbient, timeFactor);

            // Mark clusters as dirty if sun/moon direction changed significantly
            if (Vector3.DistanceSquared(oldSunDirection, sunDirection) > 0.0001f)
            {
                _clustersDirty = true;
                _shadowMapsDirty = true; // Shadow maps need updates when sun/moon moves
            }
        }

        /// <summary>
        /// Updates shadow maps for directional lights that cast shadows.
        /// </summary>
        /// <remarks>
        /// Shadow Map Updates:
        /// - Regenerates shadow maps for sun/moon directional lights if they moved
        /// - Updates shadow map matrices for rendering
        /// - Based on Eclipse engine shadow mapping system
        /// - Calculates view/projection matrices for directional light shadow mapping
        /// - Uses orthographic projection for directional lights (sun/moon)
        /// - Shadow map matrices are stored in DynamicLight for use during rendering
        ///
        /// Based on daorigins.exe/DragonAge2.exe: Shadow mapping system for directional lights
        /// - Directional lights use orthographic shadow maps (not perspective)
        /// - Shadow map covers a fixed area around the scene (configurable size)
        /// - View matrix looks from light direction, projection is orthographic
        /// </remarks>
        public void UpdateShadowMaps()
        {
            // Shadow map coverage area (world units)
            // Based on Eclipse engine: Shadow maps cover a reasonable area around the scene
            // Typical values: 50-200 units for most scenes
            // Can be made configurable per light if needed
            const float shadowMapSize = 100.0f; // Half-size (total coverage is 200x200 units)
            const float shadowMapNear = 0.1f;
            const float shadowMapFar = 500.0f; // Far plane for shadow map

            // Update sun shadow map if it casts shadows
            if (_sunLight != null && _sunLight.Enabled && _sunLight.CastShadows && _sunShadows)
            {
                // Calculate shadow map view/projection matrix based on sun direction
                // Based on Eclipse engine: Directional lights use orthographic shadow maps
                // View matrix: Look from light direction towards scene center
                // Projection matrix: Orthographic projection covering shadow map area

                // Calculate view matrix: look from light direction
                // Light direction points towards scene, so we look from opposite direction
                Vector3 lightDirection = _sunLight.Direction;
                Vector3 lightPosition = -lightDirection * shadowMapFar * 0.5f; // Position light far from scene
                Vector3 targetPosition = Vector3.Zero; // Look at scene center (can be made configurable)
                Vector3 upVector = Vector3.UnitY; // Use Y-up (can calculate proper up vector if needed)

                // If light direction is nearly parallel to up vector, use alternative up
                if (Math.Abs(Vector3.Dot(lightDirection, upVector)) > 0.9f)
                {
                    upVector = Vector3.UnitZ; // Use Z-up as alternative
                }

                // Create view matrix looking from light position towards target
                // Based on Eclipse engine: View matrix transforms world space to light space
                Matrix4x4 viewMatrix = MatrixHelper.CreateLookAt(lightPosition, targetPosition, upVector);

                // Create orthographic projection matrix
                // Orthographic projection: left, right, bottom, top, near, far
                // System.Numerics.Matrix4x4 doesn't have CreateOrthographic, so we use CreateOrthographicOffCenter
                float halfSize = shadowMapSize;
                Matrix4x4 projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                    -halfSize, // Left
                    halfSize,  // Right
                    -halfSize, // Bottom
                    halfSize,  // Top
                    shadowMapNear,
                    shadowMapFar
                );

                // Combined light space matrix: projection * view
                Matrix4x4 lightSpaceMatrix = projectionMatrix * viewMatrix;

                // Store matrices in light object for use during rendering
                _sunLight.ShadowViewMatrix = viewMatrix;
                _sunLight.ShadowProjectionMatrix = projectionMatrix;
                _sunLight.ShadowLightSpaceMatrix = lightSpaceMatrix;

                // Update GPU data (includes shadow map matrices)
                _sunLight.UpdateGpuData();
            }

            // Update moon shadow map if it casts shadows
            if (_moonLight != null && _moonLight.Enabled && _moonLight.CastShadows && _moonShadows)
            {
                // Calculate shadow map view/projection matrix based on moon direction
                // Same approach as sun, but using moon direction

                // Calculate view matrix: look from light direction
                Vector3 lightDirection = _moonLight.Direction;
                Vector3 lightPosition = -lightDirection * shadowMapFar * 0.5f; // Position light far from scene
                Vector3 targetPosition = Vector3.Zero; // Look at scene center
                Vector3 upVector = Vector3.UnitY; // Use Y-up

                // If light direction is nearly parallel to up vector, use alternative up
                if (Math.Abs(Vector3.Dot(lightDirection, upVector)) > 0.9f)
                {
                    upVector = Vector3.UnitZ; // Use Z-up as alternative
                }

                // Create view matrix looking from light position towards target
                // Based on Eclipse engine: View matrix transforms world space to light space
                Matrix4x4 viewMatrix = MatrixHelper.CreateLookAt(lightPosition, targetPosition, upVector);

                // Create orthographic projection matrix
                // Moon uses same shadow map size as sun (can be made different if needed)
                // System.Numerics.Matrix4x4 doesn't have CreateOrthographic, so we use CreateOrthographicOffCenter
                float halfSize = shadowMapSize;
                Matrix4x4 projectionMatrix = Matrix4x4.CreateOrthographicOffCenter(
                    -halfSize, // Left
                    halfSize,  // Right
                    -halfSize, // Bottom
                    halfSize,  // Top
                    shadowMapNear,
                    shadowMapFar
                );

                // Combined light space matrix: projection * view
                Matrix4x4 lightSpaceMatrix = projectionMatrix * viewMatrix;

                // Store matrices in light object for use during rendering
                _moonLight.ShadowViewMatrix = viewMatrix;
                _moonLight.ShadowProjectionMatrix = projectionMatrix;
                _moonLight.ShadowLightSpaceMatrix = lightSpaceMatrix;

                // Update GPU data (includes shadow map matrices)
                _moonLight.UpdateGpuData();
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
        /// Creates a new dynamic light with specified properties.
        /// </summary>
        /// <param name="type">Type of light to create.</param>
        /// <param name="position">Light position.</param>
        /// <param name="color">Light color.</param>
        /// <param name="radius">Light radius (for point/spot lights).</param>
        /// <param name="intensity">Light intensity.</param>
        /// <returns>The created light, or null if max lights reached.</returns>
        /// <remarks>
        /// Creates a light and configures all properties in one call.
        /// Based on daorigins.exe: Lights are created with position, color, radius, and intensity.
        /// </remarks>
        public IDynamicLight CreateLight(LightType type, Vector3 position, Vector3 color, float radius, float intensity)
        {
            IDynamicLight light = CreateLight(type);
            if (light != null)
            {
                // Access underlying DynamicLight to set properties
                // DynamicLightAdapter wraps the underlying DynamicLight
                var adapter = light as DynamicLightAdapter;
                if (adapter != null)
                {
                    DynamicLight underlyingLight = adapter.Light;
                    underlyingLight.Position = position;
                    underlyingLight.Color = color;
                    underlyingLight.Radius = radius;
                    underlyingLight.Intensity = intensity;
                    underlyingLight.Enabled = true;
                }
            }
            return light;
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

            DynamicLight dynamicLight = null;
            var directLight = light as DynamicLight;
            if (directLight != null)
            {
                dynamicLight = directLight;
            }
            else
            {
                var adapter = light as DynamicLightAdapter;
                if (adapter != null)
                {
                    dynamicLight = adapter.Light;
                }
            }

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

            DynamicLight dynamicLight = null;
            var directLight = light as DynamicLight;
            if (directLight != null)
            {
                dynamicLight = directLight;
            }
            else
            {
                var adapter = light as DynamicLightAdapter;
                if (adapter != null)
                {
                    dynamicLight = adapter.Light;
                }
            }

            if (dynamicLight != null)
            {
                _lights.Remove(dynamicLight);
                _lightMap.Remove(dynamicLight.LightId);

                // Remove from entity attachment tracking
                if (_lightEntityMap.TryGetValue(dynamicLight, out uint entityId))
                {
                    if (_entityLightMap.TryGetValue(entityId, out List<DynamicLight> entityLights))
                    {
                        entityLights.Remove(dynamicLight);
                        if (entityLights.Count == 0)
                        {
                            _entityLightMap.Remove(entityId);
                        }
                    }
                    _lightEntityMap.Remove(dynamicLight);
                }

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
        /// Attaches a light to an entity so it follows the entity's transform.
        /// </summary>
        /// <param name="light">The light to attach.</param>
        /// <param name="entityId">The entity object ID to attach to.</param>
        /// <remarks>
        /// Entity Light Attachment:
        /// - Lights attached to entities will be updated each frame via UpdateEntityLightTransforms()
        /// - Useful for torches, fires, and other dynamic lights on placeables/creatures
        /// - Based on Eclipse engine pattern: lights can be attached to entities in GIT file
        /// </remarks>
        public void AttachLightToEntity(IDynamicLight light, uint entityId)
        {
            if (light == null)
            {
                return;
            }

            var dynamicLight = light as DynamicLight;
            if (dynamicLight == null && light is DynamicLightAdapter adapter)
            {
                dynamicLight = adapter.Light;
            }

            if (dynamicLight == null || !_lights.Contains(dynamicLight))
            {
                Console.WriteLine("[EclipseLighting] Cannot attach light to entity - light not in system");
                return;
            }

            // Remove from previous entity if attached
            if (_lightEntityMap.TryGetValue(dynamicLight, out uint oldEntityId))
            {
                if (oldEntityId != entityId && _entityLightMap.TryGetValue(oldEntityId, out List<DynamicLight> oldEntityLights))
                {
                    oldEntityLights.Remove(dynamicLight);
                    if (oldEntityLights.Count == 0)
                    {
                        _entityLightMap.Remove(oldEntityId);
                    }
                }
            }

            // Add to new entity
            if (!_entityLightMap.TryGetValue(entityId, out List<DynamicLight> entityLights))
            {
                entityLights = new List<DynamicLight>();
                _entityLightMap[entityId] = entityLights;
            }

            if (!entityLights.Contains(dynamicLight))
            {
                entityLights.Add(dynamicLight);
            }

            _lightEntityMap[dynamicLight] = entityId;
        }

        /// <summary>
        /// Detaches a light from its entity.
        /// </summary>
        /// <param name="light">The light to detach.</param>
        public void DetachLightFromEntity(IDynamicLight light)
        {
            if (light == null)
            {
                return;
            }

            var dynamicLight = light as DynamicLight;
            if (dynamicLight == null && light is DynamicLightAdapter adapter)
            {
                dynamicLight = adapter.Light;
            }

            if (dynamicLight == null)
            {
                return;
            }

            if (_lightEntityMap.TryGetValue(dynamicLight, out uint entityId))
            {
                if (_entityLightMap.TryGetValue(entityId, out List<DynamicLight> entityLights))
                {
                    entityLights.Remove(dynamicLight);
                    if (entityLights.Count == 0)
                    {
                        _entityLightMap.Remove(entityId);
                    }
                }
                _lightEntityMap.Remove(dynamicLight);
            }
        }

        /// <summary>
        /// Gets or sets the time of day (0.0 = midnight, 0.5 = noon, 1.0 = midnight).
        /// </summary>
        public float TimeOfDay
        {
            get { return _timeOfDay; }
            set
            {
                _timeOfDay = value;
                // Wrap around
                if (_timeOfDay >= 1.0f) _timeOfDay -= 1.0f;
                if (_timeOfDay < 0.0f) _timeOfDay += 1.0f;
                // Update immediately
                UpdateDayNightCycle();
            }
        }

        /// <summary>
        /// Gets or sets whether the day/night cycle is enabled.
        /// </summary>
        public bool EnableDayNightCycle
        {
            get { return _enableDayNightCycle; }
            set { _enableDayNightCycle = value; }
        }

        /// <summary>
        /// Gets or sets the day/night cycle speed multiplier.
        /// </summary>
        public float DayNightCycleSpeed
        {
            get { return _dayNightCycleSpeed; }
            set { _dayNightCycleSpeed = value; }
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
        /// <param name="forceUpdate">If true, forces update even if clusters are not dirty. Should be true when called from pre-render to ensure clustering is current with view/projection matrices.</param>
        /// <remarks>
        /// Light clustering depends on view/projection matrices, so it should be updated every frame
        /// even if lights haven't changed, because the camera may have moved.
        /// Based on daorigins.exe/DragonAge2.exe: Light clustering is updated every frame during pre-render.
        /// </remarks>
        public void UpdateClustering(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix, bool forceUpdate = false)
        {
            if (!forceUpdate && !_clustersDirty)
            {
                return;
            }

            // Clear cluster data
            Array.Clear(_clusterLightCounts, 0, _clusterLightCounts.Length);
            Array.Clear(_clusterLightIndices, 0, _clusterLightIndices.Length);

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
            _buffersDirty = true;
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

            // Upload cluster assignment data to GPU buffers if graphics device is available
            if (_graphicsDevice != null && _buffersDirty)
            {
                UploadClusterDataToGpu();
                _buffersDirty = false;
            }
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
        /// <param name="fogSettings">Fog settings to apply.</param>
        public void SetFog(Andastra.Runtime.Games.Eclipse.Lighting.FogSettings fogSettings)
        {
            _fog = fogSettings;
        }

        /// <summary>
        /// Gets current fog settings.
        /// </summary>
        /// <returns>Current fog settings.</returns>
        public Andastra.Runtime.Games.Eclipse.Lighting.FogSettings GetFog()
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
            _buffersDirty = true;
        }

        /// <summary>
        /// Creates GPU buffers for cluster light data.
        /// </summary>
        private void CreateGpuBuffers()
        {
            if (_graphicsDevice == null)
            {
                return;
            }

            DisposeGpuBuffers();

            int totalClusters = ClusterCountX * ClusterCountY * ClusterCountZ;
            int maxLightIndices = totalClusters * MaxLightsPerCluster;

            // Create light index buffer (stores light indices per cluster)
            // Using uint (4 bytes) for light indices
            _lightIndexBuffer = new MonoGameGraphicsBuffer(
                _graphicsDevice,
                maxLightIndices,
                sizeof(uint),
                isDynamic: true
            );

            // Create light grid buffer (offset + count per cluster)
            int lightGridStride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(LightGridEntry));
            _lightGridBuffer = new MonoGameGraphicsBuffer(
                _graphicsDevice,
                totalClusters,
                lightGridStride,
                isDynamic: true
            );
        }

        /// <summary>
        /// Disposes GPU buffers.
        /// </summary>
        private void DisposeGpuBuffers()
        {
            _lightIndexBuffer?.Dispose();
            _lightIndexBuffer = null;

            _lightGridBuffer?.Dispose();
            _lightGridBuffer = null;
        }

        /// <summary>
        /// Uploads cluster assignment data to GPU buffers.
        /// Converts CPU cluster data (_clusterLightCounts, _clusterLightIndices) to GPU-friendly format.
        /// </summary>
        private void UploadClusterDataToGpu()
        {
            if (_graphicsDevice == null || _lightIndexBuffer == null || _lightGridBuffer == null)
            {
                return;
            }

            int totalClusters = ClusterCountX * ClusterCountY * ClusterCountZ;

            // Build light index buffer: convert int[] to uint[]
            uint[] lightIndices = new uint[_clusterLightIndices.Length];
            for (int i = 0; i < _clusterLightIndices.Length; i++)
            {
                lightIndices[i] = (uint)_clusterLightIndices[i];
            }
            _lightIndexBuffer.SetData(lightIndices);

            // Build light grid buffer: create LightGridEntry for each cluster
            LightGridEntry[] lightGrid = new LightGridEntry[totalClusters];
            uint currentOffset = 0;

            for (int i = 0; i < totalClusters; i++)
            {
                int count = _clusterLightCounts[i];
                lightGrid[i] = new LightGridEntry
                {
                    Offset = currentOffset,
                    Count = (uint)count
                };
                currentOffset += (uint)count;
            }
            _lightGridBuffer.SetData(lightGrid);
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
            _entityLightMap.Clear();
            _lightEntityMap.Clear();

            _primaryDirectional = null;
            _sunLight = null;
            _moonLight = null;

            // Release GPU buffers
            DisposeGpuBuffers();

            _disposed = true;
        }
    }

    /// <summary>
    /// Adapter to bridge MonoGame.DynamicLight to MonoGame.Interfaces.IDynamicLight interface.
    /// </summary>
    internal class DynamicLightAdapter : IDynamicLight
    {
        private readonly DynamicLight _light;
        private bool _disposed;

        public DynamicLightAdapter(DynamicLight light)
        {
            _light = light ?? throw new ArgumentNullException(nameof(light));
        }

        public uint LightId => _light.LightId;
        public LightType Type => _light.Type;
        public bool Enabled
        {
            get { return _light.Enabled; }
            set { _light.Enabled = value; }
        }
        public Vector3 Position
        {
            get { return _light.Position; }
            set { _light.Position = value; }
        }
        public Vector3 Direction
        {
            get { return _light.Direction; }
            set { _light.Direction = value; }
        }
        public Vector3 Color
        {
            get { return _light.Color; }
            set { _light.Color = value; }
        }
        public float Intensity
        {
            get { return _light.Intensity; }
            set { _light.Intensity = value; }
        }
        public float Radius
        {
            get { return _light.Radius; }
            set { _light.Radius = value; }
        }
        public float InnerConeAngle
        {
            get { return _light.InnerConeAngle; }
            set { _light.InnerConeAngle = value; }
        }
        public float OuterConeAngle
        {
            get { return _light.OuterConeAngle; }
            set { _light.OuterConeAngle = value; }
        }
        public float AreaWidth
        {
            get { return _light.AreaWidth; }
            set { _light.AreaWidth = value; }
        }
        public float AreaHeight
        {
            get { return _light.AreaHeight; }
            set { _light.AreaHeight = value; }
        }
        public bool CastShadows
        {
            get { return _light.CastShadows; }
            set { _light.CastShadows = value; }
        }
        public int ShadowResolution
        {
            get { return _light.ShadowResolution; }
            set { _light.ShadowResolution = value; }
        }
        public float ShadowBias
        {
            get { return _light.ShadowBias; }
            set { _light.ShadowBias = value; }
        }
        public float ShadowNormalBias
        {
            get { return _light.ShadowNormalBias; }
            set { _light.ShadowNormalBias = value; }
        }
        public float ShadowNearPlane
        {
            get { return _light.ShadowNearPlane; }
            set { _light.ShadowNearPlane = value; }
        }
        public float ShadowSoftness
        {
            get { return _light.ShadowSoftness; }
            set { _light.ShadowSoftness = value; }
        }
        public System.Numerics.Matrix4x4 ShadowLightSpaceMatrix
        {
            get { return _light.ShadowLightSpaceMatrix; }
            set { _light.ShadowLightSpaceMatrix = value; }
        }
        public bool RaytracedShadows
        {
            get { return _light.RaytracedShadows; }
            set { _light.RaytracedShadows = value; }
        }
        public bool Volumetric
        {
            get { return _light.Volumetric; }
            set { _light.Volumetric = value; }
        }
        public float VolumetricIntensity
        {
            get { return _light.VolumetricIntensity; }
            set { _light.VolumetricIntensity = value; }
        }
        public float Temperature
        {
            get { return _light.Temperature; }
            set { _light.Temperature = value; }
        }
        public bool UseTemperature
        {
            get { return _light.UseTemperature; }
            set { _light.UseTemperature = value; }
        }
        public IntPtr IesProfile
        {
            get { return _light.IesProfile; }
            set { _light.IesProfile = value; }
        }
        public IntPtr CookieTexture
        {
            get { return _light.CookieTexture; }
            set { _light.CookieTexture = value; }
        }
        public bool AffectsSpecular
        {
            get { return _light.AffectsSpecular; }
            set { _light.AffectsSpecular = value; }
        }
        public uint CullingMask
        {
            get { return _light.CullingMask; }
            set { _light.CullingMask = value; }
        }

        public void UpdateTransform(System.Numerics.Matrix4x4 worldMatrix)
        {
            _light.UpdateTransform(worldMatrix);
        }

        public void UpdateGpuData()
        {
            _light.UpdateGpuData();
        }

        /// <summary>
        /// Gets the underlying DynamicLight instance.
        /// </summary>
        public DynamicLight Light => _light;

        public void Dispose()
        {
            if (!_disposed)
            {
                _light.Dispose();
                _disposed = true;
            }
        }
    }
}
