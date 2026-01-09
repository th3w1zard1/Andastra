using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tools;
using JetBrains.Annotations;

namespace Andastra.Parsing.Merge
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:25-34
    // Original: class ResourceInfo:
    public class ResourceInfo
    {
        public HashSet<string> Modules { get; } = new HashSet<string>();
        public List<FileResource> FileResources { get; } = new List<FileResource>();
        public bool IsMissing { get; set; }
        public bool IsUnused { get; set; }
        public HashSet<ResourceIdentifier> DependentResources { get; } = new HashSet<ResourceIdentifier>();
        public Dictionary<string, string> ResourceHashes { get; } = new Dictionary<string, string>();
        public string ImpactOfMissing { get; set; }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:36-360
    // Original: class ModuleManager:
    public class ModuleManager
    {
        private readonly Installation.Installation _installation;
        private readonly Dictionary<ResourceIdentifier, ResourceInfo> _resourcesInfo = new Dictionary<ResourceIdentifier, ResourceInfo>();
        private readonly Dictionary<ResourceIdentifier, HashSet<string>> _conflictingResources = new Dictionary<ResourceIdentifier, HashSet<string>>();
        private readonly Dictionary<string, List<ResourceIdentifier>> _missingResources = new Dictionary<string, List<ResourceIdentifier>>();
        private readonly Dictionary<string, List<ResourceIdentifier>> _unusedResources = new Dictionary<string, List<ResourceIdentifier>>();
        private readonly Dictionary<string, HashSet<string>> _resourceToModules = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public ModuleManager(Installation.Installation installation)
        {
            _installation = installation ?? throw new ArgumentNullException(nameof(installation));
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:50-98
        // Original: def analyze_modules(self, modules: list[Module]) -> None:
        public void AnalyzeModules(List<Module> modules)
        {
            if (modules == null)
            {
                throw new ArgumentNullException(nameof(modules));
            }

            foreach (var module in modules)
            {
                string moduleName = module.GetRoot();
                Console.WriteLine($"Analyzing module '{moduleName}'...");

                // First Pass: Collect Resource Information
                foreach (var kvp in module.Resources)
                {
                    var identifier = kvp.Key;
                    var modRes = kvp.Value;

                    if (!_resourcesInfo.TryGetValue(identifier, out ResourceInfo resourceInfo))
                    {
                        resourceInfo = new ResourceInfo();
                        _resourcesInfo[identifier] = resourceInfo;
                    }

                    resourceInfo.Modules.Add(moduleName);

                    // Create FileResource instances for each location and add them to the resource info
                    foreach (var location in modRes.Locations())
                    {
                        var fileInfo = new FileInfo(location);
                        var fileResource = new FileResource(
                            resname: modRes.GetResName(),
                            restype: modRes.GetResType(),
                            size: (int)fileInfo.Length,
                            offset: 0,
                            filepath: location
                        );
                        resourceInfo.FileResources.Add(fileResource);
                        string resourceHash = ComputeSha1Hash(location);
                        resourceInfo.ResourceHashes[fileResource.FilePath.Replace('\\', '/')] = resourceHash;
                    }

                    // Check for unused resources
                    if (!modRes.IsActive())
                    {
                        resourceInfo.IsUnused = true;
                    }

                    // If the resource data is missing, mark it as missing
                    // Note: Data() is only available on ModuleResource<T>, need to check type
                    if (modRes is ModuleResource<object> typedRes && typedRes.Data() == null)
                    {
                        resourceInfo.IsMissing = true;
                        resourceInfo.ImpactOfMissing = "Critical resource missing, could impact module functionality.";
                    }
                }

                // Second Pass: Identify Dependencies and Conflicts
                foreach (var kvp in module.Resources)
                {
                    var identifier = kvp.Key;
                    var modRes = kvp.Value;

                    if (!_resourcesInfo.TryGetValue(identifier, out ResourceInfo resourceInfo))
                    {
                        continue;
                    }

                    // Find dependencies within the module
                    var dependentResources = FindDependencies(module, modRes);
                    resourceInfo.DependentResources.UnionWith(dependentResources);

                    // Check for resource conflicts across multiple modules
                    if (resourceInfo.Modules.Count > 1)
                    {
                        if (!_conflictingResources.TryGetValue(identifier, out HashSet<string> conflicts))
                        {
                            conflicts = new HashSet<string>();
                            _conflictingResources[identifier] = conflicts;
                        }
                        conflicts.UnionWith(resourceInfo.Modules);
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:99-125
        // Original: def _find_dependencies(self, module: Module, mod_res: ModuleResource) -> set[ResourceIdentifier]:
        private HashSet<ResourceIdentifier> FindDependencies(Module module, ModuleResource modRes)
        {
            var dependencies = new HashSet<ResourceIdentifier>();

            // Search for linked resources like GIT, LYT, VIS
            ResourceType restype = modRes.GetResType();
            if (restype == ResourceType.GIT || restype == ResourceType.LYT || restype == ResourceType.VIS)
            {
                var linkedResources = SearchLinkedResources(module, modRes);
                dependencies.UnionWith(linkedResources);
            }

            // Extract dependencies from GFF files
            if (restype == ResourceType.GFF || restype == ResourceType.ARE ||
                restype == ResourceType.IFO || restype == ResourceType.DLG)
            {
                if (modRes is ModuleResource<object> typedRes)
                {
                    byte[] data = typedRes.Data();
                    if (data != null)
                    {
                        var references = ExtractReferencesFromGff(data);
                        dependencies.UnionWith(references);
                    }
                }
            }

            // Extract texture and model dependencies
            if (restype == ResourceType.MDL || restype == ResourceType.MDX)
            {
                if (modRes is ModuleResource<object> typedRes2)
                {
                    byte[] modelData = typedRes2.Data();
                    if (modelData != null)
                    {
                        var modelRefs = ExtractReferencesFromModel(modelData);
                        dependencies.UnionWith(modelRefs);
                    }
                }
            }

            return dependencies;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:127-151
        // Original: def _search_linked_resources(self, module: Module, mod_res: ModuleResource) -> set[ResourceIdentifier]:
        private HashSet<ResourceIdentifier> SearchLinkedResources(Module module, ModuleResource modRes)
        {
            var linkResname = module.ModuleId();
            var linkedResources = new HashSet<ResourceIdentifier>();

            if (linkResname == null)
            {
                return linkedResources;
            }

            var queries = new List<ResourceIdentifier>
            {
                new ResourceIdentifier(linkResname.ToString(), ResourceType.LYT),
                new ResourceIdentifier(linkResname.ToString(), ResourceType.GIT),
                new ResourceIdentifier(linkResname.ToString(), ResourceType.VIS)
            };

            // Search for each resource in the module
            // Based on PyKotor implementation: module._search_resource_locations(module, queries)
            // Only add resources that actually exist in the module (have locations)
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:146-149
            // Original: search_results = module._search_resource_locations(module, queries)
            //           for query, locations in search_results.items():
            //               for _location in locations:
            //                   linked_resources.add(query)
            foreach (var query in queries)
            {
                // Check if the resource exists in the module
                // Module.Resource() returns the ModuleResource if it exists in the module, null otherwise
                ModuleResource moduleResource = module.Resource(query.ResName, query.ResType);

                // Only add the resource if it exists and has locations (is available)
                // A resource is considered found if it has locations or is active
                // This matches the PyKotor behavior where resources are only added if they have locations
                if (moduleResource != null)
                {
                    // Check if the resource has locations or is active
                    // Resources with locations are available in the module
                    List<string> locations = moduleResource.Locations();
                    if (locations != null && locations.Count > 0)
                    {
                        // Resource exists in module - add it as a linked resource
                        linkedResources.Add(query);
                    }
                    else if (moduleResource.IsActive())
                    {
                        // Resource is active even if no explicit locations (may be in override or chitin)
                        // Add it as a linked resource
                        linkedResources.Add(query);
                    }
                }
            }

            return linkedResources;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:153-170
        // Original: def _extract_references_from_gff(self, data: bytes) -> set[ResourceIdentifier]:
        private HashSet<ResourceIdentifier> ExtractReferencesFromGff(byte[] data)
        {
            var references = new HashSet<ResourceIdentifier>();

            try
            {
                var reader = new GFFBinaryReader(data);
                var gff = reader.Load();
                if (gff == null)
                {
                    return references;
                }

                // Traverse GFF fields looking for references to other resources
                TraverseGffFields(gff.Root, references);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to extract GFF references: {ex.Message}");
            }

            return references;
        }

        private void TraverseGffFields(GFFStruct gffStruct, HashSet<ResourceIdentifier> references)
        {
            foreach (var (label, fieldType, value) in gffStruct)
            {
                if (fieldType == GFFFieldType.ResRef)
                {
                    var resref = value as ResRef;
                    if (resref != null)
                    {
                        references.Add(new ResourceIdentifier(resref.ToString(), ResourceType.INVALID));
                    }
                }
                else if (fieldType == GFFFieldType.Struct)
                {
                    var nestedStruct = value as GFFStruct;
                    if (nestedStruct != null)
                    {
                        TraverseGffFields(nestedStruct, references);
                    }
                }
                else if (fieldType == GFFFieldType.List)
                {
                    var list = value as GFFList;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            TraverseGffFields(item, references);
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:172-182
        // Original: def _extract_references_from_model(self, model_data: bytes) -> set[ResourceIdentifier]:
        private HashSet<ResourceIdentifier> ExtractReferencesFromModel(byte[] modelData)
        {
            var lookupTextureQueries = new HashSet<string>();

            try
            {
                var textures = ModelTools.IterateTextures(modelData);
                var lightmaps = ModelTools.IterateLightmaps(modelData);
                lookupTextureQueries.UnionWith(textures);
                lookupTextureQueries.UnionWith(lightmaps);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to extract texture/lightmap references: {ex.Message}");
            }

            var result = new HashSet<ResourceIdentifier>();
            foreach (var texture in lookupTextureQueries)
            {
                result.Add(new ResourceIdentifier(texture, ResourceType.TGA));
            }

            return result;
        }

        private string ComputeSha1Hash(string filepath)
        {
            try
            {
                using (var sha1 = SHA1.Create())
                using (var stream = File.OpenRead(filepath))
                {
                    byte[] hash = sha1.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch
            {
                return "";
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:184-206
        // Original: def summarize(self) -> None:
        public void Summarize()
        {
            Console.WriteLine("\nSummary:");
            Console.WriteLine("--------");

            if (_resourcesInfo.Count > 0)
            {
                Console.WriteLine("\nResources Information:");
                foreach (var kvp in _resourcesInfo)
                {
                    var identifier = kvp.Key;
                    var info = kvp.Value;
                    Console.WriteLine($"Resource '{identifier}':");
                    Console.WriteLine($"  - Appears in modules: {string.Join(", ", info.Modules)}");
                    if (info.IsMissing)
                    {
                        Console.WriteLine("  - Status: Missing");
                        if (info.ImpactOfMissing != null)
                        {
                            Console.WriteLine($"  - Impact: {info.ImpactOfMissing}");
                        }
                    }
                    else if (info.IsUnused)
                    {
                        Console.WriteLine("  - Status: Unused");
                    }
                    if (info.DependentResources.Count > 0)
                    {
                        Console.WriteLine($"  - Depends on: {string.Join(", ", info.DependentResources)}");
                    }
                    if (info.Modules.Count > 1)
                    {
                        Console.WriteLine("  - Conflict: Appears in multiple modules");
                    }
                    Console.WriteLine($"  - File Resources: {info.FileResources.Count} instances found.");
                    Console.WriteLine($"  - Resource Hashes: {string.Join(", ", info.ResourceHashes.Select(hashPair => $"{hashPair.Key}={hashPair.Value}"))}");
                }
            }

            if (_conflictingResources.Count > 0)
            {
                Console.WriteLine("\nConflicting Resources:");
                foreach (var kvp in _conflictingResources)
                {
                    var resname = kvp.Key;
                    var modules = kvp.Value;
                    Console.WriteLine($"Resource '{resname}' found in modules: {string.Join(", ", modules)}");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:212-240
        // Original: def extract_all_resources(self, module_name: str, output_dir: str) -> None:
        public void ExtractAllResources(string moduleName, string outputDir)
        {
            bool useDotMod = Andastra.Parsing.Tools.FileHelpers.IsModFile(moduleName);
            var module = new Module(moduleName, _installation, useDotMod: useDotMod);
            var moduleDir = Path.Combine(outputDir, moduleName);
            Directory.CreateDirectory(moduleDir);

            Console.WriteLine($"Extracting resources from module '{moduleName}' to '{moduleDir}'...");

            foreach (var kvp in module.Resources)
            {
                var identifier = kvp.Key;
                var modRes = kvp.Value;

                byte[] resourceData = null;
                if (modRes is ModuleResource<object> typedRes)
                {
                    resourceData = typedRes.Data();
                }

                if (resourceData == null)
                {
                    Console.WriteLine($"Missing resource: {identifier}");
                    if (!_missingResources.TryGetValue(moduleName, out List<ResourceIdentifier> missing))
                    {
                        missing = new List<ResourceIdentifier>();
                        _missingResources[moduleName] = missing;
                    }
                    missing.Add(identifier);
                    continue;
                }

                string resourceFilename = $"{identifier.ResName}.{identifier.ResType.Extension}";
                string resourcePath = Path.Combine(moduleDir, resourceFilename);

                try
                {
                    File.WriteAllBytes(resourcePath, resourceData);
                    Console.WriteLine($"Extracted: {resourceFilename}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to extract {resourceFilename}: {ex.Message}");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:242-249
        // Original: def build_resource_to_modules_mapping(self) -> None:
        public void BuildResourceToModulesMapping()
        {
            Console.WriteLine("Building resource to modules mapping...");
            // Note: Installation.ModuleNames() may not exist, using alternative approach
            // Get modules from installation path
            string modulesPath = Installation.Installation.GetModulesPath(_installation.Path);
            if (!Directory.Exists(modulesPath))
            {
                return;
            }

            foreach (var moduleFile in Directory.GetFiles(modulesPath, "*.mod").Concat(Directory.GetFiles(modulesPath, "*.rim")))
            {
                string moduleName = Path.GetFileNameWithoutExtension(moduleFile);
                if (moduleName.EndsWith("_s"))
                {
                    moduleName = moduleName.Substring(0, moduleName.Length - 2);
                }
                else if (moduleName.EndsWith("_dlg"))
                {
                    moduleName = moduleName.Substring(0, moduleName.Length - 4);
                }

                bool useDotMod = Andastra.Parsing.Tools.FileHelpers.IsModFile(moduleFile);
                try
                {
                    var module = new Module(moduleName, _installation, useDotMod: useDotMod);
                    foreach (var identifier in module.Resources.Keys)
                    {
                        if (!_resourceToModules.TryGetValue(identifier.ResName.ToLowerInvariant(), out HashSet<string> modules))
                        {
                            modules = new HashSet<string>();
                            _resourceToModules[identifier.ResName.ToLowerInvariant()] = modules;
                        }
                        modules.Add(moduleName);
                    }
                }
                catch
                {
                    // Skip modules that fail to load
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:251-260
        // Original: def check_for_conflicts(self) -> None:
        public void CheckForConflicts()
        {
            if (_resourceToModules.Count == 0)
            {
                BuildResourceToModulesMapping();
            }

            Console.WriteLine("Checking for conflicting resources across modules...");
            foreach (var kvp in _resourceToModules)
            {
                var resname = kvp.Key;
                var modules = kvp.Value;
                if (modules.Count > 1)
                {
                    var identifier = new ResourceIdentifier(resname, ResourceType.INVALID);
                    if (!_conflictingResources.TryGetValue(identifier, out HashSet<string> conflicts))
                    {
                        conflicts = new HashSet<string>();
                        _conflictingResources[identifier] = conflicts;
                    }
                    conflicts.UnionWith(modules);
                    Console.WriteLine($"Conflict: Resource '{resname}' found in modules {string.Join(", ", modules)}");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:262-274
        // Original: def find_missing_resources(self, module_name: str) -> None:
        public void FindMissingResources(string moduleName)
        {
            bool useDotMod = Andastra.Parsing.Tools.FileHelpers.IsModFile(moduleName);
            var module = new Module(moduleName, _installation, useDotMod: useDotMod);
            Console.WriteLine($"Checking for missing resources in module '{moduleName}'...");

            foreach (var kvp in module.Resources)
            {
                var identifier = kvp.Key;
                var modRes = kvp.Value;
                byte[] data = null;
                if (modRes is ModuleResource<object> typedRes)
                {
                    data = typedRes.Data();
                }
                if (data == null)
                {
                    if (!_missingResources.TryGetValue(moduleName, out List<ResourceIdentifier> missing))
                    {
                        missing = new List<ResourceIdentifier>();
                        _missingResources[moduleName] = missing;
                    }
                    missing.Add(identifier);
                    Console.WriteLine($"Missing resource: {identifier}");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:276-289
        // Original: def find_unused_resources(self, module_name: str) -> None:
        public void FindUnusedResources(string moduleName)
        {
            bool useDotMod = Andastra.Parsing.Tools.FileHelpers.IsModFile(moduleName);
            var module = new Module(moduleName, _installation, useDotMod: useDotMod);
            Console.WriteLine($"Checking for unused resources in module '{moduleName}'...");

            foreach (var kvp in module.Resources)
            {
                var identifier = kvp.Key;
                var modRes = kvp.Value;
                if (!modRes.IsActive())
                {
                    if (!_unusedResources.TryGetValue(moduleName, out List<ResourceIdentifier> unused))
                    {
                        unused = new List<ResourceIdentifier>();
                        _unusedResources[moduleName] = unused;
                    }
                    unused.Add(identifier);
                    Console.WriteLine($"Unused resource: {identifier}");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:291-329
        // Original: def move_override_to_modules(self, override_dir: str, output_dir: str) -> None:
        public void MoveOverrideToModules(string overrideDir, string outputDir)
        {
            if (!Directory.Exists(overrideDir))
            {
                Console.WriteLine($"Override directory '{overrideDir}' does not exist.");
                return;
            }

            if (_resourceToModules.Count == 0)
            {
                BuildResourceToModulesMapping();
            }

            Console.WriteLine($"Moving resources from override directory '{overrideDir}' to module folders...");

            foreach (var resourceFile in Directory.GetFiles(overrideDir))
            {
                string fileName = Path.GetFileName(resourceFile);
                string resname = Path.GetFileNameWithoutExtension(resourceFile);
                string extension = Path.GetExtension(resourceFile);
                if (string.IsNullOrEmpty(extension) || extension.Length < 2)
                {
                    continue;
                }

                ResourceType restype = ResourceType.FromExtension(extension.Substring(1));
                var identifier = new ResourceIdentifier(resname, restype);

                if (!_resourceToModules.TryGetValue(resname.ToLowerInvariant(), out HashSet<string> modules))
                {
                    Console.WriteLine($"Resource '{fileName}' does not belong to any module.");
                    continue;
                }

                string resourceFileName = Path.GetFileName(resourceFile);
                foreach (var moduleName in modules)
                {
                    string moduleDir = Path.Combine(outputDir, moduleName);
                    Directory.CreateDirectory(moduleDir);
                    string destination = Path.Combine(moduleDir, resourceFileName);

                    try
                    {
                        File.Copy(resourceFile, destination, overwrite: true);
                        Console.WriteLine($"Copied '{resourceFileName}' to '{destination}'.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to copy '{resourceFileName}' to '{destination}': {ex.Message}");
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/merge/module.py:331-359
        // Original: def summarize(self) -> None: (second implementation)
        public void SummarizeMissingUnusedConflicts()
        {
            Console.WriteLine("\nSummary:");
            Console.WriteLine("--------");

            if (_missingResources.Count > 0)
            {
                Console.WriteLine("\nMissing Resources:");
                foreach (var kvp in _missingResources)
                {
                    var module = kvp.Key;
                    var resources = kvp.Value;
                    Console.WriteLine($"Module '{module}':");
                    foreach (var res in resources)
                    {
                        Console.WriteLine($"  - {res}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\nNo missing resources found.");
            }

            if (_unusedResources.Count > 0)
            {
                Console.WriteLine("\nUnused Resources:");
                foreach (var kvp in _unusedResources)
                {
                    var module = kvp.Key;
                    var resources = kvp.Value;
                    Console.WriteLine($"Module '{module}':");
                    foreach (var res in resources)
                    {
                        Console.WriteLine($"  - {res}");
                    }
                }
            }
            else
            {
                Console.WriteLine("\nNo unused resources found.");
            }

            if (_conflictingResources.Count > 0)
            {
                Console.WriteLine("\nConflicting Resources:");
                foreach (var kvp in _conflictingResources)
                {
                    var resname = kvp.Key;
                    var modules = kvp.Value;
                    Console.WriteLine($"Resource '{resname}' found in modules: {string.Join(", ", modules)}");
                }
            }
            else
            {
                Console.WriteLine("\nNo conflicting resources found.");
            }
        }
    }
}
