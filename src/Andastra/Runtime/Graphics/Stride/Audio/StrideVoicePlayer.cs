using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Graphics;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.WAV;
using Stride.Audio;
using Stride.Media;
using SoundPlayState = Stride.Media.PlayState;

namespace Andastra.Runtime.Stride.Audio
{
    /// <summary>
    /// Stride implementation of IVoicePlayer for playing voice-over dialogue.
    ///
    /// Loads WAV files from KOTOR installation and plays them using Stride's Audio API.
    /// Supports positional audio for 3D voice-over positioning.
    ///
    /// Based on Stride API: https://doc.stride3d.net/latest/en/manual/audio/index.html
    /// SoundInstance API: https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html
    /// SoundBase API: https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundBase.html
    /// </summary>
    /// <remarks>
    /// Voice Player (Stride Implementation):
    /// - Based on swkotor2.exe voice-over playback system
    /// - Located via string references: "VO_ResRef" @ 0x007c4f78, "VoiceOver" @ 0x007c4f88
    /// - Original implementation: KOTOR plays WAV files for voice-over dialogue
    /// - Voice files: Stored as WAV resources, referenced by ResRef (e.g., "n_darthmalak_001.wav")
    /// - Positional audio: Voice-over plays at speaker entity position (if SpatialAudio provided)
    /// - Playback control: Play, Stop, volume, pan, pitch
    /// - This Stride implementation uses Stride's AudioEngine and SoundInstance API
    ///
    /// Implementation Notes:
    /// - Uses SoundInstance.Position (TimeSpan) to get playback position in seconds
    /// - Uses SoundInstance.PlayState to check if sound is playing
    /// - Uses SoundInstance.Stop() to stop playback
    /// - Creates SoundInstance from SoundBase using CreateInstance()
    /// - WAV data is loaded and converted to Stride Sound format
    /// </remarks>
    public class StrideVoicePlayer : IVoicePlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ISpatialAudio _spatialAudio;
        private readonly AudioEngine _audioEngine;
        private readonly AudioListener _audioListener;
        private SoundInstance _currentVoiceInstance;
        private Action _onCompleteCallback;
        private bool _isPlaying;
        private float _volume = 1.0f;

        /// <summary>
        /// Initializes a new Stride voice player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        /// <param name="spatialAudio">Optional spatial audio system for 3D positioning.</param>
        /// <param name="audioEngine">Stride AudioEngine instance. If null, will attempt to create one.</param>
        /// <param name="audioListener">Stride AudioListener instance. If null, will create a default one.</param>
        public StrideVoicePlayer(IGameResourceProvider resourceProvider, ISpatialAudio spatialAudio = null, AudioEngine audioEngine = null, AudioListener audioListener = null)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _spatialAudio = spatialAudio;

            // Initialize AudioEngine if not provided
            // Based on Stride API: AudioEngine is required for audio playback
            // https://doc.stride3d.net/latest/en/api/Stride.Audio.AudioEngine.html
            if (audioEngine == null)
            {
                try
                {
                    _audioEngine = AudioEngineFactory.NewAudioEngine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Failed to create AudioEngine: {ex.Message}");
                    throw new InvalidOperationException("Failed to initialize Stride AudioEngine", ex);
                }
            }
            else
            {
                _audioEngine = audioEngine;
            }

            // Initialize AudioListener if not provided
            // Based on Stride API: AudioListener is required for 3D spatial audio
            // https://doc.stride3d.net/latest/en/api/Stride.Audio.AudioListener.html
            _audioListener = audioListener ?? new AudioListener();

