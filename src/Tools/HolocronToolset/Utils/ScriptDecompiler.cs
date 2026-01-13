using System;
using System.IO;
using System.Text;
using BioWare.NET;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Decomp;
using HolocronToolset.Data;
using HolocronToolset.Utils;
using NcsFile = BioWare.NET.Resource.Formats.NCS.Decomp.NcsFile;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_decompiler.py:19
    // Original: def ht_decompile_script(...):
    public static class ScriptDecompiler
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_decompiler.py:19-95
        // Original: def ht_decompile_script(compiled_bytes: bytes, installation_path: Path, *, tsl: bool) -> str:
        public static string HtDecompileScript(byte[] compiledBytes, string installationPath, bool tsl = false)
        {
            if (compiledBytes == null || compiledBytes.Length == 0)
            {
                return "";
            }

            var settings = new GlobalSettings();
            string extractPath = ScriptUtils.SetupExtractPath();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_decompiler.py:46-60
            // Original: Check for NCS decompiler path
            string ncsDecompilerPath = settings.GetValue("NcsDecompilerPath", "");
            if (string.IsNullOrEmpty(ncsDecompilerPath) || !File.Exists(ncsDecompilerPath))
            {
                // TODO:  In full implementation, would prompt user for decompiler
                // TODO: STUB - For now, try to use built-in decompiler
                try
                {
                    return DecompileUsingBuiltIn(compiledBytes, installationPath, tsl);
                }
                catch
                {
                    throw new InvalidOperationException("NCS Decompiler has not been set or is invalid.");
                }
            }

            // Use external decompiler - will be implemented when external compiler integration is available
            // TODO: STUB - For now, fall back to built-in
            try
            {
                return DecompileUsingBuiltIn(compiledBytes, installationPath, tsl);
            }
            catch
            {
                throw new InvalidOperationException("Decompilation failed.");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2196-2246
        // Original: def _decompile_ncs_dencs(self, ncs_data: bytes) -> str:
        private static string DecompileUsingBuiltIn(byte[] ncsData, string installationPath, bool tsl)
        {
            // Read NCS from bytes
            NCS ncs = NCSAuto.ReadNcs(ncsData);
            if (ncs == null)
            {
                throw new InvalidOperationException("Failed to read NCS data.");
            }

            // Create FileDecompiler
            FileDecompiler decompiler = null;

            // Try to load nwscript.nss from override folder for actions data
            if (!string.IsNullOrEmpty(installationPath))
            {
                string overridePath = Path.Combine(installationPath, "override");
                string nwscriptPath = Path.Combine(overridePath, "nwscript.nss");

                if (File.Exists(nwscriptPath))
                {
                    try
                    {
                        decompiler = new FileDecompiler(new NcsFile(nwscriptPath));
                    }
                    catch
                    {
                        // Failed to load nwscript.nss, will use empty actions
                    }
                }
            }

            // If nwscript.nss wasn't found, create decompiler without actions
            if (decompiler == null)
            {
                decompiler = new FileDecompiler();
            }

            // Decompile NCS object
            try
            {
                var scriptData = decompiler.DecompileNcsObject(ncs);
                if (scriptData == null)
                {
                    throw new InvalidOperationException("Decompilation failed: DecompileNcsObject returned null");
                }

                scriptData.GenerateCode();
                string result = scriptData.GetCode();

                if (string.IsNullOrEmpty(result))
                {
                    throw new InvalidOperationException("Decompilation failed: result is empty");
                }

                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Decompilation failed: {ex.Message}");
            }
        }
    }
}
