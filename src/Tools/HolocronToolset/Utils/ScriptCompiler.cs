using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.NCS;
using HolocronToolset.Data;
using HolocronToolset.Utils;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_compiler.py:28
    // Original: def ht_compile_script(...):
    public static class ScriptCompiler
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_compiler.py:28-73
        // Original: def ht_compile_script(source: str, installation_path: Path, *, tsl: bool) -> bytes | None:
        public static byte[] HtCompileScript(string source, string installationPath, bool tsl = false)
        {
            if (string.IsNullOrEmpty(source))
            {
                return null;
            }

            var settings = new GlobalSettings();
            string extractPath = ScriptUtils.SetupExtractPath();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_compiler.py:62-64
            // Original: if os.name == "posix" or return_value == QMessageBox.StandardButton.Yes:
            // Original: log.debug("user chose Yes, compiling with builtin")
            // Original: return bytes(bytes_ncs(compile_nss(source, Game.K2 if tsl else Game.K1, library_lookup=[extract_path])))
            // Use built-in compiler (matching Python behavior on posix or when user chooses built-in)
            try
            {
                Game game = tsl ? Game.TSL : Game.K1;
                List<string> libraryLookup = new List<string>();
                if (!string.IsNullOrEmpty(extractPath))
                {
                    libraryLookup.Add(extractPath);
                }

                NCS ncs = NCSAuto.CompileNss(source, game, null, null, libraryLookup);
                if (ncs == null)
                {
                    return null;
                }

                return NCSAuto.BytesNcs(ncs);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error compiling script: {ex}");
                return null;
            }
        }
    }
}
