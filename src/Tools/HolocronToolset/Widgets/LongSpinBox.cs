using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:14
    // Original: class LongSpinBox(QAbstractSpinBox):
    public partial class LongSpinBox : NumericUpDown
    {
        private long _min = 0;
        private long _max = 0xFFFFFFFF;

        // Public parameterless constructor for XAML
        public LongSpinBox()
        {
            InitializeComponent();
            Minimum = 0;
            Maximum = 0xFFFFFFFF;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:30-34
        // Original: def stepUp/stepDown/stepBy
        public void StepUp()
        {
            long currentValue = GetValue();
            SetValue(currentValue + 1);
        }

        public void StepDown()
        {
            long currentValue = GetValue();
            SetValue(currentValue - 1);
        }

        public void StepBy(int steps)
        {
            long currentValue = GetValue();
            SetValue(currentValue + steps);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:45-47
        // Original: def setRange(self, min_value: int, max_value: int):
        public void SetRange(long minValue, long maxValue)
        {
            _min = minValue;
            _max = maxValue;
            Minimum = minValue;
            Maximum = maxValue;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:49-50
        // Original: def _within_range(self, value: int) -> bool:
        private bool WithinRange(long value)
        {
            return _min <= value && value <= _max;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:52-61
        // Original: def clamp_line_edit(self):
        private void ClampLineEdit()
        {
            if (Value.HasValue)
            {
                long value = (long)Value.Value;
                value = Math.Max(_min, Math.Min(_max, value));
                Value = value;
            }
            else
            {
                Value = 0;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:63-72
        // Original: def setValue(self, value: int):
        public void SetValue(long value)
        {
            value = Math.Max(_min, Math.Min(_max, value));
            Value = value;
            ValueChanged?.Invoke(GetValue());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/long_spinbox.py:74-81
        // Original: def value(self) -> int:
        public long GetValue()
        {
            if (Value.HasValue)
            {
                return (long)Value.Value;
            }
            return 0;
        }
    }
}
