//
// Settings for NCS Decompiler - UI is in NCSDecomp project (Avalonia)
//
using System;
using System.Collections.Generic;
using System.IO;
using JavaSystem = CSharpKOTOR.Formats.NCS.NCSDecomp.JavaSystem;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Settings storage for the NCS decompiler.
    /// The actual UI for editing settings is in the NCSDecomp project (SettingsWindow.axaml).
    /// </summary>
    public class Settings : Properties
    {
        private static readonly string ConfigFileName = "NCSDecomp.conf";

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:377-412
        // Original: public void load() { ... }
        public new void Load()
        {
            string configToLoad = ConfigFileName;
            if (!System.IO.File.Exists(configToLoad))
            {
                string legacyConfigFile = "dencs.conf";
                if (System.IO.File.Exists(legacyConfigFile))
                {
                    configToLoad = legacyConfigFile;
                }
            }

            try
            {
                if (System.IO.File.Exists(configToLoad))
                {
                    using (var fis = new FileInputStream(configToLoad))
                    {
                        base.Load(fis);
                    }
                }
                else
                {
                    try
                    {
                        System.IO.File.Create(ConfigFileName).Close();
                    }
                    catch (Exception)
                    {
                        // Ignore
                    }
                    Reset();
                    Save();
                }
            }
            catch (Exception ex)
            {
                ex.PrintStackTrace();
                try
                {
                    System.IO.File.Create(ConfigFileName).Close();
                }
                catch (Exception)
                {
                    // Ignore
                }
                Reset();
                Save();
            }

            // Apply loaded settings to static flags (matching Java Settings.load() lines 405-411)
            string gameVariant = GetProperty("Game Variant", "k1").ToLower();
            FileDecompiler.isK2Selected = gameVariant.Equals("k2") || gameVariant.Equals("tsl") || gameVariant.Equals("2");
            FileDecompiler.preferSwitches = bool.Parse(GetProperty("Prefer Switches", "false"));
            FileDecompiler.strictSignatures = bool.Parse(GetProperty("Strict Signatures", "false"));
            string nwnnsscompPath = GetProperty("nwnnsscomp Path", "");
            FileDecompiler.nwnnsscompPath = string.IsNullOrEmpty(nwnnsscompPath) ? null : nwnnsscompPath;
        }

        public new void Save()
        {
            try
            {
                using (var fos = new FileOutputStream(ConfigFileName))
                {
                    Store(fos, null);
                }
            }
            catch (Exception ex)
            {
                ex.PrintStackTrace();
            }
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Settings.java:430-450
        // Original: public void reset() { ... }
        public new void Reset()
        {
            base.Reset();
            // Default output directory: ./ncsdecomp_converted relative to current working directory
            string defaultOutputDir = Path.Combine(JavaSystem.GetProperty("user.dir"), "ncsdecomp_converted");
            SetProperty("Output Directory", defaultOutputDir);
            SetProperty("Open Directory", JavaSystem.GetProperty("user.dir"));
            string defaultNwnnsscompPath = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "nwnnsscomp.exe");
            SetProperty("nwnnsscomp Path", defaultNwnnsscompPath);
            string defaultK1Path = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "k1_nwscript.nss");
            string defaultK2Path = Path.Combine(Path.Combine(JavaSystem.GetProperty("user.dir"), "tools"), "tsl_nwscript.nss");
            SetProperty("K1 nwscript Path", defaultK1Path);
            SetProperty("K2 nwscript Path", defaultK2Path);
            SetProperty("Game Variant", "k1");
            SetProperty("Prefer Switches", "false");
            SetProperty("Strict Signatures", "false");
            SetProperty("Overwrite Files", "false");
            SetProperty("Encoding", "Windows-1252");
            SetProperty("File Extension", ".nss");
            SetProperty("Filename Prefix", "");
            SetProperty("Filename Suffix", "");
            SetProperty("Link Scroll Bars", "false");
        }

        /// <summary>
        /// Show settings dialog. This is a no-op in the library -
        /// the actual UI is in the NCSDecomp Avalonia project.
        /// </summary>
        public virtual void Show()
        {
            // No-op in library - UI is in NCSDecomp project
            Console.WriteLine("Settings.Show() called - use NCSDecomp Avalonia app for UI");
        }
    }
}




