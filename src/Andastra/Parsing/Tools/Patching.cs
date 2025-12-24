using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Extract.Capsule;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.ARE;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Resource.Generics.UTC;
using Andastra.Parsing.Resource.Generics.UTI;

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
        public static void ConvertGffGame(BioWareGame fromGame, FileResource resource, PatchingConfig config)
        {
            BioWareGame toGame = fromGame.IsK1() ? BioWareGame.K2 : BioWareGame.K1;
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
            byte[] convertedBytes = null;
            try
            {
                byte[] resourceData = resource.GetData();
                int dataSize = resourceData.Length;

                // Match PyKotor implementation: read and write each GFF resource type
                if (resource.ResType == ResourceType.ARE)
                {
                    ARE are = AREHelpers.ReadAre(resourceData, 0, dataSize);
                    convertedBytes = AREHelpers.BytesAre(are, toGame);
                }
                else if (resource.ResType == ResourceType.DLG)
                {
                    DLG dlg = DLGHelper.ReadDlg(resourceData, 0, dataSize);
                    convertedBytes = DLGHelper.BytesDlg(dlg, toGame);
                }
                else if (resource.ResType == ResourceType.GIT)
                {
                    GIT git = ResourceAutoHelpers.ReadGit(resourceData);
                    convertedBytes = GITHelpers.BytesGit(git, toGame);
                }
                else if (resource.ResType == ResourceType.JRL)
                {
                    JRL jrl = JRLHelpers.ReadJrl(resourceData, 0, dataSize);
                    convertedBytes = JRLHelpers.BytesJrl(jrl);
                }
                else if (resource.ResType == ResourceType.PTH)
                {
                    PTH pth = PTHAuto.ReadPth(resourceData, 0, dataSize);
                    convertedBytes = PTHAuto.BytesPth(pth, toGame);
                }
                else if (resource.ResType == ResourceType.UTC)
                {
                    UTC utc = UTCHelpers.ReadUtc(resourceData, 0, dataSize);
                    convertedBytes = UTCHelpers.BytesUtc(utc, toGame);
                }
                else if (resource.ResType == ResourceType.UTD)
                {
                    UTD utd = ResourceAutoHelpers.ReadUtd(resourceData);
                    GFF utdGff = UTDHelpers.DismantleUtd(utd, toGame);
                    convertedBytes = GFFAuto.BytesGff(utdGff, ResourceType.UTD);
                }
                else if (resource.ResType == ResourceType.UTE)
                {
                    var reader = new GFFBinaryReader(resourceData);
                    GFF gff = reader.Load();
                    UTE ute = UTEHelpers.ConstructUte(gff);
                    GFF uteGff = UTEHelpers.DismantleUte(ute, toGame);
                    convertedBytes = GFFAuto.BytesGff(uteGff, ResourceType.UTE);
                }
                else if (resource.ResType == ResourceType.UTI)
                {
                    UTI uti = ResourceAutoHelpers.ReadUti(resourceData);
                    convertedBytes = UTIHelpers.BytesUti(uti, toGame);
                }
                else if (resource.ResType == ResourceType.UTM)
                {
                    Andastra.Parsing.Resource.Generics.UTM.UTM utm = Andastra.Parsing.Resource.Generics.UTM.UTMHelpers.ReadUtm(resourceData, 0, dataSize);
                    convertedBytes = Andastra.Parsing.Resource.Generics.UTM.UTMHelpers.BytesUtm(utm, toGame);
                }
                else if (resource.ResType == ResourceType.UTP)
                {
                    UTP utp = ResourceAutoHelpers.ReadUtp(resourceData);
                    GFF utpGff = UTPHelpers.DismantleUtp(utp, toGame);
                    convertedBytes = GFFAuto.BytesGff(utpGff, ResourceType.UTP);
                }
                else if (resource.ResType == ResourceType.UTS)
                {
                    UTS uts = ResourceAutoHelpers.ReadUts(resourceData);
                    GFF utsGff = UTSHelpers.DismantleUts(uts, toGame);
                    convertedBytes = GFFAuto.BytesGff(utsGff, ResourceType.UTS);
                }
                else if (resource.ResType == ResourceType.UTT)
                {
                    UTT utt = UTTAuto.ReadUtt(resourceData, 0, dataSize);
                    convertedBytes = UTTAuto.BytesUtt(utt, toGame);
                }
                else if (resource.ResType == ResourceType.UTW)
                {
                    UTW utw = UTWAuto.ReadUtw(resourceData, 0, dataSize);
                    convertedBytes = UTWAuto.BytesUtw(utw, toGame);
                }
                else
                {
                    LogMessage(config, $"Unsupported gff: {resource.Identifier}");
                    return;
                }

                // Write converted data
                if (convertedBytes != null)
                {
                    if (resource.InsideCapsule)
                    {
                        // Match PyKotor: use LazyCapsule to update capsule resource
                        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:352-356
                        LogMessage(config, $"Saving conversions in ERF/RIM at '{savepath}'");
                        LazyCapsule lazyCapsule = new LazyCapsule(savepath, createIfNotExist: true);
                        lazyCapsule.Delete(resource.ResName, resource.ResType);
                        lazyCapsule.Add(resource.ResName, resource.ResType, convertedBytes);
                    }
                    else
                    {
                        // Match PyKotor: write directly to file
                        File.WriteAllBytes(savepath, convertedBytes);
                    }
                }
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
        /// <summary>
        /// Processes translations for a TLK file.
        /// Translates all entries in the TLK from the source language to the target language using the configured translator.
        /// </summary>
        /// <param name="tlk">The TLK file to translate.</param>
        /// <param name="fromLang">Source language of the TLK entries.</param>
        /// <param name="config">Patching configuration with translator instance.</param>
        /// <remarks>
        /// Based on PyKotor implementation:
        /// - Skips empty, numeric-only, and special "do not translate" entries
        /// - Uses parallel processing with configurable thread count
        /// - Fixes encoding for translated text based on target language
        /// - Replaces entries in-place in the TLK object
        /// - Logs each translation for debugging
        /// </remarks>
        public static void ProcessTranslations(TLK tlk, Language fromLang, PatchingConfig config)
        {
            if (config.Translator == null)
            {
                return;
            }

            // Get translator interface via reflection (translator is stored as object for flexibility)
            // Expected interface:
            // - Translate(string text, Language fromLang) -> string method
            // - ToLang: Language property
            object translator = config.Translator;
            Type translatorType = translator.GetType();

            // Get ToLang property
            PropertyInfo toLangProperty = translatorType.GetProperty("ToLang");
            if (toLangProperty == null)
            {
                LogMessage(config, "Translator instance does not have ToLang property - translation skipped");
                return;
            }
            Language toLang = (Language)toLangProperty.GetValue(translator);

            // Get Translate method
            MethodInfo translateMethod = translatorType.GetMethod("Translate", new[] { typeof(string), typeof(Language) });
            if (translateMethod == null)
            {
                LogMessage(config, "Translator instance does not have Translate(string, Language) method - translation skipped");
                return;
            }

            // Helper function to translate a single entry
            // Based on PyKotor: def translate_entry(tlkentry: TLKEntry, from_lang: Language) -> tuple[str, str]
            Tuple<string, string> TranslateEntry(TLKEntry entry, Language sourceLang)
            {
                string text = entry.Text;

                // Skip empty or whitespace-only text
                if (string.IsNullOrWhiteSpace(text))
                {
                    return Tuple.Create(text, "");
                }

                // Skip numeric-only text (likely placeholder or ID)
                if (text.Trim().All(char.IsDigit))
                {
                    return Tuple.Create(text, "");
                }

                // Skip special "do not translate" markers
                if (text.Contains("Do not translate this text", StringComparison.OrdinalIgnoreCase))
                {
                    return Tuple.Create(text, text);
                }
                if (text.Contains("actual text to be translated", StringComparison.OrdinalIgnoreCase))
                {
                    return Tuple.Create(text, text);
                }

                // Translate the text
                try
                {
                    string translatedText = (string)translateMethod.Invoke(translator, new object[] { text, sourceLang });
                    return Tuple.Create(text, translatedText ?? "");
                }
                catch (Exception ex)
                {
                    LogMessage(config, $"Error translating text '{text}': {ex.Message}");
                    return Tuple.Create(text, "");
                }
            }

            // Get encoding for target language
            string targetEncoding = toLang.GetEncoding();

            // Process translations using parallel processing
            // Based on PyKotor: ThreadPoolExecutor with max_workers=config.max_threads
            int maxThreads = config.MaxThreads > 0 ? config.MaxThreads : 2;

            // Collect all entries that need translation
            var entriesToTranslate = new List<Tuple<int, TLKEntry>>();
            foreach (var entryTuple in tlk)
            {
                int stringref = entryTuple.stringref;
                TLKEntry entry = entryTuple.entry;
                entriesToTranslate.Add(Tuple.Create(stringref, entry));
            }

            // Use Parallel.ForEach for thread-safe parallel processing
            // Based on PyKotor: ThreadPoolExecutor pattern with max_workers
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxThreads
            };

            // Dictionary to store translation results (thread-safe for parallel writes)
            var translationResults = new ConcurrentDictionary<int, Tuple<string, string>>();

            // Process translations in parallel
            Parallel.ForEach(entriesToTranslate, parallelOptions, entryInfo =>
            {
                int stringref = entryInfo.Item1;
                TLKEntry entry = entryInfo.Item2;

                try
                {
                    var result = TranslateEntry(entry, fromLang);
                    translationResults[stringref] = result;
                }
                catch (Exception ex)
                {
                    LogMessage(config, $"TLK strref {stringref} generated an exception: {ex.Message}");
                }
            });

            // Apply translations to TLK (single-threaded for thread safety)
            // Based on PyKotor: concurrent.futures.as_completed pattern
            foreach (var kvp in translationResults)
            {
                int stringref = kvp.Key;
                string originalText = kvp.Value.Item1;
                string translatedText = kvp.Value.Item2;

                if (!string.IsNullOrWhiteSpace(translatedText))
                {
                    // Fix encoding for translated text
                    // Based on PyKotor: fix_encoding(translated_text, config.translator.to_lang.get_encoding())
                    string fixedText = FixEncoding(translatedText, targetEncoding);

                    // Replace entry in TLK
                    // Based on PyKotor: tlk.replace(strref, translated_text)
                    tlk.Replace(stringref, fixedText);

                    // Log translation
                    LogMessage(config, $"#{stringref} Translated {originalText} --> {fixedText}");
                }
            }
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
                    ConvertGffGame(BioWareGame.K2, resource, config);
                }
                if (config.TslConvertGffs && !resource.InsideCapsule)
                {
                    ConvertGffGame(BioWareGame.K1, resource, config);
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
