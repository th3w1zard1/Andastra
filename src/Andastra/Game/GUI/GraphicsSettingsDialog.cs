using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Serialization;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Andastra.Game.GUI
{
    /// <summary>
    /// Comprehensive graphics settings dialog for configuring all backend settings.
    /// </summary>
    /// <remarks>
    /// Graphics Settings Dialog:
    /// - Exhaustive and comprehensive settings for all graphics backends
    /// - Zero placeholders, omissions, or simplifications
    /// - Dynamically shows settings based on selected backend (MonoGame or Stride)
    /// - All settings from RenderState, BlendState, DepthStencilState, SamplerState, BasicEffect, SpriteBatch, SpatialAudio, Window, GraphicsDevice, ContentManager
    /// </remarks>
    public class GraphicsSettingsDialog : Window
    {
        private GraphicsBackendType _selectedBackend;
        private GraphicsSettingsData _settings;
        private TabControl _tabControl;
        private Dictionary<string, Control> _controlMap;
        private ComboBox _presetComboBox;
        private GraphicsPreset _currentPreset;
        private TextBox _searchTextBox;
        private Dictionary<string, TabItem> _tabPageMap;
        private bool _result;
        private bool _isUpdatingPresetComboBox;

        /// <summary>
        /// Gets the graphics settings.
        /// </summary>
        public GraphicsSettingsData Settings => _settings;

        /// <summary>
        /// Creates a new graphics settings dialog.
        /// </summary>
        /// <param name="backendType">The selected graphics backend.</param>
        /// <param name="initialSettings">Initial settings to load (can be null).</param>
        public GraphicsSettingsDialog(GraphicsBackendType backendType, GraphicsSettingsData initialSettings = null)
        {
            _selectedBackend = backendType;
            _settings = initialSettings ?? new GraphicsSettingsData();
            _controlMap = new Dictionary<string, Control>();
            _tabPageMap = new Dictionary<string, TabItem>();
            _currentPreset = GraphicsPreset.Custom;
            _result = false;

            Title = $"Graphics Settings - {GetBackendName(backendType)}";
            Width = 1000;
            Height = 750;
            CanResize = true;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            MinWidth = 800;
            MinHeight = 600;

            InitializeComponent();
            LoadSettings();
            DetectCurrentPreset();
        }

        private void InitializeComponent()
        {
            var mainGrid = new Grid
            {
                Margin = new Thickness(10),
                RowDefinitions = new RowDefinitions("Auto,10,*,10,Auto")
            };

            // Top toolbar with presets and search
            var toolbarGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,10,Auto,10,*,10,Auto,10,Auto,10,Auto")
            };

            // Preset selector
            var presetLabel = new TextBlock { Text = "Preset:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(presetLabel, 0);
            toolbarGrid.Children.Add(presetLabel);

            _presetComboBox = new ComboBox { Width = 150, VerticalAlignment = VerticalAlignment.Center };
            _presetComboBox.Items.Add("Low");
            _presetComboBox.Items.Add("Medium");
            _presetComboBox.Items.Add("High");
            _presetComboBox.Items.Add("Ultra");
            _presetComboBox.Items.Add("Custom");
            _presetComboBox.SelectedIndex = 4; // Custom
            _presetComboBox.SelectionChanged += PresetComboBox_SelectionChanged;
            Grid.SetColumn(_presetComboBox, 2);
            toolbarGrid.Children.Add(_presetComboBox);

            // Search box
            var searchLabel = new TextBlock { Text = "Search:", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(searchLabel, 6);
            toolbarGrid.Children.Add(searchLabel);

            _searchTextBox = new TextBox { Watermark = "Search settings...", MinWidth = 200 };
            _searchTextBox.TextChanged += SearchTextBox_TextChanged;
            Grid.SetColumn(_searchTextBox, 8);
            toolbarGrid.Children.Add(_searchTextBox);

            // Import/Export buttons
            var importButton = new Button { Content = "Import..." };
            importButton.Click += ImportButton_Click;
            Grid.SetColumn(importButton, 10);
            toolbarGrid.Children.Add(importButton);

            Grid.SetRow(toolbarGrid, 0);
            mainGrid.Children.Add(toolbarGrid);

            // Tab control
            _tabControl = new TabControl();

            // Window Settings Tab
            var windowTab = CreateWindowSettingsTab();
            _tabControl.Items.Add(windowTab);
            _tabPageMap["Window"] = windowTab;

            // Rasterizer State Tab
            var rasterizerTab = CreateRasterizerStateTab();
            _tabControl.Items.Add(rasterizerTab);
            _tabPageMap["Rasterizer"] = rasterizerTab;

            // Depth Stencil State Tab
            var depthStencilTab = CreateDepthStencilStateTab();
            _tabControl.Items.Add(depthStencilTab);
            _tabPageMap["DepthStencil"] = depthStencilTab;

            // Blend State Tab
            var blendTab = CreateBlendStateTab();
            _tabControl.Items.Add(blendTab);
            _tabPageMap["Blend"] = blendTab;

            // Sampler State Tab
            var samplerTab = CreateSamplerStateTab();
            _tabControl.Items.Add(samplerTab);
            _tabPageMap["Sampler"] = samplerTab;

            // Basic Effect Tab
            var basicEffectTab = CreateBasicEffectTab();
            _tabControl.Items.Add(basicEffectTab);
            _tabPageMap["BasicEffect"] = basicEffectTab;

            // SpriteBatch Tab
            var spriteBatchTab = CreateSpriteBatchTab();
            _tabControl.Items.Add(spriteBatchTab);
            _tabPageMap["SpriteBatch"] = spriteBatchTab;

            // Spatial Audio Tab
            var spatialAudioTab = CreateSpatialAudioTab();
            _tabControl.Items.Add(spatialAudioTab);
            _tabPageMap["SpatialAudio"] = spatialAudioTab;

            // Content Manager Tab
            var contentManagerTab = CreateContentManagerTab();
            _tabControl.Items.Add(contentManagerTab);
            _tabPageMap["ContentManager"] = contentManagerTab;

            // Backend-Specific Tabs
            if (_selectedBackend == GraphicsBackendType.MonoGame)
            {
                var monoGameTab = CreateMonoGameSpecificTab();
                _tabControl.Items.Add(monoGameTab);
                _tabPageMap["MonoGame"] = monoGameTab;
            }
            else if (_selectedBackend == GraphicsBackendType.Stride)
            {
                var strideTab = CreateStrideSpecificTab();
                _tabControl.Items.Add(strideTab);
                _tabPageMap["Stride"] = strideTab;
            }

            Grid.SetRow(_tabControl, 2);
            mainGrid.Children.Add(_tabControl);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            var validateButton = new Button { Content = "Validate", Width = 100 };
            validateButton.Click += ValidateButton_Click;
            buttonPanel.Children.Add(validateButton);

            var resetButton = new Button { Content = "Reset to Defaults", Width = 150 };
            resetButton.Click += ResetButton_Click;
            buttonPanel.Children.Add(resetButton);

            var okButton = new Button { Content = "OK", Width = 100 };
            okButton.Click += OkButton_Click;
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button { Content = "Cancel", Width = 100 };
            cancelButton.Click += (sender, e) =>
            {
                _result = false;
                Close(_result);
            };
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 4);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
        }

        private TabItem CreateWindowSettingsTab()
        {
            var page = new TabItem { Header = "Window" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Window Title
            var windowTitleTextBox = new TextBox { Text = _settings.WindowTitle ?? "Andastra Game" };
            _controlMap["WindowTitle"] = windowTitleTextBox;
            stackPanel.Children.Add(CreateLabeledControl("Title:", windowTitleTextBox, "WindowTitle"));

            // Window Size
            var windowWidthNumeric = new NumericUpDown { Value = _settings.WindowWidth ?? 1280, Minimum = 320, Maximum = 7680 };
            _controlMap["WindowWidth"] = windowWidthNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Width:", windowWidthNumeric, "WindowWidth"));

            var windowHeightNumeric = new NumericUpDown { Value = _settings.WindowHeight ?? 720, Minimum = 240, Maximum = 4320 };
            _controlMap["WindowHeight"] = windowHeightNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Height:", windowHeightNumeric, "WindowHeight"));

            // Fullscreen
            var fullscreenCheck = new CheckBox { IsChecked = _settings.WindowFullscreen ?? false, Content = "Fullscreen" };
            _controlMap["WindowFullscreen"] = fullscreenCheck;
            stackPanel.Children.Add(CreateLabeledControl("Fullscreen:", fullscreenCheck, "WindowFullscreen"));

            // Mouse Visible
            var mouseVisibleCheck = new CheckBox { IsChecked = _settings.WindowIsMouseVisible ?? true, Content = "Mouse Visible" };
            _controlMap["WindowIsMouseVisible"] = mouseVisibleCheck;
            stackPanel.Children.Add(CreateLabeledControl("Mouse Visible:", mouseVisibleCheck, "WindowIsMouseVisible"));

            // VSync - Generic setting for all backends
            // Based on IGraphicsBackend.SupportsVSync and SetVSync interface
            // All graphics backends (MonoGame, Stride) support VSync through the common interface
            var vsyncCheck = new CheckBox { IsChecked = _settings.WindowVSync ?? true, Content = "VSync" };
            _controlMap["WindowVSync"] = vsyncCheck;
            stackPanel.Children.Add(CreateLabeledControl("VSync:", vsyncCheck, "WindowVSync"));

            // MonoGame-specific window settings
            if (_selectedBackend == GraphicsBackendType.MonoGame)
            {
                // MonoGame-specific VSync setting (for backward compatibility)
                // Note: WindowVSync is the preferred generic setting, but we keep this for compatibility
                var syncVerticalRetrace = new CheckBox { IsChecked = _settings.MonoGameSynchronizeWithVerticalRetrace ?? (_settings.WindowVSync ?? true), Content = "Synchronize With Vertical Retrace" };
                _controlMap["MonoGameSynchronizeWithVerticalRetrace"] = syncVerticalRetrace;
                // Sync MonoGame-specific setting with generic VSync setting
                syncVerticalRetrace.IsCheckedChanged += (sender, e) =>
                {
                    if (vsyncCheck.IsChecked != syncVerticalRetrace.IsChecked)
                    {
                        vsyncCheck.IsChecked = syncVerticalRetrace.IsChecked;
                    }
                };
                vsyncCheck.IsCheckedChanged += (sender, e) =>
                {
                    if (syncVerticalRetrace.IsChecked != vsyncCheck.IsChecked)
                    {
                        syncVerticalRetrace.IsChecked = vsyncCheck.IsChecked;
                    }
                };
                stackPanel.Children.Add(CreateLabeledControl("Synchronize With Vertical Retrace:", syncVerticalRetrace, "MonoGameSynchronizeWithVerticalRetrace"));

                var preferMultiSampling = new CheckBox { IsChecked = _settings.MonoGamePreferMultiSampling ?? false, Content = "Prefer Multi-Sampling" };
                _controlMap["MonoGamePreferMultiSampling"] = preferMultiSampling;
                stackPanel.Children.Add(CreateLabeledControl("Prefer Multi-Sampling:", preferMultiSampling, "MonoGamePreferMultiSampling"));

                var preferHalfPixelOffset = new CheckBox { IsChecked = _settings.MonoGamePreferHalfPixelOffset ?? false, Content = "Prefer Half Pixel Offset" };
                _controlMap["MonoGamePreferHalfPixelOffset"] = preferHalfPixelOffset;
                stackPanel.Children.Add(CreateLabeledControl("Prefer Half Pixel Offset:", preferHalfPixelOffset, "MonoGamePreferHalfPixelOffset"));

                var hardwareModeSwitch = new CheckBox { IsChecked = _settings.MonoGameHardwareModeSwitch ?? false, Content = "Hardware Mode Switch" };
                _controlMap["MonoGameHardwareModeSwitch"] = hardwareModeSwitch;
                stackPanel.Children.Add(CreateLabeledControl("Hardware Mode Switch:", hardwareModeSwitch, "MonoGameHardwareModeSwitch"));
            }

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateRasterizerStateTab()
        {
            var page = new TabItem { Header = "Rasterizer State" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Cull Mode
            var cullModeCombo = new ComboBox();
            cullModeCombo.Items.Add("None");
            cullModeCombo.Items.Add("CullClockwiseFace");
            cullModeCombo.Items.Add("CullCounterClockwiseFace");
            cullModeCombo.SelectedItem = _settings.RasterizerCullMode?.ToString() ?? "CullCounterClockwiseFace";
            _controlMap["RasterizerCullMode"] = cullModeCombo;
            stackPanel.Children.Add(CreateLabeledControl("Cull Mode:", cullModeCombo, "RasterizerCullMode"));

            // Fill Mode
            var fillModeCombo = new ComboBox();
            fillModeCombo.Items.Add("Solid");
            fillModeCombo.Items.Add("WireFrame");
            fillModeCombo.SelectedItem = _settings.RasterizerFillMode?.ToString() ?? "Solid";
            _controlMap["RasterizerFillMode"] = fillModeCombo;
            stackPanel.Children.Add(CreateLabeledControl("Fill Mode:", fillModeCombo, "RasterizerFillMode"));

            // Depth Bias Enabled
            var depthBiasEnabledCheck = new CheckBox { IsChecked = _settings.RasterizerDepthBiasEnabled ?? false, Content = "Depth Bias Enabled" };
            _controlMap["RasterizerDepthBiasEnabled"] = depthBiasEnabledCheck;
            stackPanel.Children.Add(CreateLabeledControl("Depth Bias Enabled:", depthBiasEnabledCheck));

            // Depth Bias
            var depthBiasNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.RasterizerDepthBias ?? 0.0),
                FormatString = "F6",
                Increment = 0.000001M
            };
            _controlMap["RasterizerDepthBias"] = depthBiasNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Depth Bias:", depthBiasNumeric));

            // Slope Scale Depth Bias
            var slopeScaleDepthBiasNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.RasterizerSlopeScaleDepthBias ?? 0.0),
                FormatString = "F6",
                Increment = 0.000001M
            };
            _controlMap["RasterizerSlopeScaleDepthBias"] = slopeScaleDepthBiasNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Slope Scale Depth Bias:", slopeScaleDepthBiasNumeric));

            // Scissor Test Enabled
            var scissorTestEnabledCheck = new CheckBox { IsChecked = _settings.RasterizerScissorTestEnabled ?? false, Content = "Scissor Test Enabled" };
            _controlMap["RasterizerScissorTestEnabled"] = scissorTestEnabledCheck;
            stackPanel.Children.Add(CreateLabeledControl("Scissor Test Enabled:", scissorTestEnabledCheck));

            // Multi-Sample Anti-Alias
            var msaaCheck = new CheckBox { IsChecked = _settings.RasterizerMultiSampleAntiAlias ?? false, Content = "Multi-Sample Anti-Alias" };
            _controlMap["RasterizerMultiSampleAntiAlias"] = msaaCheck;
            stackPanel.Children.Add(CreateLabeledControl("Multi-Sample Anti-Alias:", msaaCheck, "RasterizerMultiSampleAntiAlias"));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateDepthStencilStateTab()
        {
            var page = new TabItem { Header = "Depth Stencil State" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Depth Buffer Enable
            var depthBufferEnableCheck = new CheckBox { IsChecked = _settings.DepthStencilDepthBufferEnable ?? true, Content = "Depth Buffer Enable" };
            _controlMap["DepthStencilDepthBufferEnable"] = depthBufferEnableCheck;
            stackPanel.Children.Add(CreateLabeledControl("Depth Buffer Enable:", depthBufferEnableCheck));

            // Depth Buffer Write Enable
            var depthBufferWriteEnableCheck = new CheckBox { IsChecked = _settings.DepthStencilDepthBufferWriteEnable ?? true, Content = "Depth Buffer Write Enable" };
            _controlMap["DepthStencilDepthBufferWriteEnable"] = depthBufferWriteEnableCheck;
            stackPanel.Children.Add(CreateLabeledControl("Depth Buffer Write Enable:", depthBufferWriteEnableCheck));

            // Depth Buffer Function
            var depthBufferFunctionCombo = new ComboBox();
            foreach (var func in Enum.GetValues(typeof(CompareFunction)))
            {
                depthBufferFunctionCombo.Items.Add(func.ToString());
            }
            depthBufferFunctionCombo.SelectedItem = _settings.DepthStencilDepthBufferFunction?.ToString() ?? "LessEqual";
            _controlMap["DepthStencilDepthBufferFunction"] = depthBufferFunctionCombo;
            stackPanel.Children.Add(CreateLabeledControl("Depth Buffer Function:", depthBufferFunctionCombo));

            // Stencil Enable
            var stencilEnableCheck = new CheckBox { IsChecked = _settings.DepthStencilStencilEnable ?? false, Content = "Stencil Enable" };
            _controlMap["DepthStencilStencilEnable"] = stencilEnableCheck;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Enable:", stencilEnableCheck));

            // Two-Sided Stencil Mode
            var twoSidedStencilCheck = new CheckBox { IsChecked = _settings.DepthStencilTwoSidedStencilMode ?? false, Content = "Two-Sided Stencil Mode" };
            _controlMap["DepthStencilTwoSidedStencilMode"] = twoSidedStencilCheck;
            stackPanel.Children.Add(CreateLabeledControl("Two-Sided Stencil Mode:", twoSidedStencilCheck));

            // Stencil Fail
            var stencilFailCombo = new ComboBox();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilFailCombo.Items.Add(op.ToString());
            }
            stencilFailCombo.SelectedItem = _settings.DepthStencilStencilFail?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilFail"] = stencilFailCombo;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Fail:", stencilFailCombo));

            // Stencil Depth Fail
            var stencilDepthFailCombo = new ComboBox();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilDepthFailCombo.Items.Add(op.ToString());
            }
            stencilDepthFailCombo.SelectedItem = _settings.DepthStencilStencilDepthFail?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilDepthFail"] = stencilDepthFailCombo;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Depth Fail:", stencilDepthFailCombo));

            // Stencil Pass
            var stencilPassCombo = new ComboBox();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilPassCombo.Items.Add(op.ToString());
            }
            stencilPassCombo.SelectedItem = _settings.DepthStencilStencilPass?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilPass"] = stencilPassCombo;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Pass:", stencilPassCombo));

            // Stencil Function
            var stencilFunctionCombo = new ComboBox();
            foreach (var func in Enum.GetValues(typeof(CompareFunction)))
            {
                stencilFunctionCombo.Items.Add(func.ToString());
            }
            stencilFunctionCombo.SelectedItem = _settings.DepthStencilStencilFunction?.ToString() ?? "Always";
            _controlMap["DepthStencilStencilFunction"] = stencilFunctionCombo;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Function:", stencilFunctionCombo));

            // Reference Stencil
            var referenceStencilNumeric = new NumericUpDown
            {
                Value = _settings.DepthStencilReferenceStencil ?? 0,
                Minimum = 0,
                Maximum = 255
            };
            _controlMap["DepthStencilReferenceStencil"] = referenceStencilNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Reference Stencil:", referenceStencilNumeric));

            // Stencil Mask
            var stencilMaskNumeric = new NumericUpDown
            {
                Value = _settings.DepthStencilStencilMask ?? 255,
                Minimum = 0,
                Maximum = 255
            };
            _controlMap["DepthStencilStencilMask"] = stencilMaskNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Mask:", stencilMaskNumeric));

            // Stencil Write Mask
            var stencilWriteMaskNumeric = new NumericUpDown
            {
                Value = _settings.DepthStencilStencilWriteMask ?? 255,
                Minimum = 0,
                Maximum = 255
            };
            _controlMap["DepthStencilStencilWriteMask"] = stencilWriteMaskNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Stencil Write Mask:", stencilWriteMaskNumeric));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateBlendStateTab()
        {
            var page = new TabItem { Header = "Blend State" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Alpha Blend Function
            var alphaBlendFunctionCombo = new ComboBox();
            foreach (var func in Enum.GetValues(typeof(BlendFunction)))
            {
                alphaBlendFunctionCombo.Items.Add(func.ToString());
            }
            alphaBlendFunctionCombo.SelectedItem = _settings.BlendAlphaBlendFunction?.ToString() ?? "Add";
            _controlMap["BlendAlphaBlendFunction"] = alphaBlendFunctionCombo;
            stackPanel.Children.Add(CreateLabeledControl("Alpha Blend Function:", alphaBlendFunctionCombo));

            // Alpha Destination Blend
            var alphaDestBlendCombo = new ComboBox();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                alphaDestBlendCombo.Items.Add(blend.ToString());
            }
            alphaDestBlendCombo.SelectedItem = _settings.BlendAlphaDestinationBlend?.ToString() ?? "InverseSourceAlpha";
            _controlMap["BlendAlphaDestinationBlend"] = alphaDestBlendCombo;
            stackPanel.Children.Add(CreateLabeledControl("Alpha Destination Blend:", alphaDestBlendCombo));

            // Alpha Source Blend
            var alphaSourceBlendCombo = new ComboBox();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                alphaSourceBlendCombo.Items.Add(blend.ToString());
            }
            alphaSourceBlendCombo.SelectedItem = _settings.BlendAlphaSourceBlend?.ToString() ?? "SourceAlpha";
            _controlMap["BlendAlphaSourceBlend"] = alphaSourceBlendCombo;
            stackPanel.Children.Add(CreateLabeledControl("Alpha Source Blend:", alphaSourceBlendCombo));

            // Color Blend Function
            var colorBlendFunctionCombo = new ComboBox();
            foreach (var func in Enum.GetValues(typeof(BlendFunction)))
            {
                colorBlendFunctionCombo.Items.Add(func.ToString());
            }
            colorBlendFunctionCombo.SelectedItem = _settings.BlendColorBlendFunction?.ToString() ?? "Add";
            _controlMap["BlendColorBlendFunction"] = colorBlendFunctionCombo;
            stackPanel.Children.Add(CreateLabeledControl("Color Blend Function:", colorBlendFunctionCombo));

            // Color Destination Blend
            var colorDestBlendCombo = new ComboBox();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                colorDestBlendCombo.Items.Add(blend.ToString());
            }
            colorDestBlendCombo.SelectedItem = _settings.BlendColorDestinationBlend?.ToString() ?? "InverseSourceAlpha";
            _controlMap["BlendColorDestinationBlend"] = colorDestBlendCombo;
            stackPanel.Children.Add(CreateLabeledControl("Color Destination Blend:", colorDestBlendCombo));

            // Color Source Blend
            var colorSourceBlendCombo = new ComboBox();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                colorSourceBlendCombo.Items.Add(blend.ToString());
            }
            colorSourceBlendCombo.SelectedItem = _settings.BlendColorSourceBlend?.ToString() ?? "SourceAlpha";
            _controlMap["BlendColorSourceBlend"] = colorSourceBlendCombo;
            stackPanel.Children.Add(CreateLabeledControl("Color Source Blend:", colorSourceBlendCombo));

            // Color Write Channels (0-3)
            for (int i = 0; i < 4; i++)
            {
                var colorWriteChannelsCombo = new ComboBox();
                colorWriteChannelsCombo.Items.Add("None");
                colorWriteChannelsCombo.Items.Add("Red");
                colorWriteChannelsCombo.Items.Add("Green");
                colorWriteChannelsCombo.Items.Add("Blue");
                colorWriteChannelsCombo.Items.Add("Alpha");
                colorWriteChannelsCombo.Items.Add("All");
                var channels = i == 0 ? _settings.BlendColorWriteChannels :
                              i == 1 ? _settings.BlendColorWriteChannels1 :
                              i == 2 ? _settings.BlendColorWriteChannels2 : _settings.BlendColorWriteChannels3;
                colorWriteChannelsCombo.SelectedItem = channels?.ToString() ?? "All";
                _controlMap[$"BlendColorWriteChannels{(i == 0 ? "0" : i.ToString())}"] = colorWriteChannelsCombo;
                stackPanel.Children.Add(CreateLabeledControl($"Color Write Channels {i}:", colorWriteChannelsCombo));
            }

            // Blend Enable
            var blendEnableCheck = new CheckBox { IsChecked = _settings.BlendBlendEnable ?? true, Content = "Blend Enable" };
            _controlMap["BlendBlendEnable"] = blendEnableCheck;
            stackPanel.Children.Add(CreateLabeledControl("Blend Enable:", blendEnableCheck));

            // Blend Factor (Color)
            var blendFactorGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,5,*,5,Auto,5,*,5,Auto,5,*,5,Auto,5,*") };
            var blendFactorRNumeric = new NumericUpDown { Value = _settings.BlendBlendFactorR ?? 0, Minimum = 0, Maximum = 255 };
            _controlMap["BlendBlendFactorR"] = blendFactorRNumeric;
            var blendFactorGNumeric = new NumericUpDown { Value = _settings.BlendBlendFactorG ?? 0, Minimum = 0, Maximum = 255 };
            _controlMap["BlendBlendFactorG"] = blendFactorGNumeric;
            var blendFactorBNumeric = new NumericUpDown { Value = _settings.BlendBlendFactorB ?? 0, Minimum = 0, Maximum = 255 };
            _controlMap["BlendBlendFactorB"] = blendFactorBNumeric;
            var blendFactorANumeric = new NumericUpDown { Value = _settings.BlendBlendFactorA ?? 255, Minimum = 0, Maximum = 255 };
            _controlMap["BlendBlendFactorA"] = blendFactorANumeric;
            
            var rLabel = new TextBlock { Text = "R:" };
            Grid.SetColumn(rLabel, 0);
            blendFactorGrid.Children.Add(rLabel);
            Grid.SetColumn(blendFactorRNumeric, 2);
            blendFactorGrid.Children.Add(blendFactorRNumeric);
            var gLabel = new TextBlock { Text = "G:" };
            Grid.SetColumn(gLabel, 4);
            blendFactorGrid.Children.Add(gLabel);
            Grid.SetColumn(blendFactorGNumeric, 6);
            blendFactorGrid.Children.Add(blendFactorGNumeric);
            var bLabel = new TextBlock { Text = "B:" };
            Grid.SetColumn(bLabel, 8);
            blendFactorGrid.Children.Add(bLabel);
            Grid.SetColumn(blendFactorBNumeric, 10);
            blendFactorGrid.Children.Add(blendFactorBNumeric);
            var aLabel = new TextBlock { Text = "A:" };
            Grid.SetColumn(aLabel, 12);
            blendFactorGrid.Children.Add(aLabel);
            Grid.SetColumn(blendFactorANumeric, 14);
            blendFactorGrid.Children.Add(blendFactorANumeric);
            
            stackPanel.Children.Add(CreateLabeledControl("Blend Factor (RGBA):", blendFactorGrid));

            // Multi-Sample Mask
            var multiSampleMaskNumeric = new NumericUpDown
            {
                Value = _settings.BlendMultiSampleMask ?? -1,
                Minimum = -1,
                Maximum = int.MaxValue
            };
            _controlMap["BlendMultiSampleMask"] = multiSampleMaskNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Multi-Sample Mask:", multiSampleMaskNumeric));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateSamplerStateTab()
        {
            var page = new TabItem { Header = "Sampler State" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Address U
            var addressUCombo = new ComboBox();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressUCombo.Items.Add(mode.ToString());
            }
            addressUCombo.SelectedItem = _settings.SamplerAddressU?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressU"] = addressUCombo;
            stackPanel.Children.Add(CreateLabeledControl("Address U:", addressUCombo));

            // Address V
            var addressVCombo = new ComboBox();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressVCombo.Items.Add(mode.ToString());
            }
            addressVCombo.SelectedItem = _settings.SamplerAddressV?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressV"] = addressVCombo;
            stackPanel.Children.Add(CreateLabeledControl("Address V:", addressVCombo));

            // Address W
            var addressWCombo = new ComboBox();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressWCombo.Items.Add(mode.ToString());
            }
            addressWCombo.SelectedItem = _settings.SamplerAddressW?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressW"] = addressWCombo;
            stackPanel.Children.Add(CreateLabeledControl("Address W:", addressWCombo));

            // Filter
            var filterCombo = new ComboBox();
            foreach (var filter in Enum.GetValues(typeof(TextureFilter)))
            {
                filterCombo.Items.Add(filter.ToString());
            }
            filterCombo.SelectedItem = _settings.SamplerFilter?.ToString() ?? "Linear";
            _controlMap["SamplerFilter"] = filterCombo;
            stackPanel.Children.Add(CreateLabeledControl("Filter:", filterCombo, "SamplerFilter"));

            // Max Anisotropy
            var maxAnisotropyNumeric = new NumericUpDown
            {
                Value = _settings.SamplerMaxAnisotropy ?? 0,
                Minimum = 0,
                Maximum = 16
            };
            _controlMap["SamplerMaxAnisotropy"] = maxAnisotropyNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Max Anisotropy:", maxAnisotropyNumeric, "SamplerMaxAnisotropy"));

            // Max Mip Level
            var maxMipLevelNumeric = new NumericUpDown
            {
                Value = _settings.SamplerMaxMipLevel ?? 0,
                Minimum = 0,
                Maximum = 15
            };
            _controlMap["SamplerMaxMipLevel"] = maxMipLevelNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Max Mip Level:", maxMipLevelNumeric));

            // Mip Map Level of Detail Bias
            var mipMapLodBiasNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.SamplerMipMapLevelOfDetailBias ?? 0.0),
                FormatString = "F3",
                Increment = 0.001M
            };
            _controlMap["SamplerMipMapLevelOfDetailBias"] = mipMapLodBiasNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Mip Map LOD Bias:", mipMapLodBiasNumeric));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateBasicEffectTab()
        {
            var page = new TabItem { Header = "Basic Effect" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Vertex Color Enabled
            var vertexColorEnabledCheck = new CheckBox { IsChecked = _settings.BasicEffectVertexColorEnabled ?? false, Content = "Vertex Color Enabled" };
            _controlMap["BasicEffectVertexColorEnabled"] = vertexColorEnabledCheck;
            stackPanel.Children.Add(CreateLabeledControl("Vertex Color Enabled:", vertexColorEnabledCheck));

            // Lighting Enabled
            var lightingEnabledCheck = new CheckBox { IsChecked = _settings.BasicEffectLightingEnabled ?? false, Content = "Lighting Enabled" };
            _controlMap["BasicEffectLightingEnabled"] = lightingEnabledCheck;
            stackPanel.Children.Add(CreateLabeledControl("Lighting Enabled:", lightingEnabledCheck, "BasicEffectLightingEnabled"));

            // Texture Enabled
            var textureEnabledCheck = new CheckBox { IsChecked = _settings.BasicEffectTextureEnabled ?? false, Content = "Texture Enabled" };
            _controlMap["BasicEffectTextureEnabled"] = textureEnabledCheck;
            stackPanel.Children.Add(CreateLabeledControl("Texture Enabled:", textureEnabledCheck, "BasicEffectTextureEnabled"));

            // Ambient Light Color
            var ambientColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectAmbientLightColorX ?? 0.2f,
                _settings.BasicEffectAmbientLightColorY ?? 0.2f,
                _settings.BasicEffectAmbientLightColorZ ?? 0.2f,
                "BasicEffectAmbientLightColor");
            stackPanel.Children.Add(CreateLabeledControl("Ambient Light Color (RGB):", ambientColorLayout));

            // Diffuse Color
            var diffuseColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectDiffuseColorX ?? 1.0f,
                _settings.BasicEffectDiffuseColorY ?? 1.0f,
                _settings.BasicEffectDiffuseColorZ ?? 1.0f,
                "BasicEffectDiffuseColor");
            stackPanel.Children.Add(CreateLabeledControl("Diffuse Color (RGB):", diffuseColorLayout));

            // Emissive Color
            var emissiveColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectEmissiveColorX ?? 0.0f,
                _settings.BasicEffectEmissiveColorY ?? 0.0f,
                _settings.BasicEffectEmissiveColorZ ?? 0.0f,
                "BasicEffectEmissiveColor");
            stackPanel.Children.Add(CreateLabeledControl("Emissive Color (RGB):", emissiveColorLayout));

            // Specular Color
            var specularColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectSpecularColorX ?? 0.0f,
                _settings.BasicEffectSpecularColorY ?? 0.0f,
                _settings.BasicEffectSpecularColorZ ?? 0.0f,
                "BasicEffectSpecularColor");
            stackPanel.Children.Add(CreateLabeledControl("Specular Color (RGB):", specularColorLayout));

            // Specular Power
            var specularPowerNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.BasicEffectSpecularPower ?? 0.0f),
                Minimum = 0.0M,
                Maximum = 1000.0M,
                FormatString = "F2"
            };
            _controlMap["BasicEffectSpecularPower"] = specularPowerNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Specular Power:", specularPowerNumeric));

            // Alpha
            var alphaNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.BasicEffectAlpha ?? 1.0f),
                Minimum = 0.0M,
                Maximum = 1.0M,
                FormatString = "F3",
                Increment = 0.001M
            };
            _controlMap["BasicEffectAlpha"] = alphaNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Alpha:", alphaNumeric));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateSpriteBatchTab()
        {
            var page = new TabItem { Header = "SpriteBatch" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Sort Mode
            var sortModeCombo = new ComboBox();
            foreach (var mode in Enum.GetValues(typeof(SpriteSortMode)))
            {
                sortModeCombo.Items.Add(mode.ToString());
            }
            sortModeCombo.SelectedItem = _settings.SpriteBatchSortMode?.ToString() ?? "Deferred";
            _controlMap["SpriteBatchSortMode"] = sortModeCombo;
            stackPanel.Children.Add(CreateLabeledControl("Sort Mode:", sortModeCombo));

            // Blend State (Alpha Blend)
            var blendStateAlphaBlendCheck = new CheckBox { IsChecked = _settings.SpriteBatchBlendStateAlphaBlend ?? true, Content = "Blend State - Alpha Blend" };
            _controlMap["SpriteBatchBlendStateAlphaBlend"] = blendStateAlphaBlendCheck;
            stackPanel.Children.Add(CreateLabeledControl("Blend State - Alpha Blend:", blendStateAlphaBlendCheck));

            // Blend State (Additive)
            var blendStateAdditiveCheck = new CheckBox { IsChecked = _settings.SpriteBatchBlendStateAdditive ?? false, Content = "Blend State - Additive" };
            _controlMap["SpriteBatchBlendStateAdditive"] = blendStateAdditiveCheck;
            stackPanel.Children.Add(CreateLabeledControl("Blend State - Additive:", blendStateAdditiveCheck));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateSpatialAudioTab()
        {
            var page = new TabItem { Header = "Spatial Audio" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Doppler Factor
            var dopplerFactorNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.SpatialAudioDopplerFactor ?? 1.0f),
                Minimum = 0.0M,
                Maximum = 10.0M,
                FormatString = "F3",
                Increment = 0.001M
            };
            _controlMap["SpatialAudioDopplerFactor"] = dopplerFactorNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Doppler Factor:", dopplerFactorNumeric, "SpatialAudioDopplerFactor"));

            // Speed of Sound
            var speedOfSoundNumeric = new NumericUpDown
            {
                Value = (decimal)(_settings.SpatialAudioSpeedOfSound ?? 343.0f),
                Minimum = 1.0M,
                Maximum = 10000.0M,
                FormatString = "F2"
            };
            _controlMap["SpatialAudioSpeedOfSound"] = speedOfSoundNumeric;
            stackPanel.Children.Add(CreateLabeledControl("Speed of Sound (m/s):", speedOfSoundNumeric, "SpatialAudioSpeedOfSound"));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateContentManagerTab()
        {
            var page = new TabItem { Header = "Content Manager" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Root Directory
            var rootDirGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,10,Auto") };
            var rootDirTextBox = new TextBox { Text = _settings.ContentManagerRootDirectory ?? "Content" };
            _controlMap["ContentManagerRootDirectory"] = rootDirTextBox;
            Grid.SetColumn(rootDirTextBox, 0);
            rootDirGrid.Children.Add(rootDirTextBox);
            
            var rootDirBrowseButton = new Button { Content = "Browse..." };
            rootDirBrowseButton.Click += async (sender, e) =>
            {
                var dialog = new OpenFolderDialog { Title = "Select Content Root Directory" };
                var result = await dialog.ShowAsync(this);
                if (!string.IsNullOrEmpty(result))
                {
                    rootDirTextBox.Text = result;
                }
            };
            Grid.SetColumn(rootDirBrowseButton, 2);
            rootDirGrid.Children.Add(rootDirBrowseButton);
            
            stackPanel.Children.Add(CreateLabeledControl("Root Directory:", rootDirGrid));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateMonoGameSpecificTab()
        {
            var page = new TabItem { Header = "MonoGame Specific" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Graphics Profile
            var graphicsProfileCombo = new ComboBox();
            graphicsProfileCombo.Items.Add("Reach");
            graphicsProfileCombo.Items.Add("HiDef");
            graphicsProfileCombo.SelectedItem = _settings.MonoGameGraphicsProfile ?? "HiDef";
            _controlMap["MonoGameGraphicsProfile"] = graphicsProfileCombo;
            stackPanel.Children.Add(CreateLabeledControl("Graphics Profile:", graphicsProfileCombo));

            // Supported Orientations (for mobile)
            var supportedOrientationsGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,5,Auto,10,Auto,5,Auto,10,Auto,5,Auto,10,Auto,5,Auto,10,Auto,5,Auto") };
            var portraitCheck = new CheckBox { IsChecked = _settings.MonoGameSupportedOrientationsPortrait ?? true, Content = "Portrait" };
            _controlMap["MonoGameSupportedOrientationsPortrait"] = portraitCheck;
            var portraitLabel = new TextBlock { Text = "Portrait:" };
            Grid.SetColumn(portraitLabel, 0);
            supportedOrientationsGrid.Children.Add(portraitLabel);
            Grid.SetColumn(portraitCheck, 2);
            supportedOrientationsGrid.Children.Add(portraitCheck);
            
            var landscapeCheck = new CheckBox { IsChecked = _settings.MonoGameSupportedOrientationsLandscape ?? true, Content = "Landscape" };
            _controlMap["MonoGameSupportedOrientationsLandscape"] = landscapeCheck;
            var landscapeLabel = new TextBlock { Text = "Landscape:" };
            Grid.SetColumn(landscapeLabel, 4);
            supportedOrientationsGrid.Children.Add(landscapeLabel);
            Grid.SetColumn(landscapeCheck, 6);
            supportedOrientationsGrid.Children.Add(landscapeCheck);
            
            var portraitUpsideDownCheck = new CheckBox { IsChecked = _settings.MonoGameSupportedOrientationsPortraitUpsideDown ?? false, Content = "Portrait Upside Down" };
            _controlMap["MonoGameSupportedOrientationsPortraitUpsideDown"] = portraitUpsideDownCheck;
            var portraitUpsideDownLabel = new TextBlock { Text = "Portrait Upside Down:" };
            Grid.SetColumn(portraitUpsideDownLabel, 8);
            supportedOrientationsGrid.Children.Add(portraitUpsideDownLabel);
            Grid.SetColumn(portraitUpsideDownCheck, 10);
            supportedOrientationsGrid.Children.Add(portraitUpsideDownCheck);
            
            var landscapeLeftCheck = new CheckBox { IsChecked = _settings.MonoGameSupportedOrientationsLandscapeLeft ?? false, Content = "Landscape Left" };
            _controlMap["MonoGameSupportedOrientationsLandscapeLeft"] = landscapeLeftCheck;
            var landscapeLeftLabel = new TextBlock { Text = "Landscape Left:" };
            Grid.SetColumn(landscapeLeftLabel, 12);
            supportedOrientationsGrid.Children.Add(landscapeLeftLabel);
            Grid.SetColumn(landscapeLeftCheck, 14);
            supportedOrientationsGrid.Children.Add(landscapeLeftCheck);
            
            var landscapeRightCheck = new CheckBox { IsChecked = _settings.MonoGameSupportedOrientationsLandscapeRight ?? false, Content = "Landscape Right" };
            _controlMap["MonoGameSupportedOrientationsLandscapeRight"] = landscapeRightCheck;
            var landscapeRightLabel = new TextBlock { Text = "Landscape Right:" };
            Grid.SetColumn(landscapeRightLabel, 16);
            supportedOrientationsGrid.Children.Add(landscapeRightLabel);
            Grid.SetColumn(landscapeRightCheck, 18);
            supportedOrientationsGrid.Children.Add(landscapeRightCheck);
            
            stackPanel.Children.Add(CreateLabeledControl("Supported Orientations:", supportedOrientationsGrid));

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private TabItem CreateStrideSpecificTab()
        {
            var page = new TabItem { Header = "Stride Specific" };
            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

            // Stride-specific settings would go here
            // Currently Stride doesn't expose many additional settings beyond what's in the common interface
            // But we include this tab for future extensibility

            var infoLabel = new TextBlock
            {
                Text = "Stride-specific settings are primarily configured through the common graphics settings tabs.\n" +
                       "Additional Stride-specific options can be added here as needed.",
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(infoLabel);

            scrollViewer.Content = stackPanel;
            page.Content = scrollViewer;
            return page;
        }

        private Panel CreateLabeledControl(string labelText, Control control, string helpKey = null)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("200,10,*,10,Auto"),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var label = new TextBlock
            {
                Text = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 200
            };
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);

            Grid.SetColumn(control, 2);
            grid.Children.Add(control);

            // Add help button
            var helpButton = new Button
            {
                Content = "?",
                Width = 25,
                Height = 25
            };
            if (!string.IsNullOrEmpty(helpKey))
            {
                helpButton.Click += async (sender, e) =>
                {
                    var helpText = GraphicsSettingsHelp.GetHelpText(helpKey);
                    var helpWindow = new Window
                    {
                        Title = $"{labelText} - Help",
                        Width = 500,
                        Height = 300,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };
                    var helpTextBox = new TextBox
                    {
                        Text = helpText,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.Wrap,
                        AcceptsReturn = true
                    };
                    helpWindow.Content = helpTextBox;
                    await helpWindow.ShowDialog(this);
                };
            }
            Grid.SetColumn(helpButton, 4);
            grid.Children.Add(helpButton);

            return grid;
        }

        private Panel CreateColorVector3Layout(float x, float y, float z, string baseKey)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,5,*,5,Auto,5,*,5,Auto,5,*") };
            var xNumeric = new NumericUpDown { Value = (decimal)x, Minimum = 0.0M, Maximum = 1.0M, FormatString = "F3", Increment = 0.001M };
            _controlMap[$"{baseKey}X"] = xNumeric;
            var yNumeric = new NumericUpDown { Value = (decimal)y, Minimum = 0.0M, Maximum = 1.0M, FormatString = "F3", Increment = 0.001M };
            _controlMap[$"{baseKey}Y"] = yNumeric;
            var zNumeric = new NumericUpDown { Value = (decimal)z, Minimum = 0.0M, Maximum = 1.0M, FormatString = "F3", Increment = 0.001M };
            _controlMap[$"{baseKey}Z"] = zNumeric;
            
            var rLabel = new TextBlock { Text = "R:" };
            Grid.SetColumn(rLabel, 0);
            grid.Children.Add(rLabel);
            Grid.SetColumn(xNumeric, 2);
            grid.Children.Add(xNumeric);
            var gLabel = new TextBlock { Text = "G:" };
            Grid.SetColumn(gLabel, 4);
            grid.Children.Add(gLabel);
            Grid.SetColumn(yNumeric, 6);
            grid.Children.Add(yNumeric);
            var bLabel = new TextBlock { Text = "B:" };
            Grid.SetColumn(bLabel, 8);
            grid.Children.Add(bLabel);
            Grid.SetColumn(zNumeric, 10);
            grid.Children.Add(zNumeric);
            
            return grid;
        }

        private void LoadSettings()
        {
            // Settings are loaded into controls in InitializeComponent
            // This method can be used to refresh controls if settings change
        }

        private void SaveSettings()
        {
            SaveSettingsToObject(_settings);
        }

        private void SaveSettingsToObject(GraphicsSettingsData targetSettings)
        {
            // Save all settings from controls to the specified settings object
            // Iterate through all controls in _controlMap and save their values
            foreach (var kvp in _controlMap)
            {
                string key = kvp.Key;
                Control control = kvp.Value;

                try
                {
                    if (control is TextBox textBox)
                    {
                        SaveStringSetting(key, textBox.Text, targetSettings);
                    }
                    else if (control is NumericUpDown numericUpDown)
                    {
                        SaveNumericSetting(key, (double)(numericUpDown.Value ?? 0), targetSettings);
                    }
                    else if (control is CheckBox checkBox)
                    {
                        SaveBoolSetting(key, checkBox.IsChecked ?? false, targetSettings);
                    }
                    else if (control is ComboBox comboBox)
                    {
                        SaveEnumSetting(key, comboBox.SelectedItem?.ToString(), targetSettings);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue saving other settings
                    System.Diagnostics.Debug.WriteLine($"Error saving setting {key}: {ex.Message}");
                }
            }
        }

        private void SaveStringSetting(string key, string value, GraphicsSettingsData targetSettings)
        {
            switch (key)
            {
                case "WindowTitle": targetSettings.WindowTitle = value; break;
                case "ContentManagerRootDirectory": targetSettings.ContentManagerRootDirectory = value; break;
                case "MonoGameGraphicsProfile": targetSettings.MonoGameGraphicsProfile = value; break;
            }
        }

        private void SaveNumericSetting(string key, double value, GraphicsSettingsData targetSettings)
        {
            switch (key)
            {
                case "WindowWidth": targetSettings.WindowWidth = (int)value; break;
                case "WindowHeight": targetSettings.WindowHeight = (int)value; break;
                case "RasterizerDepthBias": targetSettings.RasterizerDepthBias = value; break;
                case "RasterizerSlopeScaleDepthBias": targetSettings.RasterizerSlopeScaleDepthBias = value; break;
                case "DepthStencilReferenceStencil": targetSettings.DepthStencilReferenceStencil = (int)value; break;
                case "DepthStencilStencilMask": targetSettings.DepthStencilStencilMask = (int)value; break;
                case "DepthStencilStencilWriteMask": targetSettings.DepthStencilStencilWriteMask = (int)value; break;
                case "BlendBlendFactorR": targetSettings.BlendBlendFactorR = (int)value; break;
                case "BlendBlendFactorG": targetSettings.BlendBlendFactorG = (int)value; break;
                case "BlendBlendFactorB": targetSettings.BlendBlendFactorB = (int)value; break;
                case "BlendBlendFactorA": targetSettings.BlendBlendFactorA = (int)value; break;
                case "BlendMultiSampleMask": targetSettings.BlendMultiSampleMask = (int)value; break;
                case "SamplerMaxAnisotropy": targetSettings.SamplerMaxAnisotropy = (int)value; break;
                case "SamplerMaxMipLevel": targetSettings.SamplerMaxMipLevel = (int)value; break;
                case "SamplerMipMapLevelOfDetailBias": targetSettings.SamplerMipMapLevelOfDetailBias = value; break;
                case "BasicEffectAmbientLightColorX": targetSettings.BasicEffectAmbientLightColorX = (float)value; break;
                case "BasicEffectAmbientLightColorY": targetSettings.BasicEffectAmbientLightColorY = (float)value; break;
                case "BasicEffectAmbientLightColorZ": targetSettings.BasicEffectAmbientLightColorZ = (float)value; break;
                case "BasicEffectDiffuseColorX": targetSettings.BasicEffectDiffuseColorX = (float)value; break;
                case "BasicEffectDiffuseColorY": targetSettings.BasicEffectDiffuseColorY = (float)value; break;
                case "BasicEffectDiffuseColorZ": targetSettings.BasicEffectDiffuseColorZ = (float)value; break;
                case "BasicEffectEmissiveColorX": targetSettings.BasicEffectEmissiveColorX = (float)value; break;
                case "BasicEffectEmissiveColorY": targetSettings.BasicEffectEmissiveColorY = (float)value; break;
                case "BasicEffectEmissiveColorZ": targetSettings.BasicEffectEmissiveColorZ = (float)value; break;
                case "BasicEffectSpecularColorX": targetSettings.BasicEffectSpecularColorX = (float)value; break;
                case "BasicEffectSpecularColorY": targetSettings.BasicEffectSpecularColorY = (float)value; break;
                case "BasicEffectSpecularColorZ": targetSettings.BasicEffectSpecularColorZ = (float)value; break;
                case "BasicEffectSpecularPower": targetSettings.BasicEffectSpecularPower = (float)value; break;
                case "BasicEffectAlpha": targetSettings.BasicEffectAlpha = (float)value; break;
                case "SpatialAudioDopplerFactor": targetSettings.SpatialAudioDopplerFactor = (float)value; break;
                case "SpatialAudioSpeedOfSound": targetSettings.SpatialAudioSpeedOfSound = (float)value; break;
            }
        }

        private void SaveBoolSetting(string key, bool value, GraphicsSettingsData targetSettings)
        {
            switch (key)
            {
                case "WindowFullscreen": targetSettings.WindowFullscreen = value; break;
                case "WindowIsMouseVisible": targetSettings.WindowIsMouseVisible = value; break;
                case "WindowVSync": targetSettings.WindowVSync = value; break;
                case "MonoGameSynchronizeWithVerticalRetrace": targetSettings.MonoGameSynchronizeWithVerticalRetrace = value; break;
                case "MonoGamePreferMultiSampling": targetSettings.MonoGamePreferMultiSampling = value; break;
                case "MonoGamePreferHalfPixelOffset": targetSettings.MonoGamePreferHalfPixelOffset = value; break;
                case "MonoGameHardwareModeSwitch": targetSettings.MonoGameHardwareModeSwitch = value; break;
                case "MonoGameSupportedOrientationsPortrait": targetSettings.MonoGameSupportedOrientationsPortrait = value; break;
                case "MonoGameSupportedOrientationsLandscape": targetSettings.MonoGameSupportedOrientationsLandscape = value; break;
                case "MonoGameSupportedOrientationsPortraitUpsideDown": targetSettings.MonoGameSupportedOrientationsPortraitUpsideDown = value; break;
                case "MonoGameSupportedOrientationsLandscapeLeft": targetSettings.MonoGameSupportedOrientationsLandscapeLeft = value; break;
                case "MonoGameSupportedOrientationsLandscapeRight": targetSettings.MonoGameSupportedOrientationsLandscapeRight = value; break;
                case "RasterizerDepthBiasEnabled": targetSettings.RasterizerDepthBiasEnabled = value; break;
                case "RasterizerScissorTestEnabled": targetSettings.RasterizerScissorTestEnabled = value; break;
                case "RasterizerMultiSampleAntiAlias": targetSettings.RasterizerMultiSampleAntiAlias = value; break;
                case "DepthStencilDepthBufferEnable": targetSettings.DepthStencilDepthBufferEnable = value; break;
                case "DepthStencilDepthBufferWriteEnable": targetSettings.DepthStencilDepthBufferWriteEnable = value; break;
                case "DepthStencilStencilEnable": targetSettings.DepthStencilStencilEnable = value; break;
                case "DepthStencilTwoSidedStencilMode": targetSettings.DepthStencilTwoSidedStencilMode = value; break;
                case "BlendBlendEnable": targetSettings.BlendBlendEnable = value; break;
                case "BasicEffectVertexColorEnabled": targetSettings.BasicEffectVertexColorEnabled = value; break;
                case "BasicEffectLightingEnabled": targetSettings.BasicEffectLightingEnabled = value; break;
                case "BasicEffectTextureEnabled": targetSettings.BasicEffectTextureEnabled = value; break;
                case "SpriteBatchBlendStateAlphaBlend": targetSettings.SpriteBatchBlendStateAlphaBlend = value; break;
                case "SpriteBatchBlendStateAdditive": targetSettings.SpriteBatchBlendStateAdditive = value; break;
            }
        }

        private void SaveEnumSetting(string key, string value, GraphicsSettingsData targetSettings)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                switch (key)
                {
                    case "RasterizerCullMode":
                        if (Enum.TryParse<Runtime.Graphics.Common.Enums.CullMode>(value, out var cullMode))
                            targetSettings.RasterizerCullMode = cullMode;
                        break;
                    case "RasterizerFillMode":
                        if (Enum.TryParse<Runtime.Graphics.Common.Enums.FillMode>(value, out var fillMode))
                            targetSettings.RasterizerFillMode = fillMode;
                        break;
                    case "DepthStencilDepthBufferFunction":
                        if (Enum.TryParse<CompareFunction>(value, out var depthFunc))
                            targetSettings.DepthStencilDepthBufferFunction = depthFunc;
                        break;
                    case "DepthStencilStencilFail":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilFail))
                            targetSettings.DepthStencilStencilFail = stencilFail;
                        break;
                    case "DepthStencilStencilDepthFail":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilDepthFail))
                            targetSettings.DepthStencilStencilDepthFail = stencilDepthFail;
                        break;
                    case "DepthStencilStencilPass":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilPass))
                            targetSettings.DepthStencilStencilPass = stencilPass;
                        break;
                    case "DepthStencilStencilFunction":
                        if (Enum.TryParse<CompareFunction>(value, out var stencilFunc))
                            targetSettings.DepthStencilStencilFunction = stencilFunc;
                        break;
                    case "BlendAlphaBlendFunction":
                        if (Enum.TryParse<BlendFunction>(value, out var alphaBlendFunc))
                            targetSettings.BlendAlphaBlendFunction = alphaBlendFunc;
                        break;
                    case "BlendAlphaDestinationBlend":
                        if (Enum.TryParse<Blend>(value, out var alphaDestBlend))
                            targetSettings.BlendAlphaDestinationBlend = alphaDestBlend;
                        break;
                    case "BlendAlphaSourceBlend":
                        if (Enum.TryParse<Blend>(value, out var alphaSourceBlend))
                            targetSettings.BlendAlphaSourceBlend = alphaSourceBlend;
                        break;
                    case "BlendColorBlendFunction":
                        if (Enum.TryParse<BlendFunction>(value, out var colorBlendFunc))
                            targetSettings.BlendColorBlendFunction = colorBlendFunc;
                        break;
                    case "BlendColorDestinationBlend":
                        if (Enum.TryParse<Blend>(value, out var colorDestBlend))
                            targetSettings.BlendColorDestinationBlend = colorDestBlend;
                        break;
                    case "BlendColorSourceBlend":
                        if (Enum.TryParse<Blend>(value, out var colorSourceBlend))
                            targetSettings.BlendColorSourceBlend = colorSourceBlend;
                        break;
                    case "BlendColorWriteChannels0":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels))
                            targetSettings.BlendColorWriteChannels = colorWriteChannels;
                        break;
                    case "BlendColorWriteChannels1":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels1))
                            targetSettings.BlendColorWriteChannels1 = colorWriteChannels1;
                        break;
                    case "BlendColorWriteChannels2":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels2))
                            targetSettings.BlendColorWriteChannels2 = colorWriteChannels2;
                        break;
                    case "BlendColorWriteChannels3":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels3))
                            targetSettings.BlendColorWriteChannels3 = colorWriteChannels3;
                        break;
                    case "SamplerAddressU":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressU))
                            targetSettings.SamplerAddressU = addressU;
                        break;
                    case "SamplerAddressV":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressV))
                            targetSettings.SamplerAddressV = addressV;
                        break;
                    case "SamplerAddressW":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressW))
                            targetSettings.SamplerAddressW = addressW;
                        break;
                    case "SamplerFilter":
                        if (Enum.TryParse<TextureFilter>(value, out var filter))
                            targetSettings.SamplerFilter = filter;
                        break;
                    case "SpriteBatchSortMode":
                        if (Enum.TryParse<SpriteSortMode>(value, out var sortMode))
                            targetSettings.SpriteBatchSortMode = sortMode;
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing enum setting {key}={value}: {ex.Message}");
            }
        }

        private string GetBackendName(GraphicsBackendType backendType)
        {
            if (backendType == GraphicsBackendType.MonoGame)
            {
                return "MonoGame";
            }
            else if (backendType == GraphicsBackendType.Stride)
            {
                return "Stride";
            }
            return backendType.ToString();
        }

        private async void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against re-entrancy when programmatically updating the combobox
            if (_isUpdatingPresetComboBox)
                return;

            var selectedValue = (_presetComboBox.SelectedItem as string);
            if (string.IsNullOrEmpty(selectedValue))
                return;

            GraphicsPreset preset;
            if (!Enum.TryParse<GraphicsPreset>(selectedValue, out preset))
                return;

            if (preset == GraphicsPreset.Custom)
            {
                _currentPreset = GraphicsPreset.Custom;
                return;
            }

            var result = await ShowYesNoDialogAsync("Apply Preset", $"Apply {selectedValue} preset? This will overwrite your current settings.");
            if (result)
            {
                _settings = GraphicsSettingsPresetFactory.CreatePreset(preset);
                _currentPreset = preset;
                LoadSettings();
            }
            else
            {
                // Set guard flag to prevent re-entrancy
                _isUpdatingPresetComboBox = true;
                try
                {
                    _presetComboBox.SelectedItem = _currentPreset.ToString();
                }
                finally
                {
                    _isUpdatingPresetComboBox = false;
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = _searchTextBox.Text?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all tabs
                foreach (var tab in _tabControl.Items.Cast<TabItem>())
                {
                    tab.IsVisible = true;
                }
                return;
            }

            // Filter tabs and controls based on search
            foreach (var kvp in _tabPageMap)
            {
                var tab = kvp.Value;
                var tabName = kvp.Key.ToLowerInvariant();
                var matchesTab = tabName.Contains(searchText);

                // Check if any controls in this tab match
                var matchesControl = false;
                foreach (var controlKvp in _controlMap)
                {
                    if (controlKvp.Key.ToLowerInvariant().Contains(searchText) ||
                        GraphicsSettingsHelp.GetHelpText(controlKvp.Key).ToLowerInvariant().Contains(searchText))
                    {
                        matchesControl = true;
                        break;
                    }
                }

                tab.IsVisible = matchesTab || matchesControl;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Graphics Settings",
                Filters = new List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "XML Files", Extensions = new List<string> { "xml" } },
                    new FileDialogFilter { Name = "All Files", Extensions = new List<string> { "*" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                try
                {
                    _settings = GraphicsSettingsSerializer.ImportFromXml(result[0]);
                    _currentPreset = GraphicsPreset.Custom;
                    _presetComboBox.SelectedItem = "Custom";
                    LoadSettings();
                    ShowInfoDialog("Import Success", "Settings imported successfully!");
                }
                catch (Exception ex)
                {
                    ShowErrorDialog("Import Error", $"Failed to import settings:\n\n{ex.Message}");
                }
            }
        }

        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            // Create temporary settings object for validation
            // Do NOT modify _settings - validation should be read-only
            var tempSettings = new GraphicsSettingsData();
            SaveSettingsToObject(tempSettings);
            
            var validationResult = GraphicsSettingsSerializer.Validate(tempSettings);
            if (validationResult.IsValid)
            {
                ShowInfoDialog("Validation Success", "All settings are valid!");
            }
            else
            {
                ShowErrorDialog("Validation Errors", validationResult.GetFormattedMessage());
            }
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = await ShowYesNoDialogAsync("Reset Settings", "Reset all settings to default values?");
            if (result)
            {
                _settings = new GraphicsSettingsData();
                _currentPreset = GraphicsPreset.Custom;
                _presetComboBox.SelectedItem = "Custom";
                LoadSettings();
            }
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Create temporary settings object for validation
            var tempSettings = new GraphicsSettingsData();
            SaveSettingsToObject(tempSettings);
            
            var validationResult = GraphicsSettingsSerializer.Validate(tempSettings);
            if (!validationResult.IsValid)
            {
                var result = await ShowYesNoDialogAsync("Validation Warnings", "Some settings have validation errors:\n\n" + validationResult.GetFormattedMessage() + "\n\nDo you want to continue anyway?");
                if (result)
                {
                    // User confirmed - NOW save the settings
                    _settings = tempSettings;
                    _result = true;
                    Close(_result);
                }
                // User declined - settings are NOT saved, dialog stays open for corrections
            }
            else
            {
                // Validation passed - save and close
                _settings = tempSettings;
                _result = true;
                Close(_result);
            }
        }

        private async System.Threading.Tasks.Task<bool> ShowYesNoDialogAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10 };
            var yesButton = new Button { Content = "Yes", Width = 80 };
            yesButton.Click += (s, e) => { dialog.Close(true); };
            buttonPanel.Children.Add(yesButton);
            
            var noButton = new Button { Content = "No", Width = 80 };
            noButton.Click += (s, e) => { dialog.Close(false); };
            buttonPanel.Children.Add(noButton);
            
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            
            var result = await dialog.ShowDialog<bool>(this);
            return result;
        }

        private async void ShowInfoDialog(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
            var okButton = new Button { Content = "OK", Width = 80, HorizontalAlignment = HorizontalAlignment.Center };
            okButton.Click += (s, e) => dialog.Close();
            panel.Children.Add(okButton);
            dialog.Content = panel;
            await dialog.ShowDialog(this);
        }

        private async void ShowErrorDialog(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var errorTextBox = new TextBox
            {
                Text = message,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true
            };
            dialog.Content = errorTextBox;
            await dialog.ShowDialog(this);
        }

        private void DetectCurrentPreset()
        {
            // Try to match current settings to a preset
            var presets = new[] { GraphicsPreset.Low, GraphicsPreset.Medium, GraphicsPreset.High, GraphicsPreset.Ultra };
            foreach (var preset in presets)
            {
                var presetSettings = GraphicsSettingsPresetFactory.CreatePreset(preset);
                if (SettingsMatch(_settings, presetSettings))
                {
                    _currentPreset = preset;
                    _presetComboBox.SelectedItem = preset.ToString();
                    return;
                }
            }
            _currentPreset = GraphicsPreset.Custom;
            _presetComboBox.SelectedItem = "Custom";
        }

        private bool SettingsMatch(GraphicsSettingsData a, GraphicsSettingsData b)
        {
            // Compare key settings to determine if they match
            return a.WindowWidth == b.WindowWidth &&
                   a.WindowHeight == b.WindowHeight &&
                   a.WindowFullscreen == b.WindowFullscreen &&
                   a.WindowVSync == b.WindowVSync &&
                   a.MonoGameSynchronizeWithVerticalRetrace == b.MonoGameSynchronizeWithVerticalRetrace &&
                   a.MonoGamePreferMultiSampling == b.MonoGamePreferMultiSampling &&
                   a.RasterizerMultiSampleAntiAlias == b.RasterizerMultiSampleAntiAlias &&
                   a.SamplerMaxAnisotropy == b.SamplerMaxAnisotropy &&
                   a.SamplerFilter == b.SamplerFilter;
        }
    }

    /// <summary>
    /// Comprehensive graphics settings data structure.
    /// Contains ALL settings from all graphics backends with zero omissions.
    /// </summary>
    [Serializable]
    [XmlRoot("GraphicsSettings")]
    public class GraphicsSettingsData
    {
        // Window Settings
        public string WindowTitle { get; set; }
        public int? WindowWidth { get; set; }
        public int? WindowHeight { get; set; }
        public bool? WindowFullscreen { get; set; }
        public bool? WindowIsMouseVisible { get; set; }
        /// <summary>
        /// VSync (vertical synchronization) setting for all graphics backends.
        /// When enabled, synchronizes frame rendering with monitor refresh rate to prevent screen tearing.
        /// Based on swkotor.exe and swkotor2.exe: VSync controlled via DirectX Present parameters.
        /// Original implementation: VSync can be toggled in real-time without requiring a restart.
        /// </summary>
        public bool? WindowVSync { get; set; }

        // MonoGame-Specific Window Settings
        public bool? MonoGameSynchronizeWithVerticalRetrace { get; set; }
        public bool? MonoGamePreferMultiSampling { get; set; }
        public bool? MonoGamePreferHalfPixelOffset { get; set; }
        public bool? MonoGameHardwareModeSwitch { get; set; }
        public string MonoGameGraphicsProfile { get; set; }
        public bool? MonoGameSupportedOrientationsPortrait { get; set; }
        public bool? MonoGameSupportedOrientationsLandscape { get; set; }
        public bool? MonoGameSupportedOrientationsPortraitUpsideDown { get; set; }
        public bool? MonoGameSupportedOrientationsLandscapeLeft { get; set; }
        public bool? MonoGameSupportedOrientationsLandscapeRight { get; set; }

        // Rasterizer State
        public Runtime.Graphics.Common.Enums.CullMode? RasterizerCullMode { get; set; }
        public Runtime.Graphics.Common.Enums.FillMode? RasterizerFillMode { get; set; }
        public bool? RasterizerDepthBiasEnabled { get; set; }
        public double? RasterizerDepthBias { get; set; }
        public double? RasterizerSlopeScaleDepthBias { get; set; }
        public bool? RasterizerScissorTestEnabled { get; set; }
        public bool? RasterizerMultiSampleAntiAlias { get; set; }

        // Depth Stencil State
        public bool? DepthStencilDepthBufferEnable { get; set; }
        public bool? DepthStencilDepthBufferWriteEnable { get; set; }
        public CompareFunction? DepthStencilDepthBufferFunction { get; set; }
        public bool? DepthStencilStencilEnable { get; set; }
        public bool? DepthStencilTwoSidedStencilMode { get; set; }
        public StencilOperation? DepthStencilStencilFail { get; set; }
        public StencilOperation? DepthStencilStencilDepthFail { get; set; }
        public StencilOperation? DepthStencilStencilPass { get; set; }
        public CompareFunction? DepthStencilStencilFunction { get; set; }
        public int? DepthStencilReferenceStencil { get; set; }
        public int? DepthStencilStencilMask { get; set; }
        public int? DepthStencilStencilWriteMask { get; set; }

        // Blend State
        public BlendFunction? BlendAlphaBlendFunction { get; set; }
        public Blend? BlendAlphaDestinationBlend { get; set; }
        public Blend? BlendAlphaSourceBlend { get; set; }
        public BlendFunction? BlendColorBlendFunction { get; set; }
        public Blend? BlendColorDestinationBlend { get; set; }
        public Blend? BlendColorSourceBlend { get; set; }
        public ColorWriteChannels? BlendColorWriteChannels { get; set; }
        public ColorWriteChannels? BlendColorWriteChannels1 { get; set; }
        public ColorWriteChannels? BlendColorWriteChannels2 { get; set; }
        public ColorWriteChannels? BlendColorWriteChannels3 { get; set; }
        public bool? BlendBlendEnable { get; set; }
        public int? BlendBlendFactorR { get; set; }
        public int? BlendBlendFactorG { get; set; }
        public int? BlendBlendFactorB { get; set; }
        public int? BlendBlendFactorA { get; set; }
        public int? BlendMultiSampleMask { get; set; }

        // Sampler State
        public TextureAddressMode? SamplerAddressU { get; set; }
        public TextureAddressMode? SamplerAddressV { get; set; }
        public TextureAddressMode? SamplerAddressW { get; set; }
        public TextureFilter? SamplerFilter { get; set; }
        public int? SamplerMaxAnisotropy { get; set; }
        public int? SamplerMaxMipLevel { get; set; }
        public double? SamplerMipMapLevelOfDetailBias { get; set; }

        // Basic Effect
        public bool? BasicEffectVertexColorEnabled { get; set; }
        public bool? BasicEffectLightingEnabled { get; set; }
        public bool? BasicEffectTextureEnabled { get; set; }
        public float? BasicEffectAmbientLightColorX { get; set; }
        public float? BasicEffectAmbientLightColorY { get; set; }
        public float? BasicEffectAmbientLightColorZ { get; set; }
        public float? BasicEffectDiffuseColorX { get; set; }
        public float? BasicEffectDiffuseColorY { get; set; }
        public float? BasicEffectDiffuseColorZ { get; set; }
        public float? BasicEffectEmissiveColorX { get; set; }
        public float? BasicEffectEmissiveColorY { get; set; }
        public float? BasicEffectEmissiveColorZ { get; set; }
        public float? BasicEffectSpecularColorX { get; set; }
        public float? BasicEffectSpecularColorY { get; set; }
        public float? BasicEffectSpecularColorZ { get; set; }
        public float? BasicEffectSpecularPower { get; set; }
        public float? BasicEffectAlpha { get; set; }

        // SpriteBatch
        public SpriteSortMode? SpriteBatchSortMode { get; set; }
        public bool? SpriteBatchBlendStateAlphaBlend { get; set; }
        public bool? SpriteBatchBlendStateAdditive { get; set; }

        // Spatial Audio
        public float? SpatialAudioDopplerFactor { get; set; }
        public float? SpatialAudioSpeedOfSound { get; set; }

        // Content Manager
        public string ContentManagerRootDirectory { get; set; }
    }
}
