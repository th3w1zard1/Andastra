using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Utils;
using HolocronToolset.Widgets;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:115
    // Original: class NSSEditor(Editor):
    public class NSSEditor : Editor
    {
        private CodeEditor _codeEdit;
        private TerminalWidget _terminalWidget;
        private bool _isDecompiled;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:119-199
        // Original: def __init__(self, parent: QWidget | None = None, installation: HTInstallation | None = None):
        public NSSEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Script Editor", "script",
                new[] { ResourceType.NSS, ResourceType.NCS },
                new[] { ResourceType.NSS, ResourceType.NCS },
                installation)
        {
            _installation = installation;
            _isDecompiled = false;

            InitializeComponent();
            SetupUI();
            SetupTerminal();
            SetupSignals();
            AddHelpAction();

            // Set Content after AddHelpAction (which may wrap it in a DockPanel)
            if (Content == null && _codeEdit != null)
            {
                Content = _codeEdit;
            }

            New();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;

                // Try to find code editor from XAML
                _codeEdit = this.FindControl<CodeEditor>("codeEdit");
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupUI();
            }
        }

        private void SetupUI()
        {
            // Create code editor if not found from XAML
            if (_codeEdit == null)
            {
                _codeEdit = new CodeEditor();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py
        // Original: Terminal widget setup for NSS editor
        // Based on industry-standard IDE patterns: integrated terminal for script execution and debugging
        private void SetupTerminal()
        {
            // Initialize terminal widget for script execution and debugging
            // Terminal allows users to run scripts, view compilation output, and execute commands
            _terminalWidget = new TerminalWidget();
            
            // Terminal widget is available but not automatically visible
            // Users can toggle terminal visibility via View menu or keyboard shortcut
            // This follows common IDE patterns (VS Code, Visual Studio, etc.)
        }

        // Public property to access terminal widget for testing and external access
        // Based on Avalonia control access patterns and testability best practices
        public TerminalWidget TerminalWidget => _terminalWidget;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:149
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // TODO: STUB - Signals setup - will be implemented as needed
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2121-2162
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes | bytearray):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            _isDecompiled = false;

            if (data == null || data.Length == 0)
            {
                if (_codeEdit != null)
                {
                    _codeEdit.SetPlainText("");
                }
                return;
            }

            if (restype == ResourceType.NSS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2136-2145
                // Original: Try multiple encodings to properly decode the text
                string text = null;
                try
                {
                    text = Encoding.UTF8.GetString(data);
                }
                catch (DecoderFallbackException)
                {
                    try
                    {
                        text = Encoding.GetEncoding("windows-1252").GetString(data);
                    }
                    catch (DecoderFallbackException)
                    {
                        text = Encoding.GetEncoding("latin-1").GetString(data);
                    }
                }

                if (_codeEdit != null)
                {
                    _codeEdit.SetPlainText(text);
                }
            }
            else if (restype == ResourceType.NCS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2146-2156
                // Original: elif restype is ResourceType.NCS:
                bool errorOccurred = false;
                try
                {
                    // Matching PyKotor implementation: self._handle_user_ncs(data, resref)
                    // In full implementation, this would show a dialog asking to decompile or download
                    // TODO: SIMPLIFIED - For now, we'll attempt decompilation directly
                    if (_installation != null)
                    {
                        // Attempt decompilation using DeNCS (matching Python _decompile_ncs_dencs)
                        string source = DecompileNcsDencs(data);
                        if (_codeEdit != null)
                        {
                            _codeEdit.SetPlainText(source);
                        }
                        _isDecompiled = true;
                    }
                    else
                    {
                        errorOccurred = true;
                    }
                }
                catch (Exception ex)
                {
                    // Matching PyKotor implementation: self._handle_exc_debug_mode("Decompilation/Download Failed", e)
                    errorOccurred = true;
                    System.Console.WriteLine($"Decompilation/Download Failed: {ex.Message}");
                }

                if (errorOccurred)
                {
                    New();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2196-2246
        // Original: def _decompile_ncs_dencs(self, ncs_data: bytes) -> str:
        private string DecompileNcsDencs(byte[] ncsData)
        {
            // Use ScriptDecompiler to decompile NCS
            if (_installation != null)
            {
                try
                {
                    string decompiled = ScriptDecompiler.HtDecompileScript(ncsData, _installation.Path, _installation.Tsl);
                    if (!string.IsNullOrEmpty(decompiled))
                    {
                        return decompiled;
                    }
                }
                catch (Exception ex)
                {
                    // Decompilation failed - in full implementation would show error dialog
                    // TODO: SIMPLIFIED - For now, return empty string
                    System.Console.WriteLine($"Decompilation failed: {ex.Message}");
                }
            }

            // If decompilation fails, raise ValueError (matching Python behavior)
            throw new InvalidOperationException("Decompilation failed: decompile_ncs returned None");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2269-2291
        // Original: def build(self) -> tuple[bytes | bytearray, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            if (_codeEdit == null)
            {
                return Tuple.Create(new byte[0], new byte[0]);
            }

            string text = _codeEdit.ToPlainText();

            if (_restype == ResourceType.NCS)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2281-2291
                // Original: Compile script if restype is NCS
                if (_installation != null)
                {
                    byte[] compiled = ScriptCompiler.HtCompileScript(text, _installation.Path, _installation.Tsl);
                    if (compiled != null && compiled.Length > 0)
                    {
                        return Tuple.Create(compiled, new byte[0]);
                    }
                    // User cancelled compilation
                    return Tuple.Create(new byte[0], new byte[0]);
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2270-2279
            // Original: Encode with proper error handling
            byte[] data;
            try
            {
                data = Encoding.UTF8.GetBytes(text);
            }
            catch (EncoderFallbackException)
            {
                try
                {
                    data = Encoding.GetEncoding("windows-1252").GetBytes(text);
                }
                catch (EncoderFallbackException)
                {
                    data = Encoding.GetEncoding("latin-1").GetBytes(text);
                }
            }

            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/nss.py:2293-2296
        // Original: def new(self):
        public override void New()
        {
            base.New();
            if (_codeEdit != null)
            {
                _codeEdit.SetPlainText("\n\nvoid main()\n{\n    \n}\n");
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
