using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.IO;
using HolocronToolset.Data;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:203
    // Original: class GlobalSettings(Settings):
    public class GlobalSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:GlobalSettings
        // Original: gffSpecializedEditors: SettingsProperty[bool] = SettingsProperty("gffSpecializedEditors", True)
        public SettingsProperty<bool> GffSpecializedEditors { get; } = new SettingsProperty<bool>("GffSpecializedEditors", true);

        public bool UseBetaChannel { get; set; } = false;
        public string SelectedTheme { get; set; } = "fusion (light)";
        public string SelectedStyle { get; set; } = "";
        public int SelectedLanguage { get; set; } = 0; // 0 = English
        public bool JoinRIMsTogether { get; set; } = true;
        public string ExtractPath { get; set; } = "";
        public string NssCompilerPath { get; set; } = "";
        public string NcsDecompilerPath { get; set; } = "";
        public List<string> RecentFiles { get; set; } = new List<string>();
        private bool _firstTime = true;

        public GlobalSettings() : base("Global")
        {
            // Load settings from base class
            GffSpecializedEditors.GetValue(this);
            ExtractPath = GetValue<string>("ExtractPath", "");
            NssCompilerPath = GetValue<string>("NssCompilerPath", "");
            NcsDecompilerPath = GetValue<string>("NcsDecompilerPath", "");
            RecentFiles = GetValue<List<string>>("RecentFiles", new List<string>());
            UseBetaChannel = GetValue<bool>("UseBetaChannel", false);
            SelectedTheme = GetValue<string>("SelectedTheme", "fusion (light)");
            SelectedStyle = GetValue<string>("SelectedStyle", "");
            SelectedLanguage = GetValue<int>("SelectedLanguage", 0);
            JoinRIMsTogether = GetValue<bool>("JoinRIMsTogether", true);
            _firstTime = GetValue<bool>("FirstTime", true);
        }

        public bool GetGffSpecializedEditors()
        {
            return GffSpecializedEditors.GetValue(this);
        }

        public void SetGffSpecializedEditors(bool value)
        {
            GffSpecializedEditors.SetValue(this, value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:207-221
        // Original: def installations(self) -> dict[str, InstallationConfig]:
        public Dictionary<string, Dictionary<string, object>> Installations()
        {
            var installations = GetValue<Dictionary<string, Dictionary<string, object>>>("Installations", new Dictionary<string, Dictionary<string, object>>());

            if (_firstTime)
            {
                _firstTime = false;
                SetValue("FirstTime", false);
                // Auto-detect installations - will be implemented when path detection is available
            }

            return installations;
        }

        public void SetInstallations(Dictionary<string, Dictionary<string, object>> installations)
        {
            SetValue("Installations", installations);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:358
        // Original: showPreviewUTC: SettingsProperty[bool] = Settings.addSetting("showPreviewUTC", ...)
        public bool ShowPreviewUTC
        {
            get => GetValue<bool>("showPreviewUTC", false);
            set => SetValue("showPreviewUTC", value);
        }
    }
}
