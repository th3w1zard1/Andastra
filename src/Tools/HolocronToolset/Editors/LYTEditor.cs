using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using Andastra.Parsing.Resource.Formats.LYT;
using MDLAuto = Andastra.Parsing.Formats.MDL.MDLAuto;
using ResRef = Andastra.Parsing.Common.ResRef;
using Quaternion = Andastra.Utility.Geometry.Quaternion;
using TPCAuto = Andastra.Parsing.Formats.TPC.TPCAuto;
using TPC = Andastra.Parsing.Formats.TPC.TPC;
using TPCTextureFormat = Andastra.Parsing.Formats.TPC.TPCTextureFormat;
using HolocronToolset.Widgets;
using HolocronToolset.Editors.LYT;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:29
    // Original: class LYTEditor(Editor):
    public partial class LYTEditor : Editor
    {
        private LYT _lyt;
        private LYTEditorSettings _settings;
        private Dictionary<string, string> _importedTextures = new Dictionary<string, string>(); // Maps texture name to file path
        private Dictionary<string, string> _importedModels = new Dictionary<string, string>(); // Maps model name (ResRef) to MDL file path
        private ModelBrowser _modelBrowser; // Model browser widget for displaying imported models
        private TextureBrowser _textureBrowser; // Texture browser widget for displaying imported textures
        private LYTGraphicsScene _graphicsScene; // Graphics scene for rendering LYT layout elements

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:32-73
        // Original: def __init__(self, parent, installation):
        public LYTEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "LYT Editor", "lyt",
                new[] { ResourceType.LYT },
                new[] { ResourceType.LYT },
                installation)
        {
            _lyt = new LYT();
            _settings = new LYTEditorSettings();

            InitializeComponent();
            SetupUI();
            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            var panel = new StackPanel();
            Content = panel;
        }

        private void SetupUI()
        {
            // UI setup - will be implemented when XAML is available
            // Initialize graphics scene
            InitializeGraphicsScene();
            // Initialize model browser widget
            InitializeModelBrowser();
            // Initialize texture browser widget
            InitializeTextureBrowser();
        }

        /// <summary>
        /// Initializes the graphics scene for rendering LYT layout elements.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:50-52
        /// Original: self.scene: QGraphicsScene = QGraphicsScene()
        /// </summary>
        private void InitializeGraphicsScene()
        {
            try
            {
                // Try to find graphics scene from XAML if available
                _graphicsScene = this.FindControl<LYTGraphicsScene>("graphicsScene");
            }
            catch
            {
                // Graphics scene not found in XAML - will create programmatically if needed
            }

            // Create graphics scene if not found from XAML
            if (_graphicsScene == null)
            {
                _graphicsScene = new LYTGraphicsScene();
            }
        }

        /// <summary>
        /// Initializes the model browser widget.
        /// </summary>
        private void InitializeModelBrowser()
        {
            try
            {
                // Try to find model browser from XAML if available
                _modelBrowser = this.FindControl<ModelBrowser>("modelBrowser");
            }
            catch
            {
                // Model browser not found in XAML - will create programmatically if needed
            }

            // Create model browser if not found from XAML
            if (_modelBrowser == null)
            {
                _modelBrowser = new ModelBrowser();
                _modelBrowser.ModelSelected += OnModelSelected;
                _modelBrowser.ModelChanged += OnModelChanged;
            }

            // Initialize with current imported models
            if (_modelBrowser != null && _importedModels != null)
            {
                _modelBrowser.UpdateModels(_importedModels);
            }
        }

        /// <summary>
        /// Initializes the texture browser widget.
        /// </summary>
        private void InitializeTextureBrowser()
        {
            try
            {
                // Try to find texture browser from XAML if available
                _textureBrowser = this.FindControl<TextureBrowser>("textureBrowser");
            }
            catch
            {
                // Texture browser not found in XAML - will create programmatically if needed
            }

            // Create texture browser if not found from XAML
            if (_textureBrowser == null)
            {
                _textureBrowser = new TextureBrowser();
                _textureBrowser.TextureSelected += OnTextureSelected;
                _textureBrowser.TextureChanged += OnTextureChanged;
            }

            // Initialize with current imported textures
            if (_textureBrowser != null && _importedTextures != null)
            {
                _textureBrowser.UpdateTextures(_importedTextures);
            }
        }

        /// <summary>
        /// Handles model selection in the browser.
        /// </summary>
        private void OnModelSelected(object sender, string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return;
            }

            // Model selected - could trigger preview or usage
            System.Console.WriteLine($"Model selected in browser: {modelName}");
        }

        /// <summary>
        /// Handles model change in the browser.
        /// </summary>
        private void OnModelChanged(object sender, string modelName)
        {
            // Model changed - update any dependent UI
            if (!string.IsNullOrEmpty(modelName))
            {
                System.Console.WriteLine($"Model changed in browser: {modelName}");
            }
        }

        /// <summary>
        /// Handles texture selection in the browser.
        /// </summary>
        private void OnTextureSelected(object sender, string textureName)
        {
            if (string.IsNullOrEmpty(textureName))
            {
                return;
            }

            // Texture selected - could trigger preview or usage
            System.Console.WriteLine($"Texture selected in browser: {textureName}");
        }

        /// <summary>
        /// Handles texture change in the browser.
        /// </summary>
        private void OnTextureChanged(object sender, string textureName)
        {
            // Texture changed - update any dependent UI
            if (!string.IsNullOrEmpty(textureName))
            {
                System.Console.WriteLine($"Texture changed in browser: {textureName}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:127-131
        // Original: def add_room(self):
        public void AddRoom()
        {
            var room = new LYTRoom(new ResRef("default_room"), new Vector3(0, 0, 0));
            _lyt.Rooms.Add(room);
            UpdateScene();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:133-150
        // Original: def add_track(self):
        public void AddTrack()
        {
            if (_lyt.Rooms.Count < 2)
            {
                return;
            }

            var track = new LYTTrack(new ResRef("default_track"), new Vector3(0, 0, 0));

            // Find path through connected rooms
            var startRoom = _lyt.Rooms[0];
            var endRoom = _lyt.Rooms.Count > 1 ? _lyt.Rooms[1] : startRoom;
            var path = FindPath(startRoom, endRoom);

            if (path != null && path.Count > 0)
            {
                _lyt.Tracks.Add(track);
            }

            UpdateScene();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:152-179
        // Original: def find_path(self, start: LYTRoom, end: LYTRoom) -> list[LYTRoom] | None:
        public List<LYTRoom> FindPath(LYTRoom start, LYTRoom end)
        {
            if (start == null || end == null)
            {
                return null;
            }

            if (start.Equals(end))
            {
                return new List<LYTRoom> { start };
            }

            // Simple pathfinding - check if rooms are connected
            if (start.Connections != null && start.Connections.Contains(end))
            {
                return new List<LYTRoom> { start, end };
            }

            // A* pathfinding implementation
            var queue = new List<Tuple<float, LYTRoom, List<LYTRoom>>>
            {
                Tuple.Create(0f, start, new List<LYTRoom> { start })
            };
            var visited = new HashSet<LYTRoom> { start };

            while (queue.Count > 0)
            {
                queue.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                var current = queue[0];
                queue.RemoveAt(0);

                var (_, currentRoom, path) = current;

                if (currentRoom.Equals(end))
                {
                    return path;
                }

                if (currentRoom.Connections != null)
                {
                    foreach (var nextRoom in currentRoom.Connections.Where(conn => !visited.Contains(conn)))
                    {
                        visited.Add(nextRoom);
                        var newPath = new List<LYTRoom>(path) { nextRoom };
                        var priority = newPath.Count + (nextRoom.Position - end.Position).Length();
                        queue.Add(Tuple.Create(priority, nextRoom, newPath));
                    }
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:181-184
        // Original: def add_obstacle(self):
        public void AddObstacle()
        {
            var obstacle = new LYTObstacle(new ResRef("default_obstacle"), new Vector3(0, 0, 0));
            _lyt.Obstacles.Add(obstacle);
            UpdateScene();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:186-210
        // Original: def add_door_hook(self):
        public void AddDoorHook()
        {
            if (_lyt.Rooms.Count == 0)
            {
                return;
            }

            var firstRoom = _lyt.Rooms[0];

            var doorhook = new LYTDoorHook(
                firstRoom.Model,
                "",
                new Vector3(0, 0, 0),
                new Vector4(0, 0, 0, 1)
            );

            _lyt.Doorhooks.Add(doorhook);
            UpdateScene();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:212-214
        // Original: def generate_walkmesh(self):
        public void GenerateWalkmesh()
        {
            // Implement walkmesh generation logic here
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:216-218
        // Original: def update_zoom(self, value: int):
        public void UpdateZoom(int value)
        {
            if (_graphicsScene == null)
            {
                InitializeGraphicsScene();
            }

            if (_graphicsScene != null)
            {
                // Convert value (0-100) to zoom level (0.1-10.0)
                double zoomLevel = value / 100.0;
                _graphicsScene.ZoomLevel = zoomLevel;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:220-229
        // Original: def update_scene(self):
        public void UpdateScene()
        {
            if (_lyt == null)
            {
                return;
            }

            // Validate and ensure LYT data consistency
            ValidateAndFixLYTData();

            // Update room connections based on door hooks
            UpdateRoomConnections();

            // Update graphics scene to render all LYT elements
            UpdateGraphicsScene();
        }

        /// <summary>
        /// Updates the graphics scene to render all LYT layout elements.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:225-234
        /// Original: 
        ///     self.scene.clear()
        ///     for room in self._lyt.rooms:
        ///         self.scene.addItem(RoomItem(room, self))
        ///     for track in self._lyt.tracks:
        ///         self.scene.addItem(TrackItem(track, self))
        ///     for obstacle in self._lyt.obstacles:
        ///         self.scene.addItem(ObstacleItem(obstacle, self))
        ///     for doorhook in self._lyt.doorhooks:
        ///         self.scene.addItem(DoorHookItem(doorhook, self))
        /// </summary>
        private void UpdateGraphicsScene()
        {
            if (_graphicsScene == null)
            {
                InitializeGraphicsScene();
            }

            if (_graphicsScene == null)
            {
                return;
            }

            // Clear existing items
            _graphicsScene.Clear();

            // Add room items
            if (_lyt.Rooms != null)
            {
                foreach (var room in _lyt.Rooms)
                {
                    if (room != null)
                    {
                        var roomItem = new RoomItem(_graphicsScene, room);
                        _graphicsScene.AddItem(roomItem);
                    }
                }
            }

            // Add track items
            if (_lyt.Tracks != null)
            {
                foreach (var track in _lyt.Tracks)
                {
                    if (track != null)
                    {
                        var trackItem = new TrackItem(_graphicsScene, track);
                        _graphicsScene.AddItem(trackItem);
                    }
                }
            }

            // Add obstacle items
            if (_lyt.Obstacles != null)
            {
                foreach (var obstacle in _lyt.Obstacles)
                {
                    if (obstacle != null)
                    {
                        var obstacleItem = new ObstacleItem(_graphicsScene, obstacle);
                        _graphicsScene.AddItem(obstacleItem);
                    }
                }
            }

            // Add door hook items
            if (_lyt.DoorHooks != null)
            {
                foreach (var doorHook in _lyt.DoorHooks)
                {
                    if (doorHook != null)
                    {
                        var doorHookItem = new DoorHookItem(_graphicsScene, doorHook);
                        _graphicsScene.AddItem(doorHookItem);
                    }
                }
            }
        }

        /// <summary>
        /// Validates and fixes LYT data to ensure consistency.
        /// Performs comprehensive validation of all LYT components.
        /// </summary>
        private void ValidateAndFixLYTData()
        {
            if (_lyt == null)
            {
                return;
            }

            // Validate rooms
            ValidateRooms();

            // Validate tracks
            ValidateTracks();

            // Validate obstacles
            ValidateObstacles();

            // Validate door hooks
            ValidateDoorHooks();
        }

        /// <summary>
        /// Validates room data: ResRefs, positions, and ensures no duplicates.
        /// </summary>
        private void ValidateRooms()
        {
            if (_lyt.Rooms == null)
            {
                _lyt.Rooms = new List<LYTRoom>();
                return;
            }

            var validRooms = new List<LYTRoom>();
            var seenRoomNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roomIndex = 0;

            foreach (var room in _lyt.Rooms)
            {
                if (room == null)
                {
                    System.Console.WriteLine($"Warning: Null room at index {roomIndex}, skipping.");
                    roomIndex++;
                    continue;
                }

                // Validate ResRef
                if (room.Model == null)
                {
                    System.Console.WriteLine($"Warning: Room at index {roomIndex} has null Model, setting to default.");
                    room.Model = new ResRef("default_room");
                }
                else
                {
                    string modelStr = room.Model.ToString();
                    if (string.IsNullOrEmpty(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Room at index {roomIndex} has empty Model, setting to default.");
                        room.Model = new ResRef("default_room");
                    }
                    else if (!ResRef.IsValid(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Room at index {roomIndex} has invalid ResRef '{modelStr}', truncating if needed.");
                        try
                        {
                            room.Model.SetData(modelStr, truncate: true);
                        }
                        catch
                        {
                            room.Model = new ResRef("default_room");
                        }
                    }
                }

                // Validate position
                if (float.IsNaN(room.Position.X) || float.IsNaN(room.Position.Y) || float.IsNaN(room.Position.Z) ||
                    float.IsInfinity(room.Position.X) || float.IsInfinity(room.Position.Y) || float.IsInfinity(room.Position.Z))
                {
                    System.Console.WriteLine($"Warning: Room '{room.Model}' at index {roomIndex} has invalid position (NaN/Infinity), resetting to (0, 0, 0).");
                    room.Position = new Vector3(0, 0, 0);
                }

                // Check for duplicate room names (case-insensitive)
                string roomName = room.Model.ToString().ToLowerInvariant();
                if (seenRoomNames.Contains(roomName))
                {
                    System.Console.WriteLine($"Warning: Duplicate room name '{room.Model}' at index {roomIndex}, appending index to make unique.");
                    room.Model = new ResRef($"{room.Model}_{roomIndex}");
                    roomName = room.Model.ToString().ToLowerInvariant();
                }

                seenRoomNames.Add(roomName);

                // Ensure Connections is initialized
                if (room.Connections == null)
                {
                    room.Connections = new HashSet<LYTRoom>();
                }

                validRooms.Add(room);
                roomIndex++;
            }

            _lyt.Rooms = validRooms;
        }

        /// <summary>
        /// Validates track data: ResRefs and positions.
        /// </summary>
        private void ValidateTracks()
        {
            if (_lyt.Tracks == null)
            {
                _lyt.Tracks = new List<LYTTrack>();
                return;
            }

            var validTracks = new List<LYTTrack>();
            var trackIndex = 0;

            foreach (var track in _lyt.Tracks)
            {
                if (track == null)
                {
                    System.Console.WriteLine($"Warning: Null track at index {trackIndex}, skipping.");
                    trackIndex++;
                    continue;
                }

                // Validate ResRef
                if (track.Model == null)
                {
                    System.Console.WriteLine($"Warning: Track at index {trackIndex} has null Model, setting to default.");
                    track.Model = new ResRef("default_track");
                }
                else
                {
                    string modelStr = track.Model.ToString();
                    if (string.IsNullOrEmpty(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Track at index {trackIndex} has empty Model, setting to default.");
                        track.Model = new ResRef("default_track");
                    }
                    else if (!ResRef.IsValid(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Track at index {trackIndex} has invalid ResRef '{modelStr}', truncating if needed.");
                        try
                        {
                            track.Model.SetData(modelStr, truncate: true);
                        }
                        catch
                        {
                            track.Model = new ResRef("default_track");
                        }
                    }
                }

                // Validate position
                if (float.IsNaN(track.Position.X) || float.IsNaN(track.Position.Y) || float.IsNaN(track.Position.Z) ||
                    float.IsInfinity(track.Position.X) || float.IsInfinity(track.Position.Y) || float.IsInfinity(track.Position.Z))
                {
                    System.Console.WriteLine($"Warning: Track '{track.Model}' at index {trackIndex} has invalid position (NaN/Infinity), resetting to (0, 0, 0).");
                    track.Position = new Vector3(0, 0, 0);
                }

                validTracks.Add(track);
                trackIndex++;
            }

            _lyt.Tracks = validTracks;
        }

        /// <summary>
        /// Validates obstacle data: ResRefs and positions.
        /// </summary>
        private void ValidateObstacles()
        {
            if (_lyt.Obstacles == null)
            {
                _lyt.Obstacles = new List<LYTObstacle>();
                return;
            }

            var validObstacles = new List<LYTObstacle>();
            var obstacleIndex = 0;

            foreach (var obstacle in _lyt.Obstacles)
            {
                if (obstacle == null)
                {
                    System.Console.WriteLine($"Warning: Null obstacle at index {obstacleIndex}, skipping.");
                    obstacleIndex++;
                    continue;
                }

                // Validate ResRef
                if (obstacle.Model == null)
                {
                    System.Console.WriteLine($"Warning: Obstacle at index {obstacleIndex} has null Model, setting to default.");
                    obstacle.Model = new ResRef("default_obstacle");
                }
                else
                {
                    string modelStr = obstacle.Model.ToString();
                    if (string.IsNullOrEmpty(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Obstacle at index {obstacleIndex} has empty Model, setting to default.");
                        obstacle.Model = new ResRef("default_obstacle");
                    }
                    else if (!ResRef.IsValid(modelStr))
                    {
                        System.Console.WriteLine($"Warning: Obstacle at index {obstacleIndex} has invalid ResRef '{modelStr}', truncating if needed.");
                        try
                        {
                            obstacle.Model.SetData(modelStr, truncate: true);
                        }
                        catch
                        {
                            obstacle.Model = new ResRef("default_obstacle");
                        }
                    }
                }

                // Validate position
                if (float.IsNaN(obstacle.Position.X) || float.IsNaN(obstacle.Position.Y) || float.IsNaN(obstacle.Position.Z) ||
                    float.IsInfinity(obstacle.Position.X) || float.IsInfinity(obstacle.Position.Y) || float.IsInfinity(obstacle.Position.Z))
                {
                    System.Console.WriteLine($"Warning: Obstacle '{obstacle.Model}' at index {obstacleIndex} has invalid position (NaN/Infinity), resetting to (0, 0, 0).");
                    obstacle.Position = new Vector3(0, 0, 0);
                }

                validObstacles.Add(obstacle);
                obstacleIndex++;
            }

            _lyt.Obstacles = validObstacles;
        }

        /// <summary>
        /// Validates door hook data: room references, ResRefs, positions, and quaternions.
        /// </summary>
        private void ValidateDoorHooks()
        {
            if (_lyt.DoorHooks == null)
            {
                _lyt.DoorHooks = new List<LYTDoorHook>();
                return;
            }

            // Build a map of room names (case-insensitive) for quick lookup
            var roomNameMap = new Dictionary<string, LYTRoom>(StringComparer.OrdinalIgnoreCase);
            if (_lyt.Rooms != null)
            {
                foreach (var room in _lyt.Rooms)
                {
                    if (room != null && room.Model != null)
                    {
                        string roomName = room.Model.ToString().ToLowerInvariant();
                        if (!roomNameMap.ContainsKey(roomName))
                        {
                            roomNameMap[roomName] = room;
                        }
                    }
                }
            }

            var validDoorHooks = new List<LYTDoorHook>();
            var doorHookIndex = 0;

            foreach (var doorHook in _lyt.DoorHooks)
            {
                if (doorHook == null)
                {
                    System.Console.WriteLine($"Warning: Null door hook at index {doorHookIndex}, skipping.");
                    doorHookIndex++;
                    continue;
                }

                // Validate room reference (must match an existing room, case-insensitive)
                if (string.IsNullOrEmpty(doorHook.Room))
                {
                    System.Console.WriteLine($"Warning: Door hook at index {doorHookIndex} has empty Room name, setting to first room if available.");
                    if (_lyt.Rooms != null && _lyt.Rooms.Count > 0 && _lyt.Rooms[0] != null && _lyt.Rooms[0].Model != null)
                    {
                        doorHook.Room = _lyt.Rooms[0].Model.ToString();
                    }
                    else
                    {
                        System.Console.WriteLine($"Warning: No rooms available, skipping door hook at index {doorHookIndex}.");
                        doorHookIndex++;
                        continue;
                    }
                }

                string roomName = doorHook.Room.ToLowerInvariant();
                if (!roomNameMap.ContainsKey(roomName))
                {
                    System.Console.WriteLine($"Warning: Door hook at index {doorHookIndex} references non-existent room '{doorHook.Room}', setting to first room if available.");
                    if (_lyt.Rooms != null && _lyt.Rooms.Count > 0 && _lyt.Rooms[0] != null && _lyt.Rooms[0].Model != null)
                    {
                        doorHook.Room = _lyt.Rooms[0].Model.ToString();
                        roomName = doorHook.Room.ToLowerInvariant();
                    }
                    else
                    {
                        System.Console.WriteLine($"Warning: No rooms available, skipping door hook at index {doorHookIndex}.");
                        doorHookIndex++;
                        continue;
                    }
                }

                // Validate door name (should not be empty, but can be any string)
                if (string.IsNullOrEmpty(doorHook.Door))
                {
                    System.Console.WriteLine($"Warning: Door hook at index {doorHookIndex} has empty Door name, setting to default.");
                    doorHook.Door = "default_door";
                }

                // Validate position
                if (float.IsNaN(doorHook.Position.X) || float.IsNaN(doorHook.Position.Y) || float.IsNaN(doorHook.Position.Z) ||
                    float.IsInfinity(doorHook.Position.X) || float.IsInfinity(doorHook.Position.Y) || float.IsInfinity(doorHook.Position.Z))
                {
                    System.Console.WriteLine($"Warning: Door hook '{doorHook.Room}/{doorHook.Door}' at index {doorHookIndex} has invalid position (NaN/Infinity), resetting to (0, 0, 0).");
                    doorHook.Position = new Vector3(0, 0, 0);
                }

                // Validate and normalize quaternion
                if (doorHook.Orientation.X == 0 && doorHook.Orientation.Y == 0 && doorHook.Orientation.Z == 0 && doorHook.Orientation.W == 0)
                {
                    System.Console.WriteLine($"Warning: Door hook '{doorHook.Room}/{doorHook.Door}' at index {doorHookIndex} has zero quaternion, setting to identity.");
                    doorHook.Orientation = Quaternion.Identity;
                }
                else
                {
                    // Normalize quaternion if needed
                    doorHook.Orientation = NormalizeQuaternion(doorHook.Orientation);
                }

                validDoorHooks.Add(doorHook);
                doorHookIndex++;
            }

            _lyt.DoorHooks = validDoorHooks;
        }

        /// <summary>
        /// Normalizes a quaternion to ensure it has unit length.
        /// Returns identity quaternion if the input is invalid (zero length).
        /// </summary>
        /// <param name="q">The quaternion to normalize.</param>
        /// <returns>A normalized quaternion.</returns>
        private Quaternion NormalizeQuaternion(Quaternion q)
        {
            float lengthSquared = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;

            // Check for zero or near-zero length
            if (lengthSquared < float.Epsilon)
            {
                return Quaternion.Identity;
            }

            // Check for NaN or Infinity
            if (float.IsNaN(lengthSquared) || float.IsInfinity(lengthSquared) ||
                float.IsNaN(q.X) || float.IsNaN(q.Y) || float.IsNaN(q.Z) || float.IsNaN(q.W) ||
                float.IsInfinity(q.X) || float.IsInfinity(q.Y) || float.IsInfinity(q.Z) || float.IsInfinity(q.W))
            {
                return Quaternion.Identity;
            }

            float length = (float)Math.Sqrt(lengthSquared);

            // Normalize if not already normalized (allow small tolerance)
            if (Math.Abs(length - 1.0f) > 0.0001f)
            {
                float invLength = 1.0f / length;
                return new Quaternion(q.X * invLength, q.Y * invLength, q.Z * invLength, q.W * invLength);
            }

            return q;
        }

        /// <summary>
        /// Updates room connections based on door hooks.
        /// Rooms that share door hooks (same position within tolerance) are considered connected.
        /// </summary>
        private void UpdateRoomConnections()
        {
            if (_lyt == null || _lyt.Rooms == null || _lyt.DoorHooks == null)
            {
                return;
            }

            // Clear all existing connections
            foreach (var room in _lyt.Rooms)
            {
                if (room != null && room.Connections != null)
                {
                    room.Connections.Clear();
                }
            }

            // Build a map of room names (case-insensitive) for quick lookup
            var roomNameMap = new Dictionary<string, LYTRoom>(StringComparer.OrdinalIgnoreCase);
            foreach (var room in _lyt.Rooms)
            {
                if (room != null && room.Model != null)
                {
                    string roomName = room.Model.ToString().ToLowerInvariant();
                    if (!roomNameMap.ContainsKey(roomName))
                    {
                        roomNameMap[roomName] = room;
                    }
                }
            }

            // Group door hooks by position (within tolerance) to find connections
            // Door hooks at the same position (within 0.1 units) likely connect rooms
            const float positionTolerance = 0.1f;
            var doorHookGroups = new Dictionary<string, List<LYTDoorHook>>();

            foreach (var doorHook in _lyt.DoorHooks)
            {
                if (doorHook == null)
                {
                    continue;
                }

                // Create a position key rounded to tolerance
                int xKey = (int)(doorHook.Position.X / positionTolerance);
                int yKey = (int)(doorHook.Position.Y / positionTolerance);
                int zKey = (int)(doorHook.Position.Z / positionTolerance);
                string positionKey = $"{xKey}_{yKey}_{zKey}";

                if (!doorHookGroups.ContainsKey(positionKey))
                {
                    doorHookGroups[positionKey] = new List<LYTDoorHook>();
                }

                doorHookGroups[positionKey].Add(doorHook);
            }

            // Connect rooms that share door hook positions
            foreach (var group in doorHookGroups.Values)
            {
                if (group.Count < 2)
                {
                    continue; // Need at least 2 door hooks at same position to connect rooms
                }

                // Get all unique rooms from this group of door hooks
                var roomsInGroup = new HashSet<LYTRoom>();
                foreach (var doorHook in group)
                {
                    if (doorHook == null || string.IsNullOrEmpty(doorHook.Room))
                    {
                        continue;
                    }

                    string roomName = doorHook.Room.ToLowerInvariant();
                    if (roomNameMap.TryGetValue(roomName, out LYTRoom room))
                    {
                        roomsInGroup.Add(room);
                    }
                }

                // Connect all rooms in this group to each other (bidirectional)
                var roomsList = roomsInGroup.ToList();
                for (int i = 0; i < roomsList.Count; i++)
                {
                    for (int j = i + 1; j < roomsList.Count; j++)
                    {
                        var room1 = roomsList[i];
                        var room2 = roomsList[j];

                        if (room1 != null && room2 != null)
                        {
                            if (room1.Connections == null)
                            {
                                room1.Connections = new HashSet<LYTRoom>();
                            }
                            if (room2.Connections == null)
                            {
                                room2.Connections = new HashSet<LYTRoom>();
                            }

                            room1.Connections.Add(room2);
                            room2.Connections.Add(room1);
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:231-235
        // Original: def import_texture(self):
        public async void ImportTexture()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            try
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Import Texture",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Image Files")
                        {
                            Patterns = new[] { "*.tpc", "*.tga", "*.dds", "*.png", "*.jpg", "*.jpeg", "*.bmp" },
                            MimeTypes = new[] { "image/tga", "image/dds", "image/png", "image/jpeg", "image/bmp" }
                        },
                        new FilePickerFileType("TPC Files") { Patterns = new[] { "*.tpc" } },
                        new FilePickerFileType("TGA Files") { Patterns = new[] { "*.tga" } },
                        new FilePickerFileType("DDS Files") { Patterns = new[] { "*.dds" } },
                        new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                        new FilePickerFileType("JPEG Files") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                        new FilePickerFileType("BMP Files") { Patterns = new[] { "*.bmp" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files == null || files.Count == 0)
                {
                    return;
                }

                foreach (var file in files)
                {
                    string filePath = file.Path.LocalPath;
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        continue;
                    }

                    await ImportTextureFile(filePath);
                }

                UpdateTextureBrowser();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error importing texture: {ex}");
            }
        }

        private async Task ImportTextureFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    System.Console.WriteLine($"Error: Texture file does not exist: {filePath}");
                    return;
                }

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                string targetResref = fileName;

                // Validate file extension
                string[] supportedExtensions = { ".tpc", ".tga", ".dds", ".png", ".jpg", ".jpeg", ".bmp" };
                bool isSupported = false;
                foreach (string ext in supportedExtensions)
                {
                    if (extension == ext)
                    {
                        isSupported = true;
                        break;
                    }
                }

                if (!isSupported)
                {
                    System.Console.WriteLine($"Error: Unsupported texture format: {extension}. Supported formats: TPC, TGA, DDS, PNG, JPG, BMP");
                    return;
                }

                // Determine if we need to convert the texture
                bool needsConversion = extension != ".tpc" && extension != ".tga" && extension != ".dds";

                string overridePath = GetOverrideDirectory();
                if (string.IsNullOrEmpty(overridePath))
                {
                    System.Console.WriteLine("Warning: Could not determine override directory. Texture will not be saved to installation.");
                    return;
                }

                // Ensure override/textures directory exists
                string texturesPath = Path.Combine(overridePath, "textures");
                if (!Directory.Exists(texturesPath))
                {
                    try
                    {
                        Directory.CreateDirectory(texturesPath);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Could not create textures directory at {texturesPath}: {ex}");
                        return;
                    }
                }

                string outputTpcPath = Path.Combine(texturesPath, $"{targetResref}.tpc");
                string txiPath = Path.ChangeExtension(filePath, ".txi");

                TPC tpc = null;

                // Read the texture based on its format
                // TPCAuto.ReadTpc can directly handle TPC, TGA, and DDS formats
                if (extension == ".tpc" || extension == ".tga" || extension == ".dds")
                {
                    try
                    {
                        // TPC, TGA, and DDS can be read directly by TPCAuto
                        tpc = TPCAuto.ReadTpc(filePath, txiSource: File.Exists(txiPath) ? txiPath : null);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Failed to read {extension.ToUpperInvariant()} file {filePath}: {ex}");
                        return;
                    }
                }
                else if (needsConversion)
                {
                    // PNG, JPG, BMP formats require conversion to TPC
                    // Load the image using Avalonia's Bitmap, extract RGBA pixel data, and create TPC
                    try
                    {
                        tpc = ConvertImageToTpc(filePath);
                        if (tpc == null)
                        {
                            System.Console.WriteLine($"Error: Failed to convert {extension.ToUpperInvariant()} file {filePath} to TPC format.");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Failed to convert {extension.ToUpperInvariant()} file {filePath} to TPC: {ex}");
                        return;
                    }
                }

                if (tpc == null)
                {
                    System.Console.WriteLine($"Failed to load texture from {filePath}");
                    return;
                }

                // Check if output file already exists and handle overwrite
                if (File.Exists(outputTpcPath))
                {
                    System.Console.WriteLine($"Warning: Texture {targetResref}.tpc already exists in override directory. It will be overwritten.");
                }

                // Write as TPC to override directory
                try
                {
                    TPCAuto.WriteTpc(tpc, outputTpcPath, ResourceType.TPC);
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"Error: Failed to write TPC file to {outputTpcPath}: {ex}");
                    return;
                }

                // Write TXI file if it exists
                if (!string.IsNullOrEmpty(tpc.Txi))
                {
                    try
                    {
                        string outputTxiPath = Path.ChangeExtension(outputTpcPath, ".txi");
                        File.WriteAllText(outputTxiPath, tpc.Txi, System.Text.Encoding.ASCII);
                        System.Console.WriteLine($"Also wrote TXI file: {Path.GetFileName(outputTxiPath)}");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Warning: Failed to write TXI file: {ex}");
                    }
                }

                // Store the imported texture reference
                _importedTextures[targetResref] = outputTpcPath;

                System.Console.WriteLine($"Successfully imported texture: {targetResref} -> {outputTpcPath}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error importing texture file {filePath}: {ex}");
            }
        }

        /// <summary>
        /// Converts a PNG, JPG, or BMP image file to TPC format.
        /// Loads the image using Avalonia's Bitmap, extracts RGBA pixel data, and creates a TPC object.
        /// </summary>
        /// <param name="filePath">Path to the image file (PNG, JPG, or BMP).</param>
        /// <returns>TPC object created from the image, or null if conversion fails.</returns>
        private TPC ConvertImageToTpc(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                // Load the image using Avalonia's Bitmap
                Bitmap bitmap;
                using (var fileStream = File.OpenRead(filePath))
                {
                    bitmap = new Bitmap(fileStream);
                }

                // Get image dimensions
                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;

                if (width <= 0 || height <= 0)
                {
                    return null;
                }

                // Extract RGBA pixel data from the bitmap
                // Avalonia bitmaps use BGRA format, we need to convert to RGBA
                byte[] rgbaData = ExtractRgbaFromBitmap(bitmap, width, height);

                if (rgbaData == null || rgbaData.Length == 0)
                {
                    return null;
                }

                // Create TPC object
                TPC tpc = new TPC();
                tpc.IsAnimated = false;
                tpc.IsCubeMap = false;

                // Check if image has alpha channel
                bool hasAlpha = HasAlphaChannel(rgbaData);

                // Determine format
                TPCTextureFormat format = hasAlpha ? TPCTextureFormat.RGBA : TPCTextureFormat.RGB;

                // Set the mipmap data
                // For RGB format, we need to convert RGBA to RGB
                byte[] formatData;
                if (hasAlpha)
                {
                    formatData = rgbaData;
                }
                else
                {
                    // Convert RGBA to RGB by removing alpha channel
                    formatData = new byte[width * height * 3];
                    for (int i = 0; i < width * height; i++)
                    {
                        formatData[i * 3 + 0] = rgbaData[i * 4 + 0]; // R
                        formatData[i * 3 + 1] = rgbaData[i * 4 + 1]; // G
                        formatData[i * 3 + 2] = rgbaData[i * 4 + 2]; // B
                    }
                }

                // Use TPC.SetSingle to properly set the format and data
                tpc.SetSingle(formatData, format, width, height);

                return tpc;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error converting image to TPC: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Extracts RGBA pixel data from an Avalonia Bitmap.
        /// Converts from BGRA (Avalonia's native format) to RGBA.
        /// </summary>
        /// <param name="bitmap">The Avalonia Bitmap to extract pixels from.</param>
        /// <param name="width">Width of the image.</param>
        /// <param name="height">Height of the image.</param>
        /// <returns>RGBA pixel data as byte array, or null if extraction fails.</returns>
        private unsafe byte[] ExtractRgbaFromBitmap(Bitmap bitmap, int width, int height)
        {
            try
            {
                // Convert Bitmap to WriteableBitmap to access pixel data
                // Avalonia's regular Bitmap may not support Lock() in all versions
                WriteableBitmap writeableBitmap;
                if (bitmap is WriteableBitmap wb)
                {
                    writeableBitmap = wb;
                }
                else
                {
                    // Create a RenderTargetBitmap and render the original bitmap to it
                    // In Avalonia 11, RenderTargetBitmap doesn't have Lock(), so we save to stream and reload
                    var renderTarget = new RenderTargetBitmap(new PixelSize(width, height));
                    using (var context = renderTarget.CreateDrawingContext())
                    {
                        context.DrawImage(bitmap, new Rect(0, 0, width, height));
                    }

                    // Save RenderTargetBitmap to memory stream and decode as WriteableBitmap
                    using (var memoryStream = new MemoryStream())
                    {
                        renderTarget.Save(memoryStream);
                        memoryStream.Position = 0;
                        writeableBitmap = WriteableBitmap.Decode(memoryStream);
                    }
                    renderTarget.Dispose();
                }

                // Extract pixel data using Lock()
                byte[] rgbaData = new byte[width * height * 4];
                using (var lockedBitmap = writeableBitmap.Lock())
                {
                    int rowStride = lockedBitmap.RowBytes;
                    unsafe
                    {
                        byte* pixelPtr = (byte*)lockedBitmap.Address;

                        // Read pixel data row by row
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                // Calculate pixel offset (RGBA format, 4 bytes per pixel)
                                int pixelOffset = y * rowStride + x * 4;
                                int dstIndex = (y * width + x) * 4;

                                // Avalonia uses RGBA8888 format, so data is already in RGBA order
                                rgbaData[dstIndex + 0] = pixelPtr[pixelOffset + 0]; // R
                                rgbaData[dstIndex + 1] = pixelPtr[pixelOffset + 1]; // G
                                rgbaData[dstIndex + 2] = pixelPtr[pixelOffset + 2]; // B
                                rgbaData[dstIndex + 3] = pixelPtr[pixelOffset + 3]; // A
                            }
                        }
                    }
                }

                // Clean up if we created a new WriteableBitmap
                if (!(bitmap is WriteableBitmap))
                {
                    writeableBitmap.Dispose();
                }

                return rgbaData;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error extracting RGBA from bitmap: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Checks if pixel data contains an alpha channel with non-opaque values.
        /// </summary>
        /// <param name="pixels">RGBA pixel data (4 bytes per pixel).</param>
        /// <returns>True if any pixel has alpha < 255, false otherwise.</returns>
        private static bool HasAlphaChannel(byte[] pixels)
        {
            if (pixels == null || pixels.Length < 4)
            {
                return false;
            }

            // Check alpha channel (every 4th byte starting at index 3)
            // Early exit on first non-opaque pixel
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0xFF)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetOverrideDirectory()
        {
            if (_installation == null)
            {
                return null;
            }

            try
            {
                string installPath = _installation.Path;
                if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                {
                    return null;
                }

                // Standard KOTOR override directory is at <installPath>/override
                string overridePath = Path.Combine(installPath, "override");
                return overridePath;
            }
            catch
            {
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:237-241
        // Original: def import_model(self):
        public async void ImportModel()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            try
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Import Model",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Model Files")
                        {
                            Patterns = new[] { "*.mdl", "*.mdx" },
                            MimeTypes = new[] { "application/x-binary" }
                        },
                        new FilePickerFileType("MDL Files") { Patterns = new[] { "*.mdl" } },
                        new FilePickerFileType("MDX Files") { Patterns = new[] { "*.mdx" } },
                        new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                    }
                };

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files == null || files.Count == 0)
                {
                    return;
                }

                foreach (var file in files)
                {
                    string filePath = file.Path.LocalPath;
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        continue;
                    }

                    await ImportModelFile(filePath);
                }

                UpdateModelBrowser();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error importing model: {ex}");
            }
        }

        private async Task ImportModelFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    System.Console.WriteLine($"Error: Model file does not exist: {filePath}");
                    return;
                }

                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath).ToLowerInvariant();
                string targetResref = fileName;

                // Validate file extension
                bool isMdl = extension == ".mdl";
                bool isMdx = extension == ".mdx";

                if (!isMdl && !isMdx)
                {
                    System.Console.WriteLine($"Error: Unsupported model format: {extension}. Supported formats: MDL, MDX");
                    return;
                }

                string overridePath = GetOverrideDirectory();
                if (string.IsNullOrEmpty(overridePath))
                {
                    System.Console.WriteLine("Warning: Could not determine override directory. Model will not be saved to installation.");
                    return;
                }

                // Ensure override/models directory exists
                string modelsPath = Path.Combine(overridePath, "models");
                if (!Directory.Exists(modelsPath))
                {
                    try
                    {
                        Directory.CreateDirectory(modelsPath);
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Could not create models directory at {modelsPath}: {ex}");
                        return;
                    }
                }

                string sourceMdlPath = null;
                string sourceMdxPath = null;
                string outputMdlPath = null;
                string outputMdxPath = null;

                if (isMdl)
                {
                    // User selected MDL file
                    sourceMdlPath = filePath;
                    outputMdlPath = Path.Combine(modelsPath, $"{targetResref}.mdl");

                    // Look for corresponding MDX file in the same directory
                    string sourceMdxPathCandidate = Path.ChangeExtension(filePath, ".mdx");
                    if (File.Exists(sourceMdxPathCandidate))
                    {
                        sourceMdxPath = sourceMdxPathCandidate;
                        outputMdxPath = Path.Combine(modelsPath, $"{targetResref}.mdx");
                    }
                    else
                    {
                        System.Console.WriteLine($"Warning: MDX file not found for {Path.GetFileName(filePath)}. MDX files contain geometry data and are typically required.");
                    }
                }
                else if (isMdx)
                {
                    // User selected MDX file
                    sourceMdxPath = filePath;
                    outputMdxPath = Path.Combine(modelsPath, $"{targetResref}.mdx");

                    // Look for corresponding MDL file in the same directory
                    string sourceMdlPathCandidate = Path.ChangeExtension(filePath, ".mdl");
                    if (File.Exists(sourceMdlPathCandidate))
                    {
                        sourceMdlPath = sourceMdlPathCandidate;
                        outputMdlPath = Path.Combine(modelsPath, $"{targetResref}.mdl");
                    }
                    else
                    {
                        System.Console.WriteLine($"Warning: MDL file not found for {Path.GetFileName(filePath)}. MDL files contain model structure and are required.");
                        // We can still copy the MDX, but it won't be usable without an MDL
                    }
                }

                // Validate MDL format if we have an MDL file
                if (!string.IsNullOrEmpty(sourceMdlPath))
                {
                    try
                    {
                        ResourceType detectedFormat = MDLAuto.DetectMdl(sourceMdlPath);
                        if (detectedFormat != ResourceType.MDL && detectedFormat != ResourceType.MDL_ASCII)
                        {
                            System.Console.WriteLine($"Warning: Could not detect valid MDL format for {Path.GetFileName(sourceMdlPath)}. File may be corrupted.");
                            // Continue anyway - the user might know what they're doing
                        }
                        else
                        {
                            System.Console.WriteLine($"Detected MDL format: {detectedFormat}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Warning: Failed to validate MDL file {Path.GetFileName(sourceMdlPath)}: {ex}");
                        // Continue anyway - file might still be valid
                    }
                }

                // Copy MDL file
                if (!string.IsNullOrEmpty(sourceMdlPath) && !string.IsNullOrEmpty(outputMdlPath))
                {
                    try
                    {
                        if (File.Exists(outputMdlPath))
                        {
                            System.Console.WriteLine($"Warning: Model {targetResref}.mdl already exists in override directory. It will be overwritten.");
                        }

                        File.Copy(sourceMdlPath, outputMdlPath, overwrite: true);
                        System.Console.WriteLine($"Copied MDL: {targetResref}.mdl -> {outputMdlPath}");

                        // Store the imported model reference
                        _importedModels[targetResref] = outputMdlPath;

                        // Update model browser immediately after import
                        UpdateModelBrowser();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Failed to copy MDL file to {outputMdlPath}: {ex}");
                        return;
                    }
                }

                // Copy MDX file
                if (!string.IsNullOrEmpty(sourceMdxPath) && !string.IsNullOrEmpty(outputMdxPath))
                {
                    try
                    {
                        if (File.Exists(outputMdxPath))
                        {
                            System.Console.WriteLine($"Warning: Model geometry {targetResref}.mdx already exists in override directory. It will be overwritten.");
                        }

                        File.Copy(sourceMdxPath, outputMdxPath, overwrite: true);
                        System.Console.WriteLine($"Copied MDX: {targetResref}.mdx -> {outputMdxPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error: Failed to copy MDX file to {outputMdxPath}: {ex}");
                        // Don't return - MDL was copied successfully, MDX is supplementary
                    }
                }

                // Optionally add a room entry to the LYT using the imported model
                // This matches PyKotor behavior where importing a model makes it available for use
                // The user can then add it as a room manually or we could prompt them
                if (!string.IsNullOrEmpty(sourceMdlPath) && _lyt != null)
                {
                    // Check if a room with this model already exists
                    bool modelExists = false;
                    foreach (var room in _lyt.Rooms)
                    {
                        if (room.Model == new Andastra.Parsing.Common.ResRef(targetResref))
                        {
                            modelExists = true;
                            break;
                        }
                    }

                    if (!modelExists)
                    {
                        // Add a new room entry with the imported model at origin
                        // User can reposition it later in the editor
                        var newRoom = new LYTRoom(new Andastra.Parsing.Common.ResRef(targetResref), new Vector3(0, 0, 0));
                        _lyt.Rooms.Add(newRoom);
                        System.Console.WriteLine($"Added room entry for imported model: {targetResref} at position (0, 0, 0)");
                        UpdateScene();
                    }
                }

                System.Console.WriteLine($"Successfully imported model: {targetResref}");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error importing model file {filePath}: {ex}");
            }
        }

        // Update model browser with imported models (similar to UpdateTextureBrowser)
        public void UpdateModelBrowser()
        {
            // Ensure imported models list is maintained and valid
            if (_importedModels == null)
            {
                _importedModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // Remove invalid entries (models that no longer exist on disk)
            var validModels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _importedModels)
            {
                if (string.IsNullOrEmpty(kvp.Value) || File.Exists(kvp.Value))
                {
                    validModels[kvp.Key] = kvp.Value;
                }
                else
                {
                    System.Console.WriteLine($"Warning: Imported model file no longer exists: {kvp.Value}, removing from list.");
                }
            }
            _importedModels = validModels;

            // Update model browser widget if available
            if (_modelBrowser != null)
            {
                _modelBrowser.UpdateModels(_importedModels);
            }
            else
            {
                // Initialize model browser if not already initialized
                InitializeModelBrowser();
                if (_modelBrowser != null)
                {
                    _modelBrowser.UpdateModels(_importedModels);
                }
            }

            // Log current state for debugging
            System.Console.WriteLine($"Model browser updated. {_importedModels.Count} model(s) available.");
            foreach (var kvp in _importedModels)
            {
                System.Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
            }
        }

        public List<string> GetImportedModels()
        {
            return new List<string>(_importedModels.Keys);
        }

        public string GetImportedModelPath(string modelName)
        {
            return _importedModels.TryGetValue(modelName, out string path) ? path : null;
        }

        /// <summary>
        /// Gets the model browser widget (for UI integration and testing).
        /// </summary>
        public ModelBrowser ModelBrowser
        {
            get { return _modelBrowser; }
        }

        /// <summary>
        /// Gets the texture browser widget (for UI integration and testing).
        /// </summary>
        public TextureBrowser TextureBrowser
        {
            get { return _textureBrowser; }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:243-245
        // Original: def update_texture_browser(self):
        public void UpdateTextureBrowser()
        {
            // Ensure imported textures list is maintained and valid
            if (_importedTextures == null)
            {
                _importedTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            // Remove invalid entries (textures that no longer exist on disk)
            var validTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _importedTextures)
            {
                if (string.IsNullOrEmpty(kvp.Value) || File.Exists(kvp.Value))
                {
                    validTextures[kvp.Key] = kvp.Value;
                }
                else
                {
                    System.Console.WriteLine($"Warning: Imported texture file no longer exists: {kvp.Value}, removing from list.");
                }
            }
            _importedTextures = validTextures;

            // Update texture browser widget if available
            if (_textureBrowser != null)
            {
                _textureBrowser.UpdateTextures(_importedTextures);
            }
            else
            {
                // Initialize texture browser if not already initialized
                InitializeTextureBrowser();
                if (_textureBrowser != null)
                {
                    _textureBrowser.UpdateTextures(_importedTextures);
                }
            }

            // Log current state for debugging
            System.Console.WriteLine($"Texture browser updated. {_importedTextures.Count} texture(s) available.");
            foreach (var kvp in _importedTextures)
            {
                System.Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
            }
        }

        public List<string> GetImportedTextures()
        {
            return new List<string>(_importedTextures.Keys);
        }

        public string GetImportedTexturePath(string textureName)
        {
            return _importedTextures.TryGetValue(textureName, out string path) ? path : null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:247-264
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                _lyt = LYTAuto.ReadLyt(data);
                LoadLYT(_lyt);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load LYT: {ex}");
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:220-229
        // Original: def update_scene(self):
        private void LoadLYT(LYT lyt)
        {
            _lyt = lyt;
            UpdateScene();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:266-267
        // Original: def build(self) -> tuple[bytes, ResourceType]:
        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = LYTAuto.BytesLyt(_lyt);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _lyt = new LYT();
            UpdateScene();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
