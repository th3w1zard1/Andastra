using System;
using System.Collections.Generic;
using System.Numerics;

namespace Odyssey.MonoGame.Culling
{
    /// <summary>
    /// Distance-based culling system.
    /// 
    /// Culls objects beyond a maximum render distance, reducing both CPU
    /// and GPU work for distant geometry that would be too small to see.
    /// </summary>
    /// <remarks>
    /// Distance Culling System:
    /// - Based on swkotor2.exe rendering optimization system
    /// - Located via string references: Distance-based rendering optimizations
    /// - Original implementation: KOTOR culls distant objects to maintain performance
    /// - Distance thresholds: Per-object-type maximum render distances
    /// - Used for: Reducing render load for distant geometry that would be imperceptible
    /// - Combined with frustum culling, VIS-based room culling, and occlusion culling
    /// - Default max distance: 1000 units (configurable per object type)
    /// </remarks>
    public class DistanceCuller
    {
        private readonly Dictionary<string, float> _objectMaxDistances;
        private float _defaultMaxDistance;

        /// <summary>
        /// Gets or sets the default maximum render distance.
        /// </summary>
        public float DefaultMaxDistance
        {
            get { return _defaultMaxDistance; }
            set { _defaultMaxDistance = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Initializes a new distance culler.
        /// </summary>
        /// <param name="defaultMaxDistance">Default maximum render distance.</param>
        public DistanceCuller(float defaultMaxDistance = 1000.0f)
        {
            _objectMaxDistances = new Dictionary<string, float>();
            _defaultMaxDistance = defaultMaxDistance;
        }

        /// <summary>
        /// Tests if an object should be culled based on distance.
        /// </summary>
        /// <param name="objectType">Type identifier of the object.</param>
        /// <param name="distance">Distance from camera to object.</param>
        /// <returns>True if object should be culled (too far away).</returns>
        public bool ShouldCull(string objectType, float distance)
        {
            float maxDistance = GetMaxDistance(objectType);
            return distance > maxDistance;
        }

        /// <summary>
        /// Gets the maximum render distance for an object type.
        /// </summary>
        public float GetMaxDistance(string objectType)
        {
            float maxDistance;
            if (_objectMaxDistances.TryGetValue(objectType, out maxDistance))
            {
                return maxDistance;
            }
            return _defaultMaxDistance;
        }

        /// <summary>
        /// Sets the maximum render distance for an object type.
        /// </summary>
        public void SetMaxDistance(string objectType, float maxDistance)
        {
            _objectMaxDistances[objectType] = Math.Max(0.0f, maxDistance);
        }
    }
}

