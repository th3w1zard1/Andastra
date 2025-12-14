// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:642-720
// Original: def _add_to_install_folder(...), def _ensure_capsule_install(...), def _create_patch_for_missing_file(...)
// Helper functions for installation diff operations

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Resources;
using KotorDiff.NET.Diff;
using KotorDiff.NET.Generator;
using JetBrains.Annotations;

namespace KotorDiff.NET.Resolution
{
    /// <summary>
    /// Helper functions for installation diff operations.
    /// 1:1 port from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py
    /// </summary>
    internal static class InstallationDiffHelpers
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
            IncrementalTSLPatchDataWriter incrementalWriter = null,
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
            IncrementalTSLPatchDataWriter incrementalWriter = null)
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

            // TODO: Create empty .mod file if needed
            // This would require ERF creation functionality
            logFunc($"    Note: Module '{capsuleFilename}' will need to be created");
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
            IncrementalTSLPatchDataWriter incrementalWriter = null)
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

            // TODO: Implement patch creation using analyzers
            // This requires DiffAnalyzerFactory and format-specific analyzers
            // For now, we'll skip patch creation and just add to install list
            logFunc($"  Note: Patch creation for {filename} not yet implemented");
        }
    }
}

