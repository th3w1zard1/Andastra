using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using JetBrains.Annotations;

namespace HolocronToolset.NET.Editors
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

        // Expose filepath for derived classes
        protected string Filepath => _filepath;

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
            // Note: ResourceType is a class, not an enum, so we can't use Enum.TryParse
            // For now, we'll skip adding format variants as they would need to be looked up differently
            // This functionality can be added later when needed
            var additionalFormats = new[] { "XML", "JSON", "CSV", "ASCII", "YAML" };
            var readList = _readSupported.ToList();
            var writeList = _writeSupported.ToList();

            // TODO: Add additional format variants when ResourceType lookup by name is implemented
            // For now, just use the provided supported types
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
    }
}
