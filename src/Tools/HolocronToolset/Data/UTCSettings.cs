using System;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1163-1181
    // Original: class UTCSettings:
    public class UTCSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1164-1181
        // Original: def __init__(self): self.settings = QSettings("HolocronToolsetV3", "UTCEditor")
        public UTCSettings() : base("UTCEditor")
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1167-1173
        // Original: @property def saveUnusedFields(self) -> bool:
        public bool SaveUnusedFields
        {
            get => GetValue<bool>("saveUnusedFields", true);
            set => SetValue("saveUnusedFields", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:1175-1181
        // Original: @property def alwaysSaveK2Fields(self) -> bool:
        public bool AlwaysSaveK2Fields
        {
            get => GetValue<bool>("alwaysSaveK2Fields", false);
            set => SetValue("alwaysSaveK2Fields", value);
        }
    }
}
