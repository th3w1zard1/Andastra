using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py
    // Original: Batch patching utilities for KOTOR resources
    public class PatchingConfig
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:72-94
        // Original: class PatchingConfig
        public bool Translate { get; set; } = false;
        public bool SetUnskippable { get; set; } = false;
        public string ConvertTga { get; set; } = null; // "TGA to TPC", "TPC to TGA", or null
        public bool K1ConvertGffs { get; set; } = false;
        public bool TslConvertGffs { get; set; } = false;
        public bool AlwaysBackup { get; set; } = true;
        public int MaxThreads { get; set; } = 2;
        public object Translator { get; set; } = null; // Translator instance
        public Action<string> LogCallback { get; set; } = null;

        public bool IsPatching()
        {
            return Translate || SetUnskippable || !string.IsNullOrEmpty(ConvertTga) || K1ConvertGffs || TslConvertGffs;
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:97-100
    // Original: def log_message(config: PatchingConfig, message: str) -> None:
    public static class Patching
    {
        private static void LogMessage(PatchingConfig config, string message)
        {
            config?.LogCallback?.Invoke(message);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:103-167
        // Original: def patch_nested_gff(...)
        public static Tuple<bool, int> PatchNestedGff(
            GFFStruct gffStruct,
            GFFContent gffContent,
            GFF gff,
            PatchingConfig config,
            string currentPath = null,
            bool madeChange = false,
            int alienVoCount = -1)
        {
            if (gffContent != GFFContent.DLG && !config.Translate)
            {
                return Tuple.Create(false, alienVoCount);
            }

            if (gffContent == GFFContent.DLG && config.SetUnskippable)
            {
                object soundRaw = gffStruct.Acquire<object>("Sound", null);
                ResRef sound = soundRaw as ResRef;
                string soundStr = sound == null ? "" : sound.ToString().Trim().ToLowerInvariant();
                if (sound != null && !string.IsNullOrWhiteSpace(soundStr) && AlienSounds.All.Contains(soundStr))
                {
                    alienVoCount++;
                }
            }

            currentPath = currentPath ?? "GFFRoot";
            foreach ((string label, GFFFieldType ftype, object value) in gffStruct)
            {
                if (label.ToLowerInvariant() == "mod_name")
                {
                    continue;
                }
                string childPath = Path.Combine(currentPath, label);

                if (ftype == GFFFieldType.Struct)
                {
                    if (!(value is GFFStruct structValue))
                    {
                        throw new InvalidOperationException($"Not a GFFStruct instance: {value?.GetType().Name ?? "null"}: {value}");
                    }
                    var result = PatchNestedGff(structValue, gffContent, gff, config, childPath, madeChange, alienVoCount);
                    madeChange |= result.Item1;
                    alienVoCount = result.Item2;
                    continue;
                }

                if (ftype == GFFFieldType.List)
                {
                    if (!(value is GFFList listValue))
                    {
                        throw new InvalidOperationException($"Not a GFFList instance: {value?.GetType().Name ?? "null"}: {value}");
                    }
                    var result = RecurseThroughList(listValue, gffContent, gff, config, childPath, madeChange, alienVoCount);
                    madeChange |= result.Item1;
                    alienVoCount = result.Item2;
                    continue;
                }

                if (ftype == GFFFieldType.LocalizedString && config.Translate)
                {
                    if (!(value is LocalizedString locString))
                    {
                        throw new InvalidOperationException($"{value?.GetType().Name ?? "null"}: {value}");
                    }
                    LogMessage(config, $"Translating CExoLocString at {childPath} to {(config.Translator != null ? "unknown" : "unknown")}");
                    madeChange |= TranslateLocstring(locString, config);
                }
            }
            return Tuple.Create(madeChange, alienVoCount);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:170-201
        // Original: def recurse_through_list(...)
        public static Tuple<bool, int> RecurseThroughList(
            GFFList gffList,
            GFFContent gffContent,
            GFF gff,
            PatchingConfig config,
            string currentPath = null,
            bool madeChange = false,
            int alienVoCount = -1)
        {
            currentPath = currentPath ?? "GFFListRoot";
            int listIndex = 0;
            foreach (GFFStruct gffStruct in gffList)
            {
                var result = PatchNestedGff(gffStruct, gffContent, gff, config, Path.Combine(currentPath, listIndex.ToString()), madeChange, alienVoCount);
                madeChange |= result.Item1;
                alienVoCount = result.Item2;
                listIndex++;
            }
            return Tuple.Create(madeChange, alienVoCount);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:204-229
        // Original: def translate_locstring(locstring: LocalizedString, config: PatchingConfig) -> bool:
        public static bool TranslateLocstring(LocalizedString locstring, PatchingConfig config)
        {
            if (config.Translator == null)
            {
                return false;
            }

            bool madeChange = false;
            var newSubstrings = new Dictionary<int, string>();
            foreach ((Language lang, Gender gender, string text) in locstring)
            {
                if (text != null && !string.IsNullOrWhiteSpace(text))
                {
                    int substringId = LocalizedString.SubstringId(lang, gender);
                    newSubstrings[substringId] = text;
                }
            }

            foreach ((Language lang, Gender gender, string text) in locstring)
            {
                if (text != null && !string.IsNullOrWhiteSpace(text))
                {
                    // Translator interface would need to be defined
                    // string translatedText = config.Translator.Translate(text, fromLang: lang);
                    // LogMessage(config, $"Translated {text} --> {translatedText}");
                    // int substringId = LocalizedString.SubstringId(config.Translator.ToLang, gender);
                    // newSubstrings[substringId] = translatedText;
                    // madeChange = true;
                }
            }
            foreach (var kvp in newSubstrings)
            {
                LocalizedString.SubstringPair(kvp.Key, out Language lang, out Gender gender);
                locstring.SetData(lang, gender, kvp.Value);
            }
            return madeChange;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:232-244
        // Original: def fix_encoding(text: str, encoding: str) -> str:
        public static string FixEncoding(string text, string encoding)
        {
            try
            {
                var enc = System.Text.Encoding.GetEncoding(encoding);
                byte[] bytes = enc.GetBytes(text);
                return enc.GetString(bytes).Trim();
            }
            catch
            {
                return text.Trim();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:247-356
        // Original: def convert_gff_game(...)
        public static void ConvertGffGame(Game fromGame, FileResource resource, PatchingConfig config)
        {
            Game toGame = fromGame.IsK1() ? Game.K2 : Game.K1;
            string newName = resource.Filename();
            object convertedData = new byte[0];
            string savepath = null;
            if (!resource.InsideCapsule)
            {
                newName = config.AlwaysBackup
                    ? $"{resource.ResName}_{toGame}.{resource.ResType.Extension}"
                    : resource.Filename();
                savepath = Path.Combine(Path.GetDirectoryName(resource.FilePath), newName);
                convertedData = savepath;
            }
            else
            {
                savepath = resource.FilePath;
            }

            LogMessage(config, $"Converting {Path.GetDirectoryName(resource.PathIdent())}/{Path.GetFileName(resource.PathIdent())} to {toGame}");
            try
            {
                // Generic resource conversion - these functions need to be ported
                // TODO: STUB - For now, log that conversion is needed
                LogMessage(config, $"GFF conversion for {resource.ResType.Name} not yet fully implemented - requires generic resource read/write functions");
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException)
            {
                LogMessage(config, $"Corrupted GFF: '{resource.PathIdent()}', skipping...");
                if (!resource.InsideCapsule)
                {
                    return;
                }
                LogMessage(config, $"Corrupted GFF: '{resource.PathIdent()}', will start validation process of '{Path.GetFileName(resource.FilePath)}'...");
                object newErfRim = Salvage.ValidateCapsule(resource.FilePath, strict: true, game: toGame);
                if (newErfRim is ERF newErf)
                {
                    LogMessage(config, $"Saving salvaged ERF to '{savepath}'");
                    ERFAuto.WriteErf(newErf, savepath, ResourceType.ERF);
                    return;
                }
                if (newErfRim is RIM newRim)
                {
                    LogMessage(config, $"Saving salvaged RIM to '{savepath}'");
                    RIMAuto.WriteRim(newRim, savepath, ResourceType.RIM);
                    return;
                }
                LogMessage(config, $"Whole erf/rim is corrupt: {resource}");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:359-399
        // Original: def process_translations(tlk: TLK, from_lang: Language, config: PatchingConfig) -> None:
        public static void ProcessTranslations(TLK tlk, Language fromLang, PatchingConfig config)
        {
            if (config.Translator == null)
            {
                return;
            }

            // Translator interface would need to be defined
            // TODO: STUB - For now, log that translation is needed
            LogMessage(config, "Translation processing not fully implemented - requires translator instance");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:402-508
        // Original: def patch_resource(...)
        public static object PatchResource(FileResource resource, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (processedFiles == null)
            {
                processedFiles = new HashSet<string>();
            }

            // Handle TLK translation
            if (resource.ResType.Extension.ToLowerInvariant() == "tlk" && config.Translate && config.Translator != null)
            {
                TLK tlk = null;
                LogMessage(config, $"Loading TLK '{resource.FilePath}'");
                try
                {
                    tlk = TLKAuto.ReadTlk(resource.GetData());
                }
                catch
                {
                    LogMessage(config, $"[Error] loading TLK '{resource.Identifier}' at '{resource.FilePath}'!");
                    return null;
                }

                Language fromLang = tlk.Language;
                // Translator interface would need to be defined
                // string newFilenameStem = $"{resource.Resname()}_{config.Translator.ToLang.GetBcp47Code() ?? "UNKNOWN"}";
                // string newFilePath = Path.Combine(Path.GetDirectoryName(resource.FilePath), $"{newFilenameStem}.{resource.Restype().Extension}");
                // tlk.Language = config.Translator.ToLang;
                // LogMessage(config, $"Translating TalkTable resource at {resource.FilePath} to {config.Translator.ToLang.Name}");
                // ProcessTranslations(tlk, fromLang, config);
                // TLKAuto.WriteTlk(tlk, newFilePath, ResourceType.TLK);
                // processedFiles.Add(newFilePath);
            }

            // Handle TGA to TPC conversion
            if (resource.ResType.Extension.ToLowerInvariant() == "tga" && config.ConvertTga == "TGA to TPC")
            {
                LogMessage(config, $"Converting TGA at {resource.PathIdent()} to TPC...");
                try
                {
                    return TPCAuto.ReadTpc(resource.GetData());
                }
                catch
                {
                    LogMessage(config, $"[Error] loading TGA '{resource.Identifier}' at '{resource.FilePath}'!");
                    return null;
                }
            }

            // Handle TPC to TGA conversion
            if (resource.ResType.Extension.ToLowerInvariant() == "tpc" && config.ConvertTga == "TPC to TGA")
            {
                LogMessage(config, $"Converting TPC at {resource.PathIdent()} to TGA...");
                try
                {
                    return TPCAuto.ReadTpc(resource.GetData());
                }
                catch
                {
                    LogMessage(config, $"[Error] loading TPC '{resource.Identifier}' at '{resource.FilePath}'!");
                    return null;
                }
            }

            // Handle GFF files
            if (Enum.GetNames(typeof(GFFContent)).Contains(resource.ResType.Name.ToUpperInvariant()))
            {
                if (config.K1ConvertGffs && !resource.InsideCapsule)
                {
                    ConvertGffGame(Game.K2, resource, config);
                }
                if (config.TslConvertGffs && !resource.InsideCapsule)
                {
                    ConvertGffGame(Game.K1, resource, config);
                }

                GFF gff = null;
                try
                {
                    var reader = new GFFBinaryReader(resource.GetData());
                    gff = reader.Load();
                    string alienOwner = null;
                    if (gff.Content == GFFContent.DLG && config.SetUnskippable)
                    {
                        object skippable = gff.Root.Acquire<object>("Skippable", null);
                        if (!Equals(skippable, 0) && !Equals(skippable, "0"))
                        {
                            object conversationtype = gff.Root.Acquire<object>("ConversationType", null);
                            if (!Equals(conversationtype, "1") && !Equals(conversationtype, 1))
                            {
                                alienOwner = gff.Root.Acquire<string>("AlienRaceOwner", null); // TSL only
                            }
                        }
                    }

                    var result = PatchNestedGff(
                        gff.Root,
                        gff.Content,
                        gff,
                        config,
                        resource.PathIdent().ToString()
                    );

                    bool madeChange = result.Item1;
                    int alienVoCount = result.Item2;

                    if (config.SetUnskippable
                        && (alienOwner == null || alienOwner == "0" || alienOwner == "0")
                        && alienVoCount != -1
                        && alienVoCount < 3
                        && gff.Content == GFFContent.DLG)
                    {
                        object skippable = gff.Root.Acquire<object>("Skippable", null);
                        if (!Equals(skippable, 0) && !Equals(skippable, "0"))
                        {
                            object conversationtype = gff.Root.Acquire<object>("ConversationType", null);
                            if (!Equals(conversationtype, "1") && !Equals(conversationtype, 1))
                            {
                                LogMessage(config, $"Setting dialog {resource.PathIdent()} as unskippable");
                                madeChange = true;
                                gff.Root.SetUInt8("Skippable", 0);
                            }
                        }
                    }

                    if (madeChange)
                    {
                        return gff;
                    }
                }
                catch (Exception e)
                {
                    LogMessage(config, $"[Error] cannot load corrupted GFF '{resource.PathIdent()}'!");
                    if (!(e is IOException || e is ArgumentException))
                    {
                        LogMessage(config, $"[Error] loading GFF '{resource.PathIdent()}'!");
                    }
                    return null;
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:511-568
        // Original: def patch_and_save_noncapsule(...)
        public static void PatchAndSaveNoncapsule(FileResource resource, PatchingConfig config, string savedir = null)
        {
            object patchedData = PatchResource(resource, config);
            if (patchedData == null)
            {
                return;
            }

            Capsule capsule = resource.InsideCapsule ? new Capsule(resource.FilePath) : null;

            if (patchedData is GFF gff)
            {
                byte[] newData = GFFAuto.BytesGff(gff, ResourceType.GFF);

                string newGffFilename = resource.Filename();
                if (config.Translate && config.Translator != null)
                {
                    // Translator interface would need to be defined
                    // newGffFilename = $"{resource.ResName}_{config.Translator.ToLang.GetBcp47Code()}.{resource.ResType.Extension}";
                }

                string newPath = savedir != null
                    ? Path.Combine(savedir, newGffFilename)
                    : Path.Combine(Path.GetDirectoryName(resource.FilePath), newGffFilename);
                if (File.Exists(newPath) && savedir != null)
                {
                    LogMessage(config, $"Skipping '{newGffFilename}', already exists on disk");
                }
                else
                {
                    LogMessage(config, $"Saving patched gff to '{newPath}'");
                    File.WriteAllBytes(newPath, newData);
                }
            }
            else if (patchedData is TPC tpc)
            {
                if (capsule == null)
                {
                    string txiFile = Path.ChangeExtension(resource.FilePath, ".txi");
                    if (File.Exists(txiFile))
                    {
                        LogMessage(config, "Embedding TXI information...");
                        byte[] data = File.ReadAllBytes(txiFile);
                        string txiText = Encoding.DecodeBytesWithFallbacks(data);
                        tpc.Txi = txiText;
                    }
                }
                else
                {
                    byte[] txiData = capsule.GetResource(resource.ResName, ResourceType.TXI);
                    if (txiData != null)
                    {
                        LogMessage(config, "Embedding TXI information from resource found in capsule...");
                        string txiText = Encoding.DecodeBytesWithFallbacks(txiData);
                        tpc.Txi = txiText;
                    }
                }

                string newPath = savedir != null
                    ? Path.Combine(savedir, resource.ResName)
                    : Path.Combine(Path.GetDirectoryName(resource.FilePath), resource.ResName);
                if (config.ConvertTga == "TGA to TPC")
                {
                    newPath = Path.ChangeExtension(newPath, ".tpc");
                    TPCAuto.WriteTpc(tpc, newPath, ResourceType.TPC);
                }
                else
                {
                    newPath = Path.ChangeExtension(newPath, ".tga");
                    TPCAuto.WriteTpc(tpc, newPath, ResourceType.TGA);
                }

                if (File.Exists(newPath))
                {
                    LogMessage(config, $"Skipping '{newPath}', already exists on disk");
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:571-632
        // Original: def patch_capsule_file(...)
        public static void PatchCapsuleFile(string cFile, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (processedFiles == null)
            {
                processedFiles = new HashSet<string>();
            }

            LogMessage(config, $"Load {Path.GetFileName(cFile)}");
            Capsule fileCapsule;
            try
            {
                fileCapsule = new Capsule(cFile);
            }
            catch (ArgumentException e)
            {
                LogMessage(config, $"Could not load '{cFile}'. Reason: {e.Message}");
                return;
            }

            string newFilepath = cFile;
            if (config.Translate && config.Translator != null)
            {
                // Translator interface would need to be defined
                // string stem = Path.GetFileNameWithoutExtension(cFile);
                // string ext = Path.GetExtension(cFile);
                // newFilepath = Path.Combine(Path.GetDirectoryName(cFile), $"{stem}_{config.Translator.ToLang.GetBcp47Code()}{ext}");
            }

            var newResources = new List<Tuple<string, ResourceType, byte[]>>();
            var omittedResources = new HashSet<string>();
            foreach (var resource in fileCapsule)
            {
                if (config.IsPatching())
                {
                    object patchedData = PatchResource(new FileResource(resource.ResName, resource.ResType, resource.Data.Length, 0, resource.FilePath), config, processedFiles);
                    if (patchedData is GFF gff)
                    {
                        byte[] newData = patchedData != null ? GFFAuto.BytesGff(gff, ResourceType.GFF) : resource.Data;
                        LogMessage(config, $"Adding patched GFF resource '{resource.ResName}.{resource.ResType.Extension}' to capsule {Path.GetFileName(newFilepath)}");
                        newResources.Add(Tuple.Create(resource.ResName, resource.ResType, newData));
                        omittedResources.Add($"{resource.ResName}.{resource.ResType.Extension}");
                    }
                    else if (patchedData is TPC tpc)
                    {
                        byte[] txiResource = fileCapsule.GetResource(resource.ResName, ResourceType.TXI);
                        if (txiResource != null)
                        {
                            tpc.Txi = System.Text.Encoding.ASCII.GetString(txiResource);
                            omittedResources.Add($"{resource.ResName}.txi");
                        }

                        byte[] newData = TPCAuto.BytesTpc(tpc);
                        LogMessage(config, $"Adding patched TPC resource '{resource.ResName}.{resource.ResType.Extension}' to capsule {Path.GetFileName(newFilepath)}");
                        newResources.Add(Tuple.Create(resource.ResName, ResourceType.TPC, newData));
                        omittedResources.Add($"{resource.ResName}.{resource.ResType.Extension}");
                    }
                }
            }

            if (config.IsPatching())
            {
                ERF erfOrRim = FileHelpers.IsAnyErfTypeFile(cFile)
                    ? new ERF(ERFTypeExtensions.FromExtension(Path.GetExtension(cFile)))
                    : (ERF)(object)new RIM();
                foreach (var resource in fileCapsule)
                {
                    string ident = $"{resource.ResName}.{resource.ResType.Extension}";
                    if (!omittedResources.Contains(ident))
                    {
                        erfOrRim.SetData(resource.ResName, resource.ResType, resource.Data);
                    }
                }
                foreach (var resinfo in newResources)
                {
                    erfOrRim.SetData(resinfo.Item1, resinfo.Item2, resinfo.Item3);
                }

                LogMessage(config, $"Saving back to {Path.GetFileName(newFilepath)}");
                if (FileHelpers.IsAnyErfTypeFile(cFile))
                {
                    ERFAuto.WriteErf(erfOrRim, newFilepath, ResourceType.ERF);
                }
                else
                {
                    RIMAuto.WriteRim((RIM)(object)erfOrRim, newFilepath, ResourceType.RIM);
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:693-717
        // Original: def patch_file(...)
        public static void PatchFile(string file, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (processedFiles == null)
            {
                processedFiles = new HashSet<string>();
            }

            if (processedFiles.Contains(file))
            {
                return;
            }

            if (FileHelpers.IsCapsuleFile(file))
            {
                PatchCapsuleFile(file, config, processedFiles);
            }
            else if (config.IsPatching())
            {
                PatchAndSaveNoncapsule(FileResource.FromPath(file), config);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:719-738
        // Original: def patch_folder(...)
        public static void PatchFolder(string folderPath, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (processedFiles == null)
            {
                processedFiles = new HashSet<string>();
            }

            LogMessage(config, $"Recursing through resources in the '{Path.GetFileName(folderPath)}' folder...");
            foreach (string filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                PatchFile(filePath, config, processedFiles);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:741-753
        // Original: def is_kotor_install_dir(path: Path) -> bool:
        public static bool IsKotorInstallDir(string path)
        {
            var cPath = new CaseAwarePath(path);
            return cPath.IsDirectory() && cPath.JoinPath("chitin.key").IsFile();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:756-830
        // Original: def patch_install(...)
        public static void PatchInstall(string installPath, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (processedFiles == null)
            {
                processedFiles = new HashSet<string>();
            }

            LogMessage(config, $"Using install dir for operations:\t{installPath}");

            var kInstall = new Andastra.Parsing.Installation.Installation(installPath);
            if (config.IsPatching())
            {
                LogMessage(config, "Patching modules...");
                if (config.K1ConvertGffs || config.TslConvertGffs)
                {
                    // Module validation would need Installation to expose _modules
                    LogMessage(config, "Module validation not yet fully implemented");
                }

                // Module patching would need Installation to expose _modules
                LogMessage(config, "Module patching not yet fully implemented");
            }

            if (config.IsPatching())
            {
                LogMessage(config, "Patching Override...");
            }
            string overridePath = kInstall.OverridePath();
            Directory.CreateDirectory(overridePath);
            // Override patching would need Installation to expose override methods
            LogMessage(config, "Override patching not yet fully implemented");

            if (config.IsPatching())
            {
                LogMessage(config, "Extract and patch BIF data, saving to Override (will not overwrite)");
            }
            // Core resource patching would need Installation to expose core_resources
            LogMessage(config, "Core resource patching not yet fully implemented");

            PatchFile(Path.Combine(kInstall.Path, "dialog.tlk"), config, processedFiles);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:833-860
        // Original: def determine_input_path(...)
        public static void DetermineInputPath(string path, PatchingConfig config, HashSet<string> processedFiles = null)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException($"No such file or directory: {path}");
            }

            if (IsKotorInstallDir(path))
            {
                PatchInstall(path, config, processedFiles);
                return;
            }

            if (Directory.Exists(path))
            {
                PatchFolder(path, config, processedFiles);
                return;
            }

            if (File.Exists(path))
            {
                PatchFile(path, config, processedFiles);
                return;
            }
        }
    }
}