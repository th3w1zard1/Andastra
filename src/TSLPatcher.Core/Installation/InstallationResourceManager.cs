using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.Capsule;
using TSLPatcher.Core.Resources;
using ChitinFile = TSLPatcher.Core.Formats.Chitin.Chitin;

namespace TSLPatcher.Core.Installation;

/// <summary>
/// Manages resource lookup and caching for a KOTOR installation.
/// Provides centralized resource access across override folders, modules, chitin, etc.
/// </summary>
public class InstallationResourceManager
{
    private readonly string _installationPath;
    private readonly Game _game;

    // Cached resource lists
    private Dictionary<string, List<FileResource>>? _overrideResources;
    private Dictionary<string, List<FileResource>>? _moduleResources;
    private ChitinFile? _chitin;

    // Lazy loading flags
    private bool _overrideLoaded;
    private bool _modulesLoaded;
    private bool _chitinLoaded;

    public string InstallationPath => _installationPath;
    public Game Game => _game;

    public InstallationResourceManager(string installationPath)
    {
        _installationPath = installationPath ?? throw new ArgumentNullException(nameof(installationPath));
        _game = Installation.DetermineGame(installationPath)
            ?? throw new InvalidOperationException($"Could not determine game type for path: {installationPath}");
    }

    /// <summary>
    /// Looks up a single resource by name and type, searching in priority order.
    /// </summary>
    public ResourceResult? LookupResource(
        string resname,
        ResourceType restype,
        SearchLocation[]? order = null,
        string? moduleRoot = null)
    {
        order ??= new[]
        {
            SearchLocation.OVERRIDE,
            SearchLocation.MODULES,
            SearchLocation.CHITIN
        };

        List<LocationResult> locations = LocateResource(resname, restype, order, moduleRoot);
        if (locations.Count == 0)
        {
            return null;
        }

        // Return first match (highest priority)
        LocationResult location = locations[0];
        byte[] data = File.ReadAllBytes(location.FilePath);

        // If inside a capsule, need to extract at offset
        if (location.FileResource?.InsideCapsule == true)
        {
            data = location.FileResource.GetData();
        }

        var result = new ResourceResult(resname, restype, location.FilePath, data);
        if (location.FileResource != null)
        {
            result.SetFileResource(location.FileResource);
        }
        return result;
    }

    /// <summary>
    /// Locates all instances of a resource across the installation.
    /// </summary>
    public List<LocationResult> LocateResource(
        string resname,
        ResourceType restype,
        SearchLocation[]? order = null,
        string? moduleRoot = null)
    {
        order ??= new[]
        {
            SearchLocation.OVERRIDE,
            SearchLocation.MODULES,
            SearchLocation.CHITIN
        };

        var results = new List<LocationResult>();
        var query = new ResourceIdentifier(resname, restype);

        foreach (SearchLocation location in order)
        {
            switch (location)
            {
                case SearchLocation.OVERRIDE:
                    results.AddRange(SearchOverride(query));
                    break;

                case SearchLocation.MODULES:
                    results.AddRange(SearchModules(query, moduleRoot));
                    break;

                case SearchLocation.CHITIN:
                    results.AddRange(SearchChitin(query));
                    break;

                // Add other locations as needed
            }
        }

        return results;
    }

