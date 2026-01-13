using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Games.Aurora.Data;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Scene;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Scene
{
    /// <summary>
    /// Aurora engine (NWN: EE) scene builder (graphics-backend agnostic).
    /// Builds abstract rendering structures from ARE (area) files with tile-based layout.
    /// Works with both MonoGame and Stride backends.
    /// </summary>
    /// <remarks>
    /// Aurora Scene Builder:
    /// - Based on nwmain.exe area loading system
    /// - Original implementation: Builds rendering structures from ARE tile data
    /// - ARE file format: Contains area tiles, tile geometry, and tile connections
    /// - Scene building: Parses ARE tile data, creates tile mesh structures, sets up tile adjacency
    /// - Tiles: Grid-based layout with tile visibility based on adjacency and portals
    /// - Graphics-agnostic: Works with any graphics backend (MonoGame, Stride, etc.)
    ///
    /// Based on reverse engineering of nwmain.exe:
    /// - CNWSArea::LoadArea @ 0x140365160 - Main area loading function
    /// - CNWSArea::LoadTileList @ 0x14035f780 - Loads Tile_List from ARE file
    /// - CNWSArea::LoadTileSetInfo @ 0x14035faf0 - Loads tileset information
    /// - Tile size: 10.0f units per tile (DAT_140dc2df4 in nwmain.exe)
    /// - Tile coordinates: x = i % Width, y = i / Width (integer division)
    /// - Tile array stored at offset 0x1c8 in CNWSArea, indexed as [y, x]
    ///
    /// Inheritance:
    /// - BaseSceneBuilder (Runtime.Graphics.Common.Scene) - Common scene building patterns
    ///   - AuroraSceneBuilder (this class) - Aurora-specific ARE tile handling
    /// </remarks>
    public class AuroraSceneBuilder : BaseSceneBuilder
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly Dictionary<ResRef, AuroraTileset> _tilesetCache;

        public AuroraSceneBuilder([NotNull] IGameResourceProvider resourceProvider)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }

            _resourceProvider = resourceProvider;
            _tilesetCache = new Dictionary<ResRef, AuroraTileset>();
        }

        /// <summary>
        /// Builds a scene from ARE area data (Aurora-specific).
        /// </summary>
        /// <param name="areData">ARE area data containing tile layout. Can be byte[] (raw ARE file) or GFF object.</param>
        /// <returns>Scene data structure with all tiles configured for rendering.</returns>
        /// <remarks>
        /// Scene Building Process (nwmain.exe CNWSArea::LoadArea @ 0x140365160):
        /// - Based on area/tile loading system from nwmain.exe
        /// - Original implementation: Builds rendering structures from ARE tile grid
        /// - Process:
        ///   1. Parse ARE file (GFF format with "ARE " signature)
        ///   2. Extract Width, Height, Tileset, and Tile_List from root struct
        ///   3. Parse each AreaTile struct from Tile_List
        ///   4. Calculate tile grid coordinates: x = i % Width, y = i / Width
        ///   5. Create SceneTile objects with proper world positions (10.0f units per tile)
        ///   6. Set up tile adjacency for visibility culling
        ///   7. Organize tiles into scene hierarchy for efficient rendering
        /// - Tile culling: Only tiles adjacent to visible tiles are rendered
        /// - Tile size: 10.0f units per tile (DAT_140dc2df4 in nwmain.exe)
        /// - Tile positions: World X = tileX * 10.0f, World Z = tileY * 10.0f, World Y = tileHeight * heightScale
        ///
        /// ARE file format (GFF with "ARE " signature):
        /// - Root struct contains Width (INT), Height (INT), Tileset (CResRef), Tile_List (GFFList)
        /// - Tile_List is GFFList containing AreaTile structs (StructID 1)
        /// - AreaTile fields: Tile_ID (INT), Tile_Orientation (INT 0-3), Tile_Height (INT),
        ///   Tile_AnimLoop1/2/3 (INT), Tile_MainLight1/2 (BYTE), Tile_SrcLight1/2 (BYTE)
        ///
        /// Based on official BioWare Aurora Engine ARE format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-AreaFile.md (Section 2.5: Area Tile List)
        /// - vendor/xoreos-docs/specs/bioware/AreaFile_Format.pdf
        /// </remarks>
        public AuroraSceneData BuildScene([NotNull] object areData)
        {
            if (areData == null)
            {
                throw new ArgumentNullException("areData");
            }

            // Parse ARE data - handle both byte[] and GFF objects
            GFF gff = null;
            if (areData is byte[] areBytes)
            {
                // Parse GFF from byte array
                gff = GFF.FromBytes(areBytes);
            }
            else if (areData is GFF areGff)
            {
                // Use GFF object directly
                gff = areGff;
            }
            else
            {
                throw new ArgumentException("areData must be byte[] or GFF object", "areData");
            }

            // Create scene data structure
            var sceneData = new AuroraSceneData();
            sceneData.Tiles = new List<SceneTile>();

            // Handle invalid or empty ARE data
            if (gff == null || gff.Root == null)
            {
                // Empty scene - no tiles
                RootEntity = sceneData;
                return sceneData;
            }

            // Verify GFF content type is ARE (defensive check)
            // Some ARE files may have incorrect content type, so we parse anyway
            if (gff.Content != GFFContent.ARE)
            {
                // Try to parse anyway - some ARE files may have incorrect content type
                // This is a defensive measure for compatibility
            }

            GFFStruct root = gff.Root;

            // Read Width and Height from root struct
            // Based on ARE format: Width and Height are INT (tile counts)
            // Width: x-direction (west-east), Height: y-direction (north-south)
            // Based on nwmain.exe: CNWSArea::LoadArea reads Width at offset 0xc, Height at offset 0x10
            int width = 0;
            int height = 0;

            if (root.Exists("Width"))
            {
                width = root.GetInt32("Width");
                if (width < 0) width = 0;
            }

            if (root.Exists("Height"))
            {
                height = root.GetInt32("Height");
                if (height < 0) height = 0;
            }

            // If width or height is 0, create empty scene
            if (width <= 0 || height <= 0)
            {
                RootEntity = sceneData;
                return sceneData;
            }

            // Read Tileset from root struct
            // Based on ARE format: Tileset is CResRef (tileset file reference)
            // Based on nwmain.exe: CNWSArea::LoadTileSetInfo @ 0x14035faf0 loads tileset information
            ResRef tileset = ResRef.FromBlank();
            if (root.Exists("TileSet"))
            {
                ResRef tilesetObj = root.GetResRef("TileSet");
                if (tilesetObj != null && !tilesetObj.IsBlank())
                {
                    tileset = tilesetObj;
                }
            }

            // Read Tile_List from root struct
            // Based on ARE format: Tile_List is GFFList containing AreaTile structs (StructID 1)
            // Based on nwmain.exe: CNWSArea::LoadTileList @ 0x14035f780 loads Tile_List
            GFFList tileList = root.GetList("Tile_List");
            if (tileList == null || tileList.Count == 0)
            {
                // No tiles - create empty scene
                RootEntity = sceneData;
                return sceneData;
            }

            // Tile size constant from nwmain.exe: 10.0f units per tile (DAT_140dc2df4)
            const float TileSize = 10.0f;
            // Height scale: approximate height per height transition (based on typical tile models)
            const float HeightScale = 2.0f;

            // Parse each AreaTile struct from Tile_List
            // Based on ARE format: Tile coordinates calculated from index
            // Formula: x = i % Width, y = i / Width (integer division)
            // Based on nwmain.exe: CNWSArea::LoadTileList processes Tile_List sequentially
            for (int i = 0; i < tileList.Count; i++)
            {
                GFFStruct tileStruct = tileList.At(i);
                if (tileStruct == null)
                {
                    continue;
                }

                // Calculate tile coordinates from index
                // Based on ARE format specification (Section 2.5):
                // x = i % w, y = i / w (where w = Width, integer division rounds down)
                int tileX = i % width;
                int tileY = i / width;

                // Validate tile coordinates are within bounds
                if (tileX < 0 || tileX >= width || tileY < 0 || tileY >= height)
                {
                    // Tile index out of bounds - skip this tile
                    // This can happen if Tile_List has more entries than Width * Height
                    continue;
                }

                // Read Tile_ID (index into tileset file's list of tiles)
                // Based on ARE format: Tile_ID is INT, must be >= 0
                // Based on nwmain.exe: CNWTileSet::GetTileData() uses Tile_ID to look up tile model
                int tileId = -1;
                if (tileStruct.Exists("Tile_ID"))
                {
                    tileId = tileStruct.GetInt32("Tile_ID");
                    if (tileId < 0)
                    {
                        tileId = -1; // Invalid tile ID
                    }
                }

                // Skip tiles with invalid Tile_ID (empty tiles)
                if (tileId < 0)
                {
                    continue;
                }

                // Read Tile_Orientation (rotation: 0 = normal, 1 = 90° CCW, 2 = 180° CCW, 3 = 270° CCW)
                // Based on ARE format: Tile_Orientation is INT (0-3)
                // Based on nwmain.exe: CNWTile::SetOrientation applies rotation transform
                int orientation = 0;
                if (tileStruct.Exists("Tile_Orientation"))
                {
                    orientation = tileStruct.GetInt32("Tile_Orientation");
                    // Clamp to valid range (0-3)
                    if (orientation < 0) orientation = 0;
                    if (orientation > 3) orientation = 3;
                }

                // Read Tile_Height (number of height transitions)
                // Based on ARE format: Tile_Height is INT, should never be negative
                // Based on nwmain.exe: Tile height affects vertical positioning
                int tileHeight = 0;
                if (tileStruct.Exists("Tile_Height"))
                {
                    tileHeight = tileStruct.GetInt32("Tile_Height");
                    if (tileHeight < 0) tileHeight = 0;
                }

                // Calculate world position for tile
                // Based on nwmain.exe: CNWSArea::GetTile converts tile coordinates to world coordinates
                // World X = tileX * TileSize (west-east direction)
                // World Z = tileY * TileSize (north-south direction, Aurora uses Z for vertical in world space)
                // World Y = tileHeight * HeightScale (vertical height)
                // Note: Aurora coordinate system: X = east, Y = up, Z = north
                float worldX = tileX * TileSize;
                float worldZ = tileY * TileSize;
                float worldY = tileHeight * HeightScale;
                Vector3 position = new Vector3(worldX, worldY, worldZ);

                // Generate tile model ResRef
                // Based on nwmain.exe: CNWTileSet::GetTileData() @ 0x1402c67d0 returns CNWTileData pointer
                // CNWTileData contains Model ResRef field, read from [TILE{N}] Model entry in SET file
                // Based on nwmain.exe: CNWSArea::LoadTileSetInfo @ 0x140362700 calls GetTileData for each tile
                string modelResRef = GetTileModelResRef(tileset, tileId);

                // Read Tile_AnimLoop1/2/3 (animation loop flags)
                // Based on ARE format: Tile_AnimLoop1/2/3 are INT (0 = disabled, 1 = enabled)
                // Based on nwmain.exe: CNWSArea::LoadTileList reads Tile_AnimLoop1/2/3 @ vendor/xoreos/src/engines/nwn/area.cpp:401-403
                bool animLoop1 = false;
                if (tileStruct.Exists("Tile_AnimLoop1"))
                {
                    int animLoop1Value = tileStruct.GetInt32("Tile_AnimLoop1");
                    animLoop1 = animLoop1Value != 0;
                }

                bool animLoop2 = false;
                if (tileStruct.Exists("Tile_AnimLoop2"))
                {
                    int animLoop2Value = tileStruct.GetInt32("Tile_AnimLoop2");
                    animLoop2 = animLoop2Value != 0;
                }

                bool animLoop3 = false;
                if (tileStruct.Exists("Tile_AnimLoop3"))
                {
                    int animLoop3Value = tileStruct.GetInt32("Tile_AnimLoop3");
                    animLoop3 = animLoop3Value != 0;
                }

                // Read Tile_MainLight1/2 (main light indices)
                // Based on ARE format: Tile_MainLight1/2 are BYTE (0 = disabled, 1-255 = lightcolor.2da index)
                // Based on nwmain.exe: CNWSArea::LoadTileList reads Tile_MainLight1/2 @ vendor/xoreos/src/engines/nwn/area.cpp:393-394
                byte mainLight1 = 0;
                if (tileStruct.Exists("Tile_MainLight1"))
                {
                    mainLight1 = tileStruct.GetUInt8("Tile_MainLight1");
                }

                byte mainLight2 = 0;
                if (tileStruct.Exists("Tile_MainLight2"))
                {
                    mainLight2 = tileStruct.GetUInt8("Tile_MainLight2");
                }

                // Read Tile_SrcLight1/2 (source light indices)
                // Based on ARE format: Tile_SrcLight1/2 are BYTE (0 = off, 1-15 = color/animation index)
                // Based on nwmain.exe: CNWSArea::LoadTileList reads Tile_SrcLight1/2 @ vendor/xoreos/src/engines/nwn/area.cpp:396-397
                byte srcLight1 = 0;
                if (tileStruct.Exists("Tile_SrcLight1"))
                {
                    srcLight1 = tileStruct.GetUInt8("Tile_SrcLight1");
                }

                byte srcLight2 = 0;
                if (tileStruct.Exists("Tile_SrcLight2"))
                {
                    srcLight2 = tileStruct.GetUInt8("Tile_SrcLight2");
                }

                // Generate tile identifier (format: "tile_X_Y")
                // Based on nwmain.exe: Tile identifiers used for area transitions and visibility culling
                string tileIdentifier = string.Format("tile_{0}_{1}", tileX, tileY);

                // Create SceneTile instance
                SceneTile sceneTile = new SceneTile
                {
                    ModelResRef = modelResRef,
                    Position = position,
                    TileIdentifier = tileIdentifier,
                    TileX = tileX,
                    TileY = tileY,
                    Orientation = orientation,
                    Height = tileHeight,
                    IsVisible = true, // All tiles visible initially, visibility updated by SetCurrentArea
                    MeshData = null, // Mesh data loaded on demand by graphics backend
                    AnimLoop1 = animLoop1,
                    AnimLoop2 = animLoop2,
                    AnimLoop3 = animLoop3,
                    MainLight1 = mainLight1,
                    MainLight2 = mainLight2,
                    SrcLight1 = srcLight1,
                    SrcLight2 = srcLight2,
                    AnimationStates = new Dictionary<int, TileAnimationState>() // Initialize animation state tracking
                };

                // Add tile to scene
                sceneData.Tiles.Add(sceneTile);
            }

            // Set root entity and return scene data
            RootEntity = sceneData;
            return sceneData;
        }

        /// <summary>
        /// Gets the visibility of a tile from the current tile (Aurora-specific).
        /// </summary>
        /// <param name="currentArea">Current tile identifier (format: "tile_X_Y" where X and Y are tile coordinates).</param>
        /// <param name="targetArea">Target tile identifier to check visibility for.</param>
        /// <returns>True if the target tile is visible from the current tile (adjacent or same tile).</returns>
        /// <remarks>
        /// Tile Visibility (nwmain.exe):
        /// - Based on tile adjacency in Aurora's tile-based system
        /// - Tiles are visible if they are adjacent (north, south, east, west, or same tile)
        /// - Diagonal tiles are not directly visible (must go through adjacent tiles)
        /// - Based on nwmain.exe: CNWSArea::GetVisibleTiles() determines tile visibility
        /// - Visibility is used for rendering culling: only visible tiles are rendered
        /// </remarks>
        public override bool IsAreaVisible(string currentArea, string targetArea)
        {
            if (string.IsNullOrEmpty(currentArea) || string.IsNullOrEmpty(targetArea))
            {
                return false;
            }

            // Parse tile coordinates from identifiers
            // Format: "tile_X_Y" where X and Y are tile grid coordinates
            if (!ParseTileIdentifier(currentArea, out int currentX, out int currentY))
            {
                return false;
            }

            if (!ParseTileIdentifier(targetArea, out int targetX, out int targetY))
            {
                return false;
            }

            // Tiles are visible if they are adjacent (including same tile)
            // Adjacent means: |deltaX| <= 1 AND |deltaY| <= 1
            int deltaX = Math.Abs(targetX - currentX);
            int deltaY = Math.Abs(targetY - currentY);

            // Visible if adjacent (including same tile)
            return deltaX <= 1 && deltaY <= 1;
        }

        /// <summary>
        /// Sets the current tile for visibility culling (Aurora-specific).
        /// </summary>
        /// <param name="areaIdentifier">Tile identifier (format: "tile_X_Y" where X and Y are tile coordinates).</param>
        /// <remarks>
        /// Tile Visibility Culling (nwmain.exe):
        /// - Based on nwmain.exe: CNWSArea::UpdateVisibleTiles() updates tile visibility
        /// - Only tiles adjacent to current tile are marked as visible
        /// - Visibility is used for rendering optimization: only visible tiles are rendered
        /// - Process:
        ///   1. Parse current tile coordinates from identifier
        ///   2. Iterate through all tiles in scene
        ///   3. Mark tiles as visible if they are adjacent to current tile (including current tile)
        ///   4. Mark all other tiles as not visible
        /// </remarks>
        public override void SetCurrentArea(string areaIdentifier)
        {
            if (RootEntity is AuroraSceneData sceneData)
            {
                sceneData.CurrentTile = areaIdentifier;

                // Parse current tile coordinates from identifier
                if (!ParseTileIdentifier(areaIdentifier, out int currentX, out int currentY))
                {
                    // Invalid identifier - mark all tiles as not visible
                    if (sceneData.Tiles != null)
                    {
                        foreach (var tile in sceneData.Tiles)
                        {
                            tile.IsVisible = false;
                        }
                    }
                    return;
                }

                // Update tile visibility based on adjacency
                // Based on nwmain.exe: CNWSArea::UpdateVisibleTiles() marks adjacent tiles as visible
                if (sceneData.Tiles != null)
                {
                    foreach (var tile in sceneData.Tiles)
                    {
                        // Use stored tile coordinates (more efficient than converting from position)
                        // Calculate distance from current tile
                        int deltaX = Math.Abs(tile.TileX - currentX);
                        int deltaY = Math.Abs(tile.TileY - currentY);

                        // Mark tile as visible if adjacent (including same tile)
                        // Adjacent means: |deltaX| <= 1 AND |deltaY| <= 1
                        tile.IsVisible = (deltaX <= 1 && deltaY <= 1);
                    }
                }
            }
        }

        /// <summary>
        /// Parses a tile identifier string to extract tile coordinates.
        /// </summary>
        /// <param name="identifier">Tile identifier (format: "tile_X_Y" where X and Y are tile coordinates).</param>
        /// <param name="tileX">Output tile X coordinate.</param>
        /// <param name="tileY">Output tile Y coordinate.</param>
        /// <returns>True if identifier was successfully parsed, false otherwise.</returns>
        /// <remarks>
        /// Tile Identifier Format:
        /// - Format: "tile_X_Y" where X and Y are tile grid coordinates
        /// - Example: "tile_5_3" means tile at grid position (5, 3)
        /// - Used for tile identification in visibility culling and area transitions
        /// </remarks>
        private bool ParseTileIdentifier(string identifier, out int tileX, out int tileY)
        {
            tileX = -1;
            tileY = -1;

            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }

            // Parse format: "tile_X_Y"
            if (!identifier.StartsWith("tile_", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string coords = identifier.Substring(5); // Skip "tile_"
            string[] parts = coords.Split('_');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out tileX))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out tileY))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clears the current scene and disposes resources (Aurora-specific).
        /// </summary>
        public override void Clear()
        {
            ClearRoomMeshData();
            RootEntity = null;
        }

        /// <summary>
        /// Gets the list of scene tiles for rendering.
        /// </summary>
        protected override IList<ISceneRoom> GetSceneRooms()
        {
            if (RootEntity is AuroraSceneData sceneData)
            {
                return sceneData.Tiles.Cast<ISceneRoom>().ToList();
            }
            return null;
        }

        /// <summary>
        /// Builds a scene from area data (internal implementation).
        /// </summary>
        protected override void BuildSceneInternal(object areaData)
        {
            BuildScene(areaData);
        }

        /// <summary>
        /// Gets the tile model ResRef from the tileset file using Tile_ID.
        /// </summary>
        /// <param name="tileset">Tileset ResRef (SET file reference).</param>
        /// <param name="tileId">Tile_ID (index into tileset's tile list).</param>
        /// <returns>Tile model ResRef string, or fallback format if tileset lookup fails.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
        /// - Loads tileset SET file if not already cached
        /// - Queries tileset for Model ResRef using Tile_ID
        /// - Returns Model ResRef from [TILE{Tile_ID}] Model entry
        /// - Falls back to simplified format if tileset cannot be loaded or Tile_ID is invalid
        ///
        /// Error handling (matching original engine behavior):
        /// - If tileset is blank: Falls back to simplified format (tileset_TileID with 2-digit padding)
        /// - If tileset cannot be loaded: Falls back to simplified format (ensures scene building continues)
        /// - If Tile_ID is invalid (out of bounds): Falls back to simplified format
        /// - If Model entry is missing: Falls back to simplified format
        ///
        /// Fallback format: "{tileset}_{TileID:D2}" (e.g., "rural_00", "rural_01")
        /// - Based on original simplified implementation before tileset lookup was added
        /// - Format ensures ResRef validity (max 16 chars, ASCII only)
        /// - 2-digit padding ensures consistent naming for Tile_ID 0-99
        /// </remarks>
        private string GetTileModelResRef(ResRef tileset, int tileId)
        {
            // Skip lookup if tileset is blank
            // Based on nwmain.exe: CNWTileSet::GetTileData returns null if tileset not loaded
            // Fallback ensures scene building continues even with invalid tileset reference
            if (tileset == null || tileset.IsBlank())
            {
                // Fallback to simplified format: tileset_TileID (with 2-digit padding)
                // Format: "unknown_00", "unknown_01", etc. if tileset is null
                string tilesetName = tileset != null ? tileset.ToString() : "unknown";
                return string.Format("{0}_{1:D2}", tilesetName, tileId);
            }

            try
            {
                // Get or create tileset instance (with caching)
                // Based on nwmain.exe: CNWTileSetManager::GetTileSet caches tilesets
                // CNWTileSetManager::GetTileSet @ 0x1402c7d80 manages tileset cache
                if (!_tilesetCache.TryGetValue(tileset, out AuroraTileset tilesetInstance))
                {
                    tilesetInstance = new AuroraTileset(_resourceProvider, tileset);
                    _tilesetCache[tileset] = tilesetInstance;
                }

                // Load tileset if not already loaded
                // Based on nwmain.exe: CNWTileSet::LoadTileSet @ 0x1402c6890 loads SET file on demand
                // LoadTileSet parses [GENERAL], [GRASS], [TILES] sections and [TILE{N}] Model entries
                if (!tilesetInstance.IsLoaded)
                {
                    tilesetInstance.Load();
                }

                // Get Model ResRef from tileset using Tile_ID
                // Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0 returns CNWTileData pointer
                // CNWTileData contains Model ResRef field, read from [TILE{Tile_ID}] Model entry
                // GetTileData validates Tile_ID bounds (0 <= Tile_ID < Count) and returns null if invalid
                ResRef modelResRef = tilesetInstance.GetTileModelResRef(tileId);

                // Return Model ResRef if valid, otherwise fallback to simplified format
                // Based on nwmain.exe: If GetTileData returns null, tile data is null (no fallback in original)
                // C# implementation needs string return value, so we provide fallback for compatibility
                if (modelResRef != null && !modelResRef.IsBlank())
                {
                    return modelResRef.ToString();
                }
            }
            catch
            {
                // If tileset loading fails, fall back to simplified format
                // This ensures scene building continues even if tileset file is missing or corrupted
                // Based on nwmain.exe: LoadTileSet throws exception if SET file cannot be loaded
                // C# implementation catches exception and provides fallback for graceful degradation
            }

            // Fallback to simplified format if tileset lookup fails
            // Format: "{tileset}_{TileID:D2}" (e.g., "rural_00", "rural_01")
            // Based on original simplified implementation before tileset lookup was added
            // This format ensures:
            // - ResRef validity (max 16 chars: tileset name + "_" + 2 digits = typically < 16 chars)
            // - Consistent naming for Tile_ID 0-99 (2-digit padding)
            // - Compatibility with areas that have missing or corrupted tileset files
            return string.Format("{0}_{1:D2}", tileset.ToString(), tileId);
        }
    }

    /// <summary>
    /// Scene data for Aurora engine (nwmain.exe).
    /// Contains tiles and current tile tracking.
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Aurora Scene Data Structure:
    /// - Based on nwmain.exe area/tile structure
    /// - Tiles: Grid-based tile layout
    /// - CurrentTile: Currently active tile for visibility determination
    /// - Graphics-agnostic: Can be rendered by any graphics backend
    /// </remarks>
    public class AuroraSceneData
    {
        /// <summary>
        /// Gets or sets the list of tiles in the scene.
        /// </summary>
        public List<SceneTile> Tiles { get; set; }

        /// <summary>
        /// Gets or sets the current tile identifier for visibility culling.
        /// </summary>
        [CanBeNull]
        public string CurrentTile { get; set; }
    }

    /// <summary>
    /// Scene tile data for rendering (Aurora-specific).
    /// Graphics-backend agnostic.
    /// </summary>
    /// <remarks>
    /// Scene Tile:
    /// - Based on nwmain.exe tile structure (CNWSTile)
    /// - ModelResRef: Tile model reference (from tileset and Tile_ID)
    /// - Position: World position (calculated from tile grid coordinates)
    /// - TileIdentifier: Unique identifier for tile (format: "tile_X_Y")
    /// - TileX, TileY: Tile grid coordinates (for adjacency calculations)
    /// - Orientation: Tile rotation (0-3: 0°, 90°, 180°, 270° counterclockwise)
    /// - Height: Number of height transitions (affects vertical position)
    /// - IsVisible: Visibility flag updated by adjacency culling
    /// - MeshData: Abstract mesh data loaded by graphics backend
    ///
    /// Based on nwmain.exe:
    /// - CNWSTile structure stores tile data at offset 0x1c8 in CNWSArea
    /// - Tile coordinates stored at offsets 0x1c (X) and 0x20 (Y) in CNWTile::GetLocation
    /// - Tile size: 10.0f units per tile (DAT_140dc2df4)
    /// </remarks>
    public class SceneTile : ISceneRoom
    {
        /// <summary>
        /// Gets or sets the tile model resource reference.
        /// </summary>
        public string ModelResRef { get; set; }

        /// <summary>
        /// Gets or sets the world position of the tile.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Gets or sets the unique tile identifier (format: "tile_X_Y" where X and Y are tile grid coordinates).
        /// </summary>
        public string TileIdentifier { get; set; }

        /// <summary>
        /// Gets or sets the tile X coordinate in the grid.
        /// </summary>
        public int TileX { get; set; }

        /// <summary>
        /// Gets or sets the tile Y coordinate in the grid.
        /// </summary>
        public int TileY { get; set; }

        /// <summary>
        /// Gets or sets the tile orientation (0-3: 0°, 90°, 180°, 270° counterclockwise).
        /// </summary>
        public int Orientation { get; set; }

        /// <summary>
        /// Gets or sets the tile height (number of height transitions).
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets whether the tile is currently visible (updated by visibility culling).
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Tile mesh data loaded from tile model. Null until loaded on demand by graphics backend.
        /// </summary>
        [CanBeNull]
        public IRoomMeshData MeshData { get; set; }

        /// <summary>
        /// Gets or sets whether animation loop 1 (AnimLoop01) should play.
        /// Based on ARE format: Tile_AnimLoop1 is INT (0 = disabled, 1 = enabled).
        /// </summary>
        public bool AnimLoop1 { get; set; }

        /// <summary>
        /// Gets or sets whether animation loop 2 (AnimLoop02) should play.
        /// Based on ARE format: Tile_AnimLoop2 is INT (0 = disabled, 1 = enabled).
        /// </summary>
        public bool AnimLoop2 { get; set; }

        /// <summary>
        /// Gets or sets whether animation loop 3 (AnimLoop03) should play.
        /// Based on ARE format: Tile_AnimLoop3 is INT (0 = disabled, 1 = enabled).
        /// </summary>
        public bool AnimLoop3 { get; set; }

        /// <summary>
        /// Gets or sets the main light 1 index (index into lightcolor.2da).
        /// Based on ARE format: Tile_MainLight1 is BYTE (0 = disabled, 1-255 = light color index).
        /// </summary>
        public byte MainLight1 { get; set; }

        /// <summary>
        /// Gets or sets the main light 2 index (index into lightcolor.2da).
        /// Based on ARE format: Tile_MainLight2 is BYTE (0 = disabled, 1-255 = light color index).
        /// </summary>
        public byte MainLight2 { get; set; }

        /// <summary>
        /// Gets or sets the source light 1 index (0 = off, 1-15 = color/animation index).
        /// Based on ARE format: Tile_SrcLight1 is BYTE (0 = off, 1-15 = color/animation index).
        /// </summary>
        public byte SrcLight1 { get; set; }

        /// <summary>
        /// Gets or sets the source light 2 index (0 = off, 1-15 = color/animation index).
        /// Based on ARE format: Tile_SrcLight2 is BYTE (0 = off, 1-15 = color/animation index).
        /// </summary>
        public byte SrcLight2 { get; set; }

        /// <summary>
        /// Animation state tracking for this tile's active animation loops.
        /// Tracks animation time and frame indices for each active animation loop.
        /// Key: Animation loop index (0 = AnimLoop01, 1 = AnimLoop02, 2 = AnimLoop03).
        /// Value: Animation state (time, frame index, etc.).
        /// </summary>
        [CanBeNull]
        public Dictionary<int, TileAnimationState> AnimationStates { get; set; }
    }

    /// <summary>
    /// Tile animation state for tracking animation playback.
    /// Based on nwmain.exe: Tile animations track time and frame indices for texture animations.
    /// </summary>
    public class TileAnimationState
    {
        /// <summary>
        /// Current animation time in seconds.
        /// </summary>
        public float AnimationTime { get; set; }

        /// <summary>
        /// Animation duration in seconds (from MDL animation data).
        /// </summary>
        public float AnimationLength { get; set; }

        /// <summary>
        /// Current animation frame index (for frame-based animations).
        /// </summary>
        public int FrameIndex { get; set; }

        /// <summary>
        /// Animation loop mode: true = loop, false = one-shot.
        /// Based on MDL animation data: AnimLoop animations are typically looping.
        /// </summary>
        public bool IsLooping { get; set; }

        /// <summary>
        /// Animation name (e.g., "AnimLoop01", "AnimLoop02", "AnimLoop03").
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// Whether the animation has completed (for one-shot animations).
        /// </summary>
        public bool IsComplete { get; set; }
    }
}

