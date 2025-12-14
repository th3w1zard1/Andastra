using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using Odyssey.Content.Converters;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Navigation;

namespace Odyssey.Content.Loaders
{
    /// <summary>
    /// Orchestrates loading of game areas including layout, visibility,
    /// navigation mesh, and entity instances.
    /// </summary>
    public class AreaManager
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ModuleLoader _moduleLoader;
        private readonly GITLoader _gitLoader;
        private readonly TemplateLoader _templateLoader;
        private readonly EntityFactory _entityFactory;
        private readonly IWorld _world;

        private AreaData _currentArea;
        private string _currentAreaResRef;

        public AreaManager(
            IGameResourceProvider resourceProvider,
            IWorld world)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _world = world ?? throw new ArgumentNullException("world");

            _moduleLoader = new ModuleLoader(resourceProvider);
            _gitLoader = new GITLoader(resourceProvider);
            _templateLoader = new TemplateLoader(resourceProvider);
            _entityFactory = new EntityFactory(_templateLoader, world);
        }

        /// <summary>
        /// Gets the currently loaded area data.
        /// </summary>
        public AreaData CurrentArea
        {
            get { return _currentArea; }
        }

        /// <summary>
        /// Gets the resref of the currently loaded area.
        /// </summary>
        public string CurrentAreaResRef
        {
            get { return _currentAreaResRef; }
        }

        /// <summary>
        /// Loads an area and all its instances.
        /// </summary>
        /// <param name="areaResRef">The resref of the area to load.</param>
        /// <param name="progress">Optional progress callback (0.0 to 1.0).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The loaded area data.</returns>
        public async Task<AreaData> LoadAreaAsync(
            string areaResRef,
            Action<float, string> progress = null,
            CancellationToken ct = default(CancellationToken))
        {
            progress = progress ?? ((p, s) => { });

            // Unload current area if any
            if (_currentArea != null)
            {
                UnloadCurrentArea();
            }

            _currentAreaResRef = areaResRef;
            _currentArea = new AreaData { ResRef = areaResRef };

            progress(0.0f, "Loading layout...");
            ct.ThrowIfCancellationRequested();

            // Step 1: Load LYT (layout)
            _currentArea.Layout = await _moduleLoader.LoadLayoutAsync(areaResRef, ct);
            progress(0.1f, "Layout loaded");

            // Step 2: Load VIS (visibility)
            progress(0.15f, "Loading visibility...");
            _currentArea.Visibility = await _moduleLoader.LoadVisibilityAsync(areaResRef, ct);
            progress(0.2f, "Visibility loaded");

            // Step 3: Load walkmesh
            progress(0.25f, "Loading walkmesh...");
            if (_currentArea.Layout != null)
            {
                _currentArea.NavigationMesh = await _moduleLoader.LoadAreaNavigationAsync(_currentArea.Layout, ct);
            }
            progress(0.4f, "Walkmesh loaded");

            // Step 4: Load GIT (instances)
            progress(0.45f, "Loading instances...");
            _currentArea.InstanceData = await _gitLoader.LoadAsync(areaResRef, ct);
            progress(0.5f, "Instance data loaded");

            // Step 5: Spawn entities from GIT
            if (_currentArea.InstanceData != null)
            {
                await SpawnEntitiesAsync(_currentArea.InstanceData, progress, ct);
            }

            progress(1.0f, "Area loaded");
            return _currentArea;
        }

        private async Task SpawnEntitiesAsync(
            GITData git,
            Action<float, string> progress,
            CancellationToken ct)
        {
            int totalCount =
                git.Creatures.Count +
                git.Doors.Count +
                git.Placeables.Count +
                git.Triggers.Count +
                git.Waypoints.Count +
                git.Sounds.Count;

            int spawned = 0;
            float baseProgress = 0.5f;
            float progressRange = 0.5f;

            // Spawn creatures
            progress(baseProgress, "Spawning creatures...");
            foreach (var creature in git.Creatures)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(creature.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnCreatureAsync(
                        creature.TemplateResRef,
                        creature.Position,
                        creature.Facing,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(creature.Tag))
                    {
                        entity.Tag = creature.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
                float p = baseProgress + (progressRange * spawned / totalCount);
                progress(p, "Spawning creatures...");
            }

            // Spawn doors
            progress(baseProgress + progressRange * 0.2f, "Spawning doors...");
            foreach (var door in git.Doors)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(door.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnDoorAsync(
                        door.TemplateResRef,
                        door.Position,
                        door.Bearing,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(door.Tag))
                    {
                        entity.Tag = door.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
            }

            // Spawn placeables
            progress(baseProgress + progressRange * 0.4f, "Spawning placeables...");
            foreach (var placeable in git.Placeables)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(placeable.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnPlaceableAsync(
                        placeable.TemplateResRef,
                        placeable.Position,
                        placeable.Bearing,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(placeable.Tag))
                    {
                        entity.Tag = placeable.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
            }

            // Spawn triggers
            progress(baseProgress + progressRange * 0.6f, "Spawning triggers...");
            foreach (var trigger in git.Triggers)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(trigger.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnTriggerAsync(
                        trigger.TemplateResRef,
                        trigger.Position,
                        trigger.Geometry,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(trigger.Tag))
                    {
                        entity.Tag = trigger.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
            }

            // Spawn waypoints
            progress(baseProgress + progressRange * 0.8f, "Spawning waypoints...");
            foreach (var waypoint in git.Waypoints)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(waypoint.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnWaypointAsync(
                        waypoint.TemplateResRef,
                        waypoint.Position,
                        waypoint.Facing,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(waypoint.Tag))
                    {
                        entity.Tag = waypoint.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
            }

            // Spawn sounds
            progress(baseProgress + progressRange * 0.9f, "Spawning sounds...");
            foreach (var sound in git.Sounds)
            {
                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(sound.TemplateResRef))
                {
                    var entity = await _entityFactory.SpawnSoundAsync(
                        sound.TemplateResRef,
                        sound.Position,
                        ct);

                    if (entity != null && !string.IsNullOrEmpty(sound.Tag))
                    {
                        entity.Tag = sound.Tag;
                    }
                    _currentArea.Entities.Add(entity);
                }

                spawned++;
            }
        }

        /// <summary>
        /// Unloads the current area and destroys all entities.
        /// </summary>
        public void UnloadCurrentArea()
        {
            if (_currentArea == null)
            {
                return;
            }

            // Destroy all entities
            foreach (var entity in _currentArea.Entities)
            {
                if (entity != null)
                {
                    _world.RemoveEntity(entity);
                    if (entity is Entity coreEntity)
                    {
                        coreEntity.Destroy();
                    }
                }
            }

            _currentArea.Entities.Clear();
            _currentArea = null;
            _currentAreaResRef = null;
        }

        /// <summary>
        /// Tests if a room is visible from another room.
        /// </summary>
        public bool IsRoomVisible(string fromRoom, string toRoom)
        {
            if (_currentArea?.Visibility == null)
            {
                return true; // No visibility data - assume all visible
            }

            var vis = _currentArea.Visibility;
            if (vis.Rooms.TryGetValue(fromRoom.ToLowerInvariant(), out var visibleRooms))
            {
                return visibleRooms.Contains(toRoom.ToLowerInvariant());
            }

            return false;
        }

        /// <summary>
        /// Gets the room at a given position based on layout data.
        /// </summary>
        public string GetRoomAtPosition(Vector3 position)
        {
            if (_currentArea?.Layout == null)
            {
                return null;
            }

            foreach (var room in _currentArea.Layout.Rooms)
            {
                // Simple bounding box check
                // In a full implementation, this would check against room geometry
                Vector3 roomPos = new Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                float dist = Vector3.Distance(new Vector3(position.X, position.Y, 0), new Vector3(roomPos.X, roomPos.Y, 0));

                if (dist < 100f) // Rough check
                {
                    return room.Model;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a path through the area.
        /// </summary>
        public IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            if (_currentArea?.NavigationMesh == null)
            {
                return null;
            }

            return _currentArea.NavigationMesh.FindPath(start, goal);
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        public bool GetGroundHeight(Vector3 position, out float height)
        {
            height = 0f;

            if (_currentArea?.NavigationMesh == null)
            {
                return false;
            }

            Vector3 result;
            return _currentArea.NavigationMesh.ProjectToSurface(position, out result, out height);
        }

        /// <summary>
        /// Checks if a position is on walkable ground.
        /// </summary>
        public bool IsWalkable(Vector3 position)
        {
            if (_currentArea?.NavigationMesh == null)
            {
                return false;
            }

            int face = _currentArea.NavigationMesh.FindFaceAt(position);
            if (face < 0)
            {
                return false;
            }

            return _currentArea.NavigationMesh.IsWalkable(face);
        }
    }

    /// <summary>
    /// Contains all loaded data for an area.
    /// </summary>
    public class AreaData
    {
        /// <summary>
        /// The resref of the area.
        /// </summary>
        public string ResRef { get; set; }

        /// <summary>
        /// Room layout data.
        /// </summary>
        public LYT Layout { get; set; }

        /// <summary>
        /// Room visibility data.
        /// </summary>
        public VIS Visibility { get; set; }

        /// <summary>
        /// Combined navigation mesh for the area.
        /// </summary>
        public NavigationMesh NavigationMesh { get; set; }

        /// <summary>
        /// Raw instance data from GIT.
        /// </summary>
        public GITData InstanceData { get; set; }

        /// <summary>
        /// Spawned entities in the area.
        /// </summary>
        public List<IEntity> Entities { get; private set; }

        /// <summary>
        /// Area-wide audio properties.
        /// </summary>
        public AreaPropertiesData AudioProperties
        {
            get { return InstanceData?.AreaProperties; }
        }

        public AreaData()
        {
            Entities = new List<IEntity>();
        }
    }
}
