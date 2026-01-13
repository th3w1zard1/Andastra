using System;

namespace Andastra.Runtime.Core.Audio
{
    /// <summary>
    /// Interface for playing background music (looping audio).
    /// 
    /// Music Player:
    /// - Based on swkotor.exe: 0x005f9af0 @ 0x005f9af0 (music playback function)
    /// - Located via string references: "mus_theme_cult" @ 0x0074ebfc (K1 main menu music)
    /// - "mus_sion" @ 0x007c75e4 (K2 main menu music), "mus_theme_rep" (character creation music)
    /// - Original implementation: KOTOR plays WAV files for background music (looping)
    /// - Music files: Stored as WAV resources, referenced by ResRef (e.g., "mus_theme_cult")
    /// - Playback control: Play, Stop, Pause, Resume, volume control
    /// - Looping: Music loops continuously until stopped
    /// - This interface provides backend-agnostic music playback (MonoGame or Stride)
    /// 
    /// Implementation Notes:
    /// - Music files are WAV format (same as sound effects, but played in loop)
    /// - Volume control: 0.0f (silent) to 1.0f (full volume)
    /// - Play() starts music playback (loops automatically)
    /// - Stop() stops music playback
    /// - Pause()/Resume() for temporary pause/resume
    /// - IsPlaying property indicates if music is currently playing
    /// </remarks>
    public interface IMusicPlayer
    {
        /// <summary>
        /// Gets whether music is currently playing.
        /// </summary>
        bool IsPlaying { get; }

        /// <summary>
        /// Gets or sets the music volume (0.0f to 1.0f).
        /// </summary>
        float Volume { get; set; }

        /// <summary>
        /// Plays a music file by ResRef. Music loops continuously until stopped.
        /// </summary>
        /// <param name="musicResRef">The ResRef of the music file (e.g., "mus_theme_cult").</param>
        /// <param name="volume">Volume level (0.0f to 1.0f, default 1.0f).</param>
        /// <returns>True if music started playing successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor.exe 0x005f9af0:
        /// - K1 main menu: "mus_theme_cult"
        /// - K1 character creation: "mus_theme_rep"
        /// - K2 main menu: "mus_sion"
        /// - Music loops automatically until Stop() is called
        /// </remarks>
        bool Play(string musicResRef, float volume = 1.0f);

        /// <summary>
        /// Stops music playback.
        /// </summary>
        void Stop();

        /// <summary>
        /// Pauses music playback (can be resumed with Resume()).
        /// </summary>
        void Pause();

        /// <summary>
        /// Resumes paused music playback.
        /// </summary>
        void Resume();
    }
}

