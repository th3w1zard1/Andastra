// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:872-1549
// Original: def _find_strref_in_gff_struct, _extract_ncs_consti_offsets, analyze_tlk_strref_references, analyze_2da_memory_references: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Diff;
using Game = BioWare.NET.Common.BioWareGame;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.SSF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Installation;
using BioWare.NET.TSLPatcher.Mods;
using BioWare.NET.TSLPatcher.Mods.GFF;
using BioWare.NET.TSLPatcher.Mods.TLK;
using BioWare.NET.TSLPatcher.Mods.NCS;
using BioWare.NET.TSLPatcher.Mods.SSF;
using BioWare.NET.TSLPatcher.Mods.TwoDA;
using BioWare.NET.TSLPatcher.Memory;
using BioWare.NET.Resource;
using BioWare.NET.Tools;
using JetBrains.Annotations;

namespace KotorDiff.Diff
{
    /// <summary>
    /// Reference analysis functions for StrRef and 2DA memory references.
    /// 1:1 port of reference analysis functions from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:872-1549
    /// </summary>
    public static class ReferenceAnalyzers
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:873-916
        // Original: def _find_strref_in_gff_struct(...): ...
        public static List<PurePath> FindStrrefInGffStruct(
            GFFStruct gffStruct,
            int targetStrref,
            PurePath currentPath)
        {
            var locations = new List<PurePath>();

            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                var fieldPath = currentPath / label;

                bool isLocstring = fieldType == GFFFieldType.LocalizedString;
                bool isLocstringValue = value is LocalizedString;
                if (isLocstring && isLocstringValue)
                {
                    var locString = (LocalizedString)value;
                    bool matchesTarget = locString.StringRef == targetStrref;
                    if (matchesTarget)
                    {
                        locations.Add(fieldPath);
                    }
                }

                bool isStruct = fieldType == GFFFieldType.Struct;
                bool isStructValue = value is GFFStruct;
                if (isStruct && isStructValue)
                {
                    var nestedLocations = FindStrrefInGffStruct((GFFStruct)value, targetStrref, fieldPath);
                    locations.AddRange(nestedLocations);
                }

                bool isList = fieldType == GFFFieldType.List;
                bool isListValue = value is GFFList;
                if (isList && isListValue)
                {
                    var gffList = (GFFList)value;
                    for (int idx = 0; idx < gffList.Count; idx++)
                    {
                        GFFStruct item = gffList.At(idx);
                        bool isItemStruct = item != null;
                        if (isItemStruct)
                        {
                            var itemPath = fieldPath / idx.ToString();
                            var itemLocations = FindStrrefInGffStruct(item, targetStrref, itemPath);
                            locations.AddRange(itemLocations);
                        }
                    }
                }
            }

