using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Audio;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of ISpatialAudio.
    /// </summary>
    public class MonoGameSpatialAudio : ISpatialAudio
    {
        private readonly SpatialAudio _spatialAudio;

        /// <summary>
        /// Gets the underlying SpatialAudio instance for use by MonoGame audio players.
        /// </summary>
        public SpatialAudio UnderlyingSpatialAudio => _spatialAudio;

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

        public void SetListener(System.Numerics.Vector3 position, System.Numerics.Vector3 forward, System.Numerics.Vector3 up, System.Numerics.Vector3 velocity)
        {
            _spatialAudio.SetListener(
                ConvertVector3(position),
                ConvertVector3(forward),
                ConvertVector3(up),
                ConvertVector3(velocity)
            );
        }

        public uint CreateEmitter(System.Numerics.Vector3 position, System.Numerics.Vector3 velocity, float volume, float minDistance, float maxDistance)
        {
            return _spatialAudio.CreateEmitter(
                ConvertVector3(position),
                ConvertVector3(velocity),
                volume,
                minDistance,
                maxDistance
            );
        }

        public void UpdateEmitter(uint emitterId, System.Numerics.Vector3 position, System.Numerics.Vector3 velocity, float volume, float minDistance, float maxDistance)
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

        public Andastra.Runtime.Graphics.Audio3DParameters Calculate3DParameters(uint emitterId)
        {
            var parameters = _spatialAudio.Calculate3DParameters(emitterId);
            return new Andastra.Runtime.Graphics.Audio3DParameters
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

        private static Microsoft.Xna.Framework.Vector3 ConvertVector3(System.Numerics.Vector3 vector)
        {
            return new Microsoft.Xna.Framework.Vector3(vector.X, vector.Y, vector.Z);
        }
    }
}

