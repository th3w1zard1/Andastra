using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Save;

namespace Andastra.Runtime.Core.Module
{
    /// <summary>
    /// Runtime area implementation.
    /// Represents a game area with rooms, objects, and navigation.
    /// </summary>
    /// <remarks>
    /// Runtime Area:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) area system
    /// - Located via string references: "Area" @ 0x007be340, "AreaName" @ 0x007be340, "AREANAME" @ 0x007be1dc
    /// - "Area_Name" @ 0x007be73c, "AreaId" @ 0x007bef48, "AreaNumber" @ 0x007c7324
    /// - "Mod_Area_list" @ 0x007be748, "Mod_Entry_Area" @ 0x007be9b4 (module entry area)
    /// - "Target_Area" @ 0x007c02d4, "AreaObject" @ 0x007c0b70, "AreaList" @ 0x007c0b7c
    /// - "AreaListSize" @ 0x007c0b88, "AreaListMaxSize" @ 0x007c0ba4, "AreaPoints" @ 0x007c0b98
    /// - Area map: "AreaMap" @ 0x007bd118, "AreaMapResX" @ 0x007bd10c, "AreaMapResY" @ 0x007bd100
    /// - "AreaMapData" @ 0x007bd0e4, "AreaMapDataSize" @ 0x007bd0f0
    /// - "NW_MAP_PIN_AREA_%i" @ 0x007bd824 (map pin format string)
    /// - Area properties: "AreaProperties" @ 0x007bd228, "AreaEffectList" @ 0x007bd0d4
    /// - "AreaEffectId" @ 0x007c13f8 (area-wide effect identifier)
    /// - Events: "EVENT_AREA_TRANSITION" @ 0x007bcbdc, "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles area events including EVENT_AREA_TRANSITION (case 0x1a) and EVENT_REMOVE_FROM_AREA (case 4)
    /// - Error messages:
    ///   - "X co-ordinate outside of area, should be in [%f, %f]" @ 0x007c224c
    ///   - "Y co-ordinate outside of area, should be in [%f, %f]" @ 0x007c2284
    ///   - "Area %s is not a valid area." @ 0x007c22bc
    ///   - "Area %s not valid." @ 0x007c22dc
    /// - Debug display: "    Area Tag: " @ 0x007cb12c, "Area Name: " @ 0x007cb13c
    /// - GUI: "LBL_Area" @ 0x007cdac0, "LBL_AREANAME" @ 0x007cedb8, "areatrans_p" @ 0x007d0bdc
    /// - Pathfinding module: "?nwsareapathfind.cpp" @ 0x007be3ff indicates area pathfinding implementation
    /// - Pathfinding error: "we can not return a closest result if points are reversed." @ 0x007be478
    /// - Movement error: "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
    /// - Save game integration: FUN_004eb750 @ 0x004eb750 saves AREANAME to save game NFO file (local_78 variable holds area name)
    /// - Original implementation: Areas contain entities, rooms, walkmesh, visibility data
    /// - Based on ARE/GIT/LYT/VIS file formats documented in vendor/PyKotor/wiki/
    /// - ARE = Static area properties (lighting, fog, grass) - GFF with "ARE " signature
    /// - GIT = Dynamic object instances (creatures, doors, etc.) - GFF with "GIT " signature
    /// - LYT = Room layout and doorhooks - Binary format
    /// - VIS = Room visibility for culling - Binary format for frustum culling optimization
    /// - Area serialization: FUN_005226d0 @ 0x005226d0 saves AreaId and area state
    /// - Temporary area reference: "tmparea" @ 0x007be620 (used during area loading)
    /// </remarks>
    public class RuntimeArea : IArea
    {
        private readonly List<IEntity> _creatures;
        private readonly List<IEntity> _placeables;
        private readonly List<IEntity> _doors;
        private readonly List<IEntity> _triggers;
        private readonly List<IEntity> _waypoints;
        private readonly List<IEntity> _sounds;
        private readonly List<IEntity> _stores;
        private readonly List<IEntity> _encounters;
        private readonly Dictionary<string, List<IEntity>> _entitiesByTag;
        private readonly Dictionary<ScriptEvent, string> _scripts;

