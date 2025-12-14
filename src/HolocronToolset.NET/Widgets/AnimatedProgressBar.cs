using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace HolocronToolset.NET.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/progressbar.py:15
    // Original: class AnimatedProgressBar(QProgressBar):
    public class AnimatedProgressBar : ProgressBar
    {
        private DispatcherTimer _timer;
        private int _offset;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/progressbar.py:16-21
        // Original: def __init__(self, parent=None):
        public AnimatedProgressBar()
        {
            _offset = 0;
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // Update every 50 ms
            };
            _timer.Tick += (s, e) => UpdateAnimation();
            _timer.Start();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/progressbar.py:23-30
        // Original: def update_animation(self):
        private void UpdateAnimation()
        {
            if (Maximum == Minimum)
            {
                return;
            }

            double width = Bounds.Width;
            if (width <= 0)
            {
                return;
            }

            double filledWidth = width * (Value - Minimum) / (Maximum - Minimum);
            if (filledWidth <= 0)
            {
                return;
            }

            _offset = (_offset + 1) % (int)filledWidth;
            InvalidateVisual();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/common/widgets/progressbar.py:32-78
        // Original: def paintEvent(self, event: QPaintEvent):
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Maximum == Minimum)
            {
                return;
            }

            double width = Bounds.Width;
            double height = Bounds.Height;
            double filledWidth = width * (Value - Minimum) / (Maximum - Minimum);
            filledWidth = Math.Max(filledWidth, height); // Ensure minimum width

            if (filledWidth <= 0)
            {
                return;
            }

            // TODO: Implement shimmering effect when custom rendering is available
            // This would draw a moving light effect across the progress bar
            // For now, the base ProgressBar rendering is sufficient
        }
    }
}
