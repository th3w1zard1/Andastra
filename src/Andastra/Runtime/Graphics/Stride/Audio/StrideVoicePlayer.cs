using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Andastra.Runtime.Graphics;
using Stride.Audio;
using SoundPlayState = Stride.Media.PlayState;
using StrideAudioLayer = Stride.Audio.AudioLayer;

namespace Andastra.Runtime.Stride.Audio
{
    /// <summary>
    /// Stride implementation of IVoicePlayer for playing voice-over audio.
    ///
    /// Loads WAV files from KOTOR installation and plays them using Stride's Audio API.
    /// Supports positional audio for 3D voice-over positioning.
    ///
    /// Based on Stride API: https://doc.stride3d.net/latest/en/manual/audio/index.html
    /// SoundInstance API: https://doc.stride3d.net/latest/en/api/Stride.Audio.SoundInstance.html
    /// </summary>
    /// <remarks>
    /// Voice Player (Stride Implementation):
    /// - Based on swkotor2.exe voice-over system
    /// - Located via string references: Voice-over playback functions
    /// - Original implementation: Plays voice-over audio files (WAV format) during dialogue
    /// - Voice-over files: Stored in VO folder, referenced by dialogue entries (VoiceOverId)
    /// - Positional audio: 3D position used for spatial audio positioning
    /// - Multiple simultaneous voices: Supports multiple voice instances with IDs
    /// - This Stride implementation uses SoundInstance with DynamicSoundSource (same pattern as StrideSoundPlayer)
    /// </remarks>
    public class StrideVoicePlayer : IVoicePlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ISpatialAudio _spatialAudio;
        private readonly AudioEngine _audioEngine;
        private readonly AudioListener _audioListener;
        private readonly Dictionary<uint, VoiceInstance> _voiceInstances;
        private uint _nextVoiceInstanceId;
        private float _masterVolume;

        private class VoiceInstance
        {
            public SoundInstance SoundInstance;
            public DynamicSoundSource DynamicSoundSource;
            public uint EmitterId;
            public Vector3? Position;
            public float Volume;
            public float Pitch;
            public float Pan;
            public DateTime PlaybackStartTime;
            public float Duration;
        }

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
            _audioListener = audioListener ?? new AudioListener();

