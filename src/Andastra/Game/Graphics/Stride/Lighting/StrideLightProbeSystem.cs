using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Lighting;
using Andastra.Game.Stride.Spatial;
using BaseLightProbeSystem = Andastra.Runtime.Graphics.Common.Lighting.BaseLightProbeSystem;

namespace Andastra.Game.Stride.Lighting
{
    /// <summary>
    /// Stride-specific light probe structure.
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
    /// Stride implementation of light probe system for global illumination approximation.
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
    public class StrideLightProbeSystem : BaseLightProbeSystem
    {
        private readonly Octree<LightProbeWrapper> _probeOctree;

        /// <summary>
        /// Initializes a new light probe system with default world bounds.
        /// </summary>
        public StrideLightProbeSystem()
            : this(new Spatial.BoundingBox(new Vector3(-1000, -1000, -1000), new Vector3(1000, 1000, 1000)), 10.0f)
        {
        }

        /// <summary>
        /// Initializes a new light probe system with specified world bounds and search radius.
        /// </summary>
        /// <param name="worldBounds">Bounding box defining the world space for the octree.</param>
        /// <param name="defaultSearchRadius">Default search radius for probe queries.</param>
        public StrideLightProbeSystem(Spatial.BoundingBox worldBounds, float defaultSearchRadius)
            : base(ConvertBoundingBox(worldBounds), defaultSearchRadius)
        {
            // Initialize octree with reasonable defaults:
            // - Max depth of 8 levels (allows fine-grained spatial partitioning)
            // - Max 4 objects per node before splitting (balances memory vs query performance)
            _probeOctree = new Octree<LightProbeWrapper>(
                worldBounds,
                8,
                4,
                wrapper => ConvertBoundingBox(GetProbeBounds(wrapper.Probe))
            );
        }

        /// <summary>
        /// Converts base BoundingBox to Stride BoundingBox.
        /// </summary>
        private static Spatial.BoundingBox ConvertBoundingBox(BoundingBox bb)
        {
            return new Spatial.BoundingBox(bb.Min, bb.Max);
        }

        /// <summary>
        /// Converts Stride BoundingBox to base BoundingBox.
        /// </summary>
        private static BoundingBox ConvertBoundingBox(Spatial.BoundingBox bb)
        {
            return new BoundingBox(bb.Min, bb.Max);
        }

        /// <summary>
        /// Converts Stride LightProbe to base LightProbe.
        /// </summary>
        private static BaseLightProbeSystem.LightProbe ConvertProbe(LightProbe probe)
        {
            Vector3[] shCoeffs = null;
            if (probe.SHCoefficients != null)
            {
                shCoeffs = new Vector3[probe.SHCoefficients.Length];
                for (int i = 0; i < probe.SHCoefficients.Length; i++)
                {
                    shCoeffs[i] = probe.SHCoefficients[i];
                }
            }

            return new BaseLightProbeSystem.LightProbe
            {
                Position = probe.Position,
                SHCoefficients = shCoeffs,
                Radius = probe.Radius
            };
        }

        /// <summary>
        /// Converts base LightProbe to Stride LightProbe.
        /// </summary>
        private static LightProbe ConvertFromBaseProbe(BaseLightProbeSystem.LightProbe probe)
        {
            Vector3[] shCoeffs = null;
            if (probe.SHCoefficients != null)
            {
                shCoeffs = new Vector3[probe.SHCoefficients.Length];
                for (int i = 0; i < probe.SHCoefficients.Length; i++)
                {
                    shCoeffs[i] = probe.SHCoefficients[i];
                }
            }

            return new LightProbe
            {
                Position = probe.Position,
                SHCoefficients = shCoeffs,
                Radius = probe.Radius
            };
        }

        /// <summary>
        /// Inserts a probe wrapper into the octree.
        /// </summary>
        protected override void InsertIntoOctree(LightProbeWrapper wrapper)
        {
            _probeOctree.Insert(wrapper);
        }

