using System;
using System.IO;
using System.Threading.Tasks;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Microsoft.Xna.Framework.Audio;

namespace Andastra.Runtime.MonoGame.Audio
{
    /// <summary>
    /// MonoGame implementation of IVoicePlayer for playing voice-over audio.
    /// 
    /// Loads WAV files from KOTOR installation and plays them using MonoGame's SoundEffect API.
    /// Supports positional audio for speaker entities.
    /// 
    /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Audio.SoundEffect.html
    /// SoundEffect.LoadFromStream() loads WAV data from a stream
    /// SoundEffectInstance provides playback control (Play, Stop, Volume, Pan, Pitch)
    /// </summary>
    /// <remarks>
    /// Voice Player (MonoGame Implementation):
    /// - Based on swkotor2.exe voice-over playback system
    /// - Located via string references: "VO_ResRef" @ 0x007c3574, dialogue voice-over playback
    /// - Voice directories: "STREAMVOICE" @ 0x007c69dc, ".\streamvoice" @ 0x007c69e8, "d:\streamvoice" @ 0x007c69f8
    /// - "HD0:STREAMVOICE" @ 0x007c771c, "HD0:STREAMVOICE\" @ 0x007c3914
    /// - "HD0:STREAMVOICE\%s" @ 0x007c3944, "HD0:STREAMVOICE\%s\%s\%s" @ 0x007c3928 (voice path format strings)
    /// - Voice settings: "Voiceover Volume" @ 0x007c83cc, "Number 3D Voices" @ 0x007c7258, "Number 2D Voices" @ 0x007c726c
    /// - "VoiceChat" @ 0x007cb2ec (voice chat reference)
    /// - Original implementation: KOTOR plays WAV files for dialogue voice-overs from dialogue entries
    /// - Voice-over files: Stored as WAV resources, referenced by ResRef in DLG entries
    /// - Voice streaming: Voice files can be streamed from STREAMVOICE directory
    /// - Positional audio: Voice-over plays at speaker entity position (if SpatialAudio provided)
    /// - Playback control: Play, Stop, volume, pan, pitch (original engine uses DirectSound/EAX)
    /// - Lip sync: Voice-over playback timing synchronized with LIP file animation data
    /// - This MonoGame implementation uses SoundEffect API instead of original DirectSound/EAX
    /// </remarks>
    public class MonoGameVoicePlayer : IVoicePlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly SpatialAudio _spatialAudio;
        private SoundEffectInstance _currentInstance;
        private SoundEffect _currentSoundEffect;
        private IEntity _currentSpeaker;
        private uint _currentEmitterId;
        private bool _isPlaying;
        private float _currentTime;
        private Action _onCompleteCallback;
        private float _volume = 1.0f;

        /// <summary>
        /// Gets whether voice-over is currently playing.
        /// </summary>
        public bool IsPlaying
        {
            get { return _isPlaying && _currentInstance != null && _currentInstance.State == SoundState.Playing; }
        }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        public float CurrentTime
        {
            get
            {
                if (_currentInstance != null && _currentInstance.State == SoundState.Playing)
                {
                    // MonoGame doesn't expose playback position directly
                    // This is an approximation based on elapsed time
                    return _currentTime;
                }
                return 0f;
            }
        }

