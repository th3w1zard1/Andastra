using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.SET
{
    /// <summary>
    /// Represents a SET (tileset) file used in Aurora Engine (Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// SET File Format:
    /// - INI-style configuration file format
    /// - Contains tileset metadata and tile definitions
    /// - Based on xoreos implementation at vendor/xoreos/src/engines/nwn/tileset.cpp
    /// - [CNWTileSet::LoadTileSet] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: 0x1411e4684)
    /// 
    /// File Structure:
    /// [GENERAL]
    /// DisplayName=<strref>
    /// Transition=<float>
    /// EnvMap=<string>
    /// 
    /// [TILES]
    /// Count=<int>
    /// 
    /// [TILE0]
    /// Model=<string>
    /// 
    /// [TILE1]
    /// Model=<string>
    /// ...
    /// 
    /// Based on reverse engineering of:
    /// - [CNWTileSet::LoadTileSet] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: 0x1411e4684)
    /// - [CNWTileSet::GetTileData] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: 0x1402c67d0)
    /// - xoreos implementation: vendor/xoreos/src/engines/nwn/tileset.cpp
    /// </remarks>
    [PublicAPI]
    public class SET : IEquatable<SET>
    {
        /// <summary>
        /// Tileset display name (string reference).
        /// </summary>
        public uint DisplayName { get; set; }

        /// <summary>
        /// Height transition value for tiles.
        /// </summary>
        public float Transition { get; set; }

        /// <summary>
        /// Environment map resource reference.
        /// </summary>
        public string EnvMap { get; set; }

        /// <summary>
        /// List of tile definitions in this tileset.
        /// </summary>
        public List<SETTile> Tiles { get; set; }

        public SET()
        {
            DisplayName = 0;
            Transition = 0.0f;
            EnvMap = string.Empty;
            Tiles = new List<SETTile>();
        }

        public override bool Equals(object obj) => obj is SET other && Equals(other);

        public bool Equals(SET other)
        {
            if (other == null) return false;
            return DisplayName == other.DisplayName &&
                   Math.Abs(Transition - other.Transition) < 0.001f &&
                   string.Equals(EnvMap, other.EnvMap, StringComparison.OrdinalIgnoreCase) &&
                   Tiles.SequenceEqual(other.Tiles);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + DisplayName.GetHashCode();
                hash = hash * 23 + Transition.GetHashCode();
                hash = hash * 23 + (EnvMap?.GetHashCode() ?? 0);
                foreach (var tile in Tiles)
                {
                    hash = hash * 23 + (tile?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        /// <summary>
        /// Deserializes a SET file from a byte array.
        /// </summary>
        /// <param name="data">The SET file data.</param>
        /// <returns>A parsed SET object.</returns>
        public static SET FromBytes(byte[] data)
        {
            var reader = new SETReader(data);
            return reader.Load();
        }
    }

    /// <summary>
    /// Represents a single tile definition in a SET file.
    /// </summary>
    /// <remarks>
    /// Based on nwmain.exe: CNWTileData structure
    /// - Each tile has a model reference (MDL file)
    /// - The model's walkmesh (WOK file) contains surface material information
    /// - Based on nwmain.exe: CNWTileSet::GetTileData] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: 0x1402c67d0)
    /// </remarks>
    [PublicAPI]
    public class SETTile : IEquatable<SETTile>
    {
        /// <summary>
        /// Model resource reference (MDL file name without extension).
        /// </summary>
        public string Model { get; set; }

        public SETTile()
        {
            Model = string.Empty;
        }

        public override bool Equals(object obj) => obj is SETTile other && Equals(other);

        public bool Equals(SETTile other)
        {
            if (other == null) return false;
            return string.Equals(Model, other.Model, StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            return Model?.GetHashCode() ?? 0;
        }
    }
}

