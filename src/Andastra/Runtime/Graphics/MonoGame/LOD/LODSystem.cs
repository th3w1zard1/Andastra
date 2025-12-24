using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.MonoGame.LOD
{
    /// <summary>
    /// Level of Detail (LOD) system for managing mesh detail levels.
    /// 
    /// LOD reduces polygon count for distant objects, improving performance
    /// while maintaining visual quality. Different LOD levels are used based
    /// on distance from camera.
    /// </summary>
    /// <remarks>
    /// LOD System (Based on Original Engine):
    /// - Based on swkotor2.exe rendering system architecture
    /// - Located via string references: Original engine uses distance-based LOD for models
    /// - Original implementation: KOTOR performs distance culling and LOD selection for rendering optimization
    /// - Distance-based rendering: Objects beyond certain distance thresholds are rendered at lower detail or culled
    /// - Original behavior: MDL models may have multiple LOD meshes, engine selects based on camera distance
    /// - Modern enhancement: This system provides configurable LOD thresholds and bias for flexible LOD control
    /// - Original engine: Fixed LOD distances based on model type, no user-configurable bias
    /// </remarks>
    public class LODSystem
    {
        /// <summary>
        /// LOD level enumeration.
        /// </summary>
        public enum LODLevel
        {
            /// <summary>
            /// Highest detail - full polygon count.
            /// </summary>
            LOD0 = 0,

            /// <summary>
            /// Medium detail - reduced polygons.
            /// </summary>
            LOD1 = 1,

            /// <summary>
            /// Low detail - minimal polygons.
            /// </summary>
            LOD2 = 2,

            /// <summary>
            /// Billboard or impostor - single quad with texture.
            /// </summary>
            Billboard = 3
        }

        /// <summary>
        /// LOD configuration for a single mesh group.
        /// </summary>
        public struct LODConfig
        {
            /// <summary>
            /// Distance thresholds for each LOD level (in world units).
            /// </summary>
            public float[] DistanceThresholds;

            /// <summary>
            /// Bias factor for LOD selection (-1 to 1, 0 = default).
            /// Positive values prefer higher detail, negative prefer lower.
            /// </summary>
            public float Bias;

            /// <summary>
            /// Whether to use screen-space size for LOD selection.
            /// </summary>
            public bool UseScreenSpaceSize;

            /// <summary>
            /// Minimum screen-space size (pixels) before switching to billboard.
            /// </summary>
            public float MinScreenSize;
        }

        private readonly Dictionary<string, LODConfig> _meshConfigs;
        private LODConfig _defaultConfig;

        /// <summary>
        /// Gets or sets the default LOD configuration.
        /// </summary>
        public LODConfig DefaultConfig
        {
            get { return _defaultConfig; }
            set { _defaultConfig = value; }
        }

        /// <summary>
        /// Initializes a new LOD system.
        /// </summary>
        public LODSystem()
        {
            _meshConfigs = new Dictionary<string, LODConfig>();

            // Default configuration
            _defaultConfig = new LODConfig
            {
                DistanceThresholds = new float[] { 0.0f, 50.0f, 150.0f, 300.0f },
                Bias = 0.0f,
                UseScreenSpaceSize = false,
                MinScreenSize = 16.0f
            };
        }

        /// <summary>
        /// Selects the appropriate LOD level for a mesh based on distance.
        /// </summary>
        /// <param name="meshName">Name of the mesh.</param>
        /// <param name="distance">Distance from camera to object center.</param>
        /// <param name="screenSize">Screen-space size in pixels (optional).</param>
        /// <returns>Selected LOD level.</returns>
        public LODLevel SelectLOD(string meshName, float distance, float? screenSize = null)
        {
            LODConfig config = GetConfig(meshName);

            // Screen-space based LOD selection
            if (config.UseScreenSpaceSize && screenSize.HasValue)
            {
                if (screenSize.Value < config.MinScreenSize)
                {
                    return LODLevel.Billboard;
                }
                // Use screen size thresholds (would need to be converted from distance thresholds)
            }

            // Distance-based LOD selection
            float adjustedDistance = distance * (1.0f + config.Bias);

            if (config.DistanceThresholds.Length > 3 && adjustedDistance >= config.DistanceThresholds[3])
            {
                return LODLevel.Billboard;
            }
            if (config.DistanceThresholds.Length > 2 && adjustedDistance >= config.DistanceThresholds[2])
            {
                return LODLevel.LOD2;
            }
            if (config.DistanceThresholds.Length > 1 && adjustedDistance >= config.DistanceThresholds[1])
            {
                return LODLevel.LOD1;
            }
            return LODLevel.LOD0;
        }

        /// <summary>
        /// Calculates screen-space size for an object.
        /// </summary>
        /// <param name="worldPosition">World position of object.</param>
        /// <param name="boundingRadius">Bounding sphere radius.</param>
        /// <param name="viewMatrix">View matrix.</param>
        /// <param name="projectionMatrix">Projection matrix.</param>
        /// <param name="viewportWidth">Viewport width in pixels.</param>
        /// <param name="viewportHeight">Viewport height in pixels.</param>
        /// <returns>Screen-space size in pixels.</returns>
        public float CalculateScreenSpaceSize(
            System.Numerics.Vector3 worldPosition,
            float boundingRadius,
            Matrix viewMatrix,
            Matrix projectionMatrix,
            int viewportWidth,
            int viewportHeight)
        {
            // Transform position to view space
            Microsoft.Xna.Framework.Vector4 viewPos = Microsoft.Xna.Framework.Vector4.Transform(new Microsoft.Xna.Framework.Vector4(worldPosition, 1.0f), viewMatrix);

            // Project to clip space
            Microsoft.Xna.Framework.Vector4 clipPos = Microsoft.Xna.Framework.Vector4.Transform(viewPos, projectionMatrix);

            // Perspective divide
            if (Math.Abs(clipPos.W) > 1e-6f)
            {
                clipPos.X /= clipPos.W;
                clipPos.Y /= clipPos.W;
                clipPos.Z /= clipPos.W;
            }

            // Convert to screen space
            float screenX = (clipPos.X * 0.5f + 0.5f) * viewportWidth;
            float screenY = (1.0f - (clipPos.Y * 0.5f + 0.5f)) * viewportHeight;

            // Calculate screen-space radius
            // Approximate by projecting the sphere radius
            float distance = viewPos.Length();
            if (distance > 1e-6f)
            {
                float fov = projectionMatrix.M22; // Extract FOV from projection
                float screenRadius = (boundingRadius / distance) * (viewportHeight / (2.0f * fov));
                return screenRadius * 2.0f; // Diameter
            }

            return 0.0f;
        }

        /// <summary>
        /// Sets LOD configuration for a specific mesh.
        /// </summary>
        public void SetMeshConfig(string meshName, LODConfig config)
        {
            _meshConfigs[meshName] = config;
        }

        /// <summary>
        /// Gets LOD configuration for a mesh (or default if not found).
        /// </summary>
        public LODConfig GetConfig(string meshName)
        {
            LODConfig config;
            if (_meshConfigs.TryGetValue(meshName, out config))
            {
                return config;
            }
            return _defaultConfig;
        }

        /// <summary>
        /// Clears all mesh-specific configurations.
        /// </summary>
        public void ClearMeshConfigs()
        {
            _meshConfigs.Clear();
        }
    }

    /// <summary>
    /// LOD statistics for performance monitoring.
    /// </summary>
    public class LODStats
    {
        /// <summary>
        /// Objects rendered at each LOD level.
        /// </summary>
        public int[] ObjectsPerLevel { get; private set; }

        /// <summary>
        /// Total objects rendered.
        /// </summary>
        public int TotalObjects { get; private set; }

        public LODStats()
        {
            ObjectsPerLevel = new int[4]; // LOD0, LOD1, LOD2, Billboard
        }

        /// <summary>
        /// Records an object rendered at a specific LOD level.
        /// </summary>
        public void RecordObject(LODSystem.LODLevel level)
        {
            if ((int)level >= 0 && (int)level < ObjectsPerLevel.Length)
            {
                ObjectsPerLevel[(int)level]++;
            }
            TotalObjects++;
        }

        /// <summary>
        /// Resets statistics for a new frame.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < ObjectsPerLevel.Length; i++)
            {
                ObjectsPerLevel[i] = 0;
            }
            TotalObjects = 0;
        }

        /// <summary>
        /// Gets the percentage of objects at each LOD level.
        /// </summary>
        public float GetPercentage(LODSystem.LODLevel level)
        {
            if (TotalObjects == 0)
            {
                return 0.0f;
            }
            int index = (int)level;
            if (index >= 0 && index < ObjectsPerLevel.Length)
            {
                return (ObjectsPerLevel[index] / (float)TotalObjects) * 100.0f;
            }
            return 0.0f;
        }
    }
}

