// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:68-129
// Original: class TSLPatchDataGenerator: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Mods.GFF;
using CSharpKOTOR.Mods.SSF;
using CSharpKOTOR.Mods.TLK;
using CSharpKOTOR.Mods.TwoDA;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Formats.SSF;
using CSharpKOTOR.Resources;
using GFFContent = CSharpKOTOR.Formats.GFF.GFFContent;
using TLKAuto = CSharpKOTOR.Formats.TLK.TLKAuto;
using TwoDAAuto = CSharpKOTOR.Formats.TwoDA.TwoDAAuto;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;
using SSFAuto = CSharpKOTOR.Formats.SSF.SSFAuto;

namespace KotorDiff.NET.Generator
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:68-129
    // Original: class TSLPatchDataGenerator: ...
    public class TSLPatchDataGenerator
    {
        private readonly DirectoryInfo _tslpatchdataPath;

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:71-79
        // Original: def __init__(self, tslpatchdata_path: Path): ...
        public TSLPatchDataGenerator(DirectoryInfo tslpatchdataPath)
        {
            _tslpatchdataPath = tslpatchdataPath;
            if (!_tslpatchdataPath.Exists)
            {
                _tslpatchdataPath.Create();
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:81-129
        // Original: def generate_all_files(...): ...
        public Dictionary<string, FileInfo> GenerateAllFiles(
            ModificationsByType modifications,
            DirectoryInfo baseDataPath = null)
        {
            var generatedFiles = new Dictionary<string, FileInfo>();

            // Generate TLK files
            if (modifications.Tlk != null && modifications.Tlk.Count > 0)
            {
                var tlkFiles = GenerateTlkFiles(modifications.Tlk, baseDataPath);
                foreach (var kvp in tlkFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate 2DA files
            if (modifications.Twoda != null && modifications.Twoda.Count > 0)
            {
                var twodaFiles = Generate2DaFiles(modifications.Twoda, baseDataPath);
                foreach (var kvp in twodaFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate GFF files
            if (modifications.Gff != null && modifications.Gff.Count > 0)
            {
                var gffFiles = GenerateGffFiles(modifications.Gff, baseDataPath);
                foreach (var kvp in gffFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Generate SSF files
            if (modifications.Ssf != null && modifications.Ssf.Count > 0)
            {
                var ssfFiles = GenerateSsfFiles(modifications.Ssf, baseDataPath);
                foreach (var kvp in ssfFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            // Copy missing files from install folders if base_data_path provided
            if (modifications.Install != null && modifications.Install.Count > 0 && baseDataPath != null)
            {
                var installFiles = CopyInstallFiles(modifications.Install, baseDataPath);
                foreach (var kvp in installFiles)
                {
                    generatedFiles[kvp.Key] = kvp.Value;
                }
            }

            return generatedFiles;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:289-338
        // Original: def _generate_tlk_files(...): ...
        private Dictionary<string, FileInfo> GenerateTlkFiles(
            List<ModificationsTLK> tlkModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (tlkModifications == null || tlkModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modTlk in tlkModifications)
            {
                // All modifiers should be appends (no replacements per TSLPatcher design)
                var appends = modTlk.Modifiers.Where(m => !m.IsReplacement).ToList();

                // Warn if any replacements are found
                var replacements = modTlk.Modifiers.Where(m => m.IsReplacement).ToList();
                if (replacements.Count > 0)
                {
                    Console.WriteLine($"[WARNING] Found {replacements.Count} replacement modifiers in TLK - TSLPatcher only supports appends!");
                }

                // Create append.tlk with all modifiers
                if (appends.Count > 0)
                {
                    var appendTlk = new TLK();
                    appendTlk.Resize(appends.Count);

                    // Sort by token_id (which is the index in append.tlk)
                    var sortedAppends = appends.OrderBy(m => m.TokenId).ToList();

                    for (int appendIdx = 0; appendIdx < sortedAppends.Count; appendIdx++)
                    {
                        var modifier = sortedAppends[appendIdx];
                        string text = modifier.Text ?? "";
                        string soundStr = modifier.Sound ?? "";
                        appendTlk.Replace(appendIdx, text, soundStr);
                    }

                    var appendPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, "append.tlk"));
                    TLKAuto.WriteTlk(appendTlk, appendPath.FullName, ResourceType.TLK);
                    generated["append.tlk"] = appendPath;
                }
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:340-391
        // Original: def _generate_2da_files(...): ...
        private Dictionary<string, FileInfo> Generate2DaFiles(
            List<Modifications2DA> twodaModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (twodaModifications == null || twodaModifications.Count == 0)
            {
                return generated;
            }

            foreach (var mod2da in twodaModifications)
            {
                string filename = mod2da.SourceFile;
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));

                // Try to load base 2DA file from baseDataPath
                if (baseDataPath != null)
                {
                    // Try Override first, then other locations
                    var potentialPaths = new[]
                    {
                        new FileInfo(Path.Combine(baseDataPath.FullName, "Override", filename)),
                        new FileInfo(Path.Combine(baseDataPath.FullName, filename))
                    };

                    bool found = false;
                    foreach (var potentialPath in potentialPaths)
                    {
                        if (potentialPath.Exists)
                        {
                            try
                            {
                                // Copy using TwoDA reader/writer to ensure proper format
                                var twodaObj = new TwoDABinaryReader(potentialPath.FullName).Load();
                                TwoDAAuto.WriteTwoDA(twodaObj, outputPath.FullName, ResourceType.TwoDA);
                                generated[filename] = outputPath;
                                found = true;
                                break;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[ERROR] Failed to read base 2DA {potentialPath.FullName}: {e.Message}");
                                continue;
                            }
                        }
                    }

                    if (!found)
                    {
                        Console.WriteLine($"[WARNING] Could not find base 2DA file for {filename} - TSLPatcher may fail without it");
                    }
                }
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:393-450
        // Original: def _generate_gff_files(...): ...
        private Dictionary<string, FileInfo> GenerateGffFiles(
            List<ModificationsGFF> gffModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (gffModifications == null || gffModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modGff in gffModifications)
            {
                bool replaceFile = modGff.ReplaceFile;

                // Get the actual filename (might be different from sourcefile)
                string filename = !string.IsNullOrEmpty(modGff.SaveAs) ? modGff.SaveAs : modGff.SourceFile;

                // CRITICAL: ALL files go directly in tslpatchdata root, NOT in subdirectories
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));

                // Try to load base file if baseDataPath provided, otherwise create new
                GFF baseGff = LoadOrCreateGff(baseDataPath, filename);

                // For replace operations, apply modifications
                // For patch operations, just copy the base file as-is
                if (replaceFile)
                {
                    // Apply all modifications to generate the replacement file
                    // TODO: Implement _apply_gff_modifications
                    // For now, just copy the base file
                }

                // Write the GFF file
                GFFAuto.WriteGff(baseGff, outputPath.FullName, ResourceType.GFF);
                generated[filename] = outputPath;
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:452-488
        // Original: def _load_or_create_gff(...): ...
        private GFF LoadOrCreateGff(DirectoryInfo baseDataPath, string filename)
        {
            // Try to load base file if baseDataPath provided
            if (baseDataPath != null)
            {
                var potentialBase = new FileInfo(Path.Combine(baseDataPath.FullName, filename));
                if (potentialBase.Exists)
                {
                    try
                    {
                        var baseGff = new GFFBinaryReader(potentialBase.FullName).Load();
                        return baseGff;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[DEBUG] Could not load base GFF {potentialBase.FullName}: {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            // Create new GFF structure
            string ext = Path.GetExtension(filename).TrimStart('.').ToUpperInvariant();
            GFFContent gffContent;
            try
            {
                if (Enum.TryParse<GFFContent>(ext, out GFFContent parsedContent))
                {
                    gffContent = parsedContent;
                }
                else
                {
                    gffContent = GFFContent.GFF;
                }
            }
            catch
            {
                gffContent = GFFContent.GFF;
            }

            return new GFF(gffContent);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:911-972
        // Original: def _generate_ssf_files(...): ...
        private Dictionary<string, FileInfo> GenerateSsfFiles(
            List<ModificationsSSF> ssfModifications,
            DirectoryInfo baseDataPath)
        {
            var generated = new Dictionary<string, FileInfo>();

            if (ssfModifications == null || ssfModifications.Count == 0)
            {
                return generated;
            }

            foreach (var modSsf in ssfModifications)
            {
                bool replaceFile = modSsf.ReplaceFile;

                // Create new SSF or load base
                SSF ssf = null;
                if (baseDataPath != null)
                {
                    var potentialBase = new FileInfo(Path.Combine(baseDataPath.FullName, modSsf.SourceFile));
                    if (potentialBase.Exists)
                    {
                        try
                        {
                            ssf = new SSFBinaryReader(potentialBase.FullName).Load();
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"[DEBUG] Could not load base SSF '{potentialBase.FullName}': {e.GetType().Name}: {e.Message}");
                        }
                    }
                }

                if (ssf == null)
                {
                    ssf = new SSF();
                }

                // For replace operations, apply modifications
                // For patch operations, just copy the base file as-is
                if (replaceFile)
                {
                    // Apply modifications to generate the replacement file
                    foreach (var modifier in modSsf.Modifiers)
                    {
                        if (modifier is CSharpKOTOR.Mods.SSF.ModifySSF modifySsf)
                        {
                            // TODO: Resolve StrRef token value
                            // For now, skip applying modifications
                            // ssf.SetSound(modifySsf.Sound, modifySsf.StringRefValue);
                        }
                    }
                }

                // Write SSF file
                var outputPath = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, modSsf.SourceFile));
                SSFAuto.WriteSsf(ssf, outputPath.FullName, ResourceType.SSF);
                generated[modSsf.SourceFile] = outputPath;
            }

            return generated;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:131-231
        // Original: def _copy_install_files(...): ...
        private Dictionary<string, FileInfo> CopyInstallFiles(
            List<InstallFile> installFiles,
            DirectoryInfo baseDataPath)
        {
            var copiedFiles = new Dictionary<string, FileInfo>();

            // Group files by folder for efficient processing
            var filesByFolder = new Dictionary<string, List<string>>();
            foreach (var installFile in installFiles)
            {
                string folder = !string.IsNullOrEmpty(installFile.Destination) && installFile.Destination != "." 
                    ? installFile.Destination 
                    : "Override";
                string filename = !string.IsNullOrEmpty(installFile.SaveAs) 
                    ? installFile.SaveAs 
                    : installFile.SourceFile;
                
                if (!filesByFolder.ContainsKey(folder))
                {
                    filesByFolder[folder] = new List<string>();
                }
                filesByFolder[folder].Add(filename);
            }

            foreach (var kvp in filesByFolder)
            {
                string folder = kvp.Key;
                var filenames = kvp.Value;
                var sourceFolder = new DirectoryInfo(Path.Combine(baseDataPath.FullName, folder));

                foreach (string filename in filenames)
                {
                    var sourceFile = new FileInfo(Path.Combine(sourceFolder.FullName, filename));
                    if (sourceFile.Exists)
                    {
                        var destFile = new FileInfo(Path.Combine(_tslpatchdataPath.FullName, filename));
                        File.Copy(sourceFile.FullName, destFile.FullName, true);
                        copiedFiles[filename] = destFile;
                    }
                }
            }

            return copiedFiles;
        }
    }
}

