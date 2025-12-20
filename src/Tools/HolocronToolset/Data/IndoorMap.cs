using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text;
using System.Text.Json;
using Andastra.Parsing;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.LYT;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Tools;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.GFF.Generics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Andastra.Utility.Geometry.Quaternion;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:66
    // Original: class IndoorMap:
    public class IndoorMap
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:67-81
        // Original: def __init__(self, rooms: list[IndoorMapRoom] | None = None, module_id: str | None = None, name: LocalizedString | None = None, lighting: Color | None = None, skybox: str | None = None, warp_point: Vector3 | None = None):
        public IndoorMap(
            List<IndoorMapRoom> rooms = null,
            string moduleId = null,
            LocalizedString name = null,
            Andastra.Parsing.Common.Color lighting = null,
            string skybox = null,
            System.Numerics.Vector3? warpPoint = null)
        {
            Rooms = rooms ?? new List<IndoorMapRoom>();
            ModuleId = moduleId ?? "test01";
            Name = name ?? LocalizedString.FromEnglish("New Module");
            Lighting = lighting ?? new Andastra.Parsing.Common.Color(0.5f, 0.5f, 0.5f);
            Skybox = skybox ?? "";
            WarpPoint = warpPoint ?? System.Numerics.Vector3.Zero;
        }

        public List<IndoorMapRoom> Rooms { get; set; }
        public string ModuleId { get; set; }
        public LocalizedString Name { get; set; }
        public Andastra.Parsing.Common.Color Lighting { get; set; }
        public string Skybox { get; set; }
        public System.Numerics.Vector3 WarpPoint { get; set; }

        // Build-time state (matching Python implementation)
        private ERF _mod;
        private LYT _lyt;
        private VIS _vis;
        private ARE _are;
        private IFO _ifo;
        private GIT _git;
        private Dictionary<IndoorMapRoom, string> _roomNames;
        private Dictionary<string, string> _texRenames;
        private int _totalLm;
        private HashSet<KitComponent> _usedRooms;
        private HashSet<Kit> _usedKits;
        private HashSet<byte[]> _scanMdls;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:83-85
        // Original: def rebuild_room_connections(self):
        public void RebuildRoomConnections()
        {
            foreach (var room in Rooms)
            {
                room.RebuildConnections(Rooms);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:87-143
        // Original: def door_insertions(self) -> list[DoorInsertion]:
        public List<DoorInsertion> DoorInsertions()
        {
            var points = new List<Vector3>();
            var insertions = new List<DoorInsertion>();

            foreach (var room in Rooms)
            {
                for (int hookIndex = 0; hookIndex < room.Hooks.Count; hookIndex++)
                {
                    var connection = room.Hooks[hookIndex];
                    var room1 = room;
                    IndoorMapRoom room2 = null;
                    var hook1 = room1.Component.Hooks[hookIndex];
                    KitComponentHook hook2 = null;
                    var door = hook1.Door;
                    var position = room1.HookPosition(hook1);
                    float rotation = hook1.Rotation + room1.Rotation;

                    if (connection != null)
                    {
                        for (int otherHookIndex = 0; otherHookIndex < connection.Hooks.Count; otherHookIndex++)
                        {
                            if (connection.Hooks[otherHookIndex] == room1)
                            {
                                var otherHook = connection.Component.Hooks[otherHookIndex];
                                if (hook1.Door.Width < otherHook.Door.Width)
                                {
                                    door = otherHook.Door;
                                    hook2 = hook1;
                                    hook1 = otherHook;
                                    room2 = room1;
                                    room1 = connection;
                                }
                                else
                                {
                                    hook2 = connection.Component.Hooks[otherHookIndex];
                                    room2 = connection;
                                    rotation = hook2.Rotation + room2.Rotation;
                                }
                            }
                        }
                    }

                    if (!points.Contains(position))
                    {
                        points.Add(position);
                        bool isStatic = connection == null;
                        insertions.Add(new DoorInsertion(door, room1, room2, isStatic, position, rotation, hook1, hook2));
                    }
                }
            }

            return insertions;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:145-148
        // Original: def add_rooms(self):
        private void AddRooms()
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                string modelname = $"{ModuleId}_room{i}";
                _vis.AddRoom(modelname);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:150-170
        // Original: def process_room_components(self):
        private void ProcessRoomComponents()
        {
            foreach (var room in Rooms)
            {
                _usedRooms.Add(room.Component);
            }

            foreach (var kitRoom in _usedRooms)
            {
                _scanMdls.Add(kitRoom.Mdl);
                _usedKits.Add(kitRoom.Kit);

                // Process door padding
                foreach (var doorPaddingDict in kitRoom.Kit.TopPadding.Values.Concat(kitRoom.Kit.SidePadding.Values))
                {
                    foreach (var paddingModel in doorPaddingDict.Values)
                    {
                        _scanMdls.Add(paddingModel.Mdl);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:172-192
        // Original: def handle_textures(self):
        private void HandleTextures()
        {
            foreach (var mdl in _scanMdls)
            {
                foreach (var texture in ModelTools.IterateTextures(mdl))
                {
                    if (!_texRenames.ContainsKey(texture))
                    {
                        string renamed = $"{ModuleId}_tex{_texRenames.Count}";
                        _texRenames[texture] = renamed;

                        foreach (var kit in _usedKits)
                        {
                            string textureLower = texture.ToLowerInvariant();
                            if (kit.Textures.TryGetValue(textureLower, out byte[] textureData))
                            {
                                _mod.SetData(renamed, ResourceType.TGA, textureData);
                                if (kit.Txis.TryGetValue(textureLower, out byte[] txiData))
                                {
                                    _mod.SetData(renamed, ResourceType.TXI, txiData);
                                }
                            }
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:194-365
        // Original: def handle_lightmaps(self, installation: HTInstallation):
        private void HandleLightmaps(HTInstallation installation)
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                var room = Rooms[i];
                string modelname = $"{ModuleId}_room{i}";
                _roomNames[room] = modelname;

                // Add room to layout
                var lytRoom = new LYTRoom
                {
                    Model = new ResRef(modelname),
                    Position = room.Position
                };
                _lyt.Rooms.Add(lytRoom);

                // Add static resources
                AddStaticResources(room);

                // Process model
                var (mdl, mdx) = ProcessModel(room, installation);

                // Process lightmaps and update model with renamed lightmap references
                mdl = ProcessLightmaps(room, mdl, installation);

                // Add model resources
                AddModelResources(modelname, mdl, mdx);

                // Process BWM
                var bwm = ProcessBwm(room);
                AddBwmResource(modelname, bwm);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:236-256
        // Original: def add_static_resources(self, room: IndoorMapRoom):
        private void AddStaticResources(IndoorMapRoom room)
        {
            foreach (var kvp in room.Component.Kit.Always)
            {
                var identifier = ResourceIdentifier.FromPath(kvp.Key);
                if (identifier.ResType == ResourceType.INVALID)
                {
                    Console.WriteLine($"Invalid resource, skipping... {kvp.Key} {identifier.ResType}");
                    continue;
                }
                _mod.SetData(identifier.ResName, identifier.ResType, kvp.Value);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:258-285
        // Original: def process_model(self, room: IndoorMapRoom, installation: HTInstallation) -> tuple[bytes, bytes]:
        private (byte[] mdl, byte[] mdx) ProcessModel(IndoorMapRoom room, HTInstallation installation)
        {
            // TODO: Implement model.flip() - requires model manipulation utilities
            // For now, use the original model data
            byte[] mdl = room.Component.Mdl;
            byte[] mdx = room.Component.Mdx;

            // TODO: Implement model.transform() - requires model manipulation utilities
            // TODO: Implement model.convert_to_k1/k2() - requires model manipulation utilities
            // For now, use the original model data as-is

            return (mdl, mdx);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:287-365
        // Original: def process_lightmaps(self, room: IndoorMapRoom, mdl_data: bytes, installation: HTInstallation):
        private byte[] ProcessLightmaps(IndoorMapRoom room, byte[] mdlData, HTInstallation installation)
        {
            var lmRenames = new Dictionary<string, string>();

            foreach (var lightmap in ModelTools.IterateLightmaps(mdlData))
            {
                string renamed = $"{ModuleId}_lm{_totalLm}";
                _totalLm++;
                string lightmapLower = lightmap.ToLowerInvariant();
                lmRenames[lightmapLower] = renamed;

                // Try to get lightmap from kit first
                byte[] lightmapData = null;
                byte[] txiData = null;

                if (!room.Component.Kit.Lightmaps.TryGetValue(lightmapLower, out lightmapData))
                {
                    room.Component.Kit.Lightmaps.TryGetValue(lightmap, out lightmapData);
                }

                if (!room.Component.Kit.Txis.TryGetValue(lightmapLower, out txiData))
                {
                    room.Component.Kit.Txis.TryGetValue(lightmap, out txiData);
                }

                // If not in kit, try to load from installation
                if (lightmapData == null)
                {
                    var tpc = installation.Texture(lightmap, new[]
                    {
                        SearchLocation.CHITIN,
                        SearchLocation.OVERRIDE,
                        SearchLocation.TEXTURES_GUI,
                        SearchLocation.TEXTURES_TPA
                    });

                    if (tpc == null)
                    {
                        new RobustLogger().Warning($"Lightmap '{lightmap}' not found in kit '{room.Component.Kit.Name}' and not available in installation. Skipping lightmap.");
                        lmRenames.Remove(lightmapLower);
                        _totalLm--;
                        continue;
                    }

                    // Convert TPC to bytes
                    var tpcCopy = tpc.Copy();
                    // Convert TPC format if needed (BGR/DXT1/Greyscale -> RGB, BGRA/DXT3/DXT5 -> RGBA)
                    var format = tpcCopy.Format();
                    if (format == TPCTextureFormat.BGR || format == TPCTextureFormat.DXT1 || format == TPCTextureFormat.Greyscale)
                    {
                        tpcCopy.Convert(TPCTextureFormat.RGB);
                    }
                    else if (format == TPCTextureFormat.BGRA || format == TPCTextureFormat.DXT3 || format == TPCTextureFormat.DXT5)
                    {
                        tpcCopy.Convert(TPCTextureFormat.RGBA);
                    }
                    lightmapData = TPCAuto.BytesTpc(tpcCopy, ResourceType.TGA);
                    if (txiData == null)
                    {
                        txiData = new byte[0];
                    }
                }

                // Set the lightmap and TXI data
                _mod.SetData(renamed, ResourceType.TGA, lightmapData);
                _mod.SetData(renamed, ResourceType.TXI, txiData ?? new byte[0]);
            }

            // Change lightmap names in the model data to match the renamed lightmaps
            // This updates all references to old lightmap names with the new module-specific names
            byte[] modifiedMdlData = ModelTools.ChangeLightmaps(mdlData, lmRenames);
            return modifiedMdlData;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:367-388
        // Original: def add_model_resources(self, modelname: str, mdl_data: bytes, mdx_data: bytes):
        private void AddModelResources(string modelname, byte[] mdlData, byte[] mdxData)
        {
            _mod.SetData(modelname, ResourceType.MDL, mdlData);
            _mod.SetData(modelname, ResourceType.MDX, mdxData);
        }

        /// <summary>
        /// Processes a room's walkmesh by applying transformations and remapping transitions.
        /// This method takes the base walkmesh from a kit component, applies room-specific
        /// transformations (flip, rotation, translation), and updates transition indices
        /// to connect rooms together.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHAT THIS METHOD DOES:
        /// 
        /// This method prepares a walkmesh for use in the final module. It takes the walkmesh
        /// from a kit component (which is a reusable room template) and modifies it based on
        /// how the room is placed in the indoor map.
        /// 
        /// The method performs these steps:
        /// 1. Makes a copy of the room's base walkmesh (so we don't modify the original)
        /// 2. Flips the walkmesh if the room is flipped (mirror image)
        /// 3. Rotates the walkmesh to match the room's rotation
        /// 4. Moves the walkmesh to the room's position in the world
        /// 5. Sets the walkmesh type to AreaModel (WOK) - this is critical for navigation
        /// 6. Updates transition indices to connect this room to neighboring rooms
        /// </para>
        /// 
        /// <para>
        /// BUG FIX: WALKMESH TYPE MUST BE SET TO AREAMODEL
        /// 
        /// PROBLEM:
        /// When a walkmesh is loaded from a kit component, it might have WalkmeshType set to
        /// PlaceableOrDoor (PWK/DWK) instead of AreaModel (WOK). This happens because kit
        /// components can be created from various sources, and the walkmesh type might not
        /// be correctly set.
        /// 
        /// When the walkmesh type is not AreaModel, the BwmToNavigationMeshConverter and
        /// BwmToEclipseNavigationMeshConverter classes skip building the AABB tree. They
        /// check "if (bwm.WalkmeshType == BWMType.AreaModel)" before building the tree.
        /// 
        /// Without an AABB tree, the navigation mesh cannot efficiently find which triangle
        /// contains a given point. The AABB tree is a data structure that organizes triangles
        /// into boxes, making it fast to search for triangles. Without it, the system would
        /// need to check every single triangle, which is too slow for real-time gameplay.
        /// 
        /// SYMPTOMS OF THE BUG:
        /// - Characters cannot walk on indoor map levels/modules
        /// - Pathfinding fails (characters cannot find routes)
        /// - Height calculation fails (characters float or sink into ground)
        /// - Line of sight checks fail
        /// - The walkmesh appears to have correct surface materials, but nothing works
        /// 
        /// ROOT CAUSE:
        /// The ProcessBwm method was not setting WalkmeshType to AreaModel. It was copying
        /// the walkmesh type from the original, which might have been PlaceableOrDoor.
        /// 
        /// SOLUTION:
        /// Always set bwm.WalkmeshType = BWMType.AreaModel after applying transformations.
        /// This ensures that when the walkmesh is converted to a NavigationMesh, the AABB
        /// tree will be built, and all navigation features will work correctly.
        /// 
        /// WHY THIS WORKS:
        /// Indoor map walkmeshes are always area walkmeshes (WOK files), not placeable or
        /// door walkmeshes (PWK/DWK files). Area walkmeshes are used for the ground of
        /// entire areas, while placeable/door walkmeshes are used for individual objects.
        /// By setting the type to AreaModel, we tell the converter that this is an area
        /// walkmesh and it should build the AABB tree for efficient spatial queries.
        /// </para>
        /// 
        /// <para>
        /// TRANSFORMATIONS:
        /// 
        /// Flip: If a room is flipped (mirrored), the walkmesh vertices are reflected
        /// across the X or Y axis. This changes the positions of all vertices.
        /// 
        /// Rotate: The walkmesh is rotated around the Z axis (vertical axis) by the room's
        /// rotation angle. This changes the X and Y coordinates of all vertices.
        /// 
        /// Translate: The walkmesh is moved to the room's position in the world. This adds
        /// the room's position to all vertex coordinates.
        /// 
        /// These transformations are applied in order: flip first, then rotate, then translate.
        /// This order ensures that the walkmesh is correctly positioned relative to the room.
        /// </para>
        /// 
        /// <para>
        /// TRANSITION REMAPPING:
        /// 
        /// Transitions are connections between rooms. Each room has "hooks" (connection points)
        /// that can be linked to other rooms. When rooms are connected, the walkmesh edges
        /// at those connection points need to be updated to point to the correct neighboring
        /// room.
        /// 
        /// The method iterates through all hooks in the room and updates the transition indices
        /// in the walkmesh faces. If a hook is connected to another room, the transition index
        /// is set to that room's index. If a hook is not connected, the transition index is
        /// set to -1 (no transition).
        /// </para>
        /// </remarks>
        /// <param name="room">The room whose walkmesh should be processed</param>
        /// <returns>A processed walkmesh ready for use in the module</returns>
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:390-419
        // Original: def process_bwm(self, room: IndoorMapRoom) -> BWM:
        private BWM ProcessBwm(IndoorMapRoom room)
        {
            var bwm = DeepCopyBwm(room.BaseWalkmesh());
            bwm.Flip(room.FlipX, room.FlipY);
            bwm.Rotate(room.Rotation);
            bwm.Translate(room.Position.X, room.Position.Y, room.Position.Z);

            // CRITICAL: Set walkmesh type to AreaModel (WOK) for indoor map walkmeshes.
            // This ensures the AABB tree is built when converting to NavigationMesh.
            // Without this, BwmToNavigationMeshConverter and BwmToEclipseNavigationMeshConverter
            // will skip AABB tree construction (they check bwm.WalkmeshType == BWMType.AreaModel),
            // causing pathfinding and spatial queries to fail.
            // Original implementation: PyKotor doesn't explicitly set this, but area walkmeshes
            // are always WOK type. This fix ensures consistency.
            bwm.WalkmeshType = BWMType.AreaModel;

            for (int hookIndex = 0; hookIndex < room.Hooks.Count; hookIndex++)
            {
                var connection = room.Hooks[hookIndex];
                int dummyIndex = (int)room.Component.Hooks[hookIndex].Edge;
                int? actualIndex = connection == null ? (int?)null : Rooms.IndexOf(connection);
                RemapTransitions(bwm, dummyIndex, actualIndex);
            }

            return bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:421-447
        // Original: def remap_transitions(self, bwm: BWM, dummy_index: int, actual_index: int | None):
        private void RemapTransitions(BWM bwm, int dummyIndex, int? actualIndex)
        {
            foreach (var face in bwm.Faces)
            {
                if (face.Trans1 == dummyIndex)
                {
                    face.Trans1 = actualIndex ?? -1;
                }
                if (face.Trans2 == dummyIndex)
                {
                    face.Trans2 = actualIndex ?? -1;
                }
                if (face.Trans3 == dummyIndex)
                {
                    face.Trans3 = actualIndex ?? -1;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:449-454
        // Original: def add_bwm_resource(self, modelname: str, bwm: BWM):
        private void AddBwmResource(string modelname, BWM bwm)
        {
            _mod.SetData(modelname, ResourceType.WOK, BWMAuto.BytesBwm(bwm));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:456-576
        // Original: def handle_door_insertions(self, installation: HTInstallation):
        private void HandleDoorInsertions(HTInstallation installation)
        {
            int paddingCount = 0;
            var insertions = DoorInsertions();

            for (int i = 0; i < insertions.Count; i++)
            {
                var insert = insertions[i];
                var door = new GITDoor
                {
                    Position = insert.Position,
                    ResRef = new ResRef($"{ModuleId}_dor{i:00}")
                };
                door.Bearing = (float)(Math.PI / 180.0 * insert.Rotation);
                door.TweakColor = null;
                _git.Doors.Add(door);

                // TODO: Implement UTD deep copy and door insertion logic
                // This requires UTD manipulation utilities
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:578-608
        // Original: def process_skybox(self, kits: list[Kit]):
        private void ProcessSkybox(List<Kit> kits)
        {
            if (string.IsNullOrEmpty(Skybox))
            {
                return;
            }

            foreach (var kit in kits)
            {
                if (!kit.Skyboxes.ContainsKey(Skybox))
                {
                    continue;
                }

                var skyboxData = kit.Skyboxes[Skybox];
                string modelName = $"{ModuleId}_sky";
                // TODO: Implement model.change_textures() for skybox
                byte[] mdlConverted = skyboxData.Mdl; // ModelTools.ChangeTextures(skyboxData.Mdl, _texRenames);
                _mod.SetData(modelName, ResourceType.MDL, mdlConverted);
                _mod.SetData(modelName, ResourceType.MDX, skyboxData.Mdx);

                var lytRoom = new LYTRoom
                {
                    Model = new ResRef(modelName),
                    Position = Vector3.Zero
                };
                _lyt.Rooms.Add(lytRoom);
                _vis.AddRoom(modelName);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:610-631
        // Original: def generate_and_set_minimap(self):
        private void GenerateAndSetMinimap()
        {
            var minimap = GenerateMipmap();
            // TODO: Implement minimap TPC generation from QImage
            // This requires image manipulation utilities
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:633-653
        // Original: def handle_loadscreen(self, installation: HTInstallation):
        private void HandleLoadscreen(HTInstallation installation)
        {
            try
            {
                string loadPath = installation.Tsl ? "./kits/load_k2.tga" : "./kits/load_k1.tga";
                if (File.Exists(loadPath))
                {
                    byte[] loadTga = File.ReadAllBytes(loadPath);
                    _mod.SetData($"load_{ModuleId}", ResourceType.TGA, loadTga);
                }
            }
            catch (Exception ex)
            {
                new RobustLogger().Error($"Load screen file not found for installation '{installation.Name}': {ex.Message}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:655-683
        // Original: def set_area_attributes(self, minimap: MinimapData):
        private void SetAreaAttributes(MinimapData minimap)
        {
            _are.Tag = ModuleId;
            _are.DynamicLight = Lighting;
            _are.Name = Name;
            _are.MapPoint1 = minimap.ImagePointMin;
            _are.MapPoint2 = minimap.ImagePointMax;
            _are.WorldPoint1 = minimap.WorldPointMin;
            _are.WorldPoint2 = minimap.WorldPointMax;
            _are.MapZoom = 1;
            _are.MapResX = 1;
            _are.NorthAxis = ARENorthAxis.NegativeY;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:685-704
        // Original: def set_ifo_attributes(self):
        private void SetIfoAttributes()
        {
            _ifo.Tag = ModuleId;
            _ifo.AreaName = new ResRef(ModuleId);
            _ifo.ResRef = new ResRef(ModuleId);
            _vis.SetAllVisible();
            _ifo.EntryPosition = WarpPoint;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:706-732
        // Original: def finalize_module_data(self, output_path: os.PathLike | str):
        private void FinalizeModuleData(string outputPath)
        {
            _mod.SetData(ModuleId, ResourceType.LYT, LYTAuto.BytesLyt(_lyt));
            _mod.SetData(ModuleId, ResourceType.VIS, VISAuto.BytesVis(_vis));
            _mod.SetData(ModuleId, ResourceType.ARE, AREHelpers.BytesAre(_are));
            _mod.SetData(ModuleId, ResourceType.GIT, GITHelpers.BytesGit(_git));
            _mod.SetData("module", ResourceType.IFO, IFOHelpers.BytesIfo(_ifo));

            ERFAuto.WriteErf(_mod, outputPath, ResourceType.MOD);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:734-784
        // Original: def build(self, installation: HTInstallation, kits: list[Kit], output_path: os.PathLike | str):
        public void Build(HTInstallation installation, List<Kit> kits, string outputPath)
        {
            _mod = new ERF(ERFType.MOD);
            _lyt = new LYT();
            _vis = new VIS();
            _are = new ARE();
            _ifo = new IFO();
            _git = new GIT();
            _roomNames = new Dictionary<IndoorMapRoom, string>();
            _texRenames = new Dictionary<string, string>();
            _totalLm = 0;
            _usedRooms = new HashSet<KitComponent>();
            _usedKits = new HashSet<Kit>();
            _scanMdls = new HashSet<byte[]>(new ByteArrayComparer());

            AddRooms();
            ProcessRoomComponents();
            HandleTextures();
            HandleLightmaps(installation);
            ProcessSkybox(kits);
            GenerateAndSetMinimap();
            HandleLoadscreen(installation);
            HandleDoorInsertions(installation);
            SetAreaAttributes(GenerateMipmap());
            SetIfoAttributes();
            FinalizeModuleData(outputPath);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:786-827
        // Original: def write(self) -> bytes:
        public byte[] Write()
        {
            var data = new Dictionary<string, object>
            {
                ["module_id"] = ModuleId,
                ["name"] = new Dictionary<string, object>()
            };

            var nameDict = (Dictionary<string, object>)data["name"];
            nameDict["stringref"] = Name.StringRef;

            foreach (var (language, gender, text) in Name)
            {
                int stringId = LocalizedString.SubstringId(language, gender);
                nameDict[stringId.ToString()] = text;
            }

            data["lighting"] = new[] { Lighting.R, Lighting.G, Lighting.B };
            data["skybox"] = Skybox;
            data["warp"] = ModuleId;

            var roomsList = new List<Dictionary<string, object>>();
            foreach (var room in Rooms)
            {
                var roomData = new Dictionary<string, object>
                {
                    ["position"] = new[] { room.Position.X, room.Position.Y, room.Position.Z },
                    ["rotation"] = room.Rotation,
                    ["flip_x"] = room.FlipX,
                    ["flip_y"] = room.FlipY,
                    ["kit"] = room.Component.Kit.Name,
                    ["component"] = room.Component.Name
                };

                if (room.WalkmeshOverride != null)
                {
                    byte[] bwmBytes = BWMAuto.BytesBwm(room.WalkmeshOverride);
                    roomData["walkmesh_override"] = Convert.ToBase64String(bwmBytes);
                }

                roomsList.Add(roomData);
            }

            data["rooms"] = roomsList;

            return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:829-931
        // Original: def load(self, raw: bytes, kits: list[Kit]) -> list[MissingRoomInfo]:
        public List<MissingRoomInfo> Load(byte[] raw, List<Kit> kits)
        {
            Reset();
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Encoding.UTF8.GetString(raw));

            try
            {
                return LoadData(data, kits);
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException("Map file is corrupted.", ex);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:860-931
        // Original: def _load_data(self, data: dict[str, Any], kits: list[Kit]) -> list[MissingRoomInfo]:
        private List<MissingRoomInfo> LoadData(Dictionary<string, JsonElement> data, List<Kit> kits)
        {
            var missingRooms = new List<MissingRoomInfo>();

            var nameData = data["name"];
            Name = new LocalizedString(nameData.GetProperty("stringref").GetInt32());

            foreach (var prop in nameData.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out int substringId))
                {
                    var (language, gender) = LocalizedString.SubstringPair(substringId);
                    Name.SetData(language, gender, prop.Value.GetString());
                }
            }

            var lightingArray = data["lighting"].EnumerateArray().ToArray();
            Lighting.R = (float)lightingArray[0].GetDouble();
            Lighting.G = (float)lightingArray[1].GetDouble();
            Lighting.B = (float)lightingArray[2].GetDouble();

            ModuleId = data["warp"].GetString();
            if (data.ContainsKey("skybox"))
            {
                Skybox = data["skybox"].GetString();
            }

            foreach (var roomData in data["rooms"].EnumerateArray())
            {
                string kitName = roomData.GetProperty("kit").GetString();
                var sKit = kits.FirstOrDefault(k => k.Name == kitName);
                if (sKit == null)
                {
                    new RobustLogger().Warning($"Kit '{kitName}' is missing, skipping room.");
                    missingRooms.Add(new MissingRoomInfo(kitName, roomData.GetProperty("component").GetString(), "kit_missing"));
                    continue;
                }

                string componentName = roomData.GetProperty("component").GetString();
                var sComponent = sKit.Components.FirstOrDefault(c => c.Name == componentName);
                if (sComponent == null)
                {
                    new RobustLogger().Warning($"Component '{componentName}' is missing in kit '{sKit.Name}', skipping room.");
                    missingRooms.Add(new MissingRoomInfo(kitName, componentName, "component_missing"));
                    continue;
                }

                var positionArray = roomData.GetProperty("position").EnumerateArray().ToArray();
                var position = new Vector3(
                    (float)positionArray[0].GetDouble(),
                    (float)positionArray[1].GetDouble(),
                    (float)positionArray[2].GetDouble()
                );

                float rotation = roomData.GetProperty("rotation").GetSingle();
                bool flipX = roomData.TryGetProperty("flip_x", out var flipXProp) && flipXProp.GetBoolean();
                bool flipY = roomData.TryGetProperty("flip_y", out var flipYProp) && flipYProp.GetBoolean();

                var room = new IndoorMapRoom(sComponent, position, rotation, flipX, flipY);

                if (roomData.TryGetProperty("walkmesh_override", out var walkmeshOverrideProp))
                {
                    try
                    {
                        byte[] rawBwm = Convert.FromBase64String(walkmeshOverrideProp.GetString());
                        room.WalkmeshOverride = BWMAuto.ReadBwm(rawBwm);
                    }
                    catch (Exception ex)
                    {
                        new RobustLogger().Warning($"Failed to read walkmesh override for room '{room.Component.Name}': {ex.Message}");
                    }
                }

                Rooms.Add(room);
            }

            return missingRooms;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:933-937
        // Original: def reset(self):
        public void Reset()
        {
            Rooms.Clear();
            ModuleId = "test01";
            Name = LocalizedString.FromEnglish("New Module");
            Lighting = new Andastra.Parsing.Common.Color(0.5f, 0.5f, 0.5f);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:939-1022
        // Original: def generate_mipmap(self) -> MinimapData:
        public MinimapData GenerateMipmap()
        {
            // Get bounding box of all walkmeshes
            var walkmeshes = new List<BWM>();
            foreach (var room in Rooms)
            {
                var bwm = IndoorMapRoomHelper.DeepCopyBwm(room.BaseWalkmesh());
                bwm.Flip(room.FlipX, room.FlipY);
                bwm.Rotate(room.Rotation);
                bwm.Translate(room.Position.X, room.Position.Y, room.Position.Z);
                walkmeshes.Add(bwm);
            }

            var bbmin = new Vector3(1000000, 1000000, 1000000);
            var bbmax = new Vector3(-1000000, -1000000, -1000000);

            foreach (var bwm in walkmeshes)
            {
                foreach (var vertex in bwm.Vertices())
                {
                    NormalizeBwmVertices(bbmin, vertex, bbmax);
                }
            }

            bbmin.X -= 5;
            bbmin.Y -= 5;
            bbmax.X += 5;
            bbmax.Y += 5;

            float width = (bbmax.X - bbmin.X) * 10;
            float height = (bbmax.Y - bbmin.Y) * 10;

            // Create initial pixmap (matching Python: QPixmap(int(width), int(height)))
            int pixmapWidth = (int)width;
            int pixmapHeight = (int)height;
            var pixmap = new RenderTargetBitmap(new PixelSize(pixmapWidth, pixmapHeight));

            // Fill with black (matching Python: pixmap.fill(QColor(0)))
            using (var context = pixmap.CreateDrawingContext())
            {
                context.FillRectangle(Brushes.Black, new Rect(0, 0, pixmapWidth, pixmapHeight));

                // Draw each room's image with transformations (matching Python lines 984-997)
                foreach (var room in Rooms)
                {
                    // Get room component image
                    var roomImage = room.Component?.Image as Bitmap;
                    if (roomImage == null)
                    {
                        // Skip rooms without valid images (placeholder images are null)
                        continue;
                    }

                    // Save transformation state (matching Python: painter.save())
                    using (context.PushTransform())
                    {
                        // Translate to room position (matching Python lines 988-990)
                        // Python: painter.translate(room.position.x * 10 - bbmin.x * 10, room.position.y * 10 - bbmin.y * 10)
                        double translateX = room.Position.X * 10 - bbmin.X * 10;
                        double translateY = room.Position.Y * 10 - bbmin.Y * 10;
                        context.Transform = Matrix.CreateTranslation(translateX, translateY);

                        // Rotate (matching Python line 992: painter.rotate(room.rotation))
                        // Note: Python QPainter.rotate uses degrees, but we need radians
                        double rotationDegrees = room.Rotation * (180.0 / Math.PI);
                        context.Transform = context.Transform * Matrix.CreateRotation(rotationDegrees * (Math.PI / 180.0));

                        // Scale for flipping (matching Python line 993: painter.scale(-1 if room.flip_x else 1, -1 if room.flip_y else 1))
                        double scaleX = room.FlipX ? -1.0 : 1.0;
                        double scaleY = room.FlipY ? -1.0 : 1.0;
                        context.Transform = context.Transform * Matrix.CreateScale(scaleX, scaleY);

                        // Translate to center image (matching Python line 994: painter.translate(-image.width() / 2, -image.height() / 2))
                        double imageWidth = roomImage.PixelSize.Width;
                        double imageHeight = roomImage.PixelSize.Height;
                        context.Transform = context.Transform * Matrix.CreateTranslation(-imageWidth / 2, -imageHeight / 2);

                        // Draw the image (matching Python line 996: painter.drawImage(0, 0, image))
                        context.DrawImage(roomImage, new Rect(0, 0, imageWidth, imageHeight));
                    }
                }
            }

            // Scale pixmap to 435x256 keeping aspect ratio (matching Python line 1002)
            // Python: pixmap = pixmap.scaled(435, 256, QtCore.Qt.KeepAspectRatio)
            double scaleFactor = Math.Min(435.0 / pixmapWidth, 256.0 / pixmapHeight);
            int scaledWidth = (int)(pixmapWidth * scaleFactor);
            int scaledHeight = (int)(pixmapHeight * scaleFactor);
            var scaledPixmap = new RenderTargetBitmap(new PixelSize(scaledWidth, scaledHeight));
            using (var context = scaledPixmap.CreateDrawingContext())
            {
                context.DrawImage(pixmap, new Rect(0, 0, scaledWidth, scaledHeight));
            }

            // Create final 512x256 pixmap and center the scaled image (matching Python lines 1004-1007)
            var finalPixmap = new RenderTargetBitmap(new PixelSize(512, 256));
            using (var context = finalPixmap.CreateDrawingContext())
            {
                // Fill with black (matching Python: pixmap2.fill(QColor(0)))
                context.FillRectangle(Brushes.Black, new Rect(0, 0, 512, 256));

                // Center the scaled image vertically (matching Python line 1007)
                // Python: painter2.drawPixmap(0, int(128 - pixmap.height() / 2), pixmap)
                double centerY = 128.0 - scaledHeight / 2.0;
                context.DrawImage(scaledPixmap, new Rect(0, centerY, scaledWidth, scaledHeight));
            }

            // Transform to flip Y axis (matching Python line 1009: pixmap2.transformed(QTransform().scale(1, -1)).toImage())
            // In Avalonia, we need to manually flip by drawing with Y-axis inversion
            var flippedImage = new RenderTargetBitmap(new PixelSize(512, 256));
            using (var context = flippedImage.CreateDrawingContext())
            {
                // Draw the final pixmap flipped vertically
                using (context.PushTransform())
                {
                    // Scale Y by -1 and translate to flip
                    context.Transform = Matrix.CreateScale(1, -1) * Matrix.CreateTranslation(0, 256);
                    context.DrawImage(finalPixmap, new Rect(0, 0, 512, 256));
                }
            }

            // Convert to RGB888 format (matching Python line 1010: image.convertTo(QImage.Format.Format_RGB888))
            // In Avalonia, we create a WriteableBitmap with RGB format
            // Note: Avalonia's WriteableBitmap uses RGBA by default, but we can extract RGB data
            var rgbImage = new WriteableBitmap(new PixelSize(512, 256), new Vector(96, 96), PixelFormat.Rgba8888);
            using (var lockedBitmap = rgbImage.Lock())
            {
                // Copy pixel data from flipped image
                using (var memoryStream = new MemoryStream())
                {
                    flippedImage.Save(memoryStream);
                    memoryStream.Position = 0;

                    // Load as PNG and extract RGB data
                    var loadedBitmap = new Bitmap(memoryStream);
                    using (var sourceLocked = loadedBitmap.Lock())
                    {
                        // Copy RGBA data (Avalonia uses RGBA, but we'll use it as RGB888 equivalent)
                        unsafe
                        {
                            byte* sourcePtr = (byte*)sourceLocked.Address;
                            byte* destPtr = (byte*)lockedBitmap.Address;
                            int pixelCount = 512 * 256;
                            for (int i = 0; i < pixelCount; i++)
                            {
                                // Copy RGB (skip alpha or set to 255 for RGB888)
                                destPtr[i * 4] = sourcePtr[i * 4];         // R
                                destPtr[i * 4 + 1] = sourcePtr[i * 4 + 1]; // G
                                destPtr[i * 4 + 2] = sourcePtr[i * 4 + 2]; // B
                                destPtr[i * 4 + 3] = 255;                   // A (opaque)
                            }
                        }
                    }
                }
            }

            // Calculate image points (matching Python lines 1011-1012)
            // Python: image_point_min: Vector2 = Vector2(0 / 435, (128 - pixmap.height() / 2) / 256)
            // Python: image_point_max: Vector2 = Vector2((image_point_min.x + pixmap.width()) / 435, (image_point_min.y + pixmap.height()) / 256)
            var imagePointMin = new Vector2(0.0f / 435.0f, (float)((128.0 - scaledHeight / 2.0) / 256.0));
            var imagePointMax = new Vector2((float)((imagePointMin.X + scaledWidth) / 435.0), (float)((imagePointMin.Y + scaledHeight) / 256.0));

            // Calculate world points (matching Python lines 1013-1014)
            var worldPointMin = new Vector2(bbmax.X, bbmin.Y);
            var worldPointMax = new Vector2(bbmin.X, bbmax.Y);

            return new MinimapData(rgbImage, imagePointMin, imagePointMax, worldPointMin, worldPointMax);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1024-1042
        // Original: def serialize(self) -> dict[str, Any]:
        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["module_id"] = ModuleId,
                ["name"] = Name.ToString(),
                ["lighting"] = new Dictionary<string, float>
                {
                    ["r"] = Lighting.R,
                    ["g"] = Lighting.G,
                    ["b"] = Lighting.B
                },
                ["skybox"] = Skybox,
                ["warp_point"] = new Dictionary<string, float>
                {
                    ["x"] = WarpPoint.X,
                    ["y"] = WarpPoint.Y,
                    ["z"] = WarpPoint.Z
                },
                ["rooms"] = Rooms.Select(r => r.Serialize()).ToList()
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1044-1056
        // Original: def _normalize_bwm_vertices(self, bbmin: Vector3, vertex: Vector3, bbmax: Vector3):
        private void NormalizeBwmVertices(Vector3 bbmin, Vector3 vertex, Vector3 bbmax)
        {
            bbmin.X = Math.Min(bbmin.X, vertex.X);
            bbmin.Y = Math.Min(bbmin.Y, vertex.Y);
            bbmin.Z = Math.Min(bbmin.Z, vertex.Z);
            bbmax.X = Math.Max(bbmax.X, vertex.X);
            bbmax.Y = Math.Max(bbmax.Y, vertex.Y);
            bbmax.Z = Math.Max(bbmax.Z, vertex.Z);
        }

        // Helper method to deep copy a BWM
        private BWM DeepCopyBwm(BWM original)
        {
            if (original == null)
            {
                return null;
            }

            var copy = new BWM
            {
                WalkmeshType = original.WalkmeshType,
                Position = original.Position,
                RelativeHook1 = original.RelativeHook1,
                RelativeHook2 = original.RelativeHook2,
                AbsoluteHook1 = original.AbsoluteHook1,
                AbsoluteHook2 = original.AbsoluteHook2
            };

            foreach (var face in original.Faces)
            {
                var newFace = new BWMFace(face.V1, face.V2, face.V3)
                {
                    Material = face.Material,
                    Trans1 = face.Trans1,
                    Trans2 = face.Trans2,
                    Trans3 = face.Trans3
                };
                copy.Faces.Add(newFace);
            }

            return copy;
        }

        // Helper class for comparing byte arrays in HashSet
        private class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[] x, byte[] y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                if (x.Length != y.Length) return false;
                return x.SequenceEqual(y);
            }

            public int GetHashCode(byte[] obj)
            {
                if (obj == null) return 0;
                int hash = 17;
                foreach (byte b in obj)
                {
                    hash = hash * 31 + b;
                }
                return hash;
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:41-49
    // Original: class DoorInsertion(NamedTuple):
    public class DoorInsertion
    {
        public KitDoor Door { get; set; }
        public IndoorMapRoom Room { get; set; }
        public IndoorMapRoom Room2 { get; set; }
        public bool Static { get; set; }
        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public KitComponentHook Hook1 { get; set; }
        public KitComponentHook Hook2 { get; set; }

        public DoorInsertion(KitDoor door, IndoorMapRoom room, IndoorMapRoom room2, bool isStatic, Vector3 position, float rotation, KitComponentHook hook1, KitComponentHook hook2)
        {
            Door = door;
            Room = room;
            Room2 = room2;
            Static = isStatic;
            Position = position;
            Rotation = rotation;
            Hook1 = hook1;
            Hook2 = hook2;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:52-57
    // Original: class MinimapData(NamedTuple):
    public class MinimapData
    {
        public object Image { get; set; }
        public Vector2 ImagePointMin { get; set; }
        public Vector2 ImagePointMax { get; set; }
        public Vector2 WorldPointMin { get; set; }
        public Vector2 WorldPointMax { get; set; }

        public MinimapData(object image, Vector2 imagePointMin, Vector2 imagePointMax, Vector2 worldPointMin, Vector2 worldPointMax)
        {
            Image = image;
            ImagePointMin = imagePointMin;
            ImagePointMax = imagePointMax;
            WorldPointMin = worldPointMin;
            WorldPointMax = worldPointMax;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:60-63
    // Original: class MissingRoomInfo(NamedTuple):
    public class MissingRoomInfo
    {
        public string KitName { get; set; }
        public string ComponentName { get; set; }
        public string Reason { get; set; }

        public MissingRoomInfo(string kitName, string componentName, string reason)
        {
            KitName = kitName;
            ComponentName = componentName;
            Reason = reason;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1058
    // Original: class IndoorMapRoom:
    public class IndoorMapRoom
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1059-1074
        // Original: def __init__(self, component: KitComponent, position: Vector3, rotation: float, *, flip_x: bool, flip_y: bool):
        public IndoorMapRoom(
            KitComponent component,
            System.Numerics.Vector3 position,
            float rotation,
            bool flipX = false,
            bool flipY = false)
        {
            Component = component;
            Position = position;
            Rotation = rotation;
            FlipX = flipX;
            FlipY = flipY;
            Hooks = new List<IndoorMapRoom>();
            if (component != null && component.Hooks != null)
            {
                for (int i = 0; i < component.Hooks.Count; i++)
                {
                    Hooks.Add(null);
                }
            }
            WalkmeshOverride = null;
        }

        public KitComponent Component { get; set; }
        public System.Numerics.Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public List<IndoorMapRoom> Hooks { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public BWM WalkmeshOverride { get; set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1076-1115
        // Original: def hook_position(self, hook: KitComponentHook, *, world_offset: bool = True) -> Vector3:
        public Vector3 HookPosition(KitComponentHook hook, bool worldOffset = true)
        {
            var pos = new Vector3(hook.Position.X, hook.Position.Y, hook.Position.Z);

            pos.X = FlipX ? -pos.X : pos.X;
            pos.Y = FlipY ? -pos.Y : pos.Y;
            var temp = new Vector3(pos.X, pos.Y, pos.Z);

            float cos = (float)Math.Cos(Math.PI / 180.0 * Rotation);
            float sin = (float)Math.Sin(Math.PI / 180.0 * Rotation);
            pos.X = temp.X * cos - temp.Y * sin;
            pos.Y = temp.X * sin + temp.Y * cos;

            if (worldOffset)
            {
                pos = pos + Position;
            }

            return pos;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1117-1151
        // Original: def rebuild_connections(self, rooms: list[IndoorMapRoom]):
        public void RebuildConnections(List<IndoorMapRoom> rooms)
        {
            Hooks = new List<IndoorMapRoom>();
            if (Component?.Hooks != null)
            {
                for (int i = 0; i < Component.Hooks.Count; i++)
                {
                    Hooks.Add(null);
                }
            }

            foreach (var hook in Component?.Hooks ?? new List<KitComponentHook>())
            {
                int hookIndex = Component.Hooks.IndexOf(hook);
                var hookPos = HookPosition(hook);

                foreach (var otherRoom in rooms.Where(r => r != this))
                {
                    foreach (var otherHook in otherRoom.Component?.Hooks ?? new List<KitComponentHook>())
                    {
                        var otherHookPos = otherRoom.HookPosition(otherHook);
                        if (Vector3.Distance(hookPos, otherHookPos) < 0.001f)
                        {
                            Hooks[hookIndex] = otherRoom;
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1153-1174
        // Original: def walkmesh(self) -> BWM:
        public BWM Walkmesh()
        {
            var bwm = IndoorMapRoomHelper.DeepCopyBwm(BaseWalkmesh());
            bwm.Flip(FlipX, FlipY);
            bwm.Rotate(Rotation);
            bwm.Translate(Position.X, Position.Y, Position.Z);
            return bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1176-1178
        // Original: def base_walkmesh(self) -> BWM:
        public BWM BaseWalkmesh()
        {
            return WalkmeshOverride ?? Component.Bwm;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1180-1184
        // Original: def ensure_walkmesh_override(self) -> BWM:
        public BWM EnsureWalkmeshOverride()
        {
            if (WalkmeshOverride == null)
            {
                WalkmeshOverride = IndoorMapRoomHelper.DeepCopyBwm(Component.Bwm);
            }
            return WalkmeshOverride;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1186-1188
        // Original: def clear_walkmesh_override(self):
        public void ClearWalkmeshOverride()
        {
            WalkmeshOverride = null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1190-1205
        // Original: def serialize(self) -> dict[str, Any]:
        public Dictionary<string, object> Serialize()
        {
            return new Dictionary<string, object>
            {
                ["component_id"] = Component.GetHashCode(), // Reference to component
                ["component_name"] = Component?.Name ?? "",
                ["position"] = new Dictionary<string, float>
                {
                    ["x"] = Position.X,
                    ["y"] = Position.Y,
                    ["z"] = Position.Z
                },
                ["rotation"] = Rotation,
                ["flip_x"] = FlipX,
                ["flip_y"] = FlipY,
                ["runtime_id"] = GetHashCode()
            };
        }
    }
}
