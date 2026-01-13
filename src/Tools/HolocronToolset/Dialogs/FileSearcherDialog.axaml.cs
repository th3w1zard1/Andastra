using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using FileResource = BioWare.NET.Extract.FileResource;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:39
    // Original: class FileSearcher(QDialog):
    public partial class FileSearcherDialog : Window
    {
        private Dictionary<string, HTInstallation> _installations;
        private HTInstallation _selectedInstallation;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:55-56
        // Original: self.ui = Ui_Dialog(); self.ui.setupUi(self)
        public FileSearcherDialogUi Ui { get; private set; }

        // Public parameterless constructor for XAML
        public FileSearcherDialog() : this(null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:42-72
        // Original: def __init__(self, parent, installations):
        public FileSearcherDialog(Window parent, Dictionary<string, HTInstallation> installations)
        {
            InitializeComponent();
            _installations = installations ?? new Dictionary<string, HTInstallation>();
            SetupUI();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            Title = "File Search";
            Width = 500;
            Height = 400;

            // Initialize Ui if not already initialized
            if (Ui == null)
            {
                Ui = new FileSearcherDialogUi();
            }

            // Create all UI controls programmatically for test scenarios
            Ui.InstallationSelect = new ComboBox();
            Ui.SearchTextEdit = new TextBox();
            Ui.CaseSensitiveRadio = new RadioButton { Content = "Case Sensitive" };
            Ui.CaseInsensitiveRadio = new RadioButton { Content = "Case Insensitive", IsChecked = true };
            Ui.FilenamesOnlyCheck = new CheckBox { Content = "Filenames Only" };
            Ui.CoreCheck = new CheckBox { Content = "Core" };
            Ui.ModulesCheck = new CheckBox { Content = "Modules" };
            Ui.OverrideCheck = new CheckBox { Content = "Override" };
            Ui.SelectAllCheck = new CheckBox { Content = "Select All" };
            Ui.TypeARECheck = new CheckBox { Content = "ARE" };
            Ui.TypeGITCheck = new CheckBox { Content = "GIT" };
            Ui.TypeIFOCheck = new CheckBox { Content = "IFO" };
            Ui.TypeVISCheck = new CheckBox { Content = "VIS" };
            Ui.TypeLYTCheck = new CheckBox { Content = "LYT" };
            Ui.TypeDLGCheck = new CheckBox { Content = "DLG" };
            Ui.TypeJRLCheck = new CheckBox { Content = "JRL" };
            Ui.TypeUTCCheck = new CheckBox { Content = "UTC" };
            Ui.TypeUTDCheck = new CheckBox { Content = "UTD" };
            Ui.TypeUTECheck = new CheckBox { Content = "UTE" };
            Ui.TypeUTICheck = new CheckBox { Content = "UTI" };
            Ui.TypeUTPCheck = new CheckBox { Content = "UTP" };
            Ui.TypeUTMCheck = new CheckBox { Content = "UTM" };
            Ui.TypeUTSCheck = new CheckBox { Content = "UTS" };
            Ui.TypeUTTCheck = new CheckBox { Content = "UTT" };
            Ui.TypeUTWCheck = new CheckBox { Content = "UTW" };
            Ui.Type2DACheck = new CheckBox { Content = "2DA" };
            Ui.TypeNSSCheck = new CheckBox { Content = "NSS" };
            Ui.TypeNCSCheck = new CheckBox { Content = "NCS" };
            Ui.SearchButton = new Button { Content = "Search" };
            Ui.CancelButton = new Button { Content = "Cancel" };

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "File Search",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(titleLabel);
            panel.Children.Add(Ui.InstallationSelect);
            panel.Children.Add(Ui.SearchTextEdit);
            panel.Children.Add(Ui.CaseSensitiveRadio);
            panel.Children.Add(Ui.CaseInsensitiveRadio);
            panel.Children.Add(Ui.FilenamesOnlyCheck);
            panel.Children.Add(Ui.CoreCheck);
            panel.Children.Add(Ui.ModulesCheck);
            panel.Children.Add(Ui.OverrideCheck);
            panel.Children.Add(Ui.SelectAllCheck);
            panel.Children.Add(Ui.TypeARECheck);
            panel.Children.Add(Ui.TypeGITCheck);
            panel.Children.Add(Ui.TypeIFOCheck);
            panel.Children.Add(Ui.TypeVISCheck);
            panel.Children.Add(Ui.TypeLYTCheck);
            panel.Children.Add(Ui.TypeDLGCheck);
            panel.Children.Add(Ui.TypeJRLCheck);
            panel.Children.Add(Ui.TypeUTCCheck);
            panel.Children.Add(Ui.TypeUTDCheck);
            panel.Children.Add(Ui.TypeUTECheck);
            panel.Children.Add(Ui.TypeUTICheck);
            panel.Children.Add(Ui.TypeUTPCheck);
            panel.Children.Add(Ui.TypeUTMCheck);
            panel.Children.Add(Ui.TypeUTSCheck);
            panel.Children.Add(Ui.TypeUTTCheck);
            panel.Children.Add(Ui.TypeUTWCheck);
            panel.Children.Add(Ui.Type2DACheck);
            panel.Children.Add(Ui.TypeNSSCheck);
            panel.Children.Add(Ui.TypeNCSCheck);
            panel.Children.Add(Ui.SearchButton);
            panel.Children.Add(Ui.CancelButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:55-56
            // Original: self.ui = Ui_Dialog(); self.ui.setupUi(self)
            // Find all controls from XAML and expose via Ui property
            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            Ui = new FileSearcherDialogUi();
            
            try
            {
                Ui.InstallationSelect = this.FindControl<ComboBox>("installationSelect");
                Ui.SearchTextEdit = this.FindControl<TextBox>("searchTextEdit");
                Ui.CaseSensitiveRadio = this.FindControl<RadioButton>("caseSensitiveRadio");
                Ui.CaseInsensitiveRadio = this.FindControl<RadioButton>("caseInsensitiveRadio");
                Ui.FilenamesOnlyCheck = this.FindControl<CheckBox>("filenamesOnlyCheck");
                Ui.CoreCheck = this.FindControl<CheckBox>("coreCheck");
                Ui.ModulesCheck = this.FindControl<CheckBox>("modulesCheck");
                Ui.OverrideCheck = this.FindControl<CheckBox>("overrideCheck");
                Ui.SelectAllCheck = this.FindControl<CheckBox>("selectAllCheck");
                Ui.TypeARECheck = this.FindControl<CheckBox>("typeARECheck");
                Ui.TypeGITCheck = this.FindControl<CheckBox>("typeGITCheck");
                Ui.TypeIFOCheck = this.FindControl<CheckBox>("typeIFOCheck");
                Ui.TypeVISCheck = this.FindControl<CheckBox>("typeVISCheck");
                Ui.TypeLYTCheck = this.FindControl<CheckBox>("typeLYTCheck");
                Ui.TypeDLGCheck = this.FindControl<CheckBox>("typeDLGCheck");
                Ui.TypeJRLCheck = this.FindControl<CheckBox>("typeJRLCheck");
                Ui.TypeUTCCheck = this.FindControl<CheckBox>("typeUTCCheck");
                Ui.TypeUTDCheck = this.FindControl<CheckBox>("typeUTDCheck");
                Ui.TypeUTECheck = this.FindControl<CheckBox>("typeUTECheck");
                Ui.TypeUTICheck = this.FindControl<CheckBox>("typeUTICheck");
                Ui.TypeUTPCheck = this.FindControl<CheckBox>("typeUTPCheck");
                Ui.TypeUTMCheck = this.FindControl<CheckBox>("typeUTMCheck");
                Ui.TypeUTSCheck = this.FindControl<CheckBox>("typeUTSCheck");
                Ui.TypeUTTCheck = this.FindControl<CheckBox>("typeUTTCheck");
                Ui.TypeUTWCheck = this.FindControl<CheckBox>("typeUTWCheck");
                Ui.Type2DACheck = this.FindControl<CheckBox>("type2DACheck");
                Ui.TypeNSSCheck = this.FindControl<CheckBox>("typeNSSCheck");
                Ui.TypeNCSCheck = this.FindControl<CheckBox>("typeNCSCheck");
                Ui.SearchButton = this.FindControl<Button>("searchButton");
                Ui.CancelButton = this.FindControl<Button>("cancelButton");
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:64-72
            // Original: Setup installations in combo box - store installation as data
            if (Ui.InstallationSelect != null && _installations != null)
            {
                Ui.InstallationSelect.Items.Clear();
                foreach (var kvp in _installations)
                {
                    // Store installation as data, display name as text
                    Ui.InstallationSelect.Items.Add(new ComboBoxItem
                    {
                        Content = kvp.Key,
                        Tag = kvp.Value
                    });
                }
                if (Ui.InstallationSelect.Items.Count > 0)
                {
                    Ui.InstallationSelect.SelectedIndex = 0;
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:70-71
            // Original: Connect Select All checkbox
            if (Ui.SelectAllCheck != null)
            {
                Ui.SelectAllCheck.IsCheckedChanged += (sender, e) => ToggleAllCheckboxes(Ui.SelectAllCheck.IsChecked ?? false);
            }

            // Connect search button
            if (Ui.SearchButton != null)
            {
                Ui.SearchButton.Click += (sender, e) => OnSearch();
            }

            // Connect cancel button
            if (Ui.CancelButton != null)
            {
                Ui.CancelButton.Click += (sender, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:73-97
        // Original: def toggle_all_checkboxes(self, state: Qt.CheckState):
        private void ToggleAllCheckboxes(bool checkState)
        {
            if (Ui == null)
            {
                return;
            }

            var checkBoxes = new[]
            {
                Ui.TypeARECheck,
                Ui.TypeGITCheck,
                Ui.TypeIFOCheck,
                Ui.TypeVISCheck,
                Ui.TypeLYTCheck,
                Ui.TypeDLGCheck,
                Ui.TypeJRLCheck,
                Ui.TypeUTCCheck,
                Ui.TypeUTDCheck,
                Ui.TypeUTECheck,
                Ui.TypeUTICheck,
                Ui.TypeUTPCheck,
                Ui.TypeUTMCheck,
                Ui.TypeUTWCheck,
                Ui.TypeUTSCheck,
                Ui.TypeUTTCheck,
                Ui.Type2DACheck,
                Ui.TypeNSSCheck,
                Ui.TypeNCSCheck
            };

            foreach (var checkBox in checkBoxes)
            {
                if (checkBox != null)
                {
                    checkBox.IsChecked = checkState;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:99-149
        // Original: def accept(self):
        private void OnSearch()
        {
            if (Ui == null)
            {
                return;
            }

            if (Ui.InstallationSelect == null || Ui.InstallationSelect.SelectedItem == null)
            {
                return;
            }

            // Get installation from ComboBoxItem.Tag
            HTInstallation installation = null;
            if (Ui.InstallationSelect.SelectedItem is ComboBoxItem item && item.Tag is HTInstallation inst)
            {
                installation = inst;
            }
            else
            {
                // Fallback: try to get by name
                string selectedInstallationName = Ui.InstallationSelect.SelectedItem.ToString();
                if (_installations.ContainsKey(selectedInstallationName))
                {
                    installation = _installations[selectedInstallationName];
                }
            }

            if (installation == null)
            {
                return;
            }
            _selectedInstallation = installation;

            var checkTypes = new List<ResourceType>();
            if (Ui.TypeARECheck?.IsChecked == true) checkTypes.Add(ResourceType.ARE);
            if (Ui.TypeGITCheck?.IsChecked == true) checkTypes.Add(ResourceType.GIT);
            if (Ui.TypeIFOCheck?.IsChecked == true) checkTypes.Add(ResourceType.IFO);
            if (Ui.TypeVISCheck?.IsChecked == true) checkTypes.Add(ResourceType.VIS);
            if (Ui.TypeLYTCheck?.IsChecked == true) checkTypes.Add(ResourceType.LYT);
            if (Ui.TypeDLGCheck?.IsChecked == true) checkTypes.Add(ResourceType.DLG);
            if (Ui.TypeJRLCheck?.IsChecked == true) checkTypes.Add(ResourceType.JRL);
            if (Ui.TypeUTCCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTC);
            if (Ui.TypeUTDCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTD);
            if (Ui.TypeUTECheck?.IsChecked == true) checkTypes.Add(ResourceType.UTE);
            if (Ui.TypeUTICheck?.IsChecked == true) checkTypes.Add(ResourceType.UTI);
            if (Ui.TypeUTPCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTP);
            if (Ui.TypeUTMCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTM);
            if (Ui.TypeUTWCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTW);
            if (Ui.TypeUTSCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTS);
            if (Ui.TypeUTTCheck?.IsChecked == true) checkTypes.Add(ResourceType.UTT);
            if (Ui.Type2DACheck?.IsChecked == true) checkTypes.Add(ResourceType.TwoDA);
            if (Ui.TypeNSSCheck?.IsChecked == true) checkTypes.Add(ResourceType.NSS);
            if (Ui.TypeNCSCheck?.IsChecked == true) checkTypes.Add(ResourceType.NCS);

            var query = new FileSearchQuery
            {
                Installation = installation,
                CaseSensitive = Ui.CaseSensitiveRadio?.IsChecked ?? false,
                FilenamesOnly = Ui.FilenamesOnlyCheck?.IsChecked ?? false,
                Text = Ui.SearchTextEdit?.Text ?? "",
                SearchCore = Ui.CoreCheck?.IsChecked ?? false,
                SearchModules = Ui.ModulesCheck?.IsChecked ?? false,
                SearchOverride = Ui.OverrideCheck?.IsChecked ?? false,
                CheckTypes = checkTypes
            };

            Search(query);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:151-191
        // Original: def search(self, query):
        public void Search(FileSearchQuery query)
        {
            var results = new List<FileResource>();

            // Search core resources
            if (query.SearchCore)
            {
                results.AddRange(query.Installation.CoreResources());
            }

            // Search modules
            if (query.SearchModules)
            {
                var moduleNames = query.Installation.ModuleNames();
                foreach (var moduleName in moduleNames.Keys)
                {
                    results.AddRange(query.Installation.ModuleResources(moduleName));
                }
            }

            // Search override
            if (query.SearchOverride)
            {
                var overrideList = query.Installation.OverrideList();
                foreach (var folder in overrideList)
                {
                    results.AddRange(query.Installation.OverrideResources(folder));
                }
            }

            // Filter by search text
            if (!string.IsNullOrEmpty(query.Text))
            {
                string searchText = query.CaseSensitive ? query.Text : query.Text.ToLowerInvariant();
                results = results.Where(r =>
                {
                    string resName = query.CaseSensitive ? r.ResName : r.ResName.ToLowerInvariant();
                    if (resName.Contains(searchText))
                    {
                        return true;
                    }

                    if (query.FilenamesOnly)
                    {
                        return false;
                    }

                    if (!query.CheckTypes.Contains(r.ResType))
                    {
                        return false;
                    }

                    // Search in resource data
                    try
                    {
                        byte[] data = r.GetData();
                        string dataText = System.Text.Encoding.ASCII.GetString(data);
                        string dataTextLower = query.CaseSensitive ? dataText : dataText.ToLowerInvariant();
                        return dataTextLower.Contains(searchText);
                    }
                    catch
                    {
                        return false;
                    }
                }).ToList();
            }

            // Filter by resource types
            if (query.CheckTypes != null && query.CheckTypes.Count > 0)
            {
                results = results.Where(r => query.CheckTypes.Contains(r.ResType)).ToList();
            }

            OnFileResults(results, query.Installation);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:39-40
        // Original: file_results = Signal(list, HTInstallation)
        public event Action<List<FileResource>, HTInstallation> FileResults;

        private void OnFileResults(List<FileResource> results, HTInstallation installation)
        {
            FileResults?.Invoke(results, installation);
        }

        // Helper method to get current installation from ComboBox (for tests)
        public HTInstallation GetCurrentInstallation()
        {
            if (Ui?.InstallationSelect?.SelectedItem is ComboBoxItem item && item.Tag is HTInstallation inst)
            {
                return inst;
            }
            return null;
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:55-56
    // Original: self.ui = Ui_Dialog() - UI wrapper class exposing all controls
    public class FileSearcherDialogUi
    {
        public ComboBox InstallationSelect { get; set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:222
        // Original: dialog.ui.installationSelect.currentData() - helper to get current installation
        public HTInstallation GetCurrentInstallation()
        {
            if (InstallationSelect?.SelectedItem is ComboBoxItem item && item.Tag is HTInstallation inst)
            {
                return inst;
            }
            return null;
        }
        public TextBox SearchTextEdit { get; set; }
        public RadioButton CaseSensitiveRadio { get; set; }
        public RadioButton CaseInsensitiveRadio { get; set; }
        public CheckBox FilenamesOnlyCheck { get; set; }
        public CheckBox CoreCheck { get; set; }
        public CheckBox ModulesCheck { get; set; }
        public CheckBox OverrideCheck { get; set; }
        public CheckBox SelectAllCheck { get; set; }
        public CheckBox TypeARECheck { get; set; }
        public CheckBox TypeGITCheck { get; set; }
        public CheckBox TypeIFOCheck { get; set; }
        public CheckBox TypeVISCheck { get; set; }
        public CheckBox TypeLYTCheck { get; set; }
        public CheckBox TypeDLGCheck { get; set; }
        public CheckBox TypeJRLCheck { get; set; }
        public CheckBox TypeUTCCheck { get; set; }
        public CheckBox TypeUTDCheck { get; set; }
        public CheckBox TypeUTECheck { get; set; }
        public CheckBox TypeUTICheck { get; set; }
        public CheckBox TypeUTPCheck { get; set; }
        public CheckBox TypeUTMCheck { get; set; }
        public CheckBox TypeUTSCheck { get; set; }
        public CheckBox TypeUTTCheck { get; set; }
        public CheckBox TypeUTWCheck { get; set; }
        public CheckBox Type2DACheck { get; set; }
        public CheckBox TypeNSSCheck { get; set; }
        public CheckBox TypeNCSCheck { get; set; }
        public Button SearchButton { get; set; }
        public Button CancelButton { get; set; }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/search.py:25-36
    // Original: @dataclass class FileSearchQuery:
    public class FileSearchQuery
    {
        public HTInstallation Installation { get; set; }
        public bool CaseSensitive { get; set; }
        public bool FilenamesOnly { get; set; }
        public string Text { get; set; }
        public bool SearchCore { get; set; }
        public bool SearchModules { get; set; }
        public bool SearchOverride { get; set; }
        public List<ResourceType> CheckTypes { get; set; }

        public FileSearchQuery()
        {
            CheckTypes = new List<ResourceType>();
        }
    }
}
