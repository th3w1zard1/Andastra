using System;
using System.Linq;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Layout;
using BioWare.NET;
using BioWare.NET.Resource.Formats.LYT;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:15
    // Original: class RoomPropertiesDialog(QDialog):
    public class RoomPropertiesDialog : Window
    {
        private LYTRoom _room;
        private TextBox _modelInput;
        private NumericUpDown _xSpin;
        private NumericUpDown _ySpin;
        private NumericUpDown _zSpin;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:16-88
        // Original: def __init__(self, room: LYTRoom, parent=None):
        public RoomPropertiesDialog(LYTRoom room, Window parent = null)
        {
            _room = room ?? throw new ArgumentNullException(nameof(room));
            Title = "Room Properties";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetupUI();
        }

        private void SetupUI()
        {
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            // Model Name
            var modelLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var modelLabel = new TextBlock { Text = "Model:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _modelInput = new TextBox { Text = _room.Model ?? "", Width = 200 };
            modelLayout.Children.Add(modelLabel);
            modelLayout.Children.Add(_modelInput);
            mainPanel.Children.Add(modelLayout);

            // Position
            var posLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var posLabel = new TextBlock { Text = "Position:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            posLayout.Children.Add(posLabel);

            var xLabel = new TextBlock { Text = "X:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var pos = _room.Position;
            _xSpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)pos.X, Width = 100 };
            posLayout.Children.Add(xLabel);
            posLayout.Children.Add(_xSpin);

            var yLabel = new TextBlock { Text = "Y:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _ySpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)pos.Y, Width = 100 };
            posLayout.Children.Add(yLabel);
            posLayout.Children.Add(_ySpin);

            var zLabel = new TextBlock { Text = "Z:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _zSpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)pos.Z, Width = 100 };
            posLayout.Children.Add(zLabel);
            posLayout.Children.Add(_zSpin);

            mainPanel.Children.Add(posLayout);

            // Buttons
            var buttonLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75 };
            okButton.Click += (s, e) => Accept();
            var cancelButton = new Button { Content = "Cancel", Width = 75 };
            cancelButton.Click += (s, e) => Close();
            buttonLayout.Children.Add(okButton);
            buttonLayout.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonLayout);

            Content = mainPanel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:74-88
        // Original: def accept(self):
        private void Accept()
        {
            try
            {
                string model = _modelInput?.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(model))
                {
                    // Show error message dialog
                    // Matching PyKotor implementation: Shows error message when model name is empty
                    // Located via string references: QMessageBox usage in PyKotor dialogs
                    // Original implementation: Shows warning message box to user when validation fails
                    var errorDialog = new Window
                    {
                        Title = "Validation Error",
                        Width = 300,
                        Height = 150,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Content = new StackPanel
                        {
                            Margin = new Avalonia.Thickness(20),
                            Children =
                            {
                                new TextBlock { Text = "Model name cannot be empty.", Margin = new Avalonia.Thickness(0, 0, 0, 20) },
                                new Button
                                {
                                    Content = "OK",
                                    HorizontalAlignment = HorizontalAlignment.Right,
                                    Width = 75
                                }
                            }
                        }
                    };
                    var okButton = (errorDialog.Content as StackPanel)?.Children.OfType<Button>().FirstOrDefault();
                    if (okButton != null)
                    {
                        okButton.Click += (s, e) => errorDialog.Close();
                    }
                    errorDialog.ShowDialog(this);
                    return;
                }

                // Update room properties
                _room.Model = model;
                var newPos = new Vector3(
                    (float)(_xSpin?.Value ?? 0),
                    (float)(_ySpin?.Value ?? 0),
                    (float)(_zSpin?.Value ?? 0)
                );
                _room.Position = newPos;

                Close();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to update room properties: {ex}");
            }
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:91
    // Original: class TrackPropertiesDialog(QDialog):
    public class TrackPropertiesDialog : Window
    {
        private LYTTrack _track;
        private TextBox _modelInput;
        private NumericUpDown _xSpin;
        private NumericUpDown _ySpin;
        private NumericUpDown _zSpin;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:92-149
        // Original: def __init__(self, rooms: list[LYTRoom], track: LYTTrack, parent=None):
        public TrackPropertiesDialog(LYTTrack track, Window parent = null)
        {
            _track = track ?? throw new ArgumentNullException(nameof(track));
            Title = "Track Properties";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetupUI();
        }

        private void SetupUI()
        {
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            // Model Name
            var modelLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var modelLabel = new TextBlock { Text = "Model:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _modelInput = new TextBox { Text = _track.Model ?? "", Width = 200 };
            modelLayout.Children.Add(modelLabel);
            modelLayout.Children.Add(_modelInput);
            mainPanel.Children.Add(modelLayout);

            // Position
            var posLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var posLabel = new TextBlock { Text = "Position:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            posLayout.Children.Add(posLabel);

            var xLabel = new TextBlock { Text = "X:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var trackPos = _track.Position;
            _xSpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)trackPos.X, Width = 100 };
            posLayout.Children.Add(xLabel);
            posLayout.Children.Add(_xSpin);

            var yLabel = new TextBlock { Text = "Y:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _ySpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)trackPos.Y, Width = 100 };
            posLayout.Children.Add(yLabel);
            posLayout.Children.Add(_ySpin);

            var zLabel = new TextBlock { Text = "Z:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _zSpin = new NumericUpDown { Minimum = -10000M, Maximum = 10000M, Increment = 0.01M, Value = (decimal)trackPos.Z, Width = 100 };
            posLayout.Children.Add(zLabel);
            posLayout.Children.Add(_zSpin);

            mainPanel.Children.Add(posLayout);

            // Buttons
            var buttonLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = "OK", Width = 75 };
            okButton.Click += (s, e) => Accept();
            var cancelButton = new Button { Content = "Cancel", Width = 75 };
            cancelButton.Click += (s, e) => Close();
            buttonLayout.Children.Add(okButton);
            buttonLayout.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonLayout);

            Content = mainPanel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/lyt_dialogs.py:150-164
        // Original: def accept(self):
        private void Accept()
        {
            try
            {
                string model = _modelInput?.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(model))
                {
                    System.Console.WriteLine("Model name cannot be empty.");
                    return;
                }

                _track.Model = model;
                var newTrackPos = new Vector3(
                    (float)(_xSpin?.Value ?? 0),
                    (float)(_ySpin?.Value ?? 0),
                    (float)(_zSpin?.Value ?? 0)
                );
                _track.Position = newTrackPos;

                Close();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to update track properties: {ex}");
            }
        }
    }
}
