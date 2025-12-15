using System;
using System.Numerics;

namespace HolocronToolset.NET.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/lyt.py:8
    // Original: class LYTEditorSettings(GITSettings):
    public class LYTEditorSettings : GITSettings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/lyt.py:9-15
        // Original: def __init__(self): super().__init__()
        public LYTEditorSettings() : base()
        {
            GridSize = 1.0f;
            ShowGrid = true;
            ShowRoomLabels = true;
            ShowDoorHookLabels = true;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/lyt.py:12-15
        // Original: LYT-specific settings
        public float GridSize
        {
            get => GetValue<float>("GridSize", 1.0f);
            set => SetValue("GridSize", value);
        }

        public bool ShowGrid
        {
            get => GetValue<bool>("ShowGrid", true);
            set => SetValue("ShowGrid", value);
        }

        public bool ShowRoomLabels
        {
            get => GetValue<bool>("ShowRoomLabels", true);
            set => SetValue("ShowRoomLabels", value);
        }

        public bool ShowDoorHookLabels
        {
            get => GetValue<bool>("ShowDoorHookLabels", true);
            set => SetValue("ShowDoorHookLabels", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/lyt.py:17-22
        // Original: def load(self):
        public void Load()
        {
            GridSize = GetValue<float>("GridSize", 1.0f);
            ShowGrid = GetValue<bool>("ShowGrid", true);
            ShowRoomLabels = GetValue<bool>("ShowRoomLabels", true);
            ShowDoorHookLabels = GetValue<bool>("ShowDoorHookLabels", true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/lyt.py:24-29
        // Original: def save(self):
        public void Save()
        {
            SetValue("GridSize", GridSize);
            SetValue("ShowGrid", ShowGrid);
            SetValue("ShowRoomLabels", ShowRoomLabels);
            SetValue("ShowDoorHookLabels", ShowDoorHookLabels);
        }
    }
}
