using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Installation;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using HolocronToolset.Widgets;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:25
    // Original: class MDLEditor(Editor):
    public class MDLEditor : Editor
    {
        private MDL _mdl;
        private ModelRenderer _modelRenderer;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:26-42
        // Original: def __init__(self, parent: QWidget | None, installation: HTInstallation | None = None):
        public MDLEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Model Viewer", "none",
                new[] { ResourceType.MDL },
                new[] { ResourceType.MDL },
                installation)
        {
            _installation = installation;
            _mdl = new MDL();

            InitializeComponent();
            SetupUI();
            SetupSignals();

            if (_modelRenderer != null)
            {
                _modelRenderer.Installation = installation;
            }

            AddHelpAction();

            // Set Content after AddHelpAction (which may wrap it in a DockPanel)
            if (Content == null && _modelRenderer != null)
            {
                Content = _modelRenderer;
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

                // Try to find model renderer from XAML
                _modelRenderer = this.FindControl<ModelRenderer>("modelRenderer");
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
            // Create model renderer if not found from XAML
            if (_modelRenderer == null)
            {
                _modelRenderer = new ModelRenderer();
            }
            // Don't set Content here - AddHelpAction will wrap it in a DockPanel if needed
            // Set Content after AddHelpAction is called
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:44-45
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // Signals setup - currently empty in Python implementation
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:47-99
        // Original: def load(self, filepath: os.PathLike | str, resref: str, restype: ResourceType, data: bytes | bytearray):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            byte[] mdlData = null;
            byte[] mdxData = null;

            if (restype == ResourceType.MDL)
            {
                mdlData = data;
                string filepathLower = filepath.ToLowerInvariant();
                if (filepathLower.EndsWith(".mdl"))
                {
                    string mdxPath = Path.ChangeExtension(filepath, ".mdx");
                    if (File.Exists(mdxPath))
                    {
                        mdxData = File.ReadAllBytes(mdxPath);
                    }
                }
                else if (BioWare.NET.Tools.FileHelpers.IsAnyErfTypeFile(filepath))
                {
                    ERF erf = ERFAuto.ReadErf(filepath);
                    mdxData = erf.Get(resref, ResourceType.MDX);
                }
                else if (BioWare.NET.Tools.FileHelpers.IsRimFile(filepath))
                {
                    RIM rim = RIMAuto.ReadRim(filepath);
                    mdxData = rim.Get(resref, ResourceType.MDX);
                }
                else if (BioWare.NET.Tools.FileHelpers.IsBifFile(filepath))
                {
                    if (_installation != null)
                    {
                        var result = _installation.Resource(resref, ResourceType.MDX, new[] { SearchLocation.CHITIN });
                        if (result != null && result.Data != null)
                        {
                            mdxData = result.Data;
                        }
                    }
                }
            }
            else if (restype == ResourceType.MDX)
            {
                mdxData = data;
                string filepathLower = filepath.ToLowerInvariant();
                if (filepathLower.EndsWith(".mdx"))
                {
                    string mdlPath = Path.ChangeExtension(filepath, ".mdl");
                    if (File.Exists(mdlPath))
                    {
                        mdlData = File.ReadAllBytes(mdlPath);
                    }
                }
                else if (BioWare.NET.Tools.FileHelpers.IsAnyErfTypeFile(filepath))
                {
                    ERF erf = ERFAuto.ReadErf(filepath);
                    mdlData = erf.Get(resref, ResourceType.MDL);
                }
                else if (BioWare.NET.Tools.FileHelpers.IsRimFile(filepath))
                {
                    RIM rim = RIMAuto.ReadRim(filepath);
                    mdlData = rim.Get(resref, ResourceType.MDL);
                }
                else if (BioWare.NET.Tools.FileHelpers.IsBifFile(filepath))
                {
                    if (_installation != null)
                    {
                        var result = _installation.Resource(resref, ResourceType.MDL, new[] { SearchLocation.CHITIN });
                        if (result != null && result.Data != null)
                        {
                            mdlData = result.Data;
                        }
                    }
                }
            }

            if (mdlData == null || mdxData == null)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:95
                // Original: QMessageBox(QMessageBox.Icon.Critical, f"Could not find the '{p_filepath.stem}' MDL/MDX", "").exec()
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    $"Could not find the '{resref}' MDL/MDX",
                    "",
                    ButtonEnum.Ok,
                    Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            if (_modelRenderer != null)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:98
                // Original: self.ui.modelRenderer.set_model(mdl_data, mdx_data)
                // IMPLEMENTED: Now properly handles MDL header skipping (data[12:]) like Python implementation
                // The ModelRenderer.SetModel now parses starting at offset 12 to skip the 12-byte file header
                _modelRenderer.SetModel(mdlData, mdxData);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:99
            // Original: self._mdl = read_mdl(mdl_data, 0, 0, mdx_data, 0, 0)
            _mdl = MDLAuto.ReadMdl(mdlData, 0, null, mdxData, 0, 0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:101-108
        // Original: def _loadMDL(self, mdl: MDL):
        private void LoadMDL(MDL mdl)
        {
            _mdl = mdl;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:110-114
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = new byte[0];
            byte[] dataExt = new byte[0];
            using (var ms = new MemoryStream())
            using (var msExt = new MemoryStream())
            {
                MDLAuto.WriteMdl(_mdl, ms, ResourceType.MDL, msExt);
                data = ms.ToArray();
                dataExt = msExt.ToArray();
            }
            return Tuple.Create(data, dataExt);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/mdl.py:116-119
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _mdl = new MDL();
            if (_modelRenderer != null)
            {
                _modelRenderer.ClearModel();
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
