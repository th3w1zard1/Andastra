using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Content.Loaders
{
    /// <summary>
    /// Loads GIT (Game Instance Table) files for area instance data.
    /// GIT files contain the spawned instances of creatures, placeables, doors,
    /// triggers, waypoints, sounds, and encounters in an area.
    /// </summary>
    /// <remarks>
    /// GIT Loader:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) GIT file loading
    /// - Located via string references: "GIT " signature (GFF file format) in GFF file header
    /// - "tmpgit" @ 0x007be618 (temporary GIT file reference during loading)
    /// - GFF list field names for instance lists:
    ///   - "Creature List" @ 0x007bd01c (creature instances)
    ///   - "Door List" @ 0x007bd248 (door instances)
    ///   - "Placeable List" @ 0x007bd260 (placeable instances)
    ///   - "TriggerList" @ 0x007bd254 (trigger instances)
    ///   - "WaypointList" (waypoint instances)
    ///   - "SoundList" (sound instances)
    ///   - "Encounter List" @ 0x007bd050 (encounter instances)
    ///   - "StoreList" (store instances)
    ///   - "CameraList" @ 0x007bd16c (camera instances, KOTOR-specific)
    /// - GIT file format: GFF with "GIT " signature containing area instance data
    /// - Original implementation: 0x004dfbb0 @ 0x004dfbb0 loads creature instances from GIT
    ///   - Located via string reference: "Creature List" @ 0x007bd01c
    ///   - Original implementation (from decompiled 0x004dfbb0):
    ///     - Function signature: `uint 0x004dfbb0(void *this, undefined2 *param_1, uint *param_2, int param_3, int param_4)`
    ///     - param_1: GFF structure pointer
    ///     - param_2: GFF field pointer
    ///     - param_3: Area/context parameter
    ///     - param_4: Template loading flag (0 = load from saved state, non-zero = load from template)
    ///     - Iterates through "Creature List" GFF list field using 0x00412a60 to get list count
    ///     - For each creature struct (indexed via 0x00412ab0):
    ///       - Checks struct type via 0x004122d0 (must be type 4 = GFFStruct)
    ///       - If param_4 == 0 (load from saved state):
    ///         - Reads "ObjectId" (default 0x7F000000) via 0x00412d40
    ///         - Allocates creature object (0x1220 bytes) via operator_new
    ///         - Initializes creature via 0x005199e0 with ObjectId
    ///         - Calls 0x005223a0 to load creature instance data from GIT struct
    ///       - If param_4 != 0 (load from template):
    ///         - Allocates creature object (0x1220 bytes) with default ObjectId 0x7F000000
    ///         - Reads "TemplateResRef" (ResRef) via 0x00412f30
    ///         - Calls 0x005261b0 to load creature template from UTC file
    ///         - Calls 0x005223a0 to load creature instance data from GIT struct
    ///       - Reads position: "XPosition", "YPosition", "ZPosition" (float, default 0.0) via 0x00412e20
    ///       - Position validation: Calls 0x004f7590 to validate position on walkmesh (20.0 unit radius check)
    ///         - If validation succeeds, updates position to validated coordinates (local_58, local_54, local_50)
    ///       - Spawns creature at validated position via 0x0051bfc0 with area context (param_3)
    ///       - Reads orientation: "ZOrientation", "YOrientation", "XOrientation" (float, default 0.0) via 0x00412e20
    ///       - Converts orientation vector to quaternion via 0x00506550
    ///     - Returns 1 on success
    /// - 0x004e08e0 @ 0x004e08e0 loads placeable/door/store instances from GIT
    ///   - Located via string reference: "StoreList" (also handles "Door List" and "Placeable List")
    ///   - Original implementation (from decompiled 0x004e08e0):
    ///     - Iterates through "StoreList" GFF list field (also handles doors/placeables)
    ///     - For each struct: Reads "ObjectId" (default 0x7F000000), "ResRef" (template ResRef), "XPosition", "YPosition", "ZPosition" (float)
    ///     - Calls 0x005718f0 to load placeable/door template from UTP/UTD file
    ///     - Calls 0x00571310 to load instance data from GIT struct
    ///     - Reads orientation: "XOrientation", "YOrientation", "ZOrientation" (float, normalized if magnitude > threshold, converted to quaternion)
    ///     - Spawns placeable/door at position via 0x00570fe0
    /// - 0x004e01a0 @ 0x004e01a0 loads encounter instances from GIT
    /// - Parses GIT GFF structure, spawns entities at specified positions
    /// - GIT files define all spawned instances in an area (creatures, doors, placeables, triggers, waypoints, sounds, encounters, stores, cameras)
    /// - Each instance contains: TemplateResRef, Tag, Position (X/Y/Z), Orientation, and type-specific fields
    /// - Instance position fields: "XPosition", "YPosition", "ZPosition" for most types, "X", "Y", "Z" for doors/placeables
    /// - Instance orientation fields: "XOrientation", "YOrientation", "ZOrientation" (float, converted to quaternion for creatures/placeables), "Bearing" (float) for doors/placeables
    /// - Position validation: Creature positions validated on walkmesh (20.0 unit radius check) before spawning
    /// - Orientation normalization: Orientation vectors normalized if magnitude > threshold (DAT_007bd08c), default to (0, 1, 0) if invalid
    /// - Area properties stored in "AreaProperties" struct (ambient sounds, music, environment audio)
    /// - Based on GIT file format documentation in vendor/PyKotor/wiki/GFF-GIT.md
    /// </remarks>
    public class GITLoader
    {
        private readonly IGameResourceProvider _resourceProvider;

        public GITLoader(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
        }

        /// <summary>
        /// Loads the GIT data for an area.
        /// </summary>
        public async Task<GITData> LoadAsync(string areaResRef, CancellationToken ct = default(CancellationToken))
        {
            var id = new BioWare.NET.Resource.ResourceIdentifier(areaResRef, BioWare.NET.Common.ResourceType.GIT);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            // Use Parsing GITHelpers to parse the GFF
            GFF gff = GFF.FromBytes(data);
            var git = BioWare.NET.Resource.Formats.GFF.Generics.GITHelpers.ConstructGit(gff);
            return ParseGIT(git);
        }

        private GITData ParseGIT(BioWare.NET.Resource.Formats.GFF.Generics.GIT git)
        {
            var gitData = new GITData();

            // Parse creature instances
            if (git.Creatures != null)
            {
                foreach (var creature in git.Creatures)
                {
                    gitData.Creatures.Add(ParseCreatureInstance(creature));
                }
            }

            // Parse door instances
            if (git.Doors != null)
            {
                foreach (var door in git.Doors)
                {
                    gitData.Doors.Add(ParseDoorInstance(door));
                }
            }

            // Parse placeable instances
            if (git.Placeables != null)
            {
                foreach (var placeable in git.Placeables)
                {
                    gitData.Placeables.Add(ParsePlaceableInstance(placeable));
                }
            }

            // Parse trigger instances
            if (git.Triggers != null)
            {
                foreach (var trigger in git.Triggers)
                {
                    gitData.Triggers.Add(ParseTriggerInstance(trigger));
                }
            }

            // Parse waypoint instances
            if (git.Waypoints != null)
            {
                foreach (var waypoint in git.Waypoints)
                {
                    gitData.Waypoints.Add(ParseWaypointInstance(waypoint));
                }
            }

            // Parse sound instances
            if (git.Sounds != null)
            {
                foreach (var sound in git.Sounds)
                {
                    gitData.Sounds.Add(ParseSoundInstance(sound));
                }
            }

            // Parse encounter instances
            if (git.Encounters != null)
            {
                foreach (var encounter in git.Encounters)
                {
                    gitData.Encounters.Add(ParseEncounterInstance(encounter));
                }
            }

            // Parse store instances
            if (git.Stores != null)
            {
                foreach (var store in git.Stores)
                {
                    gitData.Stores.Add(ParseStoreInstance(store));
                }
            }

            // Parse camera instances (KOTOR specific)
            if (git.Cameras != null)
            {
                foreach (var camera in git.Cameras)
                {
                    gitData.Cameras.Add(ParseCameraInstance(camera));
                }
            }

            // Parse area properties if present
            gitData.AreaProperties = ParseAreaProperties(git);

            return gitData;
        }

        #region Instance Parsers

        private CreatureInstance ParseCreatureInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITCreature creature)
        {
            var instance = new CreatureInstance();

            instance.TemplateResRef = creature.ResRef.ToString();
            instance.Tag = ""; // GITCreature doesn't store tag, it would be in the template
            instance.XPosition = creature.Position.X;
            instance.YPosition = creature.Position.Y;
            instance.ZPosition = creature.Position.Z;
            instance.XOrientation = 0.0f; // Not stored in GITCreature
            instance.YOrientation = creature.Bearing;

            return instance;
        }

        private DoorInstance ParseDoorInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITDoor door)
        {
            var instance = new DoorInstance();

            instance.TemplateResRef = door.ResRef.ToString();
            instance.Tag = door.Tag;
            instance.LinkedTo = door.LinkedTo;
            instance.LinkedToFlags = (byte)door.LinkedToFlags;
            instance.LinkedToModule = door.LinkedToModule.ToString();
            instance.TransitionDestin = door.TransitionDestination.StringRef.ToString();
            instance.XPosition = door.Position.X;
            instance.YPosition = door.Position.Y;
            instance.ZPosition = door.Position.Z;
            instance.Bearing = door.Bearing;

            return instance;
        }

        private PlaceableInstance ParsePlaceableInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITPlaceable placeable)
        {
            var instance = new PlaceableInstance();

            instance.TemplateResRef = placeable.ResRef.ToString();
            instance.Tag = placeable.Tag;
            instance.XPosition = placeable.Position.X;
            instance.YPosition = placeable.Position.Y;
            instance.ZPosition = placeable.Position.Z;
            instance.Bearing = placeable.Bearing;

            return instance;
        }

        private TriggerInstance ParseTriggerInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITTrigger trigger)
        {
            var instance = new TriggerInstance();

            instance.TemplateResRef = trigger.ResRef;
            instance.Tag = trigger.Tag;
            instance.XPosition = trigger.Position.X;
            instance.YPosition = trigger.Position.Y;
            instance.ZPosition = trigger.Position.Z;
            // Note: GITTrigger doesn't have orientation fields, so we'll use defaults
            instance.XOrientation = 0.0f;
            instance.YOrientation = 0.0f;
            instance.ZOrientation = 0.0f;

            // Parse geometry
            if (trigger.Geometry != null)
            {
                foreach (var vertex in trigger.Geometry)
                {
                    instance.Geometry.Add(new System.Numerics.Vector3(vertex.X, vertex.Y, vertex.Z));
                }
            }

            return instance;
        }

        private TriggerInstance ParseTriggerInstance(GFFStruct s)
        {
            var instance = new TriggerInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            instance.ZOrientation = GetFloat(s, "ZOrientation");

            // Parse geometry
            if (s.TryGetList("Geometry", out GFFList geometryList))
            {
                foreach (GFFStruct vertexStruct in geometryList)
                {
                    float pointX = GetFloat(vertexStruct, "PointX");
                    float pointY = GetFloat(vertexStruct, "PointY");
                    float pointZ = GetFloat(vertexStruct, "PointZ");
                    instance.Geometry.Add(new System.Numerics.Vector3(pointX, pointY, pointZ));
                }
            }

            return instance;
        }

        private WaypointInstance ParseWaypointInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITWaypoint waypoint)
        {
            var instance = new WaypointInstance();

            instance.TemplateResRef = waypoint.ResRef;
            instance.Tag = waypoint.Tag;
            instance.XPosition = waypoint.Position.X;
            instance.YPosition = waypoint.Position.Y;
            instance.ZPosition = waypoint.Position.Z;
            // Convert bearing to orientation (bearing is rotation around Z axis)
            float bearingRad = waypoint.Bearing;
            instance.XOrientation = (float)Math.Sin(bearingRad);
            instance.YOrientation = (float)Math.Cos(bearingRad);
            instance.MapNote = waypoint.HasMapNote && waypoint.MapNote != null;
            instance.MapNoteEnabled = waypoint.MapNoteEnabled;

            return instance;
        }

        private WaypointInstance ParseWaypointInstance(GFFStruct s)
        {
            var instance = new WaypointInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            instance.MapNote = GetByte(s, "MapNote") != 0;
            instance.MapNoteEnabled = GetByte(s, "MapNoteEnabled") != 0;

            return instance;
        }

        private SoundInstance ParseSoundInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITSound sound)
        {
            var instance = new SoundInstance();

            instance.TemplateResRef = sound.ResRef;
            instance.Tag = sound.Tag;
            instance.XPosition = sound.Position.X;
            instance.YPosition = sound.Position.Y;
            instance.ZPosition = sound.Position.Z;
            // GITSound doesn't have GeneratedType, so use default
            instance.GeneratedType = 0;

            return instance;
        }

        private SoundInstance ParseSoundInstance(GFFStruct s)
        {
            var instance = new SoundInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.GeneratedType = GetInt(s, "GeneratedType");

            return instance;
        }

        private EncounterInstance ParseEncounterInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITEncounter encounter)
        {
            var instance = new EncounterInstance();

            instance.TemplateResRef = encounter.ResRef;
            instance.Tag = ""; // GITEncounter doesn't have Tag
            instance.XPosition = encounter.Position.X;
            instance.YPosition = encounter.Position.Y;
            instance.ZPosition = encounter.Position.Z;

            // Parse spawn points
            if (encounter.SpawnPoints != null)
            {
                foreach (var spawnPoint in encounter.SpawnPoints)
                {
                    instance.SpawnPoints.Add(new SpawnPoint
                    {
                        X = spawnPoint.X,
                        Y = spawnPoint.Y,
                        Z = spawnPoint.Z,
                        Orientation = spawnPoint.Orientation
                    });
                }
            }

            // Parse geometry
            if (encounter.Geometry != null)
            {
                foreach (var vertex in encounter.Geometry)
                {
                    instance.Geometry.Add(new System.Numerics.Vector3(vertex.X, vertex.Y, vertex.Z));
                }
            }

            return instance;
        }

        private EncounterInstance ParseEncounterInstance(GFFStruct s)
        {
            var instance = new EncounterInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");

            // Parse spawn points
            if (s.TryGetList("SpawnPointList", out GFFList spawnList))
            {
                foreach (GFFStruct spawnStruct in spawnList)
                {
                    var spawnPoint = new SpawnPoint
                    {
                        X = GetFloat(spawnStruct, "X"),
                        Y = GetFloat(spawnStruct, "Y"),
                        Z = GetFloat(spawnStruct, "Z"),
                        Orientation = GetFloat(spawnStruct, "Orientation")
                    };
                    instance.SpawnPoints.Add(spawnPoint);
                }
            }

            // Parse geometry
            if (s.TryGetList("Geometry", out GFFList geometryList))
            {
                foreach (GFFStruct vertexStruct in geometryList)
                {
                    float pointX = GetFloat(vertexStruct, "X");
                    float pointY = GetFloat(vertexStruct, "Y");
                    float pointZ = GetFloat(vertexStruct, "Z");
                    instance.Geometry.Add(new System.Numerics.Vector3(pointX, pointY, pointZ));
                }
            }

            return instance;
        }

        private StoreInstance ParseStoreInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITStore store)
        {
            var instance = new StoreInstance();

            instance.TemplateResRef = store.ResRef.ToString();
            instance.Tag = ""; // GITStore doesn't have Tag
            instance.XPosition = store.Position.X;
            instance.YPosition = store.Position.Y;
            instance.ZPosition = store.Position.Z;
            // Convert bearing to orientation (bearing is rotation around Z axis)
            float bearingRad = store.Bearing;
            instance.XOrientation = (float)Math.Sin(bearingRad);
            instance.YOrientation = (float)Math.Cos(bearingRad);

            return instance;
        }

        private StoreInstance ParseStoreInstance(GFFStruct s)
        {
            var instance = new StoreInstance();

            instance.TemplateResRef = GetResRef(s, "ResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");

            return instance;
        }

        private CameraInstance ParseCameraInstance(BioWare.NET.Resource.Formats.GFF.Generics.GITCamera camera)
        {
            var instance = new CameraInstance();

            instance.CameraID = camera.CameraId;
            instance.FieldOfView = camera.Fov;
            instance.Height = camera.Height;
            instance.MicRange = camera.MicRange;
            // Convert Vector4 to Quaternion (w, x, y, z)
            instance.Orientation = new System.Numerics.Quaternion(camera.Orientation.X, camera.Orientation.Y, camera.Orientation.Z, camera.Orientation.W);
            instance.Pitch = camera.Pitch;
            instance.Position = new System.Numerics.Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z);

            return instance;
        }

        private CameraInstance ParseCameraInstance(GFFStruct s)
        {
            var instance = new CameraInstance();

            instance.CameraID = GetInt(s, "CameraID");
            instance.FieldOfView = GetFloat(s, "FieldOfView");
            instance.Height = GetFloat(s, "Height");
            instance.MicRange = GetFloat(s, "MicRange");
            instance.Orientation = GetVector4(s, "Orientation");
            instance.Pitch = GetFloat(s, "Pitch");
            instance.Position = GetVector3(s, "Position");

            return instance;
        }

        private AreaPropertiesData ParseAreaProperties(BioWare.NET.Resource.Formats.GFF.Generics.GIT git)
        {
            var props = new AreaPropertiesData();

            // Use the already parsed properties from the GIT object
            props.AmbientSndDay = git.AmbientSoundId;
            props.AmbientSndDayVol = git.AmbientVolume;
            // Note: GIT doesn't have separate night ambient sound properties in KOTOR
            props.AmbientSndNight = git.AmbientSoundId; // Use day value as fallback
            props.AmbientSndNitVol = git.AmbientVolume; // Use day value as fallback
            props.EnvAudio = git.EnvAudio;
            props.MusicBattle = git.MusicBattleId;
            props.MusicDay = git.MusicStandardId;
            props.MusicDelay = git.MusicDelay;
            // Note: GIT doesn't have separate night music property in KOTOR
            props.MusicNight = git.MusicStandardId; // Use day value as fallback

            return props;
        }

        #endregion

        #region GFF Helpers

        private string GetString(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetString(name) : string.Empty;
        }

        private string GetResRef(GFFStruct s, string name)
        {
            if (s.Exists(name))
            {
                ResRef resRef = s.GetResRef(name);
                return resRef?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private int GetInt(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetInt32(name) : 0;
        }

        private byte GetByte(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetUInt8(name) : (byte)0;
        }

        private float GetFloat(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetSingle(name) : 0f;
        }

        private System.Numerics.Vector3 GetVector3(GFFStruct s, string name)
        {
            if (s.Exists(name))
            {
                System.Numerics.Vector3 v = s.GetVector3(name);
                return v;
            }
            return System.Numerics.Vector3.Zero;
        }

        private System.Numerics.Quaternion GetVector4(GFFStruct s, string name)
        {
            if (s.Exists(name))
            {
                System.Numerics.Vector4 v = s.GetVector4(name);
                return new System.Numerics.Quaternion(v.X, v.Y, v.Z, v.W);
            }
            return System.Numerics.Quaternion.Identity;
        }

        #endregion
    }

    #region GIT Data Classes

    /// <summary>
    /// Contains all instance data from a GIT file.
    /// </summary>
    public class GITData
    {
        public List<CreatureInstance> Creatures { get; private set; }
        public List<DoorInstance> Doors { get; private set; }
        public List<PlaceableInstance> Placeables { get; private set; }
        public List<TriggerInstance> Triggers { get; private set; }
        public List<WaypointInstance> Waypoints { get; private set; }
        public List<SoundInstance> Sounds { get; private set; }
        public List<EncounterInstance> Encounters { get; private set; }
        public List<StoreInstance> Stores { get; private set; }
        public List<CameraInstance> Cameras { get; private set; }
        public AreaPropertiesData AreaProperties { get; set; }

        public GITData()
        {
            Creatures = new List<CreatureInstance>();
            Doors = new List<DoorInstance>();
            Placeables = new List<PlaceableInstance>();
            Triggers = new List<TriggerInstance>();
            Waypoints = new List<WaypointInstance>();
            Sounds = new List<SoundInstance>();
            Encounters = new List<EncounterInstance>();
            Stores = new List<StoreInstance>();
            Cameras = new List<CameraInstance>();
        }
    }

    /// <summary>
    /// Base class for GIT instances.
    /// </summary>
    public abstract class GITInstance
    {
        public string TemplateResRef { get; set; }
        public string Tag { get; set; }
        public float XPosition { get; set; }
        public float YPosition { get; set; }
        public float ZPosition { get; set; }

        public System.Numerics.Vector3 Position
        {
            get { return new System.Numerics.Vector3(XPosition, YPosition, ZPosition); }
        }
    }

    /// <summary>
    /// Creature instance from GIT.
    /// </summary>
    public class CreatureInstance : GITInstance
    {
        public float XOrientation { get; set; }
        public float YOrientation { get; set; }

        public float Facing
        {
            get { return (float)Math.Atan2(YOrientation, XOrientation); }
        }
    }

    /// <summary>
    /// Door instance from GIT.
    /// </summary>
    public class DoorInstance : GITInstance
    {
        public float Bearing { get; set; }
        public string LinkedTo { get; set; }
        public byte LinkedToFlags { get; set; }
        public string LinkedToModule { get; set; }
        public string TransitionDestin { get; set; }
    }

    /// <summary>
    /// Placeable instance from GIT.
    /// </summary>
    public class PlaceableInstance : GITInstance
    {
        public float Bearing { get; set; }
    }

    /// <summary>
    /// Trigger instance from GIT.
    /// </summary>
    public class TriggerInstance : GITInstance
    {
        public float XOrientation { get; set; }
        public float YOrientation { get; set; }
        public float ZOrientation { get; set; }
        public List<System.Numerics.Vector3> Geometry { get; private set; }

        public TriggerInstance()
        {
            Geometry = new List<System.Numerics.Vector3>();
        }
    }

    /// <summary>
    /// Waypoint instance from GIT.
    /// </summary>
    public class WaypointInstance : GITInstance
    {
        public float XOrientation { get; set; }
        public float YOrientation { get; set; }
        [Obsolete("Use HasMapNote and MapNoteText instead")]
        public bool MapNote { get; set; }
        public bool MapNoteEnabled { get; set; }
        public bool HasMapNote { get; set; }
        public string MapNoteText { get; set; }

        public float Facing
        {
            get { return (float)Math.Atan2(YOrientation, XOrientation); }
        }

        public WaypointInstance()
        {
            HasMapNote = false;
            MapNoteText = string.Empty;
            MapNoteEnabled = false;
        }
    }

    /// <summary>
    /// Sound instance from GIT.
    /// </summary>
    public class SoundInstance : GITInstance
    {
        public int GeneratedType { get; set; }
    }

    /// <summary>
    /// Encounter instance from GIT.
    /// </summary>
    public class EncounterInstance : GITInstance
    {
        public List<SpawnPoint> SpawnPoints { get; private set; }
        public List<System.Numerics.Vector3> Geometry { get; private set; }

        public EncounterInstance()
        {
            SpawnPoints = new List<SpawnPoint>();
            Geometry = new List<System.Numerics.Vector3>();
        }
    }

    /// <summary>
    /// Store instance from GIT.
    /// </summary>
    public class StoreInstance : GITInstance
    {
        public float XOrientation { get; set; }
        public float YOrientation { get; set; }
    }

    /// <summary>
    /// Camera instance from GIT (KOTOR specific).
    /// </summary>
    public class CameraInstance
    {
        public int CameraID { get; set; }
        public float FieldOfView { get; set; }
        public float Height { get; set; }
        public float MicRange { get; set; }
        public System.Numerics.Quaternion Orientation { get; set; }
        public float Pitch { get; set; }
        public System.Numerics.Vector3 Position { get; set; }
    }

    /// <summary>
    /// Spawn point for encounters.
    /// </summary>
    public class SpawnPoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Orientation { get; set; }

        public System.Numerics.Vector3 Position
        {
            get { return new System.Numerics.Vector3(X, Y, Z); }
        }
    }

    /// <summary>
    /// Area-wide audio properties from GIT.
    /// </summary>
    public class AreaPropertiesData
    {
        public int AmbientSndDay { get; set; }
        public int AmbientSndDayVol { get; set; }
        public int AmbientSndNight { get; set; }
        public int AmbientSndNitVol { get; set; }
        public int EnvAudio { get; set; }
        public int MusicBattle { get; set; }
        public int MusicDay { get; set; }
        public int MusicDelay { get; set; }
        public int MusicNight { get; set; }
    }

    #endregion
}
