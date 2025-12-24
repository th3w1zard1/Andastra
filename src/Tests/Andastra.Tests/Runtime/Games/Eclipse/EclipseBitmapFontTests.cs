using System;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TXI;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Games.Eclipse.Fonts;
using Andastra.Runtime.Graphics;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Games.Eclipse
{
    /// <summary>
    /// Comprehensive tests for EclipseBitmapFont.
    /// Tests font loading, character mapping, text measurement, and error handling.
    /// </summary>
    public class EclipseBitmapFontTests : IDisposable
    {
        private IGraphicsDevice _graphicsDevice;
        private Installation _installation;
        private Mock<IResourceLookup> _mockResourceLookup;

        public EclipseBitmapFontTests()
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
        }

        public void Dispose()
        {
            _graphicsDevice?.Dispose();
        }

        [Fact]
        public void Load_WithValidTPCAndTXI_ShouldSucceed()
        {
            // Arrange
            string fontResRef = "fnt_d16x16";
            TPC testTpc = FontTestHelper.CreateTestTPC(256, 256, TPCTextureFormat.RGBA);
            TXI testTxi = FontTestHelper.CreateTestTXI(16.0f, 8.0f, 12.0f, 1.0f, 2.0f, 16, 16, 256.0f);

            byte[] tpcData = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TPC,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(fontResRef, ResourceType.TPC, "test.tpc", tpcData));

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TXI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(fontResRef, ResourceType.TXI, "test.txi", txiData));

            // Act
            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);

            // Assert
            font.Should().NotBeNull();
            font.FontHeight.Should().Be(16.0f);
            font.FontWidth.Should().Be(8.0f);
            font.BaselineHeight.Should().Be(12.0f);
            font.SpacingR.Should().Be(1.0f);
            font.SpacingB.Should().Be(2.0f);
            font.TextureWidth.Should().Be(256);
            font.TextureHeight.Should().Be(256);
            font.Texture.Should().NotBeNull();
        }

        [Fact]
        public void Load_WithTGAFormat_ShouldSucceed()
        {
            // Arrange
            string fontResRef = "fnt_test";
            byte[] tgaData = FontTestHelper.CreateTestTPCData(128, 128);
            TXI testTxi = FontTestHelper.CreateTestTXI(12.0f, 6.0f, 10.0f, 0.5f, 1.0f, 16, 16, 128.0f);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TGA,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(fontResRef, ResourceType.TGA, "test.tga", tgaData));

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TXI,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns(new ResourceResult(fontResRef, ResourceType.TXI, "test.txi", txiData));

            // Act
            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);

            // Assert
            font.Should().NotBeNull();
            font.FontHeight.Should().Be(12.0f);
        }

        [Fact]
        public void Load_WithMissingTPC_ShouldReturnNull()
        {
            // Arrange
            string fontResRef = "fnt_missing";

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TPC,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null);

            _mockResourceLookup.Setup(r => r.LookupResource(
                fontResRef,
                ResourceType.TGA,
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns((ResourceResult)null);

            // Act
            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);

            // Assert
            font.Should().BeNull();
        }

        [Fact]
        public void MeasureString_WithSimpleText_ShouldReturnCorrectSize()
        {
            // Arrange
            string fontResRef = "fnt_test";
            TPC testTpc = FontTestHelper.CreateTestTPC(256, 256);
            TXI testTxi = FontTestHelper.CreateTestTXI(16.0f, 8.0f, 12.0f, 1.0f, 2.0f, 16, 16, 256.0f);

            byte[] tpcData = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns<string, ResourceType, string[], string[]>((resRef, resType, _, __) =>
                {
                    if (resType == ResourceType.TPC)
                        return new ResourceResult(resRef, ResourceType.TPC, "test.tpc", tpcData);
                    if (resType == ResourceType.TXI)
                        return new ResourceResult(resRef, ResourceType.TXI, "test.txi", txiData);
                    return null;
                });

            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
            font.Should().NotBeNull();

            // Act
            var size = font.MeasureString("Hello");

            // Assert
            size.X.Should().BeApproximately(45.0f, 0.1f);
            size.Y.Should().BeApproximately(16.0f, 0.1f);
        }

        [Fact]
        public void MeasureString_WithEmptyString_ShouldReturnZero()
        {
            // Arrange
            string fontResRef = "fnt_test";
            TPC testTpc = FontTestHelper.CreateTestTPC(256, 256);
            TXI testTxi = FontTestHelper.CreateTestTXI(16.0f, 8.0f, 12.0f, 1.0f, 2.0f, 16, 16, 256.0f);

            byte[] tpcData = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns<string, ResourceType, string[], string[]>((resRef, resType, _, __) =>
                {
                    if (resType == ResourceType.TPC)
                        return new ResourceResult(resRef, ResourceType.TPC, "test.tpc", tpcData);
                    if (resType == ResourceType.TXI)
                        return new ResourceResult(resRef, ResourceType.TXI, "test.txi", txiData);
                    return null;
                });

            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
            font.Should().NotBeNull();

            // Act
            var size1 = font.MeasureString("");
            var size2 = font.MeasureString(null);

            // Assert
            size1.X.Should().Be(0.0f);
            size1.Y.Should().Be(0.0f);
            size2.X.Should().Be(0.0f);
            size2.Y.Should().Be(0.0f);
        }

        [Fact]
        public void GetCharacter_WithValidCharCode_ShouldReturnGlyph()
        {
            // Arrange
            string fontResRef = "fnt_test";
            TPC testTpc = FontTestHelper.CreateTestTPC(256, 256);
            TXI testTxi = FontTestHelper.CreateTestTXI(16.0f, 8.0f, 12.0f, 1.0f, 2.0f, 16, 16, 256.0f);

            byte[] tpcData = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns<string, ResourceType, string[], string[]>((resRef, resType, _, __) =>
                {
                    if (resType == ResourceType.TPC)
                        return new ResourceResult(resRef, ResourceType.TPC, "test.tpc", tpcData);
                    if (resType == ResourceType.TXI)
                        return new ResourceResult(resRef, ResourceType.TXI, "test.txi", txiData);
                    return null;
                });

            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);
            font.Should().NotBeNull();

            // Act
            var glyph = font.GetCharacter((int)'A');

            // Assert
            glyph.HasValue.Should().BeTrue();
            glyph.Value.SourceWidth.Should().BeGreaterThan(0);
            glyph.Value.SourceHeight.Should().BeGreaterThan(0);
        }

        [Fact]
        public void Properties_ShouldReturnCorrectValues()
        {
            // Arrange
            string fontResRef = "fnt_test";
            TPC testTpc = FontTestHelper.CreateTestTPC(512, 512);
            TXI testTxi = FontTestHelper.CreateTestTXI(20.0f, 10.0f, 15.0f, 2.0f, 3.0f, 32, 32, 512.0f);

            byte[] tpcData = TPCAuto.BytesTpc(testTpc, ResourceType.TPC);
            byte[] txiData = TXIAuto.BytesTxi(testTxi);

            _mockResourceLookup.Setup(r => r.LookupResource(
                It.IsAny<string>(),
                It.IsAny<ResourceType>(),
                It.IsAny<SearchLocation[]>(),
                It.IsAny<string>()))
                .Returns<string, ResourceType, string[], string[]>((resRef, resType, _, __) =>
                {
                    if (resType == ResourceType.TPC)
                        return new ResourceResult(resRef, ResourceType.TPC, "test.tpc", tpcData);
                    if (resType == ResourceType.TXI)
                        return new ResourceResult(resRef, ResourceType.TXI, "test.txi", txiData);
                    return null;
                });

            // Act
            EclipseBitmapFont font = EclipseBitmapFont.Load(fontResRef, _installation, _graphicsDevice);

            // Assert
            font.Should().NotBeNull();
            font.FontHeight.Should().Be(20.0f);
            font.FontWidth.Should().Be(10.0f);
            font.BaselineHeight.Should().Be(15.0f);
            font.SpacingR.Should().Be(2.0f);
            font.SpacingB.Should().Be(3.0f);
            font.TextureWidth.Should().Be(512);
            font.TextureHeight.Should().Be(512);
            font.Texture.Should().NotBeNull();
        }
    }
}

