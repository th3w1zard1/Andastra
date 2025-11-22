using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IniParser;
using IniParser.Model;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Namespaces;

namespace TSLPatcher.Core.Reader;

/// <summary>
/// Responsible for reading and loading namespaces from the namespaces.ini file.
/// </summary>
public class NamespaceReader
{
    private readonly IniData _ini;

    public List<PatcherNamespace> Namespaces { get; } = new();

    public NamespaceReader(IniData ini)
    {
        _ini = ini ?? throw new ArgumentNullException(nameof(ini));
    }

    /// <summary>
    /// Loads namespaces from an INI file at the specified path.
    /// </summary>
    public static List<PatcherNamespace> FromFilePath(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Namespaces file not found: {path}", path);
        }

        var parser = new IniParser.Parser.IniDataParser();
        parser.Configuration.AllowDuplicateKeys = true;
        parser.Configuration.AllowDuplicateSections = true;
        parser.Configuration.CaseInsensitive = true;

        string iniText = File.ReadAllText(path);
        var ini = parser.Parse(iniText);

        return new NamespaceReader(ini).Load();
    }

    /// <summary>
    /// Load and parse all namespaces from the INI data.
    /// </summary>
    public List<PatcherNamespace> Load()
    {
        // Find the [Namespaces] section (case-insensitive)
        var namespacesSection = _ini.Sections.FirstOrDefault(s =>
            s.SectionName.Equals("Namespaces", StringComparison.OrdinalIgnoreCase));

        if (namespacesSection == null)
        {
            throw new KeyNotFoundException("The [Namespaces] section was not found in the ini");
        }

        var namespaces = new List<PatcherNamespace>();

        foreach (var keyData in namespacesSection.Keys)
        {
            string namespaceId = keyData.Value;

            // Find the section for this namespace (case-insensitive)
            var namespaceSection = _ini.Sections.FirstOrDefault(s =>
                s.SectionName.Equals(namespaceId, StringComparison.OrdinalIgnoreCase));

            if (namespaceSection == null)
            {
                throw new KeyNotFoundException(
                    $"The '[{namespaceId}]' section was not found in the 'namespaces.ini' file, " +
                    $"referenced by '{keyData.KeyName}={namespaceId}' in [{namespacesSection.SectionName}].");
            }

            // Required fields
            string iniFilename = GetValue(namespaceSection, "IniName")
                ?? throw new KeyNotFoundException($"IniName not found in [{namespaceSection.SectionName}]");
            string infoFilename = GetValue(namespaceSection, "InfoName")
                ?? throw new KeyNotFoundException($"InfoName not found in [{namespaceSection.SectionName}]");

            var ns = new PatcherNamespace(iniFilename, infoFilename)
            {
                // Optional fields
                DataFolderPath = GetValue(namespaceSection, "DataPath") ?? string.Empty,
                Name = GetValue(namespaceSection, "Name")?.Trim() ?? string.Empty,
                Description = GetValue(namespaceSection, "Description") ?? string.Empty,
                NamespaceId = namespaceSection.SectionName
            };

            namespaces.Add(ns);
        }

        return namespaces;
    }

    private static string? GetValue(SectionData section, string key)
    {
        return section.Keys.FirstOrDefault(k =>
            k.KeyName.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}

