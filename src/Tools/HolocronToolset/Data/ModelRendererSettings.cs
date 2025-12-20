using System;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:13
    // Original: class ModelRendererSettings(Settings):
    public class ModelRendererSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:15-16
        // Original: def __init__(self): super().__init__("ModelRenderer")
        public ModelRendererSettings() : base("ModelRenderer")
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:19-22
        // Original: utcShowByDefault: SettingsProperty[bool] = Settings.addSetting("utcShowByDefault", False)
        public bool UtcShowByDefault
        {
            get => GetValue<bool>("utcShowByDefault", false);
            set => SetValue("utcShowByDefault", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:26-29
        // Original: backgroundColour: SettingsProperty[int] = Settings.addSetting("backgroundColour", 0)
        public int BackgroundColour
        {
            get => GetValue<int>("backgroundColour", 0);
            set => SetValue("backgroundColour", value);
        }
    }
}

