using System;
using System.IO;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Reader;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{
    /// <summary>
    /// Tests for ConfigReader Int32 overflow handling in ParseIntValue.
    /// 
    /// These tests verify that Int32 overflow errors are handled gracefully when parsing
    /// values from changes.ini files, particularly in HACKList sections.
    /// 
    /// The error "Failed to load namespace config: Value was either too large or too small for an Int32"
    /// occurs when ParseIntValue encounters values that exceed Int32.MaxValue (2147483647) or
    /// are less than Int32.MinValue (-2147483648).
    /// </summary>
    public class ConfigReaderInt32OverflowTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _iniFilePath;
        private readonly string _modPath;

        public ConfigReaderInt32OverflowTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _modPath = Path.Combine(_tempDir, "tslpatchdata");
            Directory.CreateDirectory(_modPath);
            _iniFilePath = Path.Combine(_modPath, "changes.ini");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithHexValueExceedingInt32Max_ShouldThrowOverflowException()
        {
            // Arrange - Hex value 0x80000000 = 2147483648, which exceeds Int32.MaxValue (2147483647)
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:0x80000000
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            // The exception might be wrapped, so check both the exception and inner exception
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<Exception>()
                .Where(ex =>
                    ex is OverflowException ||
                    (ex.InnerException is OverflowException) ||
                    ex.Message.Contains("too large") ||
                    ex.Message.Contains("too small") ||
                    ex.Message.Contains("Int32"))
                .WithMessage("*Int32*", "because Int32 overflow currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithDecimalValueExceedingInt32Max_ShouldThrowOverflowException()
        {
            // Arrange - Decimal value 2147483648 exceeds Int32.MaxValue (2147483647)
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:2147483648
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            // The exception might be wrapped, so check both the exception and inner exception
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<Exception>()
                .Where(ex =>
                    ex is OverflowException ||
                    (ex.InnerException is OverflowException) ||
                    ex.Message.Contains("too large") ||
                    ex.Message.Contains("too small") ||
                    ex.Message.Contains("Int32"))
                .WithMessage("*Int32*", "because Int32 overflow currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithHexValueInStrRef_ShouldThrowOverflowException()
        {
            // Arrange - StrRef with hex value exceeding Int32.MaxValue
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:strref0x80000000
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because Int32 overflow in strref currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithHexValueIn2DAMemory_ShouldThrowOverflowException()
        {
            // Arrange - 2DAMemory with hex value exceeding Int32.MaxValue
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:2damemory0x80000000
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because Int32 overflow in 2damemory currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithHexOffsetExceedingInt32Max_ShouldThrowOverflowException()
        {
            // Arrange - Offset value 0x80000000 exceeds Int32.MaxValue
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x80000000=u32:12345
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because Int32 overflow in offset currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithVeryLargeHexValue_ShouldThrowOverflowException()
        {
            // Arrange - Very large hex value 0xFFFFFFFF = 4294967295 (UInt32.MaxValue)
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:0xFFFFFFFF
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because very large hex values currently cause unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithNegativeValueBelowInt32Min_ShouldThrowOverflowException()
        {
            // Arrange - Negative value below Int32.MinValue (-2147483648)
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=i32:-2147483649
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because Int32 underflow currently causes unhandled exception");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LoadHackList_WithMultipleOverflowValues_ShouldThrowOverflowException()
        {
            // Arrange - Multiple values that would cause overflow
            // This test verifies the bug exists - it should throw OverflowException
            string iniContent = @"[Settings]
ModName=Test Mod

[HACKList]
test.ncs=test.ncs

[test.ncs]
0x1000=u32:0x80000000
0x2000=u32:2147483648
0x3000=u32:strref0xFFFFFFFF
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Currently throws OverflowException (this is the bug)
            Action act = () =>
            {
                var reader = ConfigReader.FromFilePath(_iniFilePath, logger);
                reader.Load(reader.Config);
            };
            act.Should().Throw<OverflowException>("because multiple Int32 overflows currently cause unhandled exception");
        }
    }
}

