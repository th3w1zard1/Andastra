using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Graphics;
using Andastra.Parsing.Formats.VIS;
using Andastra.Runtime.Graphics.Common;
using Andastra.Runtime.Graphics.Common.Effects;

namespace Andastra.Runtime.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) specific area implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Area Implementation:
    /// - Based on swkotor.exe and swkotor2.exe area systems
    /// - Uses ARE (area properties) and GIT (instances) file formats
    /// - Implements walkmesh navigation and area transitions
    /// - Supports stealth XP and area restrictions
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Area loading and management functions
    /// - swkotor2.exe: LoadAreaProperties @ 0x004e26d0, SaveAreaProperties @ 0x004e11d0
    /// - swkotor2.exe: DispatchEvent @ 0x004dcfb0 for area events
    /// - Common ARE/GIT format documentation in vendor/PyKotor/wiki/
    ///
    /// Area structure:
    /// - ARE file: GFF with "ARE " signature containing lighting, fog, grass, walkmesh
    /// - GIT file: GFF with "GIT " signature containing creature/door/placeable instances
    /// - Walkmesh: Binary format for navigation and collision detection
    /// - Area properties: Unescapable, StealthXPEnabled, lighting settings
    /// </remarks>
    [PublicAPI]
    public class OdysseyArea : BaseArea
    {
        private readonly List<IEntity> _creatures = new List<IEntity>();
        private readonly List<IEntity> _placeables = new List<IEntity>();
        private readonly List<IEntity> _doors = new List<IEntity>();
        private readonly List<IEntity> _triggers = new List<IEntity>();
        private readonly List<IEntity> _waypoints = new List<IEntity>();
        private readonly List<IEntity> _sounds = new List<IEntity>();

        private string _resRef;
        private string _displayName;
        private string _tag;
        private bool _isUnescapable;
        private bool _stealthXpEnabled;
        private INavigationMesh _navigationMesh;

        // Room and visibility data for rendering
        private List<RoomInfo> _rooms;
        private VIS _visibilityData;
        private Dictionary<string, IRoomMeshData> _roomMeshes;

        // Lighting and fog properties (from ARE file)
        private uint _ambientColor;
        private uint _dynamicAmbientColor;
        private uint _fogColor;
        private bool _fogEnabled;
        private float _fogNear;
        private float _fogFar;
        private uint _sunFogColor;
        private uint _sunDiffuseColor;
        private uint _sunAmbientColor;

        // Rendering context (set by game loop or service locator)
        private IAreaRenderContext _renderContext;

        /// <summary>
        /// Creates a new Odyssey area.
        /// </summary>
        /// <param name="resRef">The resource reference name of the area.</param>
        /// <param name="areData">ARE file data containing area properties.</param>
        /// <param name="gitData">GIT file data containing entity instances.</param>
        /// <remarks>
        /// Based on area loading sequence in swkotor2.exe.
        /// Loads ARE file first for static properties, then GIT file for dynamic instances.
        /// Initializes walkmesh and area effects.
        /// </remarks>
        public OdysseyArea(string resRef, byte[] areData, byte[] gitData)
        {
            _resRef = resRef ?? throw new ArgumentNullException(nameof(resRef));
            _tag = resRef; // Default tag to resref

            // Initialize collections
            _rooms = new List<RoomInfo>();
            _roomMeshes = new Dictionary<string, IRoomMeshData>(StringComparer.OrdinalIgnoreCase);

            // Initialize lighting/fog defaults
            _ambientColor = 0xFF808080; // Gray ambient
            _dynamicAmbientColor = 0xFF808080;
            _fogColor = 0xFF808080;
            _fogEnabled = false;
            _fogNear = 0.0f;
            _fogFar = 1000.0f;
            _sunFogColor = 0xFF808080;
            _sunDiffuseColor = 0xFFFFFFFF;
            _sunAmbientColor = 0xFF808080;

            LoadAreaGeometry(areData);
            LoadEntities(gitData);
            LoadAreaProperties(areData);
            InitializeAreaEffects();
        }

        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        public override string ResRef => _resRef;

        /// <summary>
        /// The display name of the area.
        /// </summary>
        public override string DisplayName => _displayName ?? _resRef;

        /// <summary>
        /// The tag of the area.
        /// </summary>
        public override string Tag => _tag;

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        public override IEnumerable<IEntity> Creatures => _creatures;

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        public override IEnumerable<IEntity> Placeables => _placeables;

        /// <summary>
        /// All doors in this area.
        /// </summary>
        public override IEnumerable<IEntity> Doors => _doors;

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        public override IEnumerable<IEntity> Triggers => _triggers;

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        public override IEnumerable<IEntity> Waypoints => _waypoints;

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        public override IEnumerable<IEntity> Sounds => _sounds;

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        public override INavigationMesh NavigationMesh => _navigationMesh;

        /// <summary>
        /// Gets or sets whether the area is unescapable.
        /// </summary>
        /// <remarks>
        /// Based on "Unescapable" field in AreaProperties GFF.
        /// When true, players cannot leave the area.
        /// </remarks>
        public override bool IsUnescapable
        {
            get => _isUnescapable;
            set => _isUnescapable = value;
        }

        /// <summary>
        /// Gets or sets whether stealth XP is enabled for this area.
        /// </summary>
        /// <remarks>
        /// Based on "StealthXPEnabled" field in AreaProperties GFF.
        /// Controls whether stealth actions grant XP in this area.
        /// </remarks>
        public override bool StealthXPEnabled
        {
            get => _stealthXpEnabled;
            set => _stealthXpEnabled = value;
        }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        /// <remarks>
        /// Searches all entity collections for matching tag.
        /// Returns nth occurrence (0-based indexing).
        /// </remarks>
        public override IEntity GetObjectByTag(string tag, int nth = 0)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            var allEntities = _creatures.Concat(_placeables).Concat(_doors)
                                       .Concat(_triggers).Concat(_waypoints).Concat(_sounds);

            return allEntities.Where(e => string.Equals(e.Tag, tag, StringComparison.OrdinalIgnoreCase))
                             .Skip(nth).FirstOrDefault();
        }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on walkmesh projection functions in swkotor2.exe.
        /// Checks if point can be projected onto walkable surface.
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            if (_navigationMesh == null)
            {
                return false;
            }
            // Project point to surface and check if it's walkable
            Vector3 projected;
            float height;
            if (_navigationMesh.ProjectToSurface(point, out projected, out height))
            {
                int faceIndex = _navigationMesh.FindFaceAt(projected);
                return faceIndex >= 0 && _navigationMesh.IsWalkable(faceIndex);
            }
            return false;
        }

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        /// <remarks>
        /// Based on FUN_004f5070 @ 0x004f5070 in swkotor2.exe.
        /// Projects points to walkable surfaces for accurate positioning.
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            if (_navigationMesh == null)
            {
                result = point;
                height = point.Y;
                return false;
            }

            return _navigationMesh.ProjectToSurface(point, out result, out height);
        }

        /// <summary>
        /// Loads area properties from GFF data.
        /// </summary>
        /// <remarks>
        /// Based on LoadAreaProperties @ 0x004e26d0 in swkotor2.exe.
        /// Reads AreaProperties struct from ARE file GFF.
        /// Extracts Unescapable, StealthXPEnabled, and other area settings.
        /// </remarks>
        protected override void LoadAreaProperties(byte[] gffData)
        {
            // TODO: Implement GFF parsing for area properties
            // Read AreaProperties struct containing:
            // - Unescapable (bool)
            // - StealthXPEnabled (bool)
            // - StealthXPMax, StealthXPCurrent, StealthXPLoss (int)
            // - Lighting, fog, and environmental settings

            _isUnescapable = false; // Default value
            _stealthXpEnabled = false; // Default value
        }

        /// <summary>
        /// Saves area properties to GFF data.
        /// </summary>
        /// <remarks>
        /// Based on SaveAreaProperties @ 0x004e11d0 in swkotor2.exe.
        /// Writes AreaProperties struct to GFF format.
        /// Saves current area state for persistence.
        /// </remarks>
        protected override byte[] SaveAreaProperties()
        {
            // TODO: Implement GFF serialization for area properties
            // Write AreaProperties struct with current values
            throw new NotImplementedException("Area properties serialization not yet implemented");
        }

        /// <summary>
        /// Loads entities from GIT file.
        /// </summary>
        /// <remarks>
        /// Based on entity loading in swkotor2.exe.
        /// Parses GIT file GFF containing creature, door, placeable instances.
        /// Creates appropriate entity types and attaches components.
        /// </remarks>
        protected override void LoadEntities(byte[] gitData)
        {
            // TODO: Implement GIT file parsing
            // Parse GFF with "GIT " signature
            // Load Creature List, Door List, Placeable List, etc.
            // Create OdysseyEntity instances with appropriate components
        }

        /// <summary>
        /// Loads area geometry and walkmesh from ARE file.
        /// </summary>
        /// <remarks>
        /// Based on ARE file loading in swkotor2.exe.
        /// Parses ARE file GFF containing static area data.
        /// Loads walkmesh for navigation and collision detection.
        /// </remarks>
        protected override void LoadAreaGeometry(byte[] areData)
        {
            // TODO: Implement ARE file parsing
            // Parse GFF with "ARE " signature
            // Load walkmesh data, lighting, fog, grass settings
            // Create OdysseyNavigationMesh instance
            _navigationMesh = new OdysseyNavigationMesh(); // Placeholder
        }

        /// <summary>
        /// Initializes area effects and environmental systems.
        /// </summary>
        /// <remarks>
        /// Odyssey engine has basic lighting and fog effects.
        /// Sets up area-specific environmental rendering.
        /// </remarks>
        protected override void InitializeAreaEffects()
        {
            // TODO: Initialize lighting, fog, and environmental effects
            // Based on ARE file properties and engine rendering systems
        }

        /// <summary>
        /// Removes an entity from this area's collections.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Basic entity removal without physics system.
        /// Based on swkotor.exe/swkotor2.exe entity management.
        /// </remarks>
        protected override void RemoveEntityFromArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Remove from type-specific lists
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
            }
        }

        /// <summary>
        /// Adds an entity to this area's collections.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Basic entity addition without physics system.
        /// Based on swkotor.exe/swkotor2.exe entity management.
        /// </remarks>
        protected override void AddEntityToArea(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            // Add to type-specific lists
            switch (entity.ObjectType)
            {
                case ObjectType.Creature:
                    if (!_creatures.Contains(entity))
                    {
                        _creatures.Add(entity);
                    }
                    break;
                case ObjectType.Placeable:
                    if (!_placeables.Contains(entity))
                    {
                        _placeables.Add(entity);
                    }
                    break;
                case ObjectType.Door:
                    if (!_doors.Contains(entity))
                    {
                        _doors.Add(entity);
                    }
                    break;
                case ObjectType.Trigger:
                    if (!_triggers.Contains(entity))
                    {
                        _triggers.Add(entity);
                    }
                    break;
                case ObjectType.Waypoint:
                    if (!_waypoints.Contains(entity))
                    {
                        _waypoints.Add(entity);
                    }
                    break;
                case ObjectType.Sound:
                    if (!_sounds.Contains(entity))
                    {
                        _sounds.Add(entity);
                    }
                    break;
            }
        }

        /// <summary>
        /// Updates area state each frame.
        /// </summary>
        /// <remarks>
        /// Updates area effects, processes entity spawning/despawning.
        /// Handles area-specific timed events and environmental changes.
        /// </remarks>
        public override void Update(float deltaTime)
        {
            // TODO: Update area effects and environmental systems
            // Process any pending area transitions
            // Update lighting and fog effects
        }

        /// <summary>
        /// Sets the rendering context for this area.
        /// </summary>
        /// <param name="context">The rendering context providing graphics services.</param>
        /// <remarks>
        /// Based on swkotor2.exe: Area rendering uses graphics device, room mesh renderer, and basic effect.
        /// The rendering context is set by the game loop before calling Render().
        /// </remarks>
        public void SetRenderContext(IAreaRenderContext context)
        {
            _renderContext = context;
        }

        /// <summary>
        /// Sets room layout information from LYT file.
        /// </summary>
        /// <param name="rooms">List of room information from LYT file.</param>
        /// <remarks>
        /// Based on swkotor2.exe: LYT file loading populates room list with model names and positions.
        /// Called during area loading from ModuleLoader.
        /// </remarks>
        public void SetRooms(List<RoomInfo> rooms)
        {
            if (rooms == null)
            {
                _rooms = new List<RoomInfo>();
            }
            else
            {
                _rooms = new List<RoomInfo>(rooms);
            }
        }

        /// <summary>
        /// Sets visibility data from VIS file.
        /// </summary>
        /// <param name="vis">VIS file data for room visibility culling.</param>
        /// <remarks>
        /// Based on swkotor2.exe: VIS file defines which rooms are visible from each room.
        /// Used for frustum culling optimization during rendering.
        /// </remarks>
        public void SetVisibilityData(VIS vis)
        {
            _visibilityData = vis;
        }

        /// <summary>
        /// Gets or sets the ambient color (RGBA).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Ambient color from ARE file AreaProperties.
        /// Controls base lighting level for the area.
        /// </remarks>
        public uint AmbientColor
        {
            get => _ambientColor;
            set => _ambientColor = value;
        }

        /// <summary>
        /// Gets or sets the fog color (RGBA).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Fog color from ARE file AreaProperties.
        /// </remarks>
        public uint FogColor
        {
            get => _fogColor;
            set => _fogColor = value;
        }

        /// <summary>
        /// Gets or sets whether fog is enabled.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Fog enabled flag from ARE file AreaProperties.
        /// </remarks>
        public bool FogEnabled
        {
            get => _fogEnabled;
            set => _fogEnabled = value;
        }

        /// <summary>
        /// Gets or sets the fog near distance.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Fog near distance from ARE file AreaProperties.
        /// </remarks>
        public float FogNear
        {
            get => _fogNear;
            set => _fogNear = value;
        }

        /// <summary>
        /// Gets or sets the fog far distance.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: Fog far distance from ARE file AreaProperties.
        /// </remarks>
        public float FogFar
        {
            get => _fogFar;
            set => _fogFar = value;
        }

        /// <summary>
        /// Renders the area.
        /// </summary>
        /// <remarks>
        /// Handles VIS culling, transparency sorting, and lighting.
        /// Renders static geometry, area effects, and environmental elements.
        /// 
        /// Based on swkotor2.exe: Area rendering functions
        /// - Room mesh rendering with VIS culling (swkotor2.exe: FUN_0041b6b0 @ 0x0041b6b0)
        /// - VIS culling: Uses VIS file to determine which rooms are visible from current room
        /// - Lighting: Applies ambient, diffuse, and fog effects from ARE file
        /// - Room meshes: Loaded from MDL models referenced in LYT file
        /// - Rendering order: Opaque geometry first, then transparent objects
        /// </remarks>
        public override void Render()
        {
            // If no rendering context, cannot render
            if (_renderContext == null)
            {
                return;
            }

            IGraphicsDevice graphicsDevice = _renderContext.GraphicsDevice;
            IRoomMeshRenderer roomRenderer = _renderContext.RoomMeshRenderer;
            IBasicEffect basicEffect = _renderContext.BasicEffect;
            Matrix4x4 viewMatrix = _renderContext.ViewMatrix;
            Matrix4x4 projectionMatrix = _renderContext.ProjectionMatrix;
            Vector3 cameraPosition = _renderContext.CameraPosition;

            if (graphicsDevice == null || roomRenderer == null || basicEffect == null)
            {
                return;
            }

            // Apply fog settings if enabled
            if (_fogEnabled)
            {
                // Convert fog color from RGBA uint to Vector3
                Vector3 fogColorVec = new Vector3(
                    ((_fogColor >> 16) & 0xFF) / 255.0f,
                    ((_fogColor >> 8) & 0xFF) / 255.0f,
                    (_fogColor & 0xFF) / 255.0f
                );
                // Note: BasicEffect fog support depends on implementation
                // For now, we'll set fog parameters if the effect supports it
            }

            // Apply ambient lighting
            Vector3 ambientColorVec = new Vector3(
                ((_ambientColor >> 16) & 0xFF) / 255.0f,
                ((_ambientColor >> 8) & 0xFF) / 255.0f,
                (_ambientColor & 0xFF) / 255.0f
            );
            basicEffect.AmbientLightColor = ambientColorVec;
            basicEffect.LightingEnabled = true;

            // Determine current room for VIS culling
            int currentRoomIndex = FindCurrentRoom(cameraPosition);

            // Render rooms with VIS culling
            if (_rooms != null && _rooms.Count > 0)
            {
                for (int i = 0; i < _rooms.Count; i++)
                {
                    RoomInfo room = _rooms[i];

                    // VIS culling: Check if room is visible from current room
                    if (currentRoomIndex >= 0 && _visibilityData != null)
                    {
                        if (!IsRoomVisible(currentRoomIndex, i))
                        {
                            continue; // Skip this room - not visible
                        }
                    }

                    // Skip rooms without model names
                    if (string.IsNullOrEmpty(room.ModelName))
                    {
                        continue;
                    }

                    // Get or load room mesh
                    IRoomMeshData meshData;
                    if (!_roomMeshes.TryGetValue(room.ModelName, out meshData))
                    {
                        // Try to load mesh using room renderer
                        // Note: MDL loading would require access to resource system
                        // For now, we'll skip rooms that haven't been pre-loaded
                        continue;
                    }

                    if (meshData == null || meshData.VertexBuffer == null || meshData.IndexBuffer == null)
                    {
                        continue;
                    }

                    // Validate mesh data
                    if (meshData.IndexCount < 3)
                    {
                        continue; // Need at least one triangle
                    }

                    // Set up room transform
                    Vector3 roomPos = room.Position;
                    Matrix4x4 roomWorld = MatrixHelper.CreateTranslation(roomPos);

                    // Apply room rotation if specified
                    if (Math.Abs(room.Rotation) > 0.001f)
                    {
                        Matrix4x4 rotation = MatrixHelper.CreateRotationY(MathHelper.ToRadians(room.Rotation));
                        roomWorld = Matrix4x4.Multiply(rotation, roomWorld);
                    }

                    // Set up rendering state
                    graphicsDevice.SetVertexBuffer(meshData.VertexBuffer);
                    graphicsDevice.SetIndexBuffer(meshData.IndexBuffer);

                    // Set effect parameters
                    basicEffect.World = roomWorld;
                    basicEffect.View = viewMatrix;
                    basicEffect.Projection = projectionMatrix;
                    basicEffect.VertexColorEnabled = true;
                    basicEffect.LightingEnabled = true;

                    // Draw the mesh
                    foreach (IEffectPass pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        graphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            meshData.IndexCount,
                            0,
                            meshData.IndexCount / 3 // Number of triangles
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Finds the current room index based on camera/player position.
        /// </summary>
        /// <param name="position">Camera or player position.</param>
        /// <returns>Room index, or -1 if not found.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: Room finding logic uses distance-based approach.
        /// In a full implementation, this would check if position is inside room bounds.
        /// </remarks>
        private int FindCurrentRoom(Vector3 position)
        {
            if (_rooms == null || _rooms.Count == 0)
            {
                return -1;
            }

            // Find the room closest to the position (simple distance-based approach)
            // In a full implementation, we'd check if position is inside room bounds
            int closestRoomIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _rooms.Count; i++)
            {
                RoomInfo room = _rooms[i];
                float distance = Vector3.Distance(position, room.Position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoomIndex = i;
                }
            }

            return closestRoomIndex;
        }

        /// <summary>
        /// Checks if a room is visible from the current room using VIS data.
        /// </summary>
        /// <param name="currentRoomIndex">Index of the current room.</param>
        /// <param name="targetRoomIndex">Index of the room to check visibility for.</param>
        /// <returns>True if the target room is visible from the current room.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: VIS file defines visibility graph between rooms.
        /// If no VIS data, all rooms are considered visible (fallback behavior).
        /// </remarks>
        private bool IsRoomVisible(int currentRoomIndex, int targetRoomIndex)
        {
            if (_visibilityData == null || _rooms == null)
            {
                return true; // No VIS data - render all rooms
            }

            if (currentRoomIndex < 0 || currentRoomIndex >= _rooms.Count)
            {
                return true; // Invalid current room - render all
            }

            if (targetRoomIndex < 0 || targetRoomIndex >= _rooms.Count)
            {
                return false; // Invalid target room
            }

            // Always render the current room
            if (currentRoomIndex == targetRoomIndex)
            {
                return true;
            }

            // Get room model names for VIS lookup
            string currentRoomName = _rooms[currentRoomIndex].ModelName;
            string targetRoomName = _rooms[targetRoomIndex].ModelName;

            if (string.IsNullOrEmpty(currentRoomName) || string.IsNullOrEmpty(targetRoomName))
            {
                return true; // Missing room names - render all
            }

            // Check VIS data for visibility
            // VIS stores visibility as: room -> set of visible rooms
            HashSet<string> visibleRooms = _visibilityData.GetVisibleRooms(currentRoomName);
            if (visibleRooms == null)
            {
                return true; // No visibility data for this room - render all
            }

            return visibleRooms.Contains(targetRoomName.ToLowerInvariant());
        }

        /// <summary>
        /// Unloads the area and cleans up resources.
        /// </summary>
        /// <remarks>
        /// Destroys all entities, frees walkmesh and geometry resources.
        /// Ensures proper cleanup to prevent memory leaks.
        /// </remarks>
        public override void Unload()
        {
            // TODO: Implement area unloading
            // Destroy all entities in the area
            // Free walkmesh and geometry resources
            // Clean up area effects and environmental systems
        }
    }
}
