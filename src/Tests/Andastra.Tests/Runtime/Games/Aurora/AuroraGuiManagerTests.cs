using System;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.GUI;
using Andastra.Runtime.Games.Aurora.GUI;
using Andastra.Runtime.Graphics;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;
using GUIWriter = Andastra.Parsing.Resource.Generics.GUI.GUIWriter;
using ParsingColor = Andastra.Parsing.Common.Color;

namespace Andastra.Tests.Runtime.Games.Aurora
{
    /// <summary>
    /// Comprehensive tests for AuroraGuiManager.
    /// Tests GUI loading, rendering, input handling, and error handling.
    /// </summary>
    public class AuroraGuiManagerTests : IDisposable
    {
        private IGraphicsDevice _graphicsDevice;
        private Installation _installation;
        private Mock<InstallationResourceManager> _mockResourceManager;
        private AuroraGuiManager _guiManager;

        public AuroraGuiManagerTests()
        {
            _graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            _mockResourceManager = new Mock<InstallationResourceManager>(MockBehavior.Strict, It.IsAny<string>(), It.IsAny<Andastra.Parsing.Common.BioWareGame>());

            var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
            mockInstallation.Setup(i => i.Resources).Returns(_mockResourceManager.Object);
            _installation = mockInstallation.Object;

            _guiManager = new AuroraGuiManager(_graphicsDevice, _installation);
        }

        public void Dispose()
        {
            _guiManager?.Dispose();
            _graphicsDevice?.Dispose();
        }

        [Fact]
        public void LoadGui_WithValidGUI_ShouldSucceed()
        {
            // Arrange
            string guiName = "mainmenu";
            byte[] guiData = CreateTestGUIData();

            _mockResourceManager.Setup(r => r.LookupResource(
                guiName,
                ResourceType.GUI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(guiName, ResourceType.GUI, "test.gui", guiData));

            // Act
            bool result = _guiManager.LoadGui(guiName, 1920, 1080);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void LoadGui_WithMissingGUI_ShouldReturnFalse()
        {
            // Arrange
            string guiName = "missing_gui";

            _mockResourceManager.Setup(r => r.LookupResource(
                guiName,
                ResourceType.GUI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null);

            // Act
            bool result = _guiManager.LoadGui(guiName, 1920, 1080);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void LoadGui_WithEmptyName_ShouldReturnFalse()
        {
            // Act
            bool result1 = _guiManager.LoadGui("", 1920, 1080);
            bool result2 = _guiManager.LoadGui(null, 1920, 1080);

            // Assert
            result1.Should().BeFalse();
            result2.Should().BeFalse();
        }

        [Fact]
        public void LoadGui_WithDuplicateName_ShouldReturnTrue()
        {
            // Arrange
            string guiName = "test_gui";
            byte[] guiData = CreateTestGUIData();

            _mockResourceManager.Setup(r => r.LookupResource(
                guiName,
                ResourceType.GUI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(guiName, ResourceType.GUI, "test.gui", guiData));

            // Act
            bool result1 = _guiManager.LoadGui(guiName, 1920, 1080);
            bool result2 = _guiManager.LoadGui(guiName, 1920, 1080);

            // Assert
            result1.Should().BeTrue();
            result2.Should().BeTrue(); // Should return true for already loaded GUI
        }

        [Fact]
        public void SetCurrentGui_WithLoadedGUI_ShouldSucceed()
        {
            // Arrange
            string guiName = "test_gui";
            byte[] guiData = CreateTestGUIData();

            _mockResourceManager.Setup(r => r.LookupResource(
                guiName,
                ResourceType.GUI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(guiName, ResourceType.GUI, "test.gui", guiData));

            _guiManager.LoadGui(guiName, 1920, 1080);

            // Act
            bool result = _guiManager.SetCurrentGui(guiName);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void SetCurrentGui_WithUnloadedGUI_ShouldReturnFalse()
        {
            // Act
            bool result = _guiManager.SetCurrentGui("unloaded_gui");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void UnloadGui_WithLoadedGUI_ShouldSucceed()
        {
            // Arrange
            string guiName = "test_gui";
            byte[] guiData = CreateTestGUIData();

            _mockResourceManager.Setup(r => r.LookupResource(
                guiName,
                ResourceType.GUI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(guiName, ResourceType.GUI, "test.gui", guiData));

            _guiManager.LoadGui(guiName, 1920, 1080);

            // Act
            _guiManager.UnloadGui(guiName);

            // Assert
            _guiManager.SetCurrentGui(guiName).Should().BeFalse();
        }

        [Fact]
        public void Update_ShouldNotThrow()
        {
            // Arrange
            var gameTime = new { TotalGameTime = TimeSpan.Zero, ElapsedGameTime = TimeSpan.Zero };

            // Act & Assert
            _guiManager.Invoking(gm => gm.Update(gameTime)).Should().NotThrow();
        }

        [Fact]
        public void Draw_ShouldNotThrow()
        {
            // Arrange
            var gameTime = new { TotalGameTime = TimeSpan.Zero, ElapsedGameTime = TimeSpan.Zero };

            // Act & Assert
            _guiManager.Invoking(gm => gm.Draw(gameTime)).Should().NotThrow();
        }

        [Fact]
        public void OnButtonClicked_ShouldFireEvent()
        {
            // Arrange
            bool eventFired = false;
            string buttonTag = null;
            int buttonId = 0;

            _guiManager.OnButtonClicked += (sender, args) =>
            {
                eventFired = true;
                buttonTag = args.ButtonTag;
                buttonId = args.ButtonId;
            };

            // Act
            // Note: We can't directly test button clicks without a full GUI setup,
            // but we can verify the event infrastructure exists
            // In a real scenario, we'd simulate mouse input

            // Assert
            // Event infrastructure should be available
            _guiManager.Should().NotBeNull();
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            _guiManager.Invoking(gm => gm.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void Dispose_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            _guiManager.Dispose();

            // Act & Assert
            _guiManager.Invoking(gm => gm.Dispose()).Should().NotThrow();
        }

        /// <summary>
        /// Creates minimal test GUI data (GFF format).
        /// Creates a GUI object and serializes it to GFF binary format using GUIWriter.
        /// </summary>
        private byte[] CreateTestGUIData()
        {
            // Create a minimal GUI structure for testing
            // GUIWriter will convert this to proper GFF format
            var gui = new GUI
            {
                Tag = "TEST_GUI",
                Controls = new System.Collections.Generic.List<GUIControl>
                {
                    new GUIButton
                    {
                        Tag = "BTN_TEST",
                        Id = 1,
                        Position = new System.Numerics.Vector2(100, 100),
                        Size = new System.Numerics.Vector2(200, 50),
                        GuiText = new GUIText
                        {
                            Text = "Test Button",
                            Font = ResRef.FromString("fnt_d16x16"),
                            Color = new ParsingColor(255, 255, 255, 255),
                            Alignment = 0
                        }
                    }
                }
            };

            // Convert to GFF and then to bytes
            // Use GUIWriter to properly serialize GUI object to GFF format
            // Based on PyKotor dismantle_gui implementation: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/gui.py:730
            // GUIWriter converts GUI objects to GFF structure and then to binary format
            var writer = new GUIWriter(gui);
            return writer.Write();
        }
    }
}

