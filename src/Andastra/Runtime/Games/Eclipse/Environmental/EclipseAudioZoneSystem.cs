using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Environmental
{
    /// <summary>
    /// Eclipse engine audio zone system implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Audio Zone System Implementation:
    /// - Based on daorigins.exe: Audio zone initialization and management
    /// - Based on DragonAge2.exe: Enhanced audio zones with reverb and environmental effects
    /// - Audio zones define 3D spatial audio regions with specific acoustic properties
    /// - Zones can have reverb, echo, and other environmental audio effects
    /// - Audio zones affect sounds played within their boundaries
    /// </remarks>
    [PublicAPI]
    public class EclipseAudioZoneSystem : IAudioZoneSystem
    {
        private readonly List<IAudioZone> _zones;

        /// <summary>
        /// Creates a new Eclipse audio zone system.
        /// </summary>
        /// <remarks>
        /// Initializes audio zone system with empty zone list.
        /// Based on audio zone system initialization in daorigins.exe, DragonAge2.exe.
        /// </remarks>
        public EclipseAudioZoneSystem()
        {
            _zones = new List<IAudioZone>();
        }

        /// <summary>
        /// Gets all active audio zones.
        /// </summary>
        public IEnumerable<IAudioZone> Zones => _zones;

        /// <summary>
        /// Creates a new audio zone.
        /// </summary>
        /// <param name="center">The zone center position.</param>
        /// <param name="radius">The zone radius.</param>
        /// <param name="reverbType">The reverb type for this zone.</param>
        /// <returns>The created audio zone.</returns>
        /// <remarks>
        /// Based on audio zone creation in daorigins.exe, DragonAge2.exe.
        /// Creates and adds a new zone to the system.
        /// </remarks>
        public IAudioZone CreateZone(Vector3 center, float radius, ReverbType reverbType)
        {
            var zone = new EclipseAudioZone(center, radius, reverbType);
            _zones.Add(zone);
            return zone;
        }

        /// <summary>
        /// Removes an audio zone.
        /// </summary>
        /// <param name="zone">The zone to remove.</param>
        /// <remarks>
        /// Based on audio zone removal in daorigins.exe, DragonAge2.exe.
        /// Removes the zone from the system.
        /// </remarks>
        public void RemoveZone(IAudioZone zone)
        {
            if (zone != null)
            {
                _zones.Remove(zone);
            }
        }

        /// <summary>
        /// Gets the audio zone at a specific position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>The audio zone at the position, or null if none.</returns>
        /// <remarks>
        /// Based on audio zone lookup in daorigins.exe, DragonAge2.exe.
        /// Returns the first zone that contains the position (zones can overlap).
        /// </remarks>
        public IAudioZone GetZoneAtPosition(Vector3 position)
        {
            return _zones.FirstOrDefault(zone => zone.IsActive && zone.Contains(position));
        }

        /// <summary>
        /// Updates the audio zone system.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on audio zone system update in daorigins.exe, DragonAge2.exe.
        /// Updates zone states and effects.
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Audio zone system updates:
            // - Update zone states
            // - Update reverb effects
            // - Update audio propagation

            // In a full implementation, this would:
            // - Update reverb parameters based on zone properties
            // - Update audio propagation based on zone boundaries
            // - Update listener position relative to zones
        }
    }

    /// <summary>
    /// Eclipse engine audio zone implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Audio Zone Implementation:
    /// - Based on audio zone in daorigins.exe, DragonAge2.exe
    /// - Manages individual audio zone state and properties
    /// - Supports different reverb types for different acoustic environments
    /// </remarks>
    [PublicAPI]
    public class EclipseAudioZone : IAudioZone
    {
        private Vector3 _center;
        private float _radius;
        private ReverbType _reverbType;
        private bool _isActive;

        /// <summary>
        /// Creates a new Eclipse audio zone.
        /// </summary>
        /// <param name="center">The zone center position.</param>
        /// <param name="radius">The zone radius.</param>
        /// <param name="reverbType">The reverb type for this zone.</param>
        /// <remarks>
        /// Based on audio zone creation in daorigins.exe, DragonAge2.exe.
        /// Initializes zone with specified properties.
        /// </remarks>
        public EclipseAudioZone(Vector3 center, float radius, ReverbType reverbType)
        {
            _center = center;
            _radius = Math.Max(0.0f, radius);
            _reverbType = reverbType;
            _isActive = true;
        }

        /// <summary>
        /// Gets the zone center position.
        /// </summary>
        public Vector3 Center
        {
            get => _center;
            set => _center = value;
        }

        /// <summary>
        /// Gets the zone radius.
        /// </summary>
        public float Radius
        {
            get => _radius;
            set => _radius = Math.Max(0.0f, value);
        }

        /// <summary>
        /// Gets the reverb type for this zone.
        /// </summary>
        public ReverbType ReverbType
        {
            get => _reverbType;
            set => _reverbType = value;
        }

        /// <summary>
        /// Gets whether the zone is active.
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// Checks if a position is within this zone.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>True if the position is within the zone.</returns>
        /// <remarks>
        /// Based on zone containment check in daorigins.exe, DragonAge2.exe.
        /// Uses spherical distance check.
        /// </remarks>
        public bool Contains(Vector3 position)
        {
            if (!_isActive)
            {
                return false;
            }

            Vector3 delta = position - _center;
            float distanceSquared = delta.LengthSquared();
            float radiusSquared = _radius * _radius;

            return distanceSquared <= radiusSquared;
        }
    }
}

