using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.SET
{
    /// <summary>
    /// Parser for Aurora Engine SET (tileset) files.
    /// SET files are INI-like configuration files that define tileset properties and tile models.
    /// </summary>
    /// <remarks>
    /// SET File Format (nwmain.exe CResSET):
    /// - [CResSET::GetSectionEntryValue] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: TODO: Find this address) parses SET files
    /// - Format: INI-like with sections in brackets [SECTION] and key=value pairs
    /// - Sections: [GENERAL], [GRASS], [TILES], [TILE0], [TILE1], etc.
    /// - [CNWTileSet::LoadTileSet] @ (K1: TODO: Find this address, TSL: TODO: Find this address, NWN:EE: TODO: Find this address)
    /// - Original implementation: CResSET::GetSectionEntryValue reads section/entry values
    /// </remarks>
    public class SetFileParser
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections;

        /// <summary>
        /// Initializes a new instance of SetFileParser.
        /// </summary>
        public SetFileParser()
        {
            _sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a SET file from byte array.
        /// </summary>
        /// <param name="data">SET file data as byte array.</param>
        /// <returns>Parsed SetFileParser instance.</returns>
        public static SetFileParser Parse(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("SET file data cannot be null or empty", "data");
            }

            string text = Encoding.UTF8.GetString(data);
            return ParseText(text);
        }

        /// <summary>
        /// Parses a SET file from text.
        /// </summary>
        /// <param name="text">SET file content as text.</param>
        /// <returns>Parsed SetFileParser instance.</returns>
        public static SetFileParser ParseText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("SET file text cannot be null or empty", "text");
            }

            var parser = new SetFileParser();
            parser.ParseInternal(text);
            return parser;
        }

        /// <summary>
        /// Gets a section entry value.
        /// </summary>
        /// <param name="section">Section name (e.g., "GENERAL", "TILE0").</param>
        /// <param name="entry">Entry key (e.g., "EnvMap", "Model").</param>
        /// <param name="defaultValue">Default value if not found.</param>
        /// <returns>Entry value or default value if not found.</returns>
        /// <remarks>
        /// Based on nwmain.exe: CResSET::GetSectionEntryValue @ 0x1402cc370
        /// - Reads value from section/entry pair
        /// - Returns empty string if not found (original behavior)
        /// </remarks>
        [CanBeNull]
        public string GetSectionEntryValue([NotNull] string section, [NotNull] string entry, [CanBeNull] string defaultValue = null)
        {
            if (section == null)
            {
                throw new ArgumentNullException("section");
            }
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            if (_sections.TryGetValue(section, out Dictionary<string, string> sectionData))
            {
                if (sectionData.TryGetValue(entry, out string value))
                {
                    return value;
                }
            }

            return defaultValue ?? string.Empty;
        }

        /// <summary>
        /// Checks if a section exists.
        /// </summary>
        /// <param name="section">Section name.</param>
        /// <returns>True if section exists, false otherwise.</returns>
        public bool HasSection(string section)
        {
            if (string.IsNullOrEmpty(section))
            {
                return false;
            }

            return _sections.ContainsKey(section);
        }

        /// <summary>
        /// Gets all entries in a section.
        /// </summary>
        /// <param name="section">Section name.</param>
        /// <returns>Dictionary of entry keys and values, or null if section doesn't exist.</returns>
        [CanBeNull]
        public Dictionary<string, string> GetSection([NotNull] string section)
        {
            if (section == null)
            {
                throw new ArgumentNullException("section");
            }

            if (_sections.TryGetValue(section, out Dictionary<string, string> sectionData))
            {
                return new Dictionary<string, string>(sectionData, StringComparer.OrdinalIgnoreCase);
            }

            return null;
        }

        private void ParseInternal(string text)
        {
            _sections.Clear();

            using (StringReader reader = new StringReader(text))
            {
                string currentSection = null;
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    // Trim whitespace
                    string trimmed = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    // Parse section header: [SECTION]
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        if (!_sections.ContainsKey(currentSection))
                        {
                            _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        }
                    }
                    // Parse key=value pair
                    else if (currentSection != null && trimmed.Contains("="))
                    {
                        int eqIndex = trimmed.IndexOf('=');
                        if (eqIndex > 0 && eqIndex < trimmed.Length - 1)
                        {
                            string key = trimmed.Substring(0, eqIndex).Trim();
                            string value = trimmed.Substring(eqIndex + 1).Trim();

                            // Remove quotes if present
                            if (value.Length >= 2 && value.StartsWith("\"") && value.EndsWith("\""))
                            {
                                value = value.Substring(1, value.Length - 2);
                            }

                            if (!string.IsNullOrEmpty(key))
                            {
                                _sections[currentSection][key] = value;
                            }
                        }
                    }
                }
            }
        }
    }
}