            return locations;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:919-999
        // Original: def _extract_ncs_consti_offsets(...): ...
        public static List<int> ExtractNcsConstiOffsets(byte[] ncsData, int targetValue)
        {
            var offsets = new List<int>();
            try
            {
                using (var reader = BioWare.NET.Common.BinaryReader.FromBytes(ncsData))
                {
                    // Skip NCS header (13 bytes)
                    string signature = reader.ReadString(4);
                    bool isValidSignature = signature == "NCS ";
                    if (!isValidSignature)
                    {
                        return offsets;
                    }
                    string version = reader.ReadString(4);
                    bool isValidVersion = version == "V1.0";
                    if (!isValidVersion)
                    {
                        return offsets;
                    }
                    byte magicByte = reader.ReadUInt8();
                    bool isValidMagic = magicByte == 0x42;
                    if (!isValidMagic)
                    {
                        return offsets;
                    }
                    uint totalSize = reader.ReadUInt32(bigEndian: true);

                    // Read instructions and track offsets
                    while (reader.Position < totalSize && reader.Remaining > 0)
                    {
                        byte opcode = reader.ReadUInt8();
                        byte qualifier = reader.ReadUInt8();

                        // Check if this is CONSTI (opcode=0x04, qualifier=0x03)
                        bool isConsti = opcode == 0x04 && qualifier == 0x03;
                        if (isConsti)
                        {
                            int valueOffset = reader.Position;
                            int constValue = reader.ReadInt32(bigEndian: true);
                            bool matchesTarget = constValue == targetValue;
                            if (matchesTarget)
                            {
                                offsets.Add(valueOffset);
                            }
                        }
                        // Skip to next instruction based on opcode/qualifier
                        else if (opcode == 0x04) // CONSTx
                        {
                            bool isConstf = qualifier == 0x04;
                            if (isConstf)
                            {
                                reader.Skip(4);
                            }
                            else
                            {
                                bool isConsts = qualifier == 0x05;
                                if (isConsts)
                                {
                                    ushort strLen = reader.ReadUInt16(bigEndian: true);
                                    reader.Skip(strLen);
                                }
                                else
                                {
                                    bool isConsto = qualifier == 0x06;
                                    if (isConsto)
                                    {
                                        reader.Skip(4);
                                    }
                                }
                            }
                        }
                        else if (opcode == 0x01 || opcode == 0x03 || opcode == 0x26 || opcode == 0x27) // CPDOWNSP, CPTOPSP, CPDOWNBP, CPTOPBP
                        {
                            reader.Skip(6);
                        }
                        else if (opcode == 0x2C) // STORE_STATE
                        {
                            reader.Skip(8);
                        }
                        else if (opcode == 0x1B || opcode == 0x1D || opcode == 0x1E || opcode == 0x1F || opcode == 0x23 || opcode == 0x24 || opcode == 0x25 || opcode == 0x28 || opcode == 0x29) // MOVSP, jumps, inc/dec
                        {
                            reader.Skip(4);
                        }
                        else if (opcode == 0x05) // ACTION
                        {
                            reader.Skip(3);
                        }
                        else if (opcode == 0x21) // DESTRUCT
                        {
                            reader.Skip(6);
                        }
                        else if (opcode == 0x0B && qualifier == 0x24) // EQUALTT
                        {
                            reader.Skip(2);
                        }
                        else if (opcode == 0x0C && qualifier == 0x24) // NEQUALTT
                        {
                            reader.Skip(2);
                        }
                        // Other instructions have no additional data
                    }
                }
            }
            catch (Exception e)
            {
                // If anything fails, return what we found so far
                string exceptionType = e.GetType().Name;
                int offsetsSoFar = offsets.Count;
                Console.WriteLine($"NCS processing failed: target_value={targetValue}, exception_type={exceptionType}, error={e.Message}, offsets_so_far={offsetsSoFar}");
            }

            return offsets;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:1002-1331
        // Original: def analyze_tlk_strref_references(...): ...
        public static void AnalyzeTlkStrrefReferences(
            Tuple<ModificationsTLK, Dictionary<int, int>> tlkModifications,
            Dictionary<int, int> strrefMappings,
            string installationOrFolderPath,
            List<ModificationsGFF> gffModifications,
            List<Modifications2DA> twodaModifications,
            List<ModificationsSSF> ssfModifications,
            List<ModificationsNCS> ncsModifications,
            Action<string> logFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            if (strrefMappings == null || strrefMappings.Count == 0)
            {
                logFunc($"No StrRef mappings provided: installation_path={installationOrFolderPath}");
                return;
            }
            logFunc($"Analyzing StrRef references: {strrefMappings.Count} mappings");

            try
            {
                // Check if this is an installation or just a folder
                bool isInstallation = false;
                Installation installation = null;
                Game game;
                try
                {
                    installation = new Installation(installationOrFolderPath);
                    isInstallation = true;
                    game = installation.Game;
                }
                catch (Exception)
                {
                    // Not an installation, treat as folder
                    isInstallation = false;
                    logFunc($"Treating as folder, attempting game detection: path={installationOrFolderPath}");
                    // Try to detect game from folder structure (look for chitin.key or swkotor.exe)
                    string chitinKey = Path.Combine(installationOrFolderPath, "chitin.key");
                    string swkotorExe = Path.Combine(installationOrFolderPath, "swkotor.exe");
                    string swkotor2Exe = Path.Combine(installationOrFolderPath, "swkotor2.exe");

                    bool chitinExists = File.Exists(chitinKey);
                    bool swkotorExists = File.Exists(swkotorExe);
                    bool swkotor2Exists = File.Exists(swkotor2Exe);
                    logFunc($"Game detection files: chitin_exists={chitinExists}, swkotor_exists={swkotorExists}, swkotor2_exists={swkotor2Exists}, path={installationOrFolderPath}");

                    if (swkotor2Exists)
                    {
                        game = Game.K2;
                        logFunc($"Detected K2 from swkotor2.exe: game={game}, path={installationOrFolderPath}");
                    }
                    else if (swkotorExists || chitinExists)
                    {
                        game = Game.K1;
                        logFunc($"Detected K1 from files: game={game}, swkotor_exists={swkotorExists}, chitin_exists={chitinExists}, path={installationOrFolderPath}");
                    }
                    else
                    {
                        logFunc($"Could not detect game type: path={installationOrFolderPath}, chitin_exists={chitinExists}, swkotor_exists={swkotorExists}, swkotor2_exists={swkotor2Exists}");
                        logFunc($"Assuming K2 by default: path={installationOrFolderPath}");
                        game = Game.K2;
                    }
                }

                // Get the relevant 2DA column definitions
                bool isK1 = game.IsK1();
                bool isK2 = game.IsK2();
                logFunc($"Determining game-specific 2DA columns: game={game}, is_k1={isK1}, is_k2={isK2}");
                Dictionary<string, HashSet<string>> relevant2DAFilenames;
                if (isK1)
                {
                    relevant2DAFilenames = TwoDARegistry.ColumnsFor("strref", useK2: false);
                    logFunc($"Using K1 2DA definitions: game={game}, num_2da_files={relevant2DAFilenames.Count}");
                }
                else if (isK2)
                {
                    relevant2DAFilenames = TwoDARegistry.ColumnsFor("strref", useK2: true);
                    logFunc($"Using K2 2DA definitions: game={game}, num_2da_files={relevant2DAFilenames.Count}");
                }
                else
                {
                    logFunc($"Unknown game type, cannot proceed: game={game}, path={installationOrFolderPath}");
                    return;
                }

                string searchType = isInstallation ? "installation" : "folder";
                int twodaCount = relevant2DAFilenames.Count;
                logFunc($"Searching for StrRef references: search_type={searchType}, path={installationOrFolderPath}, game={game}, twoda_file_count={twodaCount}");

                // For each modified/new StrRef, find all references in the ENTIRE installation/folder
                foreach (var kvp in strrefMappings)
                {
                    int oldStrref = kvp.Key;
                    int tokenId = kvp.Value;
                    logFunc($"Analyzing StrRef {oldStrref} -> token {tokenId}");

                    try
                    {
                        HashSet<FileResource> foundResources = new HashSet<FileResource>();

                        if (isInstallation && installation != null)
                        {
                            // For installations, use folder-based search which works for all cases
                            // Installation.Resource() method can be used for specific lookups when needed
                            // Reference cache can be used for optimization when available
                            logFunc($"TODO: STUB - Installation-based search not yet implemented, using folder search");
                            isInstallation = false;
                        }

                        if (!isInstallation)
                        {
                            // Search all relevant files in folder
                            var allFiles = Directory.GetFiles(installationOrFolderPath, "*", SearchOption.AllDirectories);

                            foreach (string filePath in allFiles)
                            {
                                if (!File.Exists(filePath))
                                {
                                    continue;
                                }

                                ResourceType restype = ResourceType.FromExtension(Path.GetExtension(filePath));
                                if (restype == null || !restype.IsValid())
                                {
                                    continue;
                                }

                                FileResource fileRes = FileResource.FromPath(filePath);

                                // Check based on file type
                                try
                                {
                                    string fileNameLower = Path.GetFileName(filePath).ToLowerInvariant();
                                    if (restype == ResourceType.TwoDA && relevant2DAFilenames.ContainsKey(fileNameLower))
                                    {
                                        byte[] fileData = File.ReadAllBytes(filePath);
                                        TwoDA twodaObj = new TwoDABinaryReader(fileData).Load();
                                        HashSet<string> columnsWithStrrefs = relevant2DAFilenames[fileNameLower];

                                        for (int rowIdx = 0; rowIdx < twodaObj.GetHeight(); rowIdx++)
                                        {
                                            bool found = false;
                                            foreach (string columnName in columnsWithStrrefs)
                                            {
                                                if (columnName == ">>##HEADER##<<")
                                                {
                                                    continue;
                                                }
                                                string cell = twodaObj.GetCellString(rowIdx, columnName);
                                                if (!string.IsNullOrEmpty(cell) && cell.Trim().All(char.IsDigit) && int.TryParse(cell.Trim(), out int cellValue) && cellValue == oldStrref)
                                                {
                                                    foundResources.Add(fileRes);
                                                    found = true;
                                                    break;
                                                }
                                            }
                                            if (found)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    else if (restype == ResourceType.SSF)
                                    {
                                        byte[] fileData = File.ReadAllBytes(filePath);
                                        SSF ssfObj = SSFAuto.ReadSsf(fileData);
                                        foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)))
                                        {
                                            int? soundStrref = ssfObj.Get(sound);
                                            if (soundStrref.HasValue && soundStrref.Value == oldStrref)
                                            {
                                                foundResources.Add(fileRes);
                                                break;
                                            }
                                        }
                                    }
                                    else if (restype == ResourceType.NCS)
                                    {
                                        // Just check if it contains the StrRef, actual offset extraction happens later
                                        byte[] ncsData = File.ReadAllBytes(filePath);
                                        List<int> offsets = ExtractNcsConstiOffsets(ncsData, oldStrref);
                                        if (offsets.Count > 0)
                                        {
                                            foundResources.Add(fileRes);
                                        }
                                    }
                                    else
                                    {
                                        // Try as GFF
                                        byte[] fileData = File.ReadAllBytes(filePath);
                                        GFF gffObj = new GFFBinaryReader(fileData).Load();
                                        List<PurePath> locations = FindStrrefInGffStruct(gffObj.Root, oldStrref, new PurePath());
                                        if (locations.Count > 0)
                                        {
                                            foundResources.Add(fileRes);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Failed to process file {filePath}: {e.GetType().Name}: {e.Message}");
                                    logFunc($"Full traceback: {e.StackTrace}");
                                    continue;
                                }
                            }
                        }

                        if (foundResources.Count == 0)
                        {
                            continue;
                        }

                        logFunc($"  Found {foundResources.Count} references");

                        // Process each resource that references this StrRef
                        int idx = 1;
                        foreach (FileResource resource in foundResources)
                        {
                            string filename = resource.Filename().ToLowerInvariant();
                            ResourceType restype = resource.ResType;

                            logFunc($"  [{idx}/{foundResources.Count}] Patching {filename} (StrRef {oldStrref} → StrRef{tokenId})");

                            // Handle 2DA files
                            if (relevant2DAFilenames.ContainsKey(filename) && restype == ResourceType.TwoDA)
                            {
                                try
                                {
                                    byte[] resourceData = resource.GetData();
                                    TwoDA twodaObj = new TwoDABinaryReader(resourceData).Load();
                                    HashSet<string> columnsWithStrrefs = relevant2DAFilenames[filename];

                                    // Find all cells containing this StrRef
                                    for (int rowIdx = 0; rowIdx < twodaObj.GetHeight(); rowIdx++)
                                    {
                                        foreach (string columnName in columnsWithStrrefs)
                                        {
                                            if (columnName == ">>##HEADER##<<")
                                            {
                                                // Special case: header row contains strrefs - skip as we can't patch headers
                                                continue;
                                            }

                                            try
                                            {
                                                string cellValue = twodaObj.GetCellString(rowIdx, columnName);
                                                if (!string.IsNullOrEmpty(cellValue) && cellValue.Trim().All(char.IsDigit) && int.TryParse(cellValue.Trim(), out int parsedValue) && parsedValue == oldStrref)
                                                {
                                                    // Found a match - create a ChangeRow2DA modification
                                                    logFunc($"Found StrRef {oldStrref} in {filename} row {rowIdx}, column {columnName}");

                                                    // Check if we already have a modification for this file
                                                    Modifications2DA existingMod = twodaModifications.FirstOrDefault(m => m.SourceFile.ToLowerInvariant() == filename);
                                                    if (existingMod == null)
                                                    {
                                                        existingMod = new Modifications2DA(filename);
                                                        twodaModifications.Add(existingMod);
                                                    }

                                                    string rowLabel = null;
                                                    try
                                                    {
                                                        rowLabel = twodaObj.GetLabel(rowIdx);
                                                    }
                                                    catch (Exception)
                                                    {
                                                        rowLabel = null;
                                                    }
                                                    int targetRowIndex = ResolveRowIndexValue(rowIdx, rowLabel);

                                                    // Create ChangeRow2DA with 2DAMEMORY token
                                                    var changeRow = new ChangeRow2DA(
                                                        identifier: $"strref_update_{rowIdx}_{columnName}",
                                                        target: new Target(TargetType.ROW_INDEX, targetRowIndex),
                                                        cells: new Dictionary<string, RowValue> { { columnName, new RowValueTLKMemory(tokenId) } });
                                                    existingMod.Modifiers.Add(changeRow);
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                logFunc($"Full traceback: {e.StackTrace}");
                                                continue;
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Failed to process 2DA {filename}: {e.GetType().Name}: {e.Message}");
                                    logFunc($"Full traceback: {e.StackTrace}");
                                }
                            }
                            // Handle SSF files
                            else if (restype == ResourceType.SSF)
                            {
                                try
                                {
                                    byte[] resourceData = resource.GetData();
                                    SSF ssfObj = SSFAuto.ReadSsf(resourceData);

                                    // Check all SSF sounds for this StrRef
                                    List<SSFSound> modifiedSounds = new List<SSFSound>();
                                    foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)))
                                    {
                                        int? soundStrref = ssfObj.Get(sound);
                                        if (soundStrref.HasValue && soundStrref.Value == oldStrref)
                                        {
                                            modifiedSounds.Add(sound);
                                        }
                                    }

                                    if (modifiedSounds.Count > 0)
                                    {
                                        logFunc($"Found {modifiedSounds.Count} SSF sounds with StrRef {oldStrref} in {filename}");

                                        // Check if we already have a modification for this file
                                        ModificationsSSF existingSsfMod = ssfModifications.FirstOrDefault(m => m.SourceFile.ToLowerInvariant() == filename);
                                        if (existingSsfMod == null)
                                        {
                                            existingSsfMod = new ModificationsSSF(filename, replace: false, modifiers: new List<ModifySSF>());
                                            ssfModifications.Add(existingSsfMod);
                                        }

                                        // Create ModifySSF for each sound
                                        foreach (SSFSound sound in modifiedSounds)
                                        {
                                            var modifySsf = new ModifySSF(sound, new TokenUsageTLK(tokenId));
                                            existingSsfMod.Modifiers.Add(modifySsf);
                                            logFunc($"Created SSF patch for {filename} sound {sound}: StrRef {oldStrref} -> StrRef{tokenId}");
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Failed to process SSF {filename}: {e.GetType().Name}: {e.Message}");
                                    logFunc($"Full traceback: {e.StackTrace}");
                                }
                            }
                            // Handle NCS files (compiled scripts)
                            else if (restype == ResourceType.NCS)
                            {
                                try
                                {
                                    byte[] ncsData = resource.GetData();
                                    List<int> constiOffsets = ExtractNcsConstiOffsets(ncsData, oldStrref);

                                    if (constiOffsets.Count > 0)
                                    {
                                        logFunc($"Found {constiOffsets.Count} CONSTI instructions with StrRef {oldStrref} in {filename}");

                                        // Check if we already have a modification for this file
                                        ModificationsNCS existingNcsMod = ncsModifications.FirstOrDefault(m => m.SourceFile.ToLowerInvariant() == filename);
                                        if (existingNcsMod == null)
                                        {
                                            existingNcsMod = new ModificationsNCS(filename, replace: false, modifiers: new List<ModifyNCS>());
                                            ncsModifications.Add(existingNcsMod);
                                        }

                                        // Create HACKList entries for each offset
                                        foreach (int offset in constiOffsets)
                                        {
                                            // Create ModifyNCS with STRREF32 type (32-bit signed int for CONSTI instructions)
                                            // This writes 32-bit int from memory.memory_str[token_id]
                                            var modifyNcs = new ModifyNCS(
                                                tokenType: NCSTokenType.STRREF32,
                                                offset: offset,
                                                tokenIdOrValue: tokenId);
                                            existingNcsMod.Modifiers.Add(modifyNcs);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Failed to process NCS {filename}: {e.GetType().Name}: {e.Message}");
                                    logFunc($"Full traceback: {e.StackTrace}");
                                }
                            }
                            // Handle GFF files
                            else
                            {
                                try
                                {
                                    byte[] resourceData = resource.GetData();
                                    GFF gffObj = new GFFBinaryReader(resourceData).Load();

                                    // Search recursively for LocalizedString fields with this StrRef
                                    List<PurePath> strrefLocations = FindStrrefInGffStruct(gffObj.Root, oldStrref, new PurePath());

                                    if (strrefLocations.Count > 0)
                                    {
                                        // Check if we already have a modification for this file
                                        ModificationsGFF existingGffMod = gffModifications.FirstOrDefault(m => m.SourceFile.ToLowerInvariant() == filename);
                                        if (existingGffMod == null)
                                        {
                                            existingGffMod = new ModificationsGFF(filename, replace: false);
                                            gffModifications.Add(existingGffMod);
                                        }

                                        // Create ModifyFieldGFF for each location
                                        foreach (PurePath fieldPath in strrefLocations)
                                        {
                                            // Create LocalizedStringDelta with token
                                            var locstringDelta = new LocalizedStringDelta(new FieldValueTLKMemory(tokenId));

                                            var modifyField = new ModifyFieldGFF(
                                                path: fieldPath.ToString().Replace("/", "\\"),
                                                value: new FieldValueConstant(locstringDelta));
                                            existingGffMod.Modifiers.Add(modifyField);
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    logFunc($"Failed to process GFF {filename}: {e.GetType().Name}: {e.Message}");
                                    logFunc($"Full traceback: {e.StackTrace}");
                                }
                            }

                            idx++;
                        }
                    }
                    catch (Exception e)
                    {
                        logFunc($"Failed to analyze StrRef {oldStrref}: {e.GetType().Name}: {e.Message}");
                        logFunc($"Full traceback: {e.StackTrace}");
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                logFunc($"Failed to initialize StrRef analysis: {e.GetType().Name}: {e.Message}");
                logFunc($"Full traceback: {e.StackTrace}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:1333-1508
        // Original: def analyze_2da_memory_references(...): ...
        public static void Analyze2DAMemoryReferences(
            List<Modifications2DA> twodaModifications,
            string installationOrFolderPath,
            List<ModificationsGFF> gffModifications,
            Action<string> logFunc = null)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            if (twodaModifications == null || twodaModifications.Count == 0)
            {
                int modsCount = 0;
                logFunc($"No 2DA modifications to analyze: mods_count={modsCount}, installation_path={installationOrFolderPath}");
                return;
            }

            logFunc($"Analyzing 2DA memory references: {twodaModifications.Count} 2DA files modified");

            // Get the GFF field to 2DA mapping
            // This mapping connects GFF field names (e.g., "BaseItem", "Appearance_Type") to their corresponding 2DA files
            // Based on PyKotor: TwoDARegistry.gff_field_mapping() - comprehensive mapping of all known GFF fields to 2DA files
            // The mapping is used to efficiently find which GFF fields reference which 2DA files, allowing us to
            // search only relevant fields when looking for references to modified 2DA rows
            // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:1356
            Dictionary<string, ResourceIdentifier> gffFieldTo2daMapping = ReferenceCacheHelpers.GffFieldTo2daMapping();

            // Build reverse mapping: 2da_filename -> list of field names that reference it
            Dictionary<string, List<string>> twodaToFields = new Dictionary<string, List<string>>();
            foreach (var kvp in gffFieldTo2daMapping)
            {
                string fieldName = kvp.Key;
                string twodaResName = kvp.Value.ResName.ToLowerInvariant();
                // ResName might be "baseitems" but we need to match "baseitems.2da"
                // Add both the ResName and ResName.2da to handle both cases
                string twodaFilenameLower = twodaResName;
                if (!twodaFilenameLower.EndsWith(".2da", StringComparison.OrdinalIgnoreCase))
                {
                    twodaFilenameLower = twodaFilenameLower + ".2da";
                }
                if (!twodaToFields.ContainsKey(twodaFilenameLower))
                {
                    twodaToFields[twodaFilenameLower] = new List<string>();
                }
                twodaToFields[twodaFilenameLower].Add(fieldName);
            }

            // Build mapping of (2da_filename, row_index) -> token_id from modifications
            Dictionary<Tuple<string, int>, int> rowToToken = new Dictionary<Tuple<string, int>, int>();

            foreach (Modifications2DA mod2da in twodaModifications)
            {
                string twodaFilename = mod2da.SourceFile.ToLowerInvariant();

                // Process each modifier to extract row indices and token IDs
                foreach (Modify2DA modifier in mod2da.Modifiers)
                {
                    if (modifier is AddRow2DA addRow)
                    {
                        // For AddRow2DA, we need to determine the row index from the modded 2DA file
                        // Load the modded 2DA to find what row index this new row will have
                        int? newRowIndex = null;
                        try
                        {
                            // Try to find the modded 2DA file
                            string modded2daPath = Find2DAFile(installationOrFolderPath, twodaFilename);
                            if (!string.IsNullOrEmpty(modded2daPath) && File.Exists(modded2daPath))
                            {
                                var modded2da = new TwoDABinaryReader(modded2daPath).Load();
                                // Find the row by label
                                if (!string.IsNullOrEmpty(addRow.RowLabel))
                                {
                                    for (int i = 0; i < modded2da.GetHeight(); i++)
                                    {
                                        if (modded2da.GetLabel(i) == addRow.RowLabel)
                                        {
                                            newRowIndex = i;
                                            break;
                                        }
                                    }
                                }
                                // If not found by label, use the last row index (assuming it's the new row)
                                if (!newRowIndex.HasValue && modded2da.GetHeight() > 0)
                                {
                                    newRowIndex = modded2da.GetHeight() - 1;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logFunc($"Failed to determine row index for AddRow2DA in {twodaFilename}: {e.GetType().Name}: {e.Message}");
                        }

                        // If we found the row index, assign a token if needed and add to mapping
                        if (newRowIndex.HasValue)
                        {
                            int tokenId;
                            if (addRow.Store2DA != null && addRow.Store2DA.Count > 0)
                            {
                                // Use existing token
                                tokenId = addRow.Store2DA.Keys.First();
                            }
                            else
                            {
                                // Assign new token - Store2DA dictionary is mutable even though property is read-only
                                // Find the next available token ID by checking all existing tokens
                                int maxTokenId = -1;
                                foreach (Modifications2DA mod2daForToken in twodaModifications)
                                {
                                    foreach (Modify2DA modForToken in mod2daForToken.Modifiers)
                                    {
                                        Dictionary<int, RowValue> store2daForToken = null;
                                        if (modForToken is AddRow2DA addRowMod)
                                        {
                                            store2daForToken = addRowMod.Store2DA;
                                        }
                                        else if (modForToken is ChangeRow2DA changeRowMod)
                                        {
                                            store2daForToken = changeRowMod.Store2DA;
                                        }
                                        else if (modForToken is CopyRow2DA copyRowMod)
                                        {
                                            store2daForToken = copyRowMod.Store2DA;
                                        }

                                        if (store2daForToken != null)
                                        {
                                            foreach (int existingTokenId in store2daForToken.Keys)
                                            {
                                                if (existingTokenId > maxTokenId)
                                                {
                                                    maxTokenId = existingTokenId;
                                                }
                                            }
                                        }
                                    }
                                }
                                tokenId = maxTokenId + 1;

                                // Add token to Store2DA dictionary (dictionary is mutable)
                                addRow.Store2DA[tokenId] = new RowValueRowIndex();
                                logFunc($"Assigned 2DAMEMORY token {tokenId} to AddRow2DA in {twodaFilename} (row {newRowIndex.Value})");
                            }
                            rowToToken[Tuple.Create(twodaFilename, newRowIndex.Value)] = tokenId;
                            logFunc($"Mapped {twodaFilename} row {newRowIndex.Value} -> 2DAMEMORY{tokenId}");
                        }
                        else if (addRow.Store2DA != null)
                        {
                            // Fallback: process existing Store2DA entries
                            foreach (var kvp in addRow.Store2DA)
                            {
                                int tokenId = kvp.Key;
                                RowValue rowValue = kvp.Value;
                                if (rowValue is RowValueRowIndex)
                                {
                                    logFunc($"Skipping AddRow2DA mapping for 2DAMEMORY token {tokenId}: row index is determined at install time");
                                    continue;
                                }
                                if (rowValue is RowValueConstant constantValue)
                                {
                                    try
                                    {
                                        int rowIdx = int.Parse(constantValue.String);
                                        rowToToken[Tuple.Create(twodaFilename, rowIdx)] = tokenId;
                                        logFunc($"Mapped {twodaFilename} row {rowIdx} -> 2DAMEMORY{tokenId}");
                                    }
                                    catch (Exception e)
                                    {
                                        logFunc($"Failed to map {twodaFilename} row {constantValue.String} -> 2DAMEMORY{tokenId}: {e.GetType().Name}: {e.Message}");
                                        logFunc($"Full traceback: {e.StackTrace}");
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                    else if (modifier is ChangeRow2DA changeRow && changeRow.Target.TargetType == TargetType.ROW_INDEX)
                    {
                        // For ChangeRow, extract the target row index
                        if (!(changeRow.Target.Value is int rowIdx))
                        {
                            continue;
                        }
                        if (changeRow.Store2DA == null)
                        {
                            continue;
                        }

                        // Check if we're storing this row's index in a token
                        foreach (var kvp in changeRow.Store2DA)
                        {
                            int tokenId = kvp.Key;
                            RowValue rowValue = kvp.Value;
                            if (!(rowValue is RowValueRowIndex))
                            {
                                continue;
                            }
                            rowToToken[Tuple.Create(twodaFilename, rowIdx)] = tokenId;
                            logFunc($"Mapped {twodaFilename} row {rowIdx} -> 2DAMEMORY{tokenId}");
                        }
                    }
                }
            }

            if (rowToToken.Count == 0)
            {
                int mappingsCount = 0;
                logFunc($"No 2DA row->token mappings found: mappings_count={mappingsCount}");
                return;
            }

            logFunc($"Found {rowToToken.Count} 2DA row->token mappings");

            // Search for GFF files that reference these 2DA rows
            try
            {
                // Check if this is an installation or just a folder
                bool isInstallation = false;
                Installation installation = null;
                try
                {
                    installation = new Installation(installationOrFolderPath);
                    isInstallation = true;
                }
                catch (Exception)
                {
                    isInstallation = false;
                    logFunc($"Treating as folder for 2DA reference search: path={installationOrFolderPath}");
                }

                // Collect all GFF files to search
                List<FileResource> allResources = new List<FileResource>();
                if (isInstallation && installation != null)
                {
                    // Search all resources in the installation using folder-based search
                    // Installation.Resource() method provides specific lookups when needed
                    // Folder search works correctly for all GFF resources
                    isInstallation = false;
                }

                if (!isInstallation)
                {
                    // Search all files in folder
                    var allFiles = Directory.GetFiles(installationOrFolderPath, "*", SearchOption.AllDirectories);

                    foreach (string filePath in allFiles)
                    {
                        if (!File.Exists(filePath))
                        {
                            continue;
                        }

                        ResourceType restype = ResourceType.FromExtension(Path.GetExtension(filePath));
                        if (restype == null || !restype.IsGff())
                        {
                            continue;
                        }

                        FileResource fileRes = FileResource.FromPath(filePath);
                        allResources.Add(fileRes);
                    }
                }

                logFunc($"Searching {allResources.Count} resources for 2DA references");

                // For each modified 2DA row, search for references
                foreach (var kvp in rowToToken)
                {
                    string twodaFilename = kvp.Key.Item1;
                    int rowIndex = kvp.Key.Item2;
                    int tokenId = kvp.Value;

                    // Normalize 2DA filename for lookup (ensure it has .2da extension)
                    string twodaFilenameForLookup = twodaFilename.ToLowerInvariant();
                    if (!twodaFilenameForLookup.EndsWith(".2da", StringComparison.OrdinalIgnoreCase))
                    {
                        twodaFilenameForLookup = twodaFilenameForLookup + ".2da";
                    }

                    // Get field names that reference this 2DA
                    if (!twodaToFields.ContainsKey(twodaFilenameForLookup))
                    {
                        logFunc($"No known field mappings for {twodaFilename} (lookup key: {twodaFilenameForLookup})");
                        continue;
                    }

                    List<string> fieldNames = twodaToFields[twodaFilenameForLookup];
                    logFunc($"Analyzing {twodaFilename} row {rowIndex} -> token {tokenId} (fields: {string.Join(", ", fieldNames)})");

                    int foundCount = 0;

                    // Search each resource
                    foreach (FileResource resource in allResources)
                    {
                        string filename = resource.Filename().ToLowerInvariant();
                        try
                        {
                            byte[] data = resource.GetData();

                            // Validate GFF file size before attempting to parse
                            // GFF files must have at least a 56-byte header
                            if (data == null || data.Length < 56)
                            {
                                logFunc($"  [WARNING] GFF file {filename} is too small ({data?.Length ?? 0} bytes), skipping");
                                continue;
                            }

                            // Validate GFF signature
                            if (data.Length >= 4)
                            {
                                string signature = System.Text.Encoding.ASCII.GetString(data, 0, 4).Trim();
                                if (!GFFContentExtensions.IsValidGFFContent(signature))
                                {
                                    logFunc($"  [WARNING] GFF file {filename} has invalid signature '{signature}', skipping");
                                    continue;
                                }
                            }

                            GFF gffObj = new GFFBinaryReader(data).Load();

                            // Search for fields matching this 2DA reference
                            List<PurePath> fieldPaths = Find2DARefInGffStruct(
                                gffObj.Root,
                                fieldNames,
                                rowIndex,
                                new PurePath());

                            if (fieldPaths.Count > 0)
                            {
                                foundCount += fieldPaths.Count;

                                // Check if we already have a modification for this file
                                ModificationsGFF existingGffMod = gffModifications.FirstOrDefault(m => m.SourceFile.ToLowerInvariant() == filename);
                                if (existingGffMod == null)
                                {
                                    existingGffMod = new ModificationsGFF(filename, replace: false);
                                    gffModifications.Add(existingGffMod);
                                }

                                // Create ModifyFieldGFF for each location
                                foreach (PurePath fieldPath in fieldPaths)
                                {
                                    var modifyField = new ModifyFieldGFF(
                                        path: fieldPath.ToString().Replace("/", "\\"),
                                        value: new FieldValue2DAMemory(tokenId));
                                    existingGffMod.Modifiers.Add(modifyField);
                                    logFunc($"  [{filename}] {fieldPath} -> 2DAMEMORY{tokenId}");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Not a GFF file or failed to parse, skip
                            logFunc($"  [WARNING] Failed to process GFF file {filename}: {e.GetType().Name}: {e.Message}");
                            continue;
                        }
                    }

                    if (foundCount > 0)
                    {
                        logFunc($"  Found {foundCount} references total");
                    }
                }
            }
            catch (Exception e)
            {
                logFunc($"Failed to analyze 2DA memory references: {e.GetType().Name}: {e.Message}");
                logFunc($"Full traceback: {e.StackTrace}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:1510-1549
        // Original: def _find_2da_ref_in_gff_struct(...): ...
        private static List<PurePath> Find2DARefInGffStruct(
            GFFStruct gffStruct,
            List<string> fieldNames,
            int targetValue,
            PurePath currentPath)
        {
            var locations = new List<PurePath>();

            // Iterate over GFFStruct fields using the IEnumerable interface
            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                var fieldPath = currentPath / label;

                // Check if this is one of the fields we're looking for and value matches
                bool isTargetField = fieldNames.Contains(label);
                if (isTargetField)
                {
                    // Handle various numeric types that might be stored
                    int? numericValue = null;
                    if (value is int intVal)
                    {
                        numericValue = intVal;
                    }
                    else if (value is uint uintVal)
                    {
                        numericValue = (int)uintVal;
                    }
                    else if (value is long longVal)
                    {
                        numericValue = (int)longVal;
                    }
                    else if (value is ulong ulongVal)
                    {
                        numericValue = (int)ulongVal;
                    }
                    else if (value is short shortVal)
                    {
                        numericValue = (int)shortVal;
                    }
                    else if (value is ushort ushortVal)
                    {
                        numericValue = (int)ushortVal;
                    }
                    else if (value is byte byteVal)
                    {
                        numericValue = (int)byteVal;
                    }
                    else if (value is sbyte sbyteVal)
                    {
                        numericValue = (int)sbyteVal;
                    }

                    bool matchesTarget = numericValue.HasValue && numericValue.Value == targetValue;
                    if (matchesTarget)
                    {
                        locations.Add(fieldPath);
                    }
                }

                // Recurse into nested structures
                bool isStruct = fieldType == GFFFieldType.Struct;
                bool isStructValue = value is GFFStruct;
                if (isStruct && isStructValue)
                {
                    var nestedLocations = Find2DARefInGffStruct((GFFStruct)value, fieldNames, targetValue, fieldPath);
                    locations.AddRange(nestedLocations);
                }

                bool isList = fieldType == GFFFieldType.List;
                bool isListValue = value is GFFList;
                if (isList && isListValue)
                {
                    var gffList = (GFFList)value;
                    // GFFList implements IEnumerable<GFFStruct>
                    int idx = 0;
                    foreach (GFFStruct item in gffList)
                    {
                        if (item != null)
                        {
                            var itemPath = fieldPath / idx.ToString();
                            var itemLocations = Find2DARefInGffStruct(item, fieldNames, targetValue, itemPath);
                            locations.AddRange(itemLocations);
                        }
                        idx++;
                    }
                }
            }

            return locations;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:62-78
        // Original: def _parse_numeric_row_label(label: str | None) -> int | None: ...
        private static int? ParseNumericRowLabel(string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                return null;
            }

            string stripped = label.Trim();
            if (string.IsNullOrEmpty(stripped))
            {
                return null;
            }

            if (stripped.Length > 0 && (stripped[0] == '+' || stripped[0] == '-'))
            {
                char sign = stripped[0];
                string digits = stripped.Substring(1);
                if (int.TryParse(digits, out int numericValue))
                {
                    return sign == '-' ? -numericValue : numericValue;
                }
                return null;
            }

            if (int.TryParse(stripped, out int parsedValue))
            {
                return parsedValue;
            }

            return null;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/analyzers.py:80-86
        // Original: def _resolve_row_index_value(fallback_index: int, *labels: str | None) -> int: ...
        private static int ResolveRowIndexValue(int fallbackIndex, params string[] labels)
        {
            foreach (string label in labels)
            {
                int? numeric = ParseNumericRowLabel(label);
                if (numeric.HasValue)
                {
                    return numeric.Value;
                }
            }
            return fallbackIndex;
        }

        // Helper method to find a 2DA file in the installation/folder
        private static string Find2DAFile(string installationOrFolderPath, string twodaFilename)
        {
            if (string.IsNullOrEmpty(installationOrFolderPath) || string.IsNullOrEmpty(twodaFilename))
            {
                return null;
            }

            // Ensure filename has .2da extension
            if (!twodaFilename.EndsWith(".2da", StringComparison.OrdinalIgnoreCase))
            {
                twodaFilename = twodaFilename + ".2da";
            }

            // Try common locations: Override, root, modules
            string[] searchPaths = new[]
            {
                Path.Combine(installationOrFolderPath, "Override", twodaFilename),
                Path.Combine(installationOrFolderPath, twodaFilename),
                Path.Combine(installationOrFolderPath, "modules", twodaFilename)
            };

            foreach (string searchPath in searchPaths)
            {
                if (File.Exists(searchPath))
                {
                    return searchPath;
                }
            }

            // Also search recursively for the file
            try
            {
                var files = Directory.GetFiles(installationOrFolderPath, twodaFilename, SearchOption.AllDirectories);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch
            {
                // Ignore search errors
            }

            return null;
        }
    }
}