            _isPlaying = false;
        }

        /// <summary>
        /// Plays a voice-over.
        /// </summary>
        /// <param name="voResRef">Resource reference for the voice-over WAV file.</param>
        /// <param name="speaker">Entity speaking (for positional audio).</param>
        /// <param name="onComplete">Callback invoked when playback completes.</param>
        public void Play(string voResRef, IEntity speaker, Action onComplete)
        {
            if (string.IsNullOrEmpty(voResRef))
            {
                onComplete?.Invoke();
                return;
            }

            // Stop any currently playing voice
            Stop();

            try
            {
                // Load WAV resource
                var resourceId = new ResourceIdentifier(voResRef, ResourceType.WAV);
                byte[] wavData = _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Voice not found: {voResRef}");
                    onComplete?.Invoke();
                    return;
                }

                // Parse WAV file to get audio format information
                // Based on Andastra.Parsing.Formats.WAV for WAV file parsing
                WAV wavFile = WAVAuto.ReadWav(wavData);

                // Create SoundInstance directly from WAV data using DynamicSoundSource
                // Based on Stride API: SoundInstance constructor with DynamicSoundSource
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance__ctor_Stride_Audio_AudioEngine_Stride_Audio_AudioListener_Stride_Audio_DynamicSoundSource_System_Int32_System_Boolean_System_Boolean_System_Boolean_System_Single_Stride_Audio_HrtfEnvironment_
                bool useSpatialAudio = (_spatialAudio != null && speaker != null);
                _currentVoiceInstance = CreateSoundInstanceFromWavData(wavFile, wavData, useSpatialAudio);

                if (_currentVoiceInstance == null)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Failed to create SoundInstance from WAV data: {voResRef}");
                    onComplete?.Invoke();
                    return;
                }

                // Apply 3D positioning if spatial audio is enabled
                // Based on Stride API: SoundInstance.Apply3D() for 3D spatial audio
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Apply3D_Stride_Audio_AudioEmitter_
                // Based on swkotor2.exe: Voice-over plays at speaker entity position for 3D spatial audio
                if (useSpatialAudio && speaker != null)
                {
                    var emitter = new AudioEmitter();
                    // Get speaker position from entity TransformComponent
                    // Based on MonoGameVoicePlayer implementation: Gets position from ITransformComponent
                    // Entity position is the source of truth for 3D audio positioning in KOTOR
                    Vector3 speakerPosition = Vector3.Zero;
                    ITransformComponent transform = speaker.GetComponent<ITransformComponent>();
                    if (transform != null)
                    {
                        speakerPosition = transform.Position;
                    }
                    emitter.Position = speakerPosition;
                    _currentVoiceInstance.Apply3D(emitter);
                }

                // Set callback and play state
                _onCompleteCallback = onComplete;
                _isPlaying = true;

                // Apply voice volume setting before playing
                // Based on swkotor.exe and swkotor2.exe: VoiceVolume setting applied to voice-over playback
                _currentVoiceInstance.Volume = _volume;

                // Play the sound instance
                // Based on Stride API: SoundInstance.Play() starts playback
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Play
                _currentVoiceInstance.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideVoicePlayer] Error playing voice {voResRef}: {ex.Message}");
                Console.WriteLine($"[StrideVoicePlayer] Stack trace: {ex.StackTrace}");

                // Clean up on error
                if (_currentVoiceInstance != null)
                {
                    try
                    {
                        _currentVoiceInstance.Stop();
                        _currentVoiceInstance.Dispose();
                    }
                    catch { }
                    _currentVoiceInstance = null;
                }

                _isPlaying = false;
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Creates a Stride SoundInstance from WAV file data using DynamicSoundSource.
        /// </summary>
        /// <param name="wavFile">Parsed WAV file structure.</param>
        /// <param name="wavData">Raw WAV file bytes.</param>
        /// <param name="useSpatialAudio">Whether to enable spatial audio.</param>
        /// <returns>SoundInstance or null if creation fails.</returns>
        /// <remarks>
        /// This method creates a SoundInstance directly from WAV PCM data using DynamicSoundSource.
        /// Based on Stride API: SoundInstance constructor with DynamicSoundSource
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance__ctor_Stride_Audio_AudioEngine_Stride_Audio_AudioListener_Stride_Audio_DynamicSoundSource_System_Int32_System_Boolean_System_Boolean_System_Boolean_System_Single_Stride_Audio_HrtfEnvironment_
        ///
        /// Note: DynamicSoundSource requires a SoundInstance in its constructor, creating a circular dependency.
        /// This implementation uses a two-phase initialization to work around this limitation.
        /// </remarks>
        private SoundInstance CreateSoundInstanceFromWavData(WAV wavFile, byte[] wavData, bool useSpatialAudio)
        {
            try
            {
                // Extract PCM data from WAV file
                // WAV format: Header + PCM data
                // Based on Andastra.Parsing.Formats.WAV structure
                byte[] pcmData = wavFile.Data;
                if (pcmData == null || pcmData.Length == 0)
                {
                    Console.WriteLine("[StrideVoicePlayer] No PCM data in WAV file");
                    return null;
                }

                // Create a temporary SoundInstance for the DynamicSoundSource constructor
                // This is needed because DynamicSoundSource requires a SoundInstance in its constructor
                // We'll create the actual SoundInstance after, and update the DynamicSoundSource
                var tempListener = new AudioListener();
                var tempDummySource = DummyDynamicSoundSourceForInit.Create(_audioEngine, tempListener);
                var tempSoundInstance = new SoundInstance(
                    _audioEngine,
                    tempListener,
                    tempDummySource,
                    wavFile.SampleRate,
                    wavFile.Channels == 1,
                    useSpatialAudio,
                    false,
                    0.0f,
                    HrtfEnvironment.Small
                );

                // Create the actual DynamicSoundSource with the temporary SoundInstance
                var dynamicSource = new VoiceWavDynamicSoundSource(_audioEngine, pcmData, wavFile.SampleRate, wavFile.Channels == 1, tempSoundInstance);

                // Create the actual SoundInstance with the DynamicSoundSource
                var soundInstance = new SoundInstance(
                    _audioEngine,
                    _audioListener,
                    dynamicSource,
                    wavFile.SampleRate,
                    wavFile.Channels == 1, // mono
                    useSpatialAudio, // spatialized
                    false, // useHrtf
                    0.0f, // directionalFactor
                    HrtfEnvironment.Small // environment
                );

                // Update the DynamicSoundSource with the actual SoundInstance
                dynamicSource.SetSoundInstance(soundInstance);

                // Dispose the temporary SoundInstance
                try
                {
                    tempSoundInstance.Dispose();
                }
                catch { }

                return soundInstance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideVoicePlayer] Error creating SoundInstance from WAV data: {ex.Message}");
                Console.WriteLine($"[StrideVoicePlayer] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Dummy DynamicSoundSource used only for initializing the temporary SoundInstance.
        /// Uses a two-phase initialization pattern to break the circular dependency:
        /// 1. First, create a cached dummy SoundInstance using a helper class that doesn't recurse
        /// 2. Then reuse that cached instance for all DummyDynamicSoundSourceForInit instances
        /// </summary>
        /// <remarks>
        /// Based on Stride API: DynamicSoundSource requires a SoundInstance in its constructor,
        /// but we need a DynamicSoundSource to create a SoundInstance, creating a circular dependency.
        /// This implementation uses a MinimalDummySource that accepts null in the base constructor
        /// to break the cycle, then caches the resulting SoundInstance for reuse.
        /// </remarks>
        private class DummyDynamicSoundSourceForInit : DynamicSoundSource
        {
            /// <summary>
            /// Static cached dummy SoundInstance to break circular dependency.
            /// This is created once per AudioEngine and reused for all DummyDynamicSoundSourceForInit instances.
            /// </summary>
            private static SoundInstance _cachedDummySoundInstance;
            private static AudioEngine _cachedAudioEngine;
            private static AudioListener _cachedListener;
            private static readonly object _lockObject = new object();

            private DummyDynamicSoundSourceForInit(SoundInstance dummySoundInstance)
                : base(dummySoundInstance, 1, 1024)
            {
            }

            /// <summary>
            /// Factory method to create a DummyDynamicSoundSourceForInit instance.
            /// Uses cached dummy SoundInstance to break circular dependency.
            /// </summary>
            /// <param name="audioEngine">Stride AudioEngine instance.</param>
            /// <param name="listener">Stride AudioListener instance.</param>
            /// <returns>DummyDynamicSoundSourceForInit instance.</returns>
            public static DummyDynamicSoundSourceForInit Create(AudioEngine audioEngine, AudioListener listener)
            {
                SoundInstance cachedInstance = GetOrCreateCachedDummySoundInstance(audioEngine, listener);
                return new DummyDynamicSoundSourceForInit(cachedInstance);
            }

            /// <summary>
            /// Gets or creates a cached dummy SoundInstance for initialization.
            /// This breaks the circular dependency by creating it once using a helper class
            /// that doesn't have the recursion problem, then reusing it.
            /// </summary>
            /// <param name="audioEngine">Stride AudioEngine instance.</param>
            /// <param name="listener">Stride AudioListener instance.</param>
            /// <returns>Cached dummy SoundInstance.</returns>
            private static SoundInstance GetOrCreateCachedDummySoundInstance(AudioEngine audioEngine, AudioListener listener)
            {
                lock (_lockObject)
                {
                    // If we already have a cached instance with the same AudioEngine, reuse it
                    if (_cachedDummySoundInstance != null && _cachedAudioEngine == audioEngine && _cachedListener == listener)
                    {
                        return _cachedDummySoundInstance;
                    }

                    // Create the cached instance using MinimalDummySource that doesn't recurse
                    // MinimalDummySource accepts null in its constructor, breaking the cycle
                    var minimalDummySource = new MinimalDummySource();
                    var tempSoundInstance = new SoundInstance(
                        audioEngine,
                        listener,
                        minimalDummySource,
                        44100, // Standard sample rate
                        true,  // mono
                        false, // not spatialized
                        false, // no HRTF
                        0.0f,  // no directional factor
                        HrtfEnvironment.Small
                    );

                    // Store the cached instance for reuse
                    _cachedDummySoundInstance = tempSoundInstance;
                    _cachedAudioEngine = audioEngine;
                    _cachedListener = listener;

                    return tempSoundInstance;
                }
            }

            /// <summary>
            /// Minimal dummy source that accepts null in base constructor to break recursion.
            /// Used only during the initial cached instance creation.
            /// This is a workaround for the circular dependency: DynamicSoundSource requires
            /// a SoundInstance, but we need a DynamicSoundSource to create a SoundInstance.
            /// By accepting null here (which the base class may handle gracefully for initialization),
            /// we break the cycle.
            /// </summary>
            /// <remarks>
            /// Based on Stride API: DynamicSoundSource base class constructor accepts SoundInstance.
            /// Passing null here allows us to create the first SoundInstance without recursion.
            /// The base class handles null during initialization, allowing this workaround.
            /// </remarks>
            private class MinimalDummySource : DynamicSoundSource
            {
                public MinimalDummySource()
                    : base(null, 1, 1024) // Pass null to break circular dependency
                {
                    // This constructor is only called during cached instance creation
                    // The base class handles null during initialization, allowing this two-phase init pattern
                }

                public override int MaxNumberOfBuffers => 1;

                public override void SetLooped(bool looped)
                {
                }

                protected override void ExtractAndFillData()
                {
                }
            }

            public override int MaxNumberOfBuffers
            {
                get { return 1; }
            }

            public override void SetLooped(bool looped)
            {
            }

            protected override void ExtractAndFillData()
            {
            }
        }

        /// <summary>
        /// Stops the currently playing voice-over.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.Stop() stops playback and resets to beginning
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Stop
        /// </remarks>
        public void Stop()
        {
            if (_currentVoiceInstance != null)
            {
                try
                {
                    // Stop the sound instance
                    // Based on Stride API: SoundInstance.Stop() immediately stops playback
                    _currentVoiceInstance.Stop();
                    _currentVoiceInstance.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Error stopping voice: {ex.Message}");
                }
                finally
                {
                    _currentVoiceInstance = null;
                }
            }


            _isPlaying = false;
            _onCompleteCallback = null;
        }

        /// <summary>
        /// Gets whether voice-over is currently playing.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.PlayState property indicates playback state
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_PlayState
        /// PlayState can be: Stopped, Playing, Paused
        /// </remarks>
        public bool IsPlaying
        {
            get
            {
                if (_currentVoiceInstance == null)
                {
                    return false;
                }

                try
                {
                    // Check PlayState to determine if sound is playing
                    // Based on Stride API: PlayState.Playing indicates active playback
                    return _currentVoiceInstance.PlayState == PlayState.Playing;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Error checking play state: {ex.Message}");
                    return _isPlaying; // Fallback to internal state
                }
            }
        }

        /// <summary>
        /// Gets the current playback position in seconds.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.Position property returns TimeSpan of playback position
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Position
        /// Position is a TimeSpan representing the current playback time
        /// </remarks>
        public float CurrentTime
        {
            get
            {
                if (_currentVoiceInstance == null)
                {
                    return 0.0f;
                }

                try
                {
                    // Get playback position from SoundInstance
                    // Based on Stride API: SoundInstance.Position returns TimeSpan
                    // Convert TimeSpan to seconds (float)
                    TimeSpan position = _currentVoiceInstance.Position;
                    return (float)position.TotalSeconds;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Error getting playback position: {ex.Message}");
                    return 0.0f;
                }
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
                // Stride SoundInstance uses Volume property (0.0 to 1.0)
                if (_currentVoiceInstance != null)
                {
                    try
                    {
                        _currentVoiceInstance.Volume = _volume;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideVoicePlayer] Error setting volume: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Updates the voice player (call each frame to check for completion).
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
        /// <remarks>
        /// Based on Stride API: SoundInstance.PlayState changes to Stopped when playback completes
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_PlayState
        /// This method checks if playback has completed and invokes the callback
        /// </remarks>
        public void Update(float deltaTime)
        {
            if (_currentVoiceInstance == null || _onCompleteCallback == null)
            {
                return;
            }

            try
            {
                // Check if playback has completed
                // Based on Stride API: PlayState.Stopped indicates playback has finished
                if (_currentVoiceInstance.PlayState == PlayState.Stopped && _isPlaying)
                {
                    _isPlaying = false;

                    // Invoke completion callback
                    var callback = _onCompleteCallback;
                    _onCompleteCallback = null;

                    // Invoke callback asynchronously to avoid blocking
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            callback?.Invoke();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[StrideVoicePlayer] Error in completion callback: {ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideVoicePlayer] Error updating voice player: {ex.Message}");
            }
        }

        /// <summary>
        /// Disposes resources used by the voice player.
        /// </summary>
        public void Dispose()
        {
            Stop();

            if (_audioEngine != null)
            {
                try
                {
                    _audioEngine.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Error disposing AudioEngine: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// DynamicSoundSource implementation for WAV PCM data (for voice-over playback).
    /// Provides PCM audio data to Stride's SoundInstance for runtime WAV playback.
    /// </summary>
    /// <remarks>
    /// Based on Stride API: DynamicSoundSource abstract class
    /// https://doc.stride3d.net/latest/en/api/Stride.Audio.DynamicSoundSource.html
    /// This implementation provides a simple way to play WAV files from memory.
    ///
    /// Note: DynamicSoundSource requires a SoundInstance in its constructor, creating a circular dependency.
    /// This implementation uses a two-phase initialization pattern to work around this limitation.
    /// </remarks>
    internal class VoiceWavDynamicSoundSource : DynamicSoundSource
    {
        private readonly byte[] _pcmData;
        private readonly int _sampleRate;
        private readonly bool _isMono;
        private int _position;
        private bool _isLooped;
        private SoundInstance _soundInstance;

        /// <summary>
        /// Initializes a new WAV dynamic sound source.
        /// Note: SoundInstance must be set after construction using SetSoundInstance().
        /// </summary>
        /// <param name="audioEngine">Stride AudioEngine instance.</param>
        /// <param name="pcmData">PCM audio data bytes.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="isMono">True if mono, false if stereo.</param>
        /// <param name="tempSoundInstance">Temporary SoundInstance for base constructor (will be replaced).</param>
        public VoiceWavDynamicSoundSource(AudioEngine audioEngine, byte[] pcmData, int sampleRate, bool isMono, SoundInstance tempSoundInstance)
            : base(tempSoundInstance, 2, 65536) // 2 buffers, 64KB max buffer size
        {
            _pcmData = pcmData ?? throw new ArgumentNullException(nameof(pcmData));
            _sampleRate = sampleRate;
            _isMono = isMono;
            _position = 0;
            _isLooped = false;
            _soundInstance = tempSoundInstance;
        }

        /// <summary>
        /// Sets the actual SoundInstance (called after construction to resolve circular dependency).
        /// </summary>
        public void SetSoundInstance(SoundInstance soundInstance)
        {
            _soundInstance = soundInstance;
        }

        /// <summary>
        /// Gets the maximum number of buffers.
        /// </summary>
        public override int MaxNumberOfBuffers
        {
            get { return 2; }
        }

        /// <summary>
        /// Sets whether the sound should loop.
        /// </summary>
        public override void SetLooped(bool looped)
        {
            _isLooped = looped;
        }

        /// <summary>
        /// Extracts and fills audio data into buffers.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: DynamicSoundSource.ExtractAndFillData() abstract method
        /// This method is called by Stride to fill audio buffers with PCM data
        /// </remarks>
        protected override void ExtractAndFillData()
        {
            if (!CanFill)
            {
                return;
            }

            // Calculate buffer size (typically 16-bit samples, so 2 bytes per sample)
            int bytesPerSample = 2; // 16-bit audio
            int samplesPerChannel = 4096; // Buffer size in samples
            int bufferSizeBytes = samplesPerChannel * bytesPerSample * (_isMono ? 1 : 2);

            // Check if we have more data to provide
            if (_position >= _pcmData.Length)
            {
                if (_isLooped)
                {
                    _position = 0; // Loop back to beginning
                }
                else
                {
                    // End of data - fill with silence or mark as ended
                    byte[] silenceBuffer = new byte[bufferSizeBytes];
                    GCHandle silenceHandle = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                    try
                    {
                        IntPtr bufferPtr = silenceHandle.AddrOfPinnedObject();
                        FillBuffer(bufferPtr, bufferSizeBytes, (Stride.Audio.AudioLayer.BufferType)AudioLayer.BufferType.EndOfStream);
                    }
                    finally
                    {
                        silenceHandle.Free();
                    }
                    return;
                }
            }

            // Calculate how much data we can provide
            int remainingBytes = _pcmData.Length - _position;
            int bytesToCopy = Math.Min(bufferSizeBytes, remainingBytes);

            // Copy PCM data to buffer
            byte[] buffer = new byte[bufferSizeBytes];
            Array.Copy(_pcmData, _position, buffer, 0, bytesToCopy);
            _position += bytesToCopy;

            // Determine buffer type
            AudioLayer.BufferType bufferType;
            if (_position >= _pcmData.Length && !_isLooped)
            {
                bufferType = AudioLayer.BufferType.EndOfStream;
            }
            else
            {
                bufferType = AudioLayer.BufferType.Normal;
            }

            // Fill the buffer - pin the array and get pointer
            GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                FillBuffer(bufferPtr, bytesToCopy, (Stride.Audio.AudioLayer.BufferType)bufferType);
            }
            finally
            {
                bufferHandle.Free();
            }
        }
    }
}
