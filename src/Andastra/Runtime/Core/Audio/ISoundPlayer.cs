using System;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Audio
{
    /// <summary>
    /// Interface for playing sound effects in the game.
    /// </summary>
    /// <remarks>
    /// Sound Player Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) sound playback system
    /// - Located via string references: "PlaySound" @ 0x007c5f70, "Sound" @ 0x007bc558 (sound entity type)
    /// - "Sound List" @ 0x007bd290 (sound entity list in area), "AmbientSound" @ 0x007c0e98 (ambient sound field)
    /// - "SoundVolume" @ 0x007c0eb0 (sound volume), "SoundDistance" @ 0x007c0ec8 (sound distance/range)
    /// - Original implementation: KOTOR plays WAV files for sound effects (ambient, combat, UI, etc.)
    /// - Sound files: Stored as WAV resources, referenced by ResRef (e.g., "sound01.wav")
    /// - Positional audio: Sounds can be played at entity positions (3D spatial audio with distance attenuation)
    /// - Playback control: Play, Stop, volume, pan, pitch
    /// - Sound types: Ambient (area background), combat (weapon hits), UI (button clicks), voice-over (dialogue), music (BGM)
    /// - Original engine: DirectSound/DirectSound3D for 3D spatial audio, WAV file format support
    /// </remarks>
    public interface ISoundPlayer
    {
        /// <summary>
        /// Plays a sound effect by ResRef.
        /// </summary>
        /// <param name="soundResRef">The sound resource reference.</param>
        /// <param name="position">Optional 3D position for spatial audio. If null, plays as 2D sound.</param>
        /// <param name="volume">Volume (0.0 to 1.0).</param>
        /// <param name="pitch">Pitch adjustment (-1.0 to 1.0).</param>
        /// <param name="pan">Stereo panning (-1.0 left to 1.0 right).</param>
        /// <returns>Sound instance ID for controlling playback, or 0 if failed.</returns>
        uint PlaySound(string soundResRef, System.Numerics.Vector3? position = null, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f);

        /// <summary>
        /// Stops a playing sound by instance ID.
        /// </summary>
        /// <param name="soundInstanceId">The sound instance ID returned from PlaySound.</param>
        void StopSound(uint soundInstanceId);

        /// <summary>
        /// Stops all currently playing sounds.
        /// </summary>
        void StopAllSounds();

        /// <summary>
        /// Sets the master volume for all sounds.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0).</param>
        void SetMasterVolume(float volume);

        /// <summary>
        /// Updates the sound system (call each frame).
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        void Update(float deltaTime);
    }
}

