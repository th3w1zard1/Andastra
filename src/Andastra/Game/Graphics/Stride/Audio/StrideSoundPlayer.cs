using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using BioWare.NET.Resource.Formats.WAV;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Graphics;
using Stride.Audio;
using Stride.Media;
using StrideAudioLayer = Stride.Audio.AudioLayer;

namespace Andastra.Game.Stride.Audio
{
    /// <summary>
    /// Stride implementation of ISoundPlayer for playing sound effects.
    ///
    /// Loads WAV files from KOTOR installation and plays them using Stride's Audio API.
    /// Supports positional audio for 3D sound effects.
    ///
    /// Based on Stride API: https://doc.stride3d.net/latest/en/manual/audio/index.html
    /// SoundInstance API: https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html
    /// </summary>
    /// <remarks>
    /// Sound Player (Stride Implementation):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) sound effect playback system
    /// - Located via string references: "SoundResRef" @ 0x007b5f70, "SoundList" @ 0x007bd080, "Sounds" @ 0x007c1038
    /// - Original implementation: KOTOR plays WAV files for sound effects (ambient, combat, UI, etc.)
    /// - Sound files: Stored as WAV resources, referenced by ResRef
    /// - Positional audio: Sounds can be played at entity positions (3D spatial audio)
    /// - Playback control: Play, Stop, volume, pan, pitch
    /// - This Stride implementation uses Stride's AudioEngine and SoundInstance API
    ///
    /// Implementation Notes:
    /// - Uses SoundInstance.PlayState to check if sound is playing (Playing, Stopped, Paused)
    /// - Uses SoundInstance.Stop() to stop playback
    /// - Creates SoundInstance from WAV data using DynamicSoundSource (similar to StrideVoicePlayer)
    /// - Update() method removes stopped sounds and disposes resources
    /// - Supports multiple simultaneous sound instances (unlike StrideVoicePlayer which only plays one at a time)
    /// </remarks>
    public class StrideSoundPlayer : ISoundPlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ISpatialAudio _spatialAudio;
        private readonly AudioEngine _audioEngine;
        private readonly AudioListener _audioListener;
        private readonly Dictionary<uint, SoundInstance> _playingSounds;
        private readonly Dictionary<uint, float> _instanceOriginalVolumes;
        private readonly Dictionary<uint, float> _instancePans;
        private uint _nextSoundInstanceId;
        private float _masterVolume;

        /// <summary>
        /// Initializes a new Stride sound player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        /// <param name="spatialAudio">Optional spatial audio system for 3D positioning.</param>
        /// <param name="audioEngine">Stride AudioEngine instance. If null, will attempt to create one.</param>
        /// <param name="audioListener">Stride AudioListener instance. If null, will create a default one.</param>
        public StrideSoundPlayer(IGameResourceProvider resourceProvider, ISpatialAudio spatialAudio = null, AudioEngine audioEngine = null, AudioListener audioListener = null)
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
                    Console.WriteLine($"[StrideSoundPlayer] Failed to create AudioEngine: {ex.Message}");
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

