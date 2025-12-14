using System;
using System.IO;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Formats.RIM;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/module.py
    // Original: Module-related utility functions
    public static class ModuleTools
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/module.py:296-341
        // Original: def rim_to_mod(...)
        public static void RimToMod(
            string filepath,
            string rimFolderpath = null,
            string moduleRoot = null,
            Game? game = null)
        {
            var rOutpath = new CaseAwarePath(filepath);
            if (!FileHelpers.IsModFile(rOutpath.GetResolvedPath()))
            {
                throw new ArgumentException("Specified file must end with the .mod extension");
            }

            moduleRoot = Installation.Installation.GetModuleRoot(moduleRoot ?? filepath);
            var rRimFolderpath = rimFolderpath != null ? new CaseAwarePath(rimFolderpath) : new CaseAwarePath(Path.GetDirectoryName(filepath));

            string filepathRim = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}.rim");
            string filepathRimS = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}_s.rim");
            string filepathDlgErf = Path.Combine(rRimFolderpath.GetResolvedPath(), $"{moduleRoot}_dlg.erf");

            var mod = new ERF(ERFType.MOD);
            if (File.Exists(filepathRim))
            {
                var rim = RIMAuto.ReadRim(filepathRim);
                foreach (var res in rim)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            if (File.Exists(filepathRimS))
            {
                var rimS = RIMAuto.ReadRim(filepathRimS);
                foreach (var res in rimS)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            if ((game == null || game.Value.IsK2()) && File.Exists(filepathDlgErf))
            {
                var dlgErf = ERFAuto.ReadErf(filepathDlgErf);
                foreach (var res in dlgErf)
                {
                    mod.SetData(res.ResRef.ToString(), res.ResType, res.Data);
                }
            }

            ERFAuto.WriteErf(mod, filepath, ResourceType.MOD);
        }
    }
}
