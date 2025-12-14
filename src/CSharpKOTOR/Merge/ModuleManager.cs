using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Tools;
using JetBrains.Annotations;

namespace CSharpKOTOR.Merge
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
                            resname: modRes.Resname(),
                            restype: modRes.Restype(),
                            size: (int)fileInfo.Length,
                            offset: 0,
                            filepath: location
                        );
                        resourceInfo.FileResources.Add(fileResource);
                        string resourceHash = fileResource.GetSha1Hash();
                        resourceInfo.ResourceHashes[fileResource.Filepath().Replace('\\', '/')] = resourceHash;
                    }

                    // Check for unused resources
                    if (!modRes.IsActive())
                    {
                        resourceInfo.IsUnused = true;
                    }

                    // If the resource data is missing, mark it as missing
                    if (modRes.Data() == null)
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
            if (modRes.Restype() == ResourceType.GIT || modRes.Restype() == ResourceType.LYT || modRes.Restype() == ResourceType.VIS)
            {
                var linkedResources = SearchLinkedResources(module, modRes);
                dependencies.UnionWith(linkedResources);
            }

            // Extract dependencies from GFF files
            if (modRes.Restype() == ResourceType.GFF || modRes.Restype() == ResourceType.ARE || 
                modRes.Restype() == ResourceType.IFO || modRes.Restype() == ResourceType.DLG)
            {
                byte[] data = modRes.Data();
                if (data != null)
                {
                    var references = ExtractReferencesFromGff(data);
                    dependencies.UnionWith(references);
                }
            }

            // Extract texture and model dependencies
            if (modRes.Restype() == ResourceType.MDL || modRes.Restype() == ResourceType.MDX)
            {
                byte[] modelData = modRes.Data();
                if (modelData != null)
                {
                    var modelRefs = ExtractReferencesFromModel(modelData);
                    dependencies.UnionWith(modelRefs);
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

            // Note: This would need to be implemented based on Module's internal search mechanism
            // For now, we'll add the queries as dependencies
            foreach (var query in queries)
            {
                linkedResources.Add(query);
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
                var gff = GFFAuto.ReadGff(data);
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
            foreach (var field in gffStruct.Fields())
            {
                if (field.Type == GFFFieldType.ResRef)
                {
                    var resref = field.Value as ResRef;
                    if (resref != null)
                    {
                        references.Add(new ResourceIdentifier(resref.ToString(), ResourceType.UNKNOWN));
                    }
                }
                else if (field.Type == GFFFieldType.Struct)
                {
                    var nestedStruct = field.Value as GFFStruct;
                    if (nestedStruct != null)
                    {
                        TraverseGffFields(nestedStruct, references);
                    }
                }
                else if (field.Type == GFFFieldType.List)
                {
                    var list = field.Value as GFFList;
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
                var textures = Model.IterateTextures(modelData);
                var lightmaps = Model.IterateLightmaps(modelData);
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
                    Console.WriteLine($"  - Resource Hashes: {string.Join(", ", info.ResourceHashes.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
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
            bool useDotMod = Misc.IsModFile(moduleName);
            var module = new Module(moduleName, _installation, useDotMod: useDotMod);
            var moduleDir = Path.Combine(outputDir, moduleName);
            Directory.CreateDirectory(moduleDir);

            Console.WriteLine($"Extracting resources from module '{moduleName}' to '{moduleDir}'...");

            foreach (var kvp in module.Resources)
            {
                var identifier = kvp.Key;
                var modRes = kvp.Value;

                byte[] resourceData = modRes.Data();
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

                string resourceFilename = $"{identifier.Resname}.{identifier.Restype.Extension()}";
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
            var moduleNames = _installation.ModuleNames();
            foreach (var moduleName in moduleNames.Keys)
            {
                bool useDotMod = Misc.IsModFile(moduleName);
                var module = new Module(moduleName, _installation, useDotMod: useDotMod);
                foreach (var identifier in module.Resources.Keys)
                {
                    if (!_resourceToModules.TryGetValue(identifier.Resname.ToLowerInvariant(), out HashSet<string> modules))
                    {
                        modules = new HashSet<string>();
                        _resourceToModules[identifier.Resname.ToLowerInvariant()] = modules;
                    }
                    modules.Add(moduleName);
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
                    var identifier = new ResourceIdentifier(resname, ResourceType.UNKNOWN);
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
            bool useDotMod = Misc.IsModFile(moduleName);
            var module = new Module(moduleName, _installation, useDotMod: useDotMod);
            Console.WriteLine($"Checking for missing resources in module '{moduleName}'...");

            foreach (var kvp in module.Resources)
            {
                var identifier = kvp.Key;
                var modRes = kvp.Value;
                if (modRes.Data() == null)
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
            bool useDotMod = Misc.IsModFile(moduleName);
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

                foreach (var moduleName in modules)
                {
                    string moduleDir = Path.Combine(outputDir, moduleName);
                    Directory.CreateDirectory(moduleDir);
                    string destination = Path.Combine(moduleDir, fileName);

                    try
                    {
                        File.Copy(resourceFile, destination, overwrite: true);
                        Console.WriteLine($"Copied '{fileName}' to '{destination}'.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to copy '{fileName}' to '{destination}': {ex.Message}");
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
