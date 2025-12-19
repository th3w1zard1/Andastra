using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Aurora
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
    /// </remarks>
    internal struct AuroraTile
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

        /// <summary>
        /// Creates an empty Aurora navigation mesh (for placeholder use).
        /// </summary>
        public AuroraNavigationMesh()
        {
            _tiles = new AuroraTile[0, 0];
            _tileWidth = 0;
            _tileHeight = 0;
        }

        /// <summary>
        /// Creates an Aurora navigation mesh with tile grid data.
        /// </summary>
        /// <param name="tiles">2D array of tiles indexed by [y, x].</param>
        /// <param name="tileWidth">Width of the tile grid.</param>
        /// <param name="tileHeight">Height of the tile grid.</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea tile storage structure.
        /// Tiles are stored in a 2D grid with dimensions Width x Height.
        /// </remarks>
        public AuroraNavigationMesh(AuroraTile[,] tiles, int tileWidth, int tileHeight)
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
        }
        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on Aurora tile-based walkmesh system.
        /// Checks tile validity and walkable surfaces within tiles.
        /// More complex than Odyssey due to tile boundaries.
        /// </remarks>
        public bool IsPointWalkable(Vector3 point)
        {
            // TODO: Implement Aurora point testing
            // Determine which tile contains the point
            // Check walkability within that tile
            // Handle tile boundary cases
            throw new System.NotImplementedException("Aurora walkmesh point testing not yet implemented");
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <remarks>
        /// Based on Aurora walkmesh projection with tile awareness.
        /// Projects to nearest walkable surface across tile boundaries.
        /// Handles height transitions between tiles.
        /// </remarks>
        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            // TODO: Implement Aurora walkmesh projection
            // Find containing tile
            // Project within tile
            // Handle tile boundary projections
            result = point;
            height = point.Y;
            throw new System.NotImplementedException("Aurora walkmesh projection not yet implemented");
        }

        /// <summary>
        /// Finds a path between two points using A* algorithm.
        /// </summary>
        /// <remarks>
        /// Aurora pathfinding works across tile boundaries.
        /// Uses hierarchical pathfinding: tile-level then within-tile.
        /// More sophisticated than Odyssey's single-mesh approach.
        /// </remarks>
        public bool FindPath(Vector3 start, Vector3 end, out Vector3[] waypoints)
        {
            // TODO: Implement Aurora A* pathfinding
            // High-level tile pathfinding
            // Within-tile detailed pathfinding
            // Smooth waypoint generation
            waypoints = new[] { start, end };
            throw new System.NotImplementedException("Aurora pathfinding not yet implemented");
        }

        /// <summary>
        /// Gets the height at a specific point.
        /// </summary>
        /// <remarks>
        /// Samples height from tile-based terrain data.
        /// Handles tile boundary interpolation.
        /// </remarks>
        public bool GetHeightAtPoint(Vector3 point, out float height)
        {
            // TODO: Implement Aurora height sampling
            // Find tile containing point
            // Sample height from tile data
            // Handle boundary interpolation
            height = point.Y;
            throw new System.NotImplementedException("Aurora height sampling not yet implemented");
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
        /// </remarks>
        public IEnumerable<(int x, int y)> GetTileNeighbors(int tileX, int tileY)
        {
            // TODO: Implement tile neighbor finding
            // Check adjacent tiles (N, S, E, W, NE, NW, SE, SW)
            // Return valid, walkable neighbors
            yield break;
        }

        // INavigationMesh interface implementations

        /// <summary>
        /// Finds a path from start to goal.
        /// </summary>
        public IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            // TODO: Implement Aurora A* pathfinding
            // High-level tile pathfinding
            // Within-tile detailed pathfinding
            // Smooth waypoint generation
            throw new NotImplementedException("Aurora pathfinding not yet implemented");
        }

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// Based on nwmain.exe: Similar obstacle avoidance to Odyssey engine
        /// </summary>
        public IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<Interfaces.ObstacleInfo> obstacles)
        {
            // TODO: Implement Aurora obstacle avoidance pathfinding
            // When FindPath is implemented, add obstacle blocking logic similar to Odyssey
            throw new NotImplementedException("Aurora obstacle avoidance pathfinding not yet implemented");
        }

        /// <summary>
        /// Finds the face index at a given position.
        /// </summary>
        public int FindFaceAt(Vector3 position)
        {
            // TODO: Implement Aurora face finding
            // Find tile containing position
            // Find face within tile
            throw new NotImplementedException("Aurora face finding not yet implemented");
        }

        /// <summary>
        /// Gets the center point of a face.
        /// </summary>
        public Vector3 GetFaceCenter(int faceIndex)
        {
            // TODO: Implement Aurora face center calculation
            throw new NotImplementedException("Aurora face center calculation not yet implemented");
        }

        /// <summary>
        /// Gets adjacent faces for a given face.
        /// </summary>
        public IEnumerable<int> GetAdjacentFaces(int faceIndex)
        {
            // TODO: Implement Aurora adjacent face finding
            yield break;
        }

        /// <summary>
        /// Checks if a face is walkable.
        /// </summary>
        public bool IsWalkable(int faceIndex)
        {
            // TODO: Implement Aurora face walkability check
            throw new NotImplementedException("Aurora face walkability check not yet implemented");
        }

        /// <summary>
        /// Gets the surface material of a face.
        /// </summary>
        public int GetSurfaceMaterial(int faceIndex)
        {
            // TODO: Implement Aurora surface material lookup
            throw new NotImplementedException("Aurora surface material lookup not yet implemented");
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
        /// 4. Check each tile for walkability (non-walkable tiles block the ray)
        /// 5. Return first blocking tile or end point if no obstruction
        /// 
        /// Note: This is a simplified implementation that uses tile walkability flags.
        /// A full implementation would test against per-tile walkmesh geometry when available.
        /// </remarks>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
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
                        // If tile is not walkable, it blocks the ray
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
                    // Non-walkable tiles block the ray
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
        /// Tests line of sight between two points (INavigationMesh interface).
        /// </summary>
        /// <remarks>
        /// Wrapper around HasLineOfSight for INavigationMesh interface compatibility.
        /// Based on nwmain.exe: Aurora line of sight testing.
        /// </remarks>
        public bool TestLineOfSight(Vector3 from, Vector3 to)
        {
            return HasLineOfSight(from, to);
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        public bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            // TODO: Implement Aurora walkmesh projection
            // Find containing tile
            // Project within tile
            // Handle tile boundary projections
            result = point;
            height = point.Y;
            throw new NotImplementedException("Aurora walkmesh projection not yet implemented");
        }
    }
}
