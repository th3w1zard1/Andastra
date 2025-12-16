using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resources;
using JetBrains.Annotations;

namespace AuroraEngine.Common.Resource.Generics
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

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:388-1594
    // Original: GIT instance classes
    [PublicAPI]
    public sealed class GITCamera
    {
        public const int GffStructId = 14;
        public int CameraId { get; set; }
        public float Fov { get; set; } = 45.0f;
        public float Height { get; set; }
        public float MicRange { get; set; }
        public Vector4 Orientation { get; set; } = new Vector4();
        public Vector3 Position { get; set; } = new Vector3();
        public float Pitch { get; set; }
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
    }

    [PublicAPI]
    public sealed class GITCreature
    {
        public const int GffStructId = 4;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public float Bearing { get; set; }
    }

    [PublicAPI]
    public sealed class GITDoor
    {
        public const int GffStructId = 8;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public float Bearing { get; set; }
        public Color TweakColor { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string LinkedTo { get; set; } = string.Empty;
        public GITModuleLink LinkedToFlags { get; set; } = GITModuleLink.NoLink;
        public ResRef LinkedToModule { get; set; } = ResRef.FromBlank();
        public LocalizedString TransitionDestination { get; set; } = LocalizedString.FromInvalid();
        public Vector3 Position { get; set; } = new Vector3();
    }

    [PublicAPI]
    public sealed class GITEncounter
    {
        public const int GffStructId = 7;
        public const int GffGeometryStructId = 1;
        public const int GffSpawnStructId = 2;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public List<Vector3> Geometry { get; set; } = new List<Vector3>();
        public List<GITEncounterSpawnPoint> SpawnPoints { get; set; } = new List<GITEncounterSpawnPoint>();
    }

    [PublicAPI]
    public sealed class GITEncounterSpawnPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Orientation { get; set; }
    }

    [PublicAPI]
    public sealed class GITPlaceable
    {
        public const int GffStructId = 9;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public float Bearing { get; set; }
        public Color TweakColor { get; set; }
        public string Tag { get; set; } = string.Empty;
    }

    [PublicAPI]
    public sealed class GITSound
    {
        public const int GffStructId = 6;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public string Tag { get; set; } = string.Empty;
    }

    [PublicAPI]
    public sealed class GITStore
    {
        public const int GffStructId = 11;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public float Bearing { get; set; }
    }

    [PublicAPI]
    public sealed class GITTrigger
    {
        public const int GffStructId = 1;
        public const int GffGeometryStructId = 3;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public List<Vector3> Geometry { get; set; } = new List<Vector3>();
        public string Tag { get; set; } = string.Empty;
        public string LinkedTo { get; set; } = string.Empty;
        public GITModuleLink LinkedToFlags { get; set; } = GITModuleLink.NoLink;
        public ResRef LinkedToModule { get; set; } = ResRef.FromBlank();
        public LocalizedString TransitionDestination { get; set; } = LocalizedString.FromInvalid();
    }

    [PublicAPI]
    public sealed class GITWaypoint
    {
        public const int GffStructId = 5;
        public ResRef ResRef { get; set; } = ResRef.FromBlank();
        public Vector3 Position { get; set; } = new Vector3();
        public string Tag { get; set; } = string.Empty;
        public LocalizedString Name { get; set; } = LocalizedString.FromInvalid();
        public LocalizedString MapNote { get; set; }
        public bool MapNoteEnabled { get; set; }
        public bool HasMapNote { get; set; }
        public float Bearing { get; set; }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:591-594
    // Original: class GITModuleLink(IntEnum):
    public enum GITModuleLink
    {
        NoLink = 0,
        ToDoor = 1,
        ToWaypoint = 2
    }
}
