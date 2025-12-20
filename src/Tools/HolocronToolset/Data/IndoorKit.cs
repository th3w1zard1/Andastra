using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LYT;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Logger;
using Vector3 = System.Numerics.Vector3;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:24
    // Original: class Kit:
    public class Kit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:25-35
        // Original: def __init__(self, name: str):
        public Kit(string name)
        {
            Name = name;
            Components = new List<KitComponent>();
            Doors = new List<KitDoor>();
            Textures = new Dictionary<string, byte[]>();
            Lightmaps = new Dictionary<string, byte[]>();
            Txis = new Dictionary<string, byte[]>();
            Always = new Dictionary<string, byte[]>();
            SidePadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            TopPadding = new Dictionary<int, Dictionary<int, MDLMDXTuple>>();
            Skyboxes = new Dictionary<string, MDLMDXTuple>();
        }

        public string Name { get; set; }
        public List<KitComponent> Components { get; set; }
        public List<KitDoor> Doors { get; set; }
        public Dictionary<string, byte[]> Textures { get; set; }
        public Dictionary<string, byte[]> Lightmaps { get; set; }
        public Dictionary<string, byte[]> Txis { get; set; }
        public Dictionary<string, byte[]> Always { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> SidePadding { get; set; }
        public Dictionary<int, Dictionary<int, MDLMDXTuple>> TopPadding { get; set; }
        public Dictionary<string, MDLMDXTuple> Skyboxes { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:38
    // Original: class KitComponent:
    public class KitComponent
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:39-47
        // Original: def __init__(self, kit: Kit, name: str, image: QImage, bwm: BWM, mdl: bytes, mdx: bytes):
        public KitComponent(Kit kit, string name, object image, BWM bwm, byte[] mdl, byte[] mdx)
        {
            Kit = kit;
            Name = name;
            Image = image;
            Bwm = bwm;
            Mdl = mdl;
            Mdx = mdx;
            Hooks = new List<KitComponentHook>();
        }

        public Kit Kit { get; set; }
        public string Name { get; set; }
        public object Image { get; set; }
        public BWM Bwm { get; set; }
        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
        public List<KitComponentHook> Hooks { get; set; }

        // Matching PyKotor implementation: deepcopy(component) behavior
        // Creates a deep copy of the component so hooks can be edited independently
        // This matches the behavior in indoor_builder.py:338 and indoor_builder.py:1721
        public KitComponent DeepCopy()
        {
            // Deep copy the BWM (walkmesh) - each component needs its own instance
            BWM bwmCopy = DeepCopyBwm(Bwm);

            // Deep copy the byte arrays (MDL and MDX model data)
            byte[] mdlCopy = null;
            if (Mdl != null)
            {
                mdlCopy = new byte[Mdl.Length];
                System.Array.Copy(Mdl, mdlCopy, Mdl.Length);
            }

            byte[] mdxCopy = null;
            if (Mdx != null)
            {
                mdxCopy = new byte[Mdx.Length];
                System.Array.Copy(Mdx, mdxCopy, Mdx.Length);
            }

            // Create new component with copied data
            // Kit reference stays the same (component belongs to the same kit)
            // Image reference stays the same (images are typically immutable or shared)
            // Name is a string (immutable in C#)
            var componentCopy = new KitComponent(Kit, Name, Image, bwmCopy, mdlCopy, mdxCopy);

            // Deep copy all hooks - each hook needs to be a new instance
            foreach (var hook in Hooks)
            {
                if (hook != null)
                {
                    // Create new hook with copied values
                    // Position is a struct (Vector3), so it's copied by value
                    // Door reference stays the same (doors are shared within a kit)
                    var hookCopy = new KitComponentHook(
                        new Vector3(hook.Position.X, hook.Position.Y, hook.Position.Z),
                        hook.Rotation,
                        hook.Edge,
                        hook.Door
                    );
                    componentCopy.Hooks.Add(hookCopy);
                }
            }

            return componentCopy;
        }

        // Helper method to deep copy a BWM (matching _DeepCopyBwm pattern from ModuleKit)
        private BWM DeepCopyBwm(BWM original)
        {
            if (original == null)
            {
                return null;
            }

            var copy = new BWM();
            copy.WalkmeshType = original.WalkmeshType;
            copy.Position = original.Position;
            copy.RelativeHook1 = original.RelativeHook1;
            copy.RelativeHook2 = original.RelativeHook2;
            copy.AbsoluteHook1 = original.AbsoluteHook1;
            copy.AbsoluteHook2 = original.AbsoluteHook2;

            // Deep copy all faces
            foreach (var face in original.Faces)
            {
                var newFace = new BWMFace(face.V1, face.V2, face.V3);
                newFace.Material = face.Material;
                newFace.Trans1 = face.Trans1;
                newFace.Trans2 = face.Trans2;
                newFace.Trans3 = face.Trans3;
                copy.Faces.Add(newFace);
            }

            return copy;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:50
    // Original: class KitComponentHook:
    public class KitComponentHook
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:51-55
        // Original: def __init__(self, position: Vector3, rotation: float, edge: int, door: KitDoor):
        public KitComponentHook(Vector3 position, float rotation, int edge, KitDoor door)
        {
            Position = position;
            Rotation = rotation;
            Edge = edge;
            Door = door;
        }

        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public int Edge { get; set; }
        public KitDoor Door { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:58
    // Original: class KitDoor:
    public class KitDoor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:59-63
        // Original: def __init__(self, utdK1: UTD, utdK2: UTD, width: float, height: float):
        public KitDoor(UTD utdK1, UTD utdK2, float width, float height)
        {
            UtdK1 = utdK1;
            UtdK2 = utdK2;
            Width = width;
            Height = height;
        }

        public UTD UtdK1 { get; set; }
        public UTD UtdK2 { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit.py:66
    // Original: class MDLMDXTuple(NamedTuple):
    public class MDLMDXTuple
    {
        public MDLMDXTuple(byte[] mdl, byte[] mdx)
        {
            Mdl = mdl;
            Mdx = mdx;
        }

        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:33-57
    // Original: class ModuleKit(Kit):
    public class ModuleKit : Kit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:43-57
        // Original: def __init__(self, name: str, module_root: str, installation: HTInstallation):
        public ModuleKit(string name, string moduleRoot, HTInstallation installation) : base(name)
        {
            ModuleRoot = moduleRoot;
            _installation = installation;
            _loaded = false;
            IsModuleKit = true;
            SourceModule = moduleRoot;
        }

        public string ModuleRoot { get; set; }
        public bool IsModuleKit { get; set; }
        public string SourceModule { get; set; }
        private HTInstallation _installation;
        private bool _loaded;
        private Andastra.Parsing.Common.Module _module;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:59-74
        // Original: def ensure_loaded(self) -> bool:
        public bool EnsureLoaded()
        {
            if (_loaded)
            {
                return Components.Count > 0;
            }

            _loaded = true;
            try
            {
                _LoadModuleComponents();
                return Components.Count > 0;
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Failed to load module kit for '{ModuleRoot}': {ex.Message}");
                return false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:76-107
        // Original: def _load_module_components(self):
        private void _LoadModuleComponents()
        {
            // Load the module
            try
            {
                _module = new Andastra.Parsing.Common.Module(ModuleRoot, _installation.Installation, useDotMod: true);
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Module '{ModuleRoot}' failed to load: {ex.Message}");
                return;
            }

            // Get the LYT resource
            var lytResource = _module.Layout();
            if (lytResource == null)
            {
                new RobustLogger().Warning($"Module '{ModuleRoot}' has no LYT resource");
                return;
            }

            var lytData = lytResource.Resource() as LYT;
            if (lytData == null)
            {
                new RobustLogger().Warning($"Failed to load LYT data for module '{ModuleRoot}'");
                return;
            }

            // Create a default door for hooks
            var defaultDoor = _CreateDefaultDoor();
            Doors.Add(defaultDoor);

            // Extract rooms from LYT
            for (int roomIdx = 0; roomIdx < lytData.Rooms.Count; roomIdx++)
            {
                var lytRoom = lytData.Rooms[roomIdx];
                var component = _CreateComponentFromLytRoom(lytRoom, roomIdx, defaultDoor);
                if (component != null)
                {
                    Components.Add(component);
                }
            }

            // Also load any doorhooks from LYT as potential hook points
            _ProcessLytDoorhooks(lytData);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:109-114
        // Original: def _create_default_door(self) -> KitDoor:
        private KitDoor _CreateDefaultDoor()
        {
            var utd = new UTD();
            utd.ResRef.SetData("sw_door");
            utd.Tag = "module_door";
            return new KitDoor(utd, utd, 2.0f, 3.0f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:116-201
        // Original: def _create_component_from_lyt_room(...) -> KitComponent | None:
        private KitComponent _CreateComponentFromLytRoom(LYTRoom lytRoom, int roomIdx, KitDoor defaultDoor)
        {
            string modelName = !string.IsNullOrEmpty(lytRoom.Model) ? lytRoom.Model.ToLowerInvariant() : $"room{roomIdx}";

            // Try to get the walkmesh (WOK) for this room
            BWM bwm = _GetRoomWalkmesh(modelName);
            // Ensure we always have a usable walkmesh with at least one face for
            // collision / snapping logic. Some modules ship with empty or missing
            // WOK data; in that case we fall back to a simple placeholder quad.
            if (bwm == null || bwm.Faces.Count == 0)
            {
                bwm = _CreatePlaceholderBwm(lytRoom.Position);
            }
            else
            {
                // Make a deep copy of the BWM so each component has its own instance
                // This prevents issues if multiple rooms share the same model name
                bwm = _DeepCopyBwm(bwm);
            }

            // CRITICAL FIX: Re-center the BWM around (0, 0)
            // Game WOKs are stored in world coordinates, but the Indoor Map Builder
            // expects BWMs centered at origin because:
            // - The preview image is drawn CENTERED at room.position
            // - The walkmesh is TRANSLATED by room.position from its original coords
            // Without re-centering, the image and walkmesh end up at different locations!
            bwm = _RecenterBwm(bwm);

            // Try to get the model data
            byte[] mdlData = _GetRoomModel(modelName);
            byte[] mdxData = _GetRoomModelExt(modelName);

            if (mdlData == null)
            {
                mdlData = new byte[0];
            }
            if (mdxData == null)
            {
                mdxData = new byte[0];
            }

            // Create a preview image from the walkmesh (now re-centered)
            // Each component gets its own image generated from its own BWM copy
            object image = _CreatePreviewImageFromBwm(bwm);

            // Create display name - include room index to ensure uniqueness
            // This helps distinguish rooms that share the same model name
            string displayName;
            if (!string.IsNullOrEmpty(lytRoom.Model))
            {
                displayName = $"{modelName.ToUpperInvariant()}_{roomIdx}";
            }
            else
            {
                displayName = $"ROOM{roomIdx}";
            }

            var component = new KitComponent(this, displayName, image, bwm, mdlData, mdxData);

            // Extract hooks from BWM edges with transitions (after recentering!)
            // This matches how kit.py extracts hooks in extract_kit()
            var doorhooks = _ExtractDoorhooksFromBwm(bwm, Doors.Count);

            // Create KitComponentHook objects from extracted doorhooks
            // Hook positions are in the recentered coordinate space (centered at 0,0)
            foreach (var doorhook in doorhooks)
            {
                var position = new Vector3(doorhook["x"], doorhook["y"], doorhook["z"]);
                float rotation = (float)doorhook["rotation"];
                int doorIndex = (int)doorhook["door"];
                int edge = (int)doorhook["edge"];

                // Get the door for this hook (use default door if index is invalid)
                KitDoor door;
                if (doorIndex >= 0 && doorIndex < Doors.Count)
                {
                    door = Doors[doorIndex];
                }
                else
                {
                    door = defaultDoor;
                }

                var hook = new KitComponentHook(position, rotation, edge, door);
                component.Hooks.Add(hook);
            }

            return component;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:203-243
        // Original: def _recenter_bwm(self, bwm: BWM) -> BWM:
        private BWM _RecenterBwm(BWM bwm)
        {
            var vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                return bwm;
            }

            // Calculate current center
            float minX = vertices.Min(v => v.X);
            float maxX = vertices.Max(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxY = vertices.Max(v => v.Y);
            float minZ = vertices.Min(v => v.Z);
            float maxZ = vertices.Max(v => v.Z);

            float centerX = (minX + maxX) / 2.0f;
            float centerY = (minY + maxY) / 2.0f;
            float centerZ = (minZ + maxZ) / 2.0f;

            // Translate all vertices to center around origin
            // Use BWM.translate() which handles all vertices in faces
            bwm.Translate(-centerX, -centerY, -centerZ);

            return bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:245-270
        // Original: def _get_room_walkmesh(self, model_name: str) -> BWM | None:
        private BWM _GetRoomWalkmesh(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            // Try to find the WOK resource
            var wokResource = _module.Resource(modelName, ResourceType.WOK);
            if (wokResource == null)
            {
                return null;
            }

            byte[] data = wokResource.Data();
            if (data == null)
            {
                return null;
            }

            try
            {
                var bwm = BWMAuto.ReadBwm(data);
                return bwm;
            }
            catch (Exception ex)
            {
                new RobustLogger().Warning($"Failed to read WOK for '{modelName}': {ex.Message}");
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:272-281
        // Original: def _get_room_model(self, model_name: str) -> bytes | None:
        private byte[] _GetRoomModel(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            var mdlResource = _module.Resource(modelName, ResourceType.MDL);
            if (mdlResource == null)
            {
                return null;
            }

            return mdlResource.Data();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:283-292
        // Original: def _get_room_model_ext(self, model_name: str) -> bytes | None:
        private byte[] _GetRoomModelExt(string modelName)
        {
            if (_module == null)
            {
                return null;
            }

            var mdxResource = _module.Resource(modelName, ResourceType.MDX);
            if (mdxResource == null)
            {
                return null;
            }

            return mdxResource.Data();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:294-313
        // Original: def _create_placeholder_bwm(self, position: Vector3) -> BWM:
        private BWM _CreatePlaceholderBwm(Vector3 position)
        {
            var bwm = new BWM();

            // Create a 10x10 unit square walkmesh at origin
            float size = 5.0f;
            var v1 = new Vector3(-size, -size, 0);
            var v2 = new Vector3(size, -size, 0);
            var v3 = new Vector3(size, size, 0);
            var v4 = new Vector3(-size, size, 0);

            var face1 = new BWMFace(v1, v2, v3);
            face1.Material = SurfaceMaterial.Stone;
            var face2 = new BWMFace(v1, v3, v4);
            face2.Material = SurfaceMaterial.Stone;

            bwm.Faces.Add(face1);
            bwm.Faces.Add(face2);

            return bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:315-405
        // Original: def _create_preview_image_from_bwm(self, bwm: BWM) -> QImage:
        private object _CreatePreviewImageFromBwm(BWM bwm)
        {
            // Collect all vertices to calculate bounding box
            var vertices = bwm.Vertices();
            if (vertices.Count == 0)
            {
                // Empty walkmesh - return blank image matching kit.py minimum size
                // For now, return null as placeholder - actual image rendering will be implemented
                // when Avalonia rendering infrastructure is available
                return null;
            }

            // Calculate bounding box (same as kit.py)
            float minX = vertices.Min(v => v.X);
            float minY = vertices.Min(v => v.Y);
            float maxX = vertices.Max(v => v.X);
            float maxY = vertices.Max(v => v.Y);

            // Add padding (same as kit.py: 5.0 units)
            float padding = 5.0f;
            minX -= padding;
            minY -= padding;
            maxX += padding;
            maxY += padding;

            // Calculate image dimensions at 10 pixels per unit (matching kit.py exactly)
            const int PIXELS_PER_UNIT = 10;
            int width = (int)((maxX - minX) * PIXELS_PER_UNIT);
            int height = (int)((maxY - minY) * PIXELS_PER_UNIT);

            // Ensure minimum size (kit.py uses 256, not 100)
            width = Math.Max(width, 256);
            height = Math.Max(height, 256);

            // For now, return null as placeholder - actual image rendering using Avalonia
            // RenderTargetBitmap will be implemented when rendering infrastructure is available
            // The image will be created with Format_RGB888, filled with black, then walkmesh
            // faces will be drawn with white (walkable) or gray (non-walkable) colors,
            // and finally the image will be mirrored to match Kit loader behavior
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/kit.py:1467-1535
        // Original: def _extract_doorhooks_from_bwm(bwm: BWM, num_doors: int) -> list[dict[str, float | int]]:
        private List<Dictionary<string, object>> _ExtractDoorhooksFromBwm(BWM bwm, int numDoors)
        {
            var doorhooks = new List<Dictionary<string, object>>();

            // Get all perimeter edges (these are the edges with transitions)
            var edges = bwm.Edges();

            // Process edges with valid transitions
            foreach (var edge in edges)
            {
                if (edge.Transition < 0) // Skip edges without transitions
                {
                    continue;
                }

                var face = edge.Face;
                // Get edge vertices based on local edge index (0, 1, or 2)
                // edge.index is the global edge index (face_index * 3 + local_edge_index)
                int faceIndex = edge.Index / 3;
                int localEdgeIndex = edge.Index % 3;

                // Get vertices for this edge
                Vector3 v1, v2;
                if (localEdgeIndex == 0)
                {
                    v1 = face.V1;
                    v2 = face.V2;
                }
                else if (localEdgeIndex == 1)
                {
                    v1 = face.V2;
                    v2 = face.V3;
                }
                else // localEdgeIndex == 2
                {
                    v1 = face.V3;
                    v2 = face.V1;
                }

                // Calculate midpoint of edge
                float midX = (v1.X + v2.X) / 2.0f;
                float midY = (v1.Y + v2.Y) / 2.0f;
                float midZ = (v1.Z + v2.Z) / 2.0f;

                // Calculate rotation (angle of edge in XY plane, in degrees)
                float dx = v2.X - v1.X;
                float dy = v2.Y - v1.Y;
                float rotation = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
                // Normalize to 0-360
                rotation = rotation % 360.0f;
                if (rotation < 0)
                {
                    rotation += 360.0f;
                }

                // Map transition index to door index
                // Transition indices typically map directly to door indices, but clamp to valid range
                int doorIndex = numDoors > 0 ? Math.Min(edge.Transition, numDoors - 1) : 0;

                doorhooks.Add(new Dictionary<string, object>
                {
                    { "x", midX },
                    { "y", midY },
                    { "z", midZ },
                    { "rotation", rotation },
                    { "door", doorIndex },
                    { "edge", edge.Index }
                });
            }

            return doorhooks;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:407-417
        // Original: def _process_lyt_doorhooks(self, lyt_data: LYT):
        private void _ProcessLytDoorhooks(LYT lytData)
        {
            // LYT doorhooks contain information about where doors connect rooms
            // This is complex and would require matching doorhooks to rooms
            // For simplicity, we'll leave hooks empty for module-derived components
            // The hooks are already extracted from BWM edges in _create_component_from_lyt_room
        }

        // Helper method to deep copy a BWM
        private BWM _DeepCopyBwm(BWM original)
        {
            var copy = new BWM();
            copy.WalkmeshType = original.WalkmeshType;
            copy.Position = original.Position;
            copy.RelativeHook1 = original.RelativeHook1;
            copy.RelativeHook2 = original.RelativeHook2;
            copy.AbsoluteHook1 = original.AbsoluteHook1;
            copy.AbsoluteHook2 = original.AbsoluteHook2;

            foreach (var face in original.Faces)
            {
                var newFace = new BWMFace(face.V1, face.V2, face.V3);
                newFace.Material = face.Material;
                newFace.Trans1 = face.Trans1;
                newFace.Trans2 = face.Trans2;
                newFace.Trans3 = face.Trans3;
                copy.Faces.Add(newFace);
            }

            return copy;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:419-503
    // Original: class ModuleKitManager:
    public class ModuleKitManager
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:426-429
        // Original: def __init__(self, installation: HTInstallation):
        public ModuleKitManager(HTInstallation installation)
        {
            _installation = installation;
            _cache = new Dictionary<string, ModuleKit>();
            _moduleNames = null;
        }

        private HTInstallation _installation;
        private Dictionary<string, ModuleKit> _cache;
        private Dictionary<string, string> _moduleNames;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:431-441
        // Original: def get_module_names(self) -> dict[str, str | None]:
        public Dictionary<string, string> GetModuleNames()
        {
            if (_moduleNames == null)
            {
                _moduleNames = _installation.ModuleNames();
            }
            return _moduleNames;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:443-458
        // Original: def get_module_roots(self) -> list[str]:
        public List<string> GetModuleRoots()
        {
            var seenRoots = new HashSet<string>();
            var roots = new List<string>();

            var moduleNames = GetModuleNames();
            foreach (var moduleFilename in moduleNames.Keys)
            {
                string root = Andastra.Parsing.Installation.Installation.GetModuleRoot(moduleFilename);
                if (!seenRoots.Contains(root))
                {
                    seenRoots.Add(root);
                    roots.Add(root);
                }
            }

            roots.Sort();
            return roots;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:460-479
        // Original: def get_module_display_name(self, module_root: str) -> str:
        public string GetModuleDisplayName(string moduleRoot)
        {
            var moduleNames = GetModuleNames();

            // Try to find the display name from various extensions
            string[] extensions = { ".rim", ".mod", "_s.rim" };
            foreach (var ext in extensions)
            {
                string filename = moduleRoot + ext;
                if (moduleNames.ContainsKey(filename))
                {
                    string areaName = moduleNames[filename];
                    if (!string.IsNullOrEmpty(areaName) && areaName != "<Unknown Area>")
                    {
                        return $"{moduleRoot.ToUpperInvariant()} - {areaName}";
                    }
                }
            }

            return moduleRoot.ToUpperInvariant();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:481-498
        // Original: def get_module_kit(self, module_root: str) -> ModuleKit:
        public ModuleKit GetModuleKit(string moduleRoot)
        {
            if (!_cache.ContainsKey(moduleRoot))
            {
                string displayName = GetModuleDisplayName(moduleRoot);
                var kit = new ModuleKit(displayName, moduleRoot, _installation);
                _cache[moduleRoot] = kit;
            }

            return _cache[moduleRoot];
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoorkit/module_converter.py:500-503
        // Original: def clear_cache(self):
        public void ClearCache()
        {
            _cache.Clear();
            _moduleNames = null;
        }
    }
}

