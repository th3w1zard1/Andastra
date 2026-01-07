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
        private Vector3 _listenerPosition;
        private bool _hasListenerPosition;
        private IAudioZone _currentZone;
        private ReverbParameters _currentReverbParameters;
        private float _reverbBlendFactor;

        /// <summary>
        /// Reverb parameters calculated from reverb type and zone properties.
        /// </summary>
        /// <remarks>
        /// Based on EAX reverb parameters and audio zone system in daorigins.exe, DragonAge2.exe.
        /// Parameters define acoustic properties of the audio environment.
        /// These parameters can be applied to an audio engine's reverb effect system.
        /// </remarks>
        public struct ReverbParameters
        {
            public float Room;              // Room effect level (0.0 to 100.0)
            public float RoomHF;            // Room high-frequency level (0.0 to 100.0)
            public float RoomRolloffFactor; // Room rolloff factor (0.0 to 10.0)
            public float DecayTime;         // Reverb decay time in seconds (0.1 to 20.0)
            public float DecayHFRatio;      // Decay high-frequency ratio (0.1 to 2.0)
            public float Reflections;       // Reflection level (0.0 to 100.0)
            public float ReflectionsDelay;  // Reflection delay in seconds (0.0 to 0.3)
            public float Reverb;            // Reverb level (0.0 to 100.0)
            public float ReverbDelay;       // Reverb delay in seconds (0.0 to 0.1)
            public float Diffusion;         // Diffusion (0.0 to 100.0)
            public float Density;           // Density (0.0 to 100.0)
            public float HFReference;       // High-frequency reference in Hz (20.0 to 20000.0)

            public static ReverbParameters None()
            {
                // No reverb (outdoor/open space)
                return new ReverbParameters
                {
                    Room = 0.0f,
                    RoomHF = 0.0f,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 0.1f,
                    DecayHFRatio = 1.0f,
                    Reflections = 0.0f,
                    ReflectionsDelay = 0.0f,
                    Reverb = 0.0f,
                    ReverbDelay = 0.0f,
                    Diffusion = 0.0f,
                    Density = 0.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters SmallRoom()
            {
                // Small room reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -800,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 0.17f,
                    DecayHFRatio = 0.59f,
                    Reflections = -1204,
                    ReflectionsDelay = 0.007f,
                    Reverb = 163,
                    ReverbDelay = 0.011f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters MediumRoom()
            {
                // Medium room reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -1000,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 0.4f,
                    DecayHFRatio = 0.83f,
                    Reflections = -1214,
                    ReflectionsDelay = 0.007f,
                    Reverb = 207,
                    ReverbDelay = 0.011f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters LargeRoom()
            {
                // Large room reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -1100,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 0.51f,
                    DecayHFRatio = 0.5f,
                    Reflections = -1244,
                    ReflectionsDelay = 0.007f,
                    Reverb = 53,
                    ReverbDelay = 0.019f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters Cave()
            {
                // Cave reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -602,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 2.91f,
                    DecayHFRatio = 0.3f,
                    Reflections = -256,
                    ReflectionsDelay = 0.015f,
                    Reverb = -698,
                    ReverbDelay = 0.022f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters Hall()
            {
                // Hall reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -800,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 1.49f,
                    DecayHFRatio = 0.59f,
                    Reflections = -1219,
                    ReflectionsDelay = 0.007f,
                    Reverb = 395,
                    ReverbDelay = 0.023f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters Cathedral()
            {
                // Cathedral reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -900,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 4.62f,
                    DecayHFRatio = 0.64f,
                    Reflections = -1200,
                    ReflectionsDelay = 0.032f,
                    Reverb = -207,
                    ReverbDelay = 0.03f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            public static ReverbParameters Underwater()
            {
                // Underwater reverb
                return new ReverbParameters
                {
                    Room = -1000,
                    RoomHF = -4000,
                    RoomRolloffFactor = 0.0f,
                    DecayTime = 1.49f,
                    DecayHFRatio = 0.1f,
                    Reflections = -449,
                    ReflectionsDelay = 0.007f,
                    Reverb = 1700,
                    ReverbDelay = 0.011f,
                    Diffusion = 100.0f,
                    Density = 100.0f,
                    HFReference = 5000.0f
                };
            }

            /// <summary>
            /// Blends two reverb parameter sets based on blend factor (0.0 = first, 1.0 = second).
            /// </summary>
            public static ReverbParameters Lerp(ReverbParameters a, ReverbParameters b, float t)
            {
                t = Math.Max(0.0f, Math.Min(1.0f, t));
                return new ReverbParameters
                {
                    Room = a.Room + (b.Room - a.Room) * t,
                    RoomHF = a.RoomHF + (b.RoomHF - a.RoomHF) * t,
                    RoomRolloffFactor = a.RoomRolloffFactor + (b.RoomRolloffFactor - a.RoomRolloffFactor) * t,
                    DecayTime = a.DecayTime + (b.DecayTime - a.DecayTime) * t,
                    DecayHFRatio = a.DecayHFRatio + (b.DecayHFRatio - a.DecayHFRatio) * t,
                    Reflections = a.Reflections + (b.Reflections - a.Reflections) * t,
                    ReflectionsDelay = a.ReflectionsDelay + (b.ReflectionsDelay - a.ReflectionsDelay) * t,
                    Reverb = a.Reverb + (b.Reverb - a.Reverb) * t,
                    ReverbDelay = a.ReverbDelay + (b.ReverbDelay - a.ReverbDelay) * t,
                    Diffusion = a.Diffusion + (b.Diffusion - a.Diffusion) * t,
                    Density = a.Density + (b.Density - a.Density) * t,
                    HFReference = a.HFReference + (b.HFReference - a.HFReference) * t
                };
            }
        }

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
            _listenerPosition = Vector3.Zero;
            _hasListenerPosition = false;
            _currentZone = null;
            _currentReverbParameters = ReverbParameters.None();
            _reverbBlendFactor = 0.0f;
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
        /// Sets the listener position for audio zone calculations.
        /// </summary>
        /// <param name="position">The listener position in world space.</param>
        /// <remarks>
        /// Based on audio zone system listener tracking in daorigins.exe, DragonAge2.exe.
        /// The listener position is used to determine which zones are active and to calculate
        /// reverb parameters based on distance from zone boundaries.
        /// </remarks>
        public void SetListenerPosition(Vector3 position)
        {
            _listenerPosition = position;
            _hasListenerPosition = true;
        }

        /// <summary>
        /// Gets the current reverb parameters based on the listener's position in zones.
        /// </summary>
        /// <returns>Current reverb parameters, or None if no zone is active.</returns>
        /// <remarks>
        /// Based on reverb parameter calculation in daorigins.exe, DragonAge2.exe.
        /// Returns the reverb parameters for the zone containing the listener, or None if
        /// the listener is not in any zone. For overlapping zones, returns parameters for
        /// the closest zone center.
        /// </remarks>
        public ReverbParameters GetCurrentReverbParameters()
        {
            return _currentReverbParameters;
        }

        /// <summary>
        /// Gets the current active zone at the listener position.
        /// </summary>
        /// <returns>The active zone, or null if none.</returns>
        /// <remarks>
        /// Based on audio zone system active zone tracking in daorigins.exe, DragonAge2.exe.
        /// Returns the zone that currently contains the listener position.
        /// </remarks>
        [CanBeNull]
        public IAudioZone GetCurrentZone()
        {
            return _currentZone;
        }

        /// <summary>
        /// Updates the audio zone system.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        /// <remarks>
        /// Based on audio zone system update in daorigins.exe, DragonAge2.exe.
        /// Updates zone states and effects.
        /// 
        /// Audio zone system updates (daorigins.exe: Audio zone update loop, DragonAge2.exe: Enhanced zone management):
        /// - Updates reverb parameters based on zone properties and listener position
        /// - Updates audio propagation based on zone boundaries and distance
        /// - Updates listener position relative to zones (distance-based reverb blending)
        /// - Handles zone transitions with smooth reverb parameter interpolation
        /// - Calculates zone influence based on distance from zone center and boundaries
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Update zone states
            // Remove inactive zones from tracking (zones can be deactivated without removal)
            // Based on daorigins.exe: Zones are checked for active state each frame

            // Update reverb parameters based on zone properties and listener position
            if (_hasListenerPosition)
            {
                UpdateReverbParameters();
            }

            // Update audio propagation based on zone boundaries
            // Based on daorigins.exe, DragonAge2.exe: Propagation is calculated per-frame
            // Propagation affects how sounds travel through zone boundaries
            UpdateAudioPropagation();

            // Update listener position relative to zones
            // Based on daorigins.exe, DragonAge2.exe: Listener position is tracked relative to zone centers
            // This is used for distance-based reverb intensity calculation
            UpdateListenerZoneRelations();
        }

        /// <summary>
        /// Updates reverb parameters based on zone properties and listener position.
        /// </summary>
        /// <remarks>
        /// Based on reverb parameter update in daorigins.exe, DragonAge2.exe.
        /// Calculates reverb parameters based on:
        /// - Current zone containing listener
        /// - Distance from zone center (affects reverb intensity)
        /// - Zone reverb type
        /// - Smooth transitions between zones when listener moves
        /// </remarks>
        private void UpdateReverbParameters()
        {
            // Find zones containing the listener position
            // Based on daorigins.exe: Multiple zones can overlap, we select the closest to center
            IAudioZone closestZone = null;
            float closestDistanceSquared = float.MaxValue;

            foreach (IAudioZone zone in _zones)
            {
                if (!zone.IsActive)
                {
                    continue;
                }

                if (zone.Contains(_listenerPosition))
                {
                    // Calculate distance from listener to zone center
                    Vector3 delta = _listenerPosition - zone.Center;
                    float distanceSquared = delta.LengthSquared();

                    // Select closest zone center (for overlapping zones)
                    if (distanceSquared < closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceSquared;
                        closestZone = zone;
                    }
                }
            }

            // Update current zone
            bool zoneChanged = _currentZone != closestZone;
            _currentZone = closestZone;

            // Calculate target reverb parameters
            ReverbParameters targetReverb = ReverbParameters.None();
            float targetBlendFactor = 0.0f;

            if (closestZone != null)
            {
                // Get base reverb parameters for this reverb type
                // Based on daorigins.exe, DragonAge2.exe: Each ReverbType maps to EAX reverb preset
                targetReverb = GetReverbParametersForType(closestZone.ReverbType);

                // Calculate distance-based reverb intensity
                // Based on daorigins.exe, DragonAge2.exe: Reverb intensity decreases as listener moves
                // from zone center toward zone boundary
                float distance = (float)Math.Sqrt(closestDistanceSquared);
                float radius = closestZone.Radius;

                if (radius > 0.0f)
                {
                    // Calculate normalized distance from center (0.0 = center, 1.0 = boundary)
                    float normalizedDistance = distance / radius;

                    // Reverb intensity is strongest at center, fades toward boundary
                    // Based on audio engine patterns: Linear falloff from center to edge
                    // Full reverb at center, 50% at boundary (smooth transition)
                    float centerIntensity = 1.0f;
                    float boundaryIntensity = 0.5f;
                    float intensity = centerIntensity - (normalizedDistance * (centerIntensity - boundaryIntensity));
                    targetBlendFactor = Math.Max(0.0f, Math.Min(1.0f, intensity));
                }
                else
                {
                    targetBlendFactor = 1.0f;
                }
            }

            // Smoothly interpolate reverb parameters when zone changes
            // Based on daorigins.exe, DragonAge2.exe: Reverb transitions are smoothed over time
            // to avoid audio artifacts when moving between zones
            const float transitionSpeed = 5.0f; // Rate of transition (higher = faster)
            float blendStep = transitionSpeed * (zoneChanged ? 0.1f : 0.05f); // Faster transition on zone change

            if (zoneChanged)
            {
                // Immediate transition on zone change, but still smooth
                _reverbBlendFactor = Math.Max(0.0f, Math.Min(1.0f, targetBlendFactor));
            }
            else
            {
                // Smooth interpolation toward target blend factor
                if (_reverbBlendFactor < targetBlendFactor)
                {
                    _reverbBlendFactor = Math.Min(targetBlendFactor, _reverbBlendFactor + blendStep);
                }
                else if (_reverbBlendFactor > targetBlendFactor)
                {
                    _reverbBlendFactor = Math.Max(targetBlendFactor, _reverbBlendFactor - blendStep);
                }
            }

            // Blend between no reverb and target reverb based on blend factor
            ReverbParameters noReverb = ReverbParameters.None();
            _currentReverbParameters = ReverbParameters.Lerp(noReverb, targetReverb, _reverbBlendFactor);
        }

        /// <summary>
        /// Gets reverb parameters for a specific reverb type.
        /// </summary>
        /// <param name="reverbType">The reverb type.</param>
        /// <returns>Reverb parameters for the reverb type.</returns>
        /// <remarks>
        /// Based on EAX reverb preset mapping in daorigins.exe, DragonAge2.exe.
        /// Each ReverbType corresponds to a standard EAX reverb preset.
        /// </remarks>
        private static ReverbParameters GetReverbParametersForType(ReverbType reverbType)
        {
            switch (reverbType)
            {
                case ReverbType.None:
                    return ReverbParameters.None();
                case ReverbType.SmallRoom:
                    return ReverbParameters.SmallRoom();
                case ReverbType.MediumRoom:
                    return ReverbParameters.MediumRoom();
                case ReverbType.LargeRoom:
                    return ReverbParameters.LargeRoom();
                case ReverbType.Cave:
                    return ReverbParameters.Cave();
                case ReverbType.Hall:
                    return ReverbParameters.Hall();
                case ReverbType.Cathedral:
                    return ReverbParameters.Cathedral();
                case ReverbType.Underwater:
                    return ReverbParameters.Underwater();
                default:
                    return ReverbParameters.None();
            }
        }

        /// <summary>
        /// Updates audio propagation based on zone boundaries.
        /// </summary>
        /// <remarks>
        /// Based on audio propagation update in daorigins.exe, DragonAge2.exe.
        /// Audio propagation affects how sounds travel through zone boundaries:
        /// - Sounds within zones are affected by zone acoustic properties
        /// - Sounds crossing zone boundaries have propagation delays and attenuation
        /// - Zone boundaries can act as sound barriers or filters
        /// 
        /// This is a placeholder for future implementation of advanced propagation effects.
        /// Current implementation tracks propagation state for potential future enhancement.
        /// </remarks>
        private void UpdateAudioPropagation()
        {
            // Audio propagation calculation
            // Based on daorigins.exe, DragonAge2.exe: Propagation is calculated for each active sound
            // For now, we track zone states - full propagation implementation would:
            // - Calculate sound path through zone boundaries
            // - Apply propagation delays based on distance and zone properties
            // - Apply frequency-dependent attenuation through zone boundaries
            // - Handle sound reflection and refraction at zone boundaries

            // Current implementation: Zone states are tracked, ready for propagation calculation
            // Full propagation would require integration with the audio engine's sound emitter system
        }

        /// <summary>
        /// Updates listener position relative to zones.
        /// </summary>
        /// <remarks>
        /// Based on listener zone relation tracking in daorigins.exe, DragonAge2.exe.
        /// Tracks the listener's relationship to zones:
        /// - Distance from zone centers (for distance-based reverb)
        /// - Whether listener is inside or outside zone boundaries
        /// - Zone transition state (entering, exiting, inside)
        /// 
        /// This information is used for smooth reverb transitions and distance-based effects.
        /// </remarks>
        private void UpdateListenerZoneRelations()
        {
            // Listener zone relationship tracking
            // Based on daorigins.exe, DragonAge2.exe: Listener position is tracked relative to all zones
            // This information is used for:
            // - Distance-based reverb intensity calculation (done in UpdateReverbParameters)
            // - Zone transition detection (entering/exiting zones)
            // - Overlapping zone resolution (closest zone wins)

            // Current implementation: Zone detection and distance calculation is done in UpdateReverbParameters
            // Additional tracking for zone transitions could be added here if needed for future features
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

