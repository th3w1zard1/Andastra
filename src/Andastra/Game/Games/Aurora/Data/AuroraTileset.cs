using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.SET;
using Andastra.Runtime.Content.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Aurora.Data
{
    /// <summary>
    /// Represents an Aurora Engine tileset loaded from a SET file.
    /// Provides access to tileset properties and tile model ResRefs.
    /// </summary>
    /// <remarks>
    /// Aurora Tileset (nwmain.exe CNWTileSet):
    /// - Based on nwmain.exe: CNWTileSet::LoadTileSet @ 0x1402c6890
    /// - Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
    /// - Original implementation: Loads SET file, parses sections, stores tile data array
    /// - Tile data array: Each tile is 0xb8 bytes (184 bytes), stored at offset 0x60
    /// - Tile count: Stored at offset 0x54, read from [TILES] Count entry
    /// - GetTileData: Returns CNWTileData pointer at offset 0x60 + (Tile_ID * 0xb8)
    /// - Model ResRef: Stored in CNWTileData structure, read from [TILE{N}] Model entry
    ///
    /// SET File Format:
    /// - [GENERAL] section: EnvMap, Transition (height transition value)
    /// - [GRASS] section: Grass, Density, Height, AmbientRed/Green/Blue, DiffuseRed/Green/Blue, GrassTextureName
    /// - [TILES] section: Count (number of tiles)
    /// - [TILE0], [TILE1], ... sections: Model (ResRef), TopLeft, TopLeftHeight, and other tile properties
    ///
    /// Based on reverse engineering of nwmain.exe:
    /// - CNWTileSet::LoadTileSet @ 0x1402c6890 - Loads and parses SET file
    /// - CResSET::GetSectionEntryValue @ 0x1402cc370 - Reads section/entry values
    /// - CNWTileSet::GetTileData @ 0x1402c67d0 - Returns tile data for Tile_ID
    /// </remarks>
    public class AuroraTileset
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly ResRef _tilesetResRef;
        private SetFileParser _setFile;
        private readonly Dictionary<int, ResRef> _tileModelCache;
        private int _tileCount;
        private bool _isLoaded;

        /// <summary>
        /// Gets the tileset resource reference.
        /// </summary>
        public ResRef TilesetResRef => _tilesetResRef;

        /// <summary>
        /// Gets the number of tiles in this tileset.
        /// </summary>
        public int TileCount => _tileCount;

        /// <summary>
        /// Gets whether the tileset is loaded.
        /// </summary>
        public bool IsLoaded => _isLoaded;

        /// <summary>
        /// Initializes a new instance of AuroraTileset.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading SET files.</param>
        /// <param name="tilesetResRef">Tileset resource reference (ResRef of the SET file).</param>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::CNWTileSet constructor
        /// - Stores CResRef to tileset file
        /// - Loads tileset on demand via LoadTileSet()
        /// </remarks>
        public AuroraTileset([NotNull] IGameResourceProvider resourceProvider, ResRef tilesetResRef)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }

            _resourceProvider = resourceProvider;
            _tilesetResRef = tilesetResRef;
            _tileModelCache = new Dictionary<int, ResRef>();
            _tileCount = 0;
            _isLoaded = false;
        }

        /// <summary>
        /// Loads the tileset from the SET file.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::LoadTileSet @ 0x1402c6890
        /// - Loads SET file via CRes::Demand
        /// - Parses [GENERAL] section: EnvMap, Transition
        /// - Parses [GRASS] section: Grass, Density, Height, Ambient/Diffuse colors, GrassTextureName
        /// - Parses [TILES] section: Count
        /// - Parses [TILE0], [TILE1], ... sections: Model ResRef for each tile
        /// - Allocates tile data array (Count * 0xb8 bytes)
        /// - Stores Model ResRef for each tile in cache
        /// </remarks>
        public void Load()
        {
            if (_isLoaded)
            {
                return;
            }

            try
            {
                // Load SET file from resource provider
                // Based on nwmain.exe: CRes::Demand loads resource data
                byte[] setData = _resourceProvider.GetResourceBytes(new ResourceIdentifier(_tilesetResRef.ToString(), ResourceType.SET));
                if (setData == null || setData.Length == 0)
                {
                    throw new InvalidOperationException($"Failed to load tileset SET file: {_tilesetResRef}");
                }

                // Parse SET file
                // Based on nwmain.exe: CResSET parses INI-like format
                _setFile = SetFileParser.Parse(setData);

                // Read tile count from [TILES] Count entry
                // Based on nwmain.exe: CNWTileSet::LoadTileSet reads [TILES] Count @ line 80-82
                string countStr = _setFile.GetSectionEntryValue("TILES", "Count", "0");
                if (!int.TryParse(countStr, out int count) || count < 0)
                {
                    throw new InvalidOperationException($"Invalid tile count in tileset {_tilesetResRef}: {countStr}");
                }

                _tileCount = count;

                // Load Model ResRef for each tile
                // Based on nwmain.exe: CNWTileSet::LoadTileSet processes each [TILE{N}] section
                // Based on xoreos: Tileset::loadTile reads Model from [TILE{N}] section
                for (int i = 0; i < _tileCount; i++)
                {
                    string tileSection = string.Format("TILE{0}", i);
                    string modelResRefStr = _setFile.GetSectionEntryValue(tileSection, "Model", null);

                    if (!string.IsNullOrEmpty(modelResRefStr))
                    {
                        // Parse ResRef from string
                        ResRef modelResRef = ResRef.FromString(modelResRefStr);
                        if (modelResRef != null && !modelResRef.IsBlank())
                        {
                            _tileModelCache[i] = modelResRef;
                        }
                    }
                }

                _isLoaded = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to load tileset {_tilesetResRef}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the tile model ResRef for a given Tile_ID.
        /// </summary>
        /// <param name="tileId">Tile_ID (index into tileset's tile list, 0-based).</param>
        /// <returns>Tile model ResRef, or blank ResRef if Tile_ID is invalid or not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::GetTileData @ 0x1402c67d0
        /// - Validates Tile_ID is within bounds (0 <= Tile_ID < Count)
        /// - Returns CNWTileData pointer at offset 0x60 + (Tile_ID * 0xb8)
        /// - CNWTileData contains Model ResRef field
        /// - This method returns the Model ResRef directly from the cached tile data
        ///
        /// Error handling (matching original engine):
        /// - If Tile_ID < 0 or Tile_ID >= Count: Returns blank ResRef (original returns null pointer)
        /// - If tileset not loaded: Returns blank ResRef
        /// - If tile has no Model entry: Returns blank ResRef
        /// </remarks>
        public ResRef GetTileModelResRef(int tileId)
        {
            // Ensure tileset is loaded
            if (!_isLoaded)
            {
                Load();
            }

            // Validate Tile_ID bounds
            // Based on nwmain.exe: CNWTileSet::GetTileData validates Tile_ID @ lines 15-21
            if (tileId < 0 || tileId >= _tileCount)
            {
                // Invalid Tile_ID - return blank ResRef (matching original behavior of returning null)
                return ResRef.FromBlank();
            }

            // Get Model ResRef from cache
            // Based on nwmain.exe: CNWTileSet::GetTileData returns CNWTileData pointer
            // CNWTileData contains Model ResRef field
            if (_tileModelCache.TryGetValue(tileId, out ResRef modelResRef))
            {
                return modelResRef;
            }

            // Tile_ID exists but has no Model entry - return blank ResRef
            return ResRef.FromBlank();
        }

        /// <summary>
        /// Gets the environment map ResRef from the [GENERAL] section.
        /// </summary>
        /// <returns>Environment map ResRef, or blank if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::LoadTileSet reads [GENERAL] EnvMap @ line 31-32
        /// </remarks>
        public ResRef GetEnvironmentMap()
        {
            if (!_isLoaded)
            {
                Load();
            }

            string envMapStr = _setFile.GetSectionEntryValue("GENERAL", "EnvMap", null);
            if (!string.IsNullOrEmpty(envMapStr))
            {
                return ResRef.FromString(envMapStr);
            }

            return ResRef.FromBlank();
        }

        /// <summary>
        /// Gets the height transition value from the [GENERAL] section.
        /// </summary>
        /// <returns>Height transition value, or 0.0f if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CNWTileSet::LoadTileSet reads [GENERAL] Transition @ line 33-35
        /// - Stored as float at offset 0x4c in CNWTileSet
        /// </remarks>
        public float GetHeightTransition()
        {
            if (!_isLoaded)
            {
                Load();
            }

            string transitionStr = _setFile.GetSectionEntryValue("GENERAL", "Transition", "0");
            if (float.TryParse(transitionStr, out float transition))
            {
                return transition;
            }

            return 0.0f;
        }
    }
}