        /// <summary>
        /// Gets or sets the voice volume (0.0 to 1.0).
        /// Based on swkotor.exe and swkotor2.exe: VoiceVolume setting from INI file
        /// </summary>
        public float Volume
        {
            get { return _volume; }
            set
            {
                _volume = Math.Max(0.0f, Math.Min(1.0f, value));
                // Apply to currently playing instance if any
                if (_currentInstance != null)
                {
                    // If spatial audio is active, calculate 3D parameters and apply volume immediately
                    // Otherwise, apply the voice volume directly
                    if (_spatialAudio == null || _currentSpeaker == null || _currentEmitterId == 0)
                    {
                        _currentInstance.Volume = _volume;
                    }
                    else
                    {
                        // Calculate current 3D audio parameters and apply volume immediately
                        // This ensures volume changes take effect immediately, not just on next Update()
                        ITransformComponent transform = _currentSpeaker.GetComponent<ITransformComponent>();
                        if (transform != null)
                        {
                            Audio3DParameters audioParams = _spatialAudio.Calculate3DParameters(_currentEmitterId);
                            // Apply voice volume setting multiplied by spatial audio volume
                            _currentInstance.Volume = audioParams.Volume * _volume;
                            _currentInstance.Pan = audioParams.Pan;
                            _currentInstance.Pitch = audioParams.DopplerShift - 1.0f; // Convert to pitch range
                        }
                        else
                        {
                            // Fallback if transform is not available
                            _currentInstance.Volume = _volume;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Initializes a new MonoGame voice player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        /// <param name="spatialAudio">Optional spatial audio system for 3D positioning.</param>
        public MonoGameVoicePlayer(IGameResourceProvider resourceProvider, SpatialAudio spatialAudio = null)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _spatialAudio = spatialAudio;
            _currentEmitterId = 0;
        }

        /// <summary>
        /// Plays a voice-over.
        /// </summary>
        /// <param name="voResRef">The voice-over resource reference.</param>
        /// <param name="speaker">The speaking entity (for positional audio).</param>
        /// <param name="onComplete">Callback when playback completes.</param>
        public void Play(string voResRef, IEntity speaker, Action onComplete)
        {
            if (string.IsNullOrEmpty(voResRef))
            {
                onComplete?.Invoke();
                return;
            }

            // Stop any currently playing voice-over
            Stop();

            _currentSpeaker = speaker;
            _onCompleteCallback = onComplete;
            _isPlaying = false;
            _currentTime = 0f;

            // Load and play WAV file asynchronously
            Task.Run(async () =>
            {
                try
                {
                    // Load WAV resource
                    var resourceId = new ResourceIdentifier(voResRef, ResourceType.WAV);
                    byte[] wavData = await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);
                    if (wavData == null || wavData.Length == 0)
                    {
                        Console.WriteLine($"[MonoGameVoicePlayer] Failed to load WAV resource: {voResRef}");
                        _onCompleteCallback?.Invoke();
                        return;
                    }

                    // Parse WAV file
                    WAV wav = WAVAuto.ReadWav(wavData);
                    if (wav == null || wav.Data == null || wav.Data.Length == 0)
                    {
                        Console.WriteLine($"[MonoGameVoicePlayer] Failed to parse WAV data: {voResRef}");
                        _onCompleteCallback?.Invoke();
                        return;
                    }

                    // Create WAV stream for MonoGame
                    // MonoGame expects standard RIFF/WAVE format
                    byte[] monoGameWavData = CreateMonoGameWavStream(wav);
                    if (monoGameWavData == null)
                    {
                        Console.WriteLine($"[MonoGameVoicePlayer] Failed to create MonoGame WAV stream: {voResRef}");
                        _onCompleteCallback?.Invoke();
                        return;
                    }

                    // Load SoundEffect from stream
                    using (MemoryStream stream = new MemoryStream(monoGameWavData))
                    {
                        _currentSoundEffect = SoundEffect.FromStream(stream);
                        if (_currentSoundEffect == null)
                        {
                            Console.WriteLine($"[MonoGameVoicePlayer] Failed to load SoundEffect: {voResRef}");
                            _onCompleteCallback?.Invoke();
                            return;
                        }

                        // Create instance for playback control
                        _currentInstance = _currentSoundEffect.CreateInstance();
                        if (_currentInstance == null)
                        {
                            Console.WriteLine($"[MonoGameVoicePlayer] Failed to create SoundEffectInstance: {voResRef}");
                            _currentSoundEffect.Dispose();
                            _currentSoundEffect = null;
                            _onCompleteCallback?.Invoke();
                            return;
                        }

                        // Configure playback
                        // Apply voice volume setting (0.0 to 1.0)
                        _currentInstance.Volume = _volume;
                        _currentInstance.IsLooped = false;

                        // Register with spatial audio if available
                        if (_spatialAudio != null && speaker != null)
                        {
                            ITransformComponent transform = speaker.GetComponent<ITransformComponent>();
                            if (transform != null)
                            {
                                _currentEmitterId++;
                                _spatialAudio.UpdateEmitter(
                                    _currentEmitterId,
                                    new Microsoft.Xna.Framework.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z),
                                    Microsoft.Xna.Framework.Vector3.Zero,
                                    1.0f,
                                    5.0f, // min distance
                                    50.0f // max distance
                                );
                            }
                        }

                        // Start playback
                        _currentInstance.Play();
                        _isPlaying = true;

                        // Monitor playback completion
                        MonitorPlayback();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonoGameVoicePlayer] Error playing voice-over {voResRef}: {ex.Message}");
                    Stop();
                    _onCompleteCallback?.Invoke();
                }
            });
        }

        /// <summary>
        /// Stops the currently playing voice-over.
        /// </summary>
        public void Stop()
        {
            if (_currentInstance != null)
            {
                _currentInstance.Stop();
                _currentInstance.Dispose();
                _currentInstance = null;
            }

            if (_currentSoundEffect != null)
            {
                _currentSoundEffect.Dispose();
                _currentSoundEffect = null;
            }

            if (_spatialAudio != null && _currentEmitterId > 0)
            {
                _spatialAudio.RemoveEmitter(_currentEmitterId);
            }

            _isPlaying = false;
            _currentTime = 0f;
            _currentSpeaker = null;
            _onCompleteCallback = null;
        }

        /// <summary>
        /// Updates voice player state (should be called each frame).
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_isPlaying && _currentInstance != null)
            {
                // Update playback time
                if (_currentInstance.State == SoundState.Playing)
                {
                    _currentTime += deltaTime;
                }

                // Update spatial audio if available
                if (_spatialAudio != null && _currentSpeaker != null && _currentEmitterId > 0)
                {
                    ITransformComponent transform = _currentSpeaker.GetComponent<ITransformComponent>();
                    if (transform != null)
                    {
                        Audio3DParameters audioParams = _spatialAudio.Calculate3DParameters(_currentEmitterId);
                        if (_currentInstance != null)
                        {
                            // Apply voice volume setting multiplied by spatial audio volume
                            _currentInstance.Volume = audioParams.Volume * _volume;
                            _currentInstance.Pan = audioParams.Pan;
                            _currentInstance.Pitch = audioParams.DopplerShift - 1.0f; // Convert to pitch range
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a MonoGame-compatible WAV stream from a WAV object.
        /// 
        /// Handles all WAV formats comprehensively:
        /// - PCM (8-bit, 16-bit, 24-bit, 32-bit) - converts to 16-bit PCM
        /// - IMA ADPCM - decodes to 16-bit PCM
        /// - MS ADPCM - decodes to 16-bit PCM
        /// - A-Law - decodes to 16-bit PCM
        /// - μ-Law - decodes to 16-bit PCM
        /// - Mono and stereo channels - preserves channel configuration
        /// - All common sample rates - preserves sample rate
        /// </summary>
        private byte[] CreateMonoGameWavStream(WAV wav)
        {
            if (wav == null)
            {
                return null;
            }

            try
            {
                // Use comprehensive format converter
                return WavFormatConverter.ConvertToMonoGameFormat(wav);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonoGameVoicePlayer] Error creating WAV stream: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Monitors playback completion.
        /// </summary>
        private void MonitorPlayback()
        {
            Task.Run(async () =>
            {
                while (_isPlaying && _currentInstance != null)
                {
                    await Task.Delay(100); // Check every 100ms

                    if (_currentInstance.State == SoundState.Stopped)
                    {
                        _isPlaying = false;
                        Action callback = _onCompleteCallback;
                        Stop();
                        callback?.Invoke();
                        break;
                    }
                }
            });
        }
    }
}

