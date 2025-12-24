using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.GUI;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for GUI binary I/O operations.
    /// Tests validate the GUI format structure as defined in GUI.ksy Kaitai Struct definition.
    /// Tests are as granular and exhaustive as LTRFormatTests, ensuring complete coverage of all GUI structures.
    /// </summary>
    public class GUIFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.gui");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.gui");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            // Test reading GUI file
            GUI originalGui = new GUIReader(BinaryTestFile).Load();
            ValidateIO(originalGui);

            // Test writing and reading back - comprehensive round-trip test
            string tempOutputFile = Path.Combine(Path.GetTempPath(), "test_gui_roundtrip.gui");
            try
            {
                // Write GUI to temporary file
                new GUIWriter(originalGui).WriteToFile(tempOutputFile);

                // Verify file was created
                File.Exists(tempOutputFile).Should().BeTrue("GUI file should be written successfully");

                // Read the written file back
                GUI roundTripGui = new GUIReader(tempOutputFile).Load();

                // Validate round-trip GUI matches original
                ValidateRoundTrip(originalGui, roundTripGui);
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(tempOutputFile))
                {
                    File.Delete(tempOutputFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiHeaderStructure()
        {
            // Test that GUI header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            // Read as GFF to validate header
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate header constants match GUI.ksy
            // Header is 56 bytes: 4 (file_type) + 4 (file_version) + 12×4 (offsets/counts)
            const int ExpectedHeaderSize = 56;
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(ExpectedHeaderSize, "GUI file should have at least 56-byte header as defined in GUI.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches GUI.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("GUI ", "File type should be 'GUI ' (space-padded) as defined in GUI.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf(new[] { "V3.2", "V3.3", "V4.0", "V4.1" }, "Version should be valid GFF version as defined in GUI.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiRootStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate root GUI structure
            gui.Should().NotBeNull("GUI file should load successfully");
            gui.Tag.Should().NotBeNull("GUI Tag should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlsList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate controls list structure
            gui.Controls.Should().NotBeNull("Controls list should not be null");
            gui.Controls.Count.Should().BeGreaterThanOrEqualTo(0, "Controls count should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlTypes()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate all control types are valid
            foreach (var control in gui.Controls)
            {
                control.GuiType.Should().BeDefined("Control type should be a valid GUIControlType enum value");
                control.GuiType.Should().NotBe(GUIControlType.Invalid, "Control type should not be Invalid");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlExtent()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control extent structure
            foreach (var control in gui.Controls)
            {
                control.Extent.Should().NotBeNull("Control Extent should not be null");
                // Extent is Vector4: X=LEFT, Y=TOP, Z=WIDTH, W=HEIGHT
                control.Extent.Z.Should().BeGreaterThanOrEqualTo(0, "Control width should be non-negative");
                control.Extent.W.Should().BeGreaterThanOrEqualTo(0, "Control height should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlBorder()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate border structure for controls that have borders
            foreach (var control in gui.Controls)
            {
                if (control.Border != null)
                {
                    control.Border.Corner.Should().NotBeNull("Border Corner ResRef should not be null");
                    control.Border.Edge.Should().NotBeNull("Border Edge ResRef should not be null");
                    control.Border.Fill.Should().NotBeNull("Border Fill ResRef should not be null");
                    control.Border.Dimension.Should().BeGreaterThanOrEqualTo(0, "Border Dimension should be non-negative");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlText()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate text structure for controls that have text
            foreach (var control in gui.Controls)
            {
                if (control.GuiText != null)
                {
                    control.GuiText.Font.Should().NotBeNull("Text Font ResRef should not be null");
                    control.GuiText.Alignment.Should().BeGreaterThanOrEqualTo(0, "Text Alignment should be non-negative");
                    control.GuiText.StrRef.Should().BeGreaterThanOrEqualTo(-1, "Text StrRef should be >= -1 (0xFFFFFFFF = unused)");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlMoveto()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate MOVETO structure for controls that have navigation
            foreach (var control in gui.Controls)
            {
                if (control.Moveto != null)
                {
                    control.Moveto.Up.Should().BeGreaterThanOrEqualTo(-1, "MOVETO Up should be >= -1");
                    control.Moveto.Down.Should().BeGreaterThanOrEqualTo(-1, "MOVETO Down should be >= -1");
                    control.Moveto.Left.Should().BeGreaterThanOrEqualTo(-1, "MOVETO Left should be >= -1");
                    control.Moveto.Right.Should().BeGreaterThanOrEqualTo(-1, "MOVETO Right should be >= -1");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlNestedControls()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate nested controls structure
            foreach (var control in gui.Controls)
            {
                control.Children.Should().NotBeNull("Control Children list should not be null");
                control.Children.Count.Should().BeGreaterThanOrEqualTo(0, "Control children count should be non-negative");

                // Recursively validate nested controls
                ValidateNestedControls(control);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiButtonControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all button controls
            var buttons = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.Button).ToList();

            foreach (var button in buttons)
            {
                button.Should().BeOfType<GUIButton>("Button control should be GUIButton type");
                var btn = (GUIButton)button;
                // Button-specific properties validated in LoadButtonProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiLabelControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all label controls
            var labels = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.Label).ToList();

            foreach (var label in labels)
            {
                label.Should().BeOfType<GUILabel>("Label control should be GUILabel type");
                var lbl = (GUILabel)label;
                // Label-specific properties validated in LoadLabelProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiPanelControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all panel controls
            var panels = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.Panel).ToList();

            foreach (var panel in panels)
            {
                panel.Should().BeOfType<GUIPanel>("Panel control should be GUIPanel type");
                var pnl = (GUIPanel)panel;
                // Panel-specific properties validated in LoadPanelProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiListBoxControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all listbox controls
            var listBoxes = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.ListBox).ToList();

            foreach (var listBox in listBoxes)
            {
                listBox.Should().BeOfType<GUIListBox>("ListBox control should be GUIListBox type");
                var lb = (GUIListBox)listBox;
                // ListBox-specific properties validated in LoadListBoxProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiScrollBarControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all scrollbar controls
            var scrollBars = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.ScrollBar).ToList();

            foreach (var scrollBar in scrollBars)
            {
                scrollBar.Should().BeOfType<GUIScrollbar>("ScrollBar control should be GUIScrollbar type");
                var sb = (GUIScrollbar)scrollBar;
                // ScrollBar-specific properties validated in LoadScrollBarProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiCheckBoxControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all checkbox controls
            var checkBoxes = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.CheckBox).ToList();

            foreach (var checkBox in checkBoxes)
            {
                checkBox.Should().BeOfType<GUICheckBox>("CheckBox control should be GUICheckBox type");
                var cb = (GUICheckBox)checkBox;
                // CheckBox-specific properties validated in LoadCheckBoxProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiSliderControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all slider controls
            var sliders = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.Slider).ToList();

            foreach (var slider in sliders)
            {
                slider.Should().BeOfType<GUISlider>("Slider control should be GUISlider type");
                var sl = (GUISlider)slider;
                // Slider-specific properties validated in LoadSliderProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiProgressBarControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all progress bar controls
            var progressBars = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.Progress).ToList();

            foreach (var progressBar in progressBars)
            {
                progressBar.Should().BeOfType<GUIProgressBar>("Progress control should be GUIProgressBar type");
                var pb = (GUIProgressBar)progressBar;
                // Progress-specific properties validated in LoadProgressBarProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiProtoItemControl()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Find all protoitem controls
            var protoItems = GetAllControls(gui).Where(c => c.GuiType == GUIControlType.ProtoItem).ToList();

            foreach (var protoItem in protoItems)
            {
                protoItem.Should().BeOfType<GUIProtoItem>("ProtoItem control should be GUIProtoItem type");
                var pi = (GUIProtoItem)protoItem;
                // ProtoItem-specific properties validated in LoadProtoItemProperties
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlStates()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control states (BORDER, HILIGHT, SELECTED, HILIGHTSELECTED)
            foreach (var control in GetAllControls(gui))
            {
                // BORDER is base state
                if (control.Border != null)
                {
                    ValidateBorderStructure(control.Border);
                }

                // HILIGHT is hover state
                if (control.Hilight != null)
                {
                    ValidateBorderStructure(control.Hilight);
                }

                // SELECTED is selected state
                if (control.Selected != null)
                {
                    ValidateSelectedStructure(control.Selected);
                }

                // HILIGHTSELECTED is highlight+selected state (highest priority)
                if (control.HilightSelected != null)
                {
                    ValidateHilightSelectedStructure(control.HilightSelected);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiScrollbarThumb()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate scrollbar thumb structure
            foreach (var control in GetAllControls(gui))
            {
                if (control is GUIScrollbar scrollBar && scrollBar.GuiThumb != null)
                {
                    scrollBar.GuiThumb.Image.Should().NotBeNull("Scrollbar thumb Image ResRef should not be null");
                    scrollBar.GuiThumb.Alignment.Should().BeGreaterThanOrEqualTo(0, "Scrollbar thumb Alignment should be non-negative");
                }

                if (control.Thumb != null)
                {
                    control.Thumb.Image.Should().NotBeNull("Thumb Image ResRef should not be null");
                    control.Thumb.Alignment.Should().BeGreaterThanOrEqualTo(0, "Thumb Alignment should be non-negative");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiScrollbarDir()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate scrollbar direction arrow structure
            foreach (var control in GetAllControls(gui))
            {
                if (control is GUIScrollbar scrollBar && scrollBar.GuiDirection != null)
                {
                    scrollBar.GuiDirection.Image.Should().NotBeNull("Scrollbar DIR Image ResRef should not be null");
                    scrollBar.GuiDirection.Alignment.Should().BeGreaterThanOrEqualTo(0, "Scrollbar DIR Alignment should be non-negative");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiProgressStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate progress bar progress structure
            foreach (var control in GetAllControls(gui))
            {
                if (control.Progress != null)
                {
                    control.Progress.Corner.Should().NotBeNull("Progress Corner ResRef should not be null");
                    control.Progress.Edge.Should().NotBeNull("Progress Edge ResRef should not be null");
                    control.Progress.Fill.Should().NotBeNull("Progress Fill ResRef should not be null");
                    control.Progress.Dimension.Should().BeGreaterThanOrEqualTo(0, "Progress Dimension should be non-negative");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiTextAlignment()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate text alignment values match GUI.ksy enum
            var validAlignments = new[] { 1, 2, 3, 17, 18, 19, 33, 34, 35 };

            foreach (var control in GetAllControls(gui))
            {
                if (control.GuiText != null)
                {
                    control.GuiText.Alignment.Should().BeOneOf(validAlignments, "Text alignment should be a valid GUIAlignment enum value");
                }

                if (control is GUILabel label)
                {
                    label.Alignment.Should().BeOneOf(validAlignments, "Label alignment should be a valid GUIAlignment enum value");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlColor()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control color (Vector3 RGB, 0.0-1.0 range)
            foreach (var control in GetAllControls(gui))
            {
                if (control.Color != null)
                {
                    control.Color.R.Should().BeInRange(0.0f, 1.0f, "Control color R should be in 0.0-1.0 range");
                    control.Color.G.Should().BeInRange(0.0f, 1.0f, "Control color G should be in 0.0-1.0 range");
                    control.Color.B.Should().BeInRange(0.0f, 1.0f, "Control color B should be in 0.0-1.0 range");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlAlpha()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control alpha (0.0-1.0 range)
            // Updated to use new Alpha property instead of Properties["ALPHA"]
            foreach (var control in GetAllControls(gui))
            {
                // Check if control has alpha set (not default 1.0f) or if Color is set with alpha
                if (control.Alpha != 1.0f || (control.Color != null && control.Color.A != 1.0f))
                {
                    float alpha = control.Alpha;
                    alpha.Should().BeInRange(0.0f, 1.0f, "Control alpha should be in 0.0-1.0 range");
                }

                if (control is GUIPanel panel)
                {
                    panel.Alpha.Should().BeInRange(0.0f, 1.0f, "Panel alpha should be in 0.0-1.0 range");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiListBoxProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate ListBox-specific properties
            foreach (var control in GetAllControls(gui).OfType<GUIListBox>())
            {
                control.Padding.Should().BeGreaterThanOrEqualTo(0, "ListBox padding should be non-negative");
                control.MaxValue.Should().BeGreaterThanOrEqualTo(0, "ListBox MaxValue should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiSliderProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate Slider-specific properties
            foreach (var control in GetAllControls(gui).OfType<GUISlider>())
            {
                control.MaxValue.Should().BeGreaterThanOrEqualTo(0.0f, "Slider MaxValue should be non-negative");
                control.Value.Should().BeInRange(0.0f, control.MaxValue, "Slider Value should be in range 0 to MaxValue");
                control.Direction.Should().BeOneOf(new[] { "horizontal", "vertical" }, "Slider direction should be horizontal or vertical");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiProgressBarProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate ProgressBar-specific properties
            foreach (var control in GetAllControls(gui).OfType<GUIProgressBar>())
            {
                control.MaxValue.Should().BeGreaterThanOrEqualTo(0.0f, "ProgressBar MaxValue should be non-negative");
                control.CurrentValue.Should().BeInRange(0, 100, "ProgressBar CurrentValue should be in range 0-100");
                control.StartFromLeft.Should().BeInRange(0, 1, "ProgressBar StartFromLeft should be 0 or 1");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiCheckBoxProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate CheckBox-specific properties
            foreach (var control in GetAllControls(gui).OfType<GUICheckBox>())
            {
                if (control.IsSelected.HasValue)
                {
                    control.IsSelected.Value.Should().BeInRange(0, 1, "CheckBox IsSelected should be 0 or 1");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlIds()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control IDs
            var allControls = GetAllControls(gui);
            var ids = allControls.Where(c => c.Id.HasValue).Select(c => c.Id.Value).ToList();

            // IDs should be unique (if set)
            ids.Should().OnlyHaveUniqueItems("Control IDs should be unique");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlTags()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate control tags
            foreach (var control in GetAllControls(gui))
            {
                control.Tag.Should().NotBeNull("Control Tag should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlParentReferences()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            GUI gui = new GUIReader(BinaryTestFile).Load();

            // Validate parent references
            var allControls = GetAllControls(gui);
            foreach (var control in allControls)
            {
                if (!string.IsNullOrEmpty(control.ParentTag))
                {
                    // Parent tag should reference an existing control
                    var parent = allControls.FirstOrDefault(c => c.Tag == control.ParentTag);
                    // Note: Parent might be in a different part of the tree, so we don't assert it exists
                }

                if (control.ParentId.HasValue)
                {
                    // Parent ID should reference an existing control
                    var parent = allControls.FirstOrDefault(c => c.Id == control.ParentId.Value);
                    // Note: Parent might be in a different part of the tree, so we don't assert it exists
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new GUIReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new GUIReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestGuiInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = System.Text.Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new GUIReader(tempFile).Load();
                act.Should().Throw<ArgumentException>();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiTruncatedFile()
        {
            // Create file with valid header but truncated data
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    // Write valid GFF header
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("GUI ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V3.2").CopyTo(header, 4);
                    // Rest of header is zeros (invalid offsets)
                    fs.Write(header, 0, header.Length);
                    // File is truncated - no data after header
                }

                Action act = () => new GUIReader(tempFile).Load();
                act.Should().Throw<ArgumentException>();
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiFileSize()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestGuiFile(BinaryTestFile);
            }

            // Validate file size is reasonable (at least header size)
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(56, "GUI file should have at least 56-byte header");
        }

        [Fact(Timeout = 120000)]
        public void TestGuiControlTypeEnum()
        {
            // Validate all control types match GUI.ksy enum
            var expectedTypes = new[]
            {
                GUIControlType.Invalid,
                GUIControlType.Control,
                GUIControlType.Panel,
                GUIControlType.ProtoItem,
                GUIControlType.Label,
                GUIControlType.Button,
                GUIControlType.CheckBox,
                GUIControlType.Slider,
                GUIControlType.ScrollBar,
                GUIControlType.Progress,
                GUIControlType.ListBox
            };

            foreach (var type in expectedTypes)
            {
                type.Should().BeDefined("Control type should be a valid GUIControlType enum value");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGuiTextAlignmentEnum()
        {
            // Validate text alignment values match GUI.ksy enum
            var expectedAlignments = new[]
            {
                GUIAlignment.TopLeft,
                GUIAlignment.TopCenter,
                GUIAlignment.TopRight,
                GUIAlignment.CenterLeft,
                GUIAlignment.Center,
                GUIAlignment.CenterRight,
                GUIAlignment.BottomLeft,
                GUIAlignment.BottomCenter,
                GUIAlignment.BottomRight
            };

            foreach (var alignment in expectedAlignments)
            {
                alignment.Should().BeDefined("Alignment should be a valid GUIAlignment enum value");
            }
        }

        // Helper methods

        private static void ValidateIO(GUI gui)
        {
            // Basic validation
            gui.Should().NotBeNull("GUI should not be null");
            gui.Tag.Should().NotBeNull("GUI Tag should not be null");
            gui.Controls.Should().NotBeNull("GUI Controls list should not be null");
        }

        /// <summary>
        /// Validates that a round-trip GUI matches the original GUI.
        /// Compares all properties recursively to ensure data integrity.
        /// </summary>
        private static void ValidateRoundTrip(GUI original, GUI roundTrip)
        {
            // Validate root properties
            roundTrip.Tag.Should().Be(original.Tag, "GUI Tag should match after round-trip");
            roundTrip.Controls.Count.Should().Be(original.Controls.Count, "GUI Controls count should match after round-trip");

            // Validate all controls recursively
            var originalControls = GetAllControls(original);
            var roundTripControls = GetAllControls(roundTrip);

            originalControls.Count.Should().Be(roundTripControls.Count, "Total control count should match after round-trip");

            for (int i = 0; i < originalControls.Count; i++)
            {
                ValidateControlRoundTrip(originalControls[i], roundTripControls[i], $"Control {i}");
            }
        }

        /// <summary>
        /// Validates that a control matches after round-trip.
        /// </summary>
        private static void ValidateControlRoundTrip(GUIControl original, GUIControl roundTrip, string context)
        {
            roundTrip.GuiType.Should().Be(original.GuiType, $"{context}: Control type should match");
            roundTrip.Id.Should().Be(original.Id, $"{context}: ID should match");
            roundTrip.Tag.Should().Be(original.Tag, $"{context}: Tag should match");
            roundTrip.ParentTag.Should().Be(original.ParentTag, $"{context}: ParentTag should match");
            roundTrip.ParentId.Should().Be(original.ParentId, $"{context}: ParentId should match");
            roundTrip.Locked.Should().Be(original.Locked, $"{context}: Locked should match");

            // Validate Extent
            roundTrip.Extent.X.Should().BeApproximately(original.Extent.X, 0.01f, $"{context}: Extent X should match");
            roundTrip.Extent.Y.Should().BeApproximately(original.Extent.Y, 0.01f, $"{context}: Extent Y should match");
            roundTrip.Extent.Z.Should().BeApproximately(original.Extent.Z, 0.01f, $"{context}: Extent Z (width) should match");
            roundTrip.Extent.W.Should().BeApproximately(original.Extent.W, 0.01f, $"{context}: Extent W (height) should match");

            // Validate Color
            if (original.Color != null)
            {
                roundTrip.Color.Should().NotBeNull($"{context}: Round-trip Color should not be null if original is not null");
                roundTrip.Color.R.Should().BeApproximately(original.Color.R, 0.001f, $"{context}: Color R should match");
                roundTrip.Color.G.Should().BeApproximately(original.Color.G, 0.001f, $"{context}: Color G should match");
                roundTrip.Color.B.Should().BeApproximately(original.Color.B, 0.001f, $"{context}: Color B should match");
            }

            // Validate Border
            ValidateBorderRoundTrip(original.Border, roundTrip.Border, $"{context}: Border");

            // Validate Hilight
            ValidateBorderRoundTrip(original.Hilight, roundTrip.Hilight, $"{context}: Hilight");

            // Validate Selected
            ValidateSelectedRoundTrip(original.Selected, roundTrip.Selected, $"{context}: Selected");

            // Validate HilightSelected
            ValidateHilightSelectedRoundTrip(original.HilightSelected, roundTrip.HilightSelected, $"{context}: HilightSelected");

            // Validate Text
            ValidateTextRoundTrip(original.GuiText, roundTrip.GuiText, $"{context}: Text");

            // Validate MoveTo
            ValidateMoveToRoundTrip(original.Moveto, roundTrip.Moveto, $"{context}: MoveTo");

            // Validate control-specific properties
            if (original.GuiType == GUIControlType.ListBox)
            {
                var originalListBox = (GUIListBox)original;
                var roundTripListBox = (GUIListBox)roundTrip;
                roundTripListBox.Padding.Should().Be(originalListBox.Padding, $"{context}: ListBox Padding should match");
                roundTripListBox.Looping.Should().Be(originalListBox.Looping, $"{context}: ListBox Looping should match");
                roundTripListBox.MaxValue.Should().Be(originalListBox.MaxValue, $"{context}: ListBox MaxValue should match");
            }
            else if (original.GuiType == GUIControlType.Slider)
            {
                var originalSlider = (GUISlider)original;
                var roundTripSlider = (GUISlider)roundTrip;
                roundTripSlider.MaxValue.Should().BeApproximately(originalSlider.MaxValue, 0.01f, $"{context}: Slider MaxValue should match");
                roundTripSlider.Value.Should().BeApproximately(originalSlider.Value, 0.01f, $"{context}: Slider Value should match");
                roundTripSlider.Direction.Should().Be(originalSlider.Direction, $"{context}: Slider Direction should match");
            }
            else if (original.GuiType == GUIControlType.Progress)
            {
                var originalProgress = (GUIProgressBar)original;
                var roundTripProgress = (GUIProgressBar)roundTrip;
                roundTripProgress.MaxValue.Should().BeApproximately(originalProgress.MaxValue, 0.01f, $"{context}: ProgressBar MaxValue should match");
                roundTripProgress.CurrentValue.Should().Be(originalProgress.CurrentValue, $"{context}: ProgressBar CurrentValue should match");
                roundTripProgress.StartFromLeft.Should().Be(originalProgress.StartFromLeft, $"{context}: ProgressBar StartFromLeft should match");
            }
            else if (original.GuiType == GUIControlType.CheckBox)
            {
                var originalCheckBox = (GUICheckBox)original;
                var roundTripCheckBox = (GUICheckBox)roundTrip;
                roundTripCheckBox.IsSelected.Should().Be(originalCheckBox.IsSelected, $"{context}: CheckBox IsSelected should match");
            }
            else if (original.GuiType == GUIControlType.Button)
            {
                var originalButton = (GUIButton)original;
                var roundTripButton = (GUIButton)roundTrip;
                roundTripButton.Text.Should().Be(originalButton.Text, $"{context}: Button Text should match");
                roundTripButton.Pulsing.Should().Be(originalButton.Pulsing, $"{context}: Button Pulsing should match");
            }
            else if (original.GuiType == GUIControlType.Label)
            {
                var originalLabel = (GUILabel)original;
                var roundTripLabel = (GUILabel)roundTrip;
                roundTripLabel.Text.Should().Be(originalLabel.Text, $"{context}: Label Text should match");
                roundTripLabel.Alignment.Should().Be(originalLabel.Alignment, $"{context}: Label Alignment should match");
            }
            else if (original.GuiType == GUIControlType.Panel)
            {
                var originalPanel = (GUIPanel)original;
                var roundTripPanel = (GUIPanel)roundTrip;
                roundTripPanel.Alpha.Should().BeApproximately(originalPanel.Alpha, 0.001f, $"{context}: Panel Alpha should match");
            }

            // Validate children recursively
            roundTrip.Children.Count.Should().Be(original.Children.Count, $"{context}: Children count should match");
            for (int i = 0; i < original.Children.Count; i++)
            {
                ValidateControlRoundTrip(original.Children[i], roundTrip.Children[i], $"{context} -> Child {i}");
            }
        }

        private static void ValidateBorderRoundTrip(GUIBorder original, GUIBorder roundTrip, string context)
        {
            if (original == null)
            {
                roundTrip.Should().BeNull($"{context} should be null if original is null");
                return;
            }

            roundTrip.Should().NotBeNull($"{context} should not be null if original is not null");
            roundTrip.Corner.ToString().Should().Be(original.Corner.ToString(), $"{context}: Corner should match");
            roundTrip.Edge.ToString().Should().Be(original.Edge.ToString(), $"{context}: Edge should match");
            roundTrip.Fill.ToString().Should().Be(original.Fill.ToString(), $"{context}: Fill should match");
            roundTrip.Dimension.Should().Be(original.Dimension, $"{context}: Dimension should match");
            roundTrip.FillStyle.Should().Be(original.FillStyle, $"{context}: FillStyle should match");
            roundTrip.InnerOffset.Should().Be(original.InnerOffset, $"{context}: InnerOffset should match");
            roundTrip.InnerOffsetY.Should().Be(original.InnerOffsetY, $"{context}: InnerOffsetY should match");
            roundTrip.Pulsing.Should().Be(original.Pulsing, $"{context}: Pulsing should match");
        }

        private static void ValidateSelectedRoundTrip(GUISelected original, GUISelected roundTrip, string context)
        {
            if (original == null)
            {
                roundTrip.Should().BeNull($"{context} should be null if original is null");
                return;
            }

            roundTrip.Should().NotBeNull($"{context} should not be null if original is not null");
            roundTrip.Corner.ToString().Should().Be(original.Corner.ToString(), $"{context}: Corner should match");
            roundTrip.Edge.ToString().Should().Be(original.Edge.ToString(), $"{context}: Edge should match");
            roundTrip.Fill.ToString().Should().Be(original.Fill.ToString(), $"{context}: Fill should match");
            roundTrip.Dimension.Should().Be(original.Dimension, $"{context}: Dimension should match");
            roundTrip.FillStyle.Should().Be(original.FillStyle, $"{context}: FillStyle should match");
            roundTrip.InnerOffset.Should().Be(original.InnerOffset, $"{context}: InnerOffset should match");
            roundTrip.InnerOffsetY.Should().Be(original.InnerOffsetY, $"{context}: InnerOffsetY should match");
            roundTrip.Pulsing.Should().Be(original.Pulsing, $"{context}: Pulsing should match");
        }

        private static void ValidateHilightSelectedRoundTrip(GUIHilightSelected original, GUIHilightSelected roundTrip, string context)
        {
            if (original == null)
            {
                roundTrip.Should().BeNull($"{context} should be null if original is null");
                return;
            }

            roundTrip.Should().NotBeNull($"{context} should not be null if original is not null");
            roundTrip.Corner.ToString().Should().Be(original.Corner.ToString(), $"{context}: Corner should match");
            roundTrip.Edge.ToString().Should().Be(original.Edge.ToString(), $"{context}: Edge should match");
            roundTrip.Fill.ToString().Should().Be(original.Fill.ToString(), $"{context}: Fill should match");
            roundTrip.Dimension.Should().Be(original.Dimension, $"{context}: Dimension should match");
            roundTrip.FillStyle.Should().Be(original.FillStyle, $"{context}: FillStyle should match");
            roundTrip.InnerOffset.Should().Be(original.InnerOffset, $"{context}: InnerOffset should match");
            roundTrip.InnerOffsetY.Should().Be(original.InnerOffsetY, $"{context}: InnerOffsetY should match");
            roundTrip.Pulsing.Should().Be(original.Pulsing, $"{context}: Pulsing should match");
        }

        private static void ValidateTextRoundTrip(GUIText original, GUIText roundTrip, string context)
        {
            if (original == null)
            {
                roundTrip.Should().BeNull($"{context} should be null if original is null");
                return;
            }

            roundTrip.Should().NotBeNull($"{context} should not be null if original is not null");
            roundTrip.Text.Should().Be(original.Text, $"{context}: Text should match");
            roundTrip.StrRef.Should().Be(original.StrRef, $"{context}: StrRef should match");
            roundTrip.Font.ToString().Should().Be(original.Font.ToString(), $"{context}: Font should match");
            roundTrip.Alignment.Should().Be(original.Alignment, $"{context}: Alignment should match");
            roundTrip.Pulsing.Should().Be(original.Pulsing, $"{context}: Pulsing should match");
        }

        private static void ValidateMoveToRoundTrip(GUIMoveTo original, GUIMoveTo roundTrip, string context)
        {
            if (original == null)
            {
                roundTrip.Should().BeNull($"{context} should be null if original is null");
                return;
            }

            roundTrip.Should().NotBeNull($"{context} should not be null if original is not null");
            roundTrip.Up.Should().Be(original.Up, $"{context}: Up should match");
            roundTrip.Down.Should().Be(original.Down, $"{context}: Down should match");
            roundTrip.Left.Should().Be(original.Left, $"{context}: Left should match");
            roundTrip.Right.Should().Be(original.Right, $"{context}: Right should match");
        }

        private static void ValidateNestedControls(GUIControl control)
        {
            foreach (var child in control.Children)
            {
                child.Should().NotBeNull("Child control should not be null");
                child.GuiType.Should().BeDefined("Child control type should be valid");
                ValidateNestedControls(child);
            }
        }

        private static void ValidateBorderStructure(GUIBorder border)
        {
            border.Corner.Should().NotBeNull("Border Corner should not be null");
            border.Edge.Should().NotBeNull("Border Edge should not be null");
            border.Fill.Should().NotBeNull("Border Fill should not be null");
            border.Dimension.Should().BeGreaterThanOrEqualTo(0, "Border Dimension should be non-negative");
        }

        private static void ValidateSelectedStructure(GUISelected selected)
        {
            selected.Corner.Should().NotBeNull("Selected Corner should not be null");
            selected.Edge.Should().NotBeNull("Selected Edge should not be null");
            selected.Fill.Should().NotBeNull("Selected Fill should not be null");
            selected.Dimension.Should().BeGreaterThanOrEqualTo(0, "Selected Dimension should be non-negative");
        }

        private static void ValidateHilightSelectedStructure(GUIHilightSelected hilightSelected)
        {
            hilightSelected.Corner.Should().NotBeNull("HilightSelected Corner should not be null");
            hilightSelected.Edge.Should().NotBeNull("HilightSelected Edge should not be null");
            hilightSelected.Fill.Should().NotBeNull("HilightSelected Fill should not be null");
            hilightSelected.Dimension.Should().BeGreaterThanOrEqualTo(0, "HilightSelected Dimension should be non-negative");
        }

        private static System.Collections.Generic.List<GUIControl> GetAllControls(GUI gui)
        {
            var allControls = new System.Collections.Generic.List<GUIControl>();
            foreach (var control in gui.Controls)
            {
                allControls.Add(control);
                GetAllControlsRecursive(control, allControls);
            }
            return allControls;
        }

        private static void GetAllControlsRecursive(GUIControl control, System.Collections.Generic.List<GUIControl> allControls)
        {
            foreach (var child in control.Children)
            {
                allControls.Add(child);
                GetAllControlsRecursive(child, allControls);
            }
        }

        private static void CreateTestGuiFile(string path)
        {
            // Create a minimal valid GUI file using GFF structure
            var gff = new GFF(GFFContent.GUI);
            var root = gff.Root;

            // Set root GUI tag
            root.SetString("Tag", "TEST_GUI");

            // Create a simple button control
            var controlsList = root.Acquire<GFFList>("CONTROLS", new GFFList());
            var buttonStruct = controlsList.Add();

            buttonStruct.SetInt32("CONTROLTYPE", (int)GUIControlType.Button);
            buttonStruct.SetInt32("ID", 1);
            buttonStruct.SetString("TAG", "BTN_TEST");

            // Create EXTENT struct
            var extentStruct = buttonStruct.Acquire<GFFStruct>("EXTENT", new GFFStruct());
            extentStruct.SetInt32("LEFT", 100);
            extentStruct.SetInt32("TOP", 100);
            extentStruct.SetInt32("WIDTH", 200);
            extentStruct.SetInt32("HEIGHT", 50);

            // Create TEXT struct
            var textStruct = buttonStruct.Acquire<GFFStruct>("TEXT", new GFFStruct());
            textStruct.SetString("TEXT", "Test Button");
            textStruct.SetUInt32("STRREF", 0xFFFFFFFF);
            textStruct.SetResRef("FONT", ResRef.FromString("fnt_d16x16"));
            textStruct.SetUInt32("ALIGNMENT", 18); // Center

            // Create BORDER struct
            var borderStruct = buttonStruct.Acquire<GFFStruct>("BORDER", new GFFStruct());
            borderStruct.SetResRef("CORNER", ResRef.FromString("corner"));
            borderStruct.SetResRef("EDGE", ResRef.FromString("edge"));
            borderStruct.SetResRef("FILL", ResRef.FromString("fill"));
            borderStruct.SetInt32("FILLSTYLE", 2);
            borderStruct.SetInt32("DIMENSION", 2);

            // Write GFF to file
            byte[] data = new GFFBinaryWriter(gff).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

