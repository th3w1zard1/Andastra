using System;
using System.Numerics;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Spatial audio abstraction for 3D sound positioning.
    /// </summary>
    /// <remarks>
    /// Spatial Audio Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 3D audio system
    /// - Located via string references: "EnvAudio" @ 0x007bd478 (environmental audio)
    /// - "EAX2 room rolloff" @ 0x007c5f24, "EAX3 room LF" @ 0x007c6010, "EAX3 room LF " @ 0x007c6030
    /// - "EAX2 room HF" @ 0x007c6040, "EAX2 room" @ 0x007c6050 (EAX audio environment)
    /// - "EAX3 modulation depth" @ 0x007c5f74, "EAX3 echo depth" @ 0x007c5fa4 (EAX effects)
    /// - "_AIL_set_digital_master_room_type@8" @ 0x0080a0f6, "_AIL_set_3D_room_type@8" @ 0x0080a11c
    /// - "_AIL_3D_room_type@4" @ 0x0080a1ec (Miles Sound System 3D audio functions)
    /// - Original implementation: Uses Miles Sound System (MSS) for 3D positional audio with EAX environmental effects
    /// - 3D audio: Calculates volume, pan, Doppler shift based on listener and emitter positions
    /// - EAX: Environmental Audio Extensions for reverb and environmental effects
    /// - This interface: Abstraction layer for modern spatial audio systems (XAudio2, OpenAL, etc.)
    /// </remarks>
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

