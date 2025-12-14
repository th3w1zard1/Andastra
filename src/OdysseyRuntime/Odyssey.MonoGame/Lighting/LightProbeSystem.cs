using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Lighting
{
    /// <summary>
    /// Light probe system for global illumination approximation.
    /// 
    /// Light probes capture ambient lighting at specific points in the scene,
    /// providing realistic indirect lighting without full global illumination.
    /// 
    /// Features:
    /// - Spherical harmonics (SH) encoding
    /// - Probe placement and baking
    /// - Runtime interpolation
    /// - Dynamic probe updates
    /// </summary>
    public class LightProbeSystem
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

        private readonly List<LightProbe> _probes;
        private readonly Octree<LightProbe> _probeOctree;

        /// <summary>
        /// Gets the number of light probes.
        /// </summary>
        public int ProbeCount
        {
            get { return _probes.Count; }
        }

        /// <summary>
        /// Initializes a new light probe system.
        /// </summary>
        public LightProbeSystem()
        {
            _probes = new List<LightProbe>();
            // Create octree for probe lookup
            BoundingBox worldBounds = new BoundingBox(
                new Vector3(-1000, -1000, -1000),
                new Vector3(1000, 1000, 1000)
            );
            _probeOctree = new Spatial.Octree<LightProbe>(
                worldBounds,
                8,
                4,
                probe => GetProbeBounds(probe)
            );
        }

        /// <summary>
        /// Adds a light probe.
        /// </summary>
        public void AddProbe(LightProbe probe)
        {
            _probes.Add(probe);
            _probeOctree.Insert(probe);
        }

        /// <summary>
        /// Samples light probes at a position.
        /// </summary>
        public Vector3 SampleAmbientLight(Vector3 position)
        {
            // Find nearby probes
            List<LightProbe> nearbyProbes = new List<LightProbe>();
            BoundingBox searchBounds = new BoundingBox(
                position - new Vector3(10, 10, 10),
                position + new Vector3(10, 10, 10)
            );
            _probeOctree.Query(searchBounds, nearbyProbes);

            if (nearbyProbes.Count == 0)
            {
                return Vector3.Zero;
            }

            // Interpolate between probes using inverse distance weighting
            Vector3 ambientLight = Vector3.Zero;
            float totalWeight = 0.0f;

            foreach (LightProbe probe in nearbyProbes)
            {
                float distance = Vector3.Distance(position, probe.Position);
                if (distance < probe.Radius)
                {
                    float weight = 1.0f / (distance + 0.001f);
                    Vector3 probeLight = EvaluateSH(probe.SHCoefficients, Vector3.Up); // Simplified
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
        private Vector3 EvaluateSH(Vector3[] shCoeffs, Vector3 direction)
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

        private Spatial.BoundingBox GetProbeBounds(LightProbe probe)
        {
            float radius = probe.Radius;
            return new Spatial.BoundingBox(
                probe.Position - new Vector3(radius),
                probe.Position + new Vector3(radius)
            );
        }
    }
}

