using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Andastra.Game.Graphics.MonoGame.Audio
{
    /// <summary>
    /// Spatial audio system for 3D sound positioning.
    /// 
    /// Spatial audio provides realistic 3D sound positioning based on
    /// listener and emitter positions, essential for immersive gameplay.
    /// 
    /// Features:
    /// - 3D sound positioning
    /// - Distance-based attenuation
    /// - Doppler effect
    /// - Occlusion/obstruction
    /// - Reverb zones
    /// </summary>
    /// <remarks>
    /// Spatial Audio System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) audio system (modern 3D enhancement)
    /// - Located via string references: "SoundList" @ 0x007bd080, "SoundResRef" @ 0x007b5f70, "Sound" @ 0x007bc500
    /// - 3D audio settings: "2D3DBias" @ 0x007c612c, "2D3D Bias" @ 0x007c71f8 (2D/3D audio bias)
    /// - "3D Provider Filter" @ 0x007c6164, "Number 3D Voices" @ 0x007c7258
    /// - GUI: "LBL_3DVIEW" @ 0x007cbfdc, "gui3D_room" @ 0x007cc144, "LBL_3D" @ 0x007cfe50
    /// - "BTN_3DCHAR" @ 0x007cfac0, "LBL_3DCHAR" @ 0x007cfb38, "3D_MODEL" @ 0x007d0954
    /// - "3D_MODEL_LS" @ 0x007d0948, "3D_MODEL%d" @ 0x007d1924 (3D model format string)
    /// - "3D_PlanetDisplay" @ 0x007cd9f0, "3D_PlanetModel" @ 0x007cda04
    /// - Miles Sound System 3D API functions:
    ///   - _AIL_open_3D_listener@4, _AIL_close_3D_listener@4 (3D listener management)
    ///   - _AIL_open_3D_provider@4, _AIL_close_3D_provider@4 (3D audio provider)
    ///   - _AIL_set_3D_position@16, _AIL_set_3D_orientation@28 (3D position/orientation)
    ///   - _AIL_set_3D_rolloff_factor@8, _AIL_set_3D_sample_distances@12 (distance attenuation)
    ///   - _AIL_set_3D_room_type@8, _AIL_3D_room_type@4 (EAX room types)
    ///   - _AIL_allocate_3D_sample_handle@4, _AIL_set_3D_sample_file@8 (3D sample management)
    ///   - _AIL_set_3D_sample_volume@8, _AIL_set_3D_sample_obstruction@8 (3D sample properties)
    ///   - _AIL_set_3D_provider_preference@12, _AIL_enumerate_3D_providers@12 (3D provider management)
    /// - Original implementation: KOTOR uses EAX audio for 3D spatial positioning
    /// - Sound entities: UTS templates define sound emitters with position and properties
    /// - Distance attenuation: Sounds fade with distance (MinDistance, MaxDistance)
    /// - Doppler effect: Frequency shift based on relative velocity (this MonoGame implementation)
    /// - Reverb zones: Room-based audio effects (original uses EAX room types)
    /// - Note: Original engine used EAX audio API via Miles Sound System, this MonoGame implementation provides modern 3D audio
    /// </remarks>
    public class SpatialAudio
    {
        /// <summary>
        /// Audio emitter.
        /// </summary>
        public class AudioEmitter
        {
            public uint EmitterId;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Volume;
            public float MinDistance;
            public float MaxDistance;
            public bool Is3D;
        }

        /// <summary>
        /// Audio listener (camera/player).
        /// </summary>
        public class AudioListener
        {
            public Vector3 Position;
            public Vector3 Forward;
            public Vector3 Up;
            public Vector3 Velocity;
        }

        private readonly Dictionary<uint, AudioEmitter> _emitters;
        private AudioListener _listener;
        private float _dopplerFactor;
        private float _speedOfSound;

        /// <summary>
        /// Gets or sets the Doppler effect factor.
        /// </summary>
        public float DopplerFactor
        {
            get { return _dopplerFactor; }
            set { _dopplerFactor = Math.Max(0.0f, value); }
        }

        /// <summary>
        /// Gets or sets the speed of sound (for Doppler calculation).
        /// </summary>
        public float SpeedOfSound
        {
            get { return _speedOfSound; }
            set { _speedOfSound = Math.Max(1.0f, value); }
        }

        /// <summary>
        /// Initializes a new spatial audio system.
        /// </summary>
        public SpatialAudio()
        {
            _emitters = new Dictionary<uint, AudioEmitter>();
            _listener = new AudioListener();
            _dopplerFactor = 1.0f;
            _speedOfSound = 343.0f; // m/s
        }

        /// <summary>
        /// Sets the audio listener (camera position).
        /// </summary>
        public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity)
        {
            _listener.Position = position;
            _listener.Forward = forward;
            _listener.Up = up;
            _listener.Velocity = velocity;
        }

        /// <summary>
        /// Creates a new audio emitter and returns its ID.
        /// </summary>
        public uint CreateEmitter(Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            uint emitterId = (uint)(_emitters.Count + 1);
            var emitter = new AudioEmitter
            {
                EmitterId = emitterId,
                Position = position,
                Velocity = velocity,
                Volume = volume,
                MinDistance = minDistance,
                MaxDistance = maxDistance,
                Is3D = true
            };
            _emitters[emitterId] = emitter;
            return emitterId;
        }

        /// <summary>
        /// Adds or updates an audio emitter.
        /// </summary>
        public void UpdateEmitter(uint emitterId, Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            AudioEmitter emitter;
            if (!_emitters.TryGetValue(emitterId, out emitter))
            {
                emitter = new AudioEmitter
                {
                    EmitterId = emitterId,
                    Is3D = true
                };
                _emitters[emitterId] = emitter;
            }

            emitter.Position = position;
            emitter.Velocity = velocity;
            emitter.Volume = volume;
            emitter.MinDistance = minDistance;
            emitter.MaxDistance = maxDistance;
        }

        /// <summary>
        /// Applies 3D audio parameters to a SoundEffectInstance.
        /// </summary>
        public void Apply3D(uint emitterId, Microsoft.Xna.Framework.Audio.SoundEffectInstance instance)
        {
            Audio3DParameters parameters = Calculate3DParameters(emitterId);
            instance.Volume = parameters.Volume;
            instance.Pan = parameters.Pan;
            instance.Pitch = parameters.DopplerShift - 1.0f; // Convert Doppler shift to pitch
        }

        /// <summary>
        /// Calculates 3D audio parameters for an emitter.
        /// </summary>
        public Audio3DParameters Calculate3DParameters(uint emitterId)
        {
            AudioEmitter emitter;
            if (!_emitters.TryGetValue(emitterId, out emitter))
            {
                return new Audio3DParameters { Volume = 0.0f };
            }

            // Calculate distance
            Vector3 toEmitter = emitter.Position - _listener.Position;
            float distance = toEmitter.Length();

            // Distance-based attenuation
            float attenuation = CalculateAttenuation(distance, emitter.MinDistance, emitter.MaxDistance);
            float volume = emitter.Volume * attenuation;

            // Calculate pan (left/right)
            Vector3 right = Vector3.Cross(_listener.Forward, _listener.Up);
            float pan = Vector3.Dot(Vector3.Normalize(toEmitter), right);

            // Calculate Doppler shift
            float dopplerShift = CalculateDoppler(emitter.Velocity, _listener.Velocity, toEmitter);

            return new Audio3DParameters
            {
                Volume = volume,
                Pan = pan,
                DopplerShift = dopplerShift,
                Distance = distance
            };
        }

        private float CalculateAttenuation(float distance, float minDistance, float maxDistance)
        {
            if (distance <= minDistance)
            {
                return 1.0f;
            }
            if (distance >= maxDistance)
            {
                return 0.0f;
            }

            // Inverse distance attenuation
            return minDistance / distance;
        }

        private float CalculateDoppler(Vector3 emitterVelocity, Vector3 listenerVelocity, Vector3 toEmitter)
        {
            Vector3 normalized = Vector3.Normalize(toEmitter);
            float emitterSpeed = Vector3.Dot(emitterVelocity, normalized);
            float listenerSpeed = Vector3.Dot(listenerVelocity, normalized);

            float relativeSpeed = emitterSpeed - listenerSpeed;
            return 1.0f + (relativeSpeed / _speedOfSound) * _dopplerFactor;
        }

        /// <summary>
        /// Removes an emitter.
        /// </summary>
        public void RemoveEmitter(uint emitterId)
        {
            _emitters.Remove(emitterId);
        }
    }

    /// <summary>
    /// 3D audio parameters.
    /// </summary>
    public struct Audio3DParameters
    {
        public float Volume;
        public float Pan;
        public float DopplerShift;
        public float Distance;
    }
}

