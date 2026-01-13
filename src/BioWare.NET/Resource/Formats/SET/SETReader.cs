using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.SET
{
    /// <summary>
    /// Reads SET (tileset) files from byte arrays.
    /// </summary>
    /// <remarks>
    /// SET File Format:
    /// - INI-style configuration file format
    /// - Line-based parsing with sections [GENERAL], [TILES], [TILE0], [TILE1], etc.
    /// - Based on xoreos implementation at vendor/xoreos/src/engines/nwn/tileset.cpp
    /// - [CNWTileSet::LoadTileSet] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: 0x1411e4684)
    /// </remarks>
    internal class SETReader : IDisposable
    {
        private readonly byte[] _data;
        private readonly StringReader _reader;
        private bool _disposed;

        public SETReader(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _data = data;
            string text = Encoding.UTF8.GetString(data);
            _reader = new StringReader(text);
        }

        public SET Load()
        {
            var set = new SET();
            string currentSection = null;
            var tileIndex = -1;

            string line;
            while ((line = _reader.ReadLine()) != null)
            {
                line = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // Handle Windows line endings (CRLF) - already handled by StringReader, but be defensive
                line = line.TrimEnd('\r', '\n');

                // Parse section headers
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();

                    // Parse tile index from section name (e.g., "TILE0" -> 0)
                    if (currentSection.StartsWith("TILE", StringComparison.OrdinalIgnoreCase))
                    {
                        string indexStr = currentSection.Substring(4);
                        if (int.TryParse(indexStr, out int index))
                        {
                            tileIndex = index;
                            // Ensure we have enough tiles
                            while (set.Tiles.Count <= index)
                            {
                                set.Tiles.Add(new SETTile());
                            }
                        }
                    }
                    continue;
                }

                // Parse key-value pairs
                int equalsIndex = line.IndexOf('=');
                if (equalsIndex < 0)
                    continue;

                string key = line.Substring(0, equalsIndex).Trim();
                string value = line.Substring(equalsIndex + 1).Trim();

                // Parse based on current section
                if (currentSection == "GENERAL")
                {
                    ParseGeneralField(set, key, value);
                }
                else if (currentSection == "TILES")
                {
                    ParseTilesField(set, key, value);
                }
                else if (currentSection != null && currentSection.StartsWith("TILE", StringComparison.OrdinalIgnoreCase) && tileIndex >= 0)
                {
                    ParseTileField(set, tileIndex, key, value);
                }
            }

            return set;
        }

        private void ParseGeneralField(SET set, string key, string value)
        {
            switch (key.ToUpperInvariant())
            {
                case "DISPLAYNAME":
                    if (uint.TryParse(value, out uint displayName))
                    {
                        set.DisplayName = displayName;
                    }
                    break;

                case "TRANSITION":
                    if (float.TryParse(value, out float transition))
                    {
                        set.Transition = transition;
                    }
                    break;

                case "ENVMAP":
                    set.EnvMap = value;
                    break;
            }
        }

        private void ParseTilesField(SET set, string key, string value)
        {
            if (key.Equals("Count", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(value, out int count))
                {
                    // Ensure we have enough tiles
                    while (set.Tiles.Count < count)
                    {
                        set.Tiles.Add(new SETTile());
                    }
                }
            }
        }

        private void ParseTileField(SET set, int tileIndex, string key, string value)
        {
            if (tileIndex < 0 || tileIndex >= set.Tiles.Count)
                return;

            var tile = set.Tiles[tileIndex];

            if (key.Equals("Model", StringComparison.OrdinalIgnoreCase))
            {
                tile.Model = value;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }
}

