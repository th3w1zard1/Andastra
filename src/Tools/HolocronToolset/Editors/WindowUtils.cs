using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Editors.DLG;
using BioWare.NET.Resource;
using FileResource = BioWare.NET.Extract.FileResource;
using JetBrains.Annotations;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Editors
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
            var recentFiles = settings.GetValue("RecentFiles", new List<string>())
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
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:178
                // Original: QMessageBox(QMessageBox.Icon.Critical, tr("Failed to get the file data."), tr("An error occurred while attempting to read the data of the file.")).exec()
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Failed to get the file data.",
                    "An error occurred while attempting to read the data of the file.",
                    ButtonEnum.Ok,
                    Icon.Error);
                errorBox.ShowAsync();
                return null;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:127-356
        // Original: def _open_resource_editor_impl(...):
        [CanBeNull]
        public static Tuple<string, Window> OpenResourceEditor(
            string filepath = null,
            string resname = null,
            ResourceType restype = null,
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
            if (targetType == BioWare.NET.Resource.ResourceType.TwoDA)
            {
                editor = new TwoDAEditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.SSF)
            {
                editor = new SSFEditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.TLK)
            {
                editor = new TLKEditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.LTR)
            {
                editor = new LTREditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.LIP)
            {
                editor = new LIPEditor(parentWindow, installation);
            }
            else if (restype.Category == "Walkmeshes")
            {
                editor = new BWMEditor(parentWindow, installation);
            }
            else if ((restype.Category == "Images" || restype.Category == "Textures") && restype != BioWare.NET.Resource.ResourceType.TXI)
            {
                editor = new TPCEditor(parentWindow, installation);
            }
            else if (restype == BioWare.NET.Resource.ResourceType.NSS || restype == BioWare.NET.Resource.ResourceType.NCS)
            {
                if (installation == null && restype == BioWare.NET.Resource.ResourceType.NCS)
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:215-219
                    // Original: QMessageBox.warning(parent_window_widget, tr("Cannot decompile NCS without an installation active"), tr("Please select an installation from the dropdown before loading an NCS."))
                    var warningBox = MessageBoxManager.GetMessageBoxStandard(
                        "Cannot decompile NCS without an installation active",
                        "Please select an installation from the dropdown before loading an NCS.",
                        ButtonEnum.Ok,
                        Icon.Warning);
                    warningBox.ShowAsync();
                    return null;
                }
                editor = new NSSEditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.DLG)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTC || targetType == BioWare.NET.Resource.ResourceType.BTC || targetType == BioWare.NET.Resource.ResourceType.BIC)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTP || targetType == BioWare.NET.Resource.ResourceType.BTP)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTD || targetType == BioWare.NET.Resource.ResourceType.BTD)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.IFO)
            {
                editor = new IFOEditor(parentWindow, installation);
            }
            else if (targetType == BioWare.NET.Resource.ResourceType.UTS)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTT || targetType == BioWare.NET.Resource.ResourceType.BTT)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTM || targetType == BioWare.NET.Resource.ResourceType.BTM)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTW)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTE || targetType == BioWare.NET.Resource.ResourceType.BTE)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.UTI || targetType == BioWare.NET.Resource.ResourceType.BTI)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.JRL)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.ARE)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.PTH)
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
            else if (targetType == BioWare.NET.Resource.ResourceType.GIT)
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
            else if (restype == BioWare.NET.Resource.ResourceType.ERF || restype == BioWare.NET.Resource.ResourceType.SAV ||
                     restype == BioWare.NET.Resource.ResourceType.MOD || restype == BioWare.NET.Resource.ResourceType.RIM ||
                     restype == BioWare.NET.Resource.ResourceType.BIF)
            {
                editor = new ERFEditor(parentWindow, installation);
            }
            else if (restype == BioWare.NET.Resource.ResourceType.MDL || restype == BioWare.NET.Resource.ResourceType.MDX)
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
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:326-335
                // Original: QMessageBox(QMessageBox.Icon.Critical, tr("Failed to open file"), trf("The selected file format '{format}' is not yet supported.", format=str(restype)), ...).show()
                // Note: C# string.Format uses positional placeholders {0}, {1}, etc., so we convert the Python named placeholder {format} to {0}
                string message = string.Format("The selected file format '{0}' is not yet supported.", restype?.ToString() ?? "unknown");
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Failed to open file",
                    message,
                    ButtonEnum.Ok,
                    Icon.Error);
                errorBox.ShowAsync();
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
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/window.py:345-352
                // Original: QMessageBox(QMessageBox.Icon.Critical, tr("An unexpected error has occurred"), str(universal_simplify_exception(e)), ...).show()
                // Note: Using ex.Message for error details (similar to universal_simplify_exception in PyKotor)
                string errorMessage = ex.Message;
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = ex.ToString();
                }
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "An unexpected error has occurred",
                    errorMessage,
                    ButtonEnum.Ok,
                    Icon.Error);
                errorBox.ShowAsync();
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

        /// <summary>
        /// Gets all tracked toolset windows.
        /// Used by MiscUtils.GetTopLevel() to find an active window when MainWindow is not available.
        /// </summary>
        /// <returns>A copy of the list of tracked windows</returns>
        public static List<Window> GetTrackedWindows()
        {
            return new List<Window>(ToolsetWindows);
        }

        /// <summary>
        /// Gets the currently focused window from tracked windows, if any.
        /// </summary>
        /// <returns>The focused window, or null if none is focused</returns>
        public static Window GetFocusedWindow()
        {
            return ToolsetWindows.FirstOrDefault(w => w.IsFocused);
        }

        /// <summary>
        /// Gets the first visible window from tracked windows, if any.
        /// </summary>
        /// <returns>The first visible window, or null if none are visible</returns>
        public static Window GetVisibleWindow()
        {
            return ToolsetWindows.FirstOrDefault(w => w.IsVisible);
        }
    }
}
