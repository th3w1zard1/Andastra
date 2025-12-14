using System;
using System.Collections.Generic;
using CSharpKOTOR.Common;
using KotorColor = CSharpKOTOR.Common.Color;

namespace HolocronToolset.NET.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:14
    // Original: class GITSettings(Settings):
    public class GITSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:15-22
        // Original: def __init__(self): super().__init__("GITEditor")
        public GITSettings() : base("GITEditor")
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:18-22
        // Original: def resetMaterialColors(self):
        public void ResetMaterialColors()
        {
            // TODO: Reset all material color settings when SettingsProperty system is fully available
            System.Console.WriteLine("Reset material colors not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:24-28
        // Original: def resetControls(self):
        public void ResetControls()
        {
            // TODO: Reset all control bind settings when SettingsProperty system is fully available
            System.Console.WriteLine("Reset controls not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:30-67
        // Original: Instance Labels (Strings)
        public string CreatureLabel
        {
            get => GetValue<string>("CreatureLabel", "");
            set => SetValue("CreatureLabel", value);
        }

        public string DoorLabel
        {
            get => GetValue<string>("DoorLabel", "");
            set => SetValue("DoorLabel", value);
        }

        public string PlaceableLabel
        {
            get => GetValue<string>("PlaceableLabel", "");
            set => SetValue("PlaceableLabel", value);
        }

        public string StoreLabel
        {
            get => GetValue<string>("StoreLabel", "");
            set => SetValue("StoreLabel", value);
        }

        public string SoundLabel
        {
            get => GetValue<string>("SoundLabel", "");
            set => SetValue("SoundLabel", value);
        }

        public string WaypointLabel
        {
            get => GetValue<string>("WaypointLabel", "");
            set => SetValue("WaypointLabel", value);
        }

        public string CameraLabel
        {
            get => GetValue<string>("CameraLabel", "");
            set => SetValue("CameraLabel", value);
        }

        public string EncounterLabel
        {
            get => GetValue<string>("EncounterLabel", "");
            set => SetValue("EncounterLabel", value);
        }

        public string TriggerLabel
        {
            get => GetValue<string>("TriggerLabel", "");
            set => SetValue("TriggerLabel", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:69-150
        // Original: Material Colours (Ints)
        public int UndefinedMaterialColour
        {
            get => GetValue<int>("UndefinedMaterialColour", new KotorColor(0.400f, 0.400f, 0.400f, 0.5f).ToRgbaInteger());
            set => SetValue("UndefinedMaterialColour", value);
        }

        public int DirtMaterialColour
        {
            get => GetValue<int>("DirtMaterialColour", new KotorColor(0.610f, 0.235f, 0.050f, 0.5f).ToRgbaInteger());
            set => SetValue("DirtMaterialColour", value);
        }

        public int ObscuringMaterialColour
        {
            get => GetValue<int>("ObscuringMaterialColour", new KotorColor(0.100f, 0.100f, 0.100f, 0.5f).ToRgbaInteger());
            set => SetValue("ObscuringMaterialColour", value);
        }

        public int GrassMaterialColour
        {
            get => GetValue<int>("GrassMaterialColour", new KotorColor(0.000f, 0.600f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("GrassMaterialColour", value);
        }

        public int StoneMaterialColour
        {
            get => GetValue<int>("StoneMaterialColour", new KotorColor(0.162f, 0.216f, 0.279f, 0.5f).ToRgbaInteger());
            set => SetValue("StoneMaterialColour", value);
        }

        public int WoodMaterialColour
        {
            get => GetValue<int>("WoodMaterialColour", new KotorColor(0.258f, 0.059f, 0.007f, 0.5f).ToRgbaInteger());
            set => SetValue("WoodMaterialColour", value);
        }

        public int WaterMaterialColour
        {
            get => GetValue<int>("WaterMaterialColour", new KotorColor(0.000f, 0.000f, 1.000f, 0.5f).ToRgbaInteger());
            set => SetValue("WaterMaterialColour", value);
        }

        public int NonWalkMaterialColour
        {
            get => GetValue<int>("NonWalkMaterialColour", new KotorColor(1.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("NonWalkMaterialColour", value);
        }

        public int TransparentMaterialColour
        {
            get => GetValue<int>("TransparentMaterialColour", new KotorColor(1.000f, 1.000f, 1.000f, 0.5f).ToRgbaInteger());
            set => SetValue("TransparentMaterialColour", value);
        }

        public int CarpetMaterialColour
        {
            get => GetValue<int>("CarpetMaterialColour", new KotorColor(1.000f, 0.000f, 1.000f, 0.5f).ToRgbaInteger());
            set => SetValue("CarpetMaterialColour", value);
        }

        public int MetalMaterialColour
        {
            get => GetValue<int>("MetalMaterialColour", new KotorColor(0.434f, 0.552f, 0.730f, 0.5f).ToRgbaInteger());
            set => SetValue("MetalMaterialColour", value);
        }

        public int PuddlesMaterialColour
        {
            get => GetValue<int>("PuddlesMaterialColour", new KotorColor(0.509f, 0.474f, 0.147f, 0.5f).ToRgbaInteger());
            set => SetValue("PuddlesMaterialColour", value);
        }

        public int SwampMaterialColour
        {
            get => GetValue<int>("SwampMaterialColour", new KotorColor(0.216f, 0.216f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("SwampMaterialColour", value);
        }

        public int MudMaterialColour
        {
            get => GetValue<int>("MudMaterialColour", new KotorColor(0.091f, 0.147f, 0.028f, 0.5f).ToRgbaInteger());
            set => SetValue("MudMaterialColour", value);
        }

        public int LeavesMaterialColour
        {
            get => GetValue<int>("LeavesMaterialColour", new KotorColor(0.000f, 0.000f, 0.216f, 0.5f).ToRgbaInteger());
            set => SetValue("LeavesMaterialColour", value);
        }

        public int DoorMaterialColour
        {
            get => GetValue<int>("DoorMaterialColour", new KotorColor(0.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("DoorMaterialColour", value);
        }

        public int LavaMaterialColour
        {
            get => GetValue<int>("LavaMaterialColour", new KotorColor(0.300f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("LavaMaterialColour", value);
        }

        public int BottomlessPitMaterialColour
        {
            get => GetValue<int>("BottomlessPitMaterialColour", new KotorColor(0.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("BottomlessPitMaterialColour", value);
        }

        public int DeepWaterMaterialColour
        {
            get => GetValue<int>("DeepWaterMaterialColour", new KotorColor(0.000f, 0.000f, 0.216f, 0.5f).ToRgbaInteger());
            set => SetValue("DeepWaterMaterialColour", value);
        }

        public int NonWalkGrassMaterialColour
        {
            get => GetValue<int>("NonWalkGrassMaterialColour", new KotorColor(0.000f, 0.600f, 0.000f, 0.5f).ToRgbaInteger());
            set => SetValue("NonWalkGrassMaterialColour", value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/editor_settings/git.py:152-189
        // Original: Binds (Controls) - TODO: Implement when SetBindWidget and key/mouse bind system is available
        // These will be implemented as Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> when available
    }
}
