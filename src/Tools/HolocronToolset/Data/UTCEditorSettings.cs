using System;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1163-1181
    // Original: class UTCSettings:
    public class UTCEditorSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1168-1173
        // Original: def saveUnusedFields(self) -> bool:
        public bool SaveUnusedFields
        {
            get => GetValue<bool>("saveUnusedFields", true);
            set => SetValue("saveUnusedFields", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1176-1181
        // Original: def alwaysSaveK2Fields(self) -> bool:
        public bool AlwaysSaveK2Fields
        {
            get => GetValue<bool>("alwaysSaveK2Fields", false);
            set => SetValue("alwaysSaveK2Fields", value);
        }

        public UTCEditorSettings() : base("UTCEditor")
        {
        }
    }
}

