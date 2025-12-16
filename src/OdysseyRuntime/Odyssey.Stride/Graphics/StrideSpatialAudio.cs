using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of ISpatialAudio.
    /// Note: Stride has built-in 3D audio support, but this is a simplified implementation
    /// that matches the interface. Full integration would use Stride's AudioEmitter and AudioListener.
    /// </summary>
    public class StrideSpatialAudio : ISpatialAudio
    {
        private readonly Dictionary<uint, AudioEmitter> _emitters;
        private AudioListener _listener;
        private float _dopplerFactor;
        private float _speedOfSound;

        private class AudioEmitter
        {
            public uint EmitterId;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Volume;
            public float MinDistance;
            public float MaxDistance;
            public bool Is3D;
        }

        private class AudioListener
        {
            public Vector3 Position;
            public Vector3 Forward;
            public Vector3 Up;
            public Vector3 Velocity;
        }

        public StrideSpatialAudio()
        {
            _emitters = new Dictionary<uint, AudioEmitter>();
            _listener = new AudioListener();
            _dopplerFactor = 1.0f;
            _speedOfSound = 343.0f; // m/s
        }

        public float DopplerFactor
        {
            get { return _dopplerFactor; }
            set { _dopplerFactor = Math.Max(0.0f, value); }
        }

        public float SpeedOfSound
        {
            get { return _speedOfSound; }
            set { _speedOfSound = Math.Max(1.0f, value); }
        }

        public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity)
        {
            _listener.Position = position;
            _listener.Forward = forward;
            _listener.Up = up;
            _listener.Velocity = velocity;
        }

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
            right = Vector3.Normalize(right);
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

        public void RemoveEmitter(uint emitterId)
        {
            _emitters.Remove(emitterId);
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
    }
}