        // Camera states storage (cameras are not runtime entities, stored separately for save system)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cameras are stored in GIT CameraList but not as runtime entities
        // Camera states are stored here when area is loaded from GIT for save system compatibility
        private readonly List<Save.EntityState> _cameraStates;

        public RuntimeArea()
        {
            _creatures = new List<IEntity>();
            _placeables = new List<IEntity>();
            _doors = new List<IEntity>();
            _triggers = new List<IEntity>();
            _waypoints = new List<IEntity>();
            _sounds = new List<IEntity>();
            _stores = new List<IEntity>();
            _encounters = new List<IEntity>();
            _entitiesByTag = new Dictionary<string, List<IEntity>>(StringComparer.OrdinalIgnoreCase);
            _scripts = new Dictionary<ScriptEvent, string>();
            _cameraStates = new List<Save.EntityState>();

            // Defaults
            ResRef = string.Empty;
            DisplayName = string.Empty;
            Tag = string.Empty;
            Rooms = new List<RoomInfo>();
            IsUnescapable = false; // Areas are escapable by default
            StealthXPEnabled = true; // Stealth XP enabled by default (matches original engine)
        }

        #region IArea Implementation

        public string ResRef { get; set; }
        public string DisplayName { get; set; }
        public string Tag { get; set; }

        public IEnumerable<IEntity> Creatures { get { return _creatures; } }
        public IEnumerable<IEntity> Placeables { get { return _placeables; } }
        public IEnumerable<IEntity> Doors { get { return _doors; } }
        public IEnumerable<IEntity> Triggers { get { return _triggers; } }
        public IEnumerable<IEntity> Waypoints { get { return _waypoints; } }
        public IEnumerable<IEntity> Sounds { get { return _sounds; } }

        public IEntity GetObjectByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            List<IEntity> entities;
            if (_entitiesByTag.TryGetValue(tag, out entities))
            {
                if (nth >= 0 && nth < entities.Count)
                {
                    return entities[nth];
                }
            }
            return null;
        }

        public INavigationMesh NavigationMesh { get; set; }

        public bool IsUnescapable { get; set; }

        /// <summary>
        /// Gets or sets whether stealth XP is enabled for this area.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): StealthXPEnabled stored in AreaProperties GFF
        /// Located via string references: "StealthXPEnabled" @ 0x007bd1b4
        /// Ghidra analysis: FUN_004e26d0 @ 0x004e26d0 reads from AreaProperties GFF at offset +0x2f4
        /// FUN_004e11d0 @ 0x004e11d0 writes to AreaProperties GFF at offset +0x2f4
        /// </summary>
        public bool StealthXPEnabled { get; set; }

        public bool IsPointWalkable(Vector3 point)
        {
            if (NavigationMesh == null)
            {
                return false;
            }

            int face = NavigationMesh.FindFaceAt(point);
            return face >= 0 && NavigationMesh.IsWalkable(face);
        }

        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = 0f;

            if (NavigationMesh == null)
            {
                return false;
            }

