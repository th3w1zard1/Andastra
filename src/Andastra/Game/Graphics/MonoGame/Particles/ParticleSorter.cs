using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.MonoGame.Particles
{
    /// <summary>
    /// Particle sorting system for proper alpha blending.
    /// 
    /// Particles must be sorted back-to-front for correct alpha blending,
    /// ensuring transparent particles render in the correct order.
    /// 
    /// Features:
    /// - Distance-based sorting
    /// - View-space sorting
    /// - Efficient sorting algorithms
    /// - Batch sorting for performance
    /// </summary>
    public class ParticleSorter
    {
        /// <summary>
        /// Particle data for sorting.
        /// </summary>
        public struct ParticleSortData
        {
            /// <summary>
            /// Particle position in world space.
            /// </summary>
            public Vector3 Position;

            /// <summary>
            /// Particle index in particle buffer.
            /// </summary>
            public int ParticleIndex;

            /// <summary>
            /// Distance from camera (for sorting).
            /// </summary>
            public float Distance;
        }

        private readonly List<ParticleSortData> _sortBuffer;
        private Vector3 _cameraPosition;

        /// <summary>
        /// Initializes a new particle sorter.
        /// </summary>
        public ParticleSorter()
        {
            _sortBuffer = new List<ParticleSortData>();
        }

        /// <summary>
        /// Sets camera position for distance calculation.
        /// </summary>
        public void SetCameraPosition(Vector3 position)
        {
            _cameraPosition = position;
        }

        /// <summary>
        /// Sorts particles by distance from camera (back-to-front).
        /// </summary>
        public void SortParticles(List<ParticleSortData> particles)
        {
            if (particles == null || particles.Count == 0)
            {
                return;
            }

            // Calculate distances
            for (int i = 0; i < particles.Count; i++)
            {
                ParticleSortData data = particles[i];
                data.Distance = Vector3.Distance(_cameraPosition, data.Position);
                particles[i] = data;
            }

            // Sort by distance (back-to-front for alpha blending)
            particles.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        }

        /// <summary>
        /// Sorts particles using view-space Z (more accurate).
        /// </summary>
        public void SortParticlesViewSpace(List<ParticleSortData> particles, Matrix viewMatrix)
        {
            if (particles == null || particles.Count == 0)
            {
                return;
            }

            // Transform to view space and use Z for sorting
            for (int i = 0; i < particles.Count; i++)
            {
                ParticleSortData data = particles[i];
                Vector4 viewPos = Vector4.Transform(new Vector4(data.Position, 1.0f), viewMatrix);
                data.Distance = viewPos.Z; // Use view-space Z
                particles[i] = data;
            }

            // Sort by view-space Z (back-to-front)
            particles.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        }
    }
}