        /// <summary>
        /// Queries the octree for probes within the specified bounds.
        /// </summary>
        protected override void QueryOctree(BoundingBox bounds, List<LightProbeWrapper> results)
        {
            Spatial.BoundingBox strideBounds = ConvertBoundingBox(bounds);
            _probeOctree.Query(strideBounds, results);
        }

        /// <summary>
        /// Clears the octree.
        /// </summary>
        protected override void ClearOctree()
        {
            _probeOctree.Clear();
        }

        /// <summary>
        /// Adds a light probe to the system (Stride LightProbe overload).
        /// </summary>
        /// <param name="probe">The light probe to add.</param>
        public void AddProbe(LightProbe probe)
        {
            base.AddProbe(ConvertProbe(probe));
        }

        /// <summary>
        /// Removes a light probe from the system (Stride LightProbe overload).
        /// </summary>
        /// <param name="probe">The light probe to remove.</param>
        /// <returns>True if the probe was found and removed, false otherwise.</returns>
        public bool RemoveProbe(LightProbe probe)
        {
            return base.RemoveProbe(ConvertProbe(probe));
        }

        /// <summary>
        /// Updates an existing light probe in the system (Stride LightProbe overload).
        /// </summary>
        /// <param name="oldProbe">The existing probe to update.</param>
        /// <param name="newProbe">The updated probe data.</param>
        /// <returns>True if the probe was found and updated, false otherwise.</returns>
        public bool UpdateProbe(LightProbe oldProbe, LightProbe newProbe)
        {
            return base.UpdateProbe(ConvertProbe(oldProbe), ConvertProbe(newProbe));
        }

        /// <summary>
        /// Samples light probes at a position using the default search radius (Stride Vector3 overload).
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <returns>Interpolated ambient light color at the position.</returns>
        public Vector3 SampleAmbientLight(Vector3 position)
        {
            return base.SampleAmbientLight(position);
        }

        /// <summary>
        /// Samples light probes at a position using a specified search radius (Stride Vector3 overload).
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <param name="searchRadius">Search radius for finding nearby probes.</param>
        /// <returns>Interpolated ambient light color at the position.</returns>
        public Vector3 SampleAmbientLight(Vector3 position, float searchRadius)
        {
            return base.SampleAmbientLight(position, searchRadius);
        }

        /// <summary>
        /// Samples light probes at a position with a specific direction (Stride Vector3 overload).
        /// </summary>
        /// <param name="position">World space position to sample at.</param>
        /// <param name="direction">Direction vector for spherical harmonics evaluation (should be normalized).</param>
        /// <param name="searchRadius">Search radius for finding nearby probes.</param>
        /// <returns>Interpolated ambient light color at the position in the specified direction.</returns>
        public Vector3 SampleAmbientLight(Vector3 position, Vector3 direction, float searchRadius)
        {
            return base.SampleAmbientLight(position, direction, searchRadius);
        }

        /// <summary>
        /// Queries all light probes within a bounding box (Stride BoundingBox overload).
        /// </summary>
        /// <param name="bounds">Bounding box to query.</param>
        /// <param name="results">List to populate with probes within the bounds.</param>
        public void QueryProbes(Spatial.BoundingBox bounds, List<LightProbe> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            List<BaseLightProbeSystem.LightProbe> baseResults = new List<BaseLightProbeSystem.LightProbe>();
            base.QueryProbes(ConvertBoundingBox(bounds), baseResults);

            foreach (BaseLightProbeSystem.LightProbe probe in baseResults)
            {
                results.Add(ConvertProbe(probe));
            }
        }

        /// <summary>
        /// Gets all light probes in the system (Stride LightProbe version).
        /// </summary>
        /// <returns>Read-only list of all light probes.</returns>
        public new IReadOnlyList<LightProbe> GetAllProbes()
        {
            List<LightProbe> results = new List<LightProbe>();
            IReadOnlyList<BaseLightProbeSystem.LightProbe> baseProbes = base.GetAllProbes();
            foreach (BaseLightProbeSystem.LightProbe probe in baseProbes)
            {
                results.Add(ConvertProbe(probe));
            }
            return results.AsReadOnly();
        }
    }
}