            return NavigationMesh.ProjectToSurface(point, out result, out height);
        }

        #endregion

        #region Extended Properties

        /// <summary>
        /// Room layout information.
        /// </summary>
        public List<RoomInfo> Rooms { get; set; }

        /// <summary>
        /// Ambient color (RGBA).
        /// </summary>
        public uint AmbientColor { get; set; }

        /// <summary>
        /// Dynamic ambient color (RGBA).
        /// </summary>
        public uint DynamicAmbientColor { get; set; }

        /// <summary>
        /// Fog color (RGBA).
        /// </summary>
        public uint FogColor { get; set; }

        /// <summary>
        /// Whether fog is enabled.
        /// </summary>
        public bool FogEnabled { get; set; }

        /// <summary>
        /// Fog near distance.
        /// </summary>
        public float FogNear { get; set; }

        /// <summary>
        /// Fog far distance.
        /// </summary>
        public float FogFar { get; set; }

        /// <summary>
        /// Sun fog color (RGBA).
        /// </summary>
        public uint SunFogColor { get; set; }

        /// <summary>
        /// Sun diffuse color (RGBA).
        /// </summary>
        public uint SunDiffuseColor { get; set; }

        /// <summary>
        /// Sun ambient color (RGBA).
        /// </summary>
        public uint SunAmbientColor { get; set; }

        /// <summary>
        /// Whether grass is enabled.
        /// </summary>
        public bool GrassEnabled { get; set; }

        /// <summary>
        /// Grass texture name.
        /// </summary>
        public string GrassTexture { get; set; }

        /// <summary>
        /// Grass density.
        /// </summary>
        public float GrassDensity { get; set; }

        /// <summary>
        /// Grass quad size.
        /// </summary>
        public float GrassQuadSize { get; set; }

        /// <summary>
        /// Ambient music resource.
        /// </summary>
        public int MusicDay { get; set; }

        /// <summary>
        /// Night ambient music resource.
        /// </summary>
        public int MusicNight { get; set; }

        /// <summary>
        /// Battle music resource.
        /// </summary>
        public int MusicBattle { get; set; }

        /// <summary>
        /// Ambient sound resource.
        /// </summary>
        public int AmbientSndDay { get; set; }

        /// <summary>
        /// Night ambient sound resource.
        /// </summary>
        public int AmbientSndNight { get; set; }

        /// <summary>
        /// Whether the area is interior.
        /// </summary>
        public bool IsInterior { get; set; }

        /// <summary>
        /// Whether the area is underground.
        /// </summary>
        public bool IsUnderground { get; set; }

        /// <summary>
        /// Whether the area has weather.
        /// </summary>
        public bool HasWeather { get; set; }

        /// <summary>
        /// Current weather type.
        /// </summary>
        public int WeatherType { get; set; }

        #endregion

        #region Entity Management

        /// <summary>
        /// Adds an entity to the area.
        /// </summary>
        public void AddEntity(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Add to type-specific list
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    _creatures.Add(entity);
                    break;
                case ObjectType.Placeable:
                    _placeables.Add(entity);
                    break;
                case ObjectType.Door:
                    _doors.Add(entity);
                    break;
                case ObjectType.Trigger:
                    _triggers.Add(entity);
                    break;
                case ObjectType.Waypoint:
                    _waypoints.Add(entity);
                    break;
                case ObjectType.Sound:
                    _sounds.Add(entity);
                    break;
                case ObjectType.Store:
                    _stores.Add(entity);
                    break;
                case ObjectType.Encounter:
                    _encounters.Add(entity);
                    break;
            }

            // Add to tag index
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                List<IEntity> tagList;
                if (!_entitiesByTag.TryGetValue(entity.Tag, out tagList))
                {
                    tagList = new List<IEntity>();
                    _entitiesByTag[entity.Tag] = tagList;
                }
                tagList.Add(entity);
            }
        }

        /// <summary>
        /// Removes an entity from the area.
        /// </summary>
        public void RemoveEntity(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Remove from type-specific list
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    _creatures.Remove(entity);
                    break;
                case ObjectType.Placeable:
                    _placeables.Remove(entity);
                    break;
                case ObjectType.Door:
                    _doors.Remove(entity);
                    break;
                case ObjectType.Trigger:
                    _triggers.Remove(entity);
                    break;
                case ObjectType.Waypoint:
                    _waypoints.Remove(entity);
                    break;
                case ObjectType.Sound:
                    _sounds.Remove(entity);
                    break;
                case ObjectType.Store:
                    _stores.Remove(entity);
                    break;
                case ObjectType.Encounter:
                    _encounters.Remove(entity);
                    break;
            }

            // Remove from tag index
            if (!string.IsNullOrEmpty(entity.Tag))
            {
                List<IEntity> tagList;
                if (_entitiesByTag.TryGetValue(entity.Tag, out tagList))
                {
                    tagList.Remove(entity);
                }
            }
        }

        /// <summary>
        /// Gets all entities in the area.
        /// </summary>
        public IEnumerable<IEntity> GetAllEntities()
        {
            return _creatures
                .Concat(_placeables)
                .Concat(_doors)
                .Concat(_triggers)
                .Concat(_waypoints)
                .Concat(_sounds)
                .Concat(_stores)
                .Concat(_encounters);
        }

        /// <summary>
        /// Gets all entities of a specific type.
        /// </summary>
        public IEnumerable<IEntity> GetEntitiesByType(ObjectType type)
        {
            switch (type)
            {
                case ObjectType.Creature:
                    return _creatures;
                case ObjectType.Placeable:
                    return _placeables;
                case ObjectType.Door:
                    return _doors;
                case ObjectType.Trigger:
                    return _triggers;
                case ObjectType.Waypoint:
                    return _waypoints;
                case ObjectType.Sound:
                    return _sounds;
                case ObjectType.Store:
                    return _stores;
                case ObjectType.Encounter:
                    return _encounters;
                default:
                    return Enumerable.Empty<IEntity>();
            }
        }

        /// <summary>
        /// Gets entities within a radius.
        /// </summary>
        public IEnumerable<IEntity> GetEntitiesInRadius(Vector3 center, float radius, ObjectType typeMask = ObjectType.All)
        {
            float radiusSq = radius * radius;

            foreach (IEntity entity in GetAllEntities())
            {
                if ((entity.ObjectType & typeMask) == 0)
                {
                    continue;
                }

                Interfaces.Components.ITransformComponent transform = entity.GetComponent<Interfaces.Components.ITransformComponent>();
                if (transform != null)
                {
                    float distSq = Vector3.DistanceSquared(center, transform.Position);
                    if (distSq <= radiusSq)
                    {
                        yield return entity;
                    }
                }
            }
        }

        #endregion

        /// <summary>
        /// Updates the area state (area effects, lighting, weather, entity spawning/despawning).
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update.</param>
        public void Update(float deltaTime)
        {
            // Basic implementation - area effects, lighting, weather updates would go here
            // Engine-specific implementations should override this method
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00404cf0 @ 0x00404cf0 updates area state
        }

        /// <summary>
        /// Gets camera states stored in this area.
        /// Cameras are not runtime entities, so they're stored separately for save system compatibility.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cameras are stored in GIT CameraList but not as runtime entities.
        /// </summary>
        public IReadOnlyList<EntityState> GetCameraStates()
        {
            return _cameraStates;
        }

        /// <summary>
        /// Adds a camera state to this area.
        /// Called when loading area from GIT to store camera data for save system.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera states are stored when area is loaded from GIT.
        /// </summary>
        public void AddCameraState(EntityState cameraState)
        {
            if (cameraState != null)
            {
                _cameraStates.Add(cameraState);
            }
        }

        /// <summary>
        /// Clears all camera states from this area.
        /// </summary>
        public void ClearCameraStates()
        {
            _cameraStates.Clear();
        }

        #region Script Management

        /// <summary>
        /// Sets a script for an area event.
        /// </summary>
        public void SetScript(ScriptEvent eventType, string scriptResRef)
        {
            if (string.IsNullOrEmpty(scriptResRef))
            {
                _scripts.Remove(eventType);
            }
            else
            {
                _scripts[eventType] = scriptResRef;
            }
        }

        /// <summary>
        /// Gets the script for an area event.
        /// </summary>
        public string GetScript(ScriptEvent eventType)
        {
            string script;
            if (_scripts.TryGetValue(eventType, out script))
            {
                return script;
            }
            return string.Empty;
        }

        #endregion
    }

    /// <summary>
    /// Room information from LYT file.
    /// </summary>
    public class RoomInfo
    {
        /// <summary>
        /// Room model name.
        /// </summary>
        public string ModelName { get; set; }

        /// <summary>
        /// Room position.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Room rotation (degrees).
        /// </summary>
        public float Rotation { get; set; }

        /// <summary>
        /// Visibility group IDs this room can see.
        /// </summary>
        public List<int> VisibleRooms { get; set; }

        public RoomInfo()
        {
            ModelName = string.Empty;
            Position = Vector3.Zero;
            Rotation = 0f;
            VisibleRooms = new List<int>();
        }
    }
}

