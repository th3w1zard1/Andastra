// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:642-720
// Original: def _add_to_install_folder(...), def _ensure_capsule_install(...), def _create_patch_for_missing_file(...)
// Helper functions for installation diff operations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Mods.SSF;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common;
using KotorDiff.Diff;
using Andastra.Parsing.TSLPatcher;
using JetBrains.Annotations;

namespace KotorDiff.Diff
{
    /// <summary>
    /// Helper functions for installation diff operations.
    /// 1:1 port from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py
    /// </summary>
    public static class InstallationDiffHelpers
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:642-720
        // Original: def _add_to_install_folder(...): ...
        /// <summary>
        /// Add a file to an install folder, creating the folder entry if needed.
        /// </summary>
        public static void AddToInstallFolder(
            ModificationsByType modificationsByType,
            string folder,
            string filename,
            Action<string> logFunc = null,
            byte[] moddedData = null,
            string moddedPath = null,
            DiffContext context = null,
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null,
            bool createPatch = true)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            // Check if this file already exists in install list
            bool fileExists = false;
            foreach (var installFile in modificationsByType.Install)
            {
                if (string.Equals(installFile.Destination, folder, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(installFile.SaveAs ?? installFile.SourceFile, filename, StringComparison.OrdinalIgnoreCase))
                {
                    fileExists = true;
                    break;
                }
            }

            if (!fileExists)
            {
                // Create new InstallFile entry
                modificationsByType.Install.Add(new InstallFile(filename, destination: folder));
                logFunc($"\n[INSTALL] {filename}");
                logFunc($"  |-- Filename: {filename}");
                logFunc($"  |-- Destination: {folder}");
                logFunc("  +-- tslpatchdata: File will be copied from appropriate source");
            }

            // Ensure host capsule is staged when targeting resources within a .mod
            string folderLower = folder.ToLowerInvariant();
            if (folderLower.EndsWith(".mod"))
            {
                string capsuleDestination = folderLower.EndsWith(".mod") ? folder : $"{folder}\\{filename}";
                string capsuleSourcePath = null;
                if (!string.IsNullOrEmpty(moddedPath) && File.Exists(moddedPath))
                {
                    capsuleSourcePath = moddedPath;
                }

                EnsureCapsuleInstall(
                    modificationsByType,
                    capsuleDestination,
                    capsulePath: capsuleSourcePath,
                    logFunc: logFunc,
                    incrementalWriter: incrementalWriter);
            }

            // Also create a patch modification (file will be patched after installation)
            // Only create patch if requested (avoid duplicates when diff_data already created one)
            if (createPatch && (moddedData != null || !string.IsNullOrEmpty(moddedPath)))
            {
                CreatePatchForMissingFile(
                    modificationsByType,
                    filename,
                    folder,
                    moddedData: moddedData,
                    moddedPath: moddedPath,
                    context: context,
                    logFunc: logFunc,
                    incrementalWriter: incrementalWriter);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:264-320
        // Original: def _ensure_capsule_install(...): ...
        /// <summary>
        /// Ensure a capsule (.mod) exists in tslpatchdata and is listed before patching.
        /// </summary>
        public static void EnsureCapsuleInstall(
            ModificationsByType modificationsByType,
            string capsuleDestination,
            string capsulePath = null,
            Action<string> logFunc = null,
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            string normalizedDestination = capsuleDestination.Replace("/", "\\");
            string capsuleFilename = Path.GetFileName(normalizedDestination);
            string capsuleSuffix = Path.GetExtension(capsuleFilename).ToLowerInvariant();

            if (capsuleSuffix != ".mod")
            {
                return;
            }

            string capsuleFolder = Path.GetDirectoryName(normalizedDestination) ?? ".";
            if (string.IsNullOrEmpty(capsuleFolder) || capsuleFolder == ".")
            {
                capsuleFolder = ".";
            }

            string filenameLower = capsuleFilename.ToLowerInvariant();
            string folderLower = capsuleFolder.ToLowerInvariant();

            bool alreadyPresent = modificationsByType.Install.Any(
                installFile => string.Equals(installFile.Destination, folderLower, StringComparison.OrdinalIgnoreCase) &&
                              string.Equals(installFile.SaveAs ?? installFile.SourceFile, filenameLower, StringComparison.OrdinalIgnoreCase));

            if (!alreadyPresent)
            {
                var installEntry = new InstallFile(
                    capsuleFilename,
                    replaceExisting: true,
                    destination: capsuleFolder);
                modificationsByType.Install.Add(installEntry);
            }

            if (incrementalWriter == null || alreadyPresent)
            {
                return;
            }

            if (!string.IsNullOrEmpty(capsulePath) && File.Exists(capsulePath))
            {
                incrementalWriter.AddInstallFile(capsuleFolder, capsuleFilename, capsulePath);
                logFunc($"    Staged module '{capsuleFilename}' for installation");
                return;
            }

            // Otherwise, add to install folder and create empty MOD if needed
            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:314-319
            incrementalWriter.AddInstallFile(capsuleFolder, capsuleFilename, null);
            string outputPath = Path.Combine(incrementalWriter.TslpatchdataPath, capsuleFilename);
            if (!File.Exists(outputPath))
            {
                // Create empty MOD file
                var emptyMod = new ERF(ERFType.MOD);
                ERFAuto.WriteErf(emptyMod, outputPath, ResourceType.MOD);
                logFunc($"    Created empty module '{capsuleFilename}' in tslpatchdata");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:485-596
        // Original: def _create_patch_for_missing_file(...): ...
        /// <summary>
        /// Create a patch modification for a file that doesn't exist in vanilla.
        /// This creates patch modifications that will be applied after the file is installed via InstallList.
        /// </summary>
        public static void CreatePatchForMissingFile(
            ModificationsByType modificationsByType,
            string filename,
            string folder,
            byte[] moddedData = null,
            string moddedPath = null,
            DiffContext context = null,
            Action<string> logFunc = null,
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            // Get modded file data
            if (moddedData == null)
            {
                if (string.IsNullOrEmpty(moddedPath) || !File.Exists(moddedPath))
                {
                    logFunc($"  Warning: Cannot create patch for {filename} - no data provided");
                    return;
                }
                moddedData = File.ReadAllBytes(moddedPath);
            }

            // Determine file extension
            string fileExt = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(fileExt))
            {
                return; // No extension, can't determine type
            }

            // Get analyzer for this file type
            var analyzer = DiffAnalyzerFactory.GetAnalyzer(fileExt);
            if (analyzer == null)
            {
                return; // No analyzer for this type, skip patch creation
            }

            try
            {
                // Create an empty/minimal file of the same type for comparison
                // For patchable formats, we'll compare modded file against an empty structure
                byte[] emptyData = CreateEmptyFileData(fileExt);
                if (emptyData == null)
                {
                    // Can't create empty file for this type, skip patch
                    return;
                }

                // Create identifier for the file (use context if available, otherwise construct)
                string identifier = context != null ? context.Where : filename;

                // Analyze differences (comparing modded file against empty)
                var result = analyzer.Analyze(emptyData, moddedData, identifier);
                if (result == null)
                {
                    return;
                }

                PatcherModifications modifications;
                Dictionary<int, int> strrefMappings = null;
                if (result is ValueTuple<PatcherModifications, Dictionary<int, int>> tuple)
                {
                    modifications = tuple.Item1;
                    strrefMappings = tuple.Item2;

                    // Store strref_mappings in writer metadata for TLK modifications
                    // This enables StrRef reference finding and linking patches
                    // Based on PyKotor implementation: vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1547-1576
                    // Matching Resolution/InstallationDiffHelpers.cs implementation for consistency
                    if (incrementalWriter != null && strrefMappings != null && strrefMappings.Count > 0)
                    {
                        // Only store strref_mappings for TLK modifications
                        // Other modification types (2DA, GFF, SSF) don't use strref_mappings
                        if (modifications is Andastra.Parsing.Mods.TLK.ModificationsTLK modTlk)
                        {
                            incrementalWriter.SetTlkMetadata(modTlk, "strref_mappings", strrefMappings);
                            logFunc($"  |-- StrRef mappings: {strrefMappings.Count} mappings stored for linking patches");
                        }
                    }
                }
                else if (result is PatcherModifications mods)
                {
                    modifications = mods;
                }
                else
                {
                    return;
                }

                if (modifications == null)
                {
                    return;
                }

                logFunc($"\n[PATCH] {filename}");

                // Set destination and sourcefile
                string resourceName = Path.GetFileName(filename);
                modifications.Destination = folder;
                modifications.SourceFile = resourceName;

                if (modifications is Andastra.Parsing.Mods.TwoDA.Modifications2DA mod2da)
                {
                    modificationsByType.Twoda.Add(mod2da);
                    logFunc("  |-- Type: [2DAList]");
                }
                else if (modifications is Andastra.Parsing.Mods.GFF.ModificationsGFF modGff)
                {
                    modGff.SaveAs = resourceName;
                    modificationsByType.Gff.Add(modGff);
                    logFunc("  |-- Type: [GFFList]");
                }
                else if (modifications is Andastra.Parsing.Mods.SSF.ModificationsSSF modSsf)
                {
                    modificationsByType.Ssf.Add(modSsf);
                    logFunc("  |-- Type: [SSFList]");
                }
                else
                {
                    // Unknown type, skip
                    return;
                }

                // Get modifier count based on type
                int modifiersCount = 0;
                if (modifications is Andastra.Parsing.Mods.TwoDA.Modifications2DA mod2daCount)
                {
                    modifiersCount = mod2daCount.Modifiers != null ? mod2daCount.Modifiers.Count : 0;
                }
                else if (modifications is Andastra.Parsing.Mods.GFF.ModificationsGFF modGff2)
                {
                    modifiersCount = modGff2.Modifiers != null ? modGff2.Modifiers.Count : 0;
                }
                else if (modifications is Andastra.Parsing.Mods.SSF.ModificationsSSF modSsf2)
                {
                    modifiersCount = modSsf2.Modifiers != null ? modSsf2.Modifiers.Count : 0;
                }

                if (modifiersCount > 0)
                {
                    logFunc($"  |-- Modifications: {modifiersCount} changes");
                }

                logFunc("  +-- tslpatchdata: Will use installed file as base, then apply patch");

                // Write immediately if using incremental writer
                if (incrementalWriter != null)
                {
                    // Use the modded file as the "vanilla" source (it's what will be installed)
                    incrementalWriter.WriteModification(modifications);
                }
            }
            catch (Exception e)
            {
                logFunc($"  Warning: Failed to create patch for {filename}: {e.GetType().Name}: {e.Message}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:598-639
        // Original: def _create_empty_file_data(ext: str) -> bytes | None: ...
        /// <summary>
        /// Create an empty/minimal file data for a given extension.
        /// </summary>
        public static byte[] CreateEmptyFileData(string ext)
        {
            if (string.IsNullOrEmpty(ext))
            {
                return null;
            }

            string extLower = ext.ToLowerInvariant();

            try
            {
                // For 2DA files, create empty 2DA
                if (extLower == "2da")
                {
                    var empty2da = new Andastra.Parsing.Formats.TwoDA.TwoDA();
                    return Andastra.Parsing.Formats.TwoDA.TwoDAAuto.Bytes2DA(empty2da, ResourceType.TwoDA);
                }

                // For SSF files, create empty SSF
                if (extLower == "ssf")
                {
                    var emptySsf = new Andastra.Parsing.Formats.SSF.SSF();
                    return Andastra.Parsing.Formats.SSF.SSFAuto.BytesSsf(emptySsf, ResourceType.SSF);
                }

                // For GFF files, create empty GFF with appropriate content type based on extension
                var gffTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "utc", "uti", "utp", "ute", "utm", "utd", "utw", "dlg", "are", "git", "ifo", "gui", "jrl", "fac", "gff"
                };

                if (gffTypes.Contains(extLower))
                {
                    // Try to determine GFFContent from extension
                    Andastra.Parsing.Formats.GFF.GFFContent gffContent;
                    try
                    {
                        // Map extension to GFFContent enum using FromResName (pass filename with extension)
                        string filename = $"dummy.{ext}";
                        gffContent = Andastra.Parsing.Formats.GFF.GFFContentExtensions.FromResName(filename);
                    }
                    catch
                    {
                        // Fallback to generic GFF content type
                        gffContent = Andastra.Parsing.Formats.GFF.GFFContent.GFF;
                    }

                    // Create empty GFF with determined content type
                    var emptyGff = new Andastra.Parsing.Formats.GFF.GFF(gffContent);
                    return Andastra.Parsing.Formats.GFF.GFFAuto.BytesGff(emptyGff, ResourceType.GFF);
                }
            }
            catch (Exception e)
            {
                // Log the exception for debugging
                Console.WriteLine($"Failed to create empty file data for extension '{ext}': {e.GetType().Name}: {e.Message}");
            }

            return null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:322-380
        // Original: def _extract_and_add_capsule_resources(...): ...
        /// <summary>
        /// Extract all resources from a capsule and add them to install folders.
        /// </summary>
        public static void ExtractAndAddCapsuleResources(
            string capsulePath,
            ModificationsByType modificationsByType,
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter,
            Action<string> logFunc)
        {
            try
            {
                var capsule = new Andastra.Parsing.Formats.Capsule.Capsule(capsulePath);
                string capsuleName = Path.GetFileName(capsulePath);

                // Determine destination based on capsule location and type
                string destination = "Override";
                string parentPath = Path.GetDirectoryName(capsulePath);
                if (!string.IsNullOrEmpty(parentPath))
                {
                    var parentInfo = new DirectoryInfo(parentPath);
                    var parentNames = new List<string>();
                    var current = parentInfo;
                    while (current != null)
                    {
                        parentNames.Add(current.Name.ToLowerInvariant());
                        current = current.Parent;
                    }

                    if (parentNames.Contains("modules"))
                    {
                        destination = $"modules\\{capsuleName}";
                    }
                }

                if (Path.GetExtension(capsulePath).ToLowerInvariant() == ".mod")
                {
                    string capsuleDestinationStr = destination.ToLowerInvariant().EndsWith(".mod") ? destination : $"{destination}\\{capsuleName}";
                    EnsureCapsuleInstall(
                        modificationsByType,
                        capsuleDestinationStr,
                        capsulePath: capsulePath,
                        logFunc: logFunc,
                        incrementalWriter: incrementalWriter);
                }

                int resourceCount = 0;
                foreach (var resource in capsule)
                {
                    string resname = resource.ResName;
                    ResourceType restype = resource.ResType;
                    string filename = $"{resname}.{restype.Extension.ToLowerInvariant()}";

                    // Add to install folder
                    AddToInstallFolder(
                        modificationsByType,
                        destination,
                        filename,
                        logFunc: logFunc,
                        createPatch: false); // Don't create patch here, just add to install

                    // Extract and copy immediately if incremental writer available
                    if (incrementalWriter != null)
                    {
                        incrementalWriter.AddInstallFile(destination, filename, capsulePath);
                    }

                    resourceCount++;
                }

                logFunc($"    Extracted {resourceCount} resources from {capsuleName}");
            }
            catch (Exception e)
            {
                logFunc($"  [Error] Failed to extract resources from capsule {Path.GetFileName(capsulePath)}: {e.GetType().Name}: {e.Message}");
                logFunc("  Full traceback:");
                logFunc($"    {e}");
            }
        }
    }
}

