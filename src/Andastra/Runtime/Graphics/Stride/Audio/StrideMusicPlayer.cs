using System;
using System.Runtime.InteropServices;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Stride.Audio;
using SoundPlayState = Stride.Media.PlayState;
using StrideAudioLayer = Stride.Audio.AudioLayer;

namespace Andastra.Runtime.Stride.Audio
{
    /// <summary>
    /// Stride implementation of IMusicPlayer for playing background music.
    ///
    /// Loads WAV files from KOTOR installation and plays them in a loop using Stride's Audio API.
    ///
    /// Based on Stride API: https://doc.stride3d.net/latest/en/manual/audio/index.html
    /// SoundInstance API with looping enabled via DynamicSoundSource
    /// </summary>
    /// <remarks>
    /// Music Player (Stride Implementation):
    /// - Based on swkotor.exe: FUN_005f9af0 @ 0x005f9af0 (music playback function)
    /// - Located via string references: "mus_theme_cult" @ 0x0074ebfc (K1 main menu music)
    /// - "mus_sion" @ 0x007c75e4 (K2 main menu music), "mus_theme_rep" (character creation music)
    /// - Original implementation: KOTOR plays WAV files for background music (looping)
    /// - Music files: Stored as WAV resources, referenced by ResRef (e.g., "mus_theme_cult")
    /// - Playback control: Play, Stop, Pause, Resume, volume control
    /// - Looping: Music loops continuously until stopped
    /// - This Stride implementation uses SoundInstance with DynamicSoundSource (same pattern as StrideSoundPlayer)
    /// </remarks>
    public class StrideMusicPlayer : IMusicPlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly AudioEngine _audioEngine;
        private readonly AudioListener _audioListener;
        private SoundInstance _currentMusicInstance;
        private string _currentMusicResRef;
        private float _volume;
        private bool _isPaused;

