using System;
using System.Collections.Generic;
using Avalonia.Input;
using BioWare.NET.Common;
using KotorColor = BioWare.NET.Common.ParsingColor;

namespace HolocronToolset.Data
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:148
    // Original: class ModuleDesignerSettings(Settings):
    public class ModuleDesignerSettings : Settings
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:149-150
        // Original: def __init__(self): super().__init__("ModuleDesigner")
        public ModuleDesignerSettings() : base("ModuleDesigner")
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:152-159
        // Original: def resetControls3d(self):
        public void ResetControls3d()
        {
            MoveCameraSensitivity3d.ResetToDefault(this);
            RotateCameraSensitivity3d.ResetToDefault(this);
            ZoomCameraSensitivity3d.ResetToDefault(this);
            BoostedMoveCameraSensitivity3d.ResetToDefault(this);
            SpeedBoostCamera3dBind.ResetToDefault(this);
            MoveCameraXY3dBind.ResetToDefault(this);
            MoveCameraZ3dBind.ResetToDefault(this);
            MoveCameraPlane3dBind.ResetToDefault(this);
            RotateCamera3dBind.ResetToDefault(this);
            ZoomCamera3dBind.ResetToDefault(this);
            ZoomCameraMM3dBind.ResetToDefault(this);
            RotateSelected3dBind.ResetToDefault(this);
            MoveSelectedXY3dBind.ResetToDefault(this);
            MoveSelectedZ3dBind.ResetToDefault(this);
            RotateObject3dBind.ResetToDefault(this);
            SelectObject3dBind.ResetToDefault(this);
            ToggleFreeCam3dBind.ResetToDefault(this);
            DeleteObject3dBind.ResetToDefault(this);
            MoveCameraToSelected3dBind.ResetToDefault(this);
            MoveCameraToCursor3dBind.ResetToDefault(this);
            MoveCameraToEntryPoint3dBind.ResetToDefault(this);
            RotateCameraLeft3dBind.ResetToDefault(this);
            RotateCameraRight3dBind.ResetToDefault(this);
            RotateCameraUp3dBind.ResetToDefault(this);
            RotateCameraDown3dBind.ResetToDefault(this);
            MoveCameraBackward3dBind.ResetToDefault(this);
            MoveCameraForward3dBind.ResetToDefault(this);
            MoveCameraLeft3dBind.ResetToDefault(this);
            MoveCameraRight3dBind.ResetToDefault(this);
            MoveCameraUp3dBind.ResetToDefault(this);
            MoveCameraDown3dBind.ResetToDefault(this);
            ZoomCameraIn3dBind.ResetToDefault(this);
            ZoomCameraOut3dBind.ResetToDefault(this);
            DuplicateObject3dBind.ResetToDefault(this);
            ResetCameraView3dBind.ResetToDefault(this);
            ToggleLockInstancesBind.ResetToDefault(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:161-168
        // Original: def resetControls2d(self):
        public void ResetControls2d()
        {
            MoveCameraSensitivity2d.ResetToDefault(this);
            RotateCameraSensitivity2d.ResetToDefault(this);
            ZoomCameraSensitivity2d.ResetToDefault(this);
            MoveCamera2dBind.ResetToDefault(this);
            ZoomCamera2dBind.ResetToDefault(this);
            RotateCamera2dBind.ResetToDefault(this);
            SelectObject2dBind.ResetToDefault(this);
            MoveObject2dBind.ResetToDefault(this);
            RotateObject2dBind.ResetToDefault(this);
            DeleteObject2dBind.ResetToDefault(this);
            SnapCameraToSelected2dBind.ResetToDefault(this);
            DuplicateObject2dBind.ResetToDefault(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:169-176
        // Original: def resetControlsFc(self):
        public void ResetControlsFc()
        {
            RotateCameraSensitivityFC.ResetToDefault(this);
            FlyCameraSpeedFC.ResetToDefault(this);
            BoostedFlyCameraSpeedFC.ResetToDefault(this);
            SpeedBoostCameraFcBind.ResetToDefault(this);
            MoveCameraForwardFcBind.ResetToDefault(this);
            MoveCameraBackwardFcBind.ResetToDefault(this);
            MoveCameraLeftFcBind.ResetToDefault(this);
            MoveCameraRightFcBind.ResetToDefault(this);
            MoveCameraUpFcBind.ResetToDefault(this);
            MoveCameraDownFcBind.ResetToDefault(this);
            RotateCameraLeftFcBind.ResetToDefault(this);
            RotateCameraRightFcBind.ResetToDefault(this);
            RotateCameraUpFcBind.ResetToDefault(this);
            RotateCameraDownFcBind.ResetToDefault(this);
            ZoomCameraInFcBind.ResetToDefault(this);
            ZoomCameraOutFcBind.ResetToDefault(this);
            MoveCameraToEntryPointFcBind.ResetToDefault(this);
            MoveCameraToCursorFcBind.ResetToDefault(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:177-184
        // Original: def resetMaterialColors(self):
        public void ResetMaterialColors()
        {
            UndefinedMaterialColour.ResetToDefault(this);
            DirtMaterialColour.ResetToDefault(this);
            ObscuringMaterialColour.ResetToDefault(this);
            GrassMaterialColour.ResetToDefault(this);
            StoneMaterialColour.ResetToDefault(this);
            WoodMaterialColour.ResetToDefault(this);
            WaterMaterialColour.ResetToDefault(this);
            NonWalkMaterialColour.ResetToDefault(this);
            TransparentMaterialColour.ResetToDefault(this);
            CarpetMaterialColour.ResetToDefault(this);
            MetalMaterialColour.ResetToDefault(this);
            PuddlesMaterialColour.ResetToDefault(this);
            SwampMaterialColour.ResetToDefault(this);
            MudMaterialColour.ResetToDefault(this);
            LeavesMaterialColour.ResetToDefault(this);
            DoorMaterialColour.ResetToDefault(this);
            LavaMaterialColour.ResetToDefault(this);
            BottomlessPitMaterialColour.ResetToDefault(this);
            DeepWaterMaterialColour.ResetToDefault(this);
            NonWalkGrassMaterialColour.ResetToDefault(this);
        }

        // Helper method to create default bind tuple
        private static Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> CreateBind(HashSet<Key> keys, HashSet<PointerUpdateKind> mouseButtons)
        {
            return Tuple.Create(keys ?? new HashSet<Key>(), mouseButtons ?? new HashSet<PointerUpdateKind>());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:185-326
        // Original: region Ints/Binds (Controls - 3D)
        public SettingsProperty<int> MoveCameraSensitivity3d { get; } = new SettingsProperty<int>("moveCameraSensitivity3d", 100);
        public SettingsProperty<int> RotateCameraSensitivity3d { get; } = new SettingsProperty<int>("rotateCameraSensitivity3d", 100);
        public SettingsProperty<int> ZoomCameraSensitivity3d { get; } = new SettingsProperty<int>("zoomCameraSensitivity3d", 100);
        public SettingsProperty<int> BoostedMoveCameraSensitivity3d { get; } = new SettingsProperty<int>("boostedMoveCameraSensitivity3d", 250);

        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> SpeedBoostCamera3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("speedBoostCamera3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftShift }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraXY3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraXY3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftCtrl }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraZ3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraZ3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftCtrl }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraPlane3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraPlane3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.MiddleButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCamera3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCamera3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCamera3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCamera3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCameraMM3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCameraMM3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.RightButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateSelected3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateSelected3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.MiddleButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveSelectedXY3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveSelectedXY3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveSelectedZ3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveSelectedZ3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftShift }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateObject3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateObject3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftAlt }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> SelectObject3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("selectObject3dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ToggleFreeCam3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("toggleFreeCam3dBind", 
                CreateBind(new HashSet<Key> { Key.F }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> DeleteObject3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("deleteObject3dBind", 
                CreateBind(new HashSet<Key> { Key.Delete }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraToSelected3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraToSelected3dBind", 
                CreateBind(new HashSet<Key> { Key.Z }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraToCursor3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraToCursor3dBind", 
                CreateBind(new HashSet<Key> { Key.X }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraToEntryPoint3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraToEntryPoint3dBind", 
                CreateBind(new HashSet<Key> { Key.C }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraLeft3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraLeft3dBind", 
                CreateBind(new HashSet<Key> { Key.D7 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraRight3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraRight3dBind", 
                CreateBind(new HashSet<Key> { Key.D9 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraUp3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraUp3dBind", 
                CreateBind(new HashSet<Key> { Key.D1 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraDown3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraDown3dBind", 
                CreateBind(new HashSet<Key> { Key.D3 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraBackward3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraBackward3dBind", 
                CreateBind(new HashSet<Key> { Key.D2 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraForward3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraForward3dBind", 
                CreateBind(new HashSet<Key> { Key.D8 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraLeft3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraLeft3dBind", 
                CreateBind(new HashSet<Key> { Key.D4 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraRight3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraRight3dBind", 
                CreateBind(new HashSet<Key> { Key.D6 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraUp3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraUp3dBind", 
                CreateBind(new HashSet<Key> { Key.Q }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraDown3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraDown3dBind", 
                CreateBind(new HashSet<Key> { Key.E }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCameraIn3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCameraIn3dBind", 
                CreateBind(new HashSet<Key> { Key.OemPlus }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCameraOut3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCameraOut3dBind", 
                CreateBind(new HashSet<Key> { Key.OemMinus }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> DuplicateObject3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("duplicateObject3dBind", 
                CreateBind(new HashSet<Key> { Key.LeftAlt }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ResetCameraView3dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("resetCameraView3dBind", 
                CreateBind(new HashSet<Key> { Key.Home }, new HashSet<PointerUpdateKind>()));

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:328-402
        // Original: region Int/Binds (Controls - 3D FreeCam)
        public SettingsProperty<int> RotateCameraSensitivityFC { get; } = new SettingsProperty<int>("rotateCameraSensitivityFC", 100);
        public SettingsProperty<int> FlyCameraSpeedFC { get; } = new SettingsProperty<int>("flyCameraSpeedFC", 100);
        public SettingsProperty<int> BoostedFlyCameraSpeedFC { get; } = new SettingsProperty<int>("boostedFlyCameraSpeedFC", 250);

        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> SpeedBoostCameraFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("speedBoostCameraFcBind", 
                CreateBind(new HashSet<Key> { Key.LeftShift }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraForwardFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraForwardFcBind", 
                CreateBind(new HashSet<Key> { Key.W }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraBackwardFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraBackwardFcBind", 
                CreateBind(new HashSet<Key> { Key.S }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraLeftFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraLeftFcBind", 
                CreateBind(new HashSet<Key> { Key.A }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraRightFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraRightFcBind", 
                CreateBind(new HashSet<Key> { Key.D }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraUpFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraUpFcBind", 
                CreateBind(new HashSet<Key> { Key.Q }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraDownFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraDownFcBind", 
                CreateBind(new HashSet<Key> { Key.E }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraLeftFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraLeftFcBind", 
                CreateBind(new HashSet<Key> { Key.D7 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraRightFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraRightFcBind", 
                CreateBind(new HashSet<Key> { Key.D9 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraUpFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraUpFcBind", 
                CreateBind(new HashSet<Key> { Key.D1 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCameraDownFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCameraDownFcBind", 
                CreateBind(new HashSet<Key> { Key.D3 }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCameraInFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCameraInFcBind", 
                CreateBind(new HashSet<Key> { Key.OemPlus }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCameraOutFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCameraOutFcBind", 
                CreateBind(new HashSet<Key> { Key.OemMinus }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraToEntryPointFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraToEntryPointFcBind", 
                CreateBind(new HashSet<Key> { Key.C }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCameraToCursorFcBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCameraToCursorFcBind", 
                CreateBind(new HashSet<Key> { Key.X }, new HashSet<PointerUpdateKind>()));

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:404-454
        // Original: region Int/Binds (Controls - 2D)
        public SettingsProperty<int> MoveCameraSensitivity2d { get; } = new SettingsProperty<int>("moveCameraSensitivity2d", 100);
        public SettingsProperty<int> RotateCameraSensitivity2d { get; } = new SettingsProperty<int>("rotateCameraSensitivity2d", 100);
        public SettingsProperty<int> ZoomCameraSensitivity2d { get; } = new SettingsProperty<int>("zoomCameraSensitivity2d", 100);

        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveCamera2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveCamera2dBind", 
                CreateBind(new HashSet<Key> { Key.LeftCtrl }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ZoomCamera2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("zoomCamera2dBind", 
                CreateBind(new HashSet<Key> { Key.LeftCtrl }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateCamera2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateCamera2dBind", 
                CreateBind(new HashSet<Key> { Key.LeftCtrl }, new HashSet<PointerUpdateKind> { PointerUpdateKind.MiddleButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> SelectObject2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("selectObject2dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> MoveObject2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("moveObject2dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> RotateObject2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("rotateObject2dBind", 
                CreateBind(new HashSet<Key>(), new HashSet<PointerUpdateKind> { PointerUpdateKind.MiddleButtonPressed }));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> DeleteObject2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("deleteObject2dBind", 
                CreateBind(new HashSet<Key> { Key.Delete }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> SnapCameraToSelected2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("snapCameraToSelected2dBind", 
                CreateBind(new HashSet<Key> { Key.Z }, new HashSet<PointerUpdateKind>()));
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> DuplicateObject2dBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("duplicateObject2dBind", 
                CreateBind(new HashSet<Key> { Key.LeftAlt }, new HashSet<PointerUpdateKind> { PointerUpdateKind.LeftButtonPressed }));

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:456-461
        // Original: region Binds (Controls - Both)
        public SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> ToggleLockInstancesBind { get; } = 
            new SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>("toggleLockInstancesBind", 
                CreateBind(new HashSet<Key> { Key.L }, new HashSet<PointerUpdateKind>()));

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:463-544
        // Original: region Ints (Material Colours)
        public SettingsProperty<int> UndefinedMaterialColour { get; } = 
            new SettingsProperty<int>("undefinedMaterialColour", new KotorColor(0.400f, 0.400f, 0.400f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> DirtMaterialColour { get; } = 
            new SettingsProperty<int>("dirtMaterialColour", new KotorColor(0.610f, 0.235f, 0.050f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> ObscuringMaterialColour { get; } = 
            new SettingsProperty<int>("obscuringMaterialColour", new KotorColor(0.100f, 0.100f, 0.100f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> GrassMaterialColour { get; } = 
            new SettingsProperty<int>("grassMaterialColour", new KotorColor(0.000f, 0.600f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> StoneMaterialColour { get; } = 
            new SettingsProperty<int>("stoneMaterialColour", new KotorColor(0.162f, 0.216f, 0.279f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> WoodMaterialColour { get; } = 
            new SettingsProperty<int>("woodMaterialColour", new KotorColor(0.258f, 0.059f, 0.007f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> WaterMaterialColour { get; } = 
            new SettingsProperty<int>("waterMaterialColour", new KotorColor(0.000f, 0.000f, 1.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> NonWalkMaterialColour { get; } = 
            new SettingsProperty<int>("nonWalkMaterialColour", new KotorColor(1.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> TransparentMaterialColour { get; } = 
            new SettingsProperty<int>("transparentMaterialColour", new KotorColor(1.000f, 1.000f, 1.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> CarpetMaterialColour { get; } = 
            new SettingsProperty<int>("carpetMaterialColour", new KotorColor(1.000f, 0.000f, 1.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> MetalMaterialColour { get; } = 
            new SettingsProperty<int>("metalMaterialColour", new KotorColor(0.434f, 0.552f, 0.730f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> PuddlesMaterialColour { get; } = 
            new SettingsProperty<int>("puddlesMaterialColour", new KotorColor(0.509f, 0.474f, 0.147f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> SwampMaterialColour { get; } = 
            new SettingsProperty<int>("swampMaterialColour", new KotorColor(0.216f, 0.216f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> MudMaterialColour { get; } = 
            new SettingsProperty<int>("mudMaterialColour", new KotorColor(0.091f, 0.147f, 0.028f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> LeavesMaterialColour { get; } = 
            new SettingsProperty<int>("leavesMaterialColour", new KotorColor(0.000f, 0.000f, 0.216f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> DoorMaterialColour { get; } = 
            new SettingsProperty<int>("doorMaterialColour", new KotorColor(0.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> LavaMaterialColour { get; } = 
            new SettingsProperty<int>("lavaMaterialColour", new KotorColor(0.300f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> BottomlessPitMaterialColour { get; } = 
            new SettingsProperty<int>("bottomlessPitMaterialColour", new KotorColor(0.000f, 0.000f, 0.000f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> DeepWaterMaterialColour { get; } = 
            new SettingsProperty<int>("deepWaterMaterialColour", new KotorColor(0.000f, 0.000f, 0.216f, 0.5f).ToRgbaInteger());
        public SettingsProperty<int> NonWalkGrassMaterialColour { get; } = 
            new SettingsProperty<int>("nonWalkGrassMaterialColour", new KotorColor(0.000f, 0.600f, 0.000f, 0.5f).ToRgbaInteger());

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:546-551
        // Original: region Ints
        public SettingsProperty<int> FieldOfView { get; } = new SettingsProperty<int>("fieldOfView", 70);
    }
}

