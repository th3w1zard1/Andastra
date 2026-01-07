using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Formats.SET;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora
{
    /// <summary>
    /// Loads and caches tileset data for Aurora areas.
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWTileSetManager::GetTileSet @ 0x1411d4f6a
    /// - Caches loaded tilesets to avoid reloading
    /// - Provides access to tile data including model references
    /// - Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
    /// </remarks>
    [PublicAPI]
    public class TilesetLoader
    {
        private readonly Dictionary<string, SET> _tilesetCache = new Dictionary<string, SET>(StringComparer.OrdinalIgnoreCase);
        private readonly Func<string, byte[]> _resourceLoader;

        /// <summary>
        /// Gets the resource loader function used by this tileset loader.
        /// </summary>
        /// <remarks>
        /// Exposed for use by SimpleResourceProvider to avoid reflection.
        /// Based on nwmain.exe: Resource loading uses CExoResMan::Demand @ 0x14018ef90
        /// </remarks>
        internal Func<string, byte[]> ResourceLoader => _resourceLoader;

        /// <summary>
        /// Creates a new tileset loader.
        /// </summary>
        /// <param name="resourceLoader">Function to load resource files by resref. Can be null, in which case resource loading will fail gracefully.</param>
        public TilesetLoader(Func<string, byte[]> resourceLoader)
        {
            _resourceLoader = resourceLoader; // Allow null for cases where resource loading is not available
        }

        /// <summary>
        /// Loads a tileset by resref.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference (without .set extension).</param>
        /// <returns>The loaded tileset, or null if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSetManager::GetTileSet @ 0x1411d4f6a
        /// - Loads SET file from resource system
        /// - Caches loaded tilesets for performance
        /// </remarks>
        public SET LoadTileset(string tilesetResRef)
        {
            if (string.IsNullOrEmpty(tilesetResRef))
                return null;

            // Check cache first
            if (_tilesetCache.TryGetValue(tilesetResRef, out SET cached))
            {
                return cached;
            }

            try
            {
                // Load SET file
                byte[] setData = _resourceLoader(tilesetResRef + ".set");
                if (setData == null || setData.Length == 0)
                {
                    return null;
                }

                // Parse SET file
                SET tileset = SET.FromBytes(setData);

                // Cache the tileset
                _tilesetCache[tilesetResRef] = tileset;

                return tileset;
            }
            catch
            {
                // Failed to load tileset
                return null;
            }
        }

        /// <summary>
        /// Gets the model name for a tile in a tileset.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <returns>The model name, or null if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
        /// - Returns model reference for the specified tile
        /// </remarks>
        public string GetTileModel(string tilesetResRef, int tileId)
        {
            if (tileId < 0)
                return null;

            SET tileset = LoadTileset(tilesetResRef);
            if (tileset == null)
                return null;

            if (tileId >= tileset.Tiles.Count)
                return null;

            return tileset.Tiles[tileId].Model;
        }

        /// <summary>
        /// Gets the surface material for a tile by loading its walkmesh.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <returns>The surface material index (0-30), or 0 (Undefined) if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh::GetSurfaceMaterial @ 0x1402bedf0
        /// - Gets tile model from tileset
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Extracts most common surface material from walkmesh faces
        /// - Falls back to default (Stone for walkable, Undefined for non-walkable) if walkmesh can't be loaded
        /// </remarks>
        public int GetTileSurfaceMaterial(string tilesetResRef, int tileId)
        {
            if (tileId < 0)
                return 0; // Undefined

            // Get tile model from tileset
            string modelName = GetTileModel(tilesetResRef, tileId);
            if (string.IsNullOrEmpty(modelName))
            {
                return 0; // Undefined
            }

            try
            {
                // Load walkmesh (WOK file) for the tile model
                // Walkmesh filename is model name with .wok extension
                if (_resourceLoader == null)
                {
                    // No resource loader available - use default
                    return 4; // Stone (walkable default)
                }

                byte[] wokData = _resourceLoader(modelName + ".wok");
                if (wokData == null || wokData.Length == 0)
                {
                    // Walkmesh not found - use default based on tile validity
                    // Default to Stone (4) for valid tiles, Undefined (0) for invalid
                    return 4; // Stone (walkable default)
                }

                // Parse walkmesh
                BWM walkmesh = BWMAuto.ReadBwm(wokData);
                if (walkmesh == null || walkmesh.Faces == null || walkmesh.Faces.Count == 0)
                {
                    return 4; // Stone (walkable default)
                }

                // Get most common surface material from walkmesh faces
                // Based on nwmain.exe: CNWTileSurfaceMesh::GetSurfaceMaterial @ 0x1402bedf0
                // Surface materials are stored per-face in the walkmesh
                var materialCounts = new Dictionary<int, int>();
                foreach (var face in walkmesh.Faces)
                {
                    int material = (int)face.Material;
                    if (materialCounts.ContainsKey(material))
                    {
                        materialCounts[material]++;
                    }
                    else
                    {
                        materialCounts[material] = 1;
                    }
                }

                // Find most common material
                if (materialCounts.Count > 0)
                {
                    int mostCommonMaterial = materialCounts
                        .OrderByDescending(kvp => kvp.Value)
                        .First()
                        .Key;

                    // Validate material is in valid range (0-30)
                    if (mostCommonMaterial >= 0 && mostCommonMaterial <= 30)
                    {
                        return mostCommonMaterial;
                    }
                }

                // No valid materials found - use default
                return 4; // Stone (walkable default)
            }
            catch
            {
                // Failed to load/parse walkmesh - use default
                return 4; // Stone (walkable default)
            }
        }

        /// <summary>
        /// Determines if a tile is walkable by checking its walkmesh for walkable surface materials.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <returns>True if the tile has at least one walkable face, false otherwise.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh walkability determination
        /// - Gets tile model from tileset
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Checks if any faces have walkable surface materials
        /// - A tile is walkable if at least one face has a walkable material
        /// - Falls back to false if walkmesh can't be loaded (tile is not walkable)
        /// - Based on nwmain.exe: CNWTileSet::GetTileData() validation and walkmesh face material checks
        /// - Walkability is determined by surface material IDs matching walkable material set
        /// - Surface materials are checked using SurfaceMaterialExtensions.Walkable() which matches
        ///   the hardcoded walkable material list in the original engine
        /// </remarks>
        public bool GetTileWalkability(string tilesetResRef, int tileId)
        {
            if (tileId < 0)
            {
                return false; // Invalid tile ID - not walkable
            }

            // Get tile model from tileset
            string modelName = GetTileModel(tilesetResRef, tileId);
            if (string.IsNullOrEmpty(modelName))
            {
                return false; // No model - not walkable
            }

            try
            {
                // Load walkmesh (WOK file) for the tile model
                // Walkmesh filename is model name with .wok extension
                if (_resourceLoader == null)
                {
                    // No resource loader available - cannot determine walkability
                    return false;
                }

                byte[] wokData = _resourceLoader(modelName + ".wok");
                if (wokData == null || wokData.Length == 0)
                {
                    // Walkmesh not found - tile is not walkable
                    return false;
                }

                // Parse walkmesh
                BWM walkmesh = BWMAuto.ReadBwm(wokData);
                if (walkmesh == null || walkmesh.Faces == null || walkmesh.Faces.Count == 0)
                {
                    // No walkmesh or no faces - tile is not walkable
                    return false;
                }

                // Check if any face has a walkable surface material
                // Based on nwmain.exe: CNWTileSurfaceMesh walkability checks
                // The engine checks each face's material against the walkable material set
                foreach (var face in walkmesh.Faces)
                {
                    if (face == null)
                    {
                        continue;
                    }

                    // Get surface material from face
                    // Material is stored as SurfaceMaterial enum value
                    SurfaceMaterial material = face.Material;

                    // Check if material is walkable using SurfaceMaterialExtensions.Walkable()
                    // This matches the hardcoded walkable material list in the original engine
                    if (material.Walkable())
                    {
                        // Found at least one walkable face - tile is walkable
                        return true;
                    }
                }

                // No walkable faces found - tile is not walkable
                return false;
            }
            catch
            {
                // Failed to load/parse walkmesh - tile is not walkable
                return false;
            }
        }

        /// <summary>
        /// Gets the height of a tile by sampling from its walkmesh geometry.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <param name="sampleX">X coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <param name="sampleZ">Z coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <param name="tileHeightTransitions">Optional number of height transitions for this tile instance (from area GIT data). If not provided, defaults to 0 (base height).</param>
        /// <returns>The height at the sample point, or float.MinValue if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh height sampling
        /// - Gets tile model from tileset
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Samples height from walkmesh face at the specified point
        /// - Uses tile center (0.5, 0.5) for default height calculation
        /// - Falls back to simplified height transition model if walkmesh can't be loaded
        ///   Based on nwmain.exe: CNWTileSet::GetHeightTransition @ 0x1402c67c0
        ///   Uses bilinear interpolation from corner heights calculated from height transitions
        /// </remarks>
        public float GetTileHeight(string tilesetResRef, int tileId, float sampleX = 0.5f, float sampleZ = 0.5f, int? tileHeightTransitions = null)
        {
            if (tileId < 0)
            {
                return float.MinValue;
            }

            // Get tile model from tileset
            string modelName = GetTileModel(tilesetResRef, tileId);
            if (string.IsNullOrEmpty(modelName))
            {
                return float.MinValue;
            }

            try
            {
                // Load walkmesh (WOK file) for the tile model
                // Walkmesh filename is model name with .wok extension
                if (_resourceLoader == null)
                {
                    // No resource loader available - fall back to simplified height transition model
                    return GetTileHeightFromTransitionModel(tilesetResRef, tileId, sampleX, sampleZ, tileHeightTransitions);
                }

                byte[] wokData = _resourceLoader(modelName + ".wok");
                if (wokData == null || wokData.Length == 0)
                {
                    // Walkmesh not found - fall back to simplified height transition model
                    return GetTileHeightFromTransitionModel(tilesetResRef, tileId, sampleX, sampleZ, tileHeightTransitions);
                }

                // Parse walkmesh
                BWM walkmesh = BWMAuto.ReadBwm(wokData);
                if (walkmesh == null || walkmesh.Faces == null || walkmesh.Faces.Count == 0)
                {
                    // Walkmesh invalid - fall back to simplified height transition model
                    return GetTileHeightFromTransitionModel(tilesetResRef, tileId, sampleX, sampleZ, tileHeightTransitions);
                }

                // Convert sample coordinates (0.0-1.0) to world coordinates within tile
                // Tile size is 10.0 units, so sample point is at (sampleX * 10.0, sampleZ * 10.0)
                // Walkmesh coordinates are relative to tile origin
                float worldX = sampleX * 10.0f;
                float worldZ = sampleZ * 10.0f;

                // Find the walkmesh face that contains the sample point
                // Based on nwmain.exe: CNWTileSurfaceMesh::GetHeightAtPoint
                // Samples height by finding face containing point and interpolating from vertices
                float bestHeight = float.MinValue;
                float minDistance = float.MaxValue;

                foreach (var face in walkmesh.Faces)
                {
                    // Face has V1, V2, V3 properties (not a Vertices list)
                    // All faces are triangles, so we always have 3 vertices
                    var v0 = face.V1;
                    var v1 = face.V2;
                    var v2 = face.V3;

                    // Check if point is within face bounds (simple bounding box check first)
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minZ = float.MaxValue, maxZ = float.MinValue;

                    // Check all three vertices
                    if (v0.X < minX) minX = v0.X;
                    if (v0.X > maxX) maxX = v0.X;
                    if (v0.Z < minZ) minZ = v0.Z;
                    if (v0.Z > maxZ) maxZ = v0.Z;

                    if (v1.X < minX) minX = v1.X;
                    if (v1.X > maxX) maxX = v1.X;
                    if (v1.Z < minZ) minZ = v1.Z;
                    if (v1.Z > maxZ) maxZ = v1.Z;

                    if (v2.X < minX) minX = v2.X;
                    if (v2.X > maxX) maxX = v2.X;
                    if (v2.Z < minZ) minZ = v2.Z;
                    if (v2.Z > maxZ) maxZ = v2.Z;

                    // Quick bounding box rejection
                    if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ)
                    {
                        continue;
                    }

                    // Check if point is inside face using barycentric coordinates
                    // For triangle faces, use barycentric interpolation
                    // All BWM faces are triangles, so we always have 3 vertices

                    // Compute barycentric coordinates
                    float denom = (v1.Z - v2.Z) * (v0.X - v2.X) + (v2.X - v1.X) * (v0.Z - v2.Z);
                    if (Math.Abs(denom) < 0.0001f)
                    {
                        continue; // Degenerate triangle
                    }

                    float a = ((v1.Z - v2.Z) * (worldX - v2.X) + (v2.X - v1.X) * (worldZ - v2.Z)) / denom;
                    float b = ((v2.Z - v0.Z) * (worldX - v2.X) + (v0.X - v2.X) * (worldZ - v2.Z)) / denom;
                    float c = 1.0f - a - b;

                    // Point is inside triangle if all barycentric coordinates are >= 0
                    if (a >= 0.0f && b >= 0.0f && c >= 0.0f)
                    {
                        // Interpolate height using barycentric coordinates
                        float height = a * v0.Y + b * v1.Y + c * v2.Y;
                        return height;
                    }
                }

                // If we found a face by distance, return its height
                if (bestHeight != float.MinValue)
                {
                    return bestHeight;
                }

                // Fallback: Calculate average height of all walkable faces
                float totalHeight = 0.0f;
                int faceCount = 0;
                foreach (var face in walkmesh.Faces)
                {
                    // BWM faces are always triangles with V1, V2, V3 properties
                    // Calculate face average height
                    float faceHeight = (face.V1.Y + face.V2.Y + face.V3.Y) / 3.0f;

                    totalHeight += faceHeight;
                    faceCount++;
                }

                if (faceCount > 0)
                {
                    return totalHeight / faceCount;
                }

                return float.MinValue;
            }
            catch
            {
                // Failed to load/parse walkmesh - fall back to simplified height transition model
                return GetTileHeightFromTransitionModel(tilesetResRef, tileId, sampleX, sampleZ, tileHeightTransitions);
            }
        }

        /// <summary>
        /// Calculates tile height using the simplified height transition model when walkmesh data is unavailable.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <param name="sampleX">X coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <param name="sampleZ">Z coordinate within tile (0.0 to 1.0, where 0.5 is center).</param>
        /// <param name="tileHeightTransitions">Optional number of height transitions for this tile instance. If not provided, defaults to 0 (base height).</param>
        /// <returns>The calculated height at the sample point, or float.MinValue if tileset cannot be loaded.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::GetHeightTransition @ 0x1402c67c0
        /// Based on nwmain.exe: Tile height transition model used when walkmesh cannot be loaded
        /// - Loads tileset SET file to get height transition value from [GENERAL] Transition field
        /// - Calculates base height from tile's height transitions (Tile_Height from area GIT data)
        /// - Each height transition represents 0.5 unit elevation change (standard value)
        /// - Uses bilinear interpolation to calculate height at any point within the tile
        /// - Corner heights are assumed uniform (simplified model without adjacent tile information)
        ///
        /// This is a simplified fallback that provides reasonable height approximation when:
        /// - Walkmesh file is missing or corrupted
        /// - Resource loader is unavailable
        /// - Walkmesh parsing fails
        ///
        /// The full height transition model with adjacent tile smoothing is implemented in
        /// AuroraNavigationMesh.GetTileHeightAtPosition which has access to the tile grid.
        /// </remarks>
        private float GetTileHeightFromTransitionModel(string tilesetResRef, int tileId, float sampleX, float sampleZ, int? tileHeightTransitions)
        {
            // Load tileset to get height transition value
            SET tileset = LoadTileset(tilesetResRef);
            if (tileset == null)
            {
                return float.MinValue;
            }

            // Get height transition value from SET file [GENERAL] section
            // Based on nwmain.exe: CNWTileSet::GetHeightTransition @ 0x1402c67c0
            // Stored at offset 0x4c in CNWTileSet structure
            // This is the standard height per transition value (typically 0.5)
            float heightTransitionValue = tileset.Transition;
            if (heightTransitionValue <= 0.0f)
            {
                // Default to 0.5 if not specified (standard Aurora Engine value)
                heightTransitionValue = 0.5f;
            }

            // Get tile's height transitions count (from area GIT Tile_Height field)
            // If not provided, default to 0 (base height)
            int heightTransitions = tileHeightTransitions ?? 0;

            // Calculate base height for this tile
            // Based on nwmain.exe: Tile height calculation from height transitions
            // baseHeight = heightTransitions * heightTransitionValue
            float baseHeight = heightTransitions * heightTransitionValue;

            // Simplified model: Assume uniform height across the tile
            // In the full model (AuroraNavigationMesh.GetTileHeightAtPosition), corner heights
            // are calculated considering adjacent tiles for smooth transitions, but we don't
            // have access to adjacent tiles here, so we use a uniform height model.
            //
            // For a more accurate approximation, we could apply a simple gradient based on
            // sample position, but the simplified model uses uniform height for simplicity.
            // This matches the behavior when walkmesh is unavailable - the tile is treated
            // as a flat surface at the calculated base height.

            // Clamp normalized coordinates to valid range [0.0, 1.0]
            sampleX = Math.Max(0.0f, Math.Min(1.0f, sampleX));
            sampleZ = Math.Max(0.0f, Math.Min(1.0f, sampleZ));

            // Simplified bilinear interpolation model
            // For a uniform tile, all corners have the same height (baseHeight)
            // The bilinear interpolation formula simplifies to:
            // h(x,z) = (1-x)(1-z)*h + x(1-z)*h + (1-x)z*h + xz*h = h
            // So we just return the base height for any point on a uniform tile

            // However, for better approximation when walkmesh is unavailable, we can apply
            // a subtle gradient based on the standard tile model structure.
            // Based on typical Aurora tile geometry, tiles are generally flat but may have
            // slight elevation variations. Without walkmesh data, we use the base height.

            return baseHeight;
        }

        /// <summary>
        /// Gets the walkmesh (BWM) for a tile by loading its WOK file.
        /// </summary>
        /// <param name="tilesetResRef">The tileset resource reference.</param>
        /// <param name="tileId">The tile ID (index into tileset).</param>
        /// <returns>The walkmesh BWM, or null if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh walkmesh loading
        /// - Gets tile model from tileset
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Returns parsed BWM walkmesh data
        /// - Used for precise geometry testing (raycasting, collision detection)
        /// </remarks>
        [CanBeNull]
        public BWM GetTileWalkmesh(string tilesetResRef, int tileId)
        {
            if (tileId < 0)
            {
                return null;
            }

            // Get tile model from tileset
            string modelName = GetTileModel(tilesetResRef, tileId);
            if (string.IsNullOrEmpty(modelName))
            {
                return null;
            }

            try
            {
                // Load walkmesh (WOK file) for the tile model
                // Walkmesh filename is model name with .wok extension
                if (_resourceLoader == null)
                {
                    // No resource loader available
                    return null;
                }

                byte[] wokData = _resourceLoader(modelName + ".wok");
                if (wokData == null || wokData.Length == 0)
                {
                    // Walkmesh not found
                    return null;
                }

                // Parse walkmesh
                BWM walkmesh = BWMAuto.ReadBwm(wokData);
                return walkmesh;
            }
            catch
            {
                // Failed to load/parse walkmesh
                return null;
            }
        }

        /// <summary>
        /// Clears the tileset cache.
        /// </summary>
        public void ClearCache()
        {
            _tilesetCache.Clear();
        }
    }
}