        /// <summary>
        /// Initializes a new Stride music player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        /// <param name="audioEngine">Stride AudioEngine instance. If null, will attempt to create one.</param>
        /// <param name="audioListener">Stride AudioListener instance. If null, will create a default one.</param>
        public StrideMusicPlayer(IGameResourceProvider resourceProvider, AudioEngine audioEngine = null, AudioListener audioListener = null)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));

            // Initialize AudioEngine if not provided
            if (audioEngine == null)
            {
                try
                {
                    _audioEngine = AudioEngineFactory.NewAudioEngine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideMusicPlayer] Failed to create AudioEngine: {ex.Message}");
                    throw new InvalidOperationException("Failed to initialize Stride AudioEngine", ex);
                }
            }
            else
            {
                _audioEngine = audioEngine;
            }

            // Initialize AudioListener if not provided
            _audioListener = audioListener ?? new AudioListener();

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
                       _currentMusicInstance.PlayState == SoundPlayState.Playing;
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
                _currentMusicInstance.PlayState == SoundPlayState.Playing)
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
                    Console.WriteLine($"[StrideMusicPlayer] Music not found: {musicResRef}");
                    return false;
                }

                // Parse WAV file to get audio format information
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null)
                {
                    Console.WriteLine($"[StrideMusicPlayer] Failed to parse WAV: {musicResRef}");
                    return false;
                }

                // Create SoundInstance from WAV data using DynamicSoundSource (same pattern as StrideSoundPlayer)
                SoundInstance soundInstance = CreateSoundInstanceFromWavData(wavFile, wavData);
                if (soundInstance == null)
                {
                    Console.WriteLine($"[StrideMusicPlayer] Failed to create SoundInstance from WAV data: {musicResRef}");
                    return false;
                }

                // Set volume
                soundInstance.Volume = Math.Max(0.0f, Math.Min(1.0f, volume));
                _volume = soundInstance.Volume;

                // Play music (looping is handled by DynamicSoundSource)
                soundInstance.Play();
                _currentMusicInstance = soundInstance;
                _currentMusicResRef = musicResRef;
                _isPaused = false;

                Console.WriteLine($"[StrideMusicPlayer] Playing music: {musicResRef} (looping)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideMusicPlayer] Exception playing music: {ex.Message}");
                Console.WriteLine($"[StrideMusicPlayer] Stack trace: {ex.StackTrace}");

                // Clean up on error
                if (_currentMusicInstance != null)
                {
                    _currentMusicInstance.Stop();
                    _currentMusicInstance.Dispose();
                    _currentMusicInstance = null;
                }
                _currentMusicResRef = null;
                return false;
            }
        }

        /// <summary>
        /// Creates a Stride SoundInstance from WAV file data using DynamicSoundSource.
        /// Uses the same pattern as StrideSoundPlayer for consistency.
        /// </summary>
        /// <param name="wavFile">Parsed WAV file structure.</param>
        /// <param name="wavData">Raw WAV file bytes.</param>
        /// <returns>SoundInstance or null if creation fails.</returns>
        private SoundInstance CreateSoundInstanceFromWavData(WAV wavFile, byte[] wavData)
        {
            try
            {
                // Extract PCM data from WAV file
                byte[] pcmData = wavFile.Data;
                if (pcmData == null || pcmData.Length == 0)
                {
                    Console.WriteLine("[StrideMusicPlayer] No PCM data in WAV file");
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
                    false, // No spatial audio for music
                    false,
                    0.0f,
                    HrtfEnvironment.Small
                );

                // Create the actual DynamicSoundSource with the temporary SoundInstance
                // Use MusicWavDynamicSoundSource which has looping enabled
                var dynamicSource = new MusicWavDynamicSoundSource(_audioEngine, pcmData, wavFile.SampleRate, wavFile.Channels == 1, tempSoundInstance);

                // Create the actual SoundInstance with the DynamicSoundSource
                var soundInstance = new SoundInstance(
                    _audioEngine,
                    _audioListener,
                    dynamicSource,
                    wavFile.SampleRate,
                    wavFile.Channels == 1, // mono
                    false, // No spatial audio for music
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
                Console.WriteLine($"[StrideMusicPlayer] Error creating SoundInstance from WAV data: {ex.Message}");
                Console.WriteLine($"[StrideMusicPlayer] Stack trace: {ex.StackTrace}");
                return null;
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

            _currentMusicResRef = null;
            _isPaused = false;
        }

        /// <summary>
        /// Pauses music playback (can be resumed with Resume()).
        /// </summary>
        public void Pause()
        {
            if (_currentMusicInstance != null && _currentMusicInstance.PlayState == SoundPlayState.Playing)
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
                _currentMusicInstance.Play();
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

        /// <summary>
        /// Dummy DynamicSoundSource used only for initializing temporary SoundInstances.
        /// Uses a two-phase initialization pattern to break the circular dependency:
        /// 1. First, create a cached dummy SoundInstance using MinimalDummySource that doesn't recurse
        /// 2. Then reuse that cached instance for all DummyDynamicSoundSourceForInit instances
        ///
        /// This workaround is necessary because Stride's DynamicSoundSource constructor requires
        /// a SoundInstance parameter, but to create a SoundInstance, you need a DynamicSoundSource,
        /// creating a circular dependency. The MinimalDummySource breaks this cycle by accepting
        /// null in its constructor (which the base class handles during initialization), allowing
        /// the first SoundInstance to be created. This cached instance is then reused for all
        /// subsequent DummyDynamicSoundSourceForInit instances.
        ///
        /// Based on Stride API: DynamicSoundSource base class
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.DynamicSoundSource.html
        /// </summary>
        private class DummyDynamicSoundSourceForInit : DynamicSoundSource
        {
            /// <summary>
            /// Static cached dummy SoundInstance to break circular dependency.
            /// This is created once per AudioEngine/Listener pair and reused for all
            /// DummyDynamicSoundSourceForInit instances.
            /// </summary>
            private static SoundInstance _cachedDummySoundInstance;
            private static AudioEngine _cachedAudioEngine;
            private static AudioListener _cachedListener;
            private static readonly object _lockObject = new object();

            private DummyDynamicSoundSourceForInit(SoundInstance dummySoundInstance)
                : base(dummySoundInstance, 1, 1024)
            {
            }

            public override int MaxNumberOfBuffers => 1;

            public override void SetLooped(bool looped)
            {
                // Dummy source doesn't loop
            }

            protected override void ExtractAndFillData()
            {
                if (!CanFill)
                {
                    return;
                }

                // Calculate buffer size for mono 16-bit audio at 44100 Hz
                // Standard buffer size: 4096 samples * 2 bytes per sample * 1 channel (mono)
                int bytesPerSample = 2; // 16-bit audio = 2 bytes per sample
                int samplesPerChannel = 4096; // Standard buffer size in samples
                int bufferSizeBytes = samplesPerChannel * bytesPerSample * 1; // 1 channel (mono)

                // Fill buffer with silence (zero bytes) and mark as end of stream
                // This is a dummy source, so we always provide silence
                byte[] silenceBuffer = new byte[bufferSizeBytes];
                GCHandle silenceHandle = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                try
                {
                    IntPtr bufferPtr = silenceHandle.AddrOfPinnedObject();
                    FillBuffer(bufferPtr, bufferSizeBytes, StrideAudioLayer.BufferType.EndOfStream);
                }
                finally
                {
                    silenceHandle.Free();
                }
            }

            /// <summary>
            /// Factory method to create a DummyDynamicSoundSourceForInit instance.
            /// Uses cached dummy SoundInstance to break circular dependency.
            /// </summary>
            /// <param name="audioEngine">Stride AudioEngine instance.</param>
            /// <param name="listener">AudioListener instance.</param>
            /// <returns>New DummyDynamicSoundSourceForInit instance.</returns>
            public static DummyDynamicSoundSourceForInit Create(AudioEngine audioEngine, AudioListener listener)
            {
                SoundInstance cachedInstance = GetOrCreateCachedDummySoundInstance(audioEngine, listener);
                return new DummyDynamicSoundSourceForInit(cachedInstance);
            }

            /// <summary>
            /// Gets or creates a cached dummy SoundInstance for initialization.
            /// This breaks the circular dependency by creating it once using MinimalDummySource
            /// that doesn't have the recursion problem, then reusing it for all subsequent
            /// DummyDynamicSoundSourceForInit instances.
            /// </summary>
            /// <param name="audioEngine">Stride AudioEngine instance.</param>
            /// <param name="listener">AudioListener instance.</param>
            /// <returns>Cached dummy SoundInstance for initialization.</returns>
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
            ///
            /// This is a workaround for the circular dependency: DynamicSoundSource requires
            /// a SoundInstance, but we need a DynamicSoundSource to create a SoundInstance.
            /// By passing null here (which the base class handles gracefully during initialization),
            /// we break the cycle. The base DynamicSoundSource constructor accepts null for
            /// the SoundInstance parameter during initialization, allowing this two-phase init pattern.
            ///
            /// Based on Stride API: DynamicSoundSource base class constructor
            /// https://doc.stride3d.net/latest/en/api/Stride.Audio.DynamicSoundSource.html
            /// </summary>
            private class MinimalDummySource : DynamicSoundSource
            {
                public MinimalDummySource()
                    : base(null, 1, 1024) // Pass null to break circular dependency - base class handles this during init
                {
                    // This constructor is only called during cached instance creation.
                    // The base DynamicSoundSource class handles null SoundInstance during
                    // initialization, allowing this two-phase init pattern to work.
                }

                public override int MaxNumberOfBuffers => 1;

                public override void SetLooped(bool looped)
                {
                    // Dummy source doesn't loop
                }

                protected override void ExtractAndFillData()
                {
                    if (!CanFill)
                    {
                        return;
                    }

                    // Calculate buffer size for mono 16-bit audio at 44100 Hz
                    // Standard buffer size: 4096 samples * 2 bytes per sample * 1 channel (mono)
                    int bytesPerSample = 2; // 16-bit audio = 2 bytes per sample
                    int samplesPerChannel = 4096; // Standard buffer size in samples
                    int bufferSizeBytes = samplesPerChannel * bytesPerSample * 1; // 1 channel (mono)

                    // Fill buffer with silence (zero bytes) and mark as end of stream
                    // This is a dummy source, so we always provide silence
                    byte[] silenceBuffer = new byte[bufferSizeBytes];
                    GCHandle silenceHandle4 = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                    try
                    {
                        IntPtr bufferPtr = silenceHandle4.AddrOfPinnedObject();
                        FillBuffer(bufferPtr, bufferSizeBytes, StrideAudioLayer.BufferType.EndOfStream);
                    }
                    finally
                    {
                        silenceHandle4.Free();
                    }
                }
            }
        }
    }

    /// <summary>
    /// DynamicSoundSource implementation for WAV PCM data (for music playback with looping).
    /// Provides PCM audio data to Stride's SoundInstance for runtime WAV playback.
    /// Similar to SoundWavDynamicSoundSource but always loops.
    /// </summary>
    /// <remarks>
    /// Based on Stride API: DynamicSoundSource abstract class
    /// This implementation provides a simple way to play WAV files from memory with looping.
    /// </remarks>
    internal class MusicWavDynamicSoundSource : DynamicSoundSource
    {
        private readonly byte[] _pcmData;
        private readonly int _sampleRate;
        private readonly bool _isMono;
        private int _position;
        private SoundInstance _soundInstance;

        /// <summary>
        /// Initializes a new WAV dynamic sound source for music (always loops).
        /// Note: SoundInstance must be set after construction using SetSoundInstance().
        /// </summary>
        /// <param name="audioEngine">Stride AudioEngine instance.</param>
        /// <param name="pcmData">PCM audio data bytes.</param>
        /// <param name="sampleRate">Sample rate in Hz.</param>
        /// <param name="isMono">True if mono, false if stereo.</param>
        /// <param name="tempSoundInstance">Temporary SoundInstance for base constructor (will be replaced).</param>
        public MusicWavDynamicSoundSource(AudioEngine audioEngine, byte[] pcmData, int sampleRate, bool isMono, SoundInstance tempSoundInstance)
            : base(tempSoundInstance, 2, 65536) // 2 buffers, 64KB max buffer size
        {
            _pcmData = pcmData ?? throw new ArgumentNullException(nameof(pcmData));
            _sampleRate = sampleRate;
            _isMono = isMono;
            _position = 0;
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
        /// Sets whether the sound should loop (always true for music).
        /// </summary>
        public override void SetLooped(bool looped)
        {
            // Music always loops, ignore parameter
        }

        /// <summary>
        /// Extracts and fills audio data into buffers.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: DynamicSoundSource.ExtractAndFillData() abstract method
        /// This method is called by Stride to fill audio buffers with PCM data
        /// Music always loops, so when we reach the end, we reset to the beginning
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
                // Music always loops - reset to beginning
                _position = 0;
            }

            // Calculate how much data we can provide
            int remainingBytes = _pcmData.Length - _position;
            int bytesToCopy = Math.Min(bufferSizeBytes, remainingBytes);

            // Copy PCM data to buffer
            byte[] buffer = new byte[bufferSizeBytes];
            Array.Copy(_pcmData, _position, buffer, 0, bytesToCopy);
            _position += bytesToCopy;

            // Music always loops, so buffer type is always Normal
            // Use Stride's BufferType directly
            AudioLayer.BufferType bufferType = AudioLayer.BufferType.Normal;

            // Fill the buffer - pin the array and get pointer
            GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                FillBuffer(bufferPtr, bytesToCopy, (StrideAudioLayer.BufferType)(int)bufferType);
            }
            finally
            {
                bufferHandle.Free();
            }
        }
    }
}

