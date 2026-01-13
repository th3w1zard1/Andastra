using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using System.Text;
using System.Text.Json;
using BioWare.NET;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.LYT;
using BioWare.NET.Resource.Formats.VIS;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.ARE;
using BioWare.NET.Resource.Formats.BWM;
using BioWare.NET.Resource.Formats.TPC;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Tools;
using SystemTextEncoding = System.Text.Encoding;
using BioWare.NET.Common.Logger;
using BioWare.NET.Extract;
using BioWare.NET.Installation;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Vector4 = System.Numerics.Vector4;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Quaternion = System.Numerics.Quaternion;

namespace HolocronToolset.Data
{
    // Helper class for BWM deep copy operations (must be defined before IndoorMapRoom uses it)
    internal static class IndoorMapRoomHelper
    {
        public static BWM DeepCopyBwm(BWM original)
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
    }

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
            ParsingColor lighting = null,
            string skybox = null,
            Vector3? warpPoint = null,
            bool? targetGameType = null)
        {
            Rooms = rooms ?? new List<IndoorMapRoom>();
            ModuleId = moduleId ?? "test01";
            Name = name ?? LocalizedString.FromEnglish("New Module");
            Lighting = lighting ?? new ParsingColor(0.5f, 0.5f, 0.5f);
            Skybox = skybox ?? "";
            WarpPoint = warpPoint ?? System.Numerics.Vector3.Zero;
            // targetGameType: null = use installation.Tsl, true = TSL/K2, false = K1
            TargetGameType = targetGameType;
        }

        public List<IndoorMapRoom> Rooms { get; set; }
        public string ModuleId { get; set; }
        public LocalizedString Name { get; set; }
        public ParsingColor Lighting { get; set; }
        public string Skybox { get; set; }
        public Vector3 WarpPoint { get; set; }
        // TargetGameType: null = use installation.Tsl, true = TSL/K2, false = K1
        public bool? TargetGameType { get; set; }

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

        /// <summary>
        /// Rebuilds connections between rooms based on hook positions.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method rebuilds the connections between rooms in the indoor map. Connections are
        /// established when two rooms have hooks (door connection points) that are very close to
        /// each other (within 0.001 units). When rooms are connected, they can share doors and
        /// the walkmesh transitions are updated to allow pathfinding between them.
        ///
        /// HOW IT WORKS:
        ///
        /// For each room in the map, this method calls RebuildConnections() on that room, passing
        /// the list of all rooms. The room's RebuildConnections() method then checks if any of
        /// its hooks are close to hooks in other rooms, and establishes connections accordingly.
        ///
        /// WHEN IT'S CALLED:
        ///
        /// This method is called when:
        /// - Rooms are added, removed, or moved in the map
        /// - Room positions, rotations, or flips are changed
        /// - The map is loaded from a file
        ///
        /// WHY IT'S NEEDED:
        ///
        /// When rooms are moved or transformed, their hook positions change. The connections
        /// between rooms need to be recalculated to reflect these new positions. Without this,
        /// doors might not be placed correctly, and walkmesh transitions might not connect properly.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:83-85
        /// Original: def rebuild_room_connections(self):
        /// </remarks>
        public void RebuildRoomConnections()
        {
            foreach (var room in Rooms)
            {
                room.RebuildConnections(Rooms);
            }
        }

        /// <summary>
        /// Generates a list of door insertions for all connected room hooks.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method creates a list of DoorInsertion objects that represent where doors should
        /// be placed in the final module. Each door insertion corresponds to a hook (connection point)
        /// on a room component. If two rooms are connected at their hooks, a door is placed between them.
        /// If a hook is not connected to another room, a static door (one that doesn't open) is placed.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Iterate Through All Rooms and Hooks
        /// - For each room in the map, look at all its hooks
        /// - Each hook is a potential door placement location
        ///
        /// STEP 2: Determine Door Type and Position
        /// - If the hook is connected to another room (room.Hooks[hookIndex] != null):
        ///   * This is a connecting door between two rooms
        ///   * Position is calculated from the hook's world position
        ///   * Rotation is the hook's rotation plus the room's rotation
        ///   * The door template is chosen from the hook's Door property
        ///   * If the connected room has a wider door, use that instead (wider doors take precedence)
        /// - If the hook is not connected (room.Hooks[hookIndex] == null):
        ///   * This is a static door (doesn't open, just blocks passage)
        ///   * Position and rotation are still calculated from the hook
        ///
        /// STEP 3: Avoid Duplicate Insertions
        /// - Keep track of positions where doors have already been placed
        /// - If a door position is already in the list, skip it (prevents duplicate doors at same location)
        ///
        /// STEP 4: Create DoorInsertion Objects
        /// - For each unique door position, create a DoorInsertion object
        /// - Store the door template, rooms, position, rotation, and hook references
        ///
        /// DOOR WIDTH SELECTION:
        ///
        /// When two rooms are connected, the door width is determined by comparing the door widths
        /// of both hooks. The wider door is used because:
        /// - A wider door can accommodate both room sizes
        /// - Using the narrower door might cause visual clipping or collision issues
        /// - The door opening animation needs to match the wider opening
        ///
        /// HOOK POSITION CALCULATION:
        ///
        /// The hook position is calculated using room.HookPosition(hook), which:
        /// 1. Takes the hook's local position (relative to component)
        /// 2. Applies room flip transformations (if room is flipped)
        /// 3. Applies room rotation transformation
        /// 4. Adds room position offset (world coordinates)
        ///
        /// This gives the final world position where the door should be placed.
        ///
        /// ROTATION CALCULATION:
        ///
        /// The door rotation is the sum of:
        /// - Hook rotation (angle of the hook edge in the component's local space)
        /// - Room rotation (how the room is rotated in the map)
        ///
        /// This ensures the door is oriented correctly relative to both the component and the room.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:87-143
        /// Original: def door_insertions(self) -> list[DoorInsertion]:
        /// </remarks>
        /// <returns>A list of DoorInsertion objects representing all door placements in the module</returns>
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

        /// <summary>
        /// Adds all rooms to the visibility system.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method adds all rooms in the indoor map to the VIS (visibility) system. The VIS
        /// system determines which rooms are visible from other rooms, which is used for culling
        /// (not rendering rooms that can't be seen) and optimization.
        ///
        /// HOW IT WORKS:
        ///
        /// For each room in the map, it creates a unique model name in the format:
        /// "{ModuleId}_room{index}" (e.g., "test01_room0", "test01_room1", etc.)
        ///
        /// This model name is then added to the VIS system. The VIS system will later determine
        /// which rooms can see each other based on the layout and door connections.
        ///
        /// WHY IT'S NEEDED:
        ///
        /// The VIS system needs to know about all rooms before it can calculate visibility between
        /// them. This method is called early in the build process to register all rooms.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:145-148
        /// Original: def add_rooms(self):
        /// </remarks>
        private void AddRooms()
        {
            for (int i = 0; i < Rooms.Count; i++)
            {
                string modelname = $"{ModuleId}_room{i}";
                _vis.AddRoom(modelname);
            }
        }

        /// <summary>
        /// Processes all room components to identify used kits and models for resource scanning.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method scans all rooms in the map to identify which kit components are being used,
        /// which kits those components belong to, and which models (MDL files) need to be processed.
        /// This information is used later to:
        /// - Extract textures from models
        /// - Rename resources to avoid conflicts
        /// - Include only necessary resources in the final module
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Collect Used Components
        /// - Iterate through all rooms in the map
        /// - Add each room's component to the _usedRooms set
        /// - This ensures each unique component is only processed once (even if used multiple times)
        ///
        /// STEP 2: Scan Models and Kits
        /// - For each used component:
        ///   * Add its MDL model to _scanMdls (for texture extraction)
        ///   * Add its kit to _usedKits (for resource lookup)
        ///
        /// STEP 3: Process Door Padding Models
        /// - Door padding is used when doors of different sizes are connected
        /// - Padding models fill gaps between mismatched door openings
        /// - For each door padding model in the kit (top and side padding):
        ///   * Add its MDL to _scanMdls (so textures can be extracted)
        ///
        /// WHY IT'S NEEDED:
        ///
        /// The build process needs to know which resources are actually used so it can:
        /// - Extract and rename textures from models
        /// - Include only necessary resources (not unused kits/components)
        /// - Process door padding models that might be needed
        ///
        /// RESOURCE SCANNING:
        ///
        /// The _scanMdls set is used later in HandleTextures() to:
        /// - Extract texture references from MDL files
        /// - Rename textures to module-specific names (e.g., "test01_tex0")
        /// - Look up texture data from kits
        ///
        /// The _usedKits set is used to:
        /// - Look up textures, lightmaps, and other resources from kit data
        /// - Ensure resources are available when needed
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:150-170
        /// Original: def process_room_components(self):
        /// </remarks>
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

        /// <summary>
        /// Handles texture extraction, renaming, and resource addition for all models.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method processes all models that are used in the module to extract texture references,
        /// rename them to module-specific names, and add the texture data to the module. This ensures
        /// that all textures are available in the final module with unique names that don't conflict
        /// with other modules or game resources.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Iterate Through All Models
        /// - For each MDL model in _scanMdls (collected in ProcessRoomComponents):
        ///   * Extract all texture references from the model
        ///   * ModelTools.IterateTextures() parses the MDL file and returns texture names
        ///
        /// STEP 2: Rename Textures
        /// - For each texture found:
        ///   * Check if it's already been renamed (in _texRenames dictionary)
        ///   * If not, create a new module-specific name: "{ModuleId}_tex{count}"
        ///   * Example: "texture01" becomes "test01_tex0", "texture02" becomes "test01_tex1"
        ///   * Store the mapping in _texRenames for later use
        ///
        /// STEP 3: Look Up Texture Data
        /// - For each renamed texture, search through all used kits:
        ///   * Try to find the texture data in kit.Textures dictionary
        ///   * Look up by lowercase texture name (case-insensitive matching)
        ///   * If found, add the texture data to the module with the new name
        ///
        /// STEP 4: Add TXI Data (Optional)
        /// - TXI files contain texture metadata (tiling, filtering, etc.)
        /// - If a TXI file exists for the texture in kit.Txis, add it to the module
        /// - If no TXI exists, the texture will use default settings
        ///
        /// WHY RENAMING IS NEEDED:
        ///
        /// Texture names must be unique within a module to avoid conflicts. If two different
        /// textures from different kits have the same name, they would overwrite each other.
        /// By renaming them to module-specific names, we ensure uniqueness.
        ///
        /// TEXTURE LOOKUP:
        ///
        /// Textures are stored in kits with their original names. When a model references a texture,
        /// we need to:
        /// 1. Find the texture data in the kit
        /// 2. Rename it to a module-specific name
        /// 3. Add it to the module
        /// 4. Update the model to reference the new name (done in ProcessModel/ProcessLightmaps)
        ///
        /// CASE-INSENSITIVE MATCHING:
        ///
        /// Texture names are matched case-insensitively because:
        /// - Game resources are often stored with inconsistent casing
        /// - Models might reference textures with different casing than stored in kits
        /// - This ensures textures are found even if casing differs
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:172-192
        /// Original: def handle_textures(self):
        /// </remarks>
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
                var lytRoom = new LYTRoom(new ResRef(modelname), room.Position);
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
        /// <summary>
        /// Processes a model for a room by applying flip, rotation, and format conversion.
        ///
        /// Processing Logic:
        /// - Flip the model based on room flip_x and flip_y properties
        /// - Rotate the model based on room rotation property
        /// - Convert the model to target system format based on installation tsl property
        /// - Return processed model and material index strings.
        ///
        /// Matching PyKotor: Tools/HolocronToolset/src/toolset/data/indoormap.py:258-285
        /// </summary>
        /// <param name="room">The room containing the model component to process.</param>
        /// <param name="installation">The installation to determine target format (K1 vs K2).</param>
        /// <returns>A tuple containing the processed MDL and MDX data.</returns>
        private (byte[] mdl, byte[] mdx) ProcessModel(IndoorMapRoom room, HTInstallation installation)
        {
            if (room == null)
            {
                throw new ArgumentNullException(nameof(room));
            }
            if (room.Component == null)
            {
                throw new ArgumentException("Room component cannot be null", nameof(room));
            }
            if (installation == null)
            {
                throw new ArgumentNullException(nameof(installation));
            }
            if (room.Component.Mdl == null || room.Component.Mdl.Length == 0)
            {
                throw new ArgumentException("Room component MDL data cannot be null or empty", nameof(room));
            }
            if (room.Component.Mdx == null || room.Component.Mdx.Length == 0)
            {
                throw new ArgumentException("Room component MDX data cannot be null or empty", nameof(room));
            }

            // Apply model flip transformation
            // Matching Python: mdl, mdx = model.flip(room.component.mdl, room.component.mdx, flip_x=room.flip_x, flip_y=room.flip_y)
            var flipped = ModelTools.Flip(room.Component.Mdl, room.Component.Mdx, room.FlipX, room.FlipY);
            if (flipped == null)
            {
                throw new InvalidOperationException("Model flip operation returned null");
            }
            byte[] mdl = flipped.Mdl;
            byte[] mdx = flipped.Mdx;

            if (mdl == null || mdl.Length == 0)
            {
                throw new InvalidOperationException("Flipped MDL data is null or empty");
            }
            if (mdx == null || mdx.Length == 0)
            {
                throw new InvalidOperationException("Flipped MDX data is null or empty");
            }

            // Apply model transformation (rotation)
            // Matching Python: mdl_transformed: bytes = model.transform(mdl, Vector3.from_null(), room.rotation)
            // Vector3.from_null() is Vector3(0, 0, 0) - no translation, only rotation
            mdl = ModelTools.Transform(mdl, System.Numerics.Vector3.Zero, room.Rotation);
            if (mdl == null || mdl.Length == 0)
            {
                throw new InvalidOperationException("Transformed MDL data is null or empty");
            }

            // Convert model to target game format (K1 or K2)
            // Use TargetGameType override if set, otherwise use installation.Tsl
            // Matching Python: target_tsl: bool = self.target_game_type if self.target_game_type is not None else installation.tsl
            bool targetTsl = TargetGameType ?? installation.Tsl;
            // Matching Python: mdl_converted: bytes = model.convert_to_k2(mdl_transformed) if target_tsl else model.convert_to_k1(mdl_transformed)
            if (targetTsl)
            {
                mdl = ModelTools.ConvertToK2(mdl);
            }
            else
            {
                mdl = ModelTools.ConvertToK1(mdl);
            }

            if (mdl == null || mdl.Length == 0)
            {
                throw new InvalidOperationException("Converted MDL data is null or empty");
            }

            return (mdl, mdx);
        }

        /// <summary>
        /// Processes lightmaps for a room's model by extracting, renaming, and updating references.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method processes lightmaps (pre-baked lighting textures) for a room's model. It:
        /// 1. Extracts lightmap references from the MDL file
        /// 2. Renames them to module-specific names
        /// 3. Looks up lightmap data from kits or installation
        /// 4. Converts lightmap format if needed
        /// 5. Adds lightmap and TXI data to the module
        /// 6. Updates the model to reference the renamed lightmaps
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Extract Lightmap References
        /// - Use ModelTools.IterateLightmaps() to find all lightmap names in the MDL file
        /// - Each lightmap reference is a texture name used for lighting
        ///
        /// STEP 2: Rename Lightmaps
        /// - For each lightmap found:
        ///   * Create a module-specific name: "{ModuleId}_lm{count}"
        ///   * Store the mapping in lmRenames dictionary
        ///   * Increment _totalLm counter for next lightmap
        ///
        /// STEP 3: Look Up Lightmap Data
        /// - First, try to find lightmap in the room's kit:
        ///   * Check kit.Lightmaps dictionary (case-insensitive)
        ///   * If found, use kit lightmap data
        /// - If not in kit, try installation resources:
        ///   * Search in CHITIN, OVERRIDE, TEXTURES_GUI, TEXTURES_TPA locations
        ///   * Load as TPC (texture) resource
        ///   * Convert format if needed (BGR/DXT1 -> RGB, BGRA/DXT3/DXT5 -> RGBA)
        ///   * Convert to TGA bytes
        ///
        /// STEP 4: Add Lightmap Resources
        /// - Add lightmap TGA data to module with renamed name
        /// - Add TXI data if available (texture metadata)
        /// - If TXI not found, add empty TXI data
        ///
        /// STEP 5: Update Model References
        /// - Use ModelTools.ChangeLightmaps() to update all lightmap references in the MDL
        /// - Old lightmap names are replaced with new module-specific names
        /// - Returns modified MDL data with updated references
        ///
        /// LIGHTMAP FORMAT CONVERSION:
        ///
        /// Lightmaps may be stored in various formats:
        /// - BGR, DXT1, Greyscale -> Converted to RGB
        /// - BGRA, DXT3, DXT5 -> Converted to RGBA
        ///
        /// This ensures consistent format for the game engine.
        ///
        /// FALLBACK BEHAVIOR:
        ///
        /// If a lightmap is not found in the kit or installation:
        /// - Log a warning
        /// - Remove it from the rename mapping
        /// - Decrement the lightmap counter
        /// - Continue processing other lightmaps
        ///
        /// The model will still work, but that lightmap will be missing (may appear dark).
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:287-365
        /// Original: def process_lightmaps(self, room: IndoorMapRoom, mdl_data: bytes, installation: HTInstallation):
        /// </remarks>
        /// <param name="room">The room whose lightmaps should be processed</param>
        /// <param name="mdlData">The MDL model data containing lightmap references</param>
        /// <param name="installation">The game installation (for resource lookup)</param>
        /// <returns>Modified MDL data with updated lightmap references</returns>
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

        /// <summary>
        /// Adds model resources (MDL and MDX) to the module.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method adds a room's model data to the module. Models consist of two files:
        /// - MDL: Model definition file (geometry, materials, textures, lightmaps)
        /// - MDX: Model extension file (animations, additional data)
        ///
        /// Both files are added with the same resource name (modelname), and the game engine
        /// automatically loads the MDX when loading the MDL.
        ///
        /// HOW IT WORKS:
        ///
        /// The method simply adds both MDL and MDX data to the module using the provided model name.
        /// The model name is typically in the format "{ModuleId}_room{index}" (e.g., "test01_room0").
        ///
        /// MODEL PROCESSING:
        ///
        /// Before calling this method, the model should have been:
        /// - Flipped (if room is flipped)
        /// - Rotated (if room is rotated)
        /// - Converted to target game format (K1 or K2)
        /// - Updated with renamed texture references
        /// - Updated with renamed lightmap references
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:367-388
        /// Original: def add_model_resources(self, modelname: str, mdl_data: bytes, mdx_data: bytes):
        /// </remarks>
        /// <param name="modelname">The resource name for the model (e.g., "test01_room0")</param>
        /// <param name="mdlData">The MDL model data</param>
        /// <param name="mdxData">The MDX model extension data</param>
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
        /// - Characters cannot walk on indoor map levels/modules (pathfinding returns no path)
        /// - Pathfinding fails (characters cannot find routes between points)
        /// - Height calculation fails (characters float or sink into ground)
        /// - Line of sight checks fail (can't see through walkable areas)
        /// - The walkmesh appears to have correct surface materials, but nothing works
        /// - Performance is very poor (frame rate drops when near walkmesh)
        /// - FindFaceAt() is extremely slow (must check every triangle instead of using AABB tree)
        ///
        /// HOW TO VERIFY THE FIX:
        ///
        /// If you suspect this bug is still occurring, check the following:
        ///
        /// 1. Walkmesh Type Check:
        ///    - After ProcessBwm(), verify: bwm.WalkmeshType == BWMType.AreaModel
        ///    - If it's PlaceableOrDoor, the bug is present
        ///
        /// 2. AABB Tree Check:
        ///    - When converting to NavigationMesh, verify: _aabbRoot != null
        ///    - If _aabbRoot is null, the AABB tree wasn't built, causing the bug
        ///
        /// 3. Material Preservation Check:
        ///    - Verify that face.Material is preserved after Flip(), Rotate(), Translate()
        ///    - Materials should remain unchanged (only vertices are modified)
        ///    - If materials are lost, faces become non-walkable
        ///
        /// 4. Walkability Check:
        ///    - Verify NavigationMesh.IsWalkable() returns true for faces with walkable materials
        ///    - Check that WalkableMaterials set matches SurfaceMaterialExtensions.WalkableMaterials
        ///    - Any mismatch will cause incorrect walkability determination
        ///
        /// 5. FindFaceAt Performance Check:
        ///    - If FindFaceAt() is very slow (checking all triangles), AABB tree is missing
        ///    - With AABB tree, FindFaceAt() should be fast (O(log n) instead of O(n))
        ///
        /// 6. Pathfinding Test:
        ///    - Try pathfinding between two points on walkable surfaces
        ///    - If pathfinding fails or is extremely slow, AABB tree is likely missing
        ///
        /// If all of these checks pass but the bug still occurs, the problem is elsewhere:
        /// - Check how the NavigationMesh is created from the BWM (converter might have issues)
        /// - Check if the game engine's walkmesh loading code is correct
        /// - Check if materials are being preserved when the BWM is written to file
        /// - Check if the BWM file format is correct (header, data structure, etc.)
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
            var bwm = IndoorMapRoomHelper.DeepCopyBwm(room.BaseWalkmesh());
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
            // Call the BWM method to perform the remapping
            // Note: The PyKotor version preserves None (null), but we convert null to -1 here
            // because -1 is the standard "no connection" value used in the game engine.
            // The game engine expects -1 for "no connection" rather than null when serializing.
            int? remapValue = actualIndex ?? -1;
            bwm.RemapTransitions(dummyIndex, remapValue);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:449-454
        // Original: def add_bwm_resource(self, modelname: str, bwm: BWM):
        private void AddBwmResource(string modelname, BWM bwm)
        {
            _mod.SetData(modelname, ResourceType.WOK, BWMAuto.BytesBwm(bwm));
        }

        /// <summary>
        /// Handles door insertion, UTD creation, and door padding for all room connections.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method processes all door insertions (places where doors should be placed) and:
        /// 1. Creates GIT door entries (door objects in the game world)
        /// 2. Creates UTD resources (door templates with properties)
        /// 3. Adds door hooks to the layout (LYT)
        /// 4. Handles door padding for mismatched door sizes (height and width)
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Get Door Insertions
        /// - Call DoorInsertions() to get list of all door placement locations
        /// - Each insertion contains: door template, rooms, position, rotation, hooks
        ///
        /// STEP 2: Create GIT Door Entry
        /// - For each insertion, create a GITDoor object:
        ///   * Position: Where the door is placed (from insertion)
        ///   * ResRef: Unique door resource name (e.g., "test01_dor00")
        ///   * Bearing: Door rotation in radians (converted from degrees)
        ///
        /// STEP 3: Create UTD Resource
        /// - Deep copy the door template UTD (UtdK1 or UtdK2 based on game)
        /// - Set door resource reference and tag
        /// - Set static flag (true if door doesn't open, false if it opens)
        /// - Add UTD to module
        ///
        /// STEP 4: Add Door Hook to Layout
        /// - Create LYTDoorHook with room name, door resref, position, and orientation
        /// - Add to _lyt.DoorHooks for layout system
        ///
        /// STEP 5: Handle Door Padding
        /// - If two rooms are connected with different door sizes:
        ///   * Height padding: If door heights differ, add top padding model
        ///   * Width padding: If door widths differ, add side padding model
        /// - Padding models fill gaps between mismatched door openings
        /// - Process padding models (transform, convert format, rename textures/lightmaps)
        /// - Add padding models to module and layout
        ///
        /// DOOR PADDING:
        ///
        /// When two rooms with different door sizes are connected, gaps appear. Padding models
        /// fill these gaps:
        ///
        /// HEIGHT PADDING:
        /// - Used when one door is taller than the other
        /// - Top padding model is placed above the shorter door
        /// - Padding size is selected from kit.TopPadding based on height difference
        ///
        /// WIDTH PADDING:
        /// - Used when one door is wider than the other
        /// - Side padding model is placed beside the narrower door
        /// - Padding size is selected from kit.SidePadding based on width difference
        ///
        /// PADDING MODEL PROCESSING:
        ///
        /// Padding models are processed similarly to room models:
        /// 1. Transform (rotate to match door rotation)
        /// 2. Convert format (K1 or K2)
        /// 3. Rename textures (use _texRenames mapping)
        /// 4. Process lightmaps (rename and add to module)
        /// 5. Add to module and layout
        ///
        /// STATIC DOORS:
        ///
        /// Static doors (insert.Static == true) are doors that don't open. They are placed at
        /// hooks that aren't connected to another room. These doors block passage but don't
        /// have opening animations.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:456-576
        /// Original: def handle_door_insertions(self, installation: HTInstallation):
        /// </remarks>
        /// <param name="installation">The game installation (for format conversion and resource lookup)</param>
        private void HandleDoorInsertions(HTInstallation installation)
        {
            int paddingCount = 0;
            var insertions = DoorInsertions();

            for (int i = 0; i < insertions.Count; i++)
            {
                var insert = insertions[i];
                string doorResname = $"{ModuleId}_dor{i:D2}";
                var door = new GITDoor
                {
                    Position = insert.Position,
                    ResRef = new ResRef(doorResname)
                };
                door.Bearing = (float)(Math.PI / 180.0 * insert.Rotation);
                door.TweakColor = null;
                _git.Doors.Add(door);

                // Deep copy UTD from door template (matching Python line 484)
                // Use TargetGameType override if set, otherwise use installation.Tsl
                bool targetTsl = TargetGameType ?? installation.Tsl;
                // Python: utd: UTD = deepcopy(insert.door.utd_k2 if target_tsl else insert.door.utd_k1)
                UTD sourceUtd = targetTsl ? insert.Door.UtdK2 : insert.Door.UtdK1;
                if (sourceUtd == null)
                {
                    new RobustLogger().Warning($"Door insertion {i} has no UTD template (UtdK1/UtdK2 is null). Skipping UTD creation.");
                    continue;
                }

                UTD utd = DeepCopyUtd(sourceUtd);
                utd.ResRef = door.ResRef;
                utd.Static = insert.Static;

                // Python: utd.tag = door_resname.title().replace("_", "")
                // C# equivalent: capitalize first letter of each word, remove underscores
                string tag = doorResname;
                if (tag.Contains("_"))
                {
                    var parts = tag.Split('_');
                    for (int j = 0; j < parts.Length; j++)
                    {
                        if (parts[j].Length > 0)
                        {
                            parts[j] = char.ToUpperInvariant(parts[j][0]) + (parts[j].Length > 1 ? parts[j].Substring(1) : "");
                        }
                    }
                    tag = string.Join("", parts);
                }
                else if (tag.Length > 0)
                {
                    tag = char.ToUpperInvariant(tag[0]) + (tag.Length > 1 ? tag.Substring(1) : "");
                }
                utd.Tag = tag;

                // Add UTD to module (matching Python line 488)
                // Python: self.mod.set_data(door_resname, ResourceType.UTD, bytes_utd(utd))
                byte[] utdData = UTDHelpers.BytesUtd(utd, targetTsl ? BioWareGame.K2 : BioWareGame.K1);
                _mod.SetData(doorResname, ResourceType.UTD, utdData);

                // Create door hook in layout (matching Python lines 490-491)
                // Python: orientation: Vector4 = Vector4.from_euler(0, 0, math.radians(door.bearing))
                // Python: self.lyt.doorhooks.append(LYTDoorHook(self.room_names[insert.room], door_resname, insert.position, orientation))
                float bearingRadians = door.Bearing;
                System.Numerics.Quaternion quaternion = QuaternionFromEuler(0.0, 0.0, bearingRadians);
                Vector4 orientation = new Vector4(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
                string roomName = _roomNames[insert.Room];
                _lyt.Doorhooks.Add(new LYTDoorHook(roomName, doorResname, insert.Position, orientation));

                // Handle padding for height/width mismatches (matching Python lines 493-576)
                if (insert.Hook1 != null && insert.Hook2 != null)
                {
                    // Height padding (matching Python lines 494-536)
                    if (insert.Hook1.Door.Height != insert.Hook2.Door.Height)
                    {
                        IndoorMapRoom cRoom = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Room : insert.Room2;
                        if (cRoom == null)
                        {
                            new RobustLogger().Warning($"No room found for door insertion {i} height padding. Skipping.");
                        }
                        else
                        {
                            KitComponentHook cHook = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Hook1 : insert.Hook2;
                            KitComponentHook altHook = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Hook2 : insert.Hook1;

                            Kit kit = cRoom.Component.Kit;
                            int doorIndex = kit.Doors.IndexOf(cHook.Door);
                            if (doorIndex >= 0 && kit.TopPadding.ContainsKey(doorIndex))
                            {
                                float height = altHook.Door.Height * 100.0f;
                                int? paddingKey = null;
                                foreach (var key in kit.TopPadding[doorIndex].Keys)
                                {
                                    if (key > height)
                                    {
                                        if (!paddingKey.HasValue || key < paddingKey.Value)
                                        {
                                            paddingKey = key;
                                        }
                                    }
                                }

                                if (paddingKey.HasValue)
                                {
                                    string paddingName = $"{ModuleId}_tpad{paddingCount}";
                                    paddingCount++;

                                    // Transform padding model (matching Python lines 518-522)
                                    byte[] padMdl = kit.TopPadding[doorIndex][paddingKey.Value].Mdl;
                                    padMdl = ModelTools.Transform(padMdl, System.Numerics.Vector3.Zero, insert.Rotation);

                                    // Convert model to target game format (matching Python line 523)
                                    // Use TargetGameType override if set, otherwise use installation.Tsl (uses outer targetTsl variable)
                                    // Python: pad_mdl_converted: bytes = model.convert_to_k2(pad_mdl) if target_tsl else model.convert_to_k1(pad_mdl)
                                    byte[] padMdlConverted = targetTsl
                                        ? ModelTools.ConvertToK2(padMdl)
                                        : ModelTools.ConvertToK1(padMdl);

                                    // Change textures (matching Python line 524)
                                    padMdlConverted = ModelTools.ChangeTextures(padMdlConverted, _texRenames);

                                    // Process lightmaps (matching Python lines 525-532)
                                    // Note: Python line 532 uses pad_mdl (original) but should use pad_mdl_converted for consistency
                                    // We use padMdlConverted to match the pattern used in width padding and for correctness
                                    var lmRenames = new Dictionary<string, string>();
                                    foreach (var lightmap in ModelTools.IterateLightmaps(padMdlConverted))
                                    {
                                        string renamed = $"{ModuleId}_lm{_totalLm}";
                                        _totalLm++;
                                        string lightmapLower = lightmap.ToLowerInvariant();
                                        lmRenames[lightmapLower] = renamed;

                                        if (kit.Lightmaps.TryGetValue(lightmapLower, out byte[] lightmapData) ||
                                            kit.Lightmaps.TryGetValue(lightmap, out lightmapData))
                                        {
                                            _mod.SetData(renamed, ResourceType.TGA, lightmapData);
                                            if (kit.Txis.TryGetValue(lightmapLower, out byte[] txiData) ||
                                                kit.Txis.TryGetValue(lightmap, out txiData))
                                            {
                                                _mod.SetData(renamed, ResourceType.TXI, txiData);
                                            }
                                            else
                                            {
                                                _mod.SetData(renamed, ResourceType.TXI, new byte[0]);
                                            }
                                        }
                                    }

                                    // Change lightmaps in model (matching Python line 532)
                                    // Note: Python uses pad_mdl here, but we use padMdlConverted for consistency with converted model
                                    padMdlConverted = ModelTools.ChangeLightmaps(padMdlConverted, lmRenames);

                                    // Add padding model resources (matching Python lines 533-534)
                                    _mod.SetData(paddingName, ResourceType.MDL, padMdlConverted);
                                    _mod.SetData(paddingName, ResourceType.MDX, kit.TopPadding[doorIndex][paddingKey.Value].Mdx);

                                    // Add padding room to layout and visibility (matching Python lines 535-536)
                                    _lyt.Rooms.Add(new LYTRoom(new ResRef(paddingName), insert.Position));
                                    _vis.AddRoom(paddingName);
                                }
                                else
                                {
                                    new RobustLogger().Info($"No padding key found for door insertion {i} height.");
                                }
                            }
                        }
                    }

                    // Width padding (matching Python lines 537-576)
                    if (insert.Hook1.Door.Width != insert.Hook2.Door.Width)
                    {
                        IndoorMapRoom cRoom = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Room : insert.Room2;
                        KitComponentHook cHook = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Hook1 : insert.Hook2;
                        KitComponentHook altHook = insert.Hook1.Door.Height < insert.Hook2.Door.Height ? insert.Hook2 : insert.Hook1;

                        if (cRoom == null)
                        {
                            new RobustLogger().Warning($"No room found for door insertion {i} width padding. Skipping.");
                        }
                        else
                        {
                            Kit kit = cRoom.Component.Kit;
                            int doorIndex = kit.Doors.IndexOf(cHook.Door);
                            if (doorIndex >= 0 && kit.SidePadding.ContainsKey(doorIndex))
                            {
                                float width = altHook.Door.Width * 100.0f;
                                int? paddingKey = null;
                                foreach (var key in kit.SidePadding[doorIndex].Keys)
                                {
                                    if (key > width)
                                    {
                                        if (!paddingKey.HasValue || key < paddingKey.Value)
                                        {
                                            paddingKey = key;
                                        }
                                    }
                                }

                                if (paddingKey.HasValue)
                                {
                                    string paddingName = $"{ModuleId}_tpad{paddingCount}";
                                    paddingCount++;

                                    // Transform padding model (matching Python lines 558-562)
                                    byte[] padMdl = kit.SidePadding[doorIndex][paddingKey.Value].Mdl;
                                    padMdl = ModelTools.Transform(padMdl, System.Numerics.Vector3.Zero, insert.Rotation);

                                    // Convert model to target game format (matching Python line 563)
                                    // Use TargetGameType override if set, otherwise use installation.Tsl (uses outer targetTsl variable)
                                    // Python: pad_mdl = model.convert_to_k2(pad_mdl) if target_tsl else model.convert_to_k1(pad_mdl)
                                    padMdl = targetTsl
                                        ? ModelTools.ConvertToK2(padMdl)
                                        : ModelTools.ConvertToK1(padMdl);

                                    // Change textures (matching Python line 564)
                                    padMdl = ModelTools.ChangeTextures(padMdl, _texRenames);

                                    // Process lightmaps (matching Python lines 565-572)
                                    var lmRenames = new Dictionary<string, string>();
                                    foreach (var lightmap in ModelTools.IterateLightmaps(padMdl))
                                    {
                                        string renamed = $"{ModuleId}_lm{_totalLm}";
                                        _totalLm++;
                                        string lightmapLower = lightmap.ToLowerInvariant();
                                        lmRenames[lightmapLower] = renamed;

                                        if (kit.Lightmaps.TryGetValue(lightmapLower, out byte[] lightmapData) ||
                                            kit.Lightmaps.TryGetValue(lightmap, out lightmapData))
                                        {
                                            _mod.SetData(renamed, ResourceType.TGA, lightmapData);
                                            if (kit.Txis.TryGetValue(lightmapLower, out byte[] txiData) ||
                                                kit.Txis.TryGetValue(lightmap, out txiData))
                                            {
                                                _mod.SetData(renamed, ResourceType.TXI, txiData);
                                            }
                                            else
                                            {
                                                _mod.SetData(renamed, ResourceType.TXI, new byte[0]);
                                            }
                                        }
                                    }

                                    // Change lightmaps in model (matching Python line 572)
                                    padMdl = ModelTools.ChangeLightmaps(padMdl, lmRenames);

                                    // Add padding model resources (matching Python lines 573-574)
                                    _mod.SetData(paddingName, ResourceType.MDL, padMdl);
                                    _mod.SetData(paddingName, ResourceType.MDX, kit.SidePadding[doorIndex][paddingKey.Value].Mdx);

                                    // Add padding room to layout and visibility (matching Python lines 575-576)
                                    _lyt.Rooms.Add(new LYTRoom(new ResRef(paddingName), insert.Position));
                                    _vis.AddRoom(paddingName);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a deep copy of a UTD object using the Dismantle/Construct pattern.
        /// Matching PyKotor implementation: deepcopy(utd) pattern used in handle_door_insertions.
        /// </summary>
        /// <param name="source">The UTD object to copy.</param>
        /// <returns>A deep copy of the UTD object.</returns>
        private static UTD DeepCopyUtd(UTD source)
        {
            // Use Dismantle/Construct pattern for reliable deep copy (matching Python deepcopy behavior)
            // This is the same pattern used in UTPEditor.CopyUtp() and other editor classes
            BioWareGame game = BioWareGame.K2; // Default game for serialization
            var gff = UTDHelpers.DismantleUtd(source, game);
            return UTDHelpers.ConstructUtd(gff);
        }

        /// <summary>
        /// Creates a quaternion from Euler angles (roll, pitch, yaw).
        /// Matching PyKotor implementation at utility/common/geometry.py:887-914
        /// </summary>
        /// <param name="roll">Rotation around X axis in radians.</param>
        /// <param name="pitch">Rotation around Y axis in radians.</param>
        /// <param name="yaw">Rotation around Z axis in radians.</param>
        /// <returns>A quaternion representing the rotation.</returns>
        private static System.Numerics.Quaternion QuaternionFromEuler(double roll, double pitch, double yaw)
        {
            // Matching Python implementation: Vector4.from_euler
            double qx = Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) - Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);
            double qy = Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2);
            double qz = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2) - Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2);
            double qw = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);

            return new System.Numerics.Quaternion((float)qx, (float)qy, (float)qz, (float)qw);
        }

        /// <summary>
        /// Processes and adds the skybox model to the module.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method processes the skybox (background environment) for the module. The skybox
        /// is a 3D model that surrounds the entire area, providing the background environment
        /// (sky, distant mountains, etc.). It's positioned at the origin (0, 0, 0) and is always
        /// visible regardless of room visibility.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Check if Skybox is Set
        /// - If Skybox property is null or empty, return early (no skybox)
        ///
        /// STEP 2: Find Skybox in Kits
        /// - Search through all provided kits for a skybox matching the Skybox name
        /// - Kit.Skyboxes dictionary contains skybox models keyed by name
        ///
        /// STEP 3: Process Skybox Model
        /// - Get skybox MDL and MDX data from kit
        /// - Update texture references using _texRenames (so textures match module naming)
        /// - ModelTools.ChangeTextures() updates all texture references in the MDL
        ///
        /// STEP 4: Add Skybox Resources
        /// - Add MDL and MDX to module with name "{ModuleId}_sky"
        /// - Add skybox room to layout at origin (0, 0, 0)
        /// - Add skybox to visibility system (always visible)
        ///
        /// SKYBOX POSITIONING:
        ///
        /// The skybox is always positioned at (0, 0, 0) because:
        /// - It surrounds the entire area, not a specific room
        /// - It's rendered as a background, not part of the walkable area
        /// - The game engine handles skybox rendering separately from room rendering
        ///
        /// TEXTURE RENAMING:
        ///
        /// Skybox textures are renamed using the same _texRenames mapping as room models.
        /// This ensures all textures in the module use consistent naming.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:578-608
        /// Original: def process_skybox(self, kits: list[Kit]):
        /// </remarks>
        /// <param name="kits">List of kits to search for skybox data</param>
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
                // Matching Python line 604: mdl_converted: bytes = model.change_textures(mdl, self.tex_renames)
                byte[] mdlConverted = ModelTools.ChangeTextures(skyboxData.Mdl, _texRenames);
                _mod.SetData(modelName, ResourceType.MDL, mdlConverted);
                _mod.SetData(modelName, ResourceType.MDX, skyboxData.Mdx);

                // Matching Python line 608: self.lyt.rooms.append(LYTRoom(model_name, Vector3(0, 0, 0)))
                var lytRoom = new LYTRoom(new ResRef(modelName), Vector3.Zero);
                _lyt.Rooms.Add(lytRoom);
                _vis.AddRoom(modelName);
            }
        }

        /// <summary>
        /// Generates a minimap image from room preview images and adds it to the module.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method generates a minimap (small overview map) for the module by combining all
        /// room preview images with their transformations (position, rotation, flip). The minimap
        /// is displayed in the game's area map interface, showing the player where they are and
        /// the layout of the area.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Generate Minimap Image
        /// - Call GenerateMipmap() to create the minimap bitmap
        /// - This combines all room preview images with transformations
        ///
        /// STEP 2: Extract Pixel Data
        /// - Convert the minimap bitmap to RGBA pixel data
        /// - Extract pixels in row-major order (y, x) matching Python's itertools.product
        /// - Each pixel is 4 bytes: R, G, B, A (alpha always 255)
        ///
        /// STEP 3: Create TPC Texture
        /// - Create a TPC object with RGBA format
        /// - Set pixel data with dimensions 512x256 (standard minimap size)
        /// - This matches the game's minimap texture format
        ///
        /// STEP 4: Add to Module
        /// - Convert TPC to TGA bytes
        /// - Add to module with name "lbl_map{ModuleId}" (e.g., "lbl_maptest01")
        /// - The game engine looks for minimaps with this naming convention
        ///
        /// MINIMAP FORMAT:
        ///
        /// The minimap is always 512x256 pixels (width x height):
        /// - This matches the game's minimap display size
        /// - The image is scaled and centered to fit this size
        /// - Black areas indicate empty space (no rooms)
        ///
        /// PIXEL EXTRACTION:
        ///
        /// Pixels are extracted in row-major order (y, x):
        /// - For y from 0 to 255:
        ///   * For x from 0 to 511:
        ///     * Extract RGBA values
        ///     * Add to TPC data array
        ///
        /// This matches Python's itertools.product(range(256), range(512)) iteration order.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:610-631
        /// Original: def generate_and_set_minimap(self):
        /// </remarks>
        private void GenerateAndSetMinimap()
        {
            var minimap = GenerateMipmap();

            // Extract pixel data from minimap image (matching Python lines 625-628)
            // Python: for y, x in itertools.product(range(256), range(512)):
            //         pixel: QColor = QColor(minimap.image.pixel(x, y))
            //         tpc_data.extend([pixel.red(), pixel.green(), pixel.blue(), 255])
            var tpcData = new List<byte>();

            // The minimap image is 512x256 (width x height)
            // We need to extract pixels in row-major order (y, x) matching Python's itertools.product(range(256), range(512))
            var bitmap = minimap.Image;

            // Check if bitmap is a WriteableBitmap (which it should be from GenerateMipmap)
            if (bitmap is WriteableBitmap writeableBitmap)
            {
                using (var lockedBitmap = writeableBitmap.Lock())
                {
                    int width = lockedBitmap.Size.Width;  // Should be 512
                    int height = lockedBitmap.Size.Height; // Should be 256

                    unsafe
                    {
                        byte* pixelPtr = (byte*)lockedBitmap.Address;
                        int rowStride = lockedBitmap.RowBytes;

                        // Iterate in row-major order (y, x) matching Python: itertools.product(range(256), range(512))
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                // Calculate pixel offset (RGBA format, 4 bytes per pixel)
                                int pixelOffset = y * rowStride + x * 4;

                                // Extract RGBA values (matching Python: [pixel.red(), pixel.green(), pixel.blue(), 255])
                                byte r = pixelPtr[pixelOffset];
                                byte g = pixelPtr[pixelOffset + 1];
                                byte b = pixelPtr[pixelOffset + 2];
                                byte a = pixelPtr[pixelOffset + 3];

                                // Add to TPC data array (always use 255 for alpha as per Python implementation)
                                tpcData.Add(r);
                                tpcData.Add(g);
                                tpcData.Add(b);
                                tpcData.Add(255);
                            }
                        }
                    }
                }
            }
            else
            {
                // Fallback: if bitmap is not WriteableBitmap, convert it
                // This shouldn't happen based on GenerateMipmap implementation, but handle it gracefully
                // Use RenderTargetBitmap to convert, then copy to WriteableBitmap
                var renderTarget = new RenderTargetBitmap(
                    new PixelSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                    new Avalonia.Vector(96, 96));

                using (var destContext = renderTarget.CreateDrawingContext())
                {
                    destContext.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height));
                }

                // Convert RenderTargetBitmap to WriteableBitmap
                var convertedBitmap = new WriteableBitmap(
                    new PixelSize(bitmap.PixelSize.Width, bitmap.PixelSize.Height),
                    new Avalonia.Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);

                // Copy pixel data from renderTarget to convertedBitmap
                // In Avalonia 11, RenderTargetBitmap doesn't have Lock(), so we save to stream and reload
                using (var memoryStream = new MemoryStream())
                {
                    renderTarget.Save(memoryStream);
                    memoryStream.Position = 0;
                    var loadedBitmap = WriteableBitmap.Decode(memoryStream);

                    // Copy pixel data from loaded bitmap to convertedBitmap
                    using (var sourceLocked = loadedBitmap.Lock())
                    using (var destLocked = convertedBitmap.Lock())
                    {
                        var sourceBuffer = sourceLocked.Address;
                        var destBuffer = destLocked.Address;
                        int stride = sourceLocked.RowBytes;
                        int size = stride * sourceLocked.Size.Height;
                        unsafe
                        {
                            System.Buffer.MemoryCopy((void*)sourceBuffer, (void*)destBuffer, size, size);
                        }
                    }
                }

                using (var lockedBitmap = convertedBitmap.Lock())
                {
                    int width = lockedBitmap.Size.Width;
                    int height = lockedBitmap.Size.Height;

                    unsafe
                    {
                        byte* pixelPtr = (byte*)lockedBitmap.Address;
                        int rowStride = lockedBitmap.RowBytes;

                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int pixelOffset = y * rowStride + x * 4;
                                byte r = pixelPtr[pixelOffset];
                                byte g = pixelPtr[pixelOffset + 1];
                                byte b = pixelPtr[pixelOffset + 2];

                                tpcData.Add(r);
                                tpcData.Add(g);
                                tpcData.Add(b);
                                tpcData.Add(255);
                            }
                        }
                    }
                }
            }

            // Create TPC from pixel data (matching Python lines 629-630)
            // Python: minimap_tpc: TPC = TPC()
            //         minimap_tpc.set_single(tpc_data, TPCTextureFormat.RGBA, 512, 256)
            var minimapTpc = new TPC();
            minimapTpc.SetSingle(tpcData.ToArray(), TPCTextureFormat.RGBA, 512, 256);

            // Convert TPC to TGA bytes and set in module (matching Python line 631)
            // Python: self.mod.set_data(f"lbl_map{self.module_id}", ResourceType.TGA, bytes_tpc(minimap_tpc, ResourceType.TGA))
            byte[] tgaData = TPCAuto.BytesTpc(minimapTpc, ResourceType.TGA);
            _mod.SetData($"lbl_map{ModuleId}", ResourceType.TGA, tgaData);
        }

        /// <summary>
        /// Handles loading screen image for the module.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method loads and adds the loading screen image that is displayed when the module
        /// is being loaded. The loading screen shows a preview image of the area while the game
        /// loads resources.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Determine Load Screen Path
        /// - For K2 (TSL): "./kits/load_k2.tga"
        /// - For K1: "./kits/load_k1.tga"
        /// - The path is relative to the installation directory
        ///
        /// STEP 2: Load and Add Image
        /// - Check if the load screen file exists
        /// - If found, read the TGA file bytes
        /// - Add to module with name "load_{ModuleId}" (e.g., "load_test01")
        ///
        /// FALLBACK BEHAVIOR:
        ///
        /// If the load screen file doesn't exist:
        /// - Log an error message
        /// - Continue without load screen (game will use default)
        ///
        /// LOAD SCREEN NAMING:
        ///
        /// The game engine looks for load screens with the naming pattern "load_{module_id}".
        /// If this resource is not found, the game uses a default loading screen.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:633-653
        /// Original: def handle_loadscreen(self, installation: HTInstallation):
        /// </remarks>
        /// <param name="installation">The game installation (to determine game version and path)</param>
        private void HandleLoadscreen(HTInstallation installation)
        {
            try
            {
                // Use TargetGameType override if set, otherwise use installation.Tsl
                bool targetTsl = TargetGameType ?? installation.Tsl;
                string loadPath = targetTsl ? "./kits/load_k2.tga" : "./kits/load_k1.tga";
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

        /// <summary>
        /// Sets area (ARE) file attributes including minimap coordinates and lighting.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method configures the ARE (area) file with module-specific settings. The ARE file
        /// contains metadata about the area, including minimap coordinates, lighting, and display
        /// settings.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Set Basic Attributes
        /// - Tag: Module ID (e.g., "test01")
        /// - Name: Localized area name
        /// - DynamicLight: Lighting color (RGB values)
        ///
        /// STEP 2: Set Minimap Coordinates
        /// - MapPoint1/MapPoint2: Image coordinates (0.0 to 1.0) defining minimap display area
        ///   * ImagePointMin: Top-left corner of minimap in image space
        ///   * ImagePointMax: Bottom-right corner of minimap in image space
        /// - WorldPoint1/WorldPoint2: World coordinates defining area bounds
        ///   * WorldPointMin: Minimum world coordinates (bottom-left)
        ///   * WorldPointMax: Maximum world coordinates (top-right)
        ///
        /// STEP 3: Set Map Display Settings
        /// - MapZoom: Zoom level (1 = normal)
        /// - MapResX: Map resolution X (1 = normal)
        /// - NorthAxis: Which axis points north (NegativeY = -Y axis is north)
        ///
        /// MINIMAP COORDINATE MAPPING:
        ///
        /// The minimap uses two coordinate systems:
        /// - Image coordinates (0.0 to 1.0): Where on the minimap texture to display
        /// - World coordinates: Actual game world positions
        ///
        /// The game engine uses these to:
        /// - Display the correct portion of the minimap
        /// - Map player position to minimap position
        /// - Show area boundaries correctly
        ///
        /// NORTH AXIS:
        ///
        /// ARENorthAxis.NegativeY means the -Y axis points north. This is the standard orientation
        /// for KOTOR areas. The game uses this to correctly orient the minimap and compass.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:655-683
        /// Original: def set_area_attributes(self, minimap: MinimapData):
        /// </remarks>
        /// <param name="minimap">Minimap data containing image and world coordinates</param>
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

        /// <summary>
        /// Sets module (IFO) file attributes including entry point and area reference.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method configures the IFO (module info) file with module-specific settings. The
        /// IFO file contains metadata about the module, including the entry point (where players
        /// spawn) and area reference.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Set Module Identification
        /// - Tag: Module ID (e.g., "test01")
        /// - AreaName: Reference to the area (ARE) file
        /// - ResRef: Module resource reference
        ///
        /// STEP 2: Set Visibility
        /// - Call _vis.SetAllVisible() to make all rooms visible to each other
        /// - This is typical for indoor maps where all rooms are connected
        ///
        /// STEP 3: Set Entry Point
        /// - EntryX, EntryY, EntryZ: Where players spawn when entering the module
        /// - These coordinates come from the WarpPoint property
        /// - The entry point should be on a walkable surface
        ///
        /// ENTRY POINT:
        ///
        /// The entry point (warp point) is where characters appear when:
        /// - First entering the module
        /// - Transitioning from another module
        /// - Respawning after death
        ///
        /// The coordinates should be:
        /// - On a walkable surface (not in a wall or void)
        /// - Accessible (not blocked by obstacles)
        /// - Within the area bounds
        ///
        /// VISIBILITY:
        ///
        /// SetAllVisible() makes all rooms visible to each other. This is appropriate for indoor
        /// maps where rooms are typically connected and should all be rendered. For outdoor areas
        /// with distance-based culling, visibility would be calculated based on distance.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:685-704
        /// Original: def set_ifo_attributes(self):
        /// </remarks>
        private void SetIfoAttributes()
        {
            _ifo.Tag = ModuleId;
            _ifo.AreaName = new ResRef(ModuleId);
            _ifo.ResRef = new ResRef(ModuleId);
            _vis.SetAllVisible();
            _ifo.EntryX = WarpPoint.X;
            _ifo.EntryY = WarpPoint.Y;
            _ifo.EntryZ = WarpPoint.Z;
        }

        /// <summary>
        /// Finalizes module data by serializing all resources and writing the MOD file.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method completes the module build process by:
        /// 1. Serializing all layout, visibility, area, and game instance data
        /// 2. Adding them to the module resource archive
        /// 3. Writing the final MOD file to disk
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Serialize Layout (LYT)
        /// - Convert LYT object to binary format using LYTAuto.BytesLyt()
        /// - Add to module with name = ModuleId, type = LYT
        /// - Contains room positions, door hooks, and layout structure
        ///
        /// STEP 2: Serialize Visibility (VIS)
        /// - Convert VIS object to binary format using VISAuto.BytesVis()
        /// - Add to module with name = ModuleId, type = VIS
        /// - Contains visibility relationships between rooms
        ///
        /// STEP 3: Serialize Area (ARE)
        /// - Convert ARE object to binary format using AREHelpers.BytesAre()
        /// - Add to module with name = ModuleId, type = ARE
        /// - Contains area metadata (lighting, minimap, properties)
        ///
        /// STEP 4: Serialize Game Instance (GIT)
        /// - Convert GIT object to binary format using GITHelpers.BytesGit()
        /// - Add to module with name = ModuleId, type = GIT
        /// - Contains doors, placeables, creatures, triggers, etc.
        ///
        /// STEP 5: Serialize Module Info (IFO)
        /// - Dismantle IFO object to GFF format using IFOHelpers.DismantleIfo()
        /// - Convert GFF to binary using GFFAuto.BytesGff()
        /// - Add to module with name = "module", type = IFO
        /// - Contains module metadata (entry point, area reference, etc.)
        ///
        /// STEP 6: Write MOD File
        /// - Use ERFAuto.WriteErf() to write the module archive to disk
        /// - MOD files are ERF archives containing all module resources
        /// - The file is written to the specified output path
        ///
        /// MOD FILE FORMAT:
        ///
        /// MOD files are ERF (Encapsulated Resource Format) archives containing:
        /// - LYT: Layout data
        /// - VIS: Visibility data
        /// - ARE: Area data
        /// - GIT: Game instance data
        /// - IFO: Module info
        /// - MDL/MDX: Room models
        /// - WOK: Walkmeshes
        /// - TGA: Textures, lightmaps, minimap
        /// - UTD: Door templates
        /// - Other resources (scripts, placeables, etc.)
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:706-732
        /// Original: def finalize_module_data(self, output_path: os.PathLike | str):
        /// </remarks>
        /// <param name="outputPath">File system path where the MOD file should be written</param>
        private void FinalizeModuleData(string outputPath)
        {
            _mod.SetData(ModuleId, ResourceType.LYT, LYTAuto.BytesLyt(_lyt));
            _mod.SetData(ModuleId, ResourceType.VIS, VISAuto.BytesVis(_vis));
            _mod.SetData(ModuleId, ResourceType.ARE, AREHelpers.BytesAre(_are));
            _mod.SetData(ModuleId, ResourceType.GIT, GITHelpers.BytesGit(_git));
            // IFO doesn't have a BytesIfo method, so use DismantleIfo + BytesGff
            GFF ifoGff = IFOHelpers.DismantleIfo(_ifo, BioWareGame.K2);
            _mod.SetData("module", ResourceType.IFO, GFFAuto.BytesGff(ifoGff, IFO.BinaryType));

            ERFAuto.WriteErf(_mod, outputPath, ResourceType.MOD);
        }

        /// <summary>
        /// Builds a complete module from the indoor map data.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method orchestrates the entire module build process. It takes the indoor map data
        /// (rooms, positions, connections) and converts it into a complete, playable game module
        /// (MOD file) that can be loaded by the game engine.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Initialize Build State
        /// - Create new ERF module archive
        /// - Initialize LYT (layout), VIS (visibility), ARE (area), IFO (module info), GIT (game instance)
        /// - Initialize tracking dictionaries and sets
        ///
        /// STEP 2: Add Rooms to Visibility
        /// - Call AddRooms() to register all rooms in the VIS system
        ///
        /// STEP 3: Process Room Components
        /// - Call ProcessRoomComponents() to identify used kits and models
        ///
        /// STEP 4: Handle Textures
        /// - Call HandleTextures() to extract, rename, and add textures
        ///
        /// STEP 5: Handle Lightmaps
        /// - Call HandleLightmaps() to process all rooms:
        ///   * Add rooms to layout
        ///   * Process models (flip, rotate, convert)
        ///   * Process lightmaps (rename, add to module)
        ///   * Process walkmeshes (transform, set type, remap transitions)
        ///
        /// STEP 6: Process Skybox
        /// - Call ProcessSkybox() to add skybox model if specified
        ///
        /// STEP 7: Generate Minimap
        /// - Call GenerateAndSetMinimap() to create minimap image
        ///
        /// STEP 8: Handle Loadscreen
        /// - Call HandleLoadscreen() to add loading screen image
        ///
        /// STEP 9: Handle Door Insertions
        /// - Call HandleDoorInsertions() to create doors and padding
        ///
        /// STEP 10: Set Area Attributes
        /// - Call SetAreaAttributes() to configure ARE file with minimap data
        ///
        /// STEP 11: Set Module Attributes
        /// - Call SetIfoAttributes() to configure IFO file
        ///
        /// STEP 12: Finalize Module
        /// - Call FinalizeModuleData() to serialize all data and write MOD file
        ///
        /// BUILD ORDER:
        ///
        /// The build process must happen in this order because:
        /// - Rooms must be registered before processing
        /// - Textures must be renamed before models are processed
        /// - Models must be processed before lightmaps are updated
        /// - Walkmeshes must be processed before doors are placed
        /// - All resources must be added before finalizing
        ///
        /// RESOURCE NAMING:
        ///
        /// All resources are renamed to module-specific names to avoid conflicts:
        /// - Models: "{ModuleId}_room{index}"
        /// - Textures: "{ModuleId}_tex{index}"
        /// - Lightmaps: "{ModuleId}_lm{index}"
        /// - Doors: "{ModuleId}_dor{index:02}"
        /// - Padding: "{ModuleId}_tpad{index}"
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:734-784
        /// Original: def build(self, installation: HTInstallation, kits: list[Kit], output_path: os.PathLike | str):
        /// </remarks>
        /// <param name="installation">The game installation (for format conversion and resource lookup)</param>
        /// <param name="kits">List of kits containing components and resources</param>
        /// <param name="outputPath">File system path where the MOD file should be written</param>
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

        /// <summary>
        /// Serializes the indoor map to JSON format for saving.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method converts the indoor map data structure into a JSON byte array that can be
        /// saved to a file. The JSON format includes all room data, module settings, and walkmesh
        /// overrides. This allows the map to be saved and loaded later.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Create Root Dictionary
        /// - module_id: Module identifier (e.g., "test01")
        /// - name: Localized string data (stringref and translations)
        /// - lighting: RGB color values
        /// - skybox: Skybox name (if set)
        /// - warp: Module ID (for entry point reference)
        ///
        /// STEP 2: Serialize Rooms
        /// - For each room, create a dictionary with:
        ///   * position: [x, y, z] coordinates
        ///   * rotation: Rotation angle in degrees
        ///   * flip_x: Whether room is flipped horizontally
        ///   * flip_y: Whether room is flipped vertically
        ///   * kit: Kit name the component belongs to
        ///   * component: Component name
        ///   * walkmesh_override: Base64-encoded BWM data (if room has custom walkmesh)
        ///
        /// STEP 3: Serialize to JSON
        /// - Convert dictionary to JSON string using JsonSerializer
        /// - Return as UTF-8 byte array
        ///
        /// WALKMESH OVERRIDES:
        ///
        /// If a room has a walkmesh override (custom walkmesh edited by user), it is serialized
        /// as Base64-encoded BWM data. This allows custom walkmeshes to be saved and restored.
        ///
        /// LOCALIZED STRINGS:
        ///
        /// The name is serialized as a dictionary containing:
        /// - stringref: String reference ID
        /// - Language/gender pairs: Substring IDs mapped to text
        ///
        /// This preserves all translations of the module name.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:786-827
        /// Original: def write(self) -> bytes:
        /// </remarks>
        /// <returns>JSON byte array containing the serialized indoor map data</returns>
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
            if (TargetGameType.HasValue)
            {
                data["target_game_type"] = TargetGameType.Value;
            }

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

            return SystemTextEncoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
        }

        /// <summary>
        /// Loads an indoor map from JSON data and returns list of missing rooms/components.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method deserializes JSON data to restore an indoor map. It loads all rooms, their
        /// positions, transformations, and walkmesh overrides. If any rooms reference kits or
        /// components that don't exist, they are reported as missing.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Reset Map State
        /// - Call Reset() to clear existing map data
        ///
        /// STEP 2: Deserialize JSON
        /// - Parse JSON string to dictionary
        /// - Handle encoding errors gracefully
        ///
        /// STEP 3: Load Data
        /// - Call LoadData() to process the dictionary
        /// - Returns list of missing rooms/components
        ///
        /// STEP 4: Handle Errors
        /// - If required keys are missing, throw ArgumentException
        /// - Indicates corrupted or invalid map file
        ///
        /// MISSING ROOMS:
        ///
        /// If a room references a kit or component that doesn't exist in the provided kits list,
        /// a MissingRoomInfo object is created and added to the return list. The caller can use
        /// this to:
        /// - Warn the user about missing content
        /// - Skip loading those rooms
        /// - Prompt for missing kit files
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:829-931
        /// Original: def load(self, raw: bytes, kits: list[Kit]) -> list[MissingRoomInfo]:
        /// </remarks>
        /// <param name="raw">JSON byte array containing the serialized map data</param>
        /// <param name="kits">List of kits to look up components from</param>
        /// <returns>List of MissingRoomInfo objects for rooms/components that couldn't be loaded</returns>
        public List<MissingRoomInfo> Load(byte[] raw, List<Kit> kits)
        {
            Reset();
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(SystemTextEncoding.UTF8.GetString(raw));

            try
            {
                return LoadData(data, kits);
            }
            catch (KeyNotFoundException ex)
            {
                throw new ArgumentException("Map file is corrupted.", ex);
            }
        }

        /// <summary>
        /// Loads indoor map data from a deserialized JSON dictionary.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method processes a deserialized JSON dictionary to restore the indoor map. It loads
        /// module settings, room data, and walkmesh overrides. Rooms that reference missing kits
        /// or components are skipped and reported.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Load Module Settings
        /// - Name: Deserialize localized string (stringref and translations)
        /// - Lighting: RGB color values
        /// - ModuleId: Module identifier
        /// - Skybox: Skybox name (if present)
        ///
        /// STEP 2: Load Rooms
        /// - For each room in the JSON:
        ///   * Look up kit by name
        ///   * Look up component by name within kit
        ///   * If kit or component missing, add to missingRooms list and skip
        ///   * Create IndoorMapRoom with position, rotation, flip settings
        ///   * Load walkmesh override if present (Base64 decode)
        ///   * Add room to Rooms list
        ///
        /// STEP 3: Return Missing Rooms
        /// - Return list of MissingRoomInfo objects
        /// - Each entry contains: kit name, component name, reason (kit_missing or component_missing)
        ///
        /// WALKMESH OVERRIDES:
        ///
        /// If a room has a walkmesh_override field, it is Base64-decoded and parsed as a BWM.
        /// This restores custom walkmeshes that were edited by the user.
        ///
        /// ERROR HANDLING:
        ///
        /// Missing kits/components are logged as warnings but don't stop the loading process.
        /// The map will load with available rooms, and missing rooms are reported to the caller.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:860-931
        /// Original: def _load_data(self, data: dict[str, Any], kits: list[Kit]) -> list[MissingRoomInfo]:
        /// </remarks>
        /// <param name="data">Deserialized JSON dictionary containing map data</param>
        /// <param name="kits">List of kits to look up components from</param>
        /// <returns>List of MissingRoomInfo objects for rooms/components that couldn't be loaded</returns>
        private List<MissingRoomInfo> LoadData(Dictionary<string, JsonElement> data, List<Kit> kits)
        {
            var missingRooms = new List<MissingRoomInfo>();

            var nameData = data["name"];
            Name = new LocalizedString(nameData.GetProperty("stringref").GetInt32());

            foreach (var prop in nameData.EnumerateObject())
            {
                if (int.TryParse(prop.Name, out int substringId))
                {
                    Language language;
                    Gender gender;
                    LocalizedString.SubstringPair(substringId, out language, out gender);
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
            if (data.ContainsKey("target_game_type"))
            {
                TargetGameType = data["target_game_type"].GetBoolean();
            }
            else
            {
                TargetGameType = null;
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

        /// <summary>
        /// Resets the indoor map to default empty state.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method clears all map data and resets it to default values. It's used when:
        /// - Creating a new map
        /// - Loading a map (to clear old data first)
        /// - Resetting the map editor
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Clear Rooms
        /// - Remove all rooms from the Rooms list
        ///
        /// STEP 2: Reset Module Settings
        /// - ModuleId: Set to "test01" (default module ID)
        /// - Name: Set to "New Module" (English)
        /// - Lighting: Set to gray (0.5, 0.5, 0.5)
        ///
        /// NOTE: Other properties (Skybox, WarpPoint) are not reset here because they may have
        /// default values set in the constructor. If needed, they should be reset explicitly.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:933-937
        /// Original: def reset(self):
        /// </remarks>
        public void Reset()
        {
            Rooms.Clear();
            ModuleId = "test01";
            Name = LocalizedString.FromEnglish("New Module");
            Lighting = new ParsingColor(0.5f, 0.5f, 0.5f);
            TargetGameType = null;
        }

        /// <summary>
        /// Generates a minimap image by combining all room preview images with transformations.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method creates a minimap (overview map) by taking all room preview images and
        /// drawing them onto a single image with their transformations applied (position, rotation,
        /// flip). The result is a 512x256 pixel image that shows the layout of the entire area.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Collect and Transform Walkmeshes
        /// - For each room, get its base walkmesh
        /// - Apply transformations (flip, rotate, translate) to match room placement
        /// - Collect all transformed walkmeshes
        ///
        /// STEP 2: Calculate Bounding Box
        /// - Find minimum and maximum X, Y, Z coordinates across all walkmesh vertices
        /// - Add 5-unit padding on all sides
        /// - This defines the area bounds
        ///
        /// STEP 3: Create Initial Pixmap
        /// - Calculate dimensions: (width * 10, height * 10) pixels (10 pixels per unit)
        /// - Create RenderTargetBitmap with calculated size
        /// - Fill with black background
        ///
        /// STEP 4: Draw Room Images
        /// - For each room:
        ///   * Get room component preview image
        ///   * Build transformation matrix:
        ///     - Translate to room position (scaled by 10)
        ///     - Rotate by room rotation
        ///     - Scale for flip (X or Y axis flip)
        ///     - Translate to center image
        ///   * Draw image with transformation applied
        ///
        /// STEP 5: Scale to Standard Size
        /// - Scale pixmap to 435x256 (keeping aspect ratio)
        /// - This is the standard minimap display size
        ///
        /// STEP 6: Center on Final Canvas
        /// - Create 512x256 final canvas
        /// - Fill with black
        /// - Center the scaled image vertically
        ///
        /// STEP 7: Flip Y-Axis
        /// - Flip image vertically (Y-axis inversion)
        /// - This matches game coordinate system (Y-up vs screen Y-down)
        ///
        /// STEP 8: Convert to RGB Format
        /// - Convert to WriteableBitmap with RGBA8888 format
        /// - Extract RGB data (alpha always 255)
        ///
        /// STEP 9: Calculate Coordinates
        /// - ImagePointMin/Max: Image coordinates (0.0 to 1.0) for minimap display area
        /// - WorldPointMin/Max: World coordinates for area bounds
        ///
        /// COORDINATE SYSTEMS:
        ///
        /// The minimap uses two coordinate systems:
        /// - Image coordinates (0.0 to 1.0): Where on the minimap texture the area is displayed
        /// - World coordinates: Actual game world positions
        ///
        /// These are used by the game engine to:
        /// - Display the correct portion of the minimap
        /// - Map player position to minimap position
        ///
        /// TRANSFORMATION ORDER:
        ///
        /// Room images are transformed in this order:
        /// 1. Translate to center (move image center to origin)
        /// 2. Scale for flip (mirror if needed)
        /// 3. Rotate (rotate around origin)
        /// 4. Translate to room position (move to final location)
        ///
        /// This ensures images are correctly positioned, rotated, and flipped.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:939-1022
        /// Original: def generate_mipmap(self) -> MinimapData:
        /// </remarks>
        /// <returns>MinimapData containing the generated image and coordinate mappings</returns>
        public MinimapData GenerateMipmap()
        {
            // Get bounding box of all walkmeshes
            var walkmeshes = new List<BWM>();
            foreach (var room in Rooms)
            {
                var bwm = DeepCopyBwm(room.BaseWalkmesh());
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
                    // Build transformation matrix: translate -> rotate -> scale -> translate to center
                    double translateX = room.Position.X * 10 - bbmin.X * 10;
                    double translateY = room.Position.Y * 10 - bbmin.Y * 10;
                    double imageWidth = roomImage.PixelSize.Width;
                    double imageHeight = roomImage.PixelSize.Height;
                    double scaleX = room.FlipX ? -1.0 : 1.0;
                    double scaleY = room.FlipY ? -1.0 : 1.0;
                    double rotationRadians = room.Rotation;

                    // Build composite transformation matrix (applied in reverse order of operations)
                    // 1. Translate to center image (last operation)
                    // 2. Scale for flipping
                    // 3. Rotate
                    // 4. Translate to room position (first operation)
                    var transform = Matrix.Identity;
                    transform = transform * Matrix.CreateTranslation(translateX, translateY);
                    transform = transform * Matrix.CreateRotation(rotationRadians);
                    transform = transform * Matrix.CreateScale(scaleX, scaleY);
                    transform = transform * Matrix.CreateTranslation(-imageWidth / 2, -imageHeight / 2);

                    using (context.PushTransform(transform))
                    {
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
                // Scale Y by -1 and translate to flip
                var flipMatrix = Matrix.CreateScale(1, -1) * Matrix.CreateTranslation(0, 256);
                using (context.PushTransform(flipMatrix))
                {
                    context.DrawImage(finalPixmap, new Rect(0, 0, 512, 256));
                }
            }

            // Convert to RGB888 format (matching Python line 1010: image.convertTo(QImage.Format.Format_RGB888))
            // In Avalonia, we create a WriteableBitmap with RGB format
            // Note: Avalonia's WriteableBitmap uses RGBA by default, but we can extract RGB data
            var rgbImage = new WriteableBitmap(new PixelSize(512, 256), new Avalonia.Vector(96, 96), PixelFormat.Rgba8888);
            using (var lockedBitmap = rgbImage.Lock())
            {
                // Copy pixel data from flipped image
                using (var memoryStream = new MemoryStream())
                {
                    flippedImage.Save(memoryStream);
                    memoryStream.Position = 0;

                    // Load as PNG and extract RGB data
                    // In Avalonia 11, Bitmap doesn't have Lock(), so we decode as WriteableBitmap
                    memoryStream.Position = 0;
                    var loadedBitmap = WriteableBitmap.Decode(memoryStream);
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

        /// <summary>
        /// Serializes the indoor map to a dictionary for JSON export.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method converts the indoor map to a dictionary structure that can be easily
        /// serialized to JSON. It includes all module settings and room data in a structured format.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Create Root Dictionary
        /// - module_id: Module identifier (string)
        /// - name: Module name as simplified string representation (not full localized string).
        ///   Uses LocalizedString.ToString() which returns the stringref if valid (>= 0),
        ///   otherwise returns the English male substring if available, or the first available
        ///   substring, or "-1" if none exist. For full localized string serialization with
        ///   all language/gender variants and stringref, use Write() method instead.
        /// - lighting: RGB color dictionary with "r", "g", "b" float values
        /// - skybox: Skybox name (string)
        /// - warp_point: Warp point coordinates dictionary with "x", "y", "z" float values
        ///
        /// STEP 2: Serialize Rooms
        /// - Call Serialize() on each room
        /// - Rooms are serialized as dictionaries with position, rotation, flip, component info
        ///
        /// NOTE: This is a simplified serialization intended for quick JSON export. For full
        /// serialization including walkmesh overrides and complete localized string data with
        /// all language/gender variants, use Write() method which produces JSON bytes with
        /// the complete data structure matching the .indoor file format specification.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1024-1042
        /// Original: def serialize(self) -> dict[str, Any]:
        /// </remarks>
        /// <returns>Dictionary containing serialized map data</returns>
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

        /// <summary>
        /// Updates bounding box min/max values to include a vertex.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method expands a bounding box to include a vertex. It updates the minimum and
        /// maximum coordinates to ensure the vertex is contained within the bounding box.
        ///
        /// HOW IT WORKS:
        ///
        /// For each axis (X, Y, Z):
        /// - If vertex coordinate is less than current minimum, update minimum
        /// - If vertex coordinate is greater than current maximum, update maximum
        ///
        /// This is used when calculating the bounding box of all walkmeshes for minimap generation.
        ///
        /// NOTE: The bbmin and bbmax parameters are passed by reference (ref/out in C#), so they
        /// are modified in place. In this implementation, Vector3 is a struct, so modifications
        /// don't persist unless the caller uses the returned values. However, this method is
        /// typically called in a loop where the bounding box is accumulated.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1044-1056
        /// Original: def _normalize_bwm_vertices(self, bbmin: Vector3, vertex: Vector3, bbmax: Vector3):
        /// </remarks>
        /// <param name="bbmin">Bounding box minimum (updated to include vertex)</param>
        /// <param name="vertex">Vertex to include in bounding box</param>
        /// <param name="bbmax">Bounding box maximum (updated to include vertex)</param>
        private void NormalizeBwmVertices(Vector3 bbmin, Vector3 vertex, Vector3 bbmax)
        {
            bbmin.X = Math.Min(bbmin.X, vertex.X);
            bbmin.Y = Math.Min(bbmin.Y, vertex.Y);
            bbmin.Z = Math.Min(bbmin.Z, vertex.Z);
            bbmax.X = Math.Max(bbmax.X, vertex.X);
            bbmax.Y = Math.Max(bbmax.Y, vertex.Y);
            bbmax.Z = Math.Max(bbmax.Z, vertex.Z);
        }

        // Helper method to deep copy a BWM (delegates to IndoorMapRoomHelper)
        public static BWM DeepCopyBwm(BWM original)
        {
            return IndoorMapRoomHelper.DeepCopyBwm(original);
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
        public Bitmap Image { get; set; }
        public Vector2 ImagePointMin { get; set; }
        public Vector2 ImagePointMax { get; set; }
        public Vector2 WorldPointMin { get; set; }
        public Vector2 WorldPointMax { get; set; }

        public MinimapData(Bitmap image, Vector2 imagePointMin, Vector2 imagePointMax, Vector2 worldPointMin, Vector2 worldPointMax)
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
            Vector3 position,
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
        public Vector3 Position { get; set; }
        public float Rotation { get; set; }
        public List<IndoorMapRoom> Hooks { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }
        public BWM WalkmeshOverride { get; set; }

        /// <summary>
        /// Calculates the world position of a hook (door connection point) with room transformations applied.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method calculates where a hook (door connection point) is located in world coordinates,
        /// taking into account the room's transformations (flip, rotation, position). This is used to:
        /// - Place doors at correct locations
        /// - Connect rooms at hook positions
        /// - Calculate door positions for the GIT file
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Get Hook Local Position
        /// - Start with hook's position (relative to component, centered at origin)
        ///
        /// STEP 2: Apply Flip Transformations
        /// - If room is flipped horizontally (FlipX), negate X coordinate
        /// - If room is flipped vertically (FlipY), negate Y coordinate
        /// - This mirrors the hook position
        ///
        /// STEP 3: Apply Rotation
        /// - Rotate the position around Z-axis by room's rotation angle
        /// - Uses rotation matrix:
        ///   * x' = x*cos(angle) - y*sin(angle)
        ///   * y' = x*sin(angle) + y*cos(angle)
        ///
        /// STEP 4: Apply World Offset (if requested)
        /// - If worldOffset is true, add room's position
        /// - This gives final world coordinates
        /// - If false, returns position relative to room center
        ///
        /// TRANSFORMATION ORDER:
        ///
        /// Transformations are applied in this order:
        /// 1. Flip (mirror)
        /// 2. Rotate
        /// 3. Translate (world offset)
        ///
        /// This matches how the walkmesh is transformed, ensuring hooks align with walkmesh edges.
        ///
        /// HOOK POSITIONING:
        ///
        /// Hooks are defined in component-local coordinates (centered at origin). When a component
        /// is placed in a room, the hook positions need to be transformed to match the room's
        /// placement. This method performs that transformation.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1076-1115
        /// Original: def hook_position(self, hook: KitComponentHook, *, world_offset: bool = True) -> Vector3:
        /// </remarks>
        /// <param name="hook">The hook whose position should be calculated</param>
        /// <param name="worldOffset">If true, add room position to get world coordinates. If false, return relative to room center.</param>
        /// <returns>The hook position in world coordinates (or room-relative if worldOffset is false)</returns>
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

        /// <summary>
        /// Rebuilds connections between this room and other rooms based on hook proximity.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method finds which other rooms this room is connected to by checking if their
        /// hooks are very close together (within 0.001 units). When two hooks are close, the
        /// rooms are considered connected, and doors can be placed between them.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Initialize Hooks List
        /// - Create new Hooks list with null entries for each component hook
        /// - This clears any existing connections
        ///
        /// STEP 2: Check Each Hook
        /// - For each hook in the component:
        ///   * Calculate hook's world position using HookPosition()
        ///   * Get hook index in component's hook list
        ///
        /// STEP 3: Find Matching Hooks in Other Rooms
        /// - For each other room in the map:
        ///   * For each hook in that room's component:
        ///     * Calculate other hook's world position
        ///     * Calculate distance between hooks
        ///     * If distance < 0.001 units, hooks are connected
        ///     * Set Hooks[hookIndex] = otherRoom to establish connection
        ///
        /// CONNECTION DETECTION:
        ///
        /// Two hooks are considered connected if their world positions are within 0.001 units.
        /// This tolerance accounts for floating-point precision errors. When rooms are placed
        /// and their hooks align, they should be exactly at the same position, but small
        /// rounding errors might occur.
        ///
        /// BIDIRECTIONAL CONNECTIONS:
        ///
        /// Connections are one-way in this method (this room -> other room). The other room's
        /// RebuildConnections() will establish the reverse connection. This ensures both rooms
        /// know they're connected to each other.
        ///
        /// WHEN IT'S CALLED:
        ///
        /// This method is called when:
        /// - Rooms are added, removed, or moved
        /// - Room positions, rotations, or flips are changed
        /// - RebuildRoomConnections() is called on the map
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1117-1151
        /// Original: def rebuild_connections(self, rooms: list[IndoorMapRoom]):
        /// </remarks>
        /// <param name="rooms">List of all rooms in the map (including this room)</param>
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

        /// <summary>
        /// Gets the room's walkmesh with all transformations applied.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method returns a transformed copy of the room's base walkmesh. The walkmesh is
        /// copied, then flipped, rotated, and translated to match the room's placement in the map.
        /// This is the walkmesh that will be used in the final module.
        ///
        /// HOW IT WORKS:
        ///
        /// STEP 1: Get Base Walkmesh
        /// - Call BaseWalkmesh() to get the original walkmesh (or override if set)
        ///
        /// STEP 2: Deep Copy
        /// - Create a deep copy so the original isn't modified
        ///
        /// STEP 3: Apply Transformations
        /// - Flip: Mirror walkmesh if room is flipped
        /// - Rotate: Rotate walkmesh by room's rotation angle
        /// - Translate: Move walkmesh to room's position
        ///
        /// TRANSFORMATION ORDER:
        ///
        /// Transformations are applied in this order:
        /// 1. Flip (mirror)
        /// 2. Rotate
        /// 3. Translate
        ///
        /// This matches how the model is transformed, ensuring walkmesh and model align.
        ///
        /// NOTE: This method does NOT set WalkmeshType to AreaModel. That is done in
        /// ProcessBwm() during the build process. This method is used for preview/editing.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1153-1174
        /// Original: def walkmesh(self) -> BWM:
        /// </remarks>
        /// <returns>A transformed copy of the room's walkmesh</returns>
        public BWM Walkmesh()
        {
            var bwm = IndoorMapRoomHelper.DeepCopyBwm(BaseWalkmesh());
            bwm.Flip(FlipX, FlipY);
            bwm.Rotate(Rotation);
            bwm.Translate(Position.X, Position.Y, Position.Z);
            return bwm;
        }

        /// <summary>
        /// Gets the base walkmesh for this room (either override or component's walkmesh).
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method returns the base walkmesh that should be used for this room. If the room
        /// has a walkmesh override (custom walkmesh edited by the user), it returns that. Otherwise,
        /// it returns the component's default walkmesh.
        ///
        /// HOW IT WORKS:
        ///
        /// - If WalkmeshOverride is not null, return it
        /// - Otherwise, return Component.Bwm (the component's default walkmesh)
        ///
        /// WALKMESH OVERRIDES:
        ///
        /// Users can edit walkmeshes in the indoor map builder. When a walkmesh is edited, it
        /// is stored in WalkmeshOverride. This allows custom walkmeshes without modifying the
        /// original component.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1176-1178
        /// Original: def base_walkmesh(self) -> BWM:
        /// </remarks>
        /// <returns>The base walkmesh (override if set, otherwise component's walkmesh)</returns>
        public BWM BaseWalkmesh()
        {
            return WalkmeshOverride ?? Component.Bwm;
        }

        /// <summary>
        /// Ensures the room has a walkmesh override, creating one if it doesn't exist.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method ensures that the room has a walkmesh override. If one doesn't exist, it
        /// creates a deep copy of the component's walkmesh and stores it as the override. This
        /// allows the walkmesh to be edited without affecting the original component.
        ///
        /// HOW IT WORKS:
        ///
        /// - If WalkmeshOverride is null:
        ///   * Create a deep copy of Component.Bwm
        ///   * Store it in WalkmeshOverride
        /// - Return WalkmeshOverride (now guaranteed to be non-null)
        ///
        /// WHY IT'S NEEDED:
        ///
        /// When editing a walkmesh in the indoor map builder, you need an independent copy so
        /// that editing doesn't affect the original component (which might be used by other rooms).
        /// This method creates that copy on-demand.
        ///
        /// DEEP COPYING:
        ///
        /// The walkmesh is deep copied using IndoorMapRoomHelper.DeepCopyBwm(), which ensures:
        /// - All faces are copied with materials preserved
        /// - All vertices are copied
        /// - All transitions are copied
        /// - The copy is completely independent
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1180-1184
        /// Original: def ensure_walkmesh_override(self) -> BWM:
        /// </remarks>
        /// <returns>The walkmesh override (created if it didn't exist)</returns>
        public BWM EnsureWalkmeshOverride()
        {
            if (WalkmeshOverride == null)
            {
                WalkmeshOverride = IndoorMapRoomHelper.DeepCopyBwm(Component.Bwm);
            }
            return WalkmeshOverride;
        }

        /// <summary>
        /// Clears the walkmesh override, reverting to the component's default walkmesh.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method removes the walkmesh override, causing the room to use the component's
        /// default walkmesh again. This is used when the user wants to discard custom walkmesh
        /// edits and revert to the original.
        ///
        /// HOW IT WORKS:
        ///
        /// Simply sets WalkmeshOverride to null. The next time BaseWalkmesh() is called, it will
        /// return Component.Bwm instead of the override.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1186-1188
        /// Original: def clear_walkmesh_override(self):
        /// </remarks>
        public void ClearWalkmeshOverride()
        {
            WalkmeshOverride = null;
        }

        /// <summary>
        /// Serializes the room to a dictionary for JSON export.
        /// </summary>
        /// <remarks>
        /// WHAT THIS METHOD DOES:
        ///
        /// This method converts the room data to a dictionary structure that can be serialized
        /// to JSON. It includes position, rotation, flip settings, and component information.
        ///
        /// HOW IT WORKS:
        ///
        /// Creates a dictionary with:
        /// - component_id: Hash code of component (for reference)
        /// - component_name: Name of the component
        /// - position: X, Y, Z coordinates dictionary
        /// - rotation: Rotation angle in degrees
        /// - flip_x: Whether room is flipped horizontally
        /// - flip_y: Whether room is flipped vertically
        /// - runtime_id: Hash code of room (for reference)
        ///
        // TODO: / NOTE: This is a simplified serialization. Walkmesh overrides are not included here.
        /// For full serialization including walkmesh overrides, the parent IndoorMap.Write()
        /// method handles that separately.
        ///
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/data/indoormap.py:1190-1205
        /// Original: def serialize(self) -> dict[str, Any]:
        /// </remarks>
        /// <returns>Dictionary containing serialized room data</returns>
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

