using System;
using System.IO;
using System.Threading.Tasks;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Microsoft.Xna.Framework.Audio;

namespace Andastra.Runtime.MonoGame.Audio
{
    /// <summary>
    /// MonoGame implementation of IMusicPlayer for playing background music.
    /// 
    /// Loads WAV files from KOTOR installation and plays them in a loop using MonoGame's SoundEffect API.
    /// 
    /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Audio.SoundEffect.html
    /// SoundEffectInstance.IsLooped = true for looping playback
    /// </summary>
    /// <remarks>
    /// Music Player (MonoGame Implementation):
    /// - Based on swkotor.exe: FUN_005f9af0 @ 0x005f9af0 (music playback function)
    /// - Located via string references: "mus_theme_cult" @ 0x0074ebfc (K1 main menu music)
    /// - "mus_sion" @ 0x007c75e4 (K2 main menu music), "mus_theme_rep" (character creation music)
    /// - Original implementation: KOTOR plays WAV files for background music (looping)
    /// - Music files: Stored as WAV resources, referenced by ResRef (e.g., "mus_theme_cult")
    /// - Playback control: Play, Stop, Pause, Resume, volume control
    /// - Looping: Music loops continuously until stopped
    /// - This MonoGame implementation uses SoundEffectInstance with IsLooped = true
    /// </remarks>
    public class MonoGameMusicPlayer : IMusicPlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private SoundEffectInstance _currentMusicInstance;
        private SoundEffect _currentMusicEffect;
        private string _currentMusicResRef;
        private float _volume;
        private bool _isPaused;

        /// <summary>
        /// Initializes a new MonoGame music player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        public MonoGameMusicPlayer(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _volume = 1.0f;
            _isPaused = false;
        }

        /// <summary>
        /// Gets whether music is currently playing.
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                return _currentMusicInstance != null &&
                       _currentMusicInstance.State == SoundState.Playing;
            }
        }

        /// <summary>
        /// Gets or sets the music volume (0.0f to 1.0f).
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            set
            {
                _volume = Math.Max(0.0f, Math.Min(1.0f, value));
                if (_currentMusicInstance != null)
                {
                    _currentMusicInstance.Volume = _volume;
                }
            }
        }

        /// <summary>
        /// Plays a music file by ResRef. Music loops continuously until stopped.
        /// </summary>
        /// <param name="musicResRef">The ResRef of the music file (e.g., "mus_theme_cult").</param>
        /// <param name="volume">Volume level (0.0f to 1.0f, default 1.0f).</param>
        /// <returns>True if music started playing successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor.exe FUN_005f9af0:
        /// - K1 main menu: "mus_theme_cult"
        /// - K1 character creation: "mus_theme_rep"
        /// - K2 main menu: "mus_sion"
        /// - Music loops automatically until Stop() is called
        /// </remarks>
        public bool Play(string musicResRef, float volume = 1.0f)
        {
            if (string.IsNullOrEmpty(musicResRef))
            {
                return false;
            }

            // If same music is already playing, just adjust volume
            if (_currentMusicResRef == musicResRef && _currentMusicInstance != null &&
                _currentMusicInstance.State == SoundState.Playing)
            {
                Volume = volume;
                return true;
            }

            // Stop current music if playing
            Stop();

            try
            {
                // Load WAV resource
                var resourceId = new ResourceIdentifier(musicResRef, ResourceType.WAV);
                byte[] wavData = _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[MonoGameMusicPlayer] Music not found: {musicResRef}");
                    return false;
                }

                // Parse WAV file to get audio format information
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null)
                {
                    Console.WriteLine($"[MonoGameMusicPlayer] Failed to parse WAV: {musicResRef}");
                    return false;
                }

                // Convert WAV to MonoGame SoundEffect format
                // MonoGame SoundEffect.LoadFromStream() expects WAV format
                using (MemoryStream stream = new MemoryStream(wavData))
                {
                    _currentMusicEffect = SoundEffect.FromStream(stream);
                }

                if (_currentMusicEffect == null)
                {
                    Console.WriteLine($"[MonoGameMusicPlayer] Failed to create SoundEffect: {musicResRef}");
                    return false;
                }

                // Create instance with looping enabled
                _currentMusicInstance = _currentMusicEffect.CreateInstance();
                if (_currentMusicInstance == null)
                {
                    Console.WriteLine($"[MonoGameMusicPlayer] Failed to create SoundEffectInstance: {musicResRef}");
                    _currentMusicEffect.Dispose();
                    _currentMusicEffect = null;
                    return false;
                }

                // Configure for looping
                _currentMusicInstance.IsLooped = true;
                _currentMusicInstance.Volume = Math.Max(0.0f, Math.Min(1.0f, volume));
                _volume = _currentMusicInstance.Volume;

                // Play music
                _currentMusicInstance.Play();
                _currentMusicResRef = musicResRef;
                _isPaused = false;

                Console.WriteLine($"[MonoGameMusicPlayer] Playing music: {musicResRef} (looping)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonoGameMusicPlayer] Exception playing music: {ex.Message}");
                Console.WriteLine($"[MonoGameMusicPlayer] Stack trace: {ex.StackTrace}");

                // Clean up on error
                if (_currentMusicInstance != null)
                {
                    _currentMusicInstance.Dispose();
                    _currentMusicInstance = null;
                }
                if (_currentMusicEffect != null)
                {
                    _currentMusicEffect.Dispose();
                    _currentMusicEffect = null;
                }
                _currentMusicResRef = null;
                return false;
            }
        }

        /// <summary>
        /// Stops music playback.
        /// </summary>
        public void Stop()
        {
            if (_currentMusicInstance != null)
            {
                _currentMusicInstance.Stop();
                _currentMusicInstance.Dispose();
                _currentMusicInstance = null;
            }

            if (_currentMusicEffect != null)
            {
                _currentMusicEffect.Dispose();
                _currentMusicEffect = null;
            }

            _currentMusicResRef = null;
            _isPaused = false;
        }

        /// <summary>
        /// Pauses music playback (can be resumed with Resume()).
        /// </summary>
        public void Pause()
        {
            if (_currentMusicInstance != null && _currentMusicInstance.State == SoundState.Playing)
            {
                _currentMusicInstance.Pause();
                _isPaused = true;
            }
        }

        /// <summary>
        /// Resumes paused music playback.
        /// </summary>
        public void Resume()
        {
            if (_currentMusicInstance != null && _isPaused)
            {
                _currentMusicInstance.Resume();
                _isPaused = false;
            }
        }

        /// <summary>
        /// Disposes the music player and stops any playing music.
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}

