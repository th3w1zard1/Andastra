using System;
using System.IO;
using System.Linq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TLK;
using TSLPatcher.Core.Installation;

namespace TSLPatcher.Core.Uninstall;

/// <summary>
/// Helper functions for uninstalling mods.
/// 1:1 port from Python uninstall.py
/// </summary>
public static class UninstallHelpers
{
    /// <summary>
    /// Uninstalls all mods from the game.
    /// 1:1 port from Python uninstall_all_mods
    ///
    /// What this method really does is delete all the contents of the override folder and delete all .MOD files from
    /// the modules folder. Then it removes all appended TLK entries using
    /// the hardcoded number of entries depending on the game. There are 49,265 TLK entries in KOTOR 1, and 136,329 in TSL.
    ///
    /// TODO: the aspyr patch contains some required files in the override folder, hardcode them and ignore those here.
    /// TODO: With the new Replace TLK syntax, the above TLK reinstall isn't possible anymore.
    /// Here, we should write the dialog.tlk and then check it's sha1 hash compared to vanilla.
    /// We could keep the vanilla TLK entries in a tlkdefs file, similar to our nwscript.nss defs.
    /// This implementation would be required regardless in K2 anyway as this function currently isn't determining if the Aspyr patch and/or TSLRCM is installed.
    /// </summary>
    /// <param name="gamePath">The path to the game installation directory</param>
    public static void UninstallAllMods(string gamePath)
    {
        var game = Core.Installation.Installation.DetermineGame(gamePath);
        if (game == null)
        {
            throw new ArgumentException($"Unable to determine game type at path: {gamePath}");
        }

        string overridePath = Core.Installation.Installation.GetOverridePath(gamePath);
        string modulesPath = Core.Installation.Installation.GetModulesPath(gamePath);

        // Remove any TLK changes
        string dialogTlkPath = Path.Combine(gamePath, "dialog.tlk");
        if (File.Exists(dialogTlkPath))
        {
            var dialogTlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();

            // Trim TLK entries based on game type
            int maxEntries = game == Game.K1 ? 49265 : 136329;
            if (dialogTlk.Entries.Count > maxEntries)
            {
                dialogTlk.Entries = dialogTlk.Entries.Take(maxEntries).ToList();
            }

            var writer = new TLKBinaryWriter(dialogTlk);
            File.WriteAllBytes(dialogTlkPath, writer.Write());
        }

        // Remove all override files
        if (Directory.Exists(overridePath))
        {
            foreach (string filePath in Directory.GetFiles(overridePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception)
                {
                    // Log or handle deletion errors if needed
                }
            }
        }

        // Remove any .MOD files
        if (Directory.Exists(modulesPath))
        {
            foreach (string filePath in Directory.GetFiles(modulesPath))
            {
                if (IsModFile(Path.GetFileName(filePath)))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                        // Log or handle deletion errors if needed
                    }
                }
            }
        }
    }

    /// <summary>
    /// Checks if a filename represents a .MOD file.
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if the file is a .MOD file, False otherwise</returns>
    private static bool IsModFile(string filename)
    {
        return filename.EndsWith(".mod", StringComparison.OrdinalIgnoreCase) ||
               filename.EndsWith(".MOD", StringComparison.Ordinal);
    }
}

