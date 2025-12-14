using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.NET.Widgets.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:13
    // Original: class GFFFieldSpinBox(QSpinBox):
    public partial class GFFFieldSpinBox : NumericUpDown
    {
        private Dictionary<int, string> _specialValueTextMapping;
        private int _minValue;
        private string _lastOperation;
        private int? _cachedValue;

        // Public parameterless constructor for XAML
        public GFFFieldSpinBox()
        {
            InitializeComponent();
            _specialValueTextMapping = new Dictionary<int, string> { { 0, "0" }, { -1, "-1" } };
            _minValue = (int)Minimum;
            Minimum = -2147483646M;
            Maximum = 2147483647M;
            _lastOperation = null;
            _cachedValue = null;
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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:28-30
        // Original: def true_minimum(self) -> int:
        private int TrueMinimum()
        {
            int minValue = (int)Minimum;
            int specialMin = _specialValueTextMapping.Keys.Any() ? _specialValueTextMapping.Keys.Min() : minValue;
            return Math.Min(Math.Min(minValue, specialMin), _minValue);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:32-34
        // Original: def true_maximum(self) -> int:
        private int TrueMaximum()
        {
            int maxValue = (int)Maximum;
            int specialMax = _specialValueTextMapping.Keys.Any() ? _specialValueTextMapping.Keys.Max() : maxValue;
            return Math.Max(maxValue, specialMax);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:36-43
        // Original: def stepBy(self, steps: int):
        public void StepBy(int steps)
        {
            _lastOperation = "stepBy";
            int currentValue = (int)Value;
            _cachedValue = NextValue(currentValue, steps);
            ApplyFinalValue(_cachedValue.HasValue ? _cachedValue.Value : currentValue);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:45-72
        // Original: def _next_value(self, current_value: int, steps: int) -> int:
        private int NextValue(int currentValue, int steps)
        {
            int tentativeNextValue = currentValue + steps;
            int trueMin = TrueMinimum();
            if (tentativeNextValue < trueMin)
            {
                return trueMin;
            }
            int maxVal = (int)Maximum;
            if (tentativeNextValue > maxVal)
            {
                return maxVal;
            }

            var specialValues = _specialValueTextMapping.Keys.OrderBy(x => x).ToList();
            if (steps > 0)
            {
                foreach (int sv in specialValues)
                {
                    if (sv > currentValue && sv <= tentativeNextValue)
                    {
                        return sv;
                    }
                }
                if (_minValue > tentativeNextValue)
                {
                    return _minValue;
                }
                return Math.Min(tentativeNextValue, maxVal);
            }
            if (_minValue <= tentativeNextValue)
            {
                return tentativeNextValue;
            }
            int specialVal = -1;
            foreach (int sv in specialValues.OrderByDescending(x => x))
            {
                if (sv <= tentativeNextValue)
                {
                    return sv;
                }
            }
            return specialVal;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:74-79
        // Original: def text_changed(self, text: str):
        private void OnTextChanged(string text)
        {
            _lastOperation = "textChanged";
            if (int.TryParse(text, out int parsedValue))
            {
                _cachedValue = parsedValue;
            }
            else
            {
                _cachedValue = (int)Value;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:81-87
        // Original: def _apply_final_value(self, value: int):
        private void ApplyFinalValue(int value)
        {
            if (value < TrueMinimum())
            {
                value = TrueMinimum();
            }
            else if (value > TrueMaximum())
            {
                value = TrueMaximum();
            }
            Value = (decimal)value;
            ValueChanged?.Invoke(value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/spinbox.py:89-91
        // Original: def setMinimum(self, value: int):
        public new void SetMinimum(int value)
        {
            _minValue = value;
            base.Minimum = Math.Min(-2, value);
        }

        public event Action<int> ValueChanged;
    }
}
