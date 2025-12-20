using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for sound entities.
    /// </summary>
    /// <remarks>
    /// Sound Component Interface:
    /// - Common interface for sound functionality across all BioWare engines
    /// - Base implementation: BaseSoundComponent in Runtime.Games.Common.Components (if common functionality exists)
    /// - Engine-specific implementations:
    ///   - Odyssey: SoundComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraSoundComponent (nwmain.exe) - if sounds are supported
    ///   - Eclipse: EclipseSoundComponent (daorigins.exe, DragonAge2.exe) - if sounds are supported
    ///   - Infinity: InfinitySoundComponent (, ) - if sounds are supported
    /// - Common functionality: Audio playback, spatial positioning, volume control, distance falloff
    /// - Sound entities emit positional audio in the game world
    /// - Based on UTS file format: GFF with "UTS " signature containing sound data
    /// - Sound entities can be active/inactive (Active field), looping (Looping field), positional (Positional field for 3D audio)
    /// - Volume: 0-127 range (Volume field), distance falloff: MinDistance (full volume) to MaxDistance (zero volume)
    /// - Continuous sounds: Play continuously when active (Continuous field)
    /// - Random sounds: Can play random sounds from SoundFiles list (Random field), randomize position (RandomPosition field)
    /// - Interval: Time between plays for non-looping sounds (Interval field, IntervalVrtn for variation)
    /// - Volume variation: VolumeVrtn field for random volume variation
    /// - Hours: Bitmask for time-based activation (Hours field, 0-23 hour range)
    /// - Pitch variation: PitchVariation field for random pitch variation in sound playback
    /// - Based on UTS file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public interface ISoundComponent : IComponent
    {
        /// <summary>
        /// Template resource reference.
        /// </summary>
        string TemplateResRef { get; set; }

        /// <summary>
        /// Whether the sound is active.
        /// </summary>
        bool Active { get; set; }

        /// <summary>
        /// Whether the sound is continuous.
        /// </summary>
        bool Continuous { get; set; }

        /// <summary>
        /// Whether the sound loops.
        /// </summary>
        bool Looping { get; set; }

        /// <summary>
        /// Whether the sound is positional (3D).
        /// </summary>
        bool Positional { get; set; }

        /// <summary>
        /// Whether to randomize position.
        /// </summary>
        bool RandomPosition { get; set; }

        /// <summary>
        /// Whether to play random sounds from list.
        /// </summary>
        bool Random { get; set; }

        /// <summary>
        /// Volume (0-127).
        /// </summary>
        int Volume { get; set; }

        /// <summary>
        /// Volume variation.
        /// </summary>
        int VolumeVrtn { get; set; }

        /// <summary>
        /// Maximum audible distance.
        /// </summary>
        float MaxDistance { get; set; }

        /// <summary>
        /// Minimum distance (full volume).
        /// </summary>
        float MinDistance { get; set; }

        /// <summary>
        /// Interval between plays (seconds).
        /// </summary>
        uint Interval { get; set; }

        /// <summary>
        /// Interval variation.
        /// </summary>
        uint IntervalVrtn { get; set; }

        /// <summary>
        /// Pitch variation.
        /// </summary>
        float PitchVariation { get; set; }

        /// <summary>
        /// List of sound file resources.
        /// </summary>
        List<string> SoundFiles { get; set; }

        /// <summary>
        /// Hours when sound is active (bitmask, 0-23).
        /// </summary>
        uint Hours { get; set; }

        /// <summary>
        /// Time since last play.
        /// </summary>
        float TimeSinceLastPlay { get; set; }

        /// <summary>
        /// Whether currently playing.
        /// </summary>
        bool IsPlaying { get; set; }
    }
}

