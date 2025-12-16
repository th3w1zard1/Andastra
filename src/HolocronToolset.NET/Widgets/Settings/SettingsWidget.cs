using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Widgets.Edit;
using AuroraEngine.Common.Common;
using SettingsBase = HolocronToolset.NET.Data.Settings;

namespace HolocronToolset.NET.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:32
    // Original: class SettingsWidget(QWidget):
    public abstract class SettingsWidget : UserControl
    {
        protected Dictionary<string, SetBindWidget> _binds;
        protected Dictionary<string, ColorEdit> _colours;
        protected SettingsBase _settings;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:33-44
        // Original: def __init__(self, parent: QWidget):
        protected SettingsWidget()
        {
            _binds = new Dictionary<string, SetBindWidget>();
            _colours = new Dictionary<string, ColorEdit>();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:46-64
        // Original: def installEventFilters(self, parent_widget, event_filter, include_types):
        protected void InstallEventFilters(Control parentWidget, Control includeTypes = null)
        {
            // TODO: Install event filters when event filter system is available
            // This should recursively install NoScrollEventFilter and HoverEventFilter on child widgets
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
        protected int ValidateColour(string colourName, int colorValue)
        {
            // TODO: Validate color value when color validation is available
            return colorValue;
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
            int colorValue = ValidateColour(colourName, _settings.GetValue<int>(colourName, 0));
            widget.SetColor(Color.FromRgbaInteger(colorValue));
            _colours[colourName] = widget;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/base.py:96-100
        // Original: def _reset_and_get_default(self, settingName: str) -> Any:
        private Tuple<HashSet<Key>, HashSet<PointerUpdateKind>> ResetAndGetDefaultBind(string settingName)
        {
            // TODO: Reset setting and get default when SettingsProperty system is fully available
            return Tuple.Create(new HashSet<Key>(), new HashSet<PointerUpdateKind>());
        }
    }
}
