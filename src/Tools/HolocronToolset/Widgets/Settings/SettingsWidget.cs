using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using HolocronToolset.Data;
using HolocronToolset.Widgets.Edit;
using HolocronToolset.Common;
using BioWare.NET.Common;
using Andastra.Utility;
using SettingsBase = HolocronToolset.Data.Settings;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:32
    // Original: class SettingsWidget(QWidget):
    public abstract class SettingsWidget : UserControl
    {
        protected Dictionary<string, SetBindWidget> _binds;
        protected Dictionary<string, ColorEdit> _colours;
        protected SettingsBase _settings;
        protected NoScrollEventFilter _noScrollEventFilter;
        protected HoverEventFilter _hoverEventFilter;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:33-44
        // Original: def __init__(self, parent: QWidget):
        protected SettingsWidget()
        {
            _binds = new Dictionary<string, SetBindWidget>();
            _colours = new Dictionary<string, ColorEdit>();
            
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:40-43
            // Original: self.noScrollEventFilter: NoScrollEventFilter = NoScrollEventFilter(self)
            // Original: self.hoverEventFilter: HoverEventFilter = HoverEventFilter(self)
            // Original: self.installEventFilters(self, self.noScrollEventFilter)
            // Initialize event filters (will be set up when widget is loaded)
            _noScrollEventFilter = new NoScrollEventFilter();
            _hoverEventFilter = new HoverEventFilter();
        }

        // Override OnLoaded to automatically install event filters when widget is loaded
        // This ensures the widget tree is fully constructed before installing filters
        // Matching PyKotor: filters are installed in __init__, but in Avalonia we need to wait for the tree to be ready
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            // Install event filters automatically when widget is loaded
            // Matching PyKotor: self.installEventFilters(self, self.noScrollEventFilter)
            InstallEventFilters(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:46-64
        // Original: def installEventFilters(self, parent_widget, event_filter, include_types):
        /// <summary>
        /// Recursively installs event filters on all child widgets matching the specified types.
        /// This method sets up NoScrollEventFilter and optionally HoverEventFilter on child controls
        /// to prevent scrollbar interaction with controls like ComboBox, Slider, etc.
        /// </summary>
        /// <param name="parentWidget">The parent widget to start installation from (typically 'this')</param>
        /// <param name="includeTypes">Optional array of control types to include. If null, uses default types: ComboBox, Slider, NumericUpDown, CheckBox</param>
        protected void InstallEventFilters(Control parentWidget, Type[] includeTypes = null)
        {
            if (parentWidget == null)
            {
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:46-64
            // Original: def installEventFilters(self, parent_widget, event_filter, include_types):
            // Original: if include_types is None:
            // Original:     include_types = [QComboBox, QSlider, QSpinBox, QGroupBox, QAbstractSpinBox, QDoubleSpinBox]
            // Set default include types if not provided (matching PyKotor default types)
            if (includeTypes == null)
            {
                includeTypes = new[] 
                { 
                    typeof(ComboBox), 
                    typeof(Slider), 
                    typeof(NumericUpDown), 
                    typeof(CheckBox),
                    // Additional types from PyKotor: QGroupBox, QAbstractSpinBox, QDoubleSpinBox
                    // Note: Avalonia equivalents may differ, but these are the core types
                };
            }

            // Install NoScrollEventFilter (primary filter for preventing scrollbar interaction)
            // Matching PyKotor: self.installEventFilters(self, self.noScrollEventFilter)
            // The NoScrollEventFilter.SetupFilter method handles recursive installation
            if (_noScrollEventFilter != null)
            {
                _noScrollEventFilter.SetupFilter(parentWidget, includeTypes);
            }

            // Note: HoverEventFilter installation is commented out in PyKotor (line 44)
            // Original: #self.installEventFilters(self, self.hoverEventFilter, include_types=[QWidget])
            // So we don't install it here, but the instance is available if needed
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:66-70
        // Original: def validateBind(self, bindName: str, bind: Bind) -> Bind:
        protected Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> ValidateBind(string bindName, Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> bind)
        {
            if (bind == null || bind.Item1 == null || bind.Item2 == null)
            {
                System.Console.WriteLine($"Invalid setting bind: '{bindName}', expected a Bind type");
                bind = ResetAndGetDefaultBind(bindName);
            }
            return bind;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:72-76
        // Original: def validateColour(self, colourName: str, color_value: int) -> int:
        protected int ValidateColour(string colourName, object colorValue)
        {
            if (!UtilityMisc.IsInt(colorValue))
            {
                System.Console.WriteLine($"Invalid color setting: '{colourName}', expected a RGBA color integer, but got {colorValue} (type {colorValue?.GetType().Name ?? "null"})");
                return ResetAndGetDefaultColour(colourName);
            }
            // Convert to int if it's a valid integer
            return Convert.ToInt32(colorValue);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:78-84
        // Original: def save(self):
        public virtual void Save()
        {
            foreach (var kvp in _binds)
            {
                var bind = ValidateBind(kvp.Key, kvp.Value.GetMouseAndKeyBinds());
                _settings.SetValue(kvp.Key, bind);
            }
            foreach (var kvp in _colours)
            {
                int colorValue = kvp.Value.GetColor().ToRgbaInteger();
                _settings.SetValue(kvp.Key, colorValue);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:86-89
        // Original: def _registerBind(self, widget: SetBindWidget, bindName: str):
        protected void RegisterBind(SetBindWidget widget, string bindName)
        {
            var bind = ValidateBind(bindName, _settings.GetValue<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>(bindName, null));
            widget.SetMouseAndKeyBinds(bind);
            _binds[bindName] = widget;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:91-94
        // Original: def _registerColour(self, widget: ColorEdit, colourName: str):
        protected void RegisterColour(ColorEdit widget, string colourName)
        {
            // Get raw value from settings (may be any type) and validate it
            object rawValue = _settings.GetValue<object>(colourName, 0);
            int colorValue = ValidateColour(colourName, rawValue);
            widget.SetColor(BioWare.NET.Common.ParsingColor.FromRgbaInteger(colorValue));
            _colours[colourName] = widget;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:96-100
        // Original: def _reset_and_get_default(self, settingName: str) -> Any:
        /// <summary>
        /// Resets a bind setting to its default value and returns the default.
        /// 
        /// This method uses the SettingsProperty system to reset the setting and retrieve
        /// its default value. If the SettingsProperty system is not available for this setting,
        /// it falls back to returning an empty bind tuple.
        /// 
        /// Matching PyKotor: _reset_and_get_default() in base.py
        /// </summary>
        /// <param name="settingName">The name of the bind setting to reset.</param>
        /// <returns>The default bind value (tuple of Key set and PointerUpdateKind set).</returns>
        private Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> ResetAndGetDefaultBind(string settingName)
        {
            try
            {
                // Reset the setting to its default value
                _settings.ResetSetting(settingName);
                
                // Get the default value from the SettingsProperty system
                object defaultValue = _settings.GetDefault(settingName);
                System.Console.WriteLine($"Due to last error, will use default value '{defaultValue}'");
                
                // Convert default value to bind tuple
                if (defaultValue is Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> bindValue)
                {
                    return bindValue;
                }
                
                // Try to deserialize if it's stored as a different format
                // This handles cases where the value might be stored as JSON or another format
                if (defaultValue != null)
                {
                    try
                    {
                        // Try to convert using GetValue with the expected type
                        var convertedValue = _settings.GetValue<Tuple<HashSet<Key>, HashSet<PointerUpdateKind>>>(settingName, null);
                        if (convertedValue != null)
                        {
                            return convertedValue;
                        }
                    }
                    catch
                    {
                        // Conversion failed, fall through to default
                    }
                }
                
                // If default is not a bind tuple, return empty bind as fallback
                System.Console.WriteLine($"Warning: Default value for '{settingName}' is not a bind tuple, using empty bind");
                return Tuple.Create(new HashSet<Key>(), new HashSet<PointerUpdateKind>());
            }
            catch (Exception ex)
            {
                // If ResetSetting or GetDefault fails (e.g., property doesn't use SettingsProperty system),
                // return empty bind as fallback
                System.Console.WriteLine($"Error resetting bind setting '{settingName}': {ex.Message}");
                System.Console.WriteLine($"Warning: SettingsProperty system not available for '{settingName}', using empty bind as fallback");
                return Tuple.Create(new HashSet<Key>(), new HashSet<PointerUpdateKind>());
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:96-100
        // Original: def _reset_and_get_default(self, settingName: str) -> Any:
        private int ResetAndGetDefaultColour(string settingName)
        {
            try
            {
                _settings.ResetSetting(settingName);
                object defaultValue = _settings.GetDefault(settingName);
                System.Console.WriteLine($"Due to last error, will use default value '{defaultValue}'");
                
                // Convert default value to int
                if (defaultValue is int intValue)
                {
                    return intValue;
                }
                if (UtilityMisc.IsInt(defaultValue))
                {
                    return Convert.ToInt32(defaultValue);
                }
                // If default is not an int, return 0 (transparent black)
                System.Console.WriteLine($"Warning: Default value for '{settingName}' is not an integer, using 0");
                return 0;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error resetting color setting '{settingName}': {ex.Message}");
                // Return 0 (transparent black) as fallback
                return 0;
            }
        }
    }
}
