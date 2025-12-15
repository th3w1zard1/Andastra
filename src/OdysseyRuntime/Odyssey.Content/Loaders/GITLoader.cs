using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;

namespace Odyssey.Content.Loaders
{
    /// <summary>
    /// Loads GIT (Game Instance Table) files for area instance data.
    /// GIT files contain the spawned instances of creatures, placeables, doors,
    /// triggers, waypoints, sounds, and encounters in an area.
    /// </summary>
    /// <remarks>
    /// GIT Loader:
    /// - Based on swkotor2.exe GIT file loading
    /// - Located via string references: "GIT " signature (GFF file format)
    /// - GIT file format: GFF with "GIT " signature containing area instance data
    /// - Lists: "Creature List", "Door List", "Placeable List", "TriggerList", "WaypointList", "SoundList", "Encounter List"
    /// - Original implementation: Parses GIT GFF structure, spawns entities at specified positions
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
            var id = new CSharpKOTOR.Resources.ResourceIdentifier(areaResRef, CSharpKOTOR.Resources.ResourceType.GIT);
            byte[] data = await _resourceProvider.GetResourceBytesAsync(id, ct);
            if (data == null)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var reader = new GFFBinaryReader(stream);
                GFF gff = reader.Load();
                return ParseGIT(gff.Root);
            }
        }

        private GITData ParseGIT(GFFStruct root)
        {
            var git = new GITData();

            // Parse creature instances
            if (root.TryGetList("Creature List", out GFFList creatureList))
            {
                foreach (GFFStruct creatureStruct in creatureList)
                {
                    git.Creatures.Add(ParseCreatureInstance(creatureStruct));
                }
            }

            // Parse door instances
            if (root.TryGetList("Door List", out GFFList doorList))
            {
                foreach (GFFStruct doorStruct in doorList)
                {
                    git.Doors.Add(ParseDoorInstance(doorStruct));
                }
            }

            // Parse placeable instances
            if (root.TryGetList("Placeable List", out GFFList placeableList))
            {
                foreach (GFFStruct placeableStruct in placeableList)
                {
                    git.Placeables.Add(ParsePlaceableInstance(placeableStruct));
                }
            }

            // Parse trigger instances
            if (root.TryGetList("TriggerList", out GFFList triggerList))
            {
                foreach (GFFStruct triggerStruct in triggerList)
                {
                    git.Triggers.Add(ParseTriggerInstance(triggerStruct));
                }
            }

            // Parse waypoint instances
            if (root.TryGetList("WaypointList", out GFFList waypointList))
            {
                foreach (GFFStruct waypointStruct in waypointList)
                {
                    git.Waypoints.Add(ParseWaypointInstance(waypointStruct));
                }
            }

            // Parse sound instances
            if (root.TryGetList("SoundList", out GFFList soundList))
            {
                foreach (GFFStruct soundStruct in soundList)
                {
                    git.Sounds.Add(ParseSoundInstance(soundStruct));
                }
            }

            // Parse encounter instances
            if (root.TryGetList("Encounter List", out GFFList encounterList))
            {
                foreach (GFFStruct encounterStruct in encounterList)
                {
                    git.Encounters.Add(ParseEncounterInstance(encounterStruct));
                }
            }

            // Parse store instances
            if (root.TryGetList("StoreList", out GFFList storeList))
            {
                foreach (GFFStruct storeStruct in storeList)
                {
                    git.Stores.Add(ParseStoreInstance(storeStruct));
                }
            }

            // Parse camera instances (KOTOR specific)
            if (root.TryGetList("CameraList", out GFFList cameraList))
            {
                foreach (GFFStruct cameraStruct in cameraList)
                {
                    git.Cameras.Add(ParseCameraInstance(cameraStruct));
                }
            }

            // Parse area properties if present
            git.AreaProperties = ParseAreaProperties(root);

            return git;
        }

        #region Instance Parsers

        private CreatureInstance ParseCreatureInstance(GFFStruct s)
        {
            var instance = new CreatureInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");

            return instance;
        }

        private DoorInstance ParseDoorInstance(GFFStruct s)
        {
            var instance = new DoorInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.LinkedTo = GetString(s, "LinkedTo");
            instance.LinkedToFlags = GetByte(s, "LinkedToFlags");
            instance.LinkedToModule = GetResRef(s, "LinkedToModule");
            instance.TransitionDestin = GetString(s, "TransitionDestin");
            instance.XPosition = GetFloat(s, "X");
            instance.YPosition = GetFloat(s, "Y");
            instance.ZPosition = GetFloat(s, "Z");
            instance.Bearing = GetFloat(s, "Bearing");

            return instance;
        }

        private PlaceableInstance ParsePlaceableInstance(GFFStruct s)
        {
            var instance = new PlaceableInstance();

            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "X");
            instance.YPosition = GetFloat(s, "Y");
            instance.ZPosition = GetFloat(s, "Z");
            instance.Bearing = GetFloat(s, "Bearing");

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

        private AreaPropertiesData ParseAreaProperties(GFFStruct root)
        {
            var props = new AreaPropertiesData();

            if (root.TryGetStruct("AreaProperties", out GFFStruct areaProps))
            {
                props.AmbientSndDay = GetInt(areaProps, "AmbientSndDay");
                props.AmbientSndDayVol = GetInt(areaProps, "AmbientSndDayVol");
                props.AmbientSndNight = GetInt(areaProps, "AmbientSndNight");
                props.AmbientSndNitVol = GetInt(areaProps, "AmbientSndNitVol");
                props.EnvAudio = GetInt(areaProps, "EnvAudio");
                props.MusicBattle = GetInt(areaProps, "MusicBattle");
                props.MusicDay = GetInt(areaProps, "MusicDay");
                props.MusicDelay = GetInt(areaProps, "MusicDelay");
                props.MusicNight = GetInt(areaProps, "MusicNight");
            }

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
        public bool MapNote { get; set; }
        public bool MapNoteEnabled { get; set; }

        public float Facing
        {
            get { return (float)Math.Atan2(YOrientation, XOrientation); }
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
