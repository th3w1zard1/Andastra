using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using JetBrains.Annotations;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:291
    // Original: class Editor(QMainWindow):
    public abstract class Editor : Window
    {
        protected const string CapsuleFilter = "*.mod *.erf *.rim *.sav";

        protected HTInstallation _installation;
        protected string _editorTitle;
        protected string _filepath;
        protected string _resname;
        protected ResourceType _restype;
        protected byte[] _revert;
        protected bool _isSaveGameResource;
        protected ResourceType[] _readSupported;
        protected ResourceType[] _writeSupported;

        // Expose filepath for derived classes and testing
        protected string Filepath => _filepath;
        
        // Public property for testing
        public string FilepathPublic => _filepath;

        // Expose installation for widgets and derived classes
        // Matching PyKotor: widgets access editor._installation directly
        internal HTInstallation Installation => _installation;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:303-350
        // Original: def __init__(self, parent, title, iconName, readSupported, writeSupported, installation):
        protected Editor(
            Window parent,
            string title,
            string iconName,
            ResourceType[] readSupported,
            ResourceType[] writeSupported,
            HTInstallation installation = null)
        {
            _installation = installation;
            _editorTitle = title;
            Title = title;
            _readSupported = readSupported ?? new ResourceType[0];
            _writeSupported = writeSupported ?? new ResourceType[0];

            SetupEditorFilters();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:489-516
        // Original: def setupEditorFilters(self, readSupported, writeSupported):
        protected void SetupEditorFilters()
        {
            // Setup file filters for open/save dialogs
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:489-516
            // Original: Additional formats handling
            // Add format variants (XML, JSON, CSV, ASCII, YAML) for each base resource type
            var additionalFormats = new[] { "XML", "JSON", "CSV", "ASCII", "YAML" };
            var readList = _readSupported.ToList();
            var writeList = _writeSupported.ToList();

            // Add format variants for read supported types
            // For each base type, look for variants like {FieldName}_XML, {FieldName}_JSON, etc.
            // Matching PyKotor: uses restype.name (field name) to construct variant names
            var readVariants = new List<ResourceType>();
            foreach (var restype in _readSupported)
            {
                string fieldName = restype.GetFieldName();
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                foreach (var addFormat in additionalFormats)
                {
                    string variantFieldName = $"{fieldName}_{addFormat}";
                    ResourceType variant = ResourceType.FromName(variantFieldName);
                    if (variant != null && !variant.IsInvalid)
                    {
                        readVariants.Add(variant);
                    }
                }
            }
            readList.AddRange(readVariants);

            // Add format variants for write supported types
            // For each base type, look for variants like {FieldName}_XML, {FieldName}_JSON, etc.
            // Matching PyKotor: uses restype.name (field name) to construct variant names
            var writeVariants = new List<ResourceType>();
            foreach (var restype in _writeSupported)
            {
                string fieldName = restype.GetFieldName();
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                foreach (var addFormat in additionalFormats)
                {
                    string variantFieldName = $"{fieldName}_{addFormat}";
                    ResourceType variant = ResourceType.FromName(variantFieldName);
                    if (variant != null && !variant.IsInvalid)
                    {
                        writeVariants.Add(variant);
                    }
                }
            }
            writeList.AddRange(writeVariants);

            _readSupported = readList.ToArray();
            _writeSupported = writeList.ToArray();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:474-487
        // Original: def refreshWindowTitle(self):
        protected void RefreshWindowTitle()
        {
            string installationName = _installation == null ? "No Installation" : _installation.Name;
            if (string.IsNullOrEmpty(_filepath) || string.IsNullOrEmpty(_resname) || _restype == null)
            {
                Title = $"{_editorTitle}({installationName})";
                return;
            }

            Title = $"{_filepath} - {_editorTitle}({installationName})";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:523-589
        // Original: def save_as(self):
        public abstract void SaveAs();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:590-644
        // Original: def save(self):
        public virtual void Save()
        {
            if (string.IsNullOrEmpty(_filepath))
            {
                SaveAs();
                return;
            }

            try
            {
                var (data, dataExt) = Build();
                if (data == null)
                {
                    return;
                }

                _revert = data;
                RefreshWindowTitle();

                // Save to file
                File.WriteAllBytes(_filepath, data);
            }
            catch (Exception)
            {
                // Show error message
                // This will be implemented with MessageBox.Avalonia when needed
                throw;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:700-750
        // Original: def load(self, filepath, resref, restype, data):
        public virtual void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            _filepath = filepath;
            _resname = resref;
            _restype = restype;
            _revert = data;
            RefreshWindowTitle();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:750-780
        // Original: def new(self):
        public virtual void New()
        {
            _filepath = null;
            _resname = null;
            _restype = null;
            _revert = null;
            RefreshWindowTitle();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:750-780
        // Original: def build(self) -> tuple[bytes, bytes]:
        public abstract Tuple<byte[], byte[]> Build();

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor.py:518-521
        // Original: def getOpenedFileName(self) -> str:
        public string GetOpenedFileName()
        {
            if (!string.IsNullOrEmpty(_filepath) && !string.IsNullOrEmpty(_resname) && _restype != null)
            {
                return $"{_resname}.{_restype.Extension}";
            }
            return "";
        }

        // Helper method for editors to safely initialize XAML
        protected bool TryLoadXaml()
        {
            try
            {
                Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:187-239
        // Original: def _add_help_action(self, wiki_filename: str | None = None):
        public void AddHelpAction(string wikiFilename = null)
        {
            string[] wikiFilenames = null;
            
            // Auto-detect wiki files if not provided
            if (string.IsNullOrEmpty(wikiFilename))
            {
                string editorClassName = GetType().Name;
                wikiFilenames = EditorWikiMapping.GetWikiFiles(editorClassName);
                if (wikiFilenames == null || wikiFilenames.Length == 0)
                {
                    // No wiki files for this editor, skip adding help
                    return;
                }
            }
            else
            {
                // Single file provided, convert to array for consistency
                wikiFilenames = new[] { wikiFilename };
            }

            // Find or create Help menu item
            MenuItem helpMenuItem = FindHelpMenuItem();
            if (helpMenuItem == null)
            {
                helpMenuItem = CreateHelpMenuItem();
            }

            // Check if Documentation action already exists (idempotent)
            MenuItem docAction = FindDocumentationAction(helpMenuItem);
            if (docAction == null)
            {
                // Add help action with question mark icon
                docAction = new MenuItem
                {
                    Header = "Documentation"
                };
                docAction.Click += (sender, e) => ShowHelpDialog(wikiFilenames);
                
                // Add F1 shortcut
                var shortcut = new KeyGesture(Key.F1);
                docAction.HotKey = shortcut;
                
                helpMenuItem.Items.Add(docAction);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editor/base.py:241-251
        // Original: def _show_help_dialog(self, wiki_filename: str):
        public void ShowHelpDialog(string wikiFilename)
        {
            if (string.IsNullOrEmpty(wikiFilename))
            {
                return;
            }

            ShowHelpDialog(new[] { wikiFilename });
        }

        // Overload to support multiple wiki files
        public void ShowHelpDialog(string[] wikiFilenames)
        {
            if (wikiFilenames == null || wikiFilenames.Length == 0)
            {
                return;
            }

            // Create non-blocking dialog with multiple files
            var dialog = new Dialogs.EditorHelpDialog(this, wikiFilenames);
            dialog.Show(); // Non-blocking show
        }

        // Helper method to find Help menu item in the window
        private MenuItem FindHelpMenuItem()
        {
            // Search for Menu controls in the window
            var menus = FindControls<Menu>(this);
            foreach (var menu in menus)
            {
                // Check if this menu contains a Help item
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem && menuItem.Header?.ToString() == "Help")
                    {
                        return menuItem;
                    }
                }
            }
            return null;
        }

        // Helper method to create Help menu item
        private MenuItem CreateHelpMenuItem()
        {
            // Find the main menu bar
            var mainMenu = FindControls<Menu>(this).FirstOrDefault();
            if (mainMenu == null)
            {
                // Create a menu bar if it doesn't exist
                // In Avalonia, menus are typically added to a DockPanel or directly to the window
                mainMenu = new Menu();
                // Add menu to window - wrap content in DockPanel if needed
                if (Content is Panel panel)
                {
                    var dockPanel = new DockPanel();
                    dockPanel.Children.Add(mainMenu);
                    DockPanel.SetDock(mainMenu, Dock.Top);
                    // Move existing content
                    var children = panel.Children.ToList();
                    foreach (var child in children)
                    {
                        panel.Children.Remove(child);
                        dockPanel.Children.Add(child);
                    }
                    Content = dockPanel;
                }
                else
                {
                    var dockPanel = new DockPanel();
                    dockPanel.Children.Add(mainMenu);
                    DockPanel.SetDock(mainMenu, Dock.Top);
                    if (Content != null && Content is Control content)
                    {
                        dockPanel.Children.Add(content);
                    }
                    Content = dockPanel;
                }
            }

            // Create Help menu item
            var helpMenuItem = new MenuItem { Header = "Help" };
            mainMenu.Items.Add(helpMenuItem);
            return helpMenuItem;
        }

        // Helper method to find Documentation action in Help menu item
        private MenuItem FindDocumentationAction(MenuItem helpMenuItem)
        {
            if (helpMenuItem == null)
            {
                return null;
            }

            foreach (var item in helpMenuItem.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header?.ToString() == "Documentation")
                {
                    return menuItem;
                }
            }
            return null;
        }

        // Helper method to find controls recursively
        private static IEnumerable<T> FindControls<T>(Control parent) where T : Control
        {
            var results = new List<T>();
            if (parent is T match)
            {
                results.Add(match);
            }

            if (parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control control)
                    {
                        results.AddRange(FindControls<T>(control));
                    }
                }
            }
            else if (parent is ContentControl contentControl && contentControl.Content is Control content)
            {
                results.AddRange(FindControls<T>(content));
            }

            return results;
        }
    }
}
