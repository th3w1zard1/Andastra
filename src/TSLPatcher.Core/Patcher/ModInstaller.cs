using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Formats.Capsule;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods;
using TSLPatcher.Core.Reader;
using TSLPatcher.Core.Resources;

namespace TSLPatcher.Core.Patcher;

/// <summary>
/// Main orchestrator for installing TSLPatcher mods.
/// </summary>
public class ModInstaller
{
    private readonly string modPath;
    private readonly string gamePath;
    private readonly string changesIniPath;
    private readonly PatchLogger log;

    private PatcherConfig? config;
    private string? backup;
    private readonly HashSet<string> processedBackupFiles = new();

    public Common.Game? Game { get; private set; }
    public string? TslPatchDataPath { get; set; }

    public ModInstaller(
        string modPath,
        string gamePath,
        string changesIniPath,
        PatchLogger? logger = null)
    {
        this.modPath = modPath ?? throw new ArgumentNullException(nameof(modPath));
        this.gamePath = gamePath ?? throw new ArgumentNullException(nameof(gamePath));
        this.changesIniPath = changesIniPath ?? throw new ArgumentNullException(nameof(changesIniPath));
        log = logger ?? new PatchLogger();

        Game = Installation.Installation.DetermineGame(this.gamePath);

        // Handle legacy syntax - look for changes.ini in various locations
        if (!File.Exists(this.changesIniPath))
        {
            string fileName = Path.GetFileName(this.changesIniPath);
            this.changesIniPath = Path.Combine(this.modPath, fileName);

            if (!File.Exists(this.changesIniPath))
            {
                this.changesIniPath = Path.Combine(this.modPath, "tslpatchdata", fileName);
            }

            if (!File.Exists(this.changesIniPath))
            {
                throw new FileNotFoundException(
                    "Could not find the changes ini file on disk.",
                    this.changesIniPath);
            }
        }
    }

