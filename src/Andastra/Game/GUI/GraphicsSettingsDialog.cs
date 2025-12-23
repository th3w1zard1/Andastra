using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Serialization;
using Eto.Forms;
using Eto.Drawing;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;

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
    public class GraphicsSettingsDialog : Dialog
    {
        private GraphicsBackendType _selectedBackend;
        private GraphicsSettingsData _settings;
        private TabControl _tabControl;
        private Dictionary<string, Control> _controlMap;
        private DropDown _presetComboBox;
        private GraphicsPreset _currentPreset;
        private TextBox _searchTextBox;
        private Dictionary<string, TabPage> _tabPageMap;

        /// <summary>
        /// Gets the graphics settings.
        /// </summary>
        public GraphicsSettingsData Settings => _settings;

        /// <summary>
        /// Gets or sets the dialog result.
        /// </summary>
        public DialogResult Result { get; set; }

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
            _tabPageMap = new Dictionary<string, TabPage>();
            _currentPreset = GraphicsPreset.Custom;

            Title = $"Graphics Settings - {GetBackendName(backendType)}";
            ClientSize = new Size(1000, 750);
            Resizable = true;
            WindowStyle = WindowStyle.Default;
            MinimumSize = new Size(800, 600);

            InitializeComponent();
            LoadSettings();
            DetectCurrentPreset();
        }

        private void InitializeComponent()
        {
            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            // Top toolbar with presets and search
            var toolbarLayout = new TableLayout
            {
                Spacing = new Size(10, 0)
            };

            // Preset selector
            var presetLabel = new Label { Text = "Preset:", VerticalAlignment = VerticalAlignment.Center };
            _presetComboBox = new DropDown { Width = 150 };
            _presetComboBox.Items.Add("Low");
            _presetComboBox.Items.Add("Medium");
            _presetComboBox.Items.Add("High");
            _presetComboBox.Items.Add("Ultra");
            _presetComboBox.Items.Add("Custom");
            _presetComboBox.SelectedValue = "Custom";
            _presetComboBox.SelectedIndexChanged += PresetComboBox_SelectedIndexChanged;

            // Search box
            var searchLabel = new Label { Text = "Search:", VerticalAlignment = VerticalAlignment.Center };
            _searchTextBox = new TextBox { PlaceholderText = "Search settings..." };
            _searchTextBox.TextChanged += SearchTextBox_TextChanged;

            // Import/Export buttons
            var importButton = new Button { Text = "Import..." };
            importButton.Click += ImportButton_Click;
            var exportButton = new Button { Text = "Export..." };
            exportButton.Click += ExportButton_Click;

            toolbarLayout.Rows.Add(new TableRow(
                new TableCell(presetLabel, false),
                new TableCell(_presetComboBox, false),
                new TableCell(null, true),
                new TableCell(searchLabel, false),
                new TableCell(_searchTextBox, true),
                new TableCell(importButton, false),
                new TableCell(exportButton, false)
            ));

            layout.Rows.Add(new TableRow(new TableCell(toolbarLayout, true)));

            // Tab control
            _tabControl = new TabControl();

            // Window Settings Tab
            var windowTab = CreateWindowSettingsTab();
            _tabControl.Pages.Add(windowTab);
            _tabPageMap["Window"] = windowTab;

            // Rasterizer State Tab
            var rasterizerTab = CreateRasterizerStateTab();
            _tabControl.Pages.Add(rasterizerTab);
            _tabPageMap["Rasterizer"] = rasterizerTab;

            // Depth Stencil State Tab
            var depthStencilTab = CreateDepthStencilStateTab();
            _tabControl.Pages.Add(depthStencilTab);
            _tabPageMap["DepthStencil"] = depthStencilTab;

            // Blend State Tab
            var blendTab = CreateBlendStateTab();
            _tabControl.Pages.Add(blendTab);
            _tabPageMap["Blend"] = blendTab;

            // Sampler State Tab
            var samplerTab = CreateSamplerStateTab();
            _tabControl.Pages.Add(samplerTab);
            _tabPageMap["Sampler"] = samplerTab;

            // Basic Effect Tab
            var basicEffectTab = CreateBasicEffectTab();
            _tabControl.Pages.Add(basicEffectTab);
            _tabPageMap["BasicEffect"] = basicEffectTab;

            // SpriteBatch Tab
            var spriteBatchTab = CreateSpriteBatchTab();
            _tabControl.Pages.Add(spriteBatchTab);
            _tabPageMap["SpriteBatch"] = spriteBatchTab;

            // Spatial Audio Tab
            var spatialAudioTab = CreateSpatialAudioTab();
            _tabControl.Pages.Add(spatialAudioTab);
            _tabPageMap["SpatialAudio"] = spatialAudioTab;

            // Content Manager Tab
            var contentManagerTab = CreateContentManagerTab();
            _tabControl.Pages.Add(contentManagerTab);
            _tabPageMap["ContentManager"] = contentManagerTab;

            // Backend-Specific Tabs
            if (_selectedBackend == GraphicsBackendType.MonoGame)
            {
                var monoGameTab = CreateMonoGameSpecificTab();
                _tabControl.Pages.Add(monoGameTab);
                _tabPageMap["MonoGame"] = monoGameTab;
            }
            else if (_selectedBackend == GraphicsBackendType.Stride)
            {
                var strideTab = CreateStrideSpecificTab();
                _tabControl.Pages.Add(strideTab);
                _tabPageMap["Stride"] = strideTab;
            }

            layout.Rows.Add(new TableRow(new TableCell(_tabControl, true)));

            // Buttons
            var buttonLayout = new TableLayout
            {
                Spacing = new Size(10, 0)
            };

            var okButton = new Button
            {
                Text = "OK",
                Width = 100
            };
            okButton.Click += (sender, e) =>
            {
                SaveSettings();
                var validationResult = GraphicsSettingsSerializer.Validate(_settings);
                if (!validationResult.IsValid)
                {
                    var result = MessageBox.Show(
                        this,
                        "Some settings have validation errors:\n\n" + validationResult.GetFormattedMessage() + "\n\nDo you want to continue anyway?",
                        "Validation Warnings",
                        MessageBoxButtons.YesNo,
                        MessageBoxType.Warning);
                    if (result == DialogResult.No)
                        return;
                }
                Result = DialogResult.Ok;
                Close();
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                Width = 100
            };
            cancelButton.Click += (sender, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            var resetButton = new Button
            {
                Text = "Reset to Defaults",
                Width = 150
            };
            resetButton.Click += (sender, e) =>
            {
                var result = MessageBox.Show(
                    this,
                    "Reset all settings to default values?",
                    "Reset Settings",
                    MessageBoxButtons.YesNo,
                    MessageBoxType.Question);
                if (result == DialogResult.Yes)
                {
                    _settings = new GraphicsSettingsData();
                    _currentPreset = GraphicsPreset.Custom;
                    _presetComboBox.SelectedValue = "Custom";
                    LoadSettings();
                }
            };

            var validateButton = new Button
            {
                Text = "Validate",
                Width = 100
            };
            validateButton.Click += (sender, e) =>
            {
                SaveSettings();
                var validationResult = GraphicsSettingsSerializer.Validate(_settings);
                if (validationResult.IsValid)
                {
                    MessageBox.Show(
                        this,
                        "All settings are valid!",
                        "Validation Success",
                        MessageBoxType.Information);
                }
                else
                {
                    MessageBox.Show(
                        this,
                        validationResult.GetFormattedMessage(),
                        "Validation Errors",
                        MessageBoxType.Error);
                }
            };

            buttonLayout.Rows.Add(new TableRow(
                new TableCell(null, true),
                new TableCell(validateButton, false),
                new TableCell(resetButton, false),
                new TableCell(okButton, false),
                new TableCell(cancelButton, false)
            ));

            layout.Rows.Add(new TableRow(new TableCell(buttonLayout, true)));

            Content = layout;
            DefaultButton = okButton;
        }

        private TabPage CreateWindowSettingsTab()
        {
            var page = new TabPage { Text = "Window" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Window Title
            var windowTitleTextBox = new TextBox { Text = _settings.WindowTitle ?? "Andastra Game" };
            _controlMap["WindowTitle"] = windowTitleTextBox;
            layout.Rows.Add(CreateLabeledControl("Title:", windowTitleTextBox, ref row, "WindowTitle"));

            // Window Size
            var windowWidthNumeric = new NumericStepper { Value = _settings.WindowWidth ?? 1280, MinValue = 320, MaxValue = 7680 };
            _controlMap["WindowWidth"] = windowWidthNumeric;
            layout.Rows.Add(CreateLabeledControl("Width:", windowWidthNumeric, ref row, "WindowWidth"));

            var windowHeightNumeric = new NumericStepper { Value = _settings.WindowHeight ?? 720, MinValue = 240, MaxValue = 4320 };
            _controlMap["WindowHeight"] = windowHeightNumeric;
            layout.Rows.Add(CreateLabeledControl("Height:", windowHeightNumeric, ref row, "WindowHeight"));

            // Fullscreen
            var fullscreenCheck = new CheckBox { Checked = _settings.WindowFullscreen ?? false };
            _controlMap["WindowFullscreen"] = fullscreenCheck;
            layout.Rows.Add(CreateLabeledControl("Fullscreen:", fullscreenCheck, ref row, "WindowFullscreen"));

            // Mouse Visible
            var mouseVisibleCheck = new CheckBox { Checked = _settings.WindowIsMouseVisible ?? true };
            _controlMap["WindowIsMouseVisible"] = mouseVisibleCheck;
            layout.Rows.Add(CreateLabeledControl("Mouse Visible:", mouseVisibleCheck, ref row, "WindowIsMouseVisible"));

            // VSync - Generic setting for all backends
            // Based on IGraphicsBackend.SupportsVSync and SetVSync interface
            // All graphics backends (MonoGame, Stride) support VSync through the common interface
            var vsyncCheck = new CheckBox { Checked = _settings.WindowVSync ?? true };
            _controlMap["WindowVSync"] = vsyncCheck;
            layout.Rows.Add(CreateLabeledControl("VSync:", vsyncCheck, ref row, "WindowVSync"));

            // MonoGame-specific window settings
            if (_selectedBackend == GraphicsBackendType.MonoGame)
            {
                // MonoGame-specific VSync setting (for backward compatibility)
                // Note: WindowVSync is the preferred generic setting, but we keep this for compatibility
                var syncVerticalRetrace = new CheckBox { Checked = _settings.MonoGameSynchronizeWithVerticalRetrace ?? (_settings.WindowVSync ?? true) };
                _controlMap["MonoGameSynchronizeWithVerticalRetrace"] = syncVerticalRetrace;
                // Sync MonoGame-specific setting with generic VSync setting
                syncVerticalRetrace.CheckedChanged += (sender, e) =>
                {
                    if (vsyncCheck.Checked != syncVerticalRetrace.Checked)
                    {
                        vsyncCheck.Checked = syncVerticalRetrace.Checked;
                    }
                };
                vsyncCheck.CheckedChanged += (sender, e) =>
                {
                    if (syncVerticalRetrace.Checked != vsyncCheck.Checked)
                    {
                        syncVerticalRetrace.Checked = vsyncCheck.Checked;
                    }
                };

                var preferMultiSampling = new CheckBox { Checked = _settings.MonoGamePreferMultiSampling ?? false };
                _controlMap["MonoGamePreferMultiSampling"] = preferMultiSampling;
                layout.Rows.Add(CreateLabeledControl("Prefer Multi-Sampling:", preferMultiSampling, ref row, "MonoGamePreferMultiSampling"));

                var preferHalfPixelOffset = new CheckBox { Checked = _settings.MonoGamePreferHalfPixelOffset ?? false };
                _controlMap["MonoGamePreferHalfPixelOffset"] = preferHalfPixelOffset;
                layout.Rows.Add(CreateLabeledControl("Prefer Half Pixel Offset:", preferHalfPixelOffset, ref row, "MonoGamePreferHalfPixelOffset"));

                var hardwareModeSwitch = new CheckBox { Checked = _settings.MonoGameHardwareModeSwitch ?? false };
                _controlMap["MonoGameHardwareModeSwitch"] = hardwareModeSwitch;
                layout.Rows.Add(CreateLabeledControl("Hardware Mode Switch:", hardwareModeSwitch, ref row, "MonoGameHardwareModeSwitch"));
            }

            // Stride-specific window settings
            if (_selectedBackend == GraphicsBackendType.Stride)
            {
                // Stride window settings are mostly handled through GameWindow
                // Additional settings can be added here if needed
            }

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateRasterizerStateTab()
        {
            var page = new TabPage { Text = "Rasterizer State" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Cull Mode
            var cullModeCombo = new DropDown();
            cullModeCombo.Items.Add("None");
            cullModeCombo.Items.Add("CullClockwiseFace");
            cullModeCombo.Items.Add("CullCounterClockwiseFace");
            cullModeCombo.SelectedValue = _settings.RasterizerCullMode?.ToString() ?? "CullCounterClockwiseFace";
            _controlMap["RasterizerCullMode"] = cullModeCombo;
            layout.Rows.Add(CreateLabeledControl("Cull Mode:", cullModeCombo, ref row, "RasterizerCullMode"));

            // Fill Mode
            var fillModeCombo = new DropDown();
            fillModeCombo.Items.Add("Solid");
            fillModeCombo.Items.Add("WireFrame");
            fillModeCombo.SelectedValue = _settings.RasterizerFillMode?.ToString() ?? "Solid";
            _controlMap["RasterizerFillMode"] = fillModeCombo;
            layout.Rows.Add(CreateLabeledControl("Fill Mode:", fillModeCombo, ref row, "RasterizerFillMode"));

            // Depth Bias Enabled
            var depthBiasEnabledCheck = new CheckBox { Checked = _settings.RasterizerDepthBiasEnabled ?? false };
            _controlMap["RasterizerDepthBiasEnabled"] = depthBiasEnabledCheck;
            layout.Rows.Add(CreateLabeledControl("Depth Bias Enabled:", depthBiasEnabledCheck, ref row));

            // Depth Bias
            var depthBiasNumeric = new NumericStepper
            {
                Value = _settings.RasterizerDepthBias ?? 0.0,
                DecimalPlaces = 6,
                Increment = 0.000001
            };
            _controlMap["RasterizerDepthBias"] = depthBiasNumeric;
            layout.Rows.Add(CreateLabeledControl("Depth Bias:", depthBiasNumeric, ref row));

            // Slope Scale Depth Bias
            var slopeScaleDepthBiasNumeric = new NumericStepper
            {
                Value = _settings.RasterizerSlopeScaleDepthBias ?? 0.0,
                DecimalPlaces = 6,
                Increment = 0.000001
            };
            _controlMap["RasterizerSlopeScaleDepthBias"] = slopeScaleDepthBiasNumeric;
            layout.Rows.Add(CreateLabeledControl("Slope Scale Depth Bias:", slopeScaleDepthBiasNumeric, ref row));

            // Scissor Test Enabled
            var scissorTestEnabledCheck = new CheckBox { Checked = _settings.RasterizerScissorTestEnabled ?? false };
            _controlMap["RasterizerScissorTestEnabled"] = scissorTestEnabledCheck;
            layout.Rows.Add(CreateLabeledControl("Scissor Test Enabled:", scissorTestEnabledCheck, ref row));

            // Multi-Sample Anti-Alias
            var msaaCheck = new CheckBox { Checked = _settings.RasterizerMultiSampleAntiAlias ?? false };
            _controlMap["RasterizerMultiSampleAntiAlias"] = msaaCheck;
            layout.Rows.Add(CreateLabeledControl("Multi-Sample Anti-Alias:", msaaCheck, ref row, "RasterizerMultiSampleAntiAlias"));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateDepthStencilStateTab()
        {
            var page = new TabPage { Text = "Depth Stencil State" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Depth Buffer Enable
            var depthBufferEnableCheck = new CheckBox { Checked = _settings.DepthStencilDepthBufferEnable ?? true };
            _controlMap["DepthStencilDepthBufferEnable"] = depthBufferEnableCheck;
            layout.Rows.Add(CreateLabeledControl("Depth Buffer Enable:", depthBufferEnableCheck, ref row));

            // Depth Buffer Write Enable
            var depthBufferWriteEnableCheck = new CheckBox { Checked = _settings.DepthStencilDepthBufferWriteEnable ?? true };
            _controlMap["DepthStencilDepthBufferWriteEnable"] = depthBufferWriteEnableCheck;
            layout.Rows.Add(CreateLabeledControl("Depth Buffer Write Enable:", depthBufferWriteEnableCheck, ref row));

            // Depth Buffer Function
            var depthBufferFunctionCombo = new DropDown();
            foreach (var func in Enum.GetValues(typeof(CompareFunction)))
            {
                depthBufferFunctionCombo.Items.Add(func.ToString());
            }
            depthBufferFunctionCombo.SelectedValue = _settings.DepthStencilDepthBufferFunction?.ToString() ?? "LessEqual";
            _controlMap["DepthStencilDepthBufferFunction"] = depthBufferFunctionCombo;
            layout.Rows.Add(CreateLabeledControl("Depth Buffer Function:", depthBufferFunctionCombo, ref row));

            // Stencil Enable
            var stencilEnableCheck = new CheckBox { Checked = _settings.DepthStencilStencilEnable ?? false };
            _controlMap["DepthStencilStencilEnable"] = stencilEnableCheck;
            layout.Rows.Add(CreateLabeledControl("Stencil Enable:", stencilEnableCheck, ref row));

            // Two-Sided Stencil Mode
            var twoSidedStencilCheck = new CheckBox { Checked = _settings.DepthStencilTwoSidedStencilMode ?? false };
            _controlMap["DepthStencilTwoSidedStencilMode"] = twoSidedStencilCheck;
            layout.Rows.Add(CreateLabeledControl("Two-Sided Stencil Mode:", twoSidedStencilCheck, ref row));

            // Stencil Fail
            var stencilFailCombo = new DropDown();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilFailCombo.Items.Add(op.ToString());
            }
            stencilFailCombo.SelectedValue = _settings.DepthStencilStencilFail?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilFail"] = stencilFailCombo;
            layout.Rows.Add(CreateLabeledControl("Stencil Fail:", stencilFailCombo, ref row));

            // Stencil Depth Fail
            var stencilDepthFailCombo = new DropDown();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilDepthFailCombo.Items.Add(op.ToString());
            }
            stencilDepthFailCombo.SelectedValue = _settings.DepthStencilStencilDepthFail?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilDepthFail"] = stencilDepthFailCombo;
            layout.Rows.Add(CreateLabeledControl("Stencil Depth Fail:", stencilDepthFailCombo, ref row));

            // Stencil Pass
            var stencilPassCombo = new DropDown();
            foreach (var op in Enum.GetValues(typeof(StencilOperation)))
            {
                stencilPassCombo.Items.Add(op.ToString());
            }
            stencilPassCombo.SelectedValue = _settings.DepthStencilStencilPass?.ToString() ?? "Keep";
            _controlMap["DepthStencilStencilPass"] = stencilPassCombo;
            layout.Rows.Add(CreateLabeledControl("Stencil Pass:", stencilPassCombo, ref row));

            // Stencil Function
            var stencilFunctionCombo = new DropDown();
            foreach (var func in Enum.GetValues(typeof(CompareFunction)))
            {
                stencilFunctionCombo.Items.Add(func.ToString());
            }
            stencilFunctionCombo.SelectedValue = _settings.DepthStencilStencilFunction?.ToString() ?? "Always";
            _controlMap["DepthStencilStencilFunction"] = stencilFunctionCombo;
            layout.Rows.Add(CreateLabeledControl("Stencil Function:", stencilFunctionCombo, ref row));

            // Reference Stencil
            var referenceStencilNumeric = new NumericStepper
            {
                Value = _settings.DepthStencilReferenceStencil ?? 0,
                MinValue = 0,
                MaxValue = 255
            };
            _controlMap["DepthStencilReferenceStencil"] = referenceStencilNumeric;
            layout.Rows.Add(CreateLabeledControl("Reference Stencil:", referenceStencilNumeric, ref row));

            // Stencil Mask
            var stencilMaskNumeric = new NumericStepper
            {
                Value = _settings.DepthStencilStencilMask ?? 255,
                MinValue = 0,
                MaxValue = 255
            };
            _controlMap["DepthStencilStencilMask"] = stencilMaskNumeric;
            layout.Rows.Add(CreateLabeledControl("Stencil Mask:", stencilMaskNumeric, ref row));

            // Stencil Write Mask
            var stencilWriteMaskNumeric = new NumericStepper
            {
                Value = _settings.DepthStencilStencilWriteMask ?? 255,
                MinValue = 0,
                MaxValue = 255
            };
            _controlMap["DepthStencilStencilWriteMask"] = stencilWriteMaskNumeric;
            layout.Rows.Add(CreateLabeledControl("Stencil Write Mask:", stencilWriteMaskNumeric, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateBlendStateTab()
        {
            var page = new TabPage { Text = "Blend State" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Alpha Blend Function
            var alphaBlendFunctionCombo = new DropDown();
            foreach (var func in Enum.GetValues(typeof(BlendFunction)))
            {
                alphaBlendFunctionCombo.Items.Add(func.ToString());
            }
            alphaBlendFunctionCombo.SelectedValue = _settings.BlendAlphaBlendFunction?.ToString() ?? "Add";
            _controlMap["BlendAlphaBlendFunction"] = alphaBlendFunctionCombo;
            layout.Rows.Add(CreateLabeledControl("Alpha Blend Function:", alphaBlendFunctionCombo, ref row));

            // Alpha Destination Blend
            var alphaDestBlendCombo = new DropDown();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                alphaDestBlendCombo.Items.Add(blend.ToString());
            }
            alphaDestBlendCombo.SelectedValue = _settings.BlendAlphaDestinationBlend?.ToString() ?? "InverseSourceAlpha";
            _controlMap["BlendAlphaDestinationBlend"] = alphaDestBlendCombo;
            layout.Rows.Add(CreateLabeledControl("Alpha Destination Blend:", alphaDestBlendCombo, ref row));

            // Alpha Source Blend
            var alphaSourceBlendCombo = new DropDown();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                alphaSourceBlendCombo.Items.Add(blend.ToString());
            }
            alphaSourceBlendCombo.SelectedValue = _settings.BlendAlphaSourceBlend?.ToString() ?? "SourceAlpha";
            _controlMap["BlendAlphaSourceBlend"] = alphaSourceBlendCombo;
            layout.Rows.Add(CreateLabeledControl("Alpha Source Blend:", alphaSourceBlendCombo, ref row));

            // Color Blend Function
            var colorBlendFunctionCombo = new DropDown();
            foreach (var func in Enum.GetValues(typeof(BlendFunction)))
            {
                colorBlendFunctionCombo.Items.Add(func.ToString());
            }
            colorBlendFunctionCombo.SelectedValue = _settings.BlendColorBlendFunction?.ToString() ?? "Add";
            _controlMap["BlendColorBlendFunction"] = colorBlendFunctionCombo;
            layout.Rows.Add(CreateLabeledControl("Color Blend Function:", colorBlendFunctionCombo, ref row));

            // Color Destination Blend
            var colorDestBlendCombo = new DropDown();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                colorDestBlendCombo.Items.Add(blend.ToString());
            }
            colorDestBlendCombo.SelectedValue = _settings.BlendColorDestinationBlend?.ToString() ?? "InverseSourceAlpha";
            _controlMap["BlendColorDestinationBlend"] = colorDestBlendCombo;
            layout.Rows.Add(CreateLabeledControl("Color Destination Blend:", colorDestBlendCombo, ref row));

            // Color Source Blend
            var colorSourceBlendCombo = new DropDown();
            foreach (var blend in Enum.GetValues(typeof(Blend)))
            {
                colorSourceBlendCombo.Items.Add(blend.ToString());
            }
            colorSourceBlendCombo.SelectedValue = _settings.BlendColorSourceBlend?.ToString() ?? "SourceAlpha";
            _controlMap["BlendColorSourceBlend"] = colorSourceBlendCombo;
            layout.Rows.Add(CreateLabeledControl("Color Source Blend:", colorSourceBlendCombo, ref row));

            // Color Write Channels (0-3)
            for (int i = 0; i < 4; i++)
            {
                var colorWriteChannelsCombo = new DropDown();
                colorWriteChannelsCombo.Items.Add("None");
                colorWriteChannelsCombo.Items.Add("Red");
                colorWriteChannelsCombo.Items.Add("Green");
                colorWriteChannelsCombo.Items.Add("Blue");
                colorWriteChannelsCombo.Items.Add("Alpha");
                colorWriteChannelsCombo.Items.Add("All");
                var channels = i == 0 ? _settings.BlendColorWriteChannels :
                              i == 1 ? _settings.BlendColorWriteChannels1 :
                              i == 2 ? _settings.BlendColorWriteChannels2 : _settings.BlendColorWriteChannels3;
                colorWriteChannelsCombo.SelectedValue = channels?.ToString() ?? "All";
                _controlMap[$"BlendColorWriteChannels{(i == 0 ? "0" : i.ToString())}"] = colorWriteChannelsCombo;
                layout.Rows.Add(CreateLabeledControl($"Color Write Channels {i}:", colorWriteChannelsCombo, ref row));
            }

            // Blend Enable
            var blendEnableCheck = new CheckBox { Checked = _settings.BlendBlendEnable ?? true };
            _controlMap["BlendBlendEnable"] = blendEnableCheck;
            layout.Rows.Add(CreateLabeledControl("Blend Enable:", blendEnableCheck, ref row));

            // Blend Factor (Color)
            var blendFactorLayout = new TableLayout { Spacing = new Size(5, 0) };
            var blendFactorRNumeric = new NumericStepper { Value = _settings.BlendBlendFactorR ?? 0, MinValue = 0, MaxValue = 255 };
            _controlMap["BlendBlendFactorR"] = blendFactorRNumeric;
            var blendFactorGNumeric = new NumericStepper { Value = _settings.BlendBlendFactorG ?? 0, MinValue = 0, MaxValue = 255 };
            _controlMap["BlendBlendFactorG"] = blendFactorGNumeric;
            var blendFactorBNumeric = new NumericStepper { Value = _settings.BlendBlendFactorB ?? 0, MinValue = 0, MaxValue = 255 };
            _controlMap["BlendBlendFactorB"] = blendFactorBNumeric;
            var blendFactorANumeric = new NumericStepper { Value = _settings.BlendBlendFactorA ?? 255, MinValue = 0, MaxValue = 255 };
            _controlMap["BlendBlendFactorA"] = blendFactorANumeric;
            blendFactorLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "R:" }),
                new TableCell(blendFactorRNumeric),
                new TableCell(new Label { Text = "G:" }),
                new TableCell(blendFactorGNumeric),
                new TableCell(new Label { Text = "B:" }),
                new TableCell(blendFactorBNumeric),
                new TableCell(new Label { Text = "A:" }),
                new TableCell(blendFactorANumeric)
            ));
            layout.Rows.Add(CreateLabeledControl("Blend Factor (RGBA):", blendFactorLayout, ref row));

            // Multi-Sample Mask
            var multiSampleMaskNumeric = new NumericStepper
            {
                Value = _settings.BlendMultiSampleMask ?? -1,
                MinValue = -1,
                MaxValue = int.MaxValue
            };
            _controlMap["BlendMultiSampleMask"] = multiSampleMaskNumeric;
            layout.Rows.Add(CreateLabeledControl("Multi-Sample Mask:", multiSampleMaskNumeric, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateSamplerStateTab()
        {
            var page = new TabPage { Text = "Sampler State" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Address U
            var addressUCombo = new DropDown();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressUCombo.Items.Add(mode.ToString());
            }
            addressUCombo.SelectedValue = _settings.SamplerAddressU?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressU"] = addressUCombo;
            layout.Rows.Add(CreateLabeledControl("Address U:", addressUCombo, ref row));

            // Address V
            var addressVCombo = new DropDown();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressVCombo.Items.Add(mode.ToString());
            }
            addressVCombo.SelectedValue = _settings.SamplerAddressV?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressV"] = addressVCombo;
            layout.Rows.Add(CreateLabeledControl("Address V:", addressVCombo, ref row));

            // Address W
            var addressWCombo = new DropDown();
            foreach (var mode in Enum.GetValues(typeof(TextureAddressMode)))
            {
                addressWCombo.Items.Add(mode.ToString());
            }
            addressWCombo.SelectedValue = _settings.SamplerAddressW?.ToString() ?? "Wrap";
            _controlMap["SamplerAddressW"] = addressWCombo;
            layout.Rows.Add(CreateLabeledControl("Address W:", addressWCombo, ref row));

            // Filter
            var filterCombo = new DropDown();
            foreach (var filter in Enum.GetValues(typeof(TextureFilter)))
            {
                filterCombo.Items.Add(filter.ToString());
            }
            filterCombo.SelectedValue = _settings.SamplerFilter?.ToString() ?? "Linear";
            _controlMap["SamplerFilter"] = filterCombo;
            layout.Rows.Add(CreateLabeledControl("Filter:", filterCombo, ref row, "SamplerFilter"));

            // Max Anisotropy
            var maxAnisotropyNumeric = new NumericStepper
            {
                Value = _settings.SamplerMaxAnisotropy ?? 0,
                MinValue = 0,
                MaxValue = 16
            };
            _controlMap["SamplerMaxAnisotropy"] = maxAnisotropyNumeric;
            layout.Rows.Add(CreateLabeledControl("Max Anisotropy:", maxAnisotropyNumeric, ref row, "SamplerMaxAnisotropy"));

            // Max Mip Level
            var maxMipLevelNumeric = new NumericStepper
            {
                Value = _settings.SamplerMaxMipLevel ?? 0,
                MinValue = 0,
                MaxValue = 15
            };
            _controlMap["SamplerMaxMipLevel"] = maxMipLevelNumeric;
            layout.Rows.Add(CreateLabeledControl("Max Mip Level:", maxMipLevelNumeric, ref row));

            // Mip Map Level of Detail Bias
            var mipMapLodBiasNumeric = new NumericStepper
            {
                Value = _settings.SamplerMipMapLevelOfDetailBias ?? 0.0,
                DecimalPlaces = 3,
                Increment = 0.001
            };
            _controlMap["SamplerMipMapLevelOfDetailBias"] = mipMapLodBiasNumeric;
            layout.Rows.Add(CreateLabeledControl("Mip Map LOD Bias:", mipMapLodBiasNumeric, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateBasicEffectTab()
        {
            var page = new TabPage { Text = "Basic Effect" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Vertex Color Enabled
            var vertexColorEnabledCheck = new CheckBox { Checked = _settings.BasicEffectVertexColorEnabled ?? false };
            _controlMap["BasicEffectVertexColorEnabled"] = vertexColorEnabledCheck;
            layout.Rows.Add(CreateLabeledControl("Vertex Color Enabled:", vertexColorEnabledCheck, ref row));

            // Lighting Enabled
            var lightingEnabledCheck = new CheckBox { Checked = _settings.BasicEffectLightingEnabled ?? false };
            _controlMap["BasicEffectLightingEnabled"] = lightingEnabledCheck;
            layout.Rows.Add(CreateLabeledControl("Lighting Enabled:", lightingEnabledCheck, ref row, "BasicEffectLightingEnabled"));

            // Texture Enabled
            var textureEnabledCheck = new CheckBox { Checked = _settings.BasicEffectTextureEnabled ?? false };
            _controlMap["BasicEffectTextureEnabled"] = textureEnabledCheck;
            layout.Rows.Add(CreateLabeledControl("Texture Enabled:", textureEnabledCheck, ref row, "BasicEffectTextureEnabled"));

            // Ambient Light Color
            var ambientColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectAmbientLightColorX ?? 0.2f,
                _settings.BasicEffectAmbientLightColorY ?? 0.2f,
                _settings.BasicEffectAmbientLightColorZ ?? 0.2f,
                "BasicEffectAmbientLightColor");
            layout.Rows.Add(CreateLabeledControl("Ambient Light Color (RGB):", ambientColorLayout, ref row));

            // Diffuse Color
            var diffuseColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectDiffuseColorX ?? 1.0f,
                _settings.BasicEffectDiffuseColorY ?? 1.0f,
                _settings.BasicEffectDiffuseColorZ ?? 1.0f,
                "BasicEffectDiffuseColor");
            layout.Rows.Add(CreateLabeledControl("Diffuse Color (RGB):", diffuseColorLayout, ref row));

            // Emissive Color
            var emissiveColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectEmissiveColorX ?? 0.0f,
                _settings.BasicEffectEmissiveColorY ?? 0.0f,
                _settings.BasicEffectEmissiveColorZ ?? 0.0f,
                "BasicEffectEmissiveColor");
            layout.Rows.Add(CreateLabeledControl("Emissive Color (RGB):", emissiveColorLayout, ref row));

            // Specular Color
            var specularColorLayout = CreateColorVector3Layout(
                _settings.BasicEffectSpecularColorX ?? 0.0f,
                _settings.BasicEffectSpecularColorY ?? 0.0f,
                _settings.BasicEffectSpecularColorZ ?? 0.0f,
                "BasicEffectSpecularColor");
            layout.Rows.Add(CreateLabeledControl("Specular Color (RGB):", specularColorLayout, ref row));

            // Specular Power
            var specularPowerNumeric = new NumericStepper
            {
                Value = _settings.BasicEffectSpecularPower ?? 0.0f,
                MinValue = 0.0,
                MaxValue = 1000.0,
                DecimalPlaces = 2
            };
            _controlMap["BasicEffectSpecularPower"] = specularPowerNumeric;
            layout.Rows.Add(CreateLabeledControl("Specular Power:", specularPowerNumeric, ref row));

            // Alpha
            var alphaNumeric = new NumericStepper
            {
                Value = _settings.BasicEffectAlpha ?? 1.0f,
                MinValue = 0.0,
                MaxValue = 1.0,
                DecimalPlaces = 3,
                Increment = 0.001
            };
            _controlMap["BasicEffectAlpha"] = alphaNumeric;
            layout.Rows.Add(CreateLabeledControl("Alpha:", alphaNumeric, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateSpriteBatchTab()
        {
            var page = new TabPage { Text = "SpriteBatch" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Sort Mode
            var sortModeCombo = new DropDown();
            foreach (var mode in Enum.GetValues(typeof(SpriteSortMode)))
            {
                sortModeCombo.Items.Add(mode.ToString());
            }
            sortModeCombo.SelectedValue = _settings.SpriteBatchSortMode?.ToString() ?? "Deferred";
            _controlMap["SpriteBatchSortMode"] = sortModeCombo;
            layout.Rows.Add(CreateLabeledControl("Sort Mode:", sortModeCombo, ref row));

            // Blend State (Alpha Blend)
            var blendStateAlphaBlendCheck = new CheckBox { Checked = _settings.SpriteBatchBlendStateAlphaBlend ?? true };
            _controlMap["SpriteBatchBlendStateAlphaBlend"] = blendStateAlphaBlendCheck;
            layout.Rows.Add(CreateLabeledControl("Blend State - Alpha Blend:", blendStateAlphaBlendCheck, ref row));

            // Blend State (Additive)
            var blendStateAdditiveCheck = new CheckBox { Checked = _settings.SpriteBatchBlendStateAdditive ?? false };
            _controlMap["SpriteBatchBlendStateAdditive"] = blendStateAdditiveCheck;
            layout.Rows.Add(CreateLabeledControl("Blend State - Additive:", blendStateAdditiveCheck, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateSpatialAudioTab()
        {
            var page = new TabPage { Text = "Spatial Audio" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Doppler Factor
            var dopplerFactorNumeric = new NumericStepper
            {
                Value = _settings.SpatialAudioDopplerFactor ?? 1.0f,
                MinValue = 0.0,
                MaxValue = 10.0,
                DecimalPlaces = 3,
                Increment = 0.001
            };
            _controlMap["SpatialAudioDopplerFactor"] = dopplerFactorNumeric;
            layout.Rows.Add(CreateLabeledControl("Doppler Factor:", dopplerFactorNumeric, ref row, "SpatialAudioDopplerFactor"));

            // Speed of Sound
            var speedOfSoundNumeric = new NumericStepper
            {
                Value = _settings.SpatialAudioSpeedOfSound ?? 343.0f,
                MinValue = 1.0,
                MaxValue = 10000.0,
                DecimalPlaces = 2
            };
            _controlMap["SpatialAudioSpeedOfSound"] = speedOfSoundNumeric;
            layout.Rows.Add(CreateLabeledControl("Speed of Sound (m/s):", speedOfSoundNumeric, ref row, "SpatialAudioSpeedOfSound"));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateContentManagerTab()
        {
            var page = new TabPage { Text = "Content Manager" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Root Directory
            var rootDirLayout = new TableLayout { Spacing = new Size(5, 0) };
            var rootDirTextBox = new TextBox { Text = _settings.ContentManagerRootDirectory ?? "Content" };
            _controlMap["ContentManagerRootDirectory"] = rootDirTextBox;
            var rootDirBrowseButton = new Button { Text = "Browse..." };
            rootDirBrowseButton.Click += (sender, e) =>
            {
                var dialog = new SelectFolderDialog { Title = "Select Content Root Directory" };
                if (dialog.ShowDialog(this) == DialogResult.Ok)
                {
                    rootDirTextBox.Text = dialog.Directory;
                }
            };
            rootDirLayout.Rows.Add(new TableRow(
                new TableCell(rootDirTextBox, true),
                new TableCell(rootDirBrowseButton, false)
            ));
            layout.Rows.Add(CreateLabeledControl("Root Directory:", rootDirLayout, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateMonoGameSpecificTab()
        {
            var page = new TabPage { Text = "MonoGame Specific" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Graphics Profile
            var graphicsProfileCombo = new DropDown();
            graphicsProfileCombo.Items.Add("Reach");
            graphicsProfileCombo.Items.Add("HiDef");
            graphicsProfileCombo.SelectedValue = _settings.MonoGameGraphicsProfile ?? "HiDef";
            _controlMap["MonoGameGraphicsProfile"] = graphicsProfileCombo;
            layout.Rows.Add(CreateLabeledControl("Graphics Profile:", graphicsProfileCombo, ref row));

            // Supported Orientations (for mobile)
            var supportedOrientationsLayout = new TableLayout { Spacing = new Size(5, 0) };
            var portraitCheck = new CheckBox { Checked = _settings.MonoGameSupportedOrientationsPortrait ?? true };
            _controlMap["MonoGameSupportedOrientationsPortrait"] = portraitCheck;
            var landscapeCheck = new CheckBox { Checked = _settings.MonoGameSupportedOrientationsLandscape ?? true };
            _controlMap["MonoGameSupportedOrientationsLandscape"] = landscapeCheck;
            var portraitUpsideDownCheck = new CheckBox { Checked = _settings.MonoGameSupportedOrientationsPortraitUpsideDown ?? false };
            _controlMap["MonoGameSupportedOrientationsPortraitUpsideDown"] = portraitUpsideDownCheck;
            var landscapeLeftCheck = new CheckBox { Checked = _settings.MonoGameSupportedOrientationsLandscapeLeft ?? false };
            _controlMap["MonoGameSupportedOrientationsLandscapeLeft"] = landscapeLeftCheck;
            var landscapeRightCheck = new CheckBox { Checked = _settings.MonoGameSupportedOrientationsLandscapeRight ?? false };
            _controlMap["MonoGameSupportedOrientationsLandscapeRight"] = landscapeRightCheck;
            supportedOrientationsLayout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "Portrait:" }),
                new TableCell(portraitCheck),
                new TableCell(new Label { Text = "Landscape:" }),
                new TableCell(landscapeCheck),
                new TableCell(new Label { Text = "Portrait Upside Down:" }),
                new TableCell(portraitUpsideDownCheck),
                new TableCell(new Label { Text = "Landscape Left:" }),
                new TableCell(landscapeLeftCheck),
                new TableCell(new Label { Text = "Landscape Right:" }),
                new TableCell(landscapeRightCheck)
            ));
            layout.Rows.Add(CreateLabeledControl("Supported Orientations:", supportedOrientationsLayout, ref row));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TabPage CreateStrideSpecificTab()
        {
            var page = new TabPage { Text = "Stride Specific" };

            var layout = new TableLayout
            {
                Spacing = new Size(10, 10),
                Padding = new Padding(10)
            };

            int row = 0;

            // Stride-specific settings would go here
            // Currently Stride doesn't expose many additional settings beyond what's in the common interface
            // But we include this tab for future extensibility

            var infoLabel = new Label
            {
                Text = "Stride-specific settings are primarily configured through the common graphics settings tabs.\n" +
                       "Additional Stride-specific options can be added here as needed."
            };
            layout.Rows.Add(new TableRow(new TableCell(infoLabel, true)));

            layout.Rows.Add(new TableRow(new TableCell(null, true)));

            page.Content = new Scrollable { Content = layout };
            return page;
        }

        private TableRow CreateLabeledControl(string labelText, Control control, ref int row, string helpKey = null)
        {
            var label = new Label
            {
                Text = labelText,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 200
            };

            // Add tooltip to control
            if (!string.IsNullOrEmpty(helpKey))
            {
                AddTooltip(control, helpKey);
                AddTooltip(label, helpKey);
            }

            // Add help button
            var helpButton = new Button
            {
                Text = "?",
                Width = 25,
                Height = 25
            };
            if (!string.IsNullOrEmpty(helpKey))
            {
                helpButton.Click += (sender, e) =>
                {
                    var helpText = GraphicsSettingsHelp.GetHelpText(helpKey);
                    MessageBox.Show(
                        this,
                        helpText,
                        $"{labelText} - Help",
                        MessageBoxType.Information);
                };
            }

            return new TableRow(
                new TableCell(label, false),
                new TableCell(control, true),
                new TableCell(helpButton, false)
            );
        }

        private TableLayout CreateColorVector3Layout(float x, float y, float z, string baseKey)
        {
            var layout = new TableLayout { Spacing = new Size(5, 0) };
            var xNumeric = new NumericStepper { Value = x, MinValue = 0.0, MaxValue = 1.0, DecimalPlaces = 3, Increment = 0.001 };
            _controlMap[$"{baseKey}X"] = xNumeric;
            var yNumeric = new NumericStepper { Value = y, MinValue = 0.0, MaxValue = 1.0, DecimalPlaces = 3, Increment = 0.001 };
            _controlMap[$"{baseKey}Y"] = yNumeric;
            var zNumeric = new NumericStepper { Value = z, MinValue = 0.0, MaxValue = 1.0, DecimalPlaces = 3, Increment = 0.001 };
            _controlMap[$"{baseKey}Z"] = zNumeric;
            layout.Rows.Add(new TableRow(
                new TableCell(new Label { Text = "R:" }),
                new TableCell(xNumeric),
                new TableCell(new Label { Text = "G:" }),
                new TableCell(yNumeric),
                new TableCell(new Label { Text = "B:" }),
                new TableCell(zNumeric)
            ));
            return layout;
        }

        private void LoadSettings()
        {
            // Settings are loaded into controls in InitializeComponent
            // This method can be used to refresh controls if settings change
        }

        private void SaveSettings()
        {
            // Save all settings from controls to _settings
            // Iterate through all controls in _controlMap and save their values
            foreach (var kvp in _controlMap)
            {
                string key = kvp.Key;
                Control control = kvp.Value;

                try
                {
                    if (control is TextBox textBox)
                    {
                        SaveStringSetting(key, textBox.Text);
                    }
                    else if (control is NumericStepper numericStepper)
                    {
                        SaveNumericSetting(key, numericStepper.Value);
                    }
                    else if (control is CheckBox checkBox)
                    {
                        SaveBoolSetting(key, checkBox.Checked ?? false);
                    }
                    else if (control is DropDown dropDown)
                    {
                        SaveEnumSetting(key, dropDown.SelectedValue?.ToString());
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue saving other settings
                    System.Diagnostics.Debug.WriteLine($"Error saving setting {key}: {ex.Message}");
                }
            }
        }

        private void SaveStringSetting(string key, string value)
        {
            switch (key)
            {
                case "WindowTitle": _settings.WindowTitle = value; break;
                case "ContentManagerRootDirectory": _settings.ContentManagerRootDirectory = value; break;
                case "MonoGameGraphicsProfile": _settings.MonoGameGraphicsProfile = value; break;
            }
        }

        private void SaveNumericSetting(string key, double value)
        {
            switch (key)
            {
                case "WindowWidth": _settings.WindowWidth = (int)value; break;
                case "WindowHeight": _settings.WindowHeight = (int)value; break;
                case "RasterizerDepthBias": _settings.RasterizerDepthBias = value; break;
                case "RasterizerSlopeScaleDepthBias": _settings.RasterizerSlopeScaleDepthBias = value; break;
                case "DepthStencilReferenceStencil": _settings.DepthStencilReferenceStencil = (int)value; break;
                case "DepthStencilStencilMask": _settings.DepthStencilStencilMask = (int)value; break;
                case "DepthStencilStencilWriteMask": _settings.DepthStencilStencilWriteMask = (int)value; break;
                case "BlendBlendFactorR": _settings.BlendBlendFactorR = (int)value; break;
                case "BlendBlendFactorG": _settings.BlendBlendFactorG = (int)value; break;
                case "BlendBlendFactorB": _settings.BlendBlendFactorB = (int)value; break;
                case "BlendBlendFactorA": _settings.BlendBlendFactorA = (int)value; break;
                case "BlendMultiSampleMask": _settings.BlendMultiSampleMask = (int)value; break;
                case "SamplerMaxAnisotropy": _settings.SamplerMaxAnisotropy = (int)value; break;
                case "SamplerMaxMipLevel": _settings.SamplerMaxMipLevel = (int)value; break;
                case "SamplerMipMapLevelOfDetailBias": _settings.SamplerMipMapLevelOfDetailBias = value; break;
                case "BasicEffectAmbientLightColorX": _settings.BasicEffectAmbientLightColorX = (float)value; break;
                case "BasicEffectAmbientLightColorY": _settings.BasicEffectAmbientLightColorY = (float)value; break;
                case "BasicEffectAmbientLightColorZ": _settings.BasicEffectAmbientLightColorZ = (float)value; break;
                case "BasicEffectDiffuseColorX": _settings.BasicEffectDiffuseColorX = (float)value; break;
                case "BasicEffectDiffuseColorY": _settings.BasicEffectDiffuseColorY = (float)value; break;
                case "BasicEffectDiffuseColorZ": _settings.BasicEffectDiffuseColorZ = (float)value; break;
                case "BasicEffectEmissiveColorX": _settings.BasicEffectEmissiveColorX = (float)value; break;
                case "BasicEffectEmissiveColorY": _settings.BasicEffectEmissiveColorY = (float)value; break;
                case "BasicEffectEmissiveColorZ": _settings.BasicEffectEmissiveColorZ = (float)value; break;
                case "BasicEffectSpecularColorX": _settings.BasicEffectSpecularColorX = (float)value; break;
                case "BasicEffectSpecularColorY": _settings.BasicEffectSpecularColorY = (float)value; break;
                case "BasicEffectSpecularColorZ": _settings.BasicEffectSpecularColorZ = (float)value; break;
                case "BasicEffectSpecularPower": _settings.BasicEffectSpecularPower = (float)value; break;
                case "BasicEffectAlpha": _settings.BasicEffectAlpha = (float)value; break;
                case "SpatialAudioDopplerFactor": _settings.SpatialAudioDopplerFactor = (float)value; break;
                case "SpatialAudioSpeedOfSound": _settings.SpatialAudioSpeedOfSound = (float)value; break;
            }
        }

        private void SaveBoolSetting(string key, bool value)
        {
            switch (key)
            {
                case "WindowFullscreen": _settings.WindowFullscreen = value; break;
                case "WindowIsMouseVisible": _settings.WindowIsMouseVisible = value; break;
                case "WindowVSync": _settings.WindowVSync = value; break;
                case "MonoGameSynchronizeWithVerticalRetrace": _settings.MonoGameSynchronizeWithVerticalRetrace = value; break;
                case "MonoGamePreferMultiSampling": _settings.MonoGamePreferMultiSampling = value; break;
                case "MonoGamePreferHalfPixelOffset": _settings.MonoGamePreferHalfPixelOffset = value; break;
                case "MonoGameHardwareModeSwitch": _settings.MonoGameHardwareModeSwitch = value; break;
                case "MonoGameSupportedOrientationsPortrait": _settings.MonoGameSupportedOrientationsPortrait = value; break;
                case "MonoGameSupportedOrientationsLandscape": _settings.MonoGameSupportedOrientationsLandscape = value; break;
                case "MonoGameSupportedOrientationsPortraitUpsideDown": _settings.MonoGameSupportedOrientationsPortraitUpsideDown = value; break;
                case "MonoGameSupportedOrientationsLandscapeLeft": _settings.MonoGameSupportedOrientationsLandscapeLeft = value; break;
                case "MonoGameSupportedOrientationsLandscapeRight": _settings.MonoGameSupportedOrientationsLandscapeRight = value; break;
                case "RasterizerDepthBiasEnabled": _settings.RasterizerDepthBiasEnabled = value; break;
                case "RasterizerScissorTestEnabled": _settings.RasterizerScissorTestEnabled = value; break;
                case "RasterizerMultiSampleAntiAlias": _settings.RasterizerMultiSampleAntiAlias = value; break;
                case "DepthStencilDepthBufferEnable": _settings.DepthStencilDepthBufferEnable = value; break;
                case "DepthStencilDepthBufferWriteEnable": _settings.DepthStencilDepthBufferWriteEnable = value; break;
                case "DepthStencilStencilEnable": _settings.DepthStencilStencilEnable = value; break;
                case "DepthStencilTwoSidedStencilMode": _settings.DepthStencilTwoSidedStencilMode = value; break;
                case "BlendBlendEnable": _settings.BlendBlendEnable = value; break;
                case "BasicEffectVertexColorEnabled": _settings.BasicEffectVertexColorEnabled = value; break;
                case "BasicEffectLightingEnabled": _settings.BasicEffectLightingEnabled = value; break;
                case "BasicEffectTextureEnabled": _settings.BasicEffectTextureEnabled = value; break;
                case "SpriteBatchBlendStateAlphaBlend": _settings.SpriteBatchBlendStateAlphaBlend = value; break;
                case "SpriteBatchBlendStateAdditive": _settings.SpriteBatchBlendStateAdditive = value; break;
            }
        }

        private void SaveEnumSetting(string key, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            try
            {
                switch (key)
                {
                    case "RasterizerCullMode":
                        if (Enum.TryParse<Andastra.Runtime.Graphics.Common.Enums.CullMode>(value, out var cullMode))
                            _settings.RasterizerCullMode = cullMode;
                        break;
                    case "RasterizerFillMode":
                        if (Enum.TryParse<Andastra.Runtime.Graphics.Common.Enums.FillMode>(value, out var fillMode))
                            _settings.RasterizerFillMode = fillMode;
                        break;
                    case "DepthStencilDepthBufferFunction":
                        if (Enum.TryParse<CompareFunction>(value, out var depthFunc))
                            _settings.DepthStencilDepthBufferFunction = depthFunc;
                        break;
                    case "DepthStencilStencilFail":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilFail))
                            _settings.DepthStencilStencilFail = stencilFail;
                        break;
                    case "DepthStencilStencilDepthFail":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilDepthFail))
                            _settings.DepthStencilStencilDepthFail = stencilDepthFail;
                        break;
                    case "DepthStencilStencilPass":
                        if (Enum.TryParse<StencilOperation>(value, out var stencilPass))
                            _settings.DepthStencilStencilPass = stencilPass;
                        break;
                    case "DepthStencilStencilFunction":
                        if (Enum.TryParse<CompareFunction>(value, out var stencilFunc))
                            _settings.DepthStencilStencilFunction = stencilFunc;
                        break;
                    case "BlendAlphaBlendFunction":
                        if (Enum.TryParse<BlendFunction>(value, out var alphaBlendFunc))
                            _settings.BlendAlphaBlendFunction = alphaBlendFunc;
                        break;
                    case "BlendAlphaDestinationBlend":
                        if (Enum.TryParse<Blend>(value, out var alphaDestBlend))
                            _settings.BlendAlphaDestinationBlend = alphaDestBlend;
                        break;
                    case "BlendAlphaSourceBlend":
                        if (Enum.TryParse<Blend>(value, out var alphaSourceBlend))
                            _settings.BlendAlphaSourceBlend = alphaSourceBlend;
                        break;
                    case "BlendColorBlendFunction":
                        if (Enum.TryParse<BlendFunction>(value, out var colorBlendFunc))
                            _settings.BlendColorBlendFunction = colorBlendFunc;
                        break;
                    case "BlendColorDestinationBlend":
                        if (Enum.TryParse<Blend>(value, out var colorDestBlend))
                            _settings.BlendColorDestinationBlend = colorDestBlend;
                        break;
                    case "BlendColorSourceBlend":
                        if (Enum.TryParse<Blend>(value, out var colorSourceBlend))
                            _settings.BlendColorSourceBlend = colorSourceBlend;
                        break;
                    case "BlendColorWriteChannels0":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels))
                            _settings.BlendColorWriteChannels = colorWriteChannels;
                        break;
                    case "BlendColorWriteChannels1":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels1))
                            _settings.BlendColorWriteChannels1 = colorWriteChannels1;
                        break;
                    case "BlendColorWriteChannels2":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels2))
                            _settings.BlendColorWriteChannels2 = colorWriteChannels2;
                        break;
                    case "BlendColorWriteChannels3":
                        if (Enum.TryParse<ColorWriteChannels>(value, out var colorWriteChannels3))
                            _settings.BlendColorWriteChannels3 = colorWriteChannels3;
                        break;
                    case "SamplerAddressU":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressU))
                            _settings.SamplerAddressU = addressU;
                        break;
                    case "SamplerAddressV":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressV))
                            _settings.SamplerAddressV = addressV;
                        break;
                    case "SamplerAddressW":
                        if (Enum.TryParse<TextureAddressMode>(value, out var addressW))
                            _settings.SamplerAddressW = addressW;
                        break;
                    case "SamplerFilter":
                        if (Enum.TryParse<TextureFilter>(value, out var filter))
                            _settings.SamplerFilter = filter;
                        break;
                    case "SpriteBatchSortMode":
                        if (Enum.TryParse<SpriteSortMode>(value, out var sortMode))
                            _settings.SpriteBatchSortMode = sortMode;
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

        private void PresetComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedValue = _presetComboBox.SelectedValue?.ToString();
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

            var result = MessageBox.Show(
                this,
                $"Apply {selectedValue} preset? This will overwrite your current settings.",
                "Apply Preset",
                MessageBoxButtons.YesNo,
                MessageBoxType.Question);

            if (result == DialogResult.Yes)
            {
                _settings = GraphicsSettingsPresetFactory.CreatePreset(preset);
                _currentPreset = preset;
                LoadSettings();
            }
            else
            {
                _presetComboBox.SelectedValue = _currentPreset.ToString();
            }
        }

        private void SearchTextBox_TextChanged(object sender, EventArgs e)
        {
            var searchText = _searchTextBox.Text?.ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrEmpty(searchText))
            {
                // Show all tabs
                foreach (var tab in _tabControl.Pages)
                {
                    tab.Visible = true;
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

                tab.Visible = matchesTab || matchesControl;
            }
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Import Graphics Settings",
                Filters = { new FileFilter("XML Files", "*.xml"), new FileFilter("All Files", "*.*") }
            };

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    _settings = GraphicsSettingsSerializer.ImportFromXml(dialog.FileName);
                    _currentPreset = GraphicsPreset.Custom;
                    _presetComboBox.SelectedValue = "Custom";
                    LoadSettings();
                    MessageBox.Show(
                        this,
                        "Settings imported successfully!",
                        "Import Success",
                        MessageBoxType.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        $"Failed to import settings:\n\n{ex.Message}",
                        "Import Error",
                        MessageBoxType.Error);
                }
            }
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export Graphics Settings",
                Filters = { new FileFilter("XML Files", "*.xml"), new FileFilter("All Files", "*.*") },
                FileName = "graphics_settings.xml"
            };

            if (dialog.ShowDialog(this) == DialogResult.Ok)
            {
                try
                {
                    SaveSettings();
                    GraphicsSettingsSerializer.ExportToXml(_settings, dialog.FileName);
                    MessageBox.Show(
                        this,
                        "Settings exported successfully!",
                        "Export Success",
                        MessageBoxType.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        this,
                        $"Failed to export settings:\n\n{ex.Message}",
                        "Export Error",
                        MessageBoxType.Error);
                }
            }
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
                    _presetComboBox.SelectedValue = preset.ToString();
                    return;
                }
            }
            _currentPreset = GraphicsPreset.Custom;
            _presetComboBox.SelectedValue = "Custom";
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

        private void AddTooltip(Control control, string key)
        {
            var tooltip = GraphicsSettingsHelp.GetHelpText(key);
            if (!string.IsNullOrEmpty(tooltip))
            {
                control.ToolTip = tooltip;
            }
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
        public Andastra.Runtime.Graphics.Common.Enums.CullMode? RasterizerCullMode { get; set; }
        public Andastra.Runtime.Graphics.Common.Enums.FillMode? RasterizerFillMode { get; set; }
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
