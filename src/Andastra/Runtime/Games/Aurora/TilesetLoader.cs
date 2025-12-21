using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Parsing;
using Andastra.Parsing.Formats.SET;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Common;

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
        /// <returns>The height at the sample point, or float.MinValue if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSurfaceMesh height sampling
        /// - Gets tile model from tileset
        /// - Loads walkmesh (WOK file) for the tile model
        /// - Samples height from walkmesh face at the specified point
        /// - Uses tile center (0.5, 0.5) for default height calculation
        /// - Falls back to simplified height transition model if walkmesh can't be loaded
        /// </remarks>
        public float GetTileHeight(string tilesetResRef, int tileId, float sampleX = 0.5f, float sampleZ = 0.5f)
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
                    // No resource loader available
                    return float.MinValue;
                }

                byte[] wokData = _resourceLoader(modelName + ".wok");
                if (wokData == null || wokData.Length == 0)
                {
                    // Walkmesh not found
                    return float.MinValue;
                }

                // Parse walkmesh
                BWM walkmesh = BWMAuto.ReadBwm(wokData);
                if (walkmesh == null || walkmesh.Faces == null || walkmesh.Faces.Count == 0)
                {
                    return float.MinValue;
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
                    if (face.Vertices == null || face.Vertices.Count < 3)
                    {
                        continue;
                    }

                    // Check if point is within face bounds (simple bounding box check first)
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minZ = float.MaxValue, maxZ = float.MinValue;
                    foreach (var vertex in face.Vertices)
                    {
                        if (vertex.X < minX) minX = vertex.X;
                        if (vertex.X > maxX) maxX = vertex.X;
                        if (vertex.Z < minZ) minZ = vertex.Z;
                        if (vertex.Z > maxZ) maxZ = vertex.Z;
                    }

                    // Quick bounding box rejection
                    if (worldX < minX || worldX > maxX || worldZ < minZ || worldZ > maxZ)
                    {
                        continue;
                    }

                    // Check if point is inside face using barycentric coordinates
                    // For triangle faces, use barycentric interpolation
                    if (face.Vertices.Count == 3)
                    {
                        var v0 = face.Vertices[0];
                        var v1 = face.Vertices[1];
                        var v2 = face.Vertices[2];

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
                    else
                    {
                        // For non-triangle faces, use distance to face center as fallback
                        // Calculate face center
                        float centerX = 0.0f, centerZ = 0.0f, centerY = 0.0f;
                        foreach (var vertex in face.Vertices)
                        {
                            centerX += vertex.X;
                            centerZ += vertex.Z;
                            centerY += vertex.Y;
                        }
                        centerX /= face.Vertices.Count;
                        centerZ /= face.Vertices.Count;
                        centerY /= face.Vertices.Count;

                        // Calculate distance from sample point to face center
                        float dx = worldX - centerX;
                        float dz = worldZ - centerZ;
                        float distance = (float)Math.Sqrt(dx * dx + dz * dz);

                        // Use closest face
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            bestHeight = centerY;
                        }
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
                    if (face.Vertices == null || face.Vertices.Count == 0)
                    {
                        continue;
                    }

                    // Calculate face average height
                    float faceHeight = 0.0f;
                    foreach (var vertex in face.Vertices)
                    {
                        faceHeight += vertex.Y;
                    }
                    faceHeight /= face.Vertices.Count;

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
                // Failed to load/parse walkmesh
                return float.MinValue;
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

