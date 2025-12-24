using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Games.Eclipse;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse.Environmental
{
    /// <summary>
    /// Interface for audio zone system in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Audio Zone Interface:
    /// - Based on daorigins.exe, DragonAge2.exe audio zone systems
    /// - Eclipse engines support spatial audio zones with reverb and environmental effects
    /// - Audio zones define 3D spatial audio regions with specific acoustic properties
    /// - Zones can have reverb, echo, and other environmental audio effects
    /// - Audio zones affect sounds played within their boundaries
    /// </remarks>
    [PublicAPI]
    public interface IAudioZoneSystem : IUpdatable
    {
        /// <summary>
        /// Gets all active audio zones.
        /// </summary>
        IEnumerable<IAudioZone> Zones { get; }

        /// <summary>
        /// Creates a new audio zone.
        /// </summary>
        /// <param name="center">The zone center position.</param>
        /// <param name="radius">The zone radius.</param>
        /// <param name="reverbType">The reverb type for this zone.</param>
        /// <returns>The created audio zone.</returns>
        IAudioZone CreateZone(Vector3 center, float radius, ReverbType reverbType);

        /// <summary>
        /// Removes an audio zone.
        /// </summary>
        /// <param name="zone">The zone to remove.</param>
        void RemoveZone(IAudioZone zone);

        /// <summary>
        /// Gets the audio zone at a specific position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>The audio zone at the position, or null if none.</returns>
        IAudioZone GetZoneAtPosition(Vector3 position);
    }

    /// <summary>
    /// Interface for individual audio zone.
    /// </summary>
    [PublicAPI]
    public interface IAudioZone
    {
        /// <summary>
        /// Gets the zone center position.
        /// </summary>
        Vector3 Center { get; set; }

        /// <summary>
        /// Gets the zone radius.
        /// </summary>
        float Radius { get; set; }

        /// <summary>
        /// Gets the reverb type for this zone.
        /// </summary>
        ReverbType ReverbType { get; set; }

        /// <summary>
        /// Gets whether the zone is active.
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Checks if a position is within this zone.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>True if the position is within the zone.</returns>
        bool Contains(Vector3 position);
    }

    /// <summary>
    /// Reverb types for audio zones in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Based on audio zone system in daorigins.exe, DragonAge2.exe.
    /// Different reverb types simulate different acoustic environments.
    /// </remarks>
    public enum ReverbType
    {
        /// <summary>
        /// No reverb (outdoor/open space).
        /// </summary>
        None = 0,

        /// <summary>
        /// Small room reverb.
        /// </summary>
        SmallRoom = 1,

        /// <summary>
        /// Medium room reverb.
        /// </summary>
        MediumRoom = 2,

        /// <summary>
        /// Large room reverb.
        /// </summary>
        LargeRoom = 3,

        /// <summary>
        /// Cave reverb.
        /// </summary>
        Cave = 4,

        /// <summary>
        /// Hall reverb.
        /// </summary>
        Hall = 5,

        /// <summary>
        /// Cathedral reverb.
        /// </summary>
        Cathedral = 6,

        /// <summary>
        /// Underwater reverb.
        /// </summary>
        Underwater = 7
    }
}

