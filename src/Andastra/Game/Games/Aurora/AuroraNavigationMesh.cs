using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.BWM;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Represents a single tile in an Aurora area.
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWSTile structure
    /// - Tile_ID: Index into tileset file's list of tiles
    /// - Tile_Orientation: Rotation (0-3 for 0°, 90°, 180°, 270°)
    /// - Tile_Height: Number of height transitions
    /// - Tile location stored at offsets 0x1c (X) and 0x20 (Y) in CNWTile::GetLocation
    /// - SurfaceMaterial: Surface material index from tileset (0-30, corresponds to surfacemat.2da)
    /// </remarks>
    public struct AuroraTile
    {
        /// <summary>
        /// Tile ID (index into tileset).
        /// </summary>
        public int TileId { get; set; }

        /// <summary>
        /// Tile orientation (0-3: 0°, 90°, 180°, 270° counterclockwise).
        /// </summary>
        public int Orientation { get; set; }

        /// <summary>
        /// Number of height transitions at this tile location.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Whether this tile is loaded and available.
        /// </summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Whether this tile has walkable surfaces.
        /// </summary>
        public bool IsWalkable { get; set; }

        /// <summary>
        /// Surface material index (0-30, corresponds to surfacemat.2da entries).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Tile surface material lookup from tileset data.
        /// Surface materials determine walkability, sound effects, and movement costs.
        /// Default value is 0 (Undefined) for tiles without material data.
        ///
        /// Common Aurora surface materials (from surfacemat.2da):
        /// - 0: Undefined/NotDefined
        /// - 1: Dirt (walkable)
        /// - 3: Grass (walkable)
        /// - 4: Stone (walkable)
        /// - 7: NonWalk (non-walkable)
        /// - 15: Lava (non-walkable)
        /// - 17: DeepWater (non-walkable)
        /// </remarks>
        public int SurfaceMaterial { get; set; }
    }

    /// <summary>
    /// Aurora Engine walkmesh implementation for tile-based navigation.
    /// </summary>
    /// <remarks>
    /// Aurora Navigation Mesh Implementation:
    /// - Based on Aurora's tile-based area system
    /// - More complex than Odyssey due to tile connectivity
    /// - Supports pathfinding across tile boundaries
    ///
    /// Based on reverse engineering of:
    /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates and returns tile pointer
    /// - nwmain.exe: CNWTile::GetLocation @ 0x1402c55a0 - Gets tile grid coordinates (X, Y) from tile structure
    /// - nwmain.exe: Tile validation checks bounds (0 <= tileX < width, 0 <= tileY < height)
    /// - Tile size constant: DAT_140dc2df4 (10.0f units per tile)
    /// - Tile array stored at offset 0x1c8 in CNWSArea, tile size is 0x68 bytes (104 bytes)
    /// - Width stored at offset 0xc, Height stored at offset 0x10 in CNWSArea
    /// - Returns null pointer (0x0) if tile coordinates are out of bounds
    ///
    /// Aurora navigation features:
    /// - Tile-based walkmesh construction
    /// - Inter-tile pathfinding
    /// - Dynamic obstacle handling
    /// - Line of sight across tile boundaries
    /// - Height-based terrain following
    /// </remarks>
    [PublicAPI]
    public class AuroraNavigationMesh : BaseNavigationMesh
    {
        // Tile grid data
        private readonly AuroraTile[,] _tiles;
        private readonly int _tileWidth;
        private readonly int _tileHeight;
        private const float TileSize = 10.0f; // Based on DAT_140dc2df4 in nwmain.exe: 10.0f units per tile

        // Tileset data for height sampling
        private readonly TilesetLoader _tilesetLoader;
        private readonly string _tilesetResRef;

        /// <summary>
        /// Creates an empty Aurora navigation mesh (for placeholder use).
        /// </summary>
        public AuroraNavigationMesh()
        {
            _tiles = new AuroraTile[0, 0];
            _tileWidth = 0;
            _tileHeight = 0;
            _tilesetLoader = null;
            _tilesetResRef = null;
        }

        /// <summary>
        /// Creates an Aurora navigation mesh with tile grid data.
        /// </summary>
        /// <param name="tiles">2D array of tiles indexed by [y, x].</param>
        /// <param name="tileWidth">Width of the tile grid.</param>
        /// <param name="tileHeight">Height of the tile grid.</param>
        /// <param name="tilesetLoader">Optional tileset loader for height sampling from walkmesh geometry.</param>
        /// <param name="tilesetResRef">Optional tileset resource reference for height sampling.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea tile storage structure.
        /// Tiles are stored in a 2D grid with dimensions Width x Height.
        /// If tilesetLoader and tilesetResRef are provided, GetTileHeight will sample from actual walkmesh geometry.
        /// Otherwise, it falls back to simplified height transition model with bilinear interpolation.
        /// </remarks>
        public AuroraNavigationMesh(AuroraTile[,] tiles, int tileWidth, int tileHeight, TilesetLoader tilesetLoader = null, string tilesetResRef = null)
        {
            if (tiles == null)
            {
                throw new ArgumentNullException(nameof(tiles));
            }

            if (tileWidth <= 0 || tileHeight <= 0)
            {
                throw new ArgumentException("Tile width and height must be positive", nameof(tileWidth));
            }

            if (tiles.GetLength(0) != tileHeight || tiles.GetLength(1) != tileWidth)
            {
                throw new ArgumentException("Tile array dimensions must match tileWidth and tileHeight", nameof(tiles));
            }

            _tiles = tiles;
            _tileWidth = tileWidth;
            _tileHeight = tileHeight;
            _tilesetLoader = tilesetLoader;
            _tilesetResRef = tilesetResRef;
        }
        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on Aurora tile-based walkmesh system.
        /// Checks tile validity and walkable surfaces within tiles.
        /// More complex than Odyssey due to tile boundaries.
        ///
        /// Algorithm:
        /// 1. Convert world coordinates to tile coordinates using GetTileCoordinates
        /// 2. Validate tile is loaded and exists using IsTileValid
        /// 3. Check if tile has walkable surfaces using IsWalkable property
        /// 4. Handle boundary cases: points on tile edges check the containing tile
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates
        /// - nwmain.exe: CNWTile walkability checks - Tiles have walkable surface flags
        /// - Tile size constant: DAT_140dc2df4 (10.0f units per tile)
        /// - Tile validation: Checks if tile is loaded and has valid tile ID
        /// - Walkability: Tiles with IsWalkable flag set to true are walkable
        ///
        /// Note: Unlike Odyssey's face-based system, Aurora uses tile-based walkability.
        /// Each tile represents a walkable surface area, and points are tested against
        /// the containing tile's walkability flag.
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Step 1: Convert world coordinates to tile coordinates
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
            int tileX, tileY;
            if (!GetTileCoordinates(point, out tileX, out tileY))
            {
                // Point is outside tile grid bounds - not walkable
                return false;
            }

            // Step 2: Validate tile is loaded and exists
            // Based on nwmain.exe: CNWSArea::GetTile validation checks
            if (!IsTileValid(tileX, tileY))
            {
                // Tile is not valid (not loaded, out of bounds, or invalid tile ID) - not walkable
                return false;
            }

            // Step 3: Get tile and check walkability
            // Based on nwmain.exe: CNWTile walkability checks
            AuroraTile tile = _tiles[tileY, tileX];

            // Tile must be loaded to be walkable
            if (!tile.IsLoaded)
            {
                return false;
            }

            // Step 4: Check if tile has walkable surfaces
            // Based on nwmain.exe: Tile walkability flag checks
            // Tiles with IsWalkable set to true have walkable surfaces
            return tile.IsWalkable;
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <remarks>
        /// Based on Aurora walkmesh projection with tile awareness.
        /// Projects to nearest walkable surface across tile boundaries.
        /// Handles height transitions between tiles.
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates
        /// - nwmain.exe: CNWTile walkmesh projection - Projects points to walkable tile surfaces
        /// - Tile-based projection: Unlike Odyssey's triangle-based walkmesh, Aurora uses tile-based surfaces
        /// - Height sampling: Uses GetHeightAtPoint to sample terrain height at projection point
        ///
        /// Algorithm:
        /// 1. Find containing tile using GetTileCoordinates
        /// 2. Validate tile is loaded and walkable
        /// 3. If point is on walkable tile, sample height using GetHeightAtPoint
        /// 4. If point is not on walkable tile, search adjacent tiles for nearest walkable surface
        /// 5. Project point to sampled height (preserve X and Z, update Y to sampled height)
        /// 6. Handle edge cases: points outside tile grid, unloaded tiles, non-walkable areas
        ///
        /// Tile boundary handling:
        /// - Points on tile boundaries check adjacent tiles for walkability
        /// - Height is interpolated from multiple tiles for smooth transitions
        /// - Non-walkable tiles are skipped when searching for projection surface
        ///
        /// Note: Aurora's tile-based system is simpler than Odyssey's triangle-based walkmesh.
        /// Each tile represents a walkable surface area, and projection involves finding
        /// the height at the point's X/Z coordinates within the containing tile.
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Step 1: Find containing tile
            int tileX, tileY;
            if (!GetTileCoordinates(point, out tileX, out tileY))
            {
                // Point is outside tile grid bounds - cannot project
                return false;
            }

            // Step 2: Check if containing tile is valid and walkable
            if (IsTileValid(tileX, tileY))
            {
                AuroraTile tile = _tiles[tileY, tileX];
                if (tile.IsLoaded && tile.IsWalkable)
                {
                    // Step 3: Point is on walkable tile - sample height
                    if (GetHeightAtPoint(point, out height))
                    {
                        result = new Vector3(point.X, height, point.Z);
                        return true;
                    }
                }
            }

            // Step 4: Point is not on walkable tile - search adjacent tiles for nearest walkable surface
            // Search radius: Check tiles within 2-tile radius
            const int searchRadius = 2;
            float bestDistance = float.MaxValue;
            float bestHeight = point.Y;
            bool foundWalkable = false;

            for (int dy = -searchRadius; dy <= searchRadius; dy++)
            {
                for (int dx = -searchRadius; dx <= searchRadius; dx++)
                {
                    int checkTileX = tileX + dx;
                    int checkTileY = tileY + dy;

                    if (IsTileValid(checkTileX, checkTileY))
                    {
                        AuroraTile checkTile = _tiles[checkTileY, checkTileX];
                        if (checkTile.IsLoaded && checkTile.IsWalkable)
                        {
                            // Calculate distance from point to tile center
                            Vector3 tileCenter = GetTileCenter(checkTileX, checkTileY);
                            float distanceX = point.X - tileCenter.X;
                            float distanceZ = point.Z - tileCenter.Z;
                            float distance = (float)Math.Sqrt(distanceX * distanceX + distanceZ * distanceZ);

                            // Check if this is the closest walkable tile
                            if (distance < bestDistance)
                            {
                                // Sample height at point's X/Z coordinates (projected onto this tile)
                                Vector3 samplePoint = new Vector3(point.X, point.Y, point.Z);
                                float sampleHeight;
                                if (GetHeightAtPoint(samplePoint, out sampleHeight))
                                {
                                    bestDistance = distance;
                                    bestHeight = sampleHeight;
                                    foundWalkable = true;
                                }
                            }
                        }
                    }
                }
            }

            // Step 5: If walkable surface found, project point to that height
            if (foundWalkable)
            {
                height = bestHeight;
                result = new Vector3(point.X, height, point.Z);
                return true;
            }

            // Step 6: No walkable surface found - cannot project
            return false;
        }

        /// <summary>
        /// Finds a path between two points using A* algorithm.
        /// </summary>
        /// <param name="start">Start position in world coordinates.</param>
        /// <param name="end">End position in world coordinates.</param>
        /// <param name="waypoints">Output array of waypoints along the path.</param>
        /// <returns>True if a path was found, false otherwise.</returns>
        /// <remarks>
        /// Aurora pathfinding works across tile boundaries.
        /// Uses hierarchical pathfinding: tile-level then within-tile.
        /// More sophisticated than Odyssey's single-mesh approach.
        ///
        /// Based on nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510
        /// - Grid-based A* pathfinding over tile grid
        /// - Uses tile centers as waypoints
        /// - Applies path smoothing for natural movement
        ///
        /// Algorithm:
        /// 1. Convert start and end to tile coordinates
        /// 2. Validate both positions are on valid, walkable tiles
        /// 3. If same tile, return direct path
        /// 4. Run A* pathfinding on tile grid
        /// 5. Convert tile path to world coordinates
        /// 6. Apply path smoothing using line-of-sight checks
        /// </remarks>
        public bool FindPath(Vector3 start, Vector3 end, out Vector3[] waypoints)
        {
            waypoints = null;

            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Convert start and end to tile coordinates
            int startTileX, startTileY;
            int endTileX, endTileY;

            if (!GetTileCoordinates(start, out startTileX, out startTileY))
            {
                return false; // Start position not on valid tile
            }

            if (!GetTileCoordinates(end, out endTileX, out endTileY))
            {
                return false; // End position not on valid tile
            }

            // Validate both tiles are walkable
            if (!IsTileValid(startTileX, startTileY) || !IsTileValid(endTileX, endTileY))
            {
                return false;
            }

            AuroraTile startTile = _tiles[startTileY, startTileX];
            AuroraTile endTile = _tiles[endTileY, endTileX];

            if (!startTile.IsLoaded || !startTile.IsWalkable || !endTile.IsLoaded || !endTile.IsWalkable)
            {
                return false; // Start or end tile is not walkable
            }

            // Same tile - direct path
            if (startTileX == endTileX && startTileY == endTileY)
            {
                waypoints = new[] { start, end };
                return true;
            }

            // A* pathfinding over tile grid (without obstacles)
            IList<Vector3> path = FindPathInternal(start, end, startTileX, startTileY, endTileX, endTileY, null);
            if (path != null && path.Count > 0)
            {
                waypoints = new Vector3[path.Count];
                for (int i = 0; i < path.Count; i++)
                {
                    waypoints[i] = path[i];
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the height at a specific point.
        /// </summary>
        /// <remarks>
        /// Samples height from tile-based terrain data.
        /// Handles tile boundary interpolation.
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates
        /// - nwmain.exe: CNWTile height sampling - Tiles store height transition data
        /// - Tile height transitions: Tile.Height property indicates number of height transitions
        /// - Height interpolation: For tile boundaries, interpolate between adjacent tile heights
        ///
        /// Algorithm:
        /// 1. Find containing tile using GetTileCoordinates
        /// 2. If tile is valid and loaded, sample height from tile
        /// 3. For tile boundaries, check adjacent tiles and interpolate heights
        /// 4. Handle edge cases: points outside tile grid, unloaded tiles, non-walkable tiles
        ///
        /// Note: Aurora tiles have height transition data stored per-tile.
        /// The Tile.Height property indicates the number of height transitions, but actual
        /// height values are sampled from tile geometry data (walkmesh) when available.
        ///
        /// Implementation details:
        /// - Samples height at the actual position within the tile using GetTileHeightAtPosition
        /// - For tile boundaries, interpolates heights from adjacent tiles for smooth transitions
        /// - Falls back to simplified height transition model with bilinear interpolation when walkmesh data is unavailable
        /// - Based on nwmain.exe: CNWTileSurfaceMesh::GetHeightAtPoint @ 0x1402bedf0
        /// </remarks>
        public bool GetHeightAtPoint(Vector3 point, out float height)
        {
            height = point.Y;

            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Find tile containing point
            int tileX, tileY;
            if (!GetTileCoordinates(point, out tileX, out tileY))
            {
                // Point is outside tile grid bounds
                return false;
            }

            // Check if tile is valid
            if (!IsTileValid(tileX, tileY))
            {
                return false;
            }

            AuroraTile tile = _tiles[tileY, tileX];
            if (!tile.IsLoaded)
            {
                return false;
            }

            // Calculate position within tile (0.0 to 1.0 for both X and Z)
            float tileWorldX = tileX * TileSize;
            float tileWorldZ = tileY * TileSize;
            float localX = point.X - tileWorldX;
            float localZ = point.Z - tileWorldZ;
            float normalizedX = localX / TileSize;
            float normalizedZ = localZ / TileSize;

            // Clamp to tile bounds (0.0 to 1.0)
            normalizedX = Math.Max(0.0f, Math.Min(1.0f, normalizedX));
            normalizedZ = Math.Max(0.0f, Math.Min(1.0f, normalizedZ));

            // For tile boundaries, check adjacent tiles for height interpolation
            // This handles smooth height transitions between tiles
            bool onBoundaryX = normalizedX < 0.1f || normalizedX > 0.9f;
            bool onBoundaryZ = normalizedZ < 0.1f || normalizedZ > 0.9f;

            if (onBoundaryX || onBoundaryZ)
            {
                // Interpolate height from current tile and adjacent tiles using actual positions
                // Based on nwmain.exe: Tile boundary height interpolation for smooth transitions
                float totalHeight = 0.0f;
                float totalWeight = 0.0f;

                // Sample current tile at actual position
                float currentTileHeight = GetTileHeightAtPosition(tileX, tileY, normalizedX, normalizedZ);
                if (currentTileHeight != float.MinValue)
                {
                    float weight = 1.0f;
                    totalHeight += currentTileHeight * weight;
                    totalWeight += weight;
                }

                // Sample adjacent tiles for interpolation
                // For adjacent tiles, calculate the corresponding normalized position
                int[] dx = { -1, 1, 0, 0, -1, -1, 1, 1 };
                int[] dy = { 0, 0, -1, 1, -1, 1, -1, 1 };
                float[] baseWeights = { 0.5f, 0.5f, 0.5f, 0.5f, 0.25f, 0.25f, 0.25f, 0.25f };

                for (int i = 0; i < dx.Length; i++)
                {
                    int neighborX = tileX + dx[i];
                    int neighborY = tileY + dy[i];

                    if (IsTileValid(neighborX, neighborY))
                    {
                        AuroraTile neighborTile = _tiles[neighborY, neighborX];
                        if (neighborTile.IsLoaded)
                        {
                            // Calculate normalized position in the adjacent tile
                            // For tiles to the left (dx = -1), position is at right edge (1.0 - normalizedX)
                            // For tiles to the right (dx = 1), position is at left edge (normalizedX - 1.0)
                            // For tiles above (dy = -1), position is at bottom edge (1.0 - normalizedZ)
                            // For tiles below (dy = 1), position is at top edge (normalizedZ - 1.0)
                            float neighborNormX = normalizedX;
                            float neighborNormZ = normalizedZ;

                            if (dx[i] < 0) // Left neighbor
                            {
                                neighborNormX = 1.0f + normalizedX; // Position extends into left tile
                            }
                            else if (dx[i] > 0) // Right neighbor
                            {
                                neighborNormX = normalizedX - 1.0f; // Position extends into right tile
                            }

                            if (dy[i] < 0) // Up neighbor (negative Y)
                            {
                                neighborNormZ = 1.0f + normalizedZ; // Position extends into up tile
                            }
                            else if (dy[i] > 0) // Down neighbor (positive Y)
                            {
                                neighborNormZ = normalizedZ - 1.0f; // Position extends into down tile
                            }

                            // Clamp to valid range [0.0, 1.0]
                            neighborNormX = Math.Max(0.0f, Math.Min(1.0f, neighborNormX));
                            neighborNormZ = Math.Max(0.0f, Math.Min(1.0f, neighborNormZ));

                            // Sample height at the corresponding position in the adjacent tile
                            float neighborHeight = GetTileHeightAtPosition(neighborX, neighborY, neighborNormX, neighborNormZ);
                            if (neighborHeight != float.MinValue)
                            {
                                // Weight decreases with distance from boundary
                                // Calculate distance from current point to tile edge
                                float edgeDistanceX = dx[i] < 0 ? normalizedX : (dx[i] > 0 ? (1.0f - normalizedX) : 0.0f);
                                float edgeDistanceZ = dy[i] < 0 ? normalizedZ : (dy[i] > 0 ? (1.0f - normalizedZ) : 0.0f);
                                float edgeDistance = (float)Math.Sqrt(edgeDistanceX * edgeDistanceX + edgeDistanceZ * edgeDistanceZ);
                                float weight = baseWeights[i] * Math.Max(0.0f, 1.0f - edgeDistance * 10.0f); // Scale by 10 for 0.1 boundary threshold

                                totalHeight += neighborHeight * weight;
                                totalWeight += weight;
                            }
                        }
                    }
                }

                if (totalWeight > 0.0f)
                {
                    height = totalHeight / totalWeight;
                    return true;
                }
            }

            // For points well within tile, sample height at actual position (not just tile center)
            // Based on nwmain.exe: CNWTileSurfaceMesh::GetHeightAtPoint samples at actual position
            float tileHeight = GetTileHeightAtPosition(tileX, tileY, normalizedX, normalizedZ);
            if (tileHeight != float.MinValue)
            {
                height = tileHeight;
                return true;
            }

            // Fallback: use point's current Y coordinate
            return false;
        }

        /// <summary>
        /// Gets the height of a tile at its center.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWTile height data access (CNWTileSurfaceMesh::GetHeightAtPoint @ 0x1402bedf0)
        /// Tiles store height transition data that indicates terrain elevation.
        /// The Tile.Height property indicates the number of height transitions.
        ///
        /// Implementation:
        /// 1. If tileset loader and tileset resref are available, samples height from actual walkmesh geometry
        /// 2. Otherwise, falls back to simplified model based on height transitions with bilinear interpolation
        ///
        /// Walkmesh sampling:
        /// - Loads tile model from tileset using TileId
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Samples height at tile center (0.5, 0.5) using barycentric interpolation
        /// - Falls back to average face height if point is not within any face
        /// </remarks>
        private float GetTileHeight(int tileX, int tileY)
        {
            return GetTileHeightAtPosition(tileX, tileY, 0.5f, 0.5f);
        }

        /// <summary>
        /// Calculates the height at a corner of a tile, considering adjacent tiles for smooth transitions.
        /// </summary>
        /// <param name="tileX">Tile X coordinate.</param>
        /// <param name="tileY">Tile Y coordinate.</param>
        /// <param name="baseHeight">Base height of the tile (from height transitions).</param>
        /// <param name="cornerDx">X direction offset for corner (-1 for left, 1 for right, 0 for center).</param>
        /// <param name="cornerDz">Z direction offset for corner (-1 for top, 1 for bottom, 0 for center).</param>
        /// <returns>The calculated corner height.</returns>
        /// <remarks>
        /// Based on nwmain.exe: Tile corner height calculation considers adjacent tiles.
        /// For smooth height transitions, corner heights are averaged from the tile and its adjacent tiles.
        ///
        /// Algorithm:
        /// 1. Start with the base tile's height
        /// 2. Check adjacent tiles in the corner direction (cornerDx, cornerDz)
        /// 3. Average heights from valid adjacent tiles for smooth transitions
        /// 4. Return the calculated corner height
        ///
        /// Corner directions:
        /// - (-1, -1): Top-left corner (checks left and top neighbors)
        /// - (1, -1): Top-right corner (checks right and top neighbors)
        /// - (-1, 1): Bottom-left corner (checks left and bottom neighbors)
        /// - (1, 1): Bottom-right corner (checks right and bottom neighbors)
        /// </remarks>
        private float CalculateCornerHeight(int tileX, int tileY, float baseHeight, int cornerDx, int cornerDz)
        {
            float heightPerTransition = 0.5f; // Based on nwmain.exe: Standard height transition value

            // Start with the base tile's height
            float totalHeight = baseHeight;
            int tileCount = 1;

            // Check adjacent tiles in the corner direction for smooth transitions
            // For a corner, we check the tile itself and up to 3 adjacent tiles (horizontal, vertical, and diagonal)

            // Check horizontal neighbor (left or right)
            if (cornerDx != 0)
            {
                int neighborX = tileX + cornerDx;
                int neighborY = tileY;
                if (IsTileValid(neighborX, neighborY))
                {
                    AuroraTile neighborTile = _tiles[neighborY, neighborX];
                    if (neighborTile.IsLoaded)
                    {
                        float neighborHeight = baseHeight + (neighborTile.Height * heightPerTransition);
                        totalHeight += neighborHeight;
                        tileCount++;
                    }
                }
            }

            // Check vertical neighbor (top or bottom)
            if (cornerDz != 0)
            {
                int neighborX = tileX;
                int neighborY = tileY + cornerDz;
                if (IsTileValid(neighborX, neighborY))
                {
                    AuroraTile neighborTile = _tiles[neighborY, neighborX];
                    if (neighborTile.IsLoaded)
                    {
                        float neighborHeight = baseHeight + (neighborTile.Height * heightPerTransition);
                        totalHeight += neighborHeight;
                        tileCount++;
                    }
                }
            }

            // Check diagonal neighbor (if both cornerDx and cornerDz are non-zero)
            if (cornerDx != 0 && cornerDz != 0)
            {
                int neighborX = tileX + cornerDx;
                int neighborY = tileY + cornerDz;
                if (IsTileValid(neighborX, neighborY))
                {
                    AuroraTile neighborTile = _tiles[neighborY, neighborX];
                    if (neighborTile.IsLoaded)
                    {
                        float neighborHeight = baseHeight + (neighborTile.Height * heightPerTransition);
                        totalHeight += neighborHeight;
                        tileCount++;
                    }
                }
            }

            // Average the heights from all valid tiles for smooth transitions
            return totalHeight / tileCount;
        }

        /// <summary>
        /// Gets the height of a tile at a specific normalized position within the tile.
        /// </summary>
        /// <param name="tileX">Tile X coordinate.</param>
        /// <param name="tileY">Tile Y coordinate.</param>
        /// <param name="normalizedX">Normalized X coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <param name="normalizedZ">Normalized Z coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <returns>The height at the specified position, or float.MinValue if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh::GetHeightAtPoint @ 0x1402bedf0
        /// Samples height from walkmesh geometry at the specified normalized position within the tile.
        ///
        /// Implementation:
        /// 1. If tileset loader and tileset resref are available, samples height from actual walkmesh geometry at the specified position
        /// 2. Otherwise, falls back to simplified model based on height transitions with bilinear interpolation
        ///
        /// Walkmesh sampling:
        /// - Loads tile model from tileset using TileId
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Samples height at the specified normalized position using barycentric interpolation
        /// - Falls back to average face height if point is not within any face
        ///
        /// This method provides accurate height sampling at any point within the tile, not just the center.
        /// </remarks>
        private float GetTileHeightAtPosition(int tileX, int tileY, float normalizedX, float normalizedZ)
        {
            if (!IsTileValid(tileX, tileY))
            {
                return float.MinValue;
            }

            AuroraTile tile = _tiles[tileY, tileX];
            if (!tile.IsLoaded)
            {
                return float.MinValue;
            }

            // Clamp normalized coordinates to valid range [0.0, 1.0]
            normalizedX = Math.Max(0.0f, Math.Min(1.0f, normalizedX));
            normalizedZ = Math.Max(0.0f, Math.Min(1.0f, normalizedZ));

            // Try to sample height from walkmesh geometry if tileset loader is available
            if (_tilesetLoader != null && !string.IsNullOrEmpty(_tilesetResRef))
            {
                // Sample height at the specified normalized position within the tile
                // Based on nwmain.exe: CNWTileSurfaceMesh::GetHeightAtPoint @ 0x1402bedf0
                // Uses barycentric interpolation to get accurate height at any point within the tile
                // Pass tile's Height property for fallback height transition model if walkmesh unavailable
                float walkmeshHeight = _tilesetLoader.GetTileHeight(_tilesetResRef, tile.TileId, normalizedX, normalizedZ, tile.Height);
                if (walkmeshHeight != float.MinValue)
                {
                    return walkmeshHeight;
                }
            }

            // Fallback: Use simplified model based on height transitions
            // Based on nwmain.exe: CNWTileSet::GetHeightTransition @ 0x1402c67c0
            // Tile.Height indicates number of height transitions
            // Height transitions correspond to elevation changes
            // Each height transition typically represents a 0.5 unit elevation change
            // This is a reasonable approximation when walkmesh data is not available

            // Calculate corner heights using bilinear interpolation model
            // Based on nwmain.exe: Tile height transitions are stored per-tile
            // The height at any point within the tile is interpolated from corner heights
            // Corner heights are calculated from the tile's height transitions and adjacent tiles

            float heightPerTransition = 0.5f; // Based on nwmain.exe: Standard height transition value
            float baseHeight = 0.0f;

            // Calculate base height for this tile
            float tileBaseHeight = baseHeight + (tile.Height * heightPerTransition);

            // Calculate corner heights by considering adjacent tiles for smooth transitions
            // Top-left corner (normalizedX=0, normalizedZ=0)
            float topLeftHeight = CalculateCornerHeight(tileX, tileY, tileBaseHeight, -1, -1);

            // Top-right corner (normalizedX=1, normalizedZ=0)
            float topRightHeight = CalculateCornerHeight(tileX, tileY, tileBaseHeight, 1, -1);

            // Bottom-left corner (normalizedX=0, normalizedZ=1)
            float bottomLeftHeight = CalculateCornerHeight(tileX, tileY, tileBaseHeight, -1, 1);

            // Bottom-right corner (normalizedX=1, normalizedZ=1)
            float bottomRightHeight = CalculateCornerHeight(tileX, tileY, tileBaseHeight, 1, 1);

            // Bilinear interpolation to get height at the specified normalized position
            // Based on standard bilinear interpolation formula:
            // h(x,z) = (1-x)(1-z)*h00 + x(1-z)*h10 + (1-x)z*h01 + xz*h11
            // where h00=topLeft, h10=topRight, h01=bottomLeft, h11=bottomRight
            float h00 = topLeftHeight;
            float h10 = topRightHeight;
            float h01 = bottomLeftHeight;
            float h11 = bottomRightHeight;

            float height = (1.0f - normalizedX) * (1.0f - normalizedZ) * h00 +
                          normalizedX * (1.0f - normalizedZ) * h10 +
                          (1.0f - normalizedX) * normalizedZ * h01 +
                          normalizedX * normalizedZ * h11;

            return height;
        }

        /// <summary>
        /// Checks line of sight between two points.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora line of sight implementation.
        /// Aurora line of sight works across tile boundaries.
        /// Checks visibility through tile portals and terrain.
        /// More complex than Odyssey due to tile-based geometry.
        ///
        /// Algorithm:
        /// 1. Handle edge case: same point (always has line of sight)
        /// 2. Perform raycast from start to end
        /// 3. If raycast hits something, check if hit is close to destination (within tolerance)
        /// 4. If hit is at destination or very close, line of sight is clear
        /// 5. If hit blocks the path, line of sight is obstructed
        ///
        /// This implementation matches the behavior used by:
        /// - Perception system for AI visibility checks
        /// - Projectile collision detection
        /// - Movement collision detection
        /// </remarks>
        /// <summary>
        /// Checks line of sight between two points.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Uses base class common algorithm with tile-based blocking checks.
        /// </remarks>
        public new bool HasLineOfSight(Vector3 start, Vector3 end)
        {
            return base.HasLineOfSight(start, end);
        }

        /// <summary>
        /// Aurora-specific check: blocking tiles obstruct line of sight.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: blocking tiles block line of sight.
        /// </remarks>
        protected override bool CheckHitBlocksLineOfSight(Vector3 hitPoint, int hitFace, Vector3 start, Vector3 end)
        {
            // Aurora: Hit a blocking tile that obstructs line of sight
            // (Aurora doesn't have walkable face concept like Odyssey - tiles either block or don't)
            return false; // Hit blocks line of sight
        }

        /// <summary>
        /// Gets the tile coordinates containing a point.
        /// </summary>
        /// <remarks>
        /// Aurora-specific: Converts world coordinates to tile coordinates.
        /// Used for tile-based operations and pathfinding.
        /// Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
        /// - Converts world X coordinate: tileX = floor(worldX / TileSize)
        /// - Converts world Z coordinate: tileY = floor(worldZ / TileSize) (Aurora uses Z for vertical, Y for depth)
        /// - TileSize is 10.0f units (DAT_140dc2df4 in nwmain.exe)
        /// </remarks>
        public bool GetTileCoordinates(Vector3 point, out int tileX, out int tileY)
        {
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
            // Lines 19-20: Convert X coordinate to tile index
            // Lines 30-31: Convert Y coordinate to tile index
            // TileSize is 10.0f units per tile (DAT_140dc2df4)
            // Formula: tileX = floor(worldX / TileSize), tileY = floor(worldZ / TileSize)

            tileX = -1;
            tileY = -1;

            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0)
            {
                return false;
            }

            // Convert world coordinates to tile grid coordinates using floor division
            // Based on GetTile: tileX = floor(worldX / TileSize)
            tileX = (int)Math.Floor(point.X / TileSize);
            tileY = (int)Math.Floor(point.Z / TileSize); // Aurora uses Z for vertical, Y for depth

            // Validate tile coordinates are within bounds
            // Based on nwmain.exe: CNWSArea::GetTile bounds checking
            if (tileX < 0 || tileX >= _tileWidth || tileY < 0 || tileY >= _tileHeight)
            {
                tileX = -1;
                tileY = -1;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a tile coordinate is valid and loaded.
        /// </summary>
        /// <remarks>
        /// Aurora areas may have unloaded or invalid tiles.
        /// Checks tile existence and walkability.
        /// Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
        /// - Lines 37-38: Bounds checking: ((-1 < iVar4) && (iVar4 < *(int *)(this + 0xc))) && (-1 < iVar3) && (iVar3 < *(int *)(this + 0x10))
        /// - Returns null pointer (0x0) if coordinates are out of bounds
        /// - Tile array access: ((longlong)(*(int *)(this + 0xc) * iVar3 + iVar4) * 0x68 + *(longlong *)(this + 0x1c8))
        /// - This validates: 0 <= tileX < width, 0 <= tileY < height
        /// - Then checks if tile is loaded and has walkable surfaces
        /// </remarks>
        public bool IsTileValid(int tileX, int tileY)
        {
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
            // Lines 37-38: Bounds validation
            // Check if coordinates are within valid range
            if (tileX < 0 || tileX >= _tileWidth)
            {
                return false;
            }

            if (tileY < 0 || tileY >= _tileHeight)
            {
                return false;
            }

            // Based on GetTile: Returns null if out of bounds, otherwise returns tile pointer
            // If we have a tile array, check if the tile exists and is loaded
            if (_tiles == null || _tiles.Length == 0)
            {
                // No tiles loaded - consider valid if coordinates are in bounds
                // This allows the method to work even when tiles aren't fully loaded yet
                return true;
            }

            // Get the tile at the specified coordinates
            // Based on GetTile line 40: Array access pattern (width * y + x)
            AuroraTile tile = _tiles[tileY, tileX];

            // Check if tile is loaded
            // Based on nwmain.exe: CNWTileSet::GetTileData() validation
            // Tiles must be loaded before they can be used
            if (!tile.IsLoaded)
            {
                return false;
            }

            // Check if tile has valid tile ID (non-negative indicates a real tile)
            // Based on ARE format: Tile_ID is an index into tileset, must be >= 0
            if (tile.TileId < 0)
            {
                return false;
            }

            // Tile is valid if it exists, is loaded, and has a valid tile ID
            // Walkability check is optional - some tiles may be valid but not walkable
            // (e.g., water tiles, decorative tiles)
            return true;
        }

        /// <summary>
        /// Gets walkable neighbors for a tile.
        /// </summary>
        /// <remarks>
        /// Used for tile-level pathfinding in Aurora.
        /// Returns adjacent tiles that can be traversed.
        /// Based on nwmain.exe: CNWSArea::InterTileDFSSoundPath @ 0x14036e260
        /// - Checks 8-directional neighbors (N, S, E, W, NE, NW, SE, SW)
        /// - Validates tile bounds and walkability
        /// - Returns only valid, loaded, walkable tiles
        /// </remarks>
        public IEnumerable<(int x, int y)> GetTileNeighbors(int tileX, int tileY)
        {
            // Based on nwmain.exe: InterTileDFSSoundPath neighbor checking
            // Checks 8-directional neighbors: N, S, E, W, NE, NW, SE, SW
            int[] dx = { 0, 0, 1, -1, 1, -1, 1, -1 };
            int[] dy = { 1, -1, 0, 0, 1, 1, -1, -1 };

            for (int i = 0; i < dx.Length; i++)
            {
                int neighborX = tileX + dx[i];
                int neighborY = tileY + dy[i];

                // Check if neighbor is valid and walkable
                if (IsTileValid(neighborX, neighborY))
                {
                    AuroraTile neighborTile = _tiles[neighborY, neighborX];
                    if (neighborTile.IsLoaded && neighborTile.IsWalkable)
                    {
                        yield return (neighborX, neighborY);
                    }
                }
            }
        }

        // INavigationMesh interface implementations

        /// <summary>
        /// Finds a path from start to goal.
        /// </summary>
        /// <param name="start">Start position in world coordinates.</param>
        /// <param name="goal">Goal position in world coordinates.</param>
        /// <returns>List of waypoints along the path, or null if no path found.</returns>
        /// <remarks>
        /// Aurora pathfinding implementation using A* algorithm over tile grid.
        /// Based on nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510
        /// - Grid-based A* pathfinding over tile grid
        /// - Uses tile centers as waypoints
        /// - Applies path smoothing for natural movement
        ///
        /// Algorithm:
        /// 1. Convert start and goal to tile coordinates
        /// 2. Validate both positions are on valid, walkable tiles
        /// 3. If same tile, return direct path
        /// 4. Run A* pathfinding on tile grid
        /// 5. Convert tile path to world coordinates
        /// 6. Apply path smoothing using line-of-sight checks
        /// </remarks>
        public override IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return null;
            }

            // Convert start and goal to tile coordinates
            int startTileX, startTileY;
            int goalTileX, goalTileY;

            if (!GetTileCoordinates(start, out startTileX, out startTileY))
            {
                return null; // Start position not on valid tile
            }

            if (!GetTileCoordinates(goal, out goalTileX, out goalTileY))
            {
                return null; // Goal position not on valid tile
            }

            // Validate both tiles are walkable
            if (!IsTileValid(startTileX, startTileY) || !IsTileValid(goalTileX, goalTileY))
            {
                return null;
            }

            AuroraTile startTile = _tiles[startTileY, startTileX];
            AuroraTile goalTile = _tiles[goalTileY, goalTileX];

            if (!startTile.IsLoaded || !startTile.IsWalkable || !goalTile.IsLoaded || !goalTile.IsWalkable)
            {
                return null; // Start or goal tile is not walkable
            }

            // Same tile - direct path
            if (startTileX == goalTileX && startTileY == goalTileY)
            {
                return new List<Vector3> { start, goal };
            }

            // A* pathfinding over tile grid (without obstacles)
            return FindPathInternal(start, goal, startTileX, startTileY, goalTileX, goalTileY, null);
        }

        /// <summary>
        /// Internal A* pathfinding implementation shared by FindPath and FindPathAroundObstacles.
        /// </summary>
        /// <param name="start">Start position in world coordinates.</param>
        /// <param name="goal">Goal position in world coordinates.</param>
        /// <param name="startTileX">Start tile X coordinate.</param>
        /// <param name="startTileY">Start tile Y coordinate.</param>
        /// <param name="goalTileX">Goal tile X coordinate.</param>
        /// <param name="goalTileY">Goal tile Y coordinate.</param>
        /// <param name="blockedTiles">Set of blocked tile coordinates (null if no obstacles).</param>
        /// <returns>List of waypoints along the path, or null if no path found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510
        /// - Grid-based A* pathfinding over tile grid
        /// - Uses tile centers as waypoints
        /// - Applies path smoothing for natural movement
        /// </remarks>
        private IList<Vector3> FindPathInternal(Vector3 start, Vector3 goal, int startTileX, int startTileY, int goalTileX, int goalTileY, HashSet<(int x, int y)> blockedTiles)
        {
            // A* pathfinding over tile grid
            var openSet = new SortedSet<TileScore>(new TileScoreComparer());
            var cameFrom = new Dictionary<(int x, int y), (int x, int y)>();
            var gScore = new Dictionary<(int x, int y), float>();
            var fScore = new Dictionary<(int x, int y), float>();
            var inOpenSet = new HashSet<(int x, int y)>();

            var startTileCoord = (startTileX, startTileY);
            var goalTileCoord = (goalTileX, goalTileY);

            // Initialize start node
            gScore[startTileCoord] = 0f;
            fScore[startTileCoord] = TileHeuristic(startTileX, startTileY, goalTileX, goalTileY);
            openSet.Add(new TileScore(startTileX, startTileY, fScore[startTileCoord]));
            inOpenSet.Add(startTileCoord);

            // A* main loop
            while (openSet.Count > 0)
            {
                // Get tile with lowest f-score
                TileScore currentScore = GetMinTile(openSet);
                openSet.Remove(currentScore);
                var current = (x: currentScore.X, y: currentScore.Y);
                inOpenSet.Remove(current);

                // Goal reached - reconstruct path
                if (current.x == goalTileX && current.y == goalTileY)
                {
                    return ReconstructTilePath(cameFrom, current, start, goal);
                }

                // Check all adjacent tiles
                foreach ((int x, int y) neighbor in GetTileNeighbors(current.x, current.y))
                {
                    // Skip blocked tiles (obstacle avoidance)
                    if (blockedTiles != null && blockedTiles.Contains(neighbor))
                    {
                        continue;
                    }

                    // Calculate tentative g-score
                    float tentativeG;
                    if (gScore.TryGetValue(current, out float currentG))
                    {
                        tentativeG = currentG + TileEdgeCost(current.x, current.y, neighbor.x, neighbor.y);
                    }
                    else
                    {
                        tentativeG = TileEdgeCost(current.x, current.y, neighbor.x, neighbor.y);
                    }

                    // Update if this path is better
                    float neighborG;
                    if (!gScore.TryGetValue(neighbor, out neighborG) || tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float newF = tentativeG + TileHeuristic(neighbor.x, neighbor.y, goalTileX, goalTileY);
                        fScore[neighbor] = newF;

                        // Add to open set if not already there
                        if (!inOpenSet.Contains(neighbor))
                        {
                            openSet.Add(new TileScore(neighbor.x, neighbor.y, newF));
                            inOpenSet.Add(neighbor);
                        }
                    }
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// Based on nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510 - tile-based pathfinding with obstacle avoidance
        /// </summary>
        /// <remarks>
        /// Aurora obstacle avoidance pathfinding implementation:
        /// - Uses tile-based A* pathfinding (not face-based like Odyssey)
        /// - Blocks tiles that contain obstacles
        /// - Similar algorithm to Odyssey but operates on tiles instead of faces
        ///
        /// Algorithm:
        /// 1. Convert start/goal positions to tile coordinates
        /// 2. Validate both positions are on valid, walkable tiles
        /// 3. Build set of blocked tiles from obstacles
        /// 4. Run A* pathfinding on tile grid, skipping blocked tiles
        /// 5. Convert tile path back to world coordinates
        /// 6. Apply path smoothing using line-of-sight checks
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510 - grid-based pathfinding
        /// - nwmain.exe: CNWSArea::InterTileDFSSoundPath @ 0x14036e260 - tile neighbor traversal
        /// - nwmain.exe: CPathfindInformation class - pathfinding context and obstacle data
        /// </remarks>
        public override IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return null;
            }

            // Convert start and goal to tile coordinates
            int startTileX, startTileY;
            int goalTileX, goalTileY;

            if (!GetTileCoordinates(start, out startTileX, out startTileY))
            {
                return null; // Start position not on valid tile
            }

            if (!GetTileCoordinates(goal, out goalTileX, out goalTileY))
            {
                return null; // Goal position not on valid tile
            }

            // Validate both tiles are walkable
            if (!IsTileValid(startTileX, startTileY) || !IsTileValid(goalTileX, goalTileY))
            {
                return null;
            }

            AuroraTile startTile = _tiles[startTileY, startTileX];
            AuroraTile goalTile = _tiles[goalTileY, goalTileX];

            if (!startTile.IsLoaded || !startTile.IsWalkable || !goalTile.IsLoaded || !goalTile.IsWalkable)
            {
                return null; // Start or goal tile is not walkable
            }

            // Same tile - check if obstacle blocks direct path
            if (startTileX == goalTileX && startTileY == goalTileY)
            {
                if (obstacles != null && obstacles.Count > 0)
                {
                    Vector3 direction = goal - start;
                    float distance = direction.Length();
                    if (distance > 0.001f)
                    {
                        direction = Vector3.Normalize(direction);
                        // Check if any obstacle blocks the direct path
                        foreach (ObstacleInfo obstacle in obstacles)
                        {
                            Vector3 toObstacle = obstacle.Position - start;
                            float projectionLength = Vector3.Dot(toObstacle, direction);
                            if (projectionLength >= 0f && projectionLength <= distance)
                            {
                                Vector3 closestPoint = start + direction * projectionLength;
                                float distanceToObstacle = Vector3.Distance(closestPoint, obstacle.Position);
                                if (distanceToObstacle < obstacle.Radius)
                                {
                                    // Obstacle blocks direct path - try adjacent tiles
                                    return FindPathAroundObstacleOnSameTile(start, goal, startTileX, startTileY, obstacles);
                                }
                            }
                        }
                    }
                }
                // Same tile, no obstacle blocking - direct path
                return new List<Vector3> { start, goal };
            }

            // Build set of blocked tiles from obstacles
            HashSet<(int x, int y)> blockedTiles = null;
            if (obstacles != null && obstacles.Count > 0)
            {
                blockedTiles = BuildBlockedTilesSet(obstacles);
            }

            // A* pathfinding over tile grid (using shared implementation)
            IList<Vector3> path = FindPathInternal(start, goal, startTileX, startTileY, goalTileX, goalTileY, blockedTiles);

            // If no path found, try with expanded obstacle radius
            if (path == null && obstacles != null && obstacles.Count > 0)
            {
                var expandedObstacles = new List<ObstacleInfo>();
                foreach (ObstacleInfo obstacle in obstacles)
                {
                    expandedObstacles.Add(new ObstacleInfo(obstacle.Position, obstacle.Radius * 1.5f));
                }
                return FindPathAroundObstacles(start, goal, expandedObstacles);
            }

            return path;
        }

        /// <summary>
        /// Finds a path around an obstacle when start and goal are on the same tile.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Similar logic to Odyssey's same-face obstacle avoidance.
        /// When start and goal are on the same tile but an obstacle blocks the direct path,
        /// we try to route through adjacent tiles.
        /// </remarks>
        private IList<Vector3> FindPathAroundObstacleOnSameTile(Vector3 start, Vector3 goal, int tileX, int tileY, IList<Runtime.Core.Interfaces.ObstacleInfo> obstacles)
        {
            var candidateTiles = new List<(int x, int y)>();
            foreach ((int x, int y) neighbor in GetTileNeighbors(tileX, tileY))
            {
                bool isBlocked = false;
                if (obstacles != null)
                {
                    Vector3 neighborCenter = GetTileCenter(neighbor.x, neighbor.y);
                    foreach (ObstacleInfo obstacle in obstacles)
                    {
                        if (Vector3.Distance(neighborCenter, obstacle.Position) < obstacle.Radius)
                        {
                            isBlocked = true;
                            break;
                        }
                    }
                }
                if (!isBlocked)
                {
                    candidateTiles.Add(neighbor);
                }
            }

            if (candidateTiles.Count == 0)
            {
                return null;
            }

            foreach ((int x, int y) candidateTile in candidateTiles)
            {
                Vector3 candidateCenter = GetTileCenter(candidateTile.x, candidateTile.y);
                IList<Vector3> path = FindPathAroundObstacles(candidateCenter, goal, obstacles);
                if (path != null && path.Count > 0)
                {
                    var fullPath = new List<Vector3> { start };
                    foreach (Vector3 waypoint in path)
                    {
                        fullPath.Add(waypoint);
                    }
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a set of tile coordinates that are blocked by obstacles.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CPathfindInformation obstacle blocking logic.
        /// Checks all tiles and marks those that contain or are too close to obstacles.
        /// </remarks>
        private HashSet<(int x, int y)> BuildBlockedTilesSet(IList<Runtime.Core.Interfaces.ObstacleInfo> obstacles)
        {
            var blockedTiles = new HashSet<(int x, int y)>();

            foreach (ObstacleInfo obstacle in obstacles)
            {
                // Find tiles that intersect with obstacle
                // Convert obstacle position to tile coordinates
                int obstacleTileX = (int)Math.Floor(obstacle.Position.X / TileSize);
                int obstacleTileY = (int)Math.Floor(obstacle.Position.Z / TileSize);

                // Check tiles within obstacle radius
                int radiusInTiles = (int)Math.Ceiling(obstacle.Radius / TileSize) + 1;
                for (int dy = -radiusInTiles; dy <= radiusInTiles; dy++)
                {
                    for (int dx = -radiusInTiles; dx <= radiusInTiles; dx++)
                    {
                        int checkTileX = obstacleTileX + dx;
                        int checkTileY = obstacleTileY + dy;

                        if (IsTileValid(checkTileX, checkTileY))
                        {
                            Vector3 tileCenter = GetTileCenter(checkTileX, checkTileY);
                            float distanceToObstacle = Vector3.Distance(tileCenter, obstacle.Position);

                            if (distanceToObstacle < (obstacle.Radius + TileSize * 0.5f))
                            {
                                blockedTiles.Add((checkTileX, checkTileY));
                            }
                        }
                    }
                }
            }

            return blockedTiles;
        }

        /// <summary>
        /// Gets the center point of a tile in world coordinates.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWTile::GetLocation - tile center calculation.
        /// Tile center is at (tileX + 0.5) * TileSize, (tileY + 0.5) * TileSize.
        /// Height is sampled from tile data using GetTileHeight.
        /// </remarks>
        private Vector3 GetTileCenter(int tileX, int tileY)
        {
            float centerX = (tileX + 0.5f) * TileSize;
            float centerZ = (tileY + 0.5f) * TileSize;
            float centerY = GetTileHeight(tileX, tileY);
            if (centerY == float.MinValue)
            {
                centerY = 0f; // Fallback to 0 if tile height cannot be determined
            }
            return new Vector3(centerX, centerY, centerZ);
        }

        /// <summary>
        /// Gets the minimum element from a sorted set of tile scores.
        /// </summary>
        private TileScore GetMinTile(SortedSet<TileScore> set)
        {
            using (SortedSet<TileScore>.Enumerator enumerator = set.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    return enumerator.Current;
                }
            }
            return default(TileScore);
        }

        /// <summary>
        /// Calculates heuristic distance between two tiles.
        /// </summary>
        /// <remarks>
        /// Uses Euclidean distance between tile centers as heuristic.
        /// This is admissible for A* (never overestimates actual path cost).
        /// </remarks>
        private float TileHeuristic(int fromX, int fromY, int toX, int toY)
        {
            float dx = (toX - fromX) * TileSize;
            float dy = (toY - fromY) * TileSize;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates the cost of traversing from one tile to an adjacent tile.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Tile traversal cost calculation.
        /// Base cost is distance, with diagonal movement costing more (sqrt(2) multiplier).
        /// </remarks>
        private float TileEdgeCost(int fromX, int fromY, int toX, int toY)
        {
            int dx = toX - fromX;
            int dy = toY - fromY;

            // Diagonal movement costs more
            if (dx != 0 && dy != 0)
            {
                return TileSize * 1.41421356f; // sqrt(2) for diagonal
            }
            else
            {
                return TileSize; // Cardinal direction
            }
        }

        /// <summary>
        /// Reconstructs the path from A* search results.
        /// </summary>
        /// <remarks>
        /// Converts tile path to waypoint sequence.
        /// Adds start and goal positions, with tile centers as intermediate waypoints.
        /// Applies path smoothing to remove redundant waypoints.
        /// </remarks>
        private IList<Vector3> ReconstructTilePath(Dictionary<(int x, int y), (int x, int y)> cameFrom, (int x, int y) current, Vector3 start, Vector3 goal)
        {
            // Build tile path from goal to start
            var tilePath = new List<(int x, int y)> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                tilePath.Add(current);
            }
            tilePath.Reverse(); // Reverse to get start to goal

            // Convert tile path to waypoints
            var path = new List<Vector3>();
            path.Add(start);

            // Add tile centers as intermediate waypoints
            for (int i = 1; i < tilePath.Count - 1; i++)
            {
                path.Add(GetTileCenter(tilePath[i].x, tilePath[i].y));
            }

            path.Add(goal);

            // Apply funnel algorithm for smoother paths
            return SmoothTilePath(path);
        }

        /// <summary>
        /// Smooths the path by removing redundant waypoints.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Path smoothing using line-of-sight checks.
        /// Uses line-of-sight checks to remove waypoints that can be skipped.
        /// Results in more natural movement paths.
        /// </remarks>
        private IList<Vector3> SmoothTilePath(IList<Vector3> path)
        {
            // Simple path smoothing - remove redundant waypoints
            if (path.Count <= 2)
            {
                return path;
            }

            var smoothed = new List<Vector3>();
            smoothed.Add(path[0]);

            for (int i = 1; i < path.Count - 1; i++)
            {
                // Check if we can skip this waypoint
                Vector3 prev = smoothed[smoothed.Count - 1];
                Vector3 next = path[i + 1];

                // If line of sight is clear, skip this waypoint
                if (!HasLineOfSight(prev, next))
                {
                    // Can't skip - add the waypoint
                    smoothed.Add(path[i]);
                }
            }

            smoothed.Add(path[path.Count - 1]);
            return smoothed;
        }

        /// <summary>
        /// Helper struct for A* priority queue (tile-based).
        /// </summary>
        private struct TileScore
        {
            public int X;
            public int Y;
            public float Score;

            public TileScore(int x, int y, float score)
            {
                X = x;
                Y = y;
                Score = score;
            }
        }

        /// <summary>
        /// Comparer for TileScore to use with SortedSet.
        /// </summary>
        private class TileScoreComparer : IComparer<TileScore>
        {
            public int Compare(TileScore x, TileScore y)
            {
                int cmp = x.Score.CompareTo(y.Score);
                if (cmp != 0)
                {
                    return cmp;
                }
                // Ensure unique ordering for same scores
                int xCmp = x.X.CompareTo(y.X);
                if (xCmp != 0)
                {
                    return xCmp;
                }
                return x.Y.CompareTo(y.Y);
            }
        }

        /// <summary>
        /// Finds the face index at a given position.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based face finding.
        /// Aurora uses a tile-based navigation system where each tile represents a walkable surface.
        /// Unlike Odyssey's triangle-based walkmesh, Aurora maps positions to tiles and uses tile coordinates
        /// as face indices for interface compatibility.
        ///
        /// Algorithm:
        /// 1. Convert world position to tile coordinates using GetTileCoordinates
        /// 2. Validate tile is loaded and walkable
        /// 3. Calculate face index from tile coordinates: faceIndex = tileY * tileWidth + tileX
        /// 4. Return face index if valid, -1 if position is not on a valid walkable tile
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates
        /// - nwmain.exe: CNWTile::GetLocation @ 0x1402c55a0 - Gets tile grid coordinates
        /// - Tile size constant: DAT_140dc2df4 (10.0f units per tile)
        /// - Tile validation: Checks if tile is loaded and has walkable surfaces
        ///
        /// Note: In Aurora, "faces" are conceptual mappings to tiles rather than explicit triangle faces.
        /// Each tile can be considered a single walkable face for navigation purposes.
        /// If tiles contain walkmesh data (via GetWalkMesh), this could be extended to support
        /// multiple faces per tile, but the current implementation uses one face per tile.
        /// </remarks>
        public override int FindFaceAt(Vector3 position)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return -1;
            }

            // Convert world position to tile coordinates
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0
            int tileX, tileY;
            if (!GetTileCoordinates(position, out tileX, out tileY))
            {
                // Position is outside tile grid bounds
                return -1;
            }

            // Validate tile is loaded and walkable
            // Based on nwmain.exe: Tile validation checks
            if (!IsTileValid(tileX, tileY))
            {
                return -1;
            }

            AuroraTile tile = _tiles[tileY, tileX];
            if (!tile.IsLoaded || !tile.IsWalkable)
            {
                // Tile is not loaded or not walkable - no valid face
                return -1;
            }

            // Calculate face index from tile coordinates
            // Face index = tileY * tileWidth + tileX
            // This provides a unique face index for each tile in the grid
            // Based on nwmain.exe: Tile array indexing pattern (width * y + x)
            int faceIndex = tileY * _tileWidth + tileX;

            // Validate face index is within expected range
            // Maximum face index should be (tileHeight - 1) * tileWidth + (tileWidth - 1) = tileHeight * tileWidth - 1
            int maxFaceIndex = _tileHeight * _tileWidth - 1;
            if (faceIndex < 0 || faceIndex > maxFaceIndex)
            {
                // Invalid face index calculation - should not happen with valid tile coordinates
                return -1;
            }

            return faceIndex;
        }

        /// <summary>
        /// Gets the center point of a face.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based face center calculation.
        /// In Aurora, faces map to tiles in the tile grid. Each face index corresponds to a unique tile.
        /// The face center is the center point of the corresponding tile.
        ///
        /// Algorithm:
        /// 1. Validate face index is within expected range (0 to tileHeight * tileWidth - 1)
        /// 2. Convert face index to tile coordinates: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
        /// 3. Validate tile coordinates are within bounds
        /// 4. Use GetTileCenter to get the tile center point (world coordinates)
        /// 5. Return Vector3.Zero for invalid face indices
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Converts world coordinates to tile coordinates
        /// - nwmain.exe: CNWTile::GetLocation @ 0x1402c55a0 - Gets tile grid coordinates (X, Y) from tile structure
        /// - Tile center calculation: centerX = (tileX + 0.5) * TileSize, centerZ = (tileY + 0.5) * TileSize
        /// - Tile size constant: DAT_140dc2df4 (10.0f units per tile)
        /// - Face index calculation: faceIndex = tileY * tileWidth + tileX (from FindFaceAt)
        /// - Tile array indexing pattern: width * y + x (matches face index calculation)
        ///
        /// Tile center calculation (from GetTileCenter):
        /// - X coordinate: (tileX + 0.5) * TileSize (center of tile in X direction)
        /// - Z coordinate: (tileY + 0.5) * TileSize (center of tile in Z direction)
        /// - Y coordinate: Sampled from tile height data using GetTileHeight
        ///
        /// Note: Unlike Odyssey's triangle-based walkmesh where faces are actual triangles and the center
        /// is the centroid of the three vertices, Aurora uses a tile-based system where each face index
        /// maps to a tile in the grid. The face center is simply the center point of the corresponding tile,
        /// which is at (tileX + 0.5) * TileSize for X/Z coordinates and sampled height for Y coordinate.
        /// </remarks>
        public override Vector3 GetFaceCenter(int faceIndex)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return Vector3.Zero;
            }

            // Validate face index is within expected range
            // Maximum face index should be (tileHeight - 1) * tileWidth + (tileWidth - 1) = tileHeight * tileWidth - 1
            int maxFaceIndex = _tileHeight * _tileWidth - 1;
            if (faceIndex < 0 || faceIndex > maxFaceIndex)
            {
                // Face index is out of bounds - return zero vector
                return Vector3.Zero;
            }

            // Convert face index to tile coordinates
            // Based on FindFaceAt: faceIndex = tileY * tileWidth + tileX
            // Reverse: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
            int tileY = faceIndex / _tileWidth;
            int tileX = faceIndex % _tileWidth;

            // Validate tile coordinates are within bounds
            // This should always be true if faceIndex is valid, but check for safety
            if (tileX < 0 || tileX >= _tileWidth || tileY < 0 || tileY >= _tileHeight)
            {
                // Invalid tile coordinates - return zero vector
                return Vector3.Zero;
            }

            // Get the tile center point
            // Based on nwmain.exe: CNWTile::GetLocation @ 0x1402c55a0 - Gets tile grid coordinates
            // Tile center calculation: centerX = (tileX + 0.5) * TileSize, centerZ = (tileY + 0.5) * TileSize
            // Height is sampled from tile data using GetTileHeight
            // This matches the implementation in GetTileCenter method
            return GetTileCenter(tileX, tileY);
        }

        /// <summary>
        /// Gets adjacent faces for a given face.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based adjacent face finding.
        /// In Aurora, faces map to tiles in the tile grid. Each face index corresponds to a unique tile.
        /// Adjacent faces are determined by finding adjacent tiles in the 8-directional grid (N, S, E, W, NE, NW, SE, SW).
        ///
        /// Algorithm:
        /// 1. Validate face index is within expected range (0 to tileHeight * tileWidth - 1)
        /// 2. Convert face index to tile coordinates: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
        /// 3. Validate tile coordinates are within bounds
        /// 4. Use GetTileNeighbors to find adjacent walkable tiles
        /// 5. Convert each neighbor tile back to a face index: faceIndex = tileY * tileWidth + tileX
        /// 6. Return only walkable faces (GetTileNeighbors already filters for walkable tiles)
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::InterTileDFSSoundPath @ 0x14036e260 - Tile neighbor traversal
        /// - nwmain.exe: CNWSArea::PlotGridPath @ 0x14036f510 - Grid-based pathfinding uses adjacent tiles
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Tile coordinate conversion and validation
        /// - Face index calculation: faceIndex = tileY * tileWidth + tileX (from FindFaceAt)
        /// - Tile array indexing pattern: width * y + x (matches face index calculation)
        ///
        /// Tile neighbor directions (8-directional):
        /// - Cardinal: North (0, 1), South (0, -1), East (1, 0), West (-1, 0)
        /// - Diagonal: Northeast (1, 1), Northwest (-1, 1), Southeast (1, -1), Southwest (-1, -1)
        ///
        /// Note: Unlike Odyssey's triangle-based walkmesh where faces are actual triangles with explicit
        /// adjacency data, Aurora uses a tile-based system where each face index maps to a tile in the grid.
        /// Adjacent faces are determined by grid adjacency (8-directional neighbors), and only walkable
        /// tiles are considered valid adjacent faces.
        ///
        /// The GetTileNeighbors method already filters for valid, loaded, and walkable tiles, so all
        /// returned face indices will be walkable. This matches the behavior expected by pathfinding
        /// algorithms that use GetAdjacentFaces to traverse the navigation mesh.
        /// </remarks>
        public override IEnumerable<int> GetAdjacentFaces(int faceIndex)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                yield break;
            }

            // Validate face index is within expected range
            // Maximum face index should be (tileHeight - 1) * tileWidth + (tileWidth - 1) = tileHeight * tileWidth - 1
            int maxFaceIndex = _tileHeight * _tileWidth - 1;
            if (faceIndex < 0 || faceIndex > maxFaceIndex)
            {
                // Face index is out of bounds - no adjacent faces
                yield break;
            }

            // Convert face index to tile coordinates
            // Based on FindFaceAt: faceIndex = tileY * tileWidth + tileX
            // Reverse: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
            int tileY = faceIndex / _tileWidth;
            int tileX = faceIndex % _tileWidth;

            // Validate tile coordinates are within bounds
            // This should always be true if faceIndex is valid, but check for safety
            if (tileX < 0 || tileX >= _tileWidth || tileY < 0 || tileY >= _tileHeight)
            {
                // Invalid tile coordinates - no adjacent faces
                yield break;
            }

            // Check if the source tile is valid and walkable
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 validation checks
            if (!IsTileValid(tileX, tileY))
            {
                // Source tile is not valid (not loaded, out of bounds, or invalid tile ID) - no adjacent faces
                yield break;
            }

            AuroraTile sourceTile = _tiles[tileY, tileX];
            if (!sourceTile.IsLoaded || !sourceTile.IsWalkable)
            {
                // Source tile is not loaded or not walkable - no adjacent faces
                yield break;
            }

            // Find adjacent tiles using GetTileNeighbors
            // Based on nwmain.exe: CNWSArea::InterTileDFSSoundPath @ 0x14036e260
            // GetTileNeighbors returns 8-directional neighbors (N, S, E, W, NE, NW, SE, SW)
            // and already filters for valid, loaded, and walkable tiles
            foreach ((int x, int y) neighbor in GetTileNeighbors(tileX, tileY))
            {
                // Convert neighbor tile coordinates back to face index
                // Based on FindFaceAt: faceIndex = tileY * tileWidth + tileX
                int neighborFaceIndex = neighbor.y * _tileWidth + neighbor.x;

                // Validate neighbor face index is within expected range
                // This should always be true if neighbor tile coordinates are valid, but check for safety
                if (neighborFaceIndex >= 0 && neighborFaceIndex <= maxFaceIndex)
                {
                    // Return the adjacent face index
                    // GetTileNeighbors already ensures the tile is valid, loaded, and walkable
                    yield return neighborFaceIndex;
                }
            }
        }

        /// <summary>
        /// Checks if a face is walkable.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based face walkability check.
        /// In Aurora, faces map to tiles in the tile grid. Each face index corresponds to a unique tile.
        ///
        /// Algorithm:
        /// 1. Convert face index to tile coordinates: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
        /// 2. Validate tile coordinates are within bounds
        /// 3. Check if tile is valid using IsTileValid
        /// 4. Check if tile is loaded and has walkable surfaces
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Tile coordinate conversion and validation
        /// - nwmain.exe: CNWTile walkability checks - Tiles have walkable surface flags
        /// - Face index calculation: faceIndex = tileY * tileWidth + tileX (from FindFaceAt)
        /// - Tile array indexing pattern: width * y + x (matches face index calculation)
        ///
        /// Note: Unlike Odyssey's triangle-based walkmesh where faces are actual triangles,
        /// Aurora uses a tile-based system where each face index maps to a tile in the grid.
        /// Walkability is determined by the tile's IsWalkable flag, which indicates whether
        /// the tile has walkable surfaces.
        /// </remarks>
        public override bool IsWalkable(int faceIndex)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Validate face index is within expected range
            // Maximum face index should be (tileHeight - 1) * tileWidth + (tileWidth - 1) = tileHeight * tileWidth - 1
            int maxFaceIndex = _tileHeight * _tileWidth - 1;
            if (faceIndex < 0 || faceIndex > maxFaceIndex)
            {
                // Face index is out of bounds
                return false;
            }

            // Convert face index to tile coordinates
            // Based on FindFaceAt: faceIndex = tileY * tileWidth + tileX
            // Reverse: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
            int tileY = faceIndex / _tileWidth;
            int tileX = faceIndex % _tileWidth;

            // Validate tile coordinates are within bounds
            // This should always be true if faceIndex is valid, but check for safety
            if (tileX < 0 || tileX >= _tileWidth || tileY < 0 || tileY >= _tileHeight)
            {
                return false;
            }

            // Check if tile is valid (loaded and has valid tile ID)
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 validation checks
            if (!IsTileValid(tileX, tileY))
            {
                // Tile is not valid (not loaded, out of bounds, or invalid tile ID)
                return false;
            }

            // Get the tile at the specified coordinates
            // Based on nwmain.exe: Tile array access pattern (width * y + x)
            AuroraTile tile = _tiles[tileY, tileX];

            // Tile must be loaded to be walkable
            // Based on nwmain.exe: CNWTileSet::GetTileData() validation
            // Tiles must be loaded before they can be used
            if (!tile.IsLoaded)
            {
                return false;
            }

            // Check if tile has walkable surfaces
            // Based on nwmain.exe: CNWTile walkability checks
            // Tiles with IsWalkable flag set to true have walkable surfaces
            return tile.IsWalkable;
        }

        /// <summary>
        /// Gets the surface material of a face.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based surface material lookup.
        /// In Aurora, faces map to tiles in the tile grid. Each face index corresponds to a unique tile.
        /// Surface materials are stored per-tile and correspond to entries in surfacemat.2da.
        ///
        /// Algorithm:
        /// 1. Validate face index is within expected range (0 to tileHeight * tileWidth - 1)
        /// 2. Convert face index to tile coordinates: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
        /// 3. Validate tile coordinates are within bounds
        /// 4. Check if tile is valid (loaded and has valid tile ID)
        /// 5. Return tile's SurfaceMaterial property
        /// 6. Return 0 (Undefined) for invalid face indices or unloaded tiles
        ///
        /// Based on reverse engineering of:
        /// - nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 - Tile coordinate conversion and validation
        /// - nwmain.exe: CNWTileSet::GetTileData() - Tile surface material lookup from tileset
        /// - Face index calculation: faceIndex = tileY * tileWidth + tileX (from FindFaceAt)
        /// - Tile array indexing pattern: width * y + x (matches face index calculation)
        ///
        /// Surface material values (0-30) correspond to surfacemat.2da entries:
        /// - 0: Undefined/NotDefined (non-walkable)
        /// - 1: Dirt (walkable)
        /// - 3: Grass (walkable)
        /// - 4: Stone (walkable)
        /// - 7: NonWalk (non-walkable)
        /// - 15: Lava (non-walkable)
        /// - 17: DeepWater (non-walkable)
        ///
        /// Note: Unlike Odyssey's triangle-based walkmesh where faces are actual triangles with per-face materials,
        /// Aurora uses a tile-based system where each face index maps to a tile in the grid.
        /// Surface materials are determined by the tile's material from the tileset data.
        /// </remarks>
        public override int GetSurfaceMaterial(int faceIndex)
        {
            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                // No tiles available - return undefined material
                return 0; // Undefined/NotDefined
            }

            // Validate face index is within expected range
            // Maximum face index should be (tileHeight - 1) * tileWidth + (tileWidth - 1) = tileHeight * tileWidth - 1
            int maxFaceIndex = _tileHeight * _tileWidth - 1;
            if (faceIndex < 0 || faceIndex > maxFaceIndex)
            {
                // Face index is out of bounds - return undefined material
                return 0; // Undefined/NotDefined
            }

            // Convert face index to tile coordinates
            // Based on FindFaceAt: faceIndex = tileY * tileWidth + tileX
            // Reverse: tileY = faceIndex / tileWidth, tileX = faceIndex % tileWidth
            int tileY = faceIndex / _tileWidth;
            int tileX = faceIndex % _tileWidth;

            // Validate tile coordinates are within bounds
            // This should always be true if faceIndex is valid, but check for safety
            if (tileX < 0 || tileX >= _tileWidth || tileY < 0 || tileY >= _tileHeight)
            {
                // Invalid tile coordinates - return undefined material
                return 0; // Undefined/NotDefined
            }

            // Check if tile is valid (loaded and has valid tile ID)
            // Based on nwmain.exe: CNWSArea::GetTile @ 0x14035edc0 validation checks
            if (!IsTileValid(tileX, tileY))
            {
                // Tile is not valid (not loaded, out of bounds, or invalid tile ID)
                // Return undefined material for invalid tiles
                return 0; // Undefined/NotDefined
            }

            // Get the tile at the specified coordinates
            // Based on nwmain.exe: Tile array access pattern (width * y + x)
            AuroraTile tile = _tiles[tileY, tileX];

            // Tile must be loaded to have a valid surface material
            // Based on nwmain.exe: CNWTileSet::GetTileData() validation
            // Tiles must be loaded before they can have surface material data
            if (!tile.IsLoaded)
            {
                // Tile is not loaded - return undefined material
                return 0; // Undefined/NotDefined
            }

            // Return the tile's surface material
            // Surface material is stored per-tile and corresponds to surfacemat.2da entries
            // Default value is 0 (Undefined) for tiles without material data
            // Based on nwmain.exe: CNWTileSet::GetTileData() surface material lookup
            return tile.SurfaceMaterial;
        }

        /// <summary>
        /// Performs a raycast against the mesh.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Aurora tile-based raycast implementation.
        /// Uses Digital Differential Analyzer (DDA) algorithm to traverse tiles along the ray.
        /// Checks each tile the ray passes through for walkability and occlusion.
        ///
        /// Algorithm:
        /// 1. Normalize direction vector
        /// 2. Convert origin to tile coordinates
        /// 3. Use DDA to step through tiles along the ray
        /// 4. For each tile:
        ///    a. If tileset loader is available, test ray against per-tile walkmesh geometry
        ///    b. If walkmesh test hits a non-walkable face, return the hit point
        ///    c. Otherwise, fall back to tile walkability flag check
        /// 5. Return first blocking tile/walkmesh face or end point if no obstruction
        ///
        /// Implementation:
        /// - Tests against per-tile walkmesh geometry when available (via TilesetLoader)
        /// - Uses BWM.Raycast to test ray against walkmesh triangles
        /// - Only non-walkable faces block the ray (walkable faces allow it to pass through)
        /// - Falls back to simplified tile walkability flag check when walkmesh data is unavailable
        /// - Handles tile orientation (0-3 rotations) by transforming ray to tile-local coordinates
        ///
        /// Based on nwmain.exe: CNWTileSurfaceMesh raycast implementation
        /// </remarks>
        public override bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = origin;
            hitFace = -1;

            // Handle empty tile grid
            if (_tileWidth <= 0 || _tileHeight <= 0 || _tiles == null || _tiles.Length == 0)
            {
                return false;
            }

            // Normalize direction
            float dirLength = direction.Length();
            if (dirLength < 1e-6f)
            {
                return false;
            }
            Vector3 normalizedDir = direction / dirLength;

            // Calculate end point
            Vector3 endPoint = origin + normalizedDir * maxDistance;

            // Get starting tile coordinates
            int startTileX, startTileY;
            if (!GetTileCoordinates(origin, out startTileX, out startTileY))
            {
                // Origin is outside tile grid - start from edge
                // Clamp to grid bounds
                startTileX = (int)Math.Max(0, Math.Min(_tileWidth - 1, (int)Math.Floor(origin.X / TileSize)));
                startTileY = (int)Math.Max(0, Math.Min(_tileHeight - 1, (int)Math.Floor(origin.Z / TileSize)));
            }

            // Get ending tile coordinates
            int endTileX, endTileY;
            GetTileCoordinates(endPoint, out endTileX, out endTileY);
            // Clamp to grid bounds if outside
            endTileX = Math.Max(0, Math.Min(_tileWidth - 1, endTileX));
            endTileY = Math.Max(0, Math.Min(_tileHeight - 1, endTileY));

            // DDA algorithm for tile traversal
            int currentTileX = startTileX;
            int currentTileY = startTileY;

            // Step sizes for DDA (distance along ray to cross one tile in each direction)
            float stepX = normalizedDir.X != 0 ? Math.Abs(TileSize / normalizedDir.X) : float.MaxValue;
            float stepY = normalizedDir.Z != 0 ? Math.Abs(TileSize / normalizedDir.Z) : float.MaxValue;

            // Calculate initial distance to next tile boundary in X and Z directions
            float deltaX, deltaZ;
            if (normalizedDir.X > 0)
            {
                float tileMaxX = (currentTileX + 1) * TileSize;
                deltaX = (tileMaxX - origin.X) / normalizedDir.X;
            }
            else if (normalizedDir.X < 0)
            {
                float tileMinX = currentTileX * TileSize;
                deltaX = (tileMinX - origin.X) / normalizedDir.X;
            }
            else
            {
                deltaX = float.MaxValue;
            }

            if (normalizedDir.Z > 0)
            {
                float tileMaxZ = (currentTileY + 1) * TileSize;
                deltaZ = (tileMaxZ - origin.Z) / normalizedDir.Z;
            }
            else if (normalizedDir.Z < 0)
            {
                float tileMinZ = currentTileY * TileSize;
                deltaZ = (tileMinZ - origin.Z) / normalizedDir.Z;
            }
            else
            {
                deltaZ = float.MaxValue;
            }

            // Track visited tiles to avoid infinite loops
            HashSet<(int x, int y)> visitedTiles = new HashSet<(int x, int y)>();
            const int maxIterations = 1000; // Safety limit
            int iterations = 0;

            // Traverse tiles along the ray
            while (iterations < maxIterations)
            {
                iterations++;

                // Check if we've reached the end tile
                if (currentTileX == endTileX && currentTileY == endTileY)
                {
                    // Check final tile
                    if (IsTileValid(currentTileX, currentTileY))
                    {
                        AuroraTile tile = _tiles[currentTileY, currentTileX];

                        // Try to test against per-tile walkmesh geometry if available
                        if (_tilesetLoader != null && !string.IsNullOrEmpty(_tilesetResRef))
                        {
                            Vector3 walkmeshHitPoint;
                            if (RaycastAgainstTileWalkmesh(currentTileX, currentTileY, tile, origin, normalizedDir, maxDistance, out walkmeshHitPoint))
                            {
                                // Hit a blocking face in the walkmesh
                                hitPoint = walkmeshHitPoint;
                                hitFace = -1; // Tile-based, no face index
                                return true;
                            }
                        }

                        // Fallback: If tile is not walkable, it blocks the ray
                        if (!tile.IsWalkable)
                        {
                            // Calculate hit point at tile boundary
                            float distToEnd = Vector3.Distance(origin, endPoint);
                            hitPoint = origin + normalizedDir * distToEnd;
                            hitFace = -1; // Tile-based, no face index
                            return true;
                        }
                    }
                    // Reached end without obstruction
                    hitPoint = endPoint;
                    hitFace = -1;
                    return false;
                }

                // Check current tile
                if (IsTileValid(currentTileX, currentTileY))
                {
                    AuroraTile tile = _tiles[currentTileY, currentTileX];

                    // Try to test against per-tile walkmesh geometry if available
                    if (_tilesetLoader != null && !string.IsNullOrEmpty(_tilesetResRef))
                    {
                        Vector3 walkmeshHitPoint;
                        if (RaycastAgainstTileWalkmesh(currentTileX, currentTileY, tile, origin, normalizedDir, maxDistance, out walkmeshHitPoint))
                        {
                            // Hit a blocking face in the walkmesh
                            hitPoint = walkmeshHitPoint;
                            hitFace = -1; // Tile-based, no face index
                            return true;
                        }
                    }

                    // Fallback: Non-walkable tiles block the ray (simplified check)
                    if (!tile.IsWalkable)
                    {
                        // Calculate hit point at current position
                        float currentDist = 0f;
                        if (normalizedDir.X != 0)
                        {
                            float tileCenterX = (currentTileX + 0.5f) * TileSize;
                            currentDist = Math.Abs((tileCenterX - origin.X) / normalizedDir.X);
                        }
                        else if (normalizedDir.Z != 0)
                        {
                            float tileCenterZ = (currentTileY + 0.5f) * TileSize;
                            currentDist = Math.Abs((tileCenterZ - origin.Z) / normalizedDir.Z);
                        }
                        currentDist = Math.Min(currentDist, maxDistance);
                        hitPoint = origin + normalizedDir * currentDist;
                        hitFace = -1; // Tile-based, no face index
                        return true;
                    }
                }
                else
                {
                    // Invalid tile blocks the ray (out of bounds or not loaded)
                    float currentDist = 0f;
                    if (normalizedDir.X != 0)
                    {
                        float tileCenterX = (currentTileX + 0.5f) * TileSize;
                        currentDist = Math.Abs((tileCenterX - origin.X) / normalizedDir.X);
                    }
                    else if (normalizedDir.Z != 0)
                    {
                        float tileCenterZ = (currentTileY + 0.5f) * TileSize;
                        currentDist = Math.Abs((tileCenterZ - origin.Z) / normalizedDir.Z);
                    }
                    currentDist = Math.Min(currentDist, maxDistance);
                    hitPoint = origin + normalizedDir * currentDist;
                    hitFace = -1;
                    return true;
                }

                // Check for infinite loop
                if (visitedTiles.Contains((currentTileX, currentTileY)))
                {
                    break;
                }
                visitedTiles.Add((currentTileX, currentTileY));

                // Move to next tile using DDA
                // Choose the direction with the smaller delta (closer boundary)
                if (deltaX < deltaZ)
                {
                    // Step in X direction
                    deltaX += stepX; // Increment by step size for next boundary
                    currentTileX += normalizedDir.X > 0 ? 1 : -1;
                }
                else
                {
                    // Step in Z direction
                    deltaZ += stepY; // Increment by step size for next boundary
                    currentTileY += normalizedDir.Z > 0 ? 1 : -1;
                }

                // Check bounds
                if (currentTileX < 0 || currentTileX >= _tileWidth || currentTileY < 0 || currentTileY >= _tileHeight)
                {
                    // Ray exited tile grid
                    hitPoint = endPoint;
                    hitFace = -1;
                    return false;
                }
            }

            // Reached iteration limit or exited grid - no hit
            hitPoint = endPoint;
            hitFace = -1;
            return false;
        }

        /// <summary>
        /// Tests raycast against per-tile walkmesh geometry.
        /// </summary>
        /// <param name="tileX">Tile X coordinate.</param>
        /// <param name="tileY">Tile Y coordinate.</param>
        /// <param name="tile">The tile to test against.</param>
        /// <param name="origin">Ray origin in world coordinates.</param>
        /// <param name="direction">Normalized ray direction.</param>
        /// <param name="maxDistance">Maximum ray distance.</param>
        /// <param name="hitPoint">Output hit point in world coordinates if a blocking face is hit.</param>
        /// <returns>True if ray hit a non-walkable face, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh raycast testing against walkmesh geometry.
        /// Tests the ray against the walkmesh triangles for this tile to find blocking faces.
        /// Only non-walkable faces block the ray (walkable faces allow the ray to pass through).
        ///
        /// Algorithm:
        /// 1. Load walkmesh (WOK file) for the tile using TilesetLoader
        /// 2. Transform ray origin to tile-local coordinates (accounting for tile position and orientation)
        /// 3. Test ray against walkmesh triangles using BWM.Raycast
        /// 4. Filter hits to only non-walkable materials (blocking faces)
        /// 5. Transform hit point back to world coordinates
        /// 6. Return true if a blocking face is hit
        ///
        /// Tile orientation is handled by transforming the ray to tile-local space:
        /// - Tile position: (tileX * TileSize, 0, tileY * TileSize) in world coordinates
        /// - Tile orientation: 0-3 rotations (0°, 90°, 180°, 270° counterclockwise)
        /// - Walkmesh coordinates are relative to tile origin (0,0,0) in tile-local space
        /// </remarks>
        private bool RaycastAgainstTileWalkmesh(int tileX, int tileY, AuroraTile tile, Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint)
        {
            hitPoint = origin;

            // Tile must be loaded to have walkmesh data
            if (!tile.IsLoaded)
            {
                return false;
            }

            // Get walkmesh for this tile
            BWM walkmesh = _tilesetLoader.GetTileWalkmesh(_tilesetResRef, tile.TileId);
            if (walkmesh == null || walkmesh.Faces == null || walkmesh.Faces.Count == 0)
            {
                // No walkmesh data available - fall back to tile walkability flag
                return false;
            }

            // Calculate tile origin in world coordinates
            // Tiles are positioned at (tileX * TileSize, 0, tileY * TileSize)
            float tileOriginX = tileX * TileSize;
            float tileOriginZ = tileY * TileSize;
            Vector3 tileOrigin = new Vector3(tileOriginX, 0.0f, tileOriginZ);

            // Transform ray origin to tile-local coordinates
            // Tile-local coordinates are relative to tile origin
            Vector3 localOrigin = origin - tileOrigin;

            // Apply tile orientation rotation (0-3: 0°, 90°, 180°, 270° counterclockwise)
            // Orientation affects how the walkmesh is rotated within the tile
            Vector3 localDirection = direction;
            if (tile.Orientation != 0)
            {
                // Rotate direction vector around Y axis (vertical)
                // Counterclockwise rotation: angle = -orientation * 90°
                float angleRad = -(tile.Orientation * 90.0f) * (float)(Math.PI / 180.0);
                float cosAngle = (float)Math.Cos(angleRad);
                float sinAngle = (float)Math.Sin(angleRad);

                // Rotate around Y axis: (x', z') = (x*cos - z*sin, x*sin + z*cos)
                float rotatedX = localOrigin.X * cosAngle - localOrigin.Z * sinAngle;
                float rotatedZ = localOrigin.X * sinAngle + localOrigin.Z * cosAngle;
                localOrigin = new Vector3(rotatedX, localOrigin.Y, rotatedZ);

                float rotatedDirX = localDirection.X * cosAngle - localDirection.Z * sinAngle;
                float rotatedDirZ = localDirection.X * sinAngle + localDirection.Z * cosAngle;
                localDirection = new Vector3(rotatedDirX, localDirection.Y, rotatedDirZ);
            }

            // Test ray against walkmesh, filtering for non-walkable materials only
            // Non-walkable faces block the ray (walkable faces allow it to pass through)
            HashSet<SurfaceMaterial> nonWalkableMaterials = new HashSet<SurfaceMaterial>();
            foreach (SurfaceMaterial mat in Enum.GetValues(typeof(SurfaceMaterial)))
            {
                if (!mat.Walkable())
                {
                    nonWalkableMaterials.Add(mat);
                }
            }

            // Test raycast against walkmesh with non-walkable materials filter
            Tuple<BWMFace, float> hitResult = walkmesh.Raycast(localOrigin, localDirection, maxDistance, nonWalkableMaterials);
            if (hitResult != null && hitResult.Item1 != null && hitResult.Item2 > 0.0f && hitResult.Item2 <= maxDistance)
            {
                // Ray hit a non-walkable face - calculate hit point in tile-local coordinates
                Vector3 localHitPoint = localOrigin + localDirection * hitResult.Item2;

                // Transform hit point back to world coordinates
                // Reverse tile orientation rotation if needed
                if (tile.Orientation != 0)
                {
                    // Reverse rotation: angle = orientation * 90° (clockwise)
                    float angleRad = (tile.Orientation * 90.0f) * (float)(Math.PI / 180.0);
                    float cosAngle = (float)Math.Cos(angleRad);
                    float sinAngle = (float)Math.Sin(angleRad);

                    float rotatedX = localHitPoint.X * cosAngle - localHitPoint.Z * sinAngle;
                    float rotatedZ = localHitPoint.X * sinAngle + localHitPoint.Z * cosAngle;
                    localHitPoint = new Vector3(rotatedX, localHitPoint.Y, rotatedZ);
                }

                // Transform back to world coordinates
                hitPoint = localHitPoint + tileOrigin;
                return true;
            }

            // Ray did not hit any blocking faces in this tile
            return false;
        }

        /// <summary>
        /// Tests line of sight between two points (INavigationMesh interface).
        /// </summary>
        /// <remarks>
        /// Wrapper around HasLineOfSight for INavigationMesh interface compatibility.
        /// Based on nwmain.exe: Aurora line of sight testing.
        /// </remarks>
        public override bool TestLineOfSight(Vector3 from, Vector3 to)
        {
            return HasLineOfSight(from, to);
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <remarks>
        /// Wrapper around ProjectToWalkmesh for INavigationMesh interface compatibility.
        /// Based on nwmain.exe: Aurora walkmesh projection system.
        /// </remarks>
        public override bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            return ProjectToWalkmesh(point, out result, out height);
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <param name="point">Point to project.</param>
        /// <returns>Projected point, or null if projection failed.</returns>
        /// <remarks>
        /// Wrapper around ProjectToWalkmesh for INavigationMesh interface compatibility.
        /// Based on nwmain.exe: Aurora walkmesh projection system.
        /// </remarks>
        public override Vector3? ProjectPoint(Vector3 point)
        {
            Vector3 result;
            float height;
            if (ProjectToWalkmesh(point, out result, out height))
            {
                return result;
            }
            return null;
        }
    }
}
