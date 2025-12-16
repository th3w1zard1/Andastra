using System;
using System.Numerics;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Spatial audio abstraction for 3D sound positioning.
    /// </summary>
    public interface ISpatialAudio
    {
        /// <summary>
        /// Gets or sets the Doppler effect factor.
        /// </summary>
        float DopplerFactor { get; set; }

        /// <summary>
        /// Gets or sets the speed of sound (for Doppler calculation).
        /// </summary>
        float SpeedOfSound { get; set; }

        /// <summary>
        /// Sets the audio listener (camera position).
        /// </summary>
        /// <param name="position">Listener position.</param>
        /// <param name="forward">Listener forward direction.</param>
        /// <param name="up">Listener up direction.</param>
        /// <param name="velocity">Listener velocity.</param>
        void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity);

        /// <summary>
        /// Creates a new audio emitter and returns its ID.
        /// </summary>
        /// <param name="position">Emitter position.</param>
        /// <param name="velocity">Emitter velocity.</param>
        /// <param name="volume">Emitter volume (0.0 to 1.0).</param>
        /// <param name="minDistance">Minimum distance for full volume.</param>
        /// <param name="maxDistance">Maximum distance (beyond this, volume is 0).</param>
        /// <returns>Emitter ID.</returns>
        uint CreateEmitter(Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance);

        /// <summary>
        /// Updates an audio emitter.
        /// </summary>
        /// <param name="emitterId">Emitter ID.</param>
        /// <param name="position">Emitter position.</param>
        /// <param name="velocity">Emitter velocity.</param>
        /// <param name="volume">Emitter volume (0.0 to 1.0).</param>
        /// <param name="minDistance">Minimum distance for full volume.</param>
        /// <param name="maxDistance">Maximum distance (beyond this, volume is 0).</param>
        void UpdateEmitter(uint emitterId, Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance);

        /// <summary>
        /// Calculates 3D audio parameters for an emitter.
        /// </summary>
        /// <param name="emitterId">Emitter ID.</param>
        /// <returns>3D audio parameters.</returns>
        Audio3DParameters Calculate3DParameters(uint emitterId);

        /// <summary>
        /// Removes an emitter.
        /// </summary>
        /// <param name="emitterId">Emitter ID.</param>
        void RemoveEmitter(uint emitterId);
    }

    /// <summary>
    /// 3D audio parameters.
    /// </summary>
    public struct Audio3DParameters
    {
        public float Volume;
        public float Pan;
        public float DopplerShift;
        public float Distance;
    }
}

