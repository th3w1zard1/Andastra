using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;
using ERFResource = BioWare.NET.Resource.Formats.ERF.ERFResource;
using RIMResource = BioWare.NET.Resource.Formats.RIM.RIMResource;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:97
    // Original: class ERFEditor(Editor):
    public partial class ERFEditor : Editor
    {
        private ObservableCollection<ERFResourceViewModel> _sourceResources;
        private CollectionViewSource _filteredResources;
        private DataGrid _tableView;
        private Button _extractButton;
        private Button _loadButton;
        private Button _unloadButton;
        private Button _openButton;
        private Button _refreshButton;
        private bool _hasChanges;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:98-146
        // Original: def __init__(self, parent, installation):
        public ERFEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "ERF Editor", "none",
                new[] { ResourceType.RIM, ResourceType.ERF, ResourceType.MOD, ResourceType.SAV, ResourceType.BIF },
                new[] { ResourceType.RIM, ResourceType.ERF, ResourceType.MOD, ResourceType.SAV, ResourceType.BIF },
                installation)
        {
            _sourceResources = new ObservableCollection<ERFResourceViewModel>();
            _filteredResources = new CollectionViewSource { Source = _sourceResources };
            _hasChanges = false;

            InitializeComponent();
            // SetupUI only if XAML was loaded (to find controls from XAML)
            // If programmatic UI was used, controls are already set up
            if (_xamlLoaded)
            {
                SetupUI();
            }
            SetupSignals();
            New();
        }

        private bool _xamlLoaded = false;

        private void InitializeComponent()
        {
            try
            {
                AvaloniaXamlLoader.Load(this);
                _xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
                _xamlLoaded = false;
            }

            if (!_xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Button panel
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _extractButton = new Button { Content = "Extract" };
            _loadButton = new Button { Content = "Load" };
            _unloadButton = new Button { Content = "Unload" };
            _openButton = new Button { Content = "Open" };
            _refreshButton = new Button { Content = "Refresh" };
            buttonPanel.Children.Add(_extractButton);
            buttonPanel.Children.Add(_loadButton);
            buttonPanel.Children.Add(_unloadButton);
            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_refreshButton);
            mainPanel.Children.Add(buttonPanel);

            // Table
            _tableView = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Extended
            };
            _tableView.Columns.Add(new DataGridTextColumn
            {
                Header = "ResRef",
                Binding = new Binding("ResRef"),
                IsReadOnly = true
            });
            _tableView.Columns.Add(new DataGridTextColumn
            {
                Header = "Type",
                Binding = new Binding("Type"),
                IsReadOnly = true
            });
            _tableView.Columns.Add(new DataGridTextColumn
            {
                Header = "Size",
                Binding = new Binding("Size"),
                IsReadOnly = true
            });
            _tableView.Columns.Add(new DataGridTextColumn
            {
                Header = "Offset",
                Binding = new Binding("Offset"),
                IsReadOnly = true
            });
            mainPanel.Children.Add(_tableView);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            _tableView = this.FindControl<DataGrid>("tableView");
            _extractButton = this.FindControl<Button>("extractButton");
            _loadButton = this.FindControl<Button>("loadButton");
            _unloadButton = this.FindControl<Button>("unloadButton");
            _openButton = this.FindControl<Button>("openButton");
            _refreshButton = this.FindControl<Button>("refreshButton");

            if (_tableView != null)
            {
                _tableView.ItemsSource = _filteredResources.View;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:175-187
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_extractButton != null)
            {
                _extractButton.Click += (s, e) => ExtractSelected();
            }

            if (_loadButton != null)
            {
                _loadButton.Click += (s, e) => SelectFilesToAdd();
            }

            if (_unloadButton != null)
            {
                _unloadButton.Click += (s, e) => RemoveSelected();
            }

            if (_openButton != null)
            {
                _openButton.Click += (s, e) => OpenSelected();
            }

            if (_refreshButton != null)
            {
                _refreshButton.Click += (s, e) => Refresh();
            }

            if (_tableView != null)
            {
                _tableView.SelectionChanged += (s, e) => OnSelectionChanged();
                _tableView.DoubleTapped += (s, e) => OpenSelected();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:199-255
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            if (_hasChanges && !PromptConfirm())
            {
                return;
            }
            _hasChanges = false;
            base.Load(filepath, resref, restype, data);

            _sourceResources.Clear();

            try
            {
                if (restype == ResourceType.RIM)
                {
                    var rim = RIMAuto.ReadRim(data);
                    int offset = 0;
                    foreach (var resource in rim)
                    {
                        _sourceResources.Add(new ERFResourceViewModel
                        {
                            ResRef = resource.ResRef.ToString(),
                            Type = resource.ResType.Extension.ToUpper(),
                            Size = HumanReadableSize(resource.Data.Length),
                            Offset = $"0x{offset:X}",
                            ErfResource = null,
                            RimResource = resource
                        });
                        offset += resource.Data.Length;
                    }
                }
                else if (restype == ResourceType.ERF || restype == ResourceType.MOD || restype == ResourceType.SAV)
                {
                    var erf = ERFAuto.ReadErf(data);
                    int offset = 0;
                    foreach (var resource in erf)
                    {
                        _sourceResources.Add(new ERFResourceViewModel
                        {
                            ResRef = resource.ResRef.ToString(),
                            Type = resource.ResType.Extension.ToUpper(),
                            Size = HumanReadableSize(resource.Data.Length),
                            Offset = $"0x{offset:X}",
                            ErfResource = resource,
                            RimResource = null
                        });
                        offset += resource.Data.Length;
                    }
                }
                else if (restype == ResourceType.BIF)
                {
                    // BIF support will be added when BIF format is available
                    System.Console.WriteLine("BIF format not yet supported");
                }

                if (_refreshButton != null)
                {
                    _refreshButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load file: {ex}");
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:257-289
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // If restype is not set (e.g., after New()), default to ERF
            ResourceType restype = _restype ?? ResourceType.ERF;
            
            if (restype == ResourceType.RIM)
            {
                var rim = new RIM();
                foreach (var viewModel in _sourceResources)
                {
                    if (viewModel.RimResource != null)
                    {
                        rim.SetData(viewModel.RimResource.ResRef.ToString(), viewModel.RimResource.ResType, viewModel.RimResource.Data);
                    }
                }
                byte[] data = RIMAuto.BytesRim(rim);
                return Tuple.Create(data, new byte[0]);
            }
            else if (restype == ResourceType.ERF || restype == ResourceType.MOD || restype == ResourceType.SAV)
            {
                ERFType erfType = ERFTypeExtensions.FromExtension(restype.Extension);
                var erf = new ERF(erfType);
                if (restype == ResourceType.SAV)
                {
                    erf.IsSaveErf = true;
                }
                foreach (var viewModel in _sourceResources)
                {
                    if (viewModel.ErfResource != null)
                    {
                        erf.SetData(viewModel.ErfResource.ResRef.ToString(), viewModel.ErfResource.ResType, viewModel.ErfResource.Data);
                    }
                }
                byte[] data = ERFAuto.BytesErf(erf, restype);
                return Tuple.Create(data, new byte[0]);
            }
            else
            {
                throw new InvalidOperationException($"Invalid restype for ERFEditor: {restype}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:291-299
        // Original: def new(self):
        public override void New()
        {
            if (_hasChanges && !PromptConfirm())
            {
                return;
            }
            _hasChanges = false;
            base.New();
            // Set default restype to ERF for new files
            _restype = ResourceType.ERF;
            _sourceResources.Clear();
            if (_refreshButton != null)
            {
                _refreshButton.IsEnabled = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:301-328
        // Original: def save(self):
        public override void Save()
        {
            _hasChanges = false;
            if (string.IsNullOrEmpty(_filepath))
            {
                SaveAs();
                return;
            }

            if (_refreshButton != null)
            {
                _refreshButton.IsEnabled = true;
            }

            var (data, _) = Build();
            _revert = data;
            File.WriteAllBytes(_filepath, data);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:376-381
        // Original: def extract_selected(self):
        private void ExtractSelected()
        {
            var selected = GetSelectedResources();
            if (selected.Count == 0)
            {
                return;
            }
            // Extract functionality will be implemented when file dialogs are available
            System.Console.WriteLine($"Extracting {selected.Count} resources");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:467-469
        // Original: def select_files_to_add(self):
        private void SelectFilesToAdd()
        {
            // File selection will be implemented when file dialogs are available
            System.Console.WriteLine("Select files to add");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:430-437
        // Original: def remove_selected(self):
        private void RemoveSelected()
        {
            _hasChanges = true;
            var selected = _tableView?.SelectedItems?.Cast<ERFResourceViewModel>().ToList() ?? new List<ERFResourceViewModel>();
            foreach (var item in selected)
            {
                _sourceResources.Remove(item);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:471-500
        // Original: def open_selected(self, *, gff_specialized=None):
        private void OpenSelected()
        {
            var selected = GetSelectedResources();
            if (selected.Count == 0)
            {
                return;
            }
            // Open in editor functionality will be implemented when WindowUtils is available
            System.Console.WriteLine($"Opening {selected.Count} resources");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:523-538
        // Original: def refresh(self):
        private void Refresh()
        {
            if (_hasChanges && !PromptConfirm())
            {
                return;
            }
            if (string.IsNullOrEmpty(_filepath))
            {
                System.Console.WriteLine("Nothing to refresh - file not loaded");
                return;
            }
            _hasChanges = false;
            byte[] data = File.ReadAllBytes(_filepath);
            Load(_filepath, _resname, _restype, data);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:540-544
        // Original: def on_selection_changed(self):
        private void OnSelectionChanged()
        {
            bool hasSelection = _tableView?.SelectedItems?.Count > 0;
            if (_extractButton != null)
            {
                _extractButton.IsEnabled = hasSelection;
            }
            if (_openButton != null)
            {
                _openButton.IsEnabled = hasSelection;
            }
            if (_unloadButton != null)
            {
                _unloadButton.IsEnabled = hasSelection;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:368-374
        // Original: def get_selected_resources(self) -> list[ERFResource]:
        private List<object> GetSelectedResources()
        {
            var selected = _tableView?.SelectedItems?.Cast<ERFResourceViewModel>().ToList() ?? new List<ERFResourceViewModel>();
            var resources = new List<object>();
            foreach (var vm in selected)
            {
                if (vm.ErfResource != null)
                {
                    resources.Add(vm.ErfResource);
                }
                else if (vm.RimResource != null)
                {
                    resources.Add(vm.RimResource);
                }
            }
            return resources;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:189-197
        // Original: def prompt_confirm(self) -> bool:
        private bool PromptConfirm()
        {
            // Confirmation dialog will be implemented when MessageBox is available
            return true;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/erf.py:71-76
        // Original: def human_readable_size(byte_size: float) -> str:
        private static string HumanReadableSize(double byteSize)
        {
            string[] units = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            foreach (string unit in units)
            {
                if (byteSize < 1024)
                {
                    return $"{Math.Round(byteSize, 2)} {unit}";
                }
                byteSize /= 1024;
            }
            return byteSize.ToString();
        }

        public override void SaveAs()
        {
            Save();
        }
    }

    // ViewModel for ERF resources
    public class ERFResourceViewModel
    {
        public string ResRef { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string Offset { get; set; }
        public ERFResource ErfResource { get; set; }
        public RIMResource RimResource { get; set; }
    }
}