            _voiceInstances = new Dictionary<uint, VoiceInstance>();
            _nextVoiceInstanceId = 1;
            _masterVolume = 1.0f;
        }

        /// <summary>
        /// Plays a voice-over audio file by ResRef.
        /// </summary>
        /// <param name="voiceResRef">The voice resource reference (e.g., "n_darthmalak01").</param>
        /// <param name="position">Optional 3D position for spatial audio. If null, plays as 2D sound.</param>
        /// <param name="volume">Volume (0.0 to 1.0). This will be multiplied by the master voice volume.</param>
        /// <param name="pitch">Pitch adjustment (-1.0 to 1.0).</param>
        /// <param name="pan">Stereo panning (-1.0 left to 1.0 right).</param>
        /// <returns>Voice instance ID for controlling playback, or 0 if failed.</returns>
        public uint PlayVoice(string voiceResRef, Vector3? position = null, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f)
        {
            if (string.IsNullOrEmpty(voiceResRef))
            {
                return 0;
            }

            try
            {
                // Load WAV file from resource provider
                byte[] wavData = _resourceProvider.GetResourceBytes(new ResourceIdentifier(voiceResRef, ResourceType.WAV));
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Voice-over not found: {voiceResRef}");
                    return 0;
                }

                // Parse WAV file to get format information
                WAV wavFile = WAVAuto.ReadWav(wavData);
                if (wavFile == null || wavFile.Data == null || wavFile.Data.Length == 0)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Failed to parse WAV file: {voiceResRef}");
                    return 0;
                }

                // Calculate sound duration
                float duration = (float)wavFile.Data.Length / (wavFile.SampleRate * wavFile.Channels * (wavFile.BitsPerSample / 8));

                // Create SoundInstance using the same pattern as StrideSoundPlayer
                bool useSpatialAudio = _spatialAudio != null && position.HasValue;
                SoundInstance soundInstance = CreateSoundInstanceFromWavData(wavFile, wavData, useSpatialAudio);
                if (soundInstance == null)
                {
                    Console.WriteLine($"[StrideVoicePlayer] Failed to create SoundInstance from WAV data: {voiceResRef}");
                    return 0;
                }

                // Create voice instance
                uint instanceId = _nextVoiceInstanceId++;
                var voiceInstance = new VoiceInstance
                {
                    SoundInstance = soundInstance,
                    Position = position,
                    Volume = Math.Max(0.0f, Math.Min(1.0f, volume)),
                    Pitch = Math.Max(-1.0f, Math.Min(1.0f, pitch)),
                    Pan = Math.Max(-1.0f, Math.Min(1.0f, pan)),
                    PlaybackStartTime = DateTime.UtcNow,
                    Duration = duration
                };

                // Set up spatial audio emitter if position is provided
                if (_spatialAudio != null && position.HasValue)
                {
                    voiceInstance.EmitterId = _spatialAudio.CreateEmitter(position.Value, Vector3.Zero, 1.0f, 1.0f, 100.0f);
                    UpdateVoiceInstance(voiceInstance);
                }
                else
                {
                    // No spatial audio - apply volume and pan directly
                    soundInstance.Volume = voiceInstance.Volume * _masterVolume;
                    soundInstance.Pan = voiceInstance.Pan;
                    soundInstance.Pitch = voiceInstance.Pitch;
                }

                // Start playback
                soundInstance.Play();
                _voiceInstances[instanceId] = voiceInstance;

                Console.WriteLine($"[StrideVoicePlayer] Playing voice-over: {voiceResRef} (instance {instanceId}, duration: {duration:F2}s)");
                return instanceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideVoicePlayer] Error playing voice-over {voiceResRef}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Creates a Stride SoundInstance from WAV file data using DynamicSoundSource.
        /// Reuses the same pattern as StrideSoundPlayer for consistency.
        /// </summary>
        private SoundInstance CreateSoundInstanceFromWavData(WAV wavFile, byte[] wavData, bool useSpatialAudio)
        {
            // Use the helper class from StrideSoundPlayer (SoundWavDynamicSoundSource is internal)
            // Reuse the same implementation pattern
            try
            {
                byte[] pcmData = wavFile.Data;
                if (pcmData == null || pcmData.Length == 0)
                {
                    Console.WriteLine("[StrideVoicePlayer] No PCM data in WAV file");
                    return null;
                }

                // Create temporary SoundInstance for DynamicSoundSource constructor (circular dependency workaround)
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
                    wavFile.Channels == 1,
                    useSpatialAudio,
                    false,
                    0.0f,
                    HrtfEnvironment.Small
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
                return null;
            }
        }

        /// <summary>
        /// Stops a playing voice-over by instance ID.
        /// </summary>
        /// <param name="voiceInstanceId">The voice instance ID returned from PlayVoice.</param>
        public void StopVoice(uint voiceInstanceId)
        {
            if (!_voiceInstances.TryGetValue(voiceInstanceId, out VoiceInstance voiceInstance))
            {
                return;
            }

            try
            {
                if (voiceInstance.SoundInstance != null)
                {
                    voiceInstance.SoundInstance.Stop();
                    voiceInstance.SoundInstance.Dispose();
                }
                if (voiceInstance.DynamicSoundSource != null)
                {
                    voiceInstance.DynamicSoundSource.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideVoicePlayer] Error stopping voice instance {voiceInstanceId}: {ex.Message}");
            }
            finally
            {
                // Clean up spatial audio emitter
                if (_spatialAudio != null && voiceInstance.EmitterId != 0)
                {
                    _spatialAudio.RemoveEmitter(voiceInstance.EmitterId);
                }

                _voiceInstances.Remove(voiceInstanceId);
            }
        }

        /// <summary>
        /// Stops all currently playing voice-overs.
        /// </summary>
        public void StopAllVoices()
        {
            var instanceIds = new List<uint>(_voiceInstances.Keys);
            foreach (uint instanceId in instanceIds)
            {
                StopVoice(instanceId);
            }
        }

        /// <summary>
        /// Sets the master volume for all voice-overs.
        /// </summary>
        /// <param name="volume">Volume (0.0 to 1.0). This affects all voice-over playback.</param>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Max(0.0f, Math.Min(1.0f, volume));
            // Update all voice instances
            foreach (var voiceInstance in _voiceInstances.Values)
            {
                UpdateVoiceInstance(voiceInstance);
            }
        }

        /// <summary>
        /// Updates the voice player system (call each frame).
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        public void Update(float deltaTime)
        {
            // Remove finished voice instances
            var finishedInstances = new List<uint>();
            foreach (var kvp in _voiceInstances)
            {
                var voiceInstance = kvp.Value;
                if (voiceInstance.SoundInstance == null ||
                    voiceInstance.SoundInstance.PlayState == SoundPlayState.Stopped ||
                    (DateTime.UtcNow - voiceInstance.PlaybackStartTime).TotalSeconds >= voiceInstance.Duration)
                {
                    finishedInstances.Add(kvp.Key);
                }
                else if (voiceInstance.SoundInstance.PlayState == SoundPlayState.Playing)
                {
                    // Update spatial audio if active
                    if (_spatialAudio != null && voiceInstance.EmitterId != 0 && voiceInstance.Position.HasValue)
                    {
                        UpdateVoiceInstance(voiceInstance);
                    }
                }
            }

            // Clean up finished instances
            foreach (uint instanceId in finishedInstances)
            {
                StopVoice(instanceId);
            }
        }

        /// <summary>
        /// Updates spatial audio parameters for a voice instance.
        /// </summary>
        private void UpdateVoiceInstance(VoiceInstance voiceInstance)
        {
            if (voiceInstance.SoundInstance == null)
            {
                return;
            }

            if (_spatialAudio != null && voiceInstance.EmitterId != 0 && voiceInstance.Position.HasValue)
            {
                // Calculate 3D audio parameters from spatial audio system
                Audio3DParameters audioParams = _spatialAudio.Calculate3DParameters(voiceInstance.EmitterId);

                // Apply voice volume setting multiplied by spatial audio volume and master volume
                voiceInstance.SoundInstance.Volume = audioParams.Volume * voiceInstance.Volume * _masterVolume;
                voiceInstance.SoundInstance.Pan = Math.Max(-1.0f, Math.Min(1.0f, audioParams.Pan + voiceInstance.Pan));
                voiceInstance.SoundInstance.Pitch = Math.Max(-1.0f, Math.Min(1.0f, voiceInstance.Pitch + (audioParams.DopplerShift - 1.0f)));
            }
            else
            {
                // No spatial audio - apply volume and pan directly
                voiceInstance.SoundInstance.Volume = voiceInstance.Volume * _masterVolume;
                voiceInstance.SoundInstance.Pan = voiceInstance.Pan;
                voiceInstance.SoundInstance.Pitch = voiceInstance.Pitch;
            }
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            StopAllVoices();
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
            }
        }
    }
}

