// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1214-4389
// Original: class IncrementalTSLPatchDataWriter: ...
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LIP;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Mods.NCS;
using Andastra.Parsing.Mods.SSF;
using Andastra.Parsing.Mods.TLK;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Resource;
// Removed: using Tools; // Removed to break circular dependency
// Using fully qualified names directly in code instead
using Andastra.Utility;
using JetBrains.Annotations;
using InstallationClass = Andastra.Parsing.Installation.Installation;
using SystemTextEncoding = System.Text.Encoding;
namespace TSLPatcher
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

        /// <summary>
        /// Gets the path to the tslpatchdata directory.
        /// </summary>
        public string TslpatchdataPath => _tslpatchdataPath;

        // Track insertion positions for each section (for real-time appending)
        private readonly Dictionary<string, string> _sectionMarkers = new Dictionary<string, string>
        {
            { "2da", "[2DAList]" },
            { "gff", "[GFFList]" },
            { "install", "[InstallList]" },
            { "ncs", "[CompileList]" },
            { "ssf", "[SSFList]" },
            { "tlk", "[TLKList]" },
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
        private int _next2DATokenId = 0;

        // StrRef and 2DA memory reference caches for linking patches
        [CanBeNull] private readonly Tools.StrRefReferenceCache _strrefCache;
        // TODO: STUB - Ensure inner dictionaries use StringComparer.OrdinalIgnoreCase for case-insensitive filename lookups
        // When creating inner Dictionary<string, Tools.TwoDAMemoryReferenceCache> instances, use:
        // new Dictionary<string, Tools.TwoDAMemoryReferenceCache>(StringComparer.OrdinalIgnoreCase)
        [CanBeNull] private readonly Dictionary<int, Dictionary<string, Andastra.Parsing.Tools.TwoDAMemoryReferenceCache>> _twodaCaches;

        // Helper method to create case-insensitive inner dictionaries
        private static Dictionary<string, Andastra.Parsing.Tools.TwoDAMemoryReferenceCache> CreateCaseInsensitiveTwoDACache()
        {
            return new Dictionary<string, Tools.TwoDAMemoryReferenceCache>(StringComparer.OrdinalIgnoreCase);
        }
        // Track TLK modifications with their source paths for intelligent cache building
        // Key: source_index (0=first/vanilla, 1=second/modded, 2=third, etc.)
        // Value: list of TLKModificationWithSource objects from that source
        private readonly Dictionary<int, List<TLKModificationWithSource>> _tlkModsBySource = new Dictionary<int, List<TLKModificationWithSource>>();

        // Track pending StrRef references that will be applied when files are diffed
        // Key: filename (lowercase) -> list of PendingStrRefReference
        private readonly Dictionary<string, List<PendingStrRefReference>> _pendingStrrefReferences = new Dictionary<string, List<PendingStrRefReference>>();

        // Track pending 2DA row references that will be applied when GFF files are diffed
        // Key: gff_filename (lowercase) -> list of Pending2DARowReference
        private readonly Dictionary<string, List<Pending2DARowReference>> _pending2DARowReferences = new Dictionary<string, List<Pending2DARowReference>>();

        // Metadata storage for TLK modifications (strref_mappings, source_installations)
        // Key: ModificationsTLK object ID, Value: dict with strref_mappings and source_installations
        private readonly Dictionary<int, Dictionary<string, object>> _tlkMetadata = new Dictionary<int, Dictionary<string, object>>();

        /// <summary>
        /// Initialize incremental writer.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1217-1317
        /// </summary>
        public IncrementalTSLPatchDataWriter(
            string tslpatchdataPath,
            string iniFilename,
            [CanBeNull] string baseDataPath = null,
            [CanBeNull] string moddedDataPath = null,
            [CanBeNull] Andastra.Parsing.Tools.StrRefReferenceCache strrefCache = null,
            [CanBeNull] Dictionary<int, Dictionary<string, Andastra.Parsing.Tools.TwoDAMemoryReferenceCache>> twodaCaches = null,
            [CanBeNull] Action<string> logFunc = null)
        {
            _tslpatchdataPath = tslpatchdataPath;
            _iniPath = Path.Combine(tslpatchdataPath, iniFilename);
            _baseDataPath = baseDataPath;
            _moddedDataPath = moddedDataPath;
            _strrefCache = strrefCache;
            _twodaCaches = twodaCaches ?? new Dictionary<int, Dictionary<string, Andastra.Parsing.Tools.TwoDAMemoryReferenceCache>>();
            _logFunc = logFunc ?? Console.WriteLine;

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
                ApplyPending2DARowReferences(filenameLower, modification, sourceData, sourcePath);
            }

            // Determine modification type and dispatch
            if (modification is Modifications2DA mod2da)
            {
                Write2DAModification(mod2da, sourceData, sourcePath, moddedSourcePath);
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
            bool isInstallation = sourcePath is InstallationClass;

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
        /// Set metadata for a TLK modification (strref_mappings, source_installations, etc.).
        /// </summary>
        public void SetTlkMetadata(ModificationsTLK tlkMod, string key, object value)
        {
            int tlkId = tlkMod.GetHashCode();
            if (!_tlkMetadata.ContainsKey(tlkId))
            {
                _tlkMetadata[tlkId] = new Dictionary<string, object>();
            }
            _tlkMetadata[tlkId][key] = value;
        }

        /// <summary>
        /// Get metadata for a TLK modification.
        /// </summary>
        [CanBeNull]
        public Dictionary<string, object> GetTlkMetadata(ModificationsTLK tlkMod)
        {
            int tlkId = tlkMod.GetHashCode();
            return _tlkMetadata.TryGetValue(tlkId, out var metadata) ? metadata : null;
        }

        /// <summary>
        /// Write 2DA resource file and INI section.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1507-1587
        /// </summary>
        private void Write2DAModification(Modifications2DA mod2da, byte[] sourceData = null, object sourcePath = null, object moddedSourcePath = null)
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
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:2075-2200
        /// </summary>
        private void WriteTlkModification(ModificationsTLK modTlk)
        {
            string filename = modTlk.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Find StrRef references and create linking patches using BATCH PROCESSING
            // Get strref_mappings and source installations from the metadata dict
            var tlkMetadata = GetTlkMetadata(modTlk);
            Dictionary<int, int> strrefMappings = new Dictionary<int, int>();
            List<object> sourceInstallations = new List<object>();

            if (tlkMetadata != null)
            {
                if (tlkMetadata.TryGetValue("strref_mappings", out var mappingsObj) && mappingsObj is Dictionary<int, int> mappings)
                {
                    strrefMappings = mappings;
                }
                if (tlkMetadata.TryGetValue("source_installations", out var sourcesObj) && sourcesObj is List<object> sources)
                {
                    sourceInstallations = sources;
                }
            }

            if (strrefMappings.Count > 0)
            {
                // If no installations from metadata, try fallback
                if (sourceInstallations.Count == 0 && !string.IsNullOrEmpty(_baseDataPath))
                {
                    sourceInstallations.Add(_baseDataPath);
                }

                // For diff entries, search BOTH installations for references
                // References to the old StrRef might exist in either installation
                if (sourceInstallations.Count > 0)
                {
                    _logFunc?.Invoke($"\n=== Creating Linking Patches from Cache ({strrefMappings.Count} StrRefs) ===");
                    CreateStrRefLinkingPatches(modTlk, strrefMappings, sourceInstallations);
                }
            }
            else
            {
                // Write INI section
                WriteToIni(new List<ModificationsTLK> { modTlk }, "tlk");
                _writtenSections.Add(filename);

                // Track in all_modifications (only if not already added)
                if (!AllModifications.Tlk.Contains(modTlk))
                {
                    AllModifications.Tlk.Add(modTlk);
                }
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
                if (pendingRef.SourcePath is InstallationClass pendingInstall && sourcePath is InstallationClass sourceInstall)
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
                if (pendingRef.LocationType == "2da")
                {
                    CreateImmediate2DAStrrefPatchSingle(
                        filename,
                        pendingRef.OldStrref,
                        pendingRef.TokenId,
                        (int)pendingRef.LocationData["row_index"],
                        (string)pendingRef.LocationData["column_name"],
                        pendingRef.LocationData.ContainsKey("resource_path") ? (string)pendingRef.LocationData["resource_path"] : null,
                        modification);
                }
                else if (pendingRef.LocationType == "ssf")
                {
                    CreateImmediateSsfStrrefPatchSingle(
                        filename,
                        pendingRef.OldStrref,
                        pendingRef.TokenId,
                        (SSFSound)pendingRef.LocationData["sound"],
                        modification);
                }
                else if (pendingRef.LocationType == "gff")
                {
                    CreateImmediateGffStrrefPatchSingle(
                        filename,
                        pendingRef.OldStrref,
                        pendingRef.TokenId,
                        (string)pendingRef.LocationData["field_path"],
                        modification);
                }
                // NCS reference finding is temporarily disabled in Python too
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
        private void ApplyPending2DARowReferences(string filename, PatcherModifications modification, byte[] sourceData, object sourcePath)
        {
            if (!_pending2DARowReferences.ContainsKey(filename))
            {
                return;
            }

            var pendingRefs = _pending2DARowReferences[filename];
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
                if (pendingRef.SourcePath is InstallationClass pendingInstall && sourcePath is InstallationClass sourceInstall)
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
                    shouldApply = Verify2DARowLocation(sourceData, pendingRef);
                }

                if (!shouldApply)
                {
                    continue;
                }

                // Apply the reference to the modification object being written
                CreateGff2DAPatch(
                    filename,
                    pendingRef.FieldPaths,
                    pendingRef.TokenId,
                    modification);
                appliedRefs.Add(pendingRef);
            }

            // Remove applied references
            foreach (var appliedRef in appliedRefs)
            {
                pendingRefs.Remove(appliedRef);
            }
            if (pendingRefs.Count == 0)
            {
                _pending2DARowReferences.Remove(filename);
            }
        }

        /// <summary>
        /// Verify that a StrRef still exists at the expected location in source data.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3151-3201
        /// </summary>
        private bool VerifyStrrefLocation(byte[] sourceData, PendingStrRefReference pendingRef)
        {
            try
            {
                if (pendingRef.LocationType == "2da")
                {
                    var reader = new TwoDABinaryReader(sourceData);
                    TwoDA twoda = reader.Load();
                    int rowIndex = (int)pendingRef.LocationData["row_index"];
                    string columnName = (string)pendingRef.LocationData["column_name"];
                    TwoDARow row = twoda.GetRow(rowIndex);
                    string cellValue = row.GetString(columnName);
                    if (!string.IsNullOrWhiteSpace(cellValue) && int.TryParse(cellValue.Trim(), out int cellStrref))
                    {
                        return cellStrref == pendingRef.OldStrref;
                    }
                    return false;
                }

                if (pendingRef.LocationType == "ssf")
                {
                    var reader = new SSFBinaryReader(sourceData);
                    SSF ssf = reader.Load();
                    SSFSound sound = (SSFSound)pendingRef.LocationData["sound"];
                    int? ssfStrref = ssf.Get(sound);
                    return ssfStrref.HasValue && ssfStrref.Value == pendingRef.OldStrref;
                }

                if (pendingRef.LocationType == "gff")
                {
                    var reader = new GFFBinaryReader(sourceData);
                    GFF gff = reader.Load();
                    string fieldPath = (string)pendingRef.LocationData["field_path"];
                    return CheckGffFieldStrref(gff.Root, fieldPath, pendingRef.OldStrref);
                }

                // NCS verification is temporarily disabled in Python too
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"[DEBUG] Error verifying StrRef location: {e.GetType().Name}: {e.Message}");
                return false;
            }

            return false;
        }

        /// <summary>
        /// Check if a GFF field at the given path contains the StrRef.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3202-3259
        /// </summary>
        private bool CheckGffFieldStrref(GFFStruct gffStruct, string fieldPath, int strref)
        {
            // Parse field path (handle array indices)
            string[] parts = fieldPath.Split('.');
            GFFStruct current = gffStruct;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Contains("[") && part.Contains("]"))
                {
                    // Array access like "ItemList[0]"
                    int bracketStart = part.IndexOf("[");
                    int bracketEnd = part.IndexOf("]");
                    string fieldLabel = part.Substring(0, bracketStart);
                    int index = int.Parse(part.Substring(bracketStart + 1, bracketEnd - bracketStart - 1));

                    // Get the list field
                    GFFList list = current.GetList(fieldLabel);
                    if (index < list.Count)
                    {
                        current = list[index];
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (i == parts.Length - 1)
                {
                    // Last part - check if it has the StrRef
                    LocalizedString locString = current.GetLocString(part);
                    return locString.StringRef == strref;
                }
                else
                {
                    // Not the last part - navigate deeper
                    current = current.GetStruct(part);
                }
            }

            return false;
        }

        /// <summary>
        /// Verify that a 2DA row still exists at the expected location in source data.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3363-3392
        /// </summary>
        private bool Verify2DARowLocation(byte[] sourceData, Pending2DARowReference pendingRef)
        {
            try
            {
                var reader = new GFFBinaryReader(sourceData);
                GFF gff = reader.Load();

                // Get the field names that should reference this 2DA file
                string twodaResname = pendingRef.TwodaFilename.ToLowerInvariant().Replace(".2da", "");
                var relevantFieldNames = new List<string>();

                // Get GFF field to 2DA mapping to identify relevant field names
                // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3363-3392
                // Original: gff_field_to_2da_mapping = _get_gff_field_to_2da_mapping()
                Dictionary<string, ResourceIdentifier> gffFieldTo2daMapping = ReferenceCacheHelpers.GffFieldTo2daMapping();
                ResourceIdentifier target2daIdentifier = new ResourceIdentifier(twodaResname, ResourceType.TwoDA);

                // Find all GFF field names that reference this 2DA file
                foreach (var kvp in gffFieldTo2daMapping)
                {
                    if (kvp.Value.Equals(target2daIdentifier))
                    {
                        relevantFieldNames.Add(kvp.Key);
                    }
                }

                // Verify all field paths in the pending reference still have the row index
                foreach (string fieldPath in pendingRef.FieldPaths)
                {
                    if (!CheckGffField2DARow(gff.Root, fieldPath, pendingRef.RowIndex, relevantFieldNames))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"[DEBUG] Error verifying 2DA row location: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a GFF field at the given path contains the 2DA row index.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3393-3452
        /// </summary>
        private bool CheckGffField2DARow(GFFStruct gffStruct, string fieldPath, int rowIndex, List<string> relevantFieldNames)
        {
            // Parse field path (handle array indices)
            string[] parts = fieldPath.Split('.');
            GFFStruct current = gffStruct;

            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Contains("[") && part.Contains("]"))
                {
                    // Array access like "ItemList[0]"
                    int bracketStart = part.IndexOf("[");
                    int bracketEnd = part.IndexOf("]");
                    string fieldLabel = part.Substring(0, bracketStart);
                    int index = int.Parse(part.Substring(bracketStart + 1, bracketEnd - bracketStart - 1));

                    // Get the list field
                    GFFList list = current.GetList(fieldLabel);
                    if (index < list.Count)
                    {
                        current = list[index];
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (i == parts.Length - 1)
                {
                    // Last part - check if it has the row index
                    // Check if this field name is relevant and has the correct row index
                    if (relevantFieldNames.Count == 0 || relevantFieldNames.Contains(part))
                    {
                        int? fieldValue = current.GetInt32(part);
                        return fieldValue.HasValue && fieldValue.Value == rowIndex;
                    }
                    return false;
                }
                else
                {
                    // Not the last part - navigate deeper
                    current = current.GetStruct(part);
                }
            }

            return false;
        }

        /// <summary>
        /// Create a single 2DA patch for a StrRef reference immediately.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3508-3569
        /// </summary>
        private void CreateImmediate2DAStrrefPatchSingle(
            string filename,
            int oldStrref,
            int tokenId,
            int rowIndex,
            string columnName,
            string resourcePath,
            PatcherModifications modification)
        {
            // Use the provided modification if it matches, otherwise find or create one
            Modifications2DA existingMod;
            bool isNewMod;

            if (modification != null && modification is Modifications2DA mod2da &&
                mod2da.SourceFile.ToLowerInvariant() == filename.ToLowerInvariant())
            {
                existingMod = mod2da;
                isNewMod = !_writtenSections.Contains(existingMod.SourceFile);
            }
            else
            {
                // Get or create 2DA modification from all_modifications
                var foundMod = AllModifications.Twoda.FirstOrDefault(m => m.SourceFile == filename);
                isNewMod = foundMod == null;

                if (foundMod == null)
                {
                    existingMod = new Modifications2DA(filename);
                    AllModifications.Twoda.Add(existingMod);
                }
                else
                {
                    existingMod = foundMod;
                }
            }

            // Create the patch
            var changeRow = new ChangeRow2DA(
                identifier: $"strref_link_{oldStrref}_{rowIndex}_{columnName}",
                target: new Target(TargetType.ROW_INDEX, rowIndex),
                cells: new Dictionary<string, RowValue> { { columnName, new RowValueTLKMemory(tokenId) } });

            existingMod.Modifiers.Add(changeRow);

            // Log the patch being created
            string pathInfo = resourcePath != null ? $" at {resourcePath}" : "";
            _logFunc?.Invoke($"    Creating patch: row {rowIndex}, column '{columnName}' -> Token {tokenId}{pathInfo}");

            // Write to INI
            if (isNewMod)
            {
                Write2DAModification(existingMod, null);
            }
            else
            {
                _writtenSections.Remove(filename);
                WriteToIni(new List<Modifications2DA> { existingMod }, "2da");
                _writtenSections.Add(filename);
            }
        }

        /// <summary>
        /// Create a single SSF patch for a StrRef reference immediately.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3571-3618
        /// </summary>
        private void CreateImmediateSsfStrrefPatchSingle(
            string filename,
            int oldStrref,
            int tokenId,
            SSFSound sound,
            PatcherModifications modification)
        {
            // Use the provided modification if it matches, otherwise find or create one
            ModificationsSSF existingMod;
            bool isNewMod;

            if (modification != null && modification is ModificationsSSF modSsf &&
                modSsf.SourceFile.ToLowerInvariant() == filename.ToLowerInvariant())
            {
                existingMod = modSsf;
                isNewMod = !_writtenSections.Contains(existingMod.SourceFile);
            }
            else
            {
                // Get or create SSF modification from all_modifications
                var foundMod = AllModifications.Ssf.FirstOrDefault(m => m.SourceFile == filename);
                isNewMod = foundMod == null;

                if (foundMod == null)
                {
                    existingMod = new ModificationsSSF(filename, replace: false, modifiers: null);
                    AllModifications.Ssf.Add(existingMod);
                }
                else
                {
                    existingMod = foundMod;
                }
            }

            // Create the patch
            var modifySsf = new ModifySSF(sound, new TokenUsageTLK(tokenId));
            existingMod.Modifiers.Add(modifySsf);

            _logFunc?.Invoke($"    Creating patch: sound '{sound}' -> Token {tokenId}");

            // Write to INI
            if (isNewMod)
            {
                WriteSsfModification(existingMod, null);
            }
            else
            {
                _writtenSections.Remove(filename);
                WriteToIni(new List<ModificationsSSF> { existingMod }, "ssf");
                _writtenSections.Add(filename);
            }
        }

        /// <summary>
        /// Create a single GFF patch for a StrRef reference immediately.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3620-3667
        /// </summary>
        private void CreateImmediateGffStrrefPatchSingle(
            string filename,
            int oldStrref,
            int tokenId,
            string fieldPath,
            PatcherModifications modification)
        {
            // Use the provided modification if it matches, otherwise find or create one
            ModificationsGFF existingMod;
            bool isNewMod;

            if (modification != null && modification is ModificationsGFF modGff &&
                modGff.SourceFile.ToLowerInvariant() == filename.ToLowerInvariant())
            {
                existingMod = modGff;
                isNewMod = !_writtenSections.Contains(existingMod.SourceFile);
            }
            else
            {
                // Get or create GFF modification from all_modifications
                var foundMod = AllModifications.Gff.FirstOrDefault(m => m.SourceFile == filename);
                isNewMod = foundMod == null;

                if (foundMod == null)
                {
                    existingMod = new ModificationsGFF(filename, replace: false, modifiers: null);
                    AllModifications.Gff.Add(existingMod);
                }
                else
                {
                    existingMod = foundMod;
                }
            }

            // Create the patch
            var locstringDelta = new LocalizedStringDelta(new FieldValueTLKMemory(tokenId));
            var modifyField = new ModifyFieldGFF(fieldPath, new FieldValueConstant(locstringDelta));
            existingMod.Modifiers.Add(modifyField);

            _logFunc?.Invoke($"    Creating patch: field '{fieldPath}' -> Token {tokenId}");

            // Write to INI
            if (isNewMod)
            {
                WriteGffModification(existingMod, null);
            }
            else
            {
                _writtenSections.Remove(filename);
                WriteToIni(new List<ModificationsGFF> { existingMod }, "gff");
                _writtenSections.Add(filename);
            }
        }

        /// <summary>
        /// Create GFF patches that replace 2DA row references with 2DAMEMORY tokens.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:3454-3506
        /// </summary>
        private void CreateGff2DAPatch(
            string gffFilename,
            List<string> fieldPaths,
            int tokenId,
            PatcherModifications modification)
        {
            // Use the provided modification if it matches, otherwise find or create one
            ModificationsGFF existingMod;
            bool isNewMod;

            if (modification != null && modification is ModificationsGFF modGff &&
                modGff.SourceFile.ToLowerInvariant() == gffFilename.ToLowerInvariant())
            {
                existingMod = modGff;
                isNewMod = !_writtenSections.Contains(existingMod.SourceFile);
            }
            else
            {
                // Find or create ModificationsGFF from all_modifications
                var foundMod = AllModifications.Gff.FirstOrDefault(m => m.SourceFile == gffFilename);
                isNewMod = foundMod == null;

                if (foundMod == null)
                {
                    existingMod = new ModificationsGFF(gffFilename, replace: false, modifiers: null);
                    AllModifications.Gff.Add(existingMod);
                }
                else
                {
                    existingMod = foundMod;
                }
            }

            // Create ModifyFieldGFF entries for each field path
            foreach (string fieldPath in fieldPaths)
            {
                // Create a FieldValue2DAMemory value
                var fieldValue = new FieldValue2DAMemory(tokenId);
                var modifier = new ModifyFieldGFF(fieldPath, fieldValue);
                existingMod.Modifiers.Add(modifier);

                _logFunc?.Invoke($"    Creating patch: {gffFilename} -> {fieldPath} = 2DAMEMORY{tokenId}");
            }

            // Re-append to update existing section
            if (existingMod.SourceFile != null && _writtenSections.Contains(existingMod.SourceFile))
            {
                _writtenSections.Remove(existingMod.SourceFile);
            }
            WriteToIni(new List<ModificationsGFF> { existingMod }, "gff");
            if (existingMod.SourceFile != null)
            {
                _writtenSections.Add(existingMod.SourceFile);
            }
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
                if (!FileHelpers.IsCapsuleFile(Path.GetFileName(sourcePath)))
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
        /// <summary>
        /// Write resource using appropriate io function.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:4164-4195
        /// Original: def _write_resource_with_io(self, data: bytes, dest_path: Path, file_ext: str) -> None: ...
        /// </summary>
        private void WriteResourceWithIo(byte[] data, string destPath, string fileExt)
        {
            try
            {
                string extUpper = fileExt.ToUpperInvariant();

                // Check if it's a GFF extension
                var gffExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "ARE", "BIC", "BTC", "BTD", "BTE", "BTI", "BTM", "BTP", "BTT",
                    "DLG", "FAC", "GFF", "GIT", "GUI", "GVT", "IFO", "INV", "ITP",
                    "JRL", "NFO", "PTH", "PT", "UTC", "UTD", "UTE", "UTI", "UTM",
                    "UTP", "UTS", "UTT", "UTW"
                };

                if (gffExtensions.Contains(extUpper))
                {
                    var reader = new GFFBinaryReader(data);
                    GFF gff = reader.Load();
                    ResourceType resType = ResourceType.FromExtension(fileExt);
                    GFFAuto.WriteGff(gff, destPath, resType);
                }
                else if (extUpper == "2DA")
                {
                    var reader = new TwoDABinaryReader(data);
                    TwoDA twoda = reader.Load();
                    TwoDAAuto.Write2DA(twoda, destPath, ResourceType.TwoDA);
                }
                else if (extUpper == "TLK")
                {
                    var reader = new TLKBinaryReader(data);
                    TLK tlk = reader.Load();
                    TLKAuto.WriteTlk(tlk, destPath, ResourceType.TLK);
                }
                else if (extUpper == "SSF")
                {
                    var reader = new SSFBinaryReader(data);
                    SSF ssf = reader.Load();
                    SSFAuto.WriteSsf(ssf, destPath, ResourceType.SSF);
                }
                else if (extUpper == "LIP")
                {
                    var reader = new LIPBinaryReader(data);
                    LIP lip = reader.Load();
                    LIPAuto.WriteLip(lip, destPath, ResourceType.LIP);
                }
                else
                {
                    // Binary file
                    File.WriteAllBytes(destPath, data);
                }
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"[Warning] Failed to use io function for {fileExt}, using binary write: {e.GetType().Name}: {e.Message}");
                _logFunc?.Invoke($"Full traceback:");
                _logFunc?.Invoke(e.StackTrace ?? "");
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
        /// Allocate a new 2DAMEMORY token ID.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1673-1677
        /// </summary>
        private int Allocate2DAToken()
        {
            int tokenId = _next2DATokenId;
            _next2DATokenId++;
            return tokenId;
        }

        /// <summary>
        /// Reserve existing 2DA token IDs to prevent conflicts.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1668-1671
        /// </summary>
        private void ReserveExistingTwodaTokens(IEnumerable<int> tokenIds)
        {
            foreach (int tokenId in tokenIds)
            {
                if (tokenId >= _next2DATokenId)
                {
                    _next2DATokenId = tokenId + 1;
                }
            }
        }

        /// <summary>
        /// Create 2DAMEMORY tokens for AddColumn index_insert values that match GFF field values.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1679-1780
        /// </summary>
        private void CreateAddColumn2DAMemoryTokens()
        {
            // Build a map of cell values -> list of (AddColumn, store_key) that have that value
            var valueToAddColumnCells = new Dictionary<string, List<Tuple<AddColumn2DA, string>>>();

            foreach (var mod2da in AllModifications.Twoda)
            {
                foreach (var modifier in mod2da.Modifiers)
                {
                    if (!(modifier is AddColumn2DA addColumn))
                    {
                        continue;
                    }

                    // Scan index_insert for values (I#)
                    if (addColumn.IndexInsert != null)
                    {
                        foreach (var kvp in addColumn.IndexInsert)
                        {
                            int rowIdx = kvp.Key;
                            var rowValue = kvp.Value;
                            if (!(rowValue is RowValueConstant constant))
                            {
                                continue;
                            }

                            string cellValueStr = constant.String;
                            string storeKey = $"I{rowIdx}";
                            if (!valueToAddColumnCells.ContainsKey(cellValueStr))
                            {
                                valueToAddColumnCells[cellValueStr] = new List<Tuple<AddColumn2DA, string>>();
                            }
                            valueToAddColumnCells[cellValueStr].Add(Tuple.Create(addColumn, storeKey));
                        }
                    }

                    // Scan label_insert for values (Llabel)
                    if (addColumn.LabelInsert != null)
                    {
                        foreach (var kvp in addColumn.LabelInsert)
                        {
                            string rowLabel = kvp.Key;
                            var rowValue = kvp.Value;
                            if (!(rowValue is RowValueConstant constant))
                            {
                                continue;
                            }

                            string cellValueStr = constant.String;
                            string storeKey = $"L{rowLabel}";
                            if (!valueToAddColumnCells.ContainsKey(cellValueStr))
                            {
                                valueToAddColumnCells[cellValueStr] = new List<Tuple<AddColumn2DA, string>>();
                            }
                            valueToAddColumnCells[cellValueStr].Add(Tuple.Create(addColumn, storeKey));
                        }
                    }
                }
            }

            if (valueToAddColumnCells.Count == 0)
            {
                return; // No AddColumn values to link
            }

            // Scan all GFF modifications for matching field values
            int tokensCreated = 0;
            foreach (var modGff in AllModifications.Gff)
            {
                foreach (var gffModifier in modGff.Modifiers)
                {
                    if (!(gffModifier is ModifyFieldGFF modifyField))
                    {
                        continue;
                    }

                    // Check if this field value matches any AddColumn cell value
                    if (!(modifyField.Value is FieldValueConstant fieldConstant))
                    {
                        continue;
                    }

                    // Convert field value to string for comparison
                    string fieldValueStr = fieldConstant.Stored?.ToString() ?? "";

                    if (!valueToAddColumnCells.ContainsKey(fieldValueStr))
                    {
                        continue;
                    }

                    // MATCH FOUND! Create token and link
                    // Use the first matching AddColumn cell (if multiple match the same value)
                    var (addColumnModifier, storeKey) = valueToAddColumnCells[fieldValueStr][0];

                    // Check if a token already exists for this value
                    int? existingTokenId = null;
                    if (addColumnModifier.Store2DA != null)
                    {
                        foreach (var kvp in addColumnModifier.Store2DA)
                        {
                            int tokenId = kvp.Key;
                            string storeValue = kvp.Value;
                            if (storeValue == storeKey)
                            {
                                existingTokenId = tokenId;
                                break;
                            }
                        }
                    }

                    int tokenIdToUse;
                    if (!existingTokenId.HasValue)
                    {
                        // Allocate new token
                        tokenIdToUse = Allocate2DAToken();

                        // Store in AddColumn: 2DAMEMORY#=I{row_idx}
                        // Note: store_2da for AddColumn uses string values, not RowValue objects
                        // Store2DA is initialized in constructor, so it should never be null
                        // We can modify the dictionary contents even though the property is read-only
                        addColumnModifier.Store2DA[tokenIdToUse] = storeKey;

                        _logFunc?.Invoke($"  [AddColumn Token] Created 2DAMEMORY{tokenIdToUse}={storeKey} for value '{fieldValueStr}'");
                        tokensCreated++;
                    }
                    else
                    {
                        tokenIdToUse = existingTokenId.Value;
                        _logFunc?.Invoke($"  [AddColumn Token] Reusing existing 2DAMEMORY{tokenIdToUse}={storeKey} for value '{fieldValueStr}'");
                    }

                    // Replace GFF field value with token reference
                    // Since Value is read-only, we need to create a new ModifyFieldGFF and replace it in the list
                    int modifierIndex = modGff.Modifiers.IndexOf(modifyField);
                    if (modifierIndex >= 0)
                    {
                        var newModifyField = new ModifyFieldGFF(modifyField.Path, new FieldValue2DAMemory(tokenIdToUse), modifyField.Identifier);
                        modGff.Modifiers[modifierIndex] = newModifyField;
                        _logFunc?.Invoke($"  [AddColumn Token] Replaced {modGff.SourceFile} {modifyField.Path} = {fieldValueStr} with 2DAMEMORY{tokenIdToUse}");
                    }
                }
            }

            if (tokensCreated > 0)
            {
                _logFunc?.Invoke($"  Created {tokensCreated} AddColumn 2DAMEMORY token(s) with cross-file linking");
            }
        }

        /// <summary>
        /// Replace cell values with 2DAMEMORY token references when values match stored tokens.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:1782-1880
        /// </summary>
        private void ReplaceCellsWith2DAMemoryTokens()
        {
            // First, handle AddColumn token linking (must happen before processing other modifiers)
            CreateAddColumn2DAMemoryTokens();

            // Track which tokens are available at each point (tokens that have been stored)
            // Format: {stored_value: token_id} - only includes tokens that have been stored so far
            var availableTokens = new Dictionary<string, int>();

            int replacementsMade = 0;

            // Process modifiers in order to ensure tokens are stored before being used
            foreach (var mod2da in AllModifications.Twoda)
            {
                foreach (var modifier in mod2da.Modifiers)
                {
                    if (!(modifier is ChangeRow2DA changeRow) && !(modifier is AddRow2DA addRow))
                    {
                        continue;
                    }

                    // Cast to concrete type to access properties
                    ChangeRow2DA changeRowMod = modifier as ChangeRow2DA;
                    AddRow2DA addRowMod = modifier as AddRow2DA;
                    Dictionary<int, RowValue> store2DA = changeRowMod?.Store2DA ?? addRowMod?.Store2DA;
                    Dictionary<string, RowValue> cells = changeRowMod?.Cells ?? addRowMod?.Cells;
                    string identifier = changeRowMod?.Identifier ?? addRowMod?.Identifier;

                    // First, process store_2da entries to add tokens to availableTokens
                    if (store2DA != null)
                    {
                        foreach (var kvp in store2DA)
                        {
                            int tokenId = kvp.Key;
                            object rowValue = kvp.Value;
                            string storedValue = null;

                            if (rowValue is RowValueRowIndex)
                            {
                                // For RowIndex, the stored value is the row index as a string
                                if (addRowMod != null)
                                {
                                    // For AddRow2DA with RowValueRowIndex, the row index isn't known until runtime
                                    // So we can't match it statically - skip this token for matching
                                    continue;
                                }
                                if (changeRowMod != null &&
                                    changeRowMod.Target != null &&
                                    changeRowMod.Target.TargetType == TargetType.ROW_INDEX &&
                                    changeRowMod.Target.Value is int rowIndex)
                                {
                                    storedValue = rowIndex.ToString();
                                }
                            }
                            else if (rowValue is RowValueConstant constant)
                            {
                                // For constant values, the stored value is the string itself
                                storedValue = constant.String;
                            }
                            else if (rowValue is RowValueRowLabel rowLabel)
                            {
                                // For RowLabel, we can match if we have the row label
                                if (changeRowMod != null &&
                                    changeRowMod.Target != null &&
                                    changeRowMod.Target.TargetType == TargetType.ROW_LABEL &&
                                    changeRowMod.Target.Value is string label)
                                {
                                    storedValue = label;
                                }
                                else if (addRowMod != null && !string.IsNullOrEmpty(addRowMod.RowLabel))
                                {
                                    storedValue = addRowMod.RowLabel;
                                }
                            }
                            else if (rowValue is RowValueRowCell rowCell)
                            {
                                // For RowValueRowCell, we need to evaluate the referenced column from the cells dictionary
                                // This allows static evaluation when the referenced column has a statically evaluable value
                                // Based on PyKotor: RowValueRowCell references another column in the same row
                                // We can evaluate it if the referenced column exists in cells and can be statically evaluated
                                if (cells != null)
                                {
                                    storedValue = EvaluateRowValueRowCellStatic(rowCell, cells, new HashSet<string>());
                                }
                            }

                            if (storedValue != null &&
                                (!availableTokens.ContainsKey(storedValue) || tokenId < availableTokens[storedValue]))
                            {
                                availableTokens[storedValue] = tokenId;
                            }
                        }
                    }

                    // Now check cell values and replace with available tokens
                    // Only replace with tokens that have been stored in previous modifiers
                    if (cells != null)
                    {
                        var cellsToUpdate = new Dictionary<string, RowValue>();
                        foreach (var kvp in cells)
                        {
                            string columnName = kvp.Key;
                            RowValue cellValue = kvp.Value;

                            if (cellValue is RowValueConstant constant)
                            {
                                string cellString = constant.String;
                                if (availableTokens.ContainsKey(cellString))
                                {
                                    int tokenId = availableTokens[cellString];
                                    // Replace with 2DAMEMORY token reference
                                    cellsToUpdate[columnName] = new RowValue2DAMemory(tokenId);
                                    replacementsMade++;
                                    _logFunc?.Invoke($"  Replaced {mod2da.SourceFile} {identifier} {columnName}={cellString} with 2DAMEMORY{tokenId}");
                                }
                            }
                        }

                        // Apply updates
                        foreach (var kvp in cellsToUpdate)
                        {
                            cells[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            if (replacementsMade > 0)
            {
                _logFunc?.Invoke($"  Replaced {replacementsMade} cell value(s) with 2DAMEMORY token references");
            }
        }

        /// <summary>
        /// Statically evaluate a RowValueRowCell by checking if the referenced column exists in cells and can be statically evaluated.
        /// Returns the evaluated string value if it can be determined statically, or null if it cannot.
        /// </summary>
        [CanBeNull]
        private string EvaluateRowValueRowCellStatic(RowValueRowCell rowCell, Dictionary<string, RowValue> cells, HashSet<string> visitedColumns)
        {
            // Check for circular references
            if (visitedColumns.Contains(rowCell.Column))
            {
                return null; // Circular reference detected, cannot evaluate statically
            }

            // Check if the referenced column exists in cells
            if (!cells.ContainsKey(rowCell.Column))
            {
                return null; // Column doesn't exist, cannot evaluate statically
            }

            RowValue referencedValue = cells[rowCell.Column];

            // If it's a constant, we can evaluate it statically
            if (referencedValue is RowValueConstant constant)
            {
                return constant.String;
            }

            // If it's another RowValueRowCell, recursively evaluate it
            if (referencedValue is RowValueRowCell nestedRowCell)
            {
                visitedColumns.Add(rowCell.Column);
                string result = EvaluateRowValueRowCellStatic(nestedRowCell, cells, visitedColumns);
                visitedColumns.Remove(rowCell.Column);
                return result;
            }

            // For other types (RowValue2DAMemory, RowValueTLKMemory, RowValueRowIndex, RowValueRowLabel, RowValueHigh),
            // we cannot evaluate them statically without runtime context
            return null;
        }

        /// <summary>
        /// Completely rewrite the INI file from all accumulated modifications.
        /// </summary>
        private void RewriteIniComplete()
        {
            // Replace cell values with 2DAMEMORY token references before rewriting
            ReplaceCellsWith2DAMemoryTokens();

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

        /// <summary>
        /// Create linking patches for StrRef references found in source installations.
        /// Searches through GFF, 2DA, and other files for references to old StrRef IDs and replaces them with new token IDs.
        /// </summary>
        private void CreateStrRefLinkingPatches(
            ModificationsTLK modTlk,
        Dictionary<int, int> strrefMappings,
        List<object> sourceInstallations)
        {
            int totalPatchesCreated = 0;

            // Process each StrRef mapping
            foreach (var kvp in strrefMappings)
            {
                int oldStrref = kvp.Key;
                int newTokenId = kvp.Value;

                _logFunc?.Invoke($"  Processing StrRef {oldStrref} -> Token {newTokenId}");

                // Search each source installation for references to this StrRef
                foreach (var source in sourceInstallations)
                {
                    int patchesForThisStrref = SearchAndPatchStrRefReferences(source, oldStrref, newTokenId);
                    totalPatchesCreated += patchesForThisStrref;
                }
            }

            if (totalPatchesCreated > 0)
            {
                _logFunc?.Invoke($"  Created {totalPatchesCreated} linking patches for StrRef references");
            }
            else
            {
                _logFunc?.Invoke("  No StrRef references found to patch");
            }
        }

        /// <summary>
        /// Search for references to a specific StrRef in a source installation and create linking patches.
        /// </summary>
        private int SearchAndPatchStrRefReferences(object source, int oldStrref, int newTokenId)
        {
            int patchesCreated = 0;

            try
            {
                string sourcePath;
                if (source is string strSource)
                {
                    sourcePath = strSource;
                }
                else if (source is InstallationClass installation)
                {
                    sourcePath = installation.Path;
                }
                else
                {
                    _logFunc?.Invoke($"    [Warning] Unsupported source type: {source.GetType().Name}");
                    return 0;
                }

                if (!Directory.Exists(sourcePath))
                {
                    _logFunc?.Invoke($"    [Warning] Source path does not exist: {sourcePath}");
                    return 0;
                }

                // Search for GFF files that might contain StrRef references
                patchesCreated += SearchGffFilesForStrRef(sourcePath, oldStrref, newTokenId);

                // Search for 2DA files that might contain StrRef references
                patchesCreated += SearchTwoDaFilesForStrRef(sourcePath, oldStrref, newTokenId);

                // Could be extended to search other file types (NSS scripts, etc.)
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"    [Error] Failed to search for StrRef references: {e.GetType().Name}: {e.Message}");
            }

            return patchesCreated;
        }

        /// <summary>
        /// Search GFF files for StrRef references and create linking patches.
        /// </summary>
        private int SearchGffFilesForStrRef(string sourcePath, int oldStrref, int newTokenId)
        {
            int patchesCreated = 0;

            try
            {
                // Find all GFF files in the source
                var gffFiles = Directory.GetFiles(sourcePath, "*.gff", SearchOption.AllDirectories);

                foreach (string gffFile in gffFiles)
                {
                    try
                    {
                        // Load the GFF file
                        byte[] gffData = File.ReadAllBytes(gffFile);
                        var gffReader = new Formats.GFF.GFFBinaryReader(gffData);
                        var gff = gffReader.Load();

                        // Search for StrRef fields in the GFF structure
                        var strrefFields = FindStrRefFieldsRecursive(gff.Root, oldStrref);

                        if (strrefFields.Count > 0)
                        {
                            // Create GFF modifications to replace StrRef with TLKMEMORY token
                            var modifications = new Mods.GFF.ModificationsGFF(gffFile, replace: false, modifiers: null);

                            foreach (var fieldPath in strrefFields)
                            {
                                // Create a TLKMEMORY modifier that replaces the StrRef field
                                var fieldValue = new Mods.GFF.FieldValueTLKMemory(newTokenId);
                                var modifier = new Mods.GFF.ModifyFieldGFF(fieldPath, fieldValue);
                                modifications.Modifiers.Add(modifier);
                            }

                            // Add to modifications and write immediately
                            AllModifications.Gff.Add(modifications);
                            WriteGffModification(modifications, gffData);

                            patchesCreated += strrefFields.Count;
                            _logFunc?.Invoke($"      Created {strrefFields.Count} GFF patches in {Path.GetFileName(gffFile)}");
                        }
                    }
                    catch (Exception e)
                    {
                        _logFunc?.Invoke($"      [Warning] Failed to process GFF file {gffFile}: {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"    [Error] Failed to search GFF files: {e.GetType().Name}: {e.Message}");
            }

            return patchesCreated;
        }

        /// <summary>
        /// Search 2DA files for StrRef references and create linking patches.
        /// </summary>
        private int SearchTwoDaFilesForStrRef(string sourcePath, int oldStrref, int newTokenId)
        {
            int patchesCreated = 0;

            try
            {
                // Find all 2DA files in the source
                var twodaFiles = Directory.GetFiles(sourcePath, "*.2da", SearchOption.AllDirectories);

                foreach (string twodaFile in twodaFiles)
                {
                    try
                    {
                        // Load the 2DA file
                        byte[] twodaData = File.ReadAllBytes(twodaFile);
                        var twodaReader = new Formats.TwoDA.TwoDABinaryReader(twodaData);
                        var twoda = twodaReader.Load();

                        // Search for cells containing the StrRef value
                        var strrefCells = FindStrRefCellsInTwoDa(twoda, oldStrref);

                        if (strrefCells.Count > 0)
                        {
                            // Create 2DA modifications to replace StrRef with TLKMEMORY token
                            var modifications = new Mods.TwoDA.Modifications2DA(twodaFile);

                            foreach (var cellRef in strrefCells)
                            {
                                // Create a TLKMEMORY modifier that replaces the cell value
                                var target = new Mods.TwoDA.Target(
                                    Mods.TwoDA.TargetType.ROW_INDEX,
                                    cellRef.RowIndex);
                                var cells = new Dictionary<string, Mods.TwoDA.RowValue>
                                {
                                    { cellRef.ColumnName, new Mods.TwoDA.RowValueTLKMemory(newTokenId) }
                                };
                                var modifier = new Mods.TwoDA.ChangeRow2DA(
                                    $"strref_{cellRef.RowIndex}_{cellRef.ColumnName}",
                                    target,
                                    cells);
                                modifications.Modifiers.Add(modifier);
                            }

                            // Add to modifications and write immediately
                            AllModifications.Twoda.Add(modifications);
                            Write2DAModification(modifications, twodaData, null, null);

                            patchesCreated += strrefCells.Count;
                            _logFunc?.Invoke($"      Created {strrefCells.Count} 2DA patches in {Path.GetFileName(twodaFile)}");
                        }
                    }
                    catch (Exception e)
                    {
                        _logFunc?.Invoke($"      [Warning] Failed to process 2DA file {twodaFile}: {e.GetType().Name}: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                _logFunc?.Invoke($"    [Error] Failed to search 2DA files: {e.GetType().Name}: {e.Message}");
            }

            return patchesCreated;
        }

        /// <summary>
        /// Recursively search GFF structure for fields containing a specific StrRef value.
        /// </summary>
        public List<string> FindStrRefFieldsRecursive(Andastra.Parsing.Formats.GFF.GFFStruct gffStruct, int targetStrref, string currentPath = "")
        {
            var foundPaths = new List<string>();

            // Iterate through all fields in the struct
            foreach (var (label, fieldType, value) in gffStruct)
            {
                string fieldPath = string.IsNullOrEmpty(currentPath) ? label : $"{currentPath}.{label}";

                // Check Int32 fields that might be StrRefs (StrRef fields are typically named with "StrRef" in them)
                if (fieldType == Formats.GFF.GFFFieldType.Int32)
                {
                    if (label.Contains("StrRef", StringComparison.OrdinalIgnoreCase) ||
                        label.Contains("strref", StringComparison.OrdinalIgnoreCase) ||
                        fieldPath.Contains("StrRef", StringComparison.OrdinalIgnoreCase) ||
                        fieldPath.Contains("strref", StringComparison.OrdinalIgnoreCase))
                    {
                        int strrefValue = gffStruct.GetInt32(label);
                        if (strrefValue == targetStrref)
                        {
                            foundPaths.Add(fieldPath);
                        }
                    }
                }
                // Check LocalizedString fields for StrRef
                else if (fieldType == Formats.GFF.GFFFieldType.LocalizedString)
                {
                    if (value is Common.LocalizedString locString)
                    {
                        if (locString.StringRef == targetStrref)
                        {
                            foundPaths.Add(fieldPath);
                        }
                    }
                }
                // Recursively search nested structs
                else if (fieldType == Formats.GFF.GFFFieldType.Struct)
                {
                    if (value is Formats.GFF.GFFStruct nestedStruct)
                    {
                        foundPaths.AddRange(FindStrRefFieldsRecursive(nestedStruct, targetStrref, fieldPath));
                    }
                }
                // Recursively search list elements
                else if (fieldType == Formats.GFF.GFFFieldType.List)
                {
                    if (value is Formats.GFF.GFFList listData)
                    {
                        for (int i = 0; i < listData.Count; i++)
                        {
                            if (listData[i] is Formats.GFF.GFFStruct listStruct)
                            {
                                string listPath = $"{fieldPath}[{i}]";
                                foundPaths.AddRange(FindStrRefFieldsRecursive(listStruct, targetStrref, listPath));
                            }
                        }
                    }
                }
            }

            return foundPaths;
        }

        /// <summary>
        /// Search 2DA file for cells containing a specific StrRef value.
        /// </summary>
        private List<TwoDaCellReference> FindStrRefCellsInTwoDa(Andastra.Parsing.Formats.TwoDA.TwoDA twoda, int targetStrref)
        {
            var foundCells = new List<TwoDaCellReference>();

            // Check if this 2DA has columns that might contain StrRefs
            // StrRef columns are often named with "strref", "stringref", etc.
            var strrefColumns = twoda.GetHeaders().Where(h =>
                h.Contains("strref", StringComparison.OrdinalIgnoreCase) ||
                h.Contains("stringref", StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (string columnName in strrefColumns)
            {
                for (int rowIndex = 0; rowIndex < twoda.GetHeight(); rowIndex++)
                {
                    string cellValue = twoda.GetCellString(rowIndex, columnName);
                    if (int.TryParse(cellValue, out int strrefValue) && strrefValue == targetStrref)
                    {
                        foundCells.Add(new TwoDaCellReference
                        {
                            RowIndex = rowIndex,
                            ColumnName = columnName,
                            Value = cellValue
                        });
                    }
                }
            }

            return foundCells;
        }

        /// <summary>
        /// Helper class to reference a 2DA cell.
        /// </summary>
        private class TwoDaCellReference
        {
            public int RowIndex { get; set; }
            public string ColumnName { get; set; }
            public string Value { get; set; }
        }

        // Expose install folders for summary
        public Dictionary<string, List<string>> InstallFolders => _installFolders;
    }
}

