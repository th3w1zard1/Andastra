using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Generics
{
    /// <summary>
    /// Game Instance Template (GIT) file handler.
    /// </summary>
    /// <remarks>
    /// WHAT IS A GIT FILE?
    /// 
    /// A GIT file is a Game Instance Template file that stores all the dynamic (changeable) information
    /// about a game area. While ARE files store static information like lighting and fog, GIT files
    /// store information about objects that can move, change, or be interacted with, like creatures,
    /// doors, placeables, triggers, waypoints, stores, encounters, sounds, and cameras.
    /// 
    /// WHAT DATA DOES IT STORE?
    /// 
    /// A GIT file contains:
    /// 
    /// 1. AUDIO PROPERTIES:
    ///    - AmbientSoundId: The ID of the ambient sound that plays in the area
    ///    - AmbientVolume: The volume of the ambient sound (0-127)
    ///    - EnvAudio: The environment audio ID (affects reverb and echo)
    ///    - MusicStandardId: The ID of the standard (non-combat) music track
    ///    - MusicBattleId: The ID of the battle music track
    ///    - MusicDelay: The delay before music starts playing (in seconds)
    /// 
    /// 2. INSTANCE LISTS:
    ///    - Creatures: List of creatures (NPCs, enemies, animals) placed in the area
    ///    - Doors: List of doors placed in the area
    ///    - Placeables: List of placeable objects (chests, tables, decorations) placed in the area
    ///    - Triggers: List of triggers (invisible areas that activate scripts) placed in the area
    ///    - Waypoints: List of waypoints (navigation points) placed in the area
    ///    - Stores: List of stores (merchants) placed in the area
    ///    - Encounters: List of encounters (spawn points for groups of enemies) placed in the area
    ///    - Sounds: List of sound objects (3D positioned sounds) placed in the area
    ///    - Cameras: List of cameras (cutscene cameras) placed in the area
    /// 
    /// HOW DOES THE GAME ENGINE USE GIT FILES?
    /// 
    /// STEP 1: Loading the Area
    /// - When the player enters an area, the engine loads the GIT file
    /// - It reads the audio properties to set up ambient sounds and music
    /// - It reads all the instance lists to know what objects to place
    /// 
    /// STEP 2: Placing Objects
    /// - For each creature in Creatures, the engine loads the creature template (UTC file) and places it
    /// - For each door in Doors, the engine loads the door template (UTD file) and places it
    /// - For each placeable in Placeables, the engine loads the placeable template (UTP file) and places it
    /// - And so on for all object types
    /// 
    /// STEP 3: Setting Up Audio
    /// - The engine plays the ambient sound at the specified volume
    /// - It plays the standard music track (switches to battle music during combat)
    /// - It applies the environment audio settings for reverb and echo
    /// 
    /// STEP 4: Managing Instances
    /// - The engine keeps track of all instances (objects) in the area
    /// - When objects are destroyed, moved, or changed, the GIT file can be updated
    /// - When the player saves the game, the current state of all instances is saved
    /// 
    /// WHY ARE GIT FILES NEEDED?
    /// 
    /// Without GIT files, the game engine wouldn't know:
    /// - Where to place creatures, doors, placeables, etc.
    /// - What music and sounds to play
    /// - What triggers to activate
    /// - Where waypoints are for navigation
    /// 
    /// The GIT file acts as a blueprint that tells the engine exactly what objects to place and where.
    /// 
    /// RELATIONSHIP TO OTHER FILES:
    /// 
    /// - ARE files: The static area information (lighting, fog, etc.)
    /// - UTC files: Creature templates referenced by Creatures
    /// - UTD files: Door templates referenced by Doors
    /// - UTP files: Placeable templates referenced by Placeables
    /// - UTT files: Trigger templates referenced by Triggers
    /// - UTW files: Waypoint templates referenced by Waypoints
    /// - UTM files: Store templates referenced by Stores
    /// - UTE files: Encounter templates referenced by Encounters
    /// - UTS files: Sound templates referenced by Sounds
    /// 
    /// Together, these files define a complete game area with all its objects and audio.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: GIT files are loaded when an area is initialized. The engine reads
    /// all instance lists and places objects at their specified positions. Audio properties are
    /// used to set up ambient sounds and music. When the player saves, the current state of all
    /// instances is written back to the GIT file.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:62-378
    /// </remarks>
    [PublicAPI]
    public sealed class GIT
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:62
        // Original: BINARY_TYPE = ResourceType.GIT
        public static readonly ResourceType BinaryType = ResourceType.GIT;

        /// <summary>
        /// Area audio properties - control ambient sounds, music, and environment audio.
        /// </summary>
        /// <remarks>
        /// WHAT ARE AUDIO PROPERTIES?
        /// 
        /// Audio properties control what sounds and music play in the area. They determine the
        /// ambient background sounds, the music tracks, and the acoustic environment (reverb, echo).
        /// 
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:72-77
        /// </remarks>

        /// <summary>
        /// The ID of the ambient sound that plays in the area.
        /// This is an index into the ambient sound list (usually defined in a 2DA file).
        /// </summary>
        /// <remarks>
        /// Original: self.ambient_sound_id: int = 0
        /// </remarks>
        public int AmbientSoundId { get; set; }

        /// <summary>
        /// The volume of the ambient sound (0-127).
        /// 0 = silent, 127 = maximum volume.
        /// </summary>
        /// <remarks>
        /// Original: self.ambient_volume: int = 0
        /// </remarks>
        public int AmbientVolume { get; set; }

        /// <summary>
        /// The environment audio ID (affects reverb and echo).
        /// This ID determines the acoustic properties of the area (indoor, outdoor, cave, etc.).
        /// </summary>
        /// <remarks>
        /// Original: self.env_audio: int = 0
        /// </remarks>
        public int EnvAudio { get; set; }

        /// <summary>
        /// The ID of the standard (non-combat) music track.
        /// This music plays when the player is not in combat.
        /// </summary>
        /// <remarks>
        /// Original: self.music_standard_id: int = 0
        /// </remarks>
        public int MusicStandardId { get; set; }

        /// <summary>
        /// The ID of the battle music track.
        /// This music plays when the player enters combat.
        /// </summary>
        /// <remarks>
        /// Original: self.music_battle_id: int = 0
        /// </remarks>
        public int MusicBattleId { get; set; }

        /// <summary>
        /// The delay before music starts playing (in seconds).
        /// This prevents music from starting immediately when the area loads.
        /// </summary>
        /// <remarks>
        /// Original: self.music_delay: int = 0
        /// </remarks>
        public int MusicDelay { get; set; }

        /// <summary>
        /// Instance lists - all the objects placed in the area.
        /// </summary>
        /// <remarks>
        /// WHAT ARE INSTANCE LISTS?
        /// 
        /// Instance lists are collections of objects that are placed in the area. Each object has
        /// a template (UTC, UTD, UTP, etc.) that defines what it is, and the GIT file defines where
        /// it's placed and how it's oriented.
        /// 
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:85-93
        /// </remarks>

        /// <summary>
        /// List of cameras (cutscene cameras) placed in the area.
        /// Cameras are used for cutscenes and scripted camera movements.
        /// </summary>
        /// <remarks>
        /// Original: self.cameras: list[GITCamera] = []
        /// </remarks>
        public List<GITCamera> Cameras { get; set; } = new List<GITCamera>();

        /// <summary>
        /// List of creatures (NPCs, enemies, animals) placed in the area.
        /// Each creature references a UTC (creature template) file that defines its appearance and stats.
        /// </summary>
        /// <remarks>
        /// Original: self.creatures: list[GITCreature] = []
        /// </remarks>
        public List<GITCreature> Creatures { get; set; } = new List<GITCreature>();

        /// <summary>
        /// List of doors placed in the area.
        /// Each door references a UTD (door template) file that defines its appearance and behavior.
        /// </summary>
        /// <remarks>
        /// Original: self.doors: list[GITDoor] = []
        /// </remarks>
        public List<GITDoor> Doors { get; set; } = new List<GITDoor>();

        /// <summary>
        /// List of encounters (spawn points for groups of enemies) placed in the area.
        /// Each encounter references a UTE (encounter template) file that defines which enemies spawn.
        /// </summary>
        /// <remarks>
        /// Original: self.encounters: list[GITEncounter] = []
        /// </remarks>
        public List<GITEncounter> Encounters { get; set; } = new List<GITEncounter>();

        /// <summary>
        /// List of placeable objects (chests, tables, decorations) placed in the area.
        /// Each placeable references a UTP (placeable template) file that defines its appearance and behavior.
        /// </summary>
        /// <remarks>
        /// Original: self.placeables: list[GITPlaceable] = []
        /// </remarks>
        public List<GITPlaceable> Placeables { get; set; } = new List<GITPlaceable>();

        /// <summary>
        /// List of sound objects (3D positioned sounds) placed in the area.
        /// Each sound references a UTS (sound template) file that defines the sound to play.
        /// </summary>
        /// <remarks>
        /// Original: self.sounds: list[GITSound] = []
        /// </remarks>
        public List<GITSound> Sounds { get; set; } = new List<GITSound>();

        /// <summary>
        /// List of stores (merchants) placed in the area.
        /// Each store references a UTM (store template) file that defines what items are sold.
        /// </summary>
        /// <remarks>
        /// Original: self.stores: list[GITStore] = []
        /// </remarks>
        public List<GITStore> Stores { get; set; } = new List<GITStore>();

        /// <summary>
        /// List of triggers (invisible areas that activate scripts) placed in the area.
        /// Each trigger references a UTT (trigger template) file that defines its shape and behavior.
        /// </summary>
        /// <remarks>
        /// Original: self.triggers: list[GITTrigger] = []
        /// </remarks>
        public List<GITTrigger> Triggers { get; set; } = new List<GITTrigger>();

        /// <summary>
        /// List of waypoints (navigation points) placed in the area.
        /// Each waypoint references a UTW (waypoint template) file that defines its appearance and name.
        /// </summary>
        /// <remarks>
        /// Original: self.waypoints: list[GITWaypoint] = []
        /// </remarks>
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
            foreach (var creature in Creatures)
            {
                yield return new ResourceIdentifier(creature.ResRef, ResourceType.UTC);
            }
            foreach (var door in Doors)
            {
                yield return new ResourceIdentifier(door.ResRef, ResourceType.UTD);
            }
            foreach (var encounter in Encounters)
            {
                yield return new ResourceIdentifier(encounter.ResRef, ResourceType.UTE);
            }
            foreach (var store in Stores)
            {
                yield return new ResourceIdentifier(store.ResRef, ResourceType.UTM);
            }
            foreach (var placeable in Placeables)
            {
                yield return new ResourceIdentifier(placeable.ResRef, ResourceType.UTP);
            }
            foreach (var sound in Sounds)
            {
                yield return new ResourceIdentifier(sound.ResRef, ResourceType.UTS);
            }
            foreach (var trigger in Triggers)
            {
                yield return new ResourceIdentifier(trigger.ResRef, ResourceType.UTT);
            }
            foreach (var waypoint in Waypoints)
            {
                yield return new ResourceIdentifier(waypoint.ResRef, ResourceType.UTW);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:133-155
        // Original: def instances(self) -> list[GITInstance]:
        public List<object> Instances()
        {
            var result = new List<object>();
            result.AddRange(Cameras.Cast<object>());
            result.AddRange(Creatures.Cast<object>());
            result.AddRange(Doors.Cast<object>());
            result.AddRange(Encounters.Cast<object>());
            result.AddRange(Placeables.Cast<object>());
            result.AddRange(Sounds.Cast<object>());
            result.AddRange(Stores.Cast<object>());
            result.AddRange(Triggers.Cast<object>());
            result.AddRange(Waypoints.Cast<object>());
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:157-159
        // Original: def next_camera_id(self) -> int:
        public int NextCameraId()
        {
            if (Cameras.Count == 0)
            {
                return 1;
            }
            return Cameras.Max(c => c.CameraId) + 1;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:161-183
        // Original: def remove(self, instance: GITInstance):
        public void Remove(object instance)
        {
            if (instance is GITCreature creature)
            {
                Creatures.Remove(creature);
            }
            else if (instance is GITPlaceable placeable)
            {
                Placeables.Remove(placeable);
            }
            else if (instance is GITDoor door)
            {
                Doors.Remove(door);
            }
            else if (instance is GITTrigger trigger)
            {
                Triggers.Remove(trigger);
            }
            else if (instance is GITEncounter encounter)
            {
                Encounters.Remove(encounter);
            }
            else if (instance is GITWaypoint waypoint)
            {
                Waypoints.Remove(waypoint);
            }
            else if (instance is GITCamera camera)
            {
                Cameras.Remove(camera);
            }
            else if (instance is GITSound sound)
            {
                Sounds.Remove(sound);
            }
            else if (instance is GITStore store)
            {
                Stores.Remove(store);
            }
            else
            {
                throw new System.ArgumentException("Could not find instance in GIT object.", nameof(instance));
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:184-222
        // Original: def index(self, instance: GITInstance) -> int:
        public int Index(object instance)
        {
            if (instance is GITCreature creature)
            {
                return Creatures.IndexOf(creature);
            }
            if (instance is GITPlaceable placeable)
            {
                return Placeables.IndexOf(placeable);
            }
            if (instance is GITDoor door)
            {
                return Doors.IndexOf(door);
            }
            if (instance is GITTrigger trigger)
            {
                return Triggers.IndexOf(trigger);
            }
            if (instance is GITEncounter encounter)
            {
                return Encounters.IndexOf(encounter);
            }
            if (instance is GITWaypoint waypoint)
            {
                return Waypoints.IndexOf(waypoint);
            }
            if (instance is GITCamera camera)
            {
                return Cameras.IndexOf(camera);
            }
            if (instance is GITSound sound)
            {
                return Sounds.IndexOf(sound);
            }
            if (instance is GITStore store)
            {
                return Stores.IndexOf(store);
            }

            throw new System.ArgumentException("Could not find instance in GIT object.", nameof(instance));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:224-276
        // Original: def add(self, instance: GITInstance) -> None:
        public void Add(object instance)
        {
            if (instance is GITCreature creature)
            {
                if (Creatures.Contains(creature))
                {
                    throw new System.ArgumentException("Creature instance already exists inside the GIT object.");
                }
                Creatures.Add(creature);
                return;
            }
            if (instance is GITPlaceable placeable)
            {
                if (Placeables.Contains(placeable))
                {
                    throw new System.ArgumentException("Placeable instance already exists inside the GIT object.");
                }
                Placeables.Add(placeable);
                return;
            }
            if (instance is GITDoor door)
            {
                if (Doors.Contains(door))
                {
                    throw new System.ArgumentException("Door instance already exists inside the GIT object.");
                }
                Doors.Add(door);
                return;
            }
            if (instance is GITTrigger trigger)
            {
                if (Triggers.Contains(trigger))
                {
                    throw new System.ArgumentException("Trigger instance already exists inside the GIT object.");
                }
                Triggers.Add(trigger);
                return;
            }
            if (instance is GITEncounter encounter)
            {
                if (Encounters.Contains(encounter))
                {
                    throw new System.ArgumentException("Encounter instance already exists inside the GIT object.");
                }
                Encounters.Add(encounter);
                return;
            }
            if (instance is GITWaypoint waypoint)
            {
                if (Waypoints.Contains(waypoint))
                {
                    throw new System.ArgumentException("Waypoint instance already exists inside the GIT object.");
                }
                Waypoints.Add(waypoint);
                return;
            }
            if (instance is GITCamera camera)
            {
                if (Cameras.Contains(camera))
                {
                    throw new System.ArgumentException("Camera instance already exists inside the GIT object.");
                }
                Cameras.Add(camera);
                return;
            }
            if (instance is GITSound sound)
            {
                if (Sounds.Contains(sound))
                {
                    throw new System.ArgumentException("Sound instance already exists inside the GIT object.");
                }
                Sounds.Add(sound);
                return;
            }
            if (instance is GITStore store)
            {
                if (Stores.Contains(store))
                {
                    throw new System.ArgumentException("Store instance already exists inside the GIT object.");
                }
                Stores.Add(store);
                return;
            }

            throw new System.ArgumentException("Tried to add invalid instance.", nameof(instance));
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/generics/git.py:388-1594
    // Original: GIT instance classes
    [PublicAPI]
    public sealed class GITCamera
    {
        public const int GffStructId = 14;
        public int CameraId { get; set; }
        // Engine default: 55.0 (swkotor2.exe: 0x004e0ff0 line 57)
        // NOTE: Engine uses 55.0 as default, not 45.0. This is the field of view angle.
        public float Fov { get; set; } = 55.0f;
        public float Height { get; set; }
        public float MicRange { get; set; }
        // Engine default: (0, 0, 0, 1) - quaternion identity (swkotor2.exe: 0x004e0ff0 line 52)
        public Vector4 Orientation { get; set; } = new Vector4(0, 0, 0, 1);
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
        [CanBeNull]
        public LocalizedString MapNote { get; set; } = null; // Can be null when HasMapNote is false, matching Python's LocalizedString | None
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
