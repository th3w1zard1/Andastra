using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;
using BioWare.NET.Common;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:20
    // Original: class ModuleDesignerWidget(SettingsWidget):
    public partial class ModuleDesignerSettingsWidget : UserControl
    {
        private ModuleDesignerSettings _settings;
        private Dictionary<string, SetBindWidget> _binds;
        private Dictionary<string, ColorEdit> _colours;
        private NumericUpDown _fovSpin;
        private NumericUpDown _moveCameraSensitivity3dEdit;
        private NumericUpDown _rotateCameraSensitivity3dEdit;
        private NumericUpDown _zoomCameraSensitivity3dEdit;
        private NumericUpDown _boostedMoveCameraSensitivity3dEdit;
        private NumericUpDown _flySpeedFcEdit;
        private NumericUpDown _rotateCameraSensitivityFcEdit;
        private NumericUpDown _boostedFlyCameraSpeedFCEdit;
        private NumericUpDown _moveCameraSensitivity2dEdit;
        private NumericUpDown _rotateCameraSensitivity2dEdit;
        private NumericUpDown _zoomCameraSensitivity2dEdit;
        private Button _controls3dResetButton;
        private Button _controlsFcResetButton;
        private Button _controls2dResetButton;
        private Button _coloursResetButton;

        public ModuleDesignerSettingsWidget()
        {
            _settings = new ModuleDesignerSettings();
            _binds = new Dictionary<string, SetBindWidget>();
            _colours = new Dictionary<string, ColorEdit>();
            InitializeComponent();
            SetupUI();
            SetupValues();
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
            var panel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            _fovSpin = new NumericUpDown { Minimum = 0, Maximum = 180, Value = 60 };
            panel.Children.Add(new TextBlock { Text = "Field of View:" });
            panel.Children.Add(_fovSpin);

            _controls3dResetButton = new Button { Content = "Reset 3D Controls" };
            _controls3dResetButton.Click += (s, e) => ResetControls3d();
            _controlsFcResetButton = new Button { Content = "Reset Fly Camera Controls" };
            _controlsFcResetButton.Click += (s, e) => ResetControlsFc();
            _controls2dResetButton = new Button { Content = "Reset 2D Controls" };
            _controls2dResetButton.Click += (s, e) => ResetControls2d();
            _coloursResetButton = new Button { Content = "Reset Colours" };
            _coloursResetButton.Click += (s, e) => ResetColours();

            panel.Children.Add(_controls3dResetButton);
            panel.Children.Add(_controlsFcResetButton);
            panel.Children.Add(_controls2dResetButton);
            panel.Children.Add(_coloursResetButton);

            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _fovSpin = this.FindControl<NumericUpDown>("fovSpin");
            _moveCameraSensitivity3dEdit = this.FindControl<NumericUpDown>("moveCameraSensitivity3dEdit");
            _rotateCameraSensitivity3dEdit = this.FindControl<NumericUpDown>("rotateCameraSensitivity3dEdit");
            _zoomCameraSensitivity3dEdit = this.FindControl<NumericUpDown>("zoomCameraSensitivity3dEdit");
            _boostedMoveCameraSensitivity3dEdit = this.FindControl<NumericUpDown>("boostedMoveCameraSensitivity3dEdit");
            _flySpeedFcEdit = this.FindControl<NumericUpDown>("flySpeedFcEdit");
            _rotateCameraSensitivityFcEdit = this.FindControl<NumericUpDown>("rotateCameraSensitivityFcEdit");
            _boostedFlyCameraSpeedFCEdit = this.FindControl<NumericUpDown>("boostedFlyCameraSpeedFCEdit");
            _moveCameraSensitivity2dEdit = this.FindControl<NumericUpDown>("moveCameraSensitivity2dEdit");
            _rotateCameraSensitivity2dEdit = this.FindControl<NumericUpDown>("rotateCameraSensitivity2dEdit");
            _zoomCameraSensitivity2dEdit = this.FindControl<NumericUpDown>("zoomCameraSensitivity2dEdit");
            _controls3dResetButton = this.FindControl<Button>("controls3dResetButton");
            _controlsFcResetButton = this.FindControl<Button>("controlsFcResetButton");
            _controls2dResetButton = this.FindControl<Button>("controls2dResetButton");
            _coloursResetButton = this.FindControl<Button>("coloursResetButton");

            if (_controls3dResetButton != null)
            {
                _controls3dResetButton.Click += (s, e) => ResetControls3d();
            }
            if (_controlsFcResetButton != null)
            {
                _controlsFcResetButton.Click += (s, e) => ResetControlsFc();
            }
            if (_controls2dResetButton != null)
            {
                _controls2dResetButton.Click += (s, e) => ResetControls2d();
            }
            if (_coloursResetButton != null)
            {
                _coloursResetButton.Click += (s, e) => ResetColours();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:76-84
        // Original: def _load3dBindValues(self):
        private void Load3dBindValues()
        {
            if (_moveCameraSensitivity3dEdit != null)
            {
                _moveCameraSensitivity3dEdit.Value = _settings.MoveCameraSensitivity3d.GetValue(_settings);
            }
            if (_rotateCameraSensitivity3dEdit != null)
            {
                _rotateCameraSensitivity3dEdit.Value = _settings.RotateCameraSensitivity3d.GetValue(_settings);
            }
            if (_zoomCameraSensitivity3dEdit != null)
            {
                _zoomCameraSensitivity3dEdit.Value = _settings.ZoomCameraSensitivity3d.GetValue(_settings);
            }
            if (_boostedMoveCameraSensitivity3dEdit != null)
            {
                _boostedMoveCameraSensitivity3dEdit.Value = _settings.BoostedMoveCameraSensitivity3d.GetValue(_settings);
            }

            // Load all 3D bind widgets
            RegisterBindIfExists("speedBoostCamera3dBind", _settings.SpeedBoostCamera3dBind);
            RegisterBindIfExists("moveCameraXY3dBind", _settings.MoveCameraXY3dBind);
            RegisterBindIfExists("moveCameraZ3dBind", _settings.MoveCameraZ3dBind);
            RegisterBindIfExists("moveCameraPlane3dBind", _settings.MoveCameraPlane3dBind);
            RegisterBindIfExists("rotateCamera3dBind", _settings.RotateCamera3dBind);
            RegisterBindIfExists("zoomCamera3dBind", _settings.ZoomCamera3dBind);
            RegisterBindIfExists("zoomCameraMM3dBind", _settings.ZoomCameraMM3dBind);
            RegisterBindIfExists("rotateSelected3dBind", _settings.RotateSelected3dBind);
            RegisterBindIfExists("moveSelectedXY3dBind", _settings.MoveSelectedXY3dBind);
            RegisterBindIfExists("moveSelectedZ3dBind", _settings.MoveSelectedZ3dBind);
            RegisterBindIfExists("rotateObject3dBind", _settings.RotateObject3dBind);
            RegisterBindIfExists("selectObject3dBind", _settings.SelectObject3dBind);
            RegisterBindIfExists("toggleFreeCam3dBind", _settings.ToggleFreeCam3dBind);
            RegisterBindIfExists("deleteObject3dBind", _settings.DeleteObject3dBind);
            RegisterBindIfExists("moveCameraToSelected3dBind", _settings.MoveCameraToSelected3dBind);
            RegisterBindIfExists("moveCameraToCursor3dBind", _settings.MoveCameraToCursor3dBind);
            RegisterBindIfExists("moveCameraToEntryPoint3dBind", _settings.MoveCameraToEntryPoint3dBind);
            RegisterBindIfExists("rotateCameraLeft3dBind", _settings.RotateCameraLeft3dBind);
            RegisterBindIfExists("rotateCameraRight3dBind", _settings.RotateCameraRight3dBind);
            RegisterBindIfExists("rotateCameraUp3dBind", _settings.RotateCameraUp3dBind);
            RegisterBindIfExists("rotateCameraDown3dBind", _settings.RotateCameraDown3dBind);
            RegisterBindIfExists("moveCameraBackward3dBind", _settings.MoveCameraBackward3dBind);
            RegisterBindIfExists("moveCameraForward3dBind", _settings.MoveCameraForward3dBind);
            RegisterBindIfExists("moveCameraLeft3dBind", _settings.MoveCameraLeft3dBind);
            RegisterBindIfExists("moveCameraRight3dBind", _settings.MoveCameraRight3dBind);
            RegisterBindIfExists("moveCameraUp3dBind", _settings.MoveCameraUp3dBind);
            RegisterBindIfExists("moveCameraDown3dBind", _settings.MoveCameraDown3dBind);
            RegisterBindIfExists("zoomCameraIn3dBind", _settings.ZoomCameraIn3dBind);
            RegisterBindIfExists("zoomCameraOut3dBind", _settings.ZoomCameraOut3dBind);
            RegisterBindIfExists("duplicateObject3dBind", _settings.DuplicateObject3dBind);
            RegisterBindIfExists("resetCameraView3dBind", _settings.ResetCameraView3dBind);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:86-93
        // Original: def _loadFcBindValues(self):
        private void LoadFcBindValues()
        {
            if (_flySpeedFcEdit != null)
            {
                _flySpeedFcEdit.Value = _settings.FlyCameraSpeedFC.GetValue(_settings);
            }
            if (_rotateCameraSensitivityFcEdit != null)
            {
                _rotateCameraSensitivityFcEdit.Value = _settings.RotateCameraSensitivityFC.GetValue(_settings);
            }
            if (_boostedFlyCameraSpeedFCEdit != null)
            {
                _boostedFlyCameraSpeedFCEdit.Value = _settings.BoostedFlyCameraSpeedFC.GetValue(_settings);
            }

            // Load all FreeCam bind widgets
            RegisterBindIfExists("speedBoostCameraFcBind", _settings.SpeedBoostCameraFcBind);
            RegisterBindIfExists("moveCameraForwardFcBind", _settings.MoveCameraForwardFcBind);
            RegisterBindIfExists("moveCameraBackwardFcBind", _settings.MoveCameraBackwardFcBind);
            RegisterBindIfExists("moveCameraLeftFcBind", _settings.MoveCameraLeftFcBind);
            RegisterBindIfExists("moveCameraRightFcBind", _settings.MoveCameraRightFcBind);
            RegisterBindIfExists("moveCameraUpFcBind", _settings.MoveCameraUpFcBind);
            RegisterBindIfExists("moveCameraDownFcBind", _settings.MoveCameraDownFcBind);
            RegisterBindIfExists("rotateCameraLeftFcBind", _settings.RotateCameraLeftFcBind);
            RegisterBindIfExists("rotateCameraRightFcBind", _settings.RotateCameraRightFcBind);
            RegisterBindIfExists("rotateCameraUpFcBind", _settings.RotateCameraUpFcBind);
            RegisterBindIfExists("rotateCameraDownFcBind", _settings.RotateCameraDownFcBind);
            RegisterBindIfExists("zoomCameraInFcBind", _settings.ZoomCameraInFcBind);
            RegisterBindIfExists("zoomCameraOutFcBind", _settings.ZoomCameraOutFcBind);
            RegisterBindIfExists("moveCameraToEntryPointFcBind", _settings.MoveCameraToEntryPointFcBind);
            RegisterBindIfExists("moveCameraToCursorFcBind", _settings.MoveCameraToCursorFcBind);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:95-102
        // Original: def _load2dBindValues(self):
        private void Load2dBindValues()
        {
            if (_moveCameraSensitivity2dEdit != null)
            {
                _moveCameraSensitivity2dEdit.Value = _settings.MoveCameraSensitivity2d.GetValue(_settings);
            }
            if (_rotateCameraSensitivity2dEdit != null)
            {
                _rotateCameraSensitivity2dEdit.Value = _settings.RotateCameraSensitivity2d.GetValue(_settings);
            }
            if (_zoomCameraSensitivity2dEdit != null)
            {
                _zoomCameraSensitivity2dEdit.Value = _settings.ZoomCameraSensitivity2d.GetValue(_settings);
            }

            // Load all 2D bind widgets
            RegisterBindIfExists("moveCamera2dBind", _settings.MoveCamera2dBind);
            RegisterBindIfExists("zoomCamera2dBind", _settings.ZoomCamera2dBind);
            RegisterBindIfExists("rotateCamera2dBind", _settings.RotateCamera2dBind);
            RegisterBindIfExists("selectObject2dBind", _settings.SelectObject2dBind);
            RegisterBindIfExists("moveObject2dBind", _settings.MoveObject2dBind);
            RegisterBindIfExists("rotateObject2dBind", _settings.RotateObject2dBind);
            RegisterBindIfExists("deleteObject2dBind", _settings.DeleteObject2dBind);
            RegisterBindIfExists("snapCameraToSelected2dBind", _settings.SnapCameraToSelected2dBind);
            RegisterBindIfExists("duplicateObject2dBind", _settings.DuplicateObject2dBind);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:104-107
        // Original: def _loadColourValues(self):
        private void LoadColourValues()
        {
            // Load all material colour widgets
            RegisterColourIfExists("undefinedMaterialColour", _settings.UndefinedMaterialColour);
            RegisterColourIfExists("dirtMaterialColour", _settings.DirtMaterialColour);
            RegisterColourIfExists("obscuringMaterialColour", _settings.ObscuringMaterialColour);
            RegisterColourIfExists("grassMaterialColour", _settings.GrassMaterialColour);
            RegisterColourIfExists("stoneMaterialColour", _settings.StoneMaterialColour);
            RegisterColourIfExists("woodMaterialColour", _settings.WoodMaterialColour);
            RegisterColourIfExists("waterMaterialColour", _settings.WaterMaterialColour);
            RegisterColourIfExists("nonWalkMaterialColour", _settings.NonWalkMaterialColour);
            RegisterColourIfExists("transparentMaterialColour", _settings.TransparentMaterialColour);
            RegisterColourIfExists("carpetMaterialColour", _settings.CarpetMaterialColour);
            RegisterColourIfExists("metalMaterialColour", _settings.MetalMaterialColour);
            RegisterColourIfExists("puddlesMaterialColour", _settings.PuddlesMaterialColour);
            RegisterColourIfExists("swampMaterialColour", _settings.SwampMaterialColour);
            RegisterColourIfExists("mudMaterialColour", _settings.MudMaterialColour);
            RegisterColourIfExists("leavesMaterialColour", _settings.LeavesMaterialColour);
            RegisterColourIfExists("doorMaterialColour", _settings.DoorMaterialColour);
            RegisterColourIfExists("lavaMaterialColour", _settings.LavaMaterialColour);
            RegisterColourIfExists("bottomlessPitMaterialColour", _settings.BottomlessPitMaterialColour);
            RegisterColourIfExists("deepWaterMaterialColour", _settings.DeepWaterMaterialColour);
            RegisterColourIfExists("nonWalkGrassMaterialColour", _settings.NonWalkGrassMaterialColour);
        }

        // Helper method to register a bind widget if it exists in the UI
        private void RegisterBindIfExists(string bindName, SettingsProperty<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>> settingsProperty)
        {
            var widget = this.FindControl<SetBindWidget>(bindName + "BindEdit");
            if (widget != null)
            {
                var bind = _settings.GetValue(settingsProperty.Name, settingsProperty.Default);
                if (bind == null || bind.Item1 == null || bind.Item2 == null)
                {
                    bind = settingsProperty.Default;
                }
                widget.SetMouseAndKeyBinds(bind);
                _binds[settingsProperty.Name] = widget;
            }
        }

        // Helper method to register a colour widget if it exists in the UI
        private void RegisterColourIfExists(string colourName, SettingsProperty<int> settingsProperty)
        {
            var widget = this.FindControl<ColorEdit>(colourName + "ColourEdit");
            if (widget != null)
            {
                int colorValue = _settings.GetValue(settingsProperty.Name, settingsProperty.Default);
                widget.SetColor(Color.FromRgbaInteger(colorValue));
                _colours[settingsProperty.Name] = widget;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:109-114
        // Original: def setup_values(self):
        private void SetupValues()
        {
            if (_fovSpin != null)
            {
                _fovSpin.Value = _settings.FieldOfView.GetValue(_settings);
            }
            Load3dBindValues();
            LoadFcBindValues();
            Load2dBindValues();
            LoadColourValues();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:116-129
        // Original: def save(self):
        public void Save()
        {
            if (_fovSpin != null)
            {
                _settings.FieldOfView.SetValue(_settings, (int)_fovSpin.Value);
            }

            // Save sensitivity values
            if (_moveCameraSensitivity3dEdit != null)
            {
                _settings.MoveCameraSensitivity3d.SetValue(_settings, (int)_moveCameraSensitivity3dEdit.Value);
            }
            if (_rotateCameraSensitivity3dEdit != null)
            {
                _settings.RotateCameraSensitivity3d.SetValue(_settings, (int)_rotateCameraSensitivity3dEdit.Value);
            }
            if (_zoomCameraSensitivity3dEdit != null)
            {
                _settings.ZoomCameraSensitivity3d.SetValue(_settings, (int)_zoomCameraSensitivity3dEdit.Value);
            }
            if (_boostedMoveCameraSensitivity3dEdit != null)
            {
                _settings.BoostedMoveCameraSensitivity3d.SetValue(_settings, (int)_boostedMoveCameraSensitivity3dEdit.Value);
            }

            if (_flySpeedFcEdit != null)
            {
                _settings.FlyCameraSpeedFC.SetValue(_settings, (int)_flySpeedFcEdit.Value);
            }
            if (_rotateCameraSensitivityFcEdit != null)
            {
                _settings.RotateCameraSensitivityFC.SetValue(_settings, (int)_rotateCameraSensitivityFcEdit.Value);
            }
            if (_boostedFlyCameraSpeedFCEdit != null)
            {
                _settings.BoostedFlyCameraSpeedFC.SetValue(_settings, (int)_boostedFlyCameraSpeedFCEdit.Value);
            }

            if (_moveCameraSensitivity2dEdit != null)
            {
                _settings.MoveCameraSensitivity2d.SetValue(_settings, (int)_moveCameraSensitivity2dEdit.Value);
            }
            if (_rotateCameraSensitivity2dEdit != null)
            {
                _settings.RotateCameraSensitivity2d.SetValue(_settings, (int)_rotateCameraSensitivity2dEdit.Value);
            }
            if (_zoomCameraSensitivity2dEdit != null)
            {
                _settings.ZoomCameraSensitivity2d.SetValue(_settings, (int)_zoomCameraSensitivity2dEdit.Value);
            }

            // Save all bind values
            foreach (var kvp in _binds)
            {
                var bind = kvp.Value.GetMouseAndKeyBinds();
                if (bind != null && bind.Item1 != null && bind.Item2 != null)
                {
                    _settings.SetValue(kvp.Key, bind);
                }
            }

            // Save all colour values
            foreach (var kvp in _colours)
            {
                int colorValue = kvp.Value.GetColor().ToRgbaInteger();
                _settings.SetValue(kvp.Key, colorValue);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:131-133
        // Original: def resetControls3d(self):
        private void ResetControls3d()
        {
            _settings.ResetControls3d();
            Load3dBindValues();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:135-137
        // Original: def resetControlsFc(self):
        private void ResetControlsFc()
        {
            _settings.ResetControlsFc();
            LoadFcBindValues();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:139-141
        // Original: def resetControls2d(self):
        private void ResetControls2d()
        {
            _settings.ResetControls2d();
            Load2dBindValues();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/module_designer.py:143-145
        // Original: def resetColours(self):
        private void ResetColours()
        {
            _settings.ResetMaterialColors();
            LoadColourValues();
        }
    }
}
