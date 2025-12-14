using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for ambient sound entities.
    /// </summary>
    /// <remarks>
    /// Based on UTS file format documentation.
    /// Sound entities emit positional audio.
    /// </remarks>
    public class SoundComponent : IComponent
    {
        public SoundComponent()
        {
            TemplateResRef = string.Empty;
            SoundFiles = new List<string>();
            Volume = 100;
            MaxDistance = 50f;
            MinDistance = 1f;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Whether the sound is active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Whether the sound is continuous.
        /// </summary>
        public bool Continuous { get; set; }

        /// <summary>
        /// Whether the sound loops.
        /// </summary>
        public bool Looping { get; set; }

        /// <summary>
        /// Whether the sound is positional (3D).
        /// </summary>
        public bool Positional { get; set; }

        /// <summary>
        /// Whether to randomize position.
        /// </summary>
        public bool RandomPosition { get; set; }

        /// <summary>
        /// Whether to play random sounds from list.
        /// </summary>
        public bool Random { get; set; }

        /// <summary>
        /// Volume (0-127).
        /// </summary>
        public int Volume { get; set; }

        /// <summary>
        /// Volume variation.
        /// </summary>
        public int VolumeVrtn { get; set; }

        /// <summary>
        /// Maximum audible distance.
        /// </summary>
        public float MaxDistance { get; set; }

        /// <summary>
        /// Minimum distance (full volume).
        /// </summary>
        public float MinDistance { get; set; }

        /// <summary>
        /// Interval between plays (seconds).
        /// </summary>
        public uint Interval { get; set; }

        /// <summary>
        /// Interval variation.
        /// </summary>
        public uint IntervalVrtn { get; set; }

        /// <summary>
        /// Pitch variation.
        /// </summary>
        public float PitchVariation { get; set; }

        /// <summary>
        /// List of sound file resources.
        /// </summary>
        public List<string> SoundFiles { get; set; }

        /// <summary>
        /// Hours when sound is active (bitmask, 0-23).
        /// </summary>
        public uint Hours { get; set; }

        /// <summary>
        /// Time since last play.
        /// </summary>
        public float TimeSinceLastPlay { get; set; }

        /// <summary>
        /// Whether currently playing.
        /// </summary>
        public bool IsPlaying { get; set; }
    }
}
