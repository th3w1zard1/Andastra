using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Xna.Framework;
using BioWareEngines.Graphics;
using BioWareEngines.MonoGame.Audio;

namespace BioWareEngines.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of ISpatialAudio.
    /// </summary>
    public class MonoGameSpatialAudio : ISpatialAudio
    {
        private readonly SpatialAudio _spatialAudio;

        public MonoGameSpatialAudio()
        {
            _spatialAudio = new SpatialAudio();
        }

        public float DopplerFactor
        {
            get { return _spatialAudio.DopplerFactor; }
            set { _spatialAudio.DopplerFactor = value; }
        }

        public float SpeedOfSound
        {
            get { return _spatialAudio.SpeedOfSound; }
            set { _spatialAudio.SpeedOfSound = value; }
        }

        public void SetListener(Vector3 position, Vector3 forward, Vector3 up, Vector3 velocity)
        {
            _spatialAudio.SetListener(
                ConvertVector3(position),
                ConvertVector3(forward),
                ConvertVector3(up),
                ConvertVector3(velocity)
            );
        }

        public uint CreateEmitter(Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            return _spatialAudio.CreateEmitter(
                ConvertVector3(position),
                ConvertVector3(velocity),
                volume,
                minDistance,
                maxDistance
            );
        }

        public void UpdateEmitter(uint emitterId, Vector3 position, Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            _spatialAudio.UpdateEmitter(
                emitterId,
                ConvertVector3(position),
                ConvertVector3(velocity),
                volume,
                minDistance,
                maxDistance
            );
        }

        public Audio3DParameters Calculate3DParameters(uint emitterId)
        {
            var parameters = _spatialAudio.Calculate3DParameters(emitterId);
            return new Audio3DParameters
            {
                Volume = parameters.Volume,
                Pan = parameters.Pan,
                DopplerShift = parameters.DopplerShift,
                Distance = parameters.Distance
            };
        }

        public void RemoveEmitter(uint emitterId)
        {
            _spatialAudio.RemoveEmitter(emitterId);
        }

        private static Microsoft.Xna.Framework.Vector3 ConvertVector3(Vector3 vector)
        {
            return new Microsoft.Xna.Framework.Vector3(vector.X, vector.Y, vector.Z);
        }
    }
}

