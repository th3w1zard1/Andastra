using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing.Formats.WAV;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Audio;
using Microsoft.Xna.Framework.Audio;

namespace Andastra.Runtime.MonoGame.Audio
{
    /// <summary>
    /// MonoGame implementation of ISoundPlayer for playing sound effects.
    ///
    /// Loads WAV files from KOTOR installation and plays them using MonoGame's SoundEffect API.
    /// Supports positional audio for 3D sound effects.
    ///
    /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Audio.SoundEffect.html
    /// SoundEffect.LoadFromStream() loads WAV data from a stream
    /// SoundEffectInstance provides playback control (Play, Stop, Volume, Pan, Pitch)
    /// </summary>
    /// <remarks>
    /// Sound Player (MonoGame Implementation):
    /// - Based on swkotor2.exe sound effect playback system
    /// - Located via string references: "SoundResRef" @ 0x007b5f70, "SoundList" @ 0x007bd080, "Sounds" @ 0x007c1038
    /// - "Sound" @ 0x007bc500, "SoundOptions" @ 0x007b5720, "Disable Sound" @ 0x007b5730
    /// - "guisounds" @ 0x007b5f7c (GUI sound effects), "PartSounds" @ 0x007bd440 (party sounds)
    /// - Sound types: "AmbientSound" @ 0x007c4c68, "FootstepSounds" @ 0x007c4c8c, "WeaponSounds" @ 0x007c4c9c
    /// - "InventorySound" @ 0x007c7164, "ExplosionSound" @ 0x007cada0
    /// - Sound properties: "SoundAppType" @ 0x007c3028, "SoundSetFile" @ 0x007c41f4, "SoundSetType" @ 0x007cbd40
    /// - "SoundSet" @ 0x007cbd50, "SoundExists" @ 0x007c3568, "SoundImpact" @ 0x007c49b4
    /// - "SoundDuration" @ 0x007c49c0, "SoundOneShot" @ 0x007c4aa4, "SoundOneShotPercentage" @ 0x007c4a8c
    /// - "SoundCessation" @ 0x007cd5f0, "ProjSound" @ 0x007c312c, "CastSound" @ 0x007c3250
    /// - "ConjSoundVFX" @ 0x007c3324, "ConjSoundFemale" @ 0x007c3334, "ConjSoundMale" @ 0x007c3344
    /// - "InvSoundType" @ 0x007c446c, "SoundProvider" @ 0x007c6154
    /// - Sound events: "LIGHTALIGNSOUND" @ 0x007bdea8, "DARKALIGNSOUND" @ 0x007bdeb8
    /// - "COMPLETESOUND" @ 0x007bdec8, "NEWQUESTSOUND" @ 0x007bded8, "LEVELUPSOUND" @ 0x007bdee8
    /// - "SOUNDPENDING" @ 0x007bdef8 (sound pending flag)
    /// - Sound format: "ShotSound%d" @ 0x007cadb0, "ImpactSound%d" @ 0x007cadf0 (sound format strings)
    /// - "Fire_Sound" @ 0x007cb618, "Collision_Sound" @ 0x007cb624
    /// - Error messages:
    ///   - "CExoSoundSource %s not freed" @ 0x007c6090
    /// - Sound directories: "HD0:STREAMSOUNDS\%s" @ 0x007c61d4 (streaming sound path format)
    /// - "sounddummy" @ 0x007c78f0, "soundapptype" @ 0x007caf54
    /// - GUI: "Sound Effects Volume" @ 0x007c83e0, "BTN_SOUND" @ 0x007d0d80, "SOUND" @ 0x007d1628
    /// - "optsound_p" @ 0x007d2134, "optsoundadv_p" @ 0x007d1eb4 (sound options panels)
    /// - "Sound Init" @ 0x007c7280 (sound initialization)
    /// - Audio: "EnvAudio" @ 0x007bd478 (environmental audio)
    /// - Video: "_BinkSetSoundSystem@8" (Bink video sound system)
    /// - Original implementation: KOTOR plays WAV files for sound effects (ambient, combat, UI, etc.)
    /// - Sound files: Stored as WAV resources, referenced by ResRef
    /// - Positional audio: Sounds can be played at entity positions (3D spatial audio)
    /// - Playback control: Play, Stop, volume, pan, pitch (original engine uses DirectSound/EAX)
    /// - This MonoGame implementation uses SoundEffect API instead of original DirectSound/EAX
    /// </remarks>
    public class MonoGameSoundPlayer : ISoundPlayer
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly SpatialAudio _spatialAudio;
        private readonly Dictionary<uint, SoundEffectInstance> _playingSounds;
        private readonly Dictionary<uint, SoundEffect> _loadedSounds;
        private readonly Dictionary<uint, float> _instanceOriginalVolumes;
        private readonly Dictionary<uint, string> _instanceResRefs;
        private readonly Dictionary<string, SoundEffect> _soundEffectCache;
        private readonly Dictionary<string, int> _cacheReferenceCounts;
        private readonly object _cacheLock;
        private uint _nextSoundInstanceId;
        private float _masterVolume;