    private List<LocationResult> SearchOverride(ResourceIdentifier query)
    {
        EnsureOverrideLoaded();
        var results = new List<LocationResult>();

        if (_overrideResources == null)
            return results;

        foreach (List<FileResource> resourceList in _overrideResources.Values)
        {
            FileResource? resource = resourceList.FirstOrDefault(r =>
                r.ResName.Equals(query.ResName, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == query.ResType);

            if (resource != null)
            {
                var location = new LocationResult(resource.FilePath, resource.Offset, resource.Size);
                location.SetFileResource(resource);
                results.Add(location);
            }
        }

        return results;
    }

    private List<LocationResult> SearchModules(ResourceIdentifier query, string? moduleRoot)
    {
        EnsureModulesLoaded();
        var results = new List<LocationResult>();

        if (_moduleResources == null)
            return results;

        Dictionary<string, List<FileResource>> modulesToSearch = _moduleResources;

        // Filter by module root if specified
        if (!string.IsNullOrEmpty(moduleRoot))
        {
            modulesToSearch = _moduleResources
                .Where(kvp => Installation.GetModuleRoot(kvp.Key).Equals(moduleRoot, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        foreach (List<FileResource> resourceList in modulesToSearch.Values)
        {
            FileResource? resource = resourceList.FirstOrDefault(r =>
                r.ResName.Equals(query.ResName, StringComparison.OrdinalIgnoreCase) &&
                r.ResType == query.ResType);

            if (resource != null)
            {
                var location = new LocationResult(resource.FilePath, resource.Offset, resource.Size);
                location.SetFileResource(resource);
                results.Add(location);
            }
        }

        return results;
    }

    private List<LocationResult> SearchChitin(ResourceIdentifier query)
    {
        EnsureChitinLoaded();
        var results = new List<LocationResult>();

        if (_chitin == null)
            return results;

        FileResource? resource = _chitin.GetResourceInfo(query.ResName, query.ResType);
        if (resource != null)
        {
            var location = new LocationResult(resource.FilePath, resource.Offset, resource.Size);
            location.SetFileResource(resource);
            results.Add(location);
        }

        return results;
    }

    private void EnsureOverrideLoaded()
    {
        if (_overrideLoaded)
            return;

        string overridePath = Installation.GetOverridePath(_installationPath);
        _overrideResources = LoadResourcesFromDirectory(overridePath, recursive: true);
        _overrideLoaded = true;
    }

    private void EnsureModulesLoaded()
    {
        if (_modulesLoaded)
            return;

        string modulesPath = Installation.GetModulesPath(_installationPath);
        _moduleResources = LoadModulesFromDirectory(modulesPath);
        _modulesLoaded = true;
    }

    private void EnsureChitinLoaded()
    {
        if (_chitinLoaded)
            return;

        string chitinPath = Installation.GetChitinPath(_installationPath);

        if (File.Exists(chitinPath))
        {
            try
            {
                _chitin = new ChitinFile(chitinPath, _installationPath, _game);
            }
            catch
            {
                // Failed to load chitin, leave as null
                _chitin = null;
            }
        }

        _chitinLoaded = true;
    }

    private static Dictionary<string, List<FileResource>> LoadResourcesFromDirectory(string path, bool recursive)
    {
        var resources = new Dictionary<string, List<FileResource>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(path))
            return resources;

        SearchOption searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        foreach (string file in Directory.GetFiles(path, "*.*", searchOption))
        {
            try
            {
                var identifier = ResourceIdentifier.FromPath(file);
                if (identifier.ResType == ResourceType.INVALID || identifier.ResType.IsInvalid)
                    continue;

                var fileInfo = new FileInfo(file);
                var resource = new FileResource(
                    identifier.ResName,
                    identifier.ResType,
                    (int)fileInfo.Length,
                    0,
                    file
                );

                string relativePath = Path.GetRelativePath(path, Path.GetDirectoryName(file) ?? path);
                if (relativePath == ".")
                    relativePath = ".";

                if (!resources.ContainsKey(relativePath))
                {
                    resources[relativePath] = new List<FileResource>();
                }

                resources[relativePath].Add(resource);
            }
            catch
            {
                // Skip files that can't be processed
            }
        }

        return resources;
    }

    private static Dictionary<string, List<FileResource>> LoadModulesFromDirectory(string path)
    {
        var modules = new Dictionary<string, List<FileResource>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(path))
            return modules;

        foreach (string file in Directory.GetFiles(path))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is not (".rim" or ".mod" or ".erf"))
                continue;

            try
            {
                var capsule = new LazyCapsule(file);
                modules[Path.GetFileName(file)] = capsule.GetResources();
            }
            catch
            {
                // Skip capsules that can't be loaded
            }
        }

        return modules;
    }

    /// <summary>
    /// Clears all cached resources, forcing a reload on next access.
    /// </summary>
    public void ClearCache()
    {
        _overrideResources = null;
        _moduleResources = null;
        _chitin = null;
        _overrideLoaded = false;
        _modulesLoaded = false;
        _chitinLoaded = false;
    }

    /// <summary>
    /// Reloads a specific module's resources.
    /// </summary>
    public void ReloadModule(string moduleName)
    {
        if (_moduleResources == null)
            return;

        string modulePath = Path.Combine(Installation.GetModulesPath(_installationPath), moduleName);
        if (!File.Exists(modulePath))
            return;

        try
        {
            var capsule = new LazyCapsule(modulePath);
            _moduleResources[moduleName] = capsule.GetResources();
        }
        catch
        {
            // Failed to reload
        }
    }
}

