using System;
using System.Numerics;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Templates
{
    /// <summary>
    /// Sound template implementation for spawning sounds from UTS data.
    /// </summary>
    /// <remarks>
    /// Sound Template:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) sound system
    /// - Located via string references: "SoundList" @ 0x007bd080 (GIT sound list), "Sound" @ 0x007bc500 (sound entity type)
    /// - "SoundResRef" @ 0x007b5f70 (sound resource reference field), "PlaySound" @ 0x007c5f70 (sound playback function)
    /// - "Volume" @ 0x007c6110 (sound volume field), "MinVolumeDist" @ 0x007c60c4, "MaxVolumeDist" @ 0x007c60d8 (sound distance falloff fields)
    /// - Sound types: "SoundOneShot" @ 0x007c4aa4 (one-shot sound flag), "SoundDuration" @ 0x007c49c0 (sound duration field)
    /// - "AmbientSound" @ 0x007c4c68 (ambient sound flag), "SoundSet" @ 0x007cbd50 (sound set field)
    /// - Sound paths: "HD0:STREAMSOUNDS\%s" @ 0x007c61d4 (streaming sound path format), "guisounds" @ 0x007b5f7c (GUI sounds directory)
    /// - Template loading: FUN_004e08e0 @ 0x004e08e0 loads sound instances from GIT
    /// - FUN_005226d0 @ 0x005226d0 (entity serialization references sound templates)
    /// - Original implementation: UTS (Sound) GFF templates define sound properties
    /// - UTS file format: GFF with "UTS " signature containing sound data (Active, Looping, Positional, ResRef, Volume, MaxDistance, MinDistance)
    /// - Sound entities emit positional audio in the game world (Positional field for 3D audio)
    /// - Sounds can be active/inactive (Active field), looping (Looping field), positional (Positional field)
    /// - Volume: 0-127 range (Volume field), distance falloff: MinDistance (full volume) to MaxDistance (zero volume)
    /// - Continuous sounds: Play continuously when active (Continuous field)
    /// - Random sounds: Can play random sounds from SoundFiles list (Random field), randomize position (RandomPosition field)
    /// - Interval: Time between plays for non-looping sounds (Interval field, IntervalVrtn for variation)
    /// - Volume variation: VolumeVrtn field for random volume variation
    /// - Hours: Bitmask for time-based activation (Hours field, 0-23 hour range)
    /// - Pitch variation: PitchVariation field for random pitch variation in sound playback
    /// - Based on UTS file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class SoundTemplate : ISoundTemplate
    {
        #region Properties

        public string ResRef { get; set; }
        public string Tag { get; set; }
        public ObjectType ObjectType { get { return ObjectType.Sound; } }
        public string[] Sounds { get; set; }
        public int Volume { get; set; }
        public bool Active { get; set; }
        public bool IsPositional { get; set; }
        public float MinDistance { get; set; }
        public float MaxDistance { get; set; }

        // Additional properties
        public string DisplayName { get; set; }
        public bool Looping { get; set; }
        public bool Random { get; set; }
        public bool RandomPosition { get; set; }
        public int Interval { get; set; }
        public int IntervalVariation { get; set; }
        public float PitchVariation { get; set; }
        public int Hours { get; set; }
        public int Priority { get; set; }
        public bool Continuous { get; set; }
        public float RandomRangeX { get; set; }
        public float RandomRangeY { get; set; }
        public float Elevation { get; set; }

        #endregion

        public SoundTemplate()
        {
            ResRef = string.Empty;
            Tag = string.Empty;
            DisplayName = string.Empty;
            Sounds = new string[0];
            Volume = 127;
            Active = true;
            IsPositional = true;
            MinDistance = 1.0f;
            MaxDistance = 50.0f;
        }

        public IEntity Spawn(IWorld world, Vector3 position, float facing)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            var entity = new Entity(ObjectType.Sound, (World)world);
            entity.Tag = Tag;
            entity.TemplateResRef = ResRef;

            // Apply position
            Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
            if (transform != null)
            {
                transform.Position = position;
                transform.Facing = facing;
            }

            // Register in world
            world.RegisterEntity(entity);

            return entity;
        }
    }
}