        /// <summary>
        /// Initializes a new MonoGame sound player.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading WAV files.</param>
        /// <param name="spatialAudio">Optional spatial audio system for 3D positioning.</param>
        public MonoGameSoundPlayer(IGameResourceProvider resourceProvider, SpatialAudio spatialAudio = null)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
            _spatialAudio = spatialAudio;
            _playingSounds = new Dictionary<uint, SoundEffectInstance>();
            _loadedSounds = new Dictionary<uint, SoundEffect>();
            _instanceOriginalVolumes = new Dictionary<uint, float>();
            _instanceResRefs = new Dictionary<uint, string>();
            _soundEffectCache = new Dictionary<string, SoundEffect>();
            _cacheReferenceCounts = new Dictionary<string, int>();
            _cacheLock = new object();
            _nextSoundInstanceId = 1;
            _masterVolume = 1.0f;
        }

        /// <summary>
        /// Plays a sound effect by ResRef.
        /// </summary>
        public uint PlaySound(string soundResRef, System.Numerics.Vector3? position = null, float volume = 1.0f, float pitch = 0.0f, float pan = 0.0f)
        {
            if (string.IsNullOrEmpty(soundResRef))
            {
                return 0;
            }

            try
            {
                // Check cache first (fast path for frequently used sounds)
                SoundEffect soundEffect = null;
                lock (_cacheLock)
                {
                    if (_soundEffectCache.TryGetValue(soundResRef, out SoundEffect cachedSound))
                    {
                        soundEffect = cachedSound;
                        _cacheReferenceCounts[soundResRef] = _cacheReferenceCounts[soundResRef] + 1;
                    }
                }

                // If not cached, load asynchronously
                if (soundEffect == null)
                {
                    soundEffect = LoadSoundEffectAsync(soundResRef).Result;
                    if (soundEffect == null)
                    {
                        Console.WriteLine($"[MonoGameSoundPlayer] Failed to load sound: {soundResRef}");
                        return 0;
                    }

                    // Cache the loaded sound effect
                    lock (_cacheLock)
                    {
                        if (!_soundEffectCache.ContainsKey(soundResRef))
                        {
                            _soundEffectCache[soundResRef] = soundEffect;
                            _cacheReferenceCounts[soundResRef] = 1;
                        }
                        else
                        {
                            // Another thread loaded it first, dispose our duplicate
                            soundEffect.Dispose();
                            soundEffect = _soundEffectCache[soundResRef];
                            _cacheReferenceCounts[soundResRef] = _cacheReferenceCounts[soundResRef] + 1;
                        }
                    }
                }

                // Create instance from cached sound effect
                SoundEffectInstance instance = soundEffect.CreateInstance();
                if (instance == null)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Failed to create SoundEffectInstance: {soundResRef}");
                    return 0;
                }

                // Store original volume before applying master volume
                float originalVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

                // Configure instance
                instance.Volume = originalVolume * _masterVolume;
                instance.Pitch = Math.Max(-1.0f, Math.Min(1.0f, pitch));
                instance.Pan = Math.Max(-1.0f, Math.Min(1.0f, pan));

                // Apply 3D positioning if provided
                if (position.HasValue && _spatialAudio != null)
                {
                    // Convert System.Numerics.Vector3 to Microsoft.Xna.Framework.Vector3
                    var xnaPosition = new Microsoft.Xna.Framework.Vector3(position.Value.X, position.Value.Y, position.Value.Z);
                    uint emitterId = _spatialAudio.CreateEmitter(xnaPosition, Microsoft.Xna.Framework.Vector3.Zero, originalVolume, 1.0f, 30.0f);
                    _spatialAudio.Apply3D(emitterId, instance);
                }

                // Play sound
                instance.Play();

                // Track instance and store original volume
                uint instanceId = _nextSoundInstanceId++;
                _playingSounds[instanceId] = instance;
                _loadedSounds[instanceId] = soundEffect;
                _instanceOriginalVolumes[instanceId] = originalVolume;
                _instanceResRefs[instanceId] = soundResRef;

                return instanceId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonoGameSoundPlayer] Exception playing sound: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Stops a playing sound by instance ID.
        /// </summary>
        public void StopSound(uint soundInstanceId)
        {
            if (_playingSounds.TryGetValue(soundInstanceId, out SoundEffectInstance instance))
            {
                instance.Stop();
                instance.Dispose();
                _playingSounds.Remove(soundInstanceId);

                // SoundEffect is cached, don't dispose it - just remove reference
                _loadedSounds.Remove(soundInstanceId);

                // Decrement cache reference count
                if (_instanceResRefs.TryGetValue(soundInstanceId, out string resRef))
                {
                    lock (_cacheLock)
                    {
                        if (_cacheReferenceCounts.TryGetValue(resRef, out int count))
                        {
                            _cacheReferenceCounts[resRef] = Math.Max(0, count - 1);
                        }
                    }
                    _instanceResRefs.Remove(soundInstanceId);
                }

                // Clean up stored original volume
                _instanceOriginalVolumes.Remove(soundInstanceId);
            }
        }

        /// <summary>
        /// Stops all currently playing sounds.
        /// </summary>
        public void StopAllSounds()
        {
            foreach (var kvp in _playingSounds)
            {
                kvp.Value.Stop();
                kvp.Value.Dispose();
            }
            _playingSounds.Clear();

            // SoundEffects are cached, don't dispose them - just clear references
            _loadedSounds.Clear();

            // Reset cache reference counts for all sounds
            lock (_cacheLock)
            {
                var resRefs = new List<string>(_instanceResRefs.Values);
                foreach (string resRef in resRefs)
                {
                    if (_cacheReferenceCounts.TryGetValue(resRef, out int count))
                    {
                        _cacheReferenceCounts[resRef] = 0;
                    }
                }
            }

            _instanceResRefs.Clear();

            // Clean up all stored original volumes
            _instanceOriginalVolumes.Clear();
        }

        /// <summary>
        /// Sets the master volume for all sounds.
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

            // Update volume of all playing sounds using their stored original volumes
            foreach (var kvp in _playingSounds)
            {
                uint instanceId = kvp.Key;
                SoundEffectInstance instance = kvp.Value;

                if (_instanceOriginalVolumes.TryGetValue(instanceId, out float originalVolume))
                {
                    // Recalculate volume as original volume * master volume
                    instance.Volume = originalVolume * _masterVolume;
                }
            }
        }

        /// <summary>
        /// Updates the sound system (call each frame).
        /// </summary>
        public void Update(float deltaTime)
        {
            // Clean up finished sounds
            var finishedSounds = new List<uint>();
            foreach (var kvp in _playingSounds)
            {
                if (kvp.Value.State == SoundState.Stopped)
                {
                    finishedSounds.Add(kvp.Key);
                }
            }

            foreach (uint instanceId in finishedSounds)
            {
                _playingSounds[instanceId].Dispose();
                _playingSounds.Remove(instanceId);

                // SoundEffect is cached, don't dispose it - just remove reference
                _loadedSounds.Remove(instanceId);

                // Decrement cache reference count
                if (_instanceResRefs.TryGetValue(instanceId, out string resRef))
                {
                    lock (_cacheLock)
                    {
                        if (_cacheReferenceCounts.TryGetValue(resRef, out int count))
                        {
                            _cacheReferenceCounts[resRef] = Math.Max(0, count - 1);
                        }
                    }
                    _instanceResRefs.Remove(instanceId);
                }

                // Clean up stored original volume
                _instanceOriginalVolumes.Remove(instanceId);
            }
        }

        /// <summary>
        /// Loads a sound effect asynchronously from a ResRef.
        /// 
        /// Uses Task.Run to perform I/O operations on a background thread, preventing
        /// blocking of the main thread during resource loading. This matches the pattern
        /// used in MonoGameVoicePlayer for async resource loading.
        /// 
        /// Based on swkotor2.exe sound loading system - original engine loads WAV resources
        /// asynchronously from disk/archive files to prevent audio playback stalls.
        /// </summary>
        /// <param name="soundResRef">The sound resource reference to load.</param>
        /// <returns>Task that completes with the loaded SoundEffect, or null if loading failed.</returns>
        private Task<SoundEffect> LoadSoundEffectAsync(string soundResRef)
        {
            return Task.Run(async () =>
            {
                try
                {
                    // Load WAV resource asynchronously
                    var resourceId = new ResourceIdentifier(soundResRef, ResourceType.WAV);
                    byte[] wavData = await _resourceProvider.GetResourceBytesAsync(resourceId, CancellationToken.None);
                    if (wavData == null || wavData.Length == 0)
                    {
                        Console.WriteLine($"[MonoGameSoundPlayer] Sound not found: {soundResRef}");
                        return null;
                    }

                    // Parse WAV file
                    WAV wav = WAVAuto.ReadWav(wavData);
                    if (wav == null)
                    {
                        Console.WriteLine($"[MonoGameSoundPlayer] Failed to parse WAV: {soundResRef}");
                        return null;
                    }

                    // Convert Andastra.Parsing WAV to MonoGame-compatible format
                    byte[] wavBytes = CreateMonoGameWavStream(wav);
                    if (wavBytes == null || wavBytes.Length == 0)
                    {
                        Console.WriteLine($"[MonoGameSoundPlayer] Failed to convert WAV: {soundResRef}");
                        return null;
                    }

                    // Load SoundEffect from stream
                    SoundEffect soundEffect = null;
                    using (var stream = new MemoryStream(wavBytes))
                    {
                        try
                        {
                            soundEffect = SoundEffect.FromStream(stream);
                            if (soundEffect == null)
                            {
                                Console.WriteLine($"[MonoGameSoundPlayer] Failed to load SoundEffect: {soundResRef}");
                                return null;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[MonoGameSoundPlayer] Exception loading SoundEffect: {ex.Message}");
                            return null;
                        }
                    }

                    return soundEffect;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Exception in LoadSoundEffectAsync: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Converts Andastra.Parsing WAV object to MonoGame-compatible RIFF/WAVE byte array.
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
                Console.WriteLine($"[MonoGameSoundPlayer] Exception creating WAV stream: {ex.Message}");
                return null;
            }
        }
    }
}

