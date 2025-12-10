using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Formats.GFF;
using JetBrains.Annotations;

namespace CSharpKOTOR.Resource.Generics
{
    /// <summary>
    /// Game Instance Template (GIT) file handler.
    ///
    /// GIT files store dynamic area information including creatures, doors, placeables,
    /// triggers, waypoints, stores, encounters, sounds, and cameras. This is the runtime
    /// instance data for areas, stored as a GFF file. GIT files define where objects are
    /// placed in an area, their positions, orientations, and instance-specific properties.
    /// </summary>
    [PublicAPI]
    public sealed class GIT
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:62
        // Original: BINARY_TYPE = ResourceType.GIT
        public static readonly ResourceType BinaryType = ResourceType.GIT;

        // Area audio properties (ambient sounds, music, environment audio)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:72-77
        // Original: self.ambient_sound_id: int = 0
        public int AmbientSoundId { get; set; }

        // Original: self.ambient_volume: int = 0
        public int AmbientVolume { get; set; }

        // Original: self.env_audio: int = 0
        public int EnvAudio { get; set; }

        // Original: self.music_standard_id: int = 0
        public int MusicStandardId { get; set; }

        // Original: self.music_battle_id: int = 0
        public int MusicBattleId { get; set; }

        // Original: self.music_delay: int = 0
        public int MusicDelay { get; set; }

        // Instance lists (creatures, doors, placeables, triggers, waypoints, stores, encounters, sounds, cameras)
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:85-93
        // Original: self.cameras: list[GITCamera] = []
        public List<GITCamera> Cameras { get; set; } = new List<GITCamera>();

        // Original: self.creatures: list[GITCreature] = []
        public List<GITCreature> Creatures { get; set; } = new List<GITCreature>();

        // Original: self.doors: list[GITDoor] = []
        public List<GITDoor> Doors { get; set; } = new List<GITDoor>();

        // Original: self.encounters: list[GITEncounter] = []
        public List<GITEncounter> Encounters { get; set; } = new List<GITEncounter>();

        // Original: self.placeables: list[GITPlaceable] = []
        public List<GITPlaceable> Placeables { get; set; } = new List<GITPlaceable>();

        // Original: self.sounds: list[GITSound] = []
        public List<GITSound> Sounds { get; set; } = new List<GITSound>();

        // Original: self.stores: list[GITStore] = []
        public List<GITStore> Stores { get; set; } = new List<GITStore>();

        // Original: self.triggers: list[GITTrigger] = []
        public List<GITTrigger> Triggers { get; set; } = new List<GITTrigger>();

        // Original: self.waypoints: list[GITWaypoint] = []
        public List<GITWaypoint> Waypoints { get; set; } = new List<GITWaypoint>();

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:64-67
        // Original: def __init__(self):
        public GIT()
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:95-110
        // Original: def __iter__(self) -> Generator[ResRef, Any, None]:
        public IEnumerable<ResRef> GetResourceIdentifiers()
        {
            // Iterate over creatures
            foreach (GITCreature creature in Creatures)
            {
                yield return creature.ResRef;
            }
            // Iterate over doors
            foreach (GITDoor door in Doors)
            {
                yield return door.ResRef;
            }
            // Iterate over placeables
            foreach (GITPlaceable placeable in Placeables)
            {
                yield return placeable.ResRef;
            }
            // Iterate over triggers
            foreach (GITTrigger trigger in Triggers)
            {
                yield return trigger.ResRef;
            }
            // Iterate over waypoints
            foreach (GITWaypoint waypoint in Waypoints)
            {
                yield return waypoint.ResRef;
            }
            // Iterate over stores
            foreach (GITStore store in Stores)
            {
                yield return store.ResRef;
            }
            // Iterate over encounters
            foreach (GITEncounter encounter in Encounters)
            {
                yield return encounter.ResRef;
            }
            // Iterate over sounds
            foreach (GITSound sound in Sounds)
            {
                yield return sound.ResRef;
            }
            // Iterate over cameras
            foreach (GITCamera camera in Cameras)
            {
                yield return camera.ResRef;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:112-125
        // Original: def iter_resource_identifiers(self) -> Generator[ResourceIdentifier, Any, None]:
        public IEnumerable<ResourceIdentifier> IterResourceIdentifiers()
        {
            foreach (ResRef resRef in GetResourceIdentifiers())
            {
                yield return new ResourceIdentifier(resRef, ResourceType.GIT);
            }
        }
    }

    // Placeholder classes - need to be implemented
    [PublicAPI]
    public sealed class GITCamera
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITCreature
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITDoor
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITEncounter
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITPlaceable
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITSound
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITStore
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITTrigger
    {
        public ResRef ResRef { get; set; }
    }

    [PublicAPI]
    public sealed class GITWaypoint
    {
        public ResRef ResRef { get; set; }
    }
}
