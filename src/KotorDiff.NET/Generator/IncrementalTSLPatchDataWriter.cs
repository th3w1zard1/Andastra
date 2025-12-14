// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1214-4389
// Original: class IncrementalTSLPatchDataWriter: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.SSF;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Mods.GFF;
using CSharpKOTOR.Mods.NCS;
using CSharpKOTOR.Mods.SSF;
using CSharpKOTOR.Mods.TLK;
using CSharpKOTOR.Mods.TwoDA;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Tools;
using CSharpKOTOR.Utility;
using SystemTextEncoding = System.Text.Encoding;
using JetBrains.Annotations;
using KotorDiff.NET.Diff;
using KotorDiff.NET.Logger;

namespace KotorDiff.NET.Generator
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1166-1212
    // Original: @dataclass class TwoDALinkTarget, PendingStrRefReference, Pending2DARowReference
    /// <summary>
    /// Link target for 2DA memory tokens.
    /// </summary>
    public class TwoDALinkTarget
    {
        public int RowIndex { get; set; }
        public int TokenId { get; set; }
        [CanBeNull] public string RowLabel { get; set; }
    }

    /// <summary>
    /// Temporarily stored StrRef reference that will be applied when the file is diffed.
    /// </summary>
    public class PendingStrRefReference
    {
        public string Filename { get; set; }
        public object SourcePath { get; set; } // Installation or Path
        public int OldStrref { get; set; }
        public int TokenId { get; set; }
        public string LocationType { get; set; } // "2da", "ssf", "gff", "ncs"
        public Dictionary<string, object> LocationData { get; set; }
    }

    /// <summary>
    /// Temporarily stored 2DA row reference that will be applied when the GFF file is diffed.
    /// </summary>
    public class Pending2DARowReference
    {
        public string GffFilename { get; set; }
        public object SourcePath { get; set; } // Installation or Path
        public string TwodaFilename { get; set; }
        public int RowIndex { get; set; }
        public int TokenId { get; set; }
        public List<string> FieldPaths { get; set; }
    }

    /// <summary>
    /// Wrapper for TLK modification with its source path.
    /// </summary>
    public class TLKModificationWithSource
    {
        public ModificationsTLK Modification { get; set; }
        public object SourcePath { get; set; } // Installation or Path
        public int SourceIndex { get; set; }
        public bool IsInstallation { get; set; }
    }

    /// <summary>
    /// Writes tslpatchdata files and INI sections incrementally during diff.
    /// 1:1 port of IncrementalTSLPatchDataWriter from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1214-4389
    /// </summary>
    public class IncrementalTSLPatchDataWriter
    {
        private readonly string _tslpatchdataPath;
        private readonly string _iniPath;
        [CanBeNull] private readonly string _baseDataPath;
        [CanBeNull] private readonly string _moddedDataPath;
        [CanBeNull] private readonly Action<string> _logFunc;

        // Track what we've written
        private readonly HashSet<string> _writtenSections = new HashSet<string>();
        private readonly Dictionary<string, List<string>> _installFolders = new Dictionary<string, List<string>>();

        // Track modifications for final InstallList generation
        public ModificationsByType AllModifications { get; }

        // Track insertion positions for each section (for real-time appending)
        private readonly Dictionary<string, string> _sectionMarkers = new Dictionary<string, string>
        {
            { "tlk", "[TLKList]" },
            { "install", "[InstallList]" },
            { "2da", "[2DAList]" },
            { "gff", "[GFFList]" },
            { "ncs", "[CompileList]" },
            { "ssf", "[SSFList]" }
        };

        // Track folder numbers for InstallList
        private readonly Dictionary<string, int> _folderNumbers = new Dictionary<string, int>();
        private int _nextFolderNumber = 0;

        // Performance optimization: batch INI writes to reduce overhead
        private readonly HashSet<string> _pendingIniWrites = new HashSet<string>();
        private readonly bool _batchWrites = true;
        private int _writesSinceLastFlush = 0;
        private const int WriteBatchSize = 50;

        // Track global 2DAMEMORY token allocation
        private int _next2DaTokenId = 0;

        // StrRef and 2DA memory reference caches for linking patches
        [CanBeNull] private readonly StrRefReferenceCache _strrefCache;
        [CanBeNull] private readonly Dictionary<int, CaseInsensitiveDict<TwoDAMemoryReferenceCache>> _twodaCaches;

        // Track TLK modifications with their source paths for intelligent cache building
        // Key: source_index (0=first/vanilla, 1=second/modded, 2=third, etc.)
        // Value: list of TLKModificationWithSource objects from that source
        private readonly Dictionary<int, List<TLKModificationWithSource>> _tlkModsBySource = new Dictionary<int, List<TLKModificationWithSource>>();

        // Track pending StrRef references that will be applied when files are diffed
        // Key: filename (lowercase) -> list of PendingStrRefReference
        private readonly Dictionary<string, List<PendingStrRefReference>> _pendingStrrefReferences = new Dictionary<string, List<PendingStrRefReference>>();

        // Track pending 2DA row references that will be applied when GFF files are diffed
        // Key: gff_filename (lowercase) -> list of Pending2DARowReference
        private readonly Dictionary<string, List<Pending2DARowReference>> _pending2DaRowReferences = new Dictionary<string, List<Pending2DARowReference>>();

        /// <summary>
        /// Initialize incremental writer.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1217-1317
        /// </summary>
        public IncrementalTSLPatchDataWriter(
            string tslpatchdataPath,
            string iniFilename,
            [CanBeNull] string baseDataPath = null,
            [CanBeNull] string moddedDataPath = null,
            [CanBeNull] StrRefReferenceCache strrefCache = null,
            [CanBeNull] Dictionary<int, CaseInsensitiveDict<TwoDAMemoryReferenceCache>> twodaCaches = null,
            [CanBeNull] Action<string> logFunc = null)
        {
            _tslpatchdataPath = tslpatchdataPath;
            _iniPath = Path.Combine(tslpatchdataPath, iniFilename);
            _baseDataPath = baseDataPath;
            _moddedDataPath = moddedDataPath;
            _strrefCache = strrefCache;
            _twodaCaches = twodaCaches ?? new Dictionary<int, CaseInsensitiveDict<TwoDAMemoryReferenceCache>>();
            _logFunc = logFunc ?? ((msg) => DiffLogger.GetLogger()?.Info(msg));

            // Create tslpatchdata directory
            Directory.CreateDirectory(_tslpatchdataPath);

            // Track modifications for final InstallList generation
            AllModifications = ModificationsByType.CreateEmpty();

            // Initialize INI file with all headers
            InitializeIni();
        }

        /// <summary>
        /// Initialize INI file with header, [Settings], and all List section headers.
        /// </summary>
        private void InitializeIni()
        {
            var headerLines = GenerateCustomHeader();
            var settingsLines = GenerateSettings();

            // Write all section headers in TSLPatcher-compliant order
            var sectionHeaders = new List<string>
            {
                "",
                "[TLKList]",
                "",
                "",
                "[InstallList]",
                "",
                "",
                "[2DAList]",
                "",
                "",
                "[GFFList]",
                "",
                "",
                "[CompileList]",
                "",
                "",
                "[SSFList]",
                ""
            };

            var content = string.Join("\n", headerLines.Concat(new[] { "" }).Concat(settingsLines).Concat(sectionHeaders));
            File.WriteAllText(_iniPath, content, SystemTextEncoding.UTF8);
        }

        /// <summary>
        /// Generate custom INI file header with HoloPatcher.NET branding.
        /// </summary>
        private List<string> GenerateCustomHeader()
        {
            string today = DateTime.UtcNow.ToString("MM/dd/yyyy");
            return new List<string>
            {
                "; ============================================================================",
                $";  TSLPatcher Modifications File — Generated by HoloPatcher.NET ({today})",
                "; ============================================================================",
                ";",
                ";  This file is part of the HoloPatcher.NET ecosystem",
                ";",
                ";  FORMATTING NOTES:",
                ";    • This file is TSLPatcher-compliant and machine-generated.",
                ";    • You may add blank lines between sections (for readability).",
                ";    • You may add comment lines starting with semicolon.",
                ";    • Do NOT add blank lines or comments inside a section (between keys).",
                "; ============================================================================",
                ""
            };
        }

        /// <summary>
        /// Generate default [Settings] section with all required TSLPatcher keys.
        /// </summary>
        private List<string> GenerateSettings()
        {
            return new List<string>
            {
                "[Settings]",
                "FileExists=1",
                "WindowCaption=Mod Installer",
                "ConfirmMessage=Install this mod?",
                "LogLevel=3",
                "InstallerMode=1",
                "BackupFiles=1",
                "PlaintextLog=0",
                "LookupGameFolder=0",
                "LookupGameNumber=1",
                "SaveProcessedScripts=0",
                ""
            };
        }

        /// <summary>
        /// Write a modification's resource file and INI section immediately.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1471-1506
        /// </summary>
        public void WriteModification(PatcherModifications modification, byte[] sourceData = null, object sourcePath = null, object moddedSourcePath = null)
        {
            // Check for and apply pending StrRef references before writing
            string filenameLower = modification.SourceFile.ToLowerInvariant();
            ApplyPendingStrrefReferences(filenameLower, modification, sourceData, sourcePath);

            // Check for and apply pending 2DA row references before writing (for GFF files)
            if (modification is ModificationsGFF)
            {
                ApplyPending2DaRowReferences(filenameLower, modification, sourceData, sourcePath);
            }

            // Determine modification type and dispatch
            if (modification is Modifications2DA mod2da)
            {
                Write2DaModification(mod2da, sourceData, sourcePath, moddedSourcePath);
            }
            else if (modification is ModificationsGFF modGff)
            {
                WriteGffModification(modGff, sourceData);
            }
            else if (modification is ModificationsTLK modTlk)
            {
                WriteTlkModification(modTlk);
            }
            else if (modification is ModificationsSSF modSsf)
            {
                WriteSsfModification(modSsf, sourceData);
            }
            else if (modification is ModificationsNCS modNcs)
            {
                WriteNcsModification(modNcs, sourceData);
            }
            else
            {
                _logFunc?.Invoke($"[Warning] Unknown modification type: {modification.GetType().Name}");
            }
        }

        /// <summary>
        /// Register a TLK modification with its source path for cache building.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1318-1344
        /// </summary>
        public void RegisterTlkModificationWithSource(ModificationsTLK tlkMod, object sourcePath, int sourceIndex)
        {
            bool isInstallation = sourcePath is Installation;

            var wrapped = new TLKModificationWithSource
            {
                Modification = tlkMod,
                SourcePath = sourcePath,
                SourceIndex = sourceIndex,
                IsInstallation = isInstallation
            };

            if (!_tlkModsBySource.ContainsKey(sourceIndex))
            {
                _tlkModsBySource[sourceIndex] = new List<TLKModificationWithSource>();
            }

            _tlkModsBySource[sourceIndex].Add(wrapped);
            _logFunc?.Invoke($"[DEBUG] Registered TLK mod from source {sourceIndex}: {tlkMod.SourceFile}");
        }

        /// <summary>
        /// Write 2DA resource file and INI section.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1507-1587
        /// </summary>
        private void Write2DaModification(Modifications2DA mod2da, byte[] sourceData = null, object sourcePath = null, object moddedSourcePath = null)
        {
            string filename = mod2da.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla 2DA that will be patched)
            if (sourceData != null && sourceData.Length > 0)
            {
                string destPath = Path.Combine(_tslpatchdataPath, filename);
                File.WriteAllBytes(destPath, sourceData);
            }

            // Add to install folders
            AddToInstallFolder("Override", filename);

            // Write INI section
            WriteToIni(new List<Modifications2DA> { mod2da }, "2da");
            _writtenSections.Add(filename);

            // Track in all_modifications (only if not already added)
            if (!AllModifications.Twoda.Contains(mod2da))
            {
                AllModifications.Twoda.Add(mod2da);
            }
        }

        /// <summary>
        /// Write GFF resource file and INI section.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1959-2043
        /// </summary>
        private void WriteGffModification(ModificationsGFF modGff, byte[] sourceData = null)
        {
            string filename = modGff.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla GFF that will be patched)
            if (sourceData != null && sourceData.Length > 0)
            {
                string destPath = Path.Combine(_tslpatchdataPath, filename);
                File.WriteAllBytes(destPath, sourceData);
            }

            string destination = modGff.Destination ?? "Override";
            AddToInstallFolder(destination, filename);

            // Write INI section
            WriteToIni(new List<ModificationsGFF> { modGff }, "gff");
            _writtenSections.Add(filename);

            // Track in all_modifications (only if not already added)
            if (!AllModifications.Gff.Contains(modGff))
            {
                AllModifications.Gff.Add(modGff);
            }
        }

        /// <summary>
        /// Write TLK modification and create linking patches.
        /// </summary>
        private void WriteTlkModification(ModificationsTLK modTlk)
        {
            string filename = modTlk.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Generate append.tlk file
            var appends = modTlk.Modifiers.Where(m => !m.IsReplacement).ToList();

            if (appends.Count > 0)
            {
                var appendTlk = new TLK();
                appendTlk.Resize(appends.Count);

                var sortedAppends = appends.OrderBy(m => m.TokenId).ToList();

                for (int appendIdx = 0; appendIdx < sortedAppends.Count; appendIdx++)
                {
                    var modifier = sortedAppends[appendIdx];
                    string text = modifier.Text ?? "";
                    string soundStr = modifier.Sound?.ToString() ?? "";
                    appendTlk.Replace(appendIdx, text, soundStr);
                }

                string appendPath = Path.Combine(_tslpatchdataPath, "append.tlk");
                var writer = new TLKBinaryWriter(appendTlk);
                byte[] tlkData = writer.Write();
                File.WriteAllBytes(appendPath, tlkData);
            }

            // Add to install folders
            AddToInstallFolder(".", "append.tlk");

            // Write INI section
            WriteToIni(new List<ModificationsTLK> { modTlk }, "tlk");
            _writtenSections.Add(filename);

            // Track in all_modifications (only if not already added)
            if (!AllModifications.Tlk.Contains(modTlk))
            {
                AllModifications.Tlk.Add(modTlk);
            }
        }


        /// <summary>
        /// Write SSF modification.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3839-3876
        /// </summary>
        private void WriteSsfModification(ModificationsSSF modSsf, byte[] sourceData = null)
        {
            string filename = modSsf.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla SSF that will be patched)
            if (sourceData != null && sourceData.Length > 0)
            {
                string destPath = Path.Combine(_tslpatchdataPath, filename);
                File.WriteAllBytes(destPath, sourceData);
            }

            string destination = modSsf.Destination ?? "Override";
            AddToInstallFolder(destination, filename);

            // Write INI section
            WriteToIni(new List<ModificationsSSF> { modSsf }, "ssf");
            _writtenSections.Add(filename);

            // Track in all_modifications (only if not already added)
            if (!AllModifications.Ssf.Contains(modSsf))
            {
                AllModifications.Ssf.Add(modSsf);
            }
        }

        /// <summary>
        /// Write NCS modification.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3877-3904
        /// </summary>
        private void WriteNcsModification(ModificationsNCS modNcs, byte[] sourceData = null)
        {
            string filename = modNcs.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla NCS that will be patched)
            if (sourceData != null && sourceData.Length > 0)
            {
                string destPath = Path.Combine(_tslpatchdataPath, filename);
                File.WriteAllBytes(destPath, sourceData);
            }

            // Add to install folders
            AddToInstallFolder("Override", filename);

            // Write INI section
            WriteToIni(new List<ModificationsNCS> { modNcs }, "ncs");
            _writtenSections.Add(filename);

            // Track in all_modifications (only if not already added)
            if (!AllModifications.Ncs.Contains(modNcs))
            {
                AllModifications.Ncs.Add(modNcs);
            }
        }

        /// <summary>
        /// Check and apply pending StrRef references for a file being diffed.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3053-3149
        /// </summary>
        private void ApplyPendingStrrefReferences(string filename, PatcherModifications modification, byte[] sourceData, object sourcePath)
        {
            if (!_pendingStrrefReferences.ContainsKey(filename))
            {
                return;
            }

            var pendingRefs = _pendingStrrefReferences[filename];
            if (pendingRefs == null || pendingRefs.Count == 0)
            {
                return;
            }

            var appliedRefs = new List<PendingStrRefReference>();
            foreach (var pendingRef in pendingRefs)
            {
                // Only apply references if they come from the same path
                bool shouldApply = false;

                if (sourcePath == null)
                {
                    continue;
                }

                // Check if paths match exactly
                if (pendingRef.SourcePath is Installation pendingInstall && sourcePath is Installation sourceInstall)
                {
                    shouldApply = pendingInstall.Path == sourceInstall.Path;
                }
                else if (pendingRef.SourcePath is string pendingPath && sourcePath is string sourcePathStr)
                {
                    shouldApply = pendingPath == sourcePathStr;
                }

                // Also verify the StrRef still exists at the expected location in the source data
                if (shouldApply && sourceData != null)
                {
                    shouldApply = VerifyStrrefLocation(sourceData, pendingRef);
                }

                if (!shouldApply)
                {
                    continue;
                }

                // Apply the reference to the modification object being written
                // TODO: Implement immediate patch creation methods
                appliedRefs.Add(pendingRef);
            }

            // Remove applied references
            foreach (var appliedRef in appliedRefs)
            {
                pendingRefs.Remove(appliedRef);
            }
            if (pendingRefs.Count == 0)
            {
                _pendingStrrefReferences.Remove(filename);
            }
        }

        /// <summary>
        /// Check and apply pending 2DA row references for a GFF file being diffed.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3294-3362
        /// </summary>
        private void ApplyPending2DaRowReferences(string filename, PatcherModifications modification, byte[] sourceData, object sourcePath)
        {
            if (!_pending2DaRowReferences.ContainsKey(filename))
            {
                return;
            }

            var pendingRefs = _pending2DaRowReferences[filename];
            if (pendingRefs == null || pendingRefs.Count == 0)
            {
                return;
            }

            var appliedRefs = new List<Pending2DARowReference>();
            foreach (var pendingRef in pendingRefs)
            {
                // Only apply references if they come from the same path
                bool shouldApply = false;

                if (sourcePath == null)
                {
                    continue;
                }

                // Check if paths match exactly
                if (pendingRef.SourcePath is Installation pendingInstall && sourcePath is Installation sourceInstall)
                {
                    shouldApply = pendingInstall.Path == sourceInstall.Path;
                }
                else if (pendingRef.SourcePath is string pendingPath && sourcePath is string sourcePathStr)
                {
                    shouldApply = pendingPath == sourcePathStr;
                }

                // Also verify the 2DA row still exists at the expected location in the source data
                if (shouldApply && sourceData != null)
                {
                    shouldApply = Verify2DaRowLocation(sourceData, pendingRef);
                }

                if (!shouldApply)
                {
                    continue;
                }

                // Apply the reference to the modification object being written
                // TODO: Implement immediate patch creation methods
                appliedRefs.Add(pendingRef);
            }

            // Remove applied references
            foreach (var appliedRef in appliedRefs)
            {
                pendingRefs.Remove(appliedRef);
            }
            if (pendingRefs.Count == 0)
            {
                _pending2DaRowReferences.Remove(filename);
            }
        }

        /// <summary>
        /// Verify that a StrRef still exists at the expected location in source data.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3151-3201
        /// </summary>
        private bool VerifyStrrefLocation(byte[] sourceData, PendingStrRefReference pendingRef)
        {
            // TODO: Implement StrRef location verification
            return true; // Placeholder - always return true for now
        }

        /// <summary>
        /// Verify that a 2DA row still exists at the expected location in source data.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3363-3392
        /// </summary>
        private bool Verify2DaRowLocation(byte[] sourceData, Pending2DARowReference pendingRef)
        {
            // TODO: Implement 2DA row location verification
            return true; // Placeholder - always return true for now
        }

        /// <summary>
        /// Add a file to InstallList and copy it to tslpatchdata.
        /// </summary>
        public void AddInstallFile(string folder, string filename, [CanBeNull] string sourcePath = null)
        {
            // Add to tracking
            AddToInstallFolder(folder, filename);

            // Copy file if source provided
            if (sourcePath != null && File.Exists(sourcePath))
            {
                // CRITICAL: ALL files go directly in tslpatchdata root, NOT in subdirectories
                // The folder parameter is only used in the INI to tell TSLPatcher where to install
                string destPath = Path.Combine(_tslpatchdataPath, filename);

                try
                {
                    // Extract file data (may be from capsule or loose file)
                    byte[] sourceData = ExtractFileData(sourcePath, filename);

                    if (sourceData != null && sourceData.Length > 0)
                    {
                        // Use appropriate io function based on extension
                        string fileExt = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();
                        WriteResourceWithIo(sourceData, destPath, fileExt);
                    }
                    else
                    {
                        _logFunc?.Invoke($"[Warning] Could not extract data for install file: {filename}");
                    }
                }
                catch (Exception e)
                {
                    _logFunc?.Invoke($"[Error] Failed to copy install file {filename}: {e.GetType().Name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Extract file data from source (handles both loose files and capsules).
        /// </summary>
        private byte[] ExtractFileData(string sourcePath, string filename)
        {
            if (File.Exists(sourcePath))
            {
                // If the filename itself is the capsule, copy the entire file verbatim
                if (filename.Equals(Path.GetFileName(sourcePath), StringComparison.OrdinalIgnoreCase))
                {
                    return File.ReadAllBytes(sourcePath);
                }

                // If it's a loose file, just read it
                if (!DiffEngineUtils.IsCapsuleFile(Path.GetFileName(sourcePath)))
                {
                    return File.ReadAllBytes(sourcePath);
                }

                // Otherwise extract the resource from the capsule
                try
                {
                    var capsule = new Capsule(sourcePath);
                    string resref = Path.GetFileNameWithoutExtension(filename);
                    string resExt = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant();

                    foreach (var res in capsule)
                    {
                        if (res.ResName.Equals(resref, StringComparison.OrdinalIgnoreCase) &&
                            res.ResType.Extension.ToLowerInvariant() == resExt)
                        {
                            return res.Data;
                        }
                    }
                }
                catch (Exception e)
                {
                    _logFunc?.Invoke($"[Error] Failed to extract from capsule {sourcePath}: {e.GetType().Name}: {e.Message}");
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Write resource using appropriate io function.
        /// </summary>
        private void WriteResourceWithIo(byte[] data, string destPath, string fileExt)
        {
            try
            {
                // TODO: Implement format-specific writers
                // For now, just write binary
                File.WriteAllBytes(destPath, data);
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"[Warning] Failed to use io function for {fileExt}, using binary write: {e.GetType().Name}: {e.Message}");
                File.WriteAllBytes(destPath, data);
            }
        }

        /// <summary>
        /// Add a file to the install folder tracking.
        /// </summary>
        private void AddToInstallFolder(string folder, string filename)
        {
            if (folder == ".") folder = "Override"; // TSLPatcher treats "." as Override for InstallList

            if (!_installFolders.ContainsKey(folder))
            {
                _installFolders[folder] = new List<string>();
            }

            if (!_installFolders[folder].Contains(filename, StringComparer.OrdinalIgnoreCase))
            {
                _installFolders[folder].Add(filename);
            }
        }

        /// <summary>
        /// Write modifications to INI file.
        /// </summary>
        private void WriteToIni<T>(List<T> modifications, string modType) where T : PatcherModifications
        {
            // Track that this section needs to be written
            _pendingIniWrites.Add(modType);
            _writesSinceLastFlush++;

            // Flush if we've accumulated enough writes or if batching is disabled
            if (!_batchWrites || _writesSinceLastFlush >= WriteBatchSize)
            {
                FlushPendingWrites();
            }
        }

        /// <summary>
        /// Flush all pending INI writes by rewriting the complete file.
        /// </summary>
        private void FlushPendingWrites()
        {
            if (_pendingIniWrites.Count > 0)
            {
                RewriteIniComplete();
                _pendingIniWrites.Clear();
                _writesSinceLastFlush = 0;
            }
        }

        /// <summary>
        /// Completely rewrite the INI file from all accumulated modifications.
        /// </summary>
        private void RewriteIniComplete()
        {
            var serializer = new TSLPatcherINISerializer();

            // Build InstallFile list from install_folders tracking
            var installFiles = new List<InstallFile>();
            foreach (var kvp in _installFolders)
            {
                string folder = kvp.Key;
                foreach (string filename in kvp.Value)
                {
                    installFiles.Add(new InstallFile(filename, destination: folder));
                }
            }

            // Create a ModificationsByType with all accumulated modifications
            var modificationsByType = new ModificationsByType
            {
                Tlk = AllModifications.Tlk,
                Install = installFiles,
                Twoda = AllModifications.Twoda,
                Gff = AllModifications.Gff,
                Ssf = AllModifications.Ssf,
                Ncs = AllModifications.Ncs,
                Nss = AllModifications.Nss
            };

            // Generate complete INI content (includes header and settings)
            // Use verbose=false to avoid duplicate logging during incremental writes
            string iniContent = serializer.Serialize(
                modificationsByType,
                includeHeader: true,
                includeSettings: true,
                verbose: false
            );

            // Write the entire file from scratch
            File.WriteAllText(_iniPath, iniContent, SystemTextEncoding.UTF8);
        }

        /// <summary>
        /// Finalize the INI file.
        /// All sections are already written incrementally in real-time.
        /// This method just logs a summary and flushes any pending writes.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:4354-4371
        /// </summary>
        public void FinalizeWriter()
        {
            // Flush any remaining pending writes
            FlushPendingWrites();

            _logFunc?.Invoke($"\nINI finalized at: {_iniPath}");
            _logFunc?.Invoke($"  TLK modifications: {AllModifications.Tlk.Count}");
            _logFunc?.Invoke($"  2DA modifications: {AllModifications.Twoda.Count}");
            _logFunc?.Invoke($"  GFF modifications: {AllModifications.Gff.Count}");
            _logFunc?.Invoke($"  SSF modifications: {AllModifications.Ssf.Count}");
            _logFunc?.Invoke($"  NCS modifications: {AllModifications.Ncs.Count}");
            int totalInstallFiles = _installFolders.Values.Sum(files => files.Count);
            _logFunc?.Invoke($"  Install files: {totalInstallFiles}");
            _logFunc?.Invoke($"  Install folders: {_installFolders.Count}");
        }

        /// <summary>
        /// Write pending TLK modifications to INI.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py
        /// </summary>
        public void WritePendingTlkModifications()
        {
            // Flush any pending TLK writes
            if (_pendingIniWrites.Contains("tlk"))
            {
                FlushPendingWrites();
            }
        }

        // Expose install folders for summary
        public Dictionary<string, List<string>> InstallFolders => _installFolders;
    }
}


