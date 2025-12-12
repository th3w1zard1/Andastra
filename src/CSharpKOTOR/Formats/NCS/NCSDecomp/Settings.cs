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

        public new void Load()
        {
            try
            {
                if (System.IO.File.Exists(ConfigFileName))
                {
                    using (var fis = new FileInputStream(ConfigFileName))
                    {
                        base.Load(fis);
                    }
                }
                else
                {
                    Reset();
                    Save();
                }
            }
            catch (Exception ex)
            {
                ex.PrintStackTrace();
                Reset();
                Save();
            }
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

        public new void Reset()
        {
            base.Reset();
            SetProperty("Output Directory", JavaSystem.GetProperty("user.dir"));
            SetProperty("Open Directory", JavaSystem.GetProperty("user.dir"));
            SetProperty("Link Scroll Bars", "false");
            SetProperty("NWScript Path", ""); // Empty = auto-detect
            SetProperty("Game Type", "K1"); // K1 or TSL
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




