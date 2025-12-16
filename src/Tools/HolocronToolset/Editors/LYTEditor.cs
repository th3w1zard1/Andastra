using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Formats.LYT;
using Andastra.Parsing.Resources;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:29
    // Original: class LYTEditor(Editor):
    public partial class LYTEditor : Editor
    {
        private LYT _lyt;
        private LYTEditorSettings _settings;

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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:127-131
        // Original: def add_room(self):
        public void AddRoom()
        {
            var room = new LYTRoom("default_room", new Vector3(0, 0, 0));
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

            var track = new LYTTrack("default_track", new Vector3(0, 0, 0));

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
            if (start.Connections.Contains(end))
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

                foreach (var nextRoom in currentRoom.Connections)
                {
                    if (visited.Contains(nextRoom))
                    {
                        continue;
                    }

                    visited.Add(nextRoom);
                    var newPath = new List<LYTRoom>(path) { nextRoom };
                    var priority = newPath.Count + (nextRoom.Position - end.Position).Magnitude();
                    queue.Add(Tuple.Create(priority, nextRoom, newPath));
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:181-184
        // Original: def add_obstacle(self):
        public void AddObstacle()
        {
            var obstacle = new LYTObstacle("default_obstacle", new Vector3(0, 0, 0));
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
            // Zoom functionality - will be implemented when graphics view is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:220-229
        // Original: def update_scene(self):
        public void UpdateScene()
        {
            // Scene update - will be implemented when graphics scene is available
            // For now, just ensure LYT data is consistent
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:231-235
        // Original: def import_texture(self):
        public void ImportTexture()
        {
            // TODO: Implement texture import logic
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:237-241
        // Original: def import_model(self):
        public void ImportModel()
        {
            // TODO: Implement model import logic
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/lyt.py:243-245
        // Original: def update_texture_browser(self):
        public void UpdateTextureBrowser()
        {
            // TODO: Update texture browser with imported textures
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