    /// <summary>
    /// Gets the patcher configuration, loading it if necessary.
    /// </summary>
    public PatcherConfig Config()
    {
        if (config != null)
        {
            return config;
        }

        if (!File.Exists(changesIniPath))
        {
            throw new FileNotFoundException($"Changes INI file not found: {changesIniPath}");
        }

        ConfigReader reader = ConfigReader.FromFilePath(changesIniPath, log);
        config = reader.Load(new PatcherConfig());

        // Check required files
        if (config.RequiredFiles.Count > 0)
        {
            for (int i = 0; i < config.RequiredFiles.Count; i++)
            {
                string[] files = config.RequiredFiles[i];
                foreach (string file in files)
                {
                    string requiredFilePath = Path.Combine(gamePath, "Override", file);
                    if (!File.Exists(requiredFilePath))
                    {
                        string message = i < config.RequiredMessages.Count
                            ? config.RequiredMessages[i].Trim()
                            : "cannot install - missing a required mod";
                        throw new InvalidOperationException(message);
                    }
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Creates a backup directory and returns its path.
    /// </summary>
    public (string backupPath, HashSet<string> processedFiles) GetBackup()
    {
        if (backup != null)
        {
            return (backup, processedBackupFiles);
        }

        string backupDir = modPath;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");

        // Find the root directory containing tslpatchdata
        while (!Directory.Exists(Path.Combine(backupDir, "tslpatchdata")) &&
               !string.IsNullOrEmpty(Path.GetDirectoryName(backupDir)))
        {
            backupDir = Path.GetDirectoryName(backupDir) ?? backupDir;
        }

        // Remove old uninstall directory if it exists
        string uninstallDir = Path.Combine(backupDir, "uninstall");
        if (Directory.Exists(uninstallDir))
        {
            try
            {
                Directory.Delete(uninstallDir, recursive: true);
            }
            catch (Exception ex)
            {
                log.AddWarning($"Could not initialize uninstall directory: {ex.Message}");
            }
        }

        // Create new backup directory
        backupDir = Path.Combine(backupDir, "backup", timestamp);
        try
        {
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception ex)
        {
            log.AddWarning($"Could not create backup folder: {ex.Message}");
        }

        log.AddNote($"Using backup directory: '{backupDir}'");
        backup = backupDir;

        return (backup, processedBackupFiles);
    }

    /// <summary>
    /// Installs the mod by applying all patches.
    /// </summary>
    public void Install(
        CancellationToken? cancellationToken = null,
        Action<int>? progressCallback = null)
    {
        if (Game == null)
        {
            throw new InvalidOperationException(
                "Chosen KOTOR directory is not a valid installation - cannot initialize ModInstaller.");
        }

        PatcherMemory memory = new();
        PatcherConfig cfg = Config();

        List<PatcherModifications> patchesList = new();
        patchesList.AddRange(cfg.InstallList);
        // Note: TSLPatcher executes [InstallList] after [TLKList]
        patchesList.AddRange(GetTlkPatches(cfg));
        patchesList.AddRange(cfg.Patches2DA);
        patchesList.AddRange(cfg.PatchesGFF);
        patchesList.AddRange(cfg.PatchesNSS);
        // Note: TSLPatcher executes [CompileList] after [HACKList]
        patchesList.AddRange(cfg.PatchesNCS);
        patchesList.AddRange(cfg.PatchesSSF);

        int totalPatches = patchesList.Count;
        int currentPatch = 0;

        log.AddNote($"Starting installation of {totalPatches} patches...");

        // Prepare NSS compilation if needed
        // TODO: Implement PrepareCompileList equivalent if we port NCS/NSS compilers
        // For now assuming simple token replacement in memory or similar

        foreach (PatcherModifications patch in patchesList)
        {
            cancellationToken?.ThrowIfCancellationRequested();

            currentPatch++;
            progressCallback?.Invoke(currentPatch);

            try
            {
                string outputContainerPath = Path.Combine(gamePath, patch.Destination);

                (bool exists, Capsule? capsule) = HandleCapsuleAndBackup(patch, outputContainerPath);

                if (!ShouldPatch(patch, exists, capsule))
                {
                    continue;
                }

                byte[]? dataToPatch = LookupResource(patch, outputContainerPath, exists, capsule);

                if (dataToPatch == null)
                {
                    log.AddError($"Could not locate resource to {patch.Action.ToLower().Trim()}: '{patch.SourceFile}'");
                    continue;
                }

                if (dataToPatch.Length == 0)
                {
                    log.AddNote($"'{patch.SourceFile}' has no content/data and is completely empty.");
                }

                object result = patch.PatchResource(dataToPatch, memory, log, Game.Value);

                // If PatchResource returns the boolean true, it means skip
                if (result is bool b && b)
                {
                    log.AddNote($"Skipping '{patch.SourceFile}' - patch_resource determined that this file can be skipped.");
                    continue;
                }

                if (result is byte[] patchedData)
                {
                    if (capsule != null)
                    {
                        HandleOverrideType(patch);
                        HandleModRimShadow(patch);

                        (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
                        capsule.Add(resName, resType, patchedData);
                        capsule.Save();
                    }
                    else
                    {
                        string destinationDir = Path.GetDirectoryName(outputContainerPath);
                        if (!string.IsNullOrEmpty(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        string destinationPath;
                        if (Directory.Exists(outputContainerPath))
                        {
                            destinationPath = Path.Combine(outputContainerPath, patch.SaveAs);
                        }
                        else
                        {
                            // If outputContainerPath doesn't exist, maybe it's meant to be the directory?
                            // TSLPatcher creates directories as needed.
                            Directory.CreateDirectory(outputContainerPath);
                            destinationPath = Path.Combine(outputContainerPath, patch.SaveAs);
                        }

                        File.WriteAllBytes(destinationPath, patchedData);
                    }

                    log.CompletePatch();
                }
            }
            catch (Exception ex)
            {
                log.AddError($"Error applying patch: {ex.Message}");
                // throw; // Don't stop entire installation on one file error, usually?
                // TSLPatcher behavior usually continues unless it's critical.
                // For now we catch and log.
            }
        }

        log.AddNote("Installation complete!");
    }

    private (bool exists, Capsule? capsule) HandleCapsuleAndBackup(
        PatcherModifications patch,
        string outputContainerPath)
    {
        Capsule? capsule = null;
        bool exists = false;

        if (IsCapsuleFile(patch.Destination))
        {
            bool capsuleExists = File.Exists(outputContainerPath);

            if (capsuleExists)
            {
                (string backupPath, HashSet<string> processedFiles) = GetBackup();
                CreateBackup(outputContainerPath, backupPath, processedFiles, Path.GetDirectoryName(patch.Destination) ?? "");
            }

            capsule = new Capsule(outputContainerPath, createIfNotExist: true);

            (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
            exists = capsule.Contains(resName, resType);
        }
        else
        {
            // It's a folder
            string fullPath = Path.Combine(outputContainerPath, patch.SaveAs);
            (string backupPath, HashSet<string> processedFiles) = GetBackup();

            // We backup the TARGET file if it exists
            if (File.Exists(fullPath))
            {
                CreateBackup(fullPath, backupPath, processedFiles, patch.Destination);
                exists = true;
            }
        }

        return (exists, capsule);
    }

    private void CreateBackup(string filePath, string backupRoot, HashSet<string> processedFiles, string relativeDir)
    {
        if (processedFiles.Contains(filePath))
        {
            return;
        }

        if (!File.Exists(filePath))
        {
            return;
        }

        string backupDestDir = Path.Combine(backupRoot, relativeDir);
        Directory.CreateDirectory(backupDestDir);
        string backupDestFile = Path.Combine(backupDestDir, Path.GetFileName(filePath));

        File.Copy(filePath, backupDestFile, true);
        processedFiles.Add(filePath);
        log.AddNote($"Backed up {Path.GetFileName(filePath)} to {relativeDir}");
    }

    public byte[]? LookupResource(
        PatcherModifications patch,
        string outputContainerPath,
        bool existsAtOutput,
        Capsule? capsule)
    {
        try
        {
            if (patch.ReplaceFile || !existsAtOutput)
            {
                // Load from mod path
                string sourcePath = Path.Combine(modPath, patch.SourceFolder, patch.SourceFile);
                if (File.Exists(sourcePath))
                {
                    return File.ReadAllBytes(sourcePath);
                }

                // Fallback to tslpatchdata if needed (if SourceFolder was ".")
                if (patch.SourceFolder == "." && TslPatchDataPath != null)
                {
                    string altPath = Path.Combine(TslPatchDataPath, patch.SourceFile);
                    if (File.Exists(altPath))
                    {
                        return File.ReadAllBytes(altPath);
                    }
                }

                // If still not found, check if it's in the mod directory directly (legacy behavior)
                string directPath = Path.Combine(modPath, patch.SourceFile);
                if (File.Exists(directPath))
                {
                    return File.ReadAllBytes(directPath);
                }

                return null;
            }

            if (capsule == null)
            {
                // Load from game folder
                string targetPath = Path.Combine(outputContainerPath, patch.SaveAs);
                if (File.Exists(targetPath))
                {
                    return File.ReadAllBytes(targetPath);
                }
                return null;
            }
            else
            {
                // Load from capsule
                (string resName, ResourceType resType) = ResourceIdentifier.FromPath(patch.SaveAs).Unpack();
                return capsule.GetResource(resName, resType);
            }
        }
        catch (Exception ex)
        {
            log.AddError($"Could not load source file: {ex.Message}");
            return null;
        }
    }

    public bool ShouldPatch(
        PatcherModifications patch,
        bool exists,
        Capsule? capsule = null)
    {
        string localFolder = patch.Destination == "." ? new DirectoryInfo(gamePath).Name : patch.Destination;
        string containerType = capsule == null ? "folder" : "archive";

        if (patch.ReplaceFile && exists)
        {
            string saveAsStr = patch.SaveAs != patch.SourceFile ? $"'{patch.SaveAs}' in" : "in";
            log.AddNote($"{patch.Action.TrimEnd()}ing '{patch.SourceFile}' and replacing existing file {saveAsStr} the '{localFolder}' {containerType}");
            return true;
        }

        if (!patch.SkipIfNotReplace && !patch.ReplaceFile && exists)
        {
            log.AddNote($"{patch.Action.TrimEnd()}ing existing file '{patch.SaveAs}' in the '{localFolder}' {containerType}");
            return true;
        }

        if (patch.SkipIfNotReplace && !patch.ReplaceFile && exists)
        {
            log.AddNote($"'{patch.SaveAs}' already exists in the '{localFolder}' {containerType}. Skipping file...");
            return false;
        }

        // If file doesn't exist in capsule and we're not replacing, we can't patch it
        if (capsule != null && !exists && !patch.ReplaceFile)
        {
            return false;
        }

        string saveType = (capsule != null && patch.SaveAs == patch.SourceFile) ? "adding" : "saving";
        string savingAsStr = patch.SaveAs != patch.SourceFile ? $"as '{patch.SaveAs}' in" : "to";
        log.AddNote($"{patch.Action.TrimEnd()}ing '{patch.SourceFile}' and {saveType} {savingAsStr} the '{localFolder}' {containerType}");
        return true;
    }

    private void HandleOverrideType(PatcherModifications patch)
    {
        string overrideType = patch.OverrideTypeValue.ToLowerInvariant();
        if (string.IsNullOrEmpty(overrideType) || overrideType == OverrideType.IGNORE)
        {
            return;
        }

        string overrideDir = Path.Combine(gamePath, "Override");
        string overrideResourcePath = Path.Combine(overrideDir, patch.SaveAs);

        if (!File.Exists(overrideResourcePath))
        {
            return;
        }

        if (overrideType == OverrideType.RENAME)
        {
            // Rename existing file
            string ext = Path.GetExtension(overrideResourcePath);
            string name = Path.GetFileNameWithoutExtension(overrideResourcePath);
            string directory = Path.GetDirectoryName(overrideResourcePath) ?? overrideDir;

            string newName = $"{name}_old{ext}";
            string newPath = Path.Combine(directory, newName);

            // Ensure unique name
            int counter = 1;
            while (File.Exists(newPath))
            {
                newName = $"{name}_old{counter}{ext}";
                newPath = Path.Combine(directory, newName);
                counter++;
            }

            try
            {
                File.Move(overrideResourcePath, newPath);
                log.AddNote($"Renamed existing Override file '{patch.SaveAs}' to '{newName}' to prevent shadowing.");
            }
            catch (Exception ex)
            {
                log.AddError($"Failed to rename existing Override file '{patch.SaveAs}': {ex.Message}");
            }
        }
        else if (overrideType == OverrideType.WARN)
        {
            log.AddWarning($"A resource located at '{overrideResourcePath}' is shadowing this mod's changes in {patch.Destination}!");
        }
    }

    private void HandleModRimShadow(PatcherModifications patch)
    {
        if (!patch.Destination.EndsWith(".rim", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string rimPath = Path.Combine(gamePath, patch.Destination);
        string modFilePath = Path.ChangeExtension(rimPath, ".mod");

        if (File.Exists(modFilePath))
        {
            log.AddWarning($"A .mod file exists at '{modFilePath}' which may shadow changes made to '{patch.Destination}'!");
        }
    }

    private static bool IsCapsuleFile(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext == ".mod" || ext == ".rim" || ext == ".erf" || ext == ".sav";
    }

    /// <summary>
    /// Gets TLK patches from the configuration.
    /// Returns main TLK patches and female dialog patches if applicable.
    /// </summary>
    private List<PatcherModifications> GetTlkPatches(PatcherConfig config)
    {
        List<PatcherModifications> tlkPatches = new();

        if (config.PatchesTLK.Modifiers.Count == 0)
        {
            return tlkPatches;
        }

        // Add main TLK patches
        tlkPatches.Add(config.PatchesTLK);

        // Check if female dialog file exists
        string femaleDialogFilename = "dialogf.tlk";
        string femaleDialogFilePath = Path.Combine(gamePath, femaleDialogFilename);

        if (File.Exists(femaleDialogFilePath))
        {
            // Create a deep copy of the TLK patches for female dialog
            var femaleTlkPatches = new Mods.TLK.ModificationsTLK(
                config.PatchesTLK.SourceFile,
                config.PatchesTLK.ReplaceFile);

            // Copy all modifiers
            foreach (Mods.TLK.ModifyTLK modifier in config.PatchesTLK.Modifiers)
            {
                femaleTlkPatches.Modifiers.Add(modifier);
            }

            // Copy other properties
            femaleTlkPatches.SourceFolder = config.PatchesTLK.SourceFolder;
            femaleTlkPatches.Destination = config.PatchesTLK.Destination;
            femaleTlkPatches.OverrideTypeValue = config.PatchesTLK.OverrideTypeValue;
            femaleTlkPatches.SkipIfNotReplace = config.PatchesTLK.SkipIfNotReplace;

            // Use female source file if it exists, otherwise use main source file
            string femaleSourceFile = config.PatchesTLK.SourcefileF;
            if (!string.IsNullOrEmpty(femaleSourceFile))
            {
                string femaleSourcePath = Path.Combine(modPath, config.PatchesTLK.SourceFolder, femaleSourceFile);
                if (File.Exists(femaleSourcePath))
                {
                    femaleTlkPatches.SourceFile = femaleSourceFile;
                }
            }

            // Set save as to female dialog filename
            femaleTlkPatches.SaveAs = femaleDialogFilename;

            tlkPatches.Add(femaleTlkPatches);
        }

        return tlkPatches;
    }
}
