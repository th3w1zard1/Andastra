using System;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Audio
{
    /// <summary>
    /// Interface for playing voice-over audio (dialogue) in the game.
    /// </summary>
    /// <remarks>
    /// Voice Player Interface:
    /// - Based on swkotor.exe and swkotor2.exe voice-over playback system
    /// - Located via string references: "VoiceVolume" @ 0x007c2ce4 (voice volume setting in swkotor2.exe)
    /// - "PlayVoice" @ 0x007c5f80 (voice playback function reference), "Voice" @ 0x007bc560 (voice entity type)
    /// - Original implementation: KOTOR plays WAV files for voice-over dialogue
    /// - Voice files: Stored as WAV resources, referenced by ResRef (e.g., "n_darthmalak01.wav")
    /// - Dialogue system: Voice-overs are triggered during conversations and cutscenes
    /// - Playback control: Play, Stop, volume control
    /// - Voice types: Character dialogue, narrator, ambient voice (background conversations)
    /// - Volume control: Separate from sound effects volume, controlled by VoiceVolume setting (0.0 to 1.0)
    /// - Original engine: DirectSound for voice playback, WAV file format support
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00631ff0 @ 0x00631ff0 (writes VoiceVolume to INI file)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00633270 @ 0x00633270 (loads VoiceVolume from INI file)
    /// </remarks>
    public interface IVoicePlayer
    {
        /// <summary>
        /// Plays a voice-over audio file by ResRef.
        /// </summary>
        /// <param name="voiceResRef">The voice resource reference (e.g., "n_darthmalak01").</param>
        /// <param name="position">Optional 3D position for spatial audio. If null, plays as 2D sound.</param>
        /// <param name="volume">Volume (0.0 to 1.0). This will be multiplied by the master voice volume.</param>
        /// <param name="pitch">Pitch adjustment (-1.0 to 1.0).</param>
        /// <param name="pan">Stereo panning (-1.0 left to 1.0 right).</param>
        /// <returns>Voice instance ID for controlling playback, or 0 if failed.</returns>
        /// <remarks>
        /// Voice-overs are typically dialogue spoken by characters during conversations.
        /// The volume parameter is a relative volume that will be multiplied by the master voice volume
        /// setting from GameSettings.Audio.VoiceVolume.
        /// </remarks>
        uint PlayVoice(string voiceResRef, System.Numerics.Vector3? position = null, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f);

        /// <summary>
        /// Stops a playing voice-over by instance ID.
        /// </summary>
        /// <param name="voiceInstanceId">The voice instance ID returned from PlayVoice.</param>
        void StopVoice(uint voiceInstanceId);

        /// <summary>
        /// Stops all currently playing voice-overs.
        /// </summary>
        void StopAllVoices();

        /// <summary>
        /// Sets the master volume for all voice-overs.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0). This affects all voice-over playback.</param>
        /// <remarks>
        /// This sets the master voice volume that is applied to all voice-over playback.
        /// The volume is typically set from GameSettings.Audio.VoiceVolume.
        /// </remarks>
        void SetMasterVolume(float volume);

        /// <summary>
        /// Updates the voice player system (call each frame).
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        void Update(float deltaTime);
    }
}

