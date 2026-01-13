using BioWare.NET.Common;
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
        private static GlobalSettings _instance;
        private static readonly object _lock = new object();

        public static GlobalSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new GlobalSettings();
                        }
                    }
                }
                return _instance;
            }
        }
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:311
        // Original: app_env_variables: SettingsProperty[dict[str, str]] = Settings.addSetting("EnvironmentVariables", {...})
        public Dictionary<string, string> AppEnvVariables
        {
            get => GetValue("EnvironmentVariables", new Dictionary<string, string>());
            set => SetValue("EnvironmentVariables", value);
        }

        public GlobalSettings() : base("Global")
        {
            // Load settings from base class
            GffSpecializedEditors.GetValue(this);
            ExtractPath = GetValue("ExtractPath", "");
            NssCompilerPath = GetValue("NssCompilerPath", "");
            NcsDecompilerPath = GetValue("NcsDecompilerPath", "");
            RecentFiles = GetValue("RecentFiles", new List<string>());
            UseBetaChannel = GetValue("UseBetaChannel", false);
            SelectedTheme = GetValue("SelectedTheme", "fusion (light)");
            SelectedStyle = GetValue("SelectedStyle", "");
            SelectedLanguage = GetValue("SelectedLanguage", 0);
            JoinRIMsTogether = GetValue("JoinRIMsTogether", true);
            _firstTime = GetValue("FirstTime", true);
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
            var installations = GetValue("Installations", new Dictionary<string, Dictionary<string, object>>());

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
            get => GetValue("showPreviewUTC", false);
            set => SetValue("showPreviewUTC", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py
        // Original: showPreviewUTD: SettingsProperty[bool] = Settings.addSetting("showPreviewUTD", ...)
        public bool ShowPreviewUTD
        {
            get => GetValue("showPreviewUTD", false);
            set => SetValue("showPreviewUTD", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py
        // Original: showPreviewUTP: SettingsProperty[bool] = Settings.addSetting("showPreviewUTP", ...)
        public bool ShowPreviewUTP
        {
            get => GetValue("showPreviewUTP", false);
            set => SetValue("showPreviewUTP", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:60-67
        // Original: settings.value("GlobalFont", "")
        public string GlobalFont
        {
            get => GetValue("GlobalFont", "");
            set => SetValue("GlobalFont", value);
        }
    }
}
