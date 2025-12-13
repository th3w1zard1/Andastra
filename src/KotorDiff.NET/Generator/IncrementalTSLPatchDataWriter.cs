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
using JetBrains.Annotations;
using KotorDiff.NET.Diff;
using KotorDiff.NET.Logger;

namespace KotorDiff.NET.Generator
{
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

        /// <summary>
        /// Initialize incremental writer.
        /// </summary>
        public IncrementalTSLPatchDataWriter(
            string tslpatchdataPath,
            string iniFilename,
            [CanBeNull] string baseDataPath = null,
            [CanBeNull] string moddedDataPath = null,
            [CanBeNull] object strrefCache = null, // TODO: Implement StrRefReferenceCache
            [CanBeNull] object twodaCaches = null, // TODO: Implement TwoDAMemoryReferenceCache
            [CanBeNull] Action<string> logFunc = null)
        {
            _tslpatchdataPath = tslpatchdataPath;
            _iniPath = Path.Combine(tslpatchdataPath, iniFilename);
            _baseDataPath = baseDataPath;
            _moddedDataPath = moddedDataPath;
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
            File.WriteAllText(_iniPath, content, Encoding.UTF8);
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
        /// </summary>
        public void AddModification(PatcherModifications modification)
        {
            if (modification is Modifications2DA mod2da)
            {
                Write2DaModification(mod2da);
            }
            else if (modification is ModificationsGFF modGff)
            {
                WriteGffModification(modGff);
            }
            else if (modification is ModificationsTLK modTlk)
            {
                WriteTlkModification(modTlk);
            }
            else if (modification is ModificationsSSF modSsf)
            {
                WriteSsfModification(modSsf);
            }
            else if (modification is ModificationsNCS modNcs)
            {
                WriteNcsModification(modNcs);
            }
            else
            {
                _logFunc?.Invoke($"[Warning] Unknown modification type: {modification.GetType().Name}");
            }
        }

        /// <summary>
        /// Write 2DA resource file and INI section.
        /// </summary>
        private void Write2DaModification(Modifications2DA mod2da)
        {
            string filename = mod2da.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla 2DA that will be patched)
            // TODO: Load source_data and write 2DA file

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
        /// </summary>
        private void WriteGffModification(ModificationsGFF modGff)
        {
            string filename = modGff.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla GFF that will be patched)
            // TODO: Load source_data and write GFF file

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
        /// Write SSF resource file and INI section.
        /// </summary>
        private void WriteSsfModification(ModificationsSSF modSsf)
        {
            string filename = modSsf.SourceFile;

            // Skip if already written
            if (_writtenSections.Contains(filename))
            {
                return;
            }

            // Write resource file (base vanilla SSF that will be patched)
            // TODO: Load source_data and write SSF file

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
        /// Write NCS modification (placeholder for now).
        /// </summary>
        private void WriteNcsModification(ModificationsNCS modNcs)
        {
            // TODO: Implement NCS modification writing
            _logFunc?.Invoke($"[Warning] NCS modification writing not yet implemented for {modNcs.SourceFile}");
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
            File.WriteAllText(_iniPath, iniContent, Encoding.UTF8);
        }

        /// <summary>
        /// Finalize the INI file.
        /// All sections are already written incrementally in real-time.
        /// This method just logs a summary and flushes any pending writes.
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

        // Expose install folders for summary
        public Dictionary<string, List<string>> InstallFolders => _installFolders;
    }
}

