using FluentAssertions;
using HolocronToolset.NET.Config;
using Xunit;

namespace HolocronToolset.NET.Tests.Config
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py
    // Tests for ConfigVersion utility functions
    public class ConfigVersionTests
    {
        [Fact]
        public void VersionToToolsetTag_WithThreePartVersion_RemovesSecondDot()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_version.py:4
            // Original: version_to_toolset_tag("4.0.0") should return "v4.00-toolset"
            string result = ConfigVersion.VersionToToolsetTag("4.0.0");
            result.Should().Be("v4.00-toolset");
        }

        [Fact]
        public void VersionToToolsetTag_WithTwoPartVersion_KeepsAsIs()
        {
            string result = ConfigVersion.VersionToToolsetTag("4.0");
            result.Should().Be("v4.0-toolset");
        }

        [Fact]
        public void ToolsetTagToVersion_WithThreePartVersion_ReturnsCorrectly()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_version.py:14
            // Original: toolset_tag_to_version("v4.0.0-toolset") should return "4.0.0"
            string result = ConfigVersion.ToolsetTagToVersion("v4.0.0-toolset");
            result.Should().Be("4.0.0");
        }

        [Fact]
        public void ToolsetTagToVersion_WithTwoPartVersion_ReturnsCorrectly()
        {
            string result = ConfigVersion.ToolsetTagToVersion("v4.0-toolset");
            result.Should().Be("4.0");
        }

        [Fact]
        public void ToolsetTagToVersion_WithLegacyTypoFormat_HandlesCorrectly()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/config/config_version.py:27
            // Original: toolset_tag_to_version("v400-toolset") should return "4.0.0"
            string result = ConfigVersion.ToolsetTagToVersion("v400-toolset");
            result.Should().Be("4.0.0");
        }
    }
}
