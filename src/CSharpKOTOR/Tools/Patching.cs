using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.RIM;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Formats.TPC;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Tools
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:359-397
        // Original: def process_translations(tlk: TLK, from_lang: Language, config: PatchingConfig) -> None:
        public static void ProcessTranslations(TLK tlk, Language fromLang, PatchingConfig config)
        {
            if (config.Translator == null)
            {
                return;
            }

            // Simplified translation processing - full implementation would use translator
            LogMessage(config, "Translation processing not fully implemented - requires translator instance");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/patching.py:741-753
        // Original: def is_kotor_install_dir(path: Path) -> bool:
        public static bool IsKotorInstallDir(string path)
        {
            if (!Directory.Exists(path))
            {
                return false;
            }
            string chitinKey = Path.Combine(path, "chitin.key");
            return File.Exists(chitinKey);
        }
    }
}
