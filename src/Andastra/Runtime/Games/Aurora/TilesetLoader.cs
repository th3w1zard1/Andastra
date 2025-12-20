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
        /// Clears the tileset cache.
        /// </summary>
        public void ClearCache()
        {
            _tilesetCache.Clear();
        }
    }
}

