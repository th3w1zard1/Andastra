using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using AuroraEngine.Common.Resources;
using FileResource = AuroraEngine.Common.Resources.FileResource;
using JetBrains.Annotations;
using NSSEditor = HolocronToolset.NET.Editors.NSSEditor;

namespace HolocronToolset.NET.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:26
    // Original: TOOLSET_WINDOWS: list[QDialog | QMainWindow] = []
    public static class WindowUtils
    {
        private static readonly List<Window> ToolsetWindows = new List<Window>();
        private static readonly object UniqueSentinel = new object();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:31-62
        // Original: def add_window(window: QDialog | QMainWindow, *, show: bool = True):
        public static void AddWindow(Window window, bool show = true)
        {
            if (window == null)
            {
                return;
            }

            // Store original closing handler
            window.Closing += (sender, e) =>
            {
                if (sender is Window w && ToolsetWindows.Contains(w))
                {
                    ToolsetWindows.Remove(w);
                }
            };

            if (show)
            {
                window.Show();
            }
            ToolsetWindows.Add(window);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:65-72
        // Original: def add_recent_file(file: Path):
        public static void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return;
            }

            var settings = new Settings("Global");
            var recentFiles = settings.GetValue<List<string>>("RecentFiles", new List<string>())
                .Where(fp => File.Exists(fp))
                .ToList();

            recentFiles.Insert(0, filePath);
            if (recentFiles.Count > 15)
            {
                recentFiles.RemoveAt(recentFiles.Count - 1);
            }

            settings.SetValue("RecentFiles", recentFiles);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:75-356
        // Original: def open_resource_editor(...):
        [CanBeNull]
        public static Tuple<string, Window> OpenResourceEditor(
            FileResource resource,
            HTInstallation installation = null,
            Window parentWindow = null,
            bool? gffSpecialized = null)
        {
            if (resource == null)
            {
                return null;
            }

            try
            {
                byte[] data = resource.GetData();
                return OpenResourceEditor(
                    resource.FilePath,
                    resource.ResName,
                    resource.ResType,
                    data,
                    installation,
                    parentWindow,
                    gffSpecialized);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error getting resource data: {ex}");
                // TODO: Show MessageBox when MessageBox.Avalonia is available
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:127-356
        // Original: def _open_resource_editor_impl(...):
        [CanBeNull]
        public static Tuple<string, Window> OpenResourceEditor(
            string filepath = null,
            string resname = null,
            AuroraEngine.Common.Resources.ResourceType restype = null,
            byte[] data = null,
            HTInstallation installation = null,
            Window parentWindow = null,
            bool? gffSpecialized = null)
        {
            if (restype == null)
            {
                return null;
            }

            // Get GFF specialized setting if not provided
            if (gffSpecialized == null)
            {
                var settings = new GlobalSettings();
                gffSpecialized = settings.GetGffSpecializedEditors();
            }

            Editor editor = null;
            var targetType = restype.TargetType();

            // Route to appropriate editor based on resource type
            if (targetType == AuroraEngine.Common.Resources.ResourceType.TwoDA)
            {
                editor = new TwoDAEditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.SSF)
            {
                editor = new SSFEditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.TLK)
            {
                editor = new TLKEditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.LTR)
            {
                editor = new LTREditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.LIP)
            {
                editor = new LIPEditor(parentWindow, installation);
            }
            else if (restype.Category == "Walkmeshes")
            {
                editor = new BWMEditor(parentWindow, installation);
            }
            else if ((restype.Category == "Images" || restype.Category == "Textures") && restype != AuroraEngine.Common.Resources.ResourceType.TXI)
            {
                editor = new TPCEditor(parentWindow, installation);
            }
            else if (restype == AuroraEngine.Common.Resources.ResourceType.NSS || restype == AuroraEngine.Common.Resources.ResourceType.NCS)
            {
                if (installation == null && restype == AuroraEngine.Common.Resources.ResourceType.NCS)
                {
                    // Show warning for NCS without installation
                    // TODO: Show MessageBox when MessageBox.Avalonia is available
                    return null;
                }
                editor = new NSSEditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.DLG)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new DLGEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTC || targetType == AuroraEngine.Common.Resources.ResourceType.BTC || targetType == AuroraEngine.Common.Resources.ResourceType.BIC)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTCEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTP || targetType == AuroraEngine.Common.Resources.ResourceType.BTP)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTPEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTD || targetType == AuroraEngine.Common.Resources.ResourceType.BTD)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTDEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.IFO)
            {
                editor = new IFOEditor(parentWindow, installation);
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTS)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTSEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTT || targetType == AuroraEngine.Common.Resources.ResourceType.BTT)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTTEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTM || targetType == AuroraEngine.Common.Resources.ResourceType.BTM)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTMEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTW)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTWEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTE || targetType == AuroraEngine.Common.Resources.ResourceType.BTE)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTEEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.UTI || targetType == AuroraEngine.Common.Resources.ResourceType.BTI)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new UTIEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.JRL)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new JRLEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.ARE)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new AREEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.PTH)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new PTHEditor(parentWindow, installation);
                }
            }
            else if (targetType == AuroraEngine.Common.Resources.ResourceType.GIT)
            {
                if (installation == null || !gffSpecialized.Value)
                {
                    editor = new GFFEditor(parentWindow, installation);
                }
                else
                {
                    editor = new GITEditor(parentWindow, installation);
                }
            }
            else if (restype.Category == "Audio")
            {
                editor = new WAVEditor(parentWindow, installation);
            }
            else if (restype == AuroraEngine.Common.Resources.ResourceType.ERF || restype == AuroraEngine.Common.Resources.ResourceType.SAV ||
                     restype == AuroraEngine.Common.Resources.ResourceType.MOD || restype == AuroraEngine.Common.Resources.ResourceType.RIM ||
                     restype == AuroraEngine.Common.Resources.ResourceType.BIF)
            {
                editor = new ERFEditor(parentWindow, installation);
            }
            else if (restype == AuroraEngine.Common.Resources.ResourceType.MDL || restype == AuroraEngine.Common.Resources.ResourceType.MDX)
            {
                editor = new MDLEditor(parentWindow, installation);
            }
            else if (targetType.Contents == "gff")
            {
                editor = new GFFEditor(parentWindow, installation);
            }
            else if (restype.Contents == "plaintext")
            {
                editor = new TXTEditor(parentWindow, installation);
            }

            if (editor == null)
            {
                // TODO: Show error message when MessageBox.Avalonia is available
                return null;
            }

            try
            {
                editor.Load(filepath, resname, restype, data);
                AddWindow(editor, show: true);
                return Tuple.Create(filepath, (Window)editor);
            }
            catch (Exception ex)
            {
                // TODO: Show error message when MessageBox.Avalonia is available
                System.Console.WriteLine($"Error loading resource: {ex}");
                return null;
            }
        }

        public static void CloseAllWindows()
        {
            var windows = new List<Window>(ToolsetWindows);
            foreach (var window in windows)
            {
                try
                {
                    window.Close();
                }
                catch
                {
                    // Ignore errors when closing
                }
            }
            ToolsetWindows.Clear();
        }

        public static int WindowCount => ToolsetWindows.Count;
    }
}
