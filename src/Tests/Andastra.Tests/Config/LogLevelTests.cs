using Andastra.Parsing.Config;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Config
{

    /// <summary>
    /// Tests for LogLevel enum
    /// </summary>
    public class LogLevelTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LogLevel_ShouldHaveExpectedValues()
        {
            // Assert
            ((int)LogLevel.Nothing).Should().Be(0);
            ((int)LogLevel.General).Should().Be(1);
            ((int)LogLevel.Errors).Should().Be(2);
            ((int)LogLevel.Warnings).Should().Be(3);
            ((int)LogLevel.Full).Should().Be(4);
        }

        [Theory(Timeout = 120000)] // 2 minutes timeout
        [InlineData(LogLevel.Nothing, 0)]
        [InlineData(LogLevel.General, 1)]
        [InlineData(LogLevel.Errors, 2)]
        [InlineData(LogLevel.Warnings, 3)]
        [InlineData(LogLevel.Full, 4)]
        public void LogLevel_ShouldCastToInt(LogLevel level, int expected)
        {
            // Act
            int value = (int)level;

            // Assert
            value.Should().Be(expected);
        }

        [Theory(Timeout = 120000)] // 2 minutes timeout
        [InlineData(0, LogLevel.Nothing)]
        [InlineData(1, LogLevel.General)]
        [InlineData(2, LogLevel.Errors)]
        [InlineData(3, LogLevel.Warnings)]
        [InlineData(4, LogLevel.Full)]
        public void LogLevel_ShouldCastFromInt(int value, LogLevel expected)
        {
            // Act
            LogLevel level = (LogLevel)value;

            // Assert
            level.Should().Be(expected);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LogLevel_ShouldCompare()
        {
            // Assert
            (LogLevel.Full > LogLevel.Warnings).Should().BeTrue();
            (LogLevel.Warnings > LogLevel.Errors).Should().BeTrue();
            (LogLevel.Errors > LogLevel.General).Should().BeTrue();
            (LogLevel.General > LogLevel.Nothing).Should().BeTrue();
        }

    }
}

