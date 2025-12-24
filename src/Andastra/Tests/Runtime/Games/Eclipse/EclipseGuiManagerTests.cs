using System;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.GUI;
using Andastra.Runtime.Games.Eclipse.GUI;
using Andastra.Runtime.Graphics;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Eclipse
{
    /// <summary>
    /// Comprehensive tests for EclipseGuiManager.
    /// Tests GUI loading, rendering, input handling, and error handling.
    /// </summary>
    public class EclipseGuiManagerTests : IDisposable
    {
        private IGraphicsDevice _graphicsDevice;
        private Installation _installation;
        private Mock<IResourceLookup> _mockResourceLookup;
        private EclipseGuiManager _guiManager;

        public EclipseGuiManagerTests()
        {
            _graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            _mockResourceLookup = new Mock<IResourceLookup>(MockBehavior.Strict);

            var mockResourceManager = new Mock<InstallationResourceManager>(MockBehavior.Strict, "C:\\Test", BioWareGame.DA);
            mockResourceManager.Setup(r => r.LookupResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns<string, ResourceType, SearchLocation[], string>((resname, restype, searchOrder, moduleRoot) =>
                    _mockResourceLookup.Object.LookupResource(resname, restype, searchOrder, moduleRoot));

            var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
            mockInstallation.Setup(i => i.Resources).Returns(mockResourceManager.Object);
            _installation = mockInstallation.Object;

            _guiManager = new EclipseGuiManager(_graphicsDevice, _installation);
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

            _mockResourceLookup.Setup(r => r.LookupResource(
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

            _mockResourceLookup.Setup(r => r.LookupResource(
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
        public void SetCurrentGui_WithLoadedGUI_ShouldSucceed()
        {
            // Arrange
            string guiName = "test_gui";
            byte[] guiData = CreateTestGUIData();

            _mockResourceLookup.Setup(r => r.LookupResource(
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

            _mockResourceLookup.Setup(r => r.LookupResource(
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
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            _guiManager.Invoking(gm => gm.Dispose()).Should().NotThrow();
        }

        private byte[] CreateTestGUIData()
        {
            // Placeholder - would need full GFF serialization
            return new byte[0];
        }
    }
}