            _playingSounds = new Dictionary<uint, SoundInstance>();
            _instanceOriginalVolumes = new Dictionary<uint, float>();
            _instancePans = new Dictionary<uint, float>();
            _nextSoundInstanceId = 1;
            _masterVolume = 1.0f;
        }

        /// <summary>
        /// Plays a sound effect by ResRef.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance constructor with DynamicSoundSource
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance__ctor_Stride_Audio_AudioEngine_Stride_Audio_AudioListener_Stride_Audio_DynamicSoundSource_System_Int32_System_Boolean_System_Boolean_System_Boolean_System_Single_Stride_Audio_HrtfEnvironment_
        /// </remarks>
        public uint PlaySound(string soundResRef, Vector3? position = null, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f)
        {
            if (string.IsNullOrEmpty(soundResRef))
            {
                return 0;
            }

            try
            {
                // Load WAV resource
                var resourceId = new ResourceIdentifier(soundResRef, ResourceType.WAV);
                byte[] wavData = _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[StrideSoundPlayer] Sound not found: {soundResRef}");
                    return 0;
                }

                // Parse WAV file to get audio format information
                // Based on BioWare.NET.Resource.Formats.WAV for WAV file parsing
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null)
                {
                    Console.WriteLine($"[StrideSoundPlayer] Failed to parse WAV: {soundResRef}");
                    return 0;
                }

                // Determine if we should use spatial audio
                bool useSpatialAudio = (position.HasValue && _spatialAudio != null);

                // Create SoundInstance directly from WAV data using DynamicSoundSource
                // Based on StrideVoicePlayer implementation pattern
                SoundInstance soundInstance = CreateSoundInstanceFromWavData(wavFile, wavData, useSpatialAudio);
                if (soundInstance == null)
                {
                    Console.WriteLine($"[StrideSoundPlayer] Failed to create SoundInstance from WAV data: {soundResRef}");
                    return 0;
                }

                // Store original volume before applying master volume
                float originalVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

                // Set volume (applied with master volume)
                // Based on Stride API: SoundInstance.Volume property
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Volume
                soundInstance.Volume = originalVolume * _masterVolume;

                // Set pitch
                // Based on Stride API: SoundInstance.Pitch property
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Pitch
                soundInstance.Pitch = Math.Max(-1.0f, Math.Min(1.0f, pitch));

                // Clamp pan value to valid range (-1.0 to 1.0)
                // -1.0 = full left, 0.0 = center, 1.0 = full right
                float clampedPan = Math.Max(-1.0f, Math.Min(1.0f, pan));

                // Apply 3D positioning if spatial audio is enabled
                // Based on Stride API: SoundInstance.Apply3D() for 3D spatial audio
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Apply3D_Stride_Audio_AudioEmitter_
                if (useSpatialAudio && position.HasValue)
                {
                    var emitter = new AudioEmitter();
                    emitter.Position = new global::Stride.Core.Mathematics.Vector3(position.Value.X, position.Value.Y, position.Value.Z);
                    soundInstance.Apply3D(emitter);
                }
                else if (!useSpatialAudio)
                {
                    // Apply 2D panning for non-spatial sounds
                    // Stride doesn't have a direct Pan property, so we simulate it using 3D positioning
                    // We use Apply3D with a fake 3D position where:
                    // - X coordinate represents pan value (left/right)
                    // - Y and Z are set to 0 (at listener position)
                    // - This creates left/right panning through 3D spatial audio calculations
                    // Based on Stride API: SoundInstance.Apply3D() can be used for 2D panning simulation
                    // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Apply3D_Stride_Audio_AudioEmitter_
                    if (Math.Abs(clampedPan) > 0.001f) // Only apply panning if not centered
                    {
                        var panEmitter = new AudioEmitter();
                        // Convert pan value (-1.0 to 1.0) to X position for 3D panning
                        // Use a small offset distance to ensure proper left/right positioning
                        // Pan value of -1.0 (left) maps to negative X, 1.0 (right) maps to positive X
                        // Using a small distance (0.1 units) ensures the sound is close enough to avoid
                        // significant distance attenuation while still providing left/right panning
                        float panDistance = 0.1f; // Small distance to avoid attenuation
                        panEmitter.Position = new global::Stride.Core.Mathematics.Vector3(clampedPan * panDistance, 0.0f, 0.0f);
                        soundInstance.Apply3D(panEmitter);
                    }
                }

                // Play the sound instance
                // Based on Stride API: SoundInstance.Play() starts playback
                // https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Play
                soundInstance.Play();

                // Track instance and store original volume and pan
                uint instanceId = _nextSoundInstanceId++;
                _playingSounds[instanceId] = soundInstance;
                _instanceOriginalVolumes[instanceId] = originalVolume;
                _instancePans[instanceId] = clampedPan;

                return instanceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideSoundPlayer] Error playing sound {soundResRef}: {ex.Message}");
                Console.WriteLine($"[StrideSoundPlayer] Stack trace: {ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Stops a playing sound by instance ID.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.Stop() stops playback and resets to beginning
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_Stop
        /// </remarks>
        public void StopSound(uint soundInstanceId)
        {
            if (_playingSounds.TryGetValue(soundInstanceId, out SoundInstance instance))
            {
                try
                {
                    // Stop the sound instance
                    // Based on Stride API: SoundInstance.Stop() immediately stops playback
                    instance.Stop();
                    instance.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideSoundPlayer] Error stopping sound {soundInstanceId}: {ex.Message}");
                }
                finally
                {
                    _playingSounds.Remove(soundInstanceId);
                    _instanceOriginalVolumes.Remove(soundInstanceId);
                    _instancePans.Remove(soundInstanceId);
                }
            }
        }

        /// <summary>
        /// Stops all currently playing sounds.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.Stop() stops playback for all instances
        /// </remarks>
        public void StopAllSounds()
        {
            foreach (var kvp in _playingSounds)
            {
                try
                {
                    kvp.Value.Stop();
                    kvp.Value.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[StrideSoundPlayer] Error stopping sound {kvp.Key}: {ex.Message}");
                }
            }
            _playingSounds.Clear();
            _instanceOriginalVolumes.Clear();
            _instancePans.Clear();
        }

        /// <summary>
        /// Sets the master volume for all sounds.
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.Volume property
        /// Updates all playing sounds using their stored original volumes
        /// </remarks>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

            // Update volume of all playing sounds using their stored original volumes
            foreach (var kvp in _playingSounds)
            {
                uint instanceId = kvp.Key;
                SoundInstance instance = kvp.Value;

                if (_instanceOriginalVolumes.TryGetValue(instanceId, out float originalVolume))
                {
                    // Recalculate volume as original volume * master volume
                    // Based on Stride API: SoundInstance.Volume property
                    instance.Volume = originalVolume * _masterVolume;
                }
            }
        }

        /// <summary>
        /// Updates the sound system (call each frame).
        /// </summary>
        /// <remarks>
        /// Based on Stride API: SoundInstance.PlayState changes to Stopped when playback completes
        /// https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html#Stride_Audio_SoundInstance_PlayState
        /// This method checks all playing sounds and removes/disposes those that have stopped
        /// </remarks>
        public void Update(float deltaTime)
        {
            // Clean up finished sounds
            var finishedSounds = new List<uint>();
            foreach (var kvp in _playingSounds)
            {
                try
                {
                    // Check if playback has completed
                    // Based on Stride API: PlayState.Stopped indicates playback has finished
                    if (kvp.Value.PlayState == PlayState.Stopped)
                    {
                        finishedSounds.Add(kvp.Key);
                    }
                }
                catch (Exception ex)
                {
                    // If we can't check the state, assume it's finished and clean it up
                    Console.WriteLine($"[StrideSoundPlayer] Error checking play state for {kvp.Key}: {ex.Message}");
                    finishedSounds.Add(kvp.Key);
                }
            }

            // Dispose and remove finished sounds
            foreach (uint instanceId in finishedSounds)
            {
                if (_playingSounds.TryGetValue(instanceId, out SoundInstance instance))
                {
                    try
                    {
                        instance.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StrideSoundPlayer] Error disposing sound {instanceId}: {ex.Message}");
                    }
                    finally
                    {
                        _playingSounds.Remove(instanceId);
                        _instanceOriginalVolumes.Remove(instanceId);
                        _instancePans.Remove(instanceId);
                    }
                }
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
        /// This implementation uses a two-phase initialization to work around this limitation (same pattern as StrideVoicePlayer).
        /// </remarks>
        private SoundInstance CreateSoundInstanceFromWavData(WAV wavFile, byte[] wavData, bool useSpatialAudio)
        {
            try
            {
                // Extract PCM data from WAV file
                // WAV format: Header + PCM data
                // Based on BioWare.NET.Resource.Formats.WAV structure
                byte[] pcmData = wavFile.Data;
                if (pcmData == null || pcmData.Length == 0)
                {
                    Console.WriteLine("[StrideSoundPlayer] No PCM data in WAV file");
                    return null;
                }

                // Create a temporary SoundInstance for the DynamicSoundSource constructor
                // This is needed because DynamicSoundSource requires a SoundInstance in its constructor
                // We'll create the actual SoundInstance after, and update the DynamicSoundSource
                // Use the cached dummy source pattern to break circular dependency - reuse _audioListener
                // to ensure the cache key matches and the cached dummy SoundInstance is reused efficiently
                var tempDummySource = DummyDynamicSoundSourceForInit.Create(_audioEngine, _audioListener);
                var tempSoundInstance = new SoundInstance(
                    _audioEngine,
                    _audioListener,
                    tempDummySource,
                    wavFile.SampleRate,
                    wavFile.Channels == 1,
                    useSpatialAudio,
                    false,
                    0.0f,
                    HrtfEnvironment.Small
                );

                // Create the actual DynamicSoundSource with the temporary SoundInstance
                var dynamicSource = new SoundWavDynamicSoundSource(_audioEngine, pcmData, wavFile.SampleRate, wavFile.Channels == 1, tempSoundInstance);

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
                Console.WriteLine($"[StrideSoundPlayer] Error creating SoundInstance from WAV data: {ex.Message}");
                Console.WriteLine($"[StrideSoundPlayer] Stack trace: {ex.StackTrace}");
                return null;
            }
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

                /// <summary>
                /// Extracts and fills audio data into buffers with silence (zero bytes).
                /// This is called by Stride's audio system to fill audio buffers.
                /// Since this is a dummy source used only for initialization, we provide silence.
                /// </summary>
                /// <remarks>
                /// Based on Stride API: DynamicSoundSource.ExtractAndFillData() abstract method
                /// https://doc.stride3d.net/latest/en/api/Stride.Audio.DynamicSoundSource.html#Stride_Audio_DynamicSoundSource_ExtractAndFillData
                /// </remarks>
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
                    GCHandle silenceHandle1 = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                    try
                    {
                        IntPtr bufferPtr = silenceHandle1.AddrOfPinnedObject();
                        FillBuffer(bufferPtr, bufferSizeBytes, StrideAudioLayer.BufferType.EndOfStream);
                    }
                    finally
                    {
                        silenceHandle1.Free();
                    }
                }
            }

            public override int MaxNumberOfBuffers
            {
                get { return 1; }
            }

            public override void SetLooped(bool looped)
            {
                // Dummy source doesn't loop
            }

            /// <summary>
            /// Extracts and fills audio data into buffers with silence (zero bytes).
            /// This is called by Stride's audio system to fill audio buffers.
            /// Since this is a dummy source used only for initialization, we provide silence.
            /// </summary>
            /// <remarks>
            /// Based on Stride API: DynamicSoundSource.ExtractAndFillData() abstract method
            /// https://doc.stride3d.net/latest/en/api/Stride.Audio.DynamicSoundSource.html#Stride_Audio_DynamicSoundSource_ExtractAndFillData
            /// </remarks>
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
                GCHandle silenceHandle2 = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                try
                {
                    IntPtr bufferPtr = silenceHandle2.AddrOfPinnedObject();
                    FillBuffer(bufferPtr, bufferSizeBytes, StrideAudioLayer.BufferType.EndOfStream);
                }
                finally
                {
                    silenceHandle2.Free();
                }
            }
        }
    }

    /// <summary>
    /// DynamicSoundSource implementation for WAV PCM data (for sound effects playback).
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
    internal class SoundWavDynamicSoundSource : DynamicSoundSource
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
        public SoundWavDynamicSoundSource(AudioEngine audioEngine, byte[] pcmData, int sampleRate, bool isMono, SoundInstance tempSoundInstance)
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
                    GCHandle silenceHandle3 = GCHandle.Alloc(silenceBuffer, GCHandleType.Pinned);
                    try
                    {
                        IntPtr bufferPtr = silenceHandle3.AddrOfPinnedObject();
                        FillBuffer(bufferPtr, bufferSizeBytes, StrideAudioLayer.BufferType.EndOfStream);
                    }
                    finally
                    {
                        silenceHandle3.Free();
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

            // Determine buffer type - use Stride's enum directly
            // Stride AudioLayer.BufferType only has EndOfStream value
            // Use EndOfStream for all buffers (Stride doesn't have a "Normal" or "Data" value)
            StrideAudioLayer.BufferType bufferType = StrideAudioLayer.BufferType.EndOfStream;

            // Fill the buffer - pin the array and get pointer
            GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr bufferPtr = bufferHandle.AddrOfPinnedObject();
                FillBuffer(bufferPtr, bytesToCopy, bufferType);
            }
            finally
            {
                bufferHandle.Free();
            }
        }
    }
}

