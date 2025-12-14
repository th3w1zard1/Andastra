using System;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/misc.py
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

        public GlobalSettings() : base("Global")
        {
            // Load settings from base class
            GffSpecializedEditors.GetValue(this);
        }

        public bool GetGffSpecializedEditors()
        {
            return GffSpecializedEditors.GetValue(this);
        }

        public void SetGffSpecializedEditors(bool value)
        {
            GffSpecializedEditors.SetValue(this, value);
        }
    }
}
