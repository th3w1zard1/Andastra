using System;
using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Runtime.Graphics.Common.Lighting
{
    /// <summary>
    /// Base class for light probe systems providing common functionality.
    ///
    /// Light probes capture ambient lighting at specific points in the scene,
    /// providing realistic indirect lighting without full global illumination.
    ///
    /// Features:
    /// - Spherical harmonics (SH) encoding
    /// - Probe placement and baking
    /// - Runtime interpolation
    /// - Dynamic probe updates
    /// - Octree-based spatial acceleration for efficient probe queries
    /// </summary>
    public abstract class BaseLightProbeSystem
    {
        /// <summary>
        /// Light probe data with spherical harmonics.
        /// </summary>
        public struct LightProbe
        {
            /// <summary>
            /// Probe position.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Spherical harmonics coefficients (9 coefficients for L2 SH).
            /// </summary>
            public Vector3[] SHCoefficients;

            /// <summary>
            /// Probe influence radius.
            /// </summary>
            public float Radius;
        }

        /// <summary>
        /// Bounding box structure for spatial queries.
        /// </summary>
        public struct BoundingBox
        {
            public Vector3 Min;
            public Vector3 Max;

            public BoundingBox(Vector3 min, Vector3 max)
            {
                Min = min;
                Max = max;
            }

            public Vector3 GetCenter()
            {
                return (Min + Max) * 0.5f;
            }

            public Vector3 GetSize()
            {
                return Max - Min;
            }

            public bool Intersects(Vector3 otherMin, Vector3 otherMax)
            {
                return Min.X <= otherMax.X && Max.X >= otherMin.X &&
                       Min.Y <= otherMax.Y && Max.Y >= otherMin.Y &&
                       Min.Z <= otherMax.Z && Max.Z >= otherMin.Z;
            }
        }

        /// <summary>
        /// Wrapper class for LightProbe to enable octree storage (octree requires class types).
        /// </summary>
        protected class LightProbeWrapper
        {
            public LightProbe Probe;

            public LightProbeWrapper(LightProbe probe)
            {
                Probe = probe;
            }
        }

        protected readonly List<LightProbe> _probes;
        protected readonly Dictionary<LightProbe, LightProbeWrapper> _probeToWrapper;
        protected readonly float _defaultSearchRadius;
        protected readonly BoundingBox _worldBounds;

        /// <summary>
        /// Gets the number of light probes.
        /// </summary>
        public int ProbeCount
        {
            get { return _probes.Count; }
        }

        /// <summary>
        /// Gets the default search radius for probe queries.
        /// </summary>
        public float DefaultSearchRadius
        {
            get { return _defaultSearchRadius; }
        }

        /// <summary>
        /// Initializes a new light probe system with specified world bounds and search radius.
        /// </summary>
        /// <param name="worldBounds">Bounding box defining the world space for the octree.</param>
        /// <param name="defaultSearchRadius">Default search radius for probe queries.</param>
        protected BaseLightProbeSystem(BoundingBox worldBounds, float defaultSearchRadius)
        {
            if (defaultSearchRadius <= 0.0f)
            {
                throw new ArgumentException("Default search radius must be positive.", nameof(defaultSearchRadius));
            }

            _probes = new List<LightProbe>();
            _probeToWrapper = new Dictionary<LightProbe, LightProbeWrapper>();
            _worldBounds = worldBounds;
            _defaultSearchRadius = defaultSearchRadius;
        }

        /// <summary>
        /// Adds a light probe to the system.
        /// </summary>
        /// <param name="probe">The light probe to add.</param>
        public void AddProbe(LightProbe probe)
        {
            _probes.Add(probe);
            LightProbeWrapper wrapper = new LightProbeWrapper(probe);
            _probeToWrapper[probe] = wrapper;
            InsertIntoOctree(wrapper);
        }

        /// <summary>
        /// Removes a light probe from the system.
        /// </summary>
        /// <param name="probe">The light probe to remove.</param>
        /// <returns>True if the probe was found and removed, false otherwise.</returns>
        public bool RemoveProbe(LightProbe probe)
        {
            if (!_probeToWrapper.TryGetValue(probe, out LightProbeWrapper wrapper))
            {
                return false;
            }

            // Note: Octree doesn't have a Remove method, so we need to rebuild it
            // This is acceptable for light probes as they are typically added during initialization
            // and rarely removed at runtime. For frequent removals, consider implementing Remove in Octree.
            _probes.Remove(probe);
            _probeToWrapper.Remove(probe);

            // Rebuild octree without the removed probe
            ClearOctree();
            foreach (LightProbe remainingProbe in _probes)
            {
                LightProbeWrapper remainingWrapper = new LightProbeWrapper(remainingProbe);
                _probeToWrapper[remainingProbe] = remainingWrapper;
                InsertIntoOctree(remainingWrapper);
            }

            return true;
        }

        /// <summary>
        /// Updates an existing light probe in the system.
        /// </summary>
        /// <param name="oldProbe">The existing probe to update.</param>
        /// <param name="newProbe">The updated probe data.</param>
        /// <returns>True if the probe was found and updated, false otherwise.</returns>
        public bool UpdateProbe(LightProbe oldProbe, LightProbe newProbe)
        {
            if (!RemoveProbe(oldProbe))
            {
                return false;
            }

            AddProbe(newProbe);
            return true;
        }

        /// <summary>
        /// Clears all light probes from the system.
        /// </summary>
        public void Clear()
        {
            _probes.Clear();
            _probeToWrapper.Clear();
            ClearOctree();
        }

        /// <summary>
        /// Samples light probes at a position using the default search radius.
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <returns>Interpolated ambient light color at the position.</returns>
        public Vector3 SampleAmbientLight(Vector3 position)
        {
            return SampleAmbientLight(position, _defaultSearchRadius);
        }

        /// <summary>
        /// Samples light probes at a position using a specified search radius.
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <param name="searchRadius">Search radius for finding nearby probes.</param>
        /// <returns>Interpolated ambient light color at the position.</returns>
        public Vector3 SampleAmbientLight(Vector3 position, float searchRadius)
        {
            if (searchRadius <= 0.0f)
            {
                throw new ArgumentException("Search radius must be positive.", nameof(searchRadius));
            }

            // Find nearby probes using octree spatial query
            List<LightProbeWrapper> nearbyWrappers = new List<LightProbeWrapper>();
            BoundingBox searchBounds = new BoundingBox(
                position - new Vector3(searchRadius),
                position + new Vector3(searchRadius)
            );
            QueryOctree(searchBounds, nearbyWrappers);

            if (nearbyWrappers.Count == 0)
            {
                return Vector3.Zero;
            }

            // Interpolate between probes using inverse distance weighting
            Vector3 ambientLight = Vector3.Zero;
            float totalWeight = 0.0f;

            foreach (LightProbeWrapper wrapper in nearbyWrappers)
            {
                LightProbe probe = wrapper.Probe;
                float distance = Vector3.Distance(position, probe.Position);

                // Only consider probes within their influence radius
                if (distance < probe.Radius)
                {
                    // Use inverse distance weighting with small epsilon to avoid division by zero
                    float weight = 1.0f / (distance + 0.001f);
                    Vector3 probeLight = EvaluateSH(probe.SHCoefficients, Vector3.UnitY); // Simplified - uses up direction
                    ambientLight += probeLight * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight > 0.0f)
            {
                ambientLight /= totalWeight;
            }

            return ambientLight;
        }

        /// <summary>
        /// Samples light probes at a position with a specific direction for spherical harmonics evaluation.
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <param name="direction">Direction vector for spherical harmonics evaluation (should be normalized).</param>
        /// <param name="searchRadius">Search radius for finding nearby probes.</param>
        /// <returns>Interpolated ambient light color at the position in the specified direction.</returns>
        public Vector3 SampleAmbientLight(Vector3 position, Vector3 direction, float searchRadius)
        {
            if (searchRadius <= 0.0f)
            {
                throw new ArgumentException("Search radius must be positive.", nameof(searchRadius));
            }

            // Find nearby probes using octree spatial query
            List<LightProbeWrapper> nearbyWrappers = new List<LightProbeWrapper>();
            BoundingBox searchBounds = new BoundingBox(
                position - new Vector3(searchRadius),
                position + new Vector3(searchRadius)
            );
            QueryOctree(searchBounds, nearbyWrappers);

            if (nearbyWrappers.Count == 0)
            {
                return Vector3.Zero;
            }

            // Interpolate between probes using inverse distance weighting
            Vector3 ambientLight = Vector3.Zero;
            float totalWeight = 0.0f;

            foreach (LightProbeWrapper wrapper in nearbyWrappers)
            {
                LightProbe probe = wrapper.Probe;
                float distance = Vector3.Distance(position, probe.Position);

                // Only consider probes within their influence radius
                if (distance < probe.Radius)
                {
                    // Use inverse distance weighting with small epsilon to avoid division by zero
                    float weight = 1.0f / (distance + 0.001f);
                    Vector3 probeLight = EvaluateSH(probe.SHCoefficients, direction);
                    ambientLight += probeLight * weight;
                    totalWeight += weight;
                }
            }

            if (totalWeight > 0.0f)
            {
                ambientLight /= totalWeight;
            }

            return ambientLight;
        }

        /// <summary>
        /// Evaluates spherical harmonics at a direction.
        /// </summary>
        /// <param name="shCoeffs">Spherical harmonics coefficients.</param>
        /// <param name="direction">Direction vector (should be normalized).</param>
        /// <returns>Evaluated light color.</returns>
        protected Vector3 EvaluateSH(Vector3[] shCoeffs, Vector3 direction)
        {
            if (shCoeffs == null || shCoeffs.Length < 9)
            {
                return Vector3.Zero;
            }

            // Evaluate L2 spherical harmonics basis functions
            float x = direction.X;
            float y = direction.Y;
            float z = direction.Z;

            // L0
            float sh0 = 0.282095f;

            // L1
            float sh1 = 0.488603f * y;
            float sh2 = 0.488603f * z;
            float sh3 = 0.488603f * x;

            // L2
            float sh4 = 1.092548f * x * y;
            float sh5 = 1.092548f * y * z;
            float sh6 = 0.315392f * (3.0f * z * z - 1.0f);
            float sh7 = 1.092548f * x * z;
            float sh8 = 0.546274f * (x * x - y * y);

            // Evaluate SH
            Vector3 result = shCoeffs[0] * sh0 +
                           shCoeffs[1] * sh1 +
                           shCoeffs[2] * sh2 +
                           shCoeffs[3] * sh3 +
                           shCoeffs[4] * sh4 +
                           shCoeffs[5] * sh5 +
                           shCoeffs[6] * sh6 +
                           shCoeffs[7] * sh7 +
                           shCoeffs[8] * sh8;

            return result;
        }

        /// <summary>
        /// Gets the bounding box for a light probe based on its position and radius.
        /// </summary>
        /// <param name="probe">The light probe.</param>
        /// <returns>Bounding box encompassing the probe's influence area.</returns>
        protected BoundingBox GetProbeBounds(LightProbe probe)
        {
            float radius = probe.Radius;
            return new BoundingBox(
                probe.Position - new Vector3(radius),
                probe.Position + new Vector3(radius)
            );
        }

        /// <summary>
        /// Queries all light probes within a bounding box.
        /// </summary>
        /// <param name="bounds">Bounding box to query.</param>
        /// <param name="results">List to populate with probes within the bounds.</param>
        public void QueryProbes(BoundingBox bounds, List<LightProbe> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            List<LightProbeWrapper> wrappers = new List<LightProbeWrapper>();
            QueryOctree(bounds, wrappers);

            foreach (LightProbeWrapper wrapper in wrappers)
            {
                results.Add(wrapper.Probe);
            }
        }

        /// <summary>
        /// Gets all light probes in the system.
        /// </summary>
        /// <returns>Read-only list of all light probes.</returns>
        public IReadOnlyList<LightProbe> GetAllProbes()
        {
            return _probes.AsReadOnly();
        }

        /// <summary>
        /// Inserts a probe wrapper into the octree. Must be implemented by derived classes.
        /// </summary>
        /// <param name="wrapper">The probe wrapper to insert.</param>
        protected abstract void InsertIntoOctree(LightProbeWrapper wrapper);

        /// <summary>
        /// Queries the octree for probes within the specified bounds. Must be implemented by derived classes.
        /// </summary>
        /// <param name="bounds">Bounding box to query.</param>
        /// <param name="results">List to populate with matching probe wrappers.</param>
        protected abstract void QueryOctree(BoundingBox bounds, List<LightProbeWrapper> results);

        /// <summary>
        /// Clears the octree. Must be implemented by derived classes.
        /// </summary>
        protected abstract void ClearOctree();
    }
}

