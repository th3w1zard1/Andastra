using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Audio;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Audio;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Formats.WAV;

namespace Odyssey.MonoGame.Audio
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
                // Load WAV resource (synchronously for now - TODO: make async)
                var resourceId = new ResourceIdentifier(soundResRef, ResourceType.WAV);
                byte[] wavData = _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                if (wavData == null || wavData.Length == 0)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Sound not found: {soundResRef}");
                    return 0;
                }

                WAV wav = WAVAuto.ReadWav(wavData);
                if (wav == null)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Failed to parse WAV: {soundResRef}");
                    return 0;
                }

                // Convert CSharpKOTOR WAV to MonoGame-compatible format
                byte[] wavBytes = CreateMonoGameWavStream(wav);
                if (wavBytes == null || wavBytes.Length == 0)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Failed to convert WAV: {soundResRef}");
                    return 0;
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
                            return 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MonoGameSoundPlayer] Exception loading SoundEffect: {ex.Message}");
                        return 0;
                    }
                }

                // Create instance
                SoundEffectInstance instance = soundEffect.CreateInstance();
                if (instance == null)
                {
                    Console.WriteLine($"[MonoGameSoundPlayer] Failed to create SoundEffectInstance: {soundResRef}");
                    soundEffect.Dispose();
                    return 0;
                }

                // Configure instance
                instance.Volume = volume * _masterVolume;
                instance.Pitch = Math.Max(-1.0f, Math.Min(1.0f, pitch));
                instance.Pan = Math.Max(-1.0f, Math.Min(1.0f, pan));

                // Apply 3D positioning if provided
                if (position.HasValue && _spatialAudio != null)
                {
                    // Convert System.Numerics.Vector3 to Microsoft.Xna.Framework.Vector3
                    var xnaPosition = new Microsoft.Xna.Framework.Vector3(position.Value.X, position.Value.Y, position.Value.Z);
                    uint emitterId = _spatialAudio.CreateEmitter(xnaPosition, Microsoft.Xna.Framework.Vector3.Zero, volume, 1.0f, 30.0f);
                    _spatialAudio.Apply3D(emitterId, instance);
                }

                // Play sound
                instance.Play();

                // Track instance
                uint instanceId = _nextSoundInstanceId++;
                _playingSounds[instanceId] = instance;
                _loadedSounds[instanceId] = soundEffect;

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

                if (_loadedSounds.TryGetValue(soundInstanceId, out SoundEffect soundEffect))
                {
                    soundEffect.Dispose();
                    _loadedSounds.Remove(soundInstanceId);
                }
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

            foreach (var kvp in _loadedSounds)
            {
                kvp.Value.Dispose();
            }
            _loadedSounds.Clear();
        }

        /// <summary>
        /// Sets the master volume for all sounds.
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Math.Max(0.0f, Math.Min(1.0f, volume));

            // Update volume of all playing sounds
            foreach (var instance in _playingSounds.Values)
            {
                // Note: We'd need to store original volume per instance to properly update
                // For now, this is a simplified implementation
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

                if (_loadedSounds.TryGetValue(instanceId, out SoundEffect soundEffect))
                {
                    soundEffect.Dispose();
                    _loadedSounds.Remove(instanceId);
                }
            }
        }

        /// <summary>
        /// Converts CSharpKOTOR WAV object to MonoGame-compatible RIFF/WAVE byte array.
        /// </summary>
        private byte[] CreateMonoGameWavStream(WAV wav)
        {
            // This is a simplified conversion - full implementation would handle all WAV formats
            // MonoGame expects standard RIFF/WAVE format
            try
            {
                using (var stream = new MemoryStream())
                {
                    // Write RIFF header
                    stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                    int dataSize = wav.Data.Length + 36; // Approximate size
                    stream.Write(BitConverter.GetBytes(dataSize), 0, 4);
                    stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

                    // Write fmt chunk
                    stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
                    stream.Write(BitConverter.GetBytes(16), 0, 4); // fmt chunk size
                    stream.Write(BitConverter.GetBytes((ushort)1), 0, 2); // PCM format
                    stream.Write(BitConverter.GetBytes((ushort)wav.Channels), 0, 2);
                    stream.Write(BitConverter.GetBytes((uint)wav.SampleRate), 0, 4);
                    stream.Write(BitConverter.GetBytes((uint)(wav.SampleRate * wav.Channels * (wav.BitsPerSample / 8))), 0, 4); // Byte rate
                    stream.Write(BitConverter.GetBytes((ushort)(wav.Channels * (wav.BitsPerSample / 8))), 0, 2); // Block align
                    stream.Write(BitConverter.GetBytes((ushort)wav.BitsPerSample), 0, 2);

                    // Write data chunk
                    stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
                    stream.Write(BitConverter.GetBytes((uint)wav.Data.Length), 0, 4);
                    stream.Write(wav.Data, 0, wav.Data.Length);

                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MonoGameSoundPlayer] Exception creating WAV stream: {ex.Message}");
                return null;
            }
        }
    }
}

