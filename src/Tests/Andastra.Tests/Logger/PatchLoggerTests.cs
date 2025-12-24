using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Andastra.Parsing.Logger;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Logger
{

    /// <summary>
    /// Tests for PatchLogger functionality
    /// </summary>
    public class PatchLoggerTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddVerboseShouldAddLogWithVerboseType()
        {
            // Arrange
            var logger = new PatchLogger();

            // Act
            logger.AddVerbose("Test verbose message");

            // Assert
            logger.AllLogs.Should().HaveCount(1);
            logger.AllLogs[0].LogType.Should().Be(LogType.Verbose);
            logger.AllLogs[0].Message.Should().Be("Test verbose message");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddNoteShouldAddLogWithNoteType()
        {
            // Arrange
            var logger = new PatchLogger();

            // Act
            logger.AddNote("Test note message");

            // Assert
            logger.AllLogs.Should().HaveCount(1);
            logger.AllLogs[0].LogType.Should().Be(LogType.Note);
            logger.AllLogs[0].Message.Should().Be("Test note message");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddWarningShouldAddLogWithWarningType()
        {
            // Arrange
            var logger = new PatchLogger();

            // Act
            logger.AddWarning("Test warning message");

            // Assert
            logger.AllLogs.Should().HaveCount(1);
            logger.AllLogs[0].LogType.Should().Be(LogType.Warning);
            logger.AllLogs[0].Message.Should().Be("Test warning message");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddErrorShouldAddLogWithErrorType()
        {
            // Arrange
            var logger = new PatchLogger();

            // Act
            logger.AddError("Test error message");

            // Assert
            logger.AllLogs.Should().HaveCount(1);
            logger.AllLogs[0].LogType.Should().Be(LogType.Error);
            logger.AllLogs[0].Message.Should().Be("Test error message");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LogAddedEventShouldFireWhenLogAdded()
        {
            // Arrange
            var logger = new PatchLogger();
            PatchLog addedLog = null;
            logger.LogAdded += (sender, log) => addedLog = log;

            // Act
            logger.AddNote("Test message");

            // Assert
            addedLog.Should().NotBeNull();
            addedLog.Message.Should().Be("Test message");
            addedLog.LogType.Should().Be(LogType.Note);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void VerboseLogsShouldFilterByVerboseType()
        {
            // Arrange
            var logger = new PatchLogger();
            logger.AddVerbose("Verbose 1");
            logger.AddNote("Note 1");
            logger.AddVerbose("Verbose 2");
            logger.AddWarning("Warning 1");

            // Act
            var verboseLogs = logger.VerboseLogs.ToList();

            // Assert
            verboseLogs.Should().HaveCount(2);
            verboseLogs.All(l => l.LogType == LogType.Verbose).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void NotesShouldFilterByNoteType()
        {
            // Arrange
            var logger = new PatchLogger();
            logger.AddNote("Note 1");
            logger.AddVerbose("Verbose 1");
            logger.AddNote("Note 2");

            // Act
            var notes = logger.Notes.ToList();

            // Assert
            notes.Should().HaveCount(2);
            notes.All(l => l.LogType == LogType.Note).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void WarningsShouldFilterByWarningType()
        {
            // Arrange
            var logger = new PatchLogger();
            logger.AddWarning("Warning 1");
            logger.AddNote("Note 1");
            logger.AddWarning("Warning 2");

            // Act
            var warnings = logger.Warnings.ToList();

            // Assert
            warnings.Should().HaveCount(2);
            warnings.All(l => l.LogType == LogType.Warning).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ErrorsShouldFilterByErrorType()
        {
            // Arrange
            var logger = new PatchLogger();
            logger.AddError("Error 1");
            logger.AddNote("Note 1");
            logger.AddError("Error 2");

            // Act
            var errors = logger.Errors.ToList();

            // Assert
            errors.Should().HaveCount(2);
            errors.All(l => l.LogType == LogType.Error).Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void MultipleLogTypesShouldMaintainOrder()
        {
            // Arrange
            var logger = new PatchLogger();

            // Act
            logger.AddVerbose("Message 1");
            logger.AddNote("Message 2");
            logger.AddWarning("Message 3");
            logger.AddError("Message 4");

            // Assert
            logger.AllLogs.Should().HaveCount(4);
            logger.AllLogs[0].LogType.Should().Be(LogType.Verbose);
            logger.AllLogs[1].LogType.Should().Be(LogType.Note);
            logger.AllLogs[2].LogType.Should().Be(LogType.Warning);
            logger.AllLogs[3].LogType.Should().Be(LogType.Error);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public async Task ThreadSafetyShouldHandleConcurrentAccess()
        {
            // Arrange
            var logger = new PatchLogger();
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                tasks.Add(Task.Run(() => logger.AddNote($"Message {index}")));
            }
            await Task.WhenAll(tasks.ToArray());

            // Assert
            logger.AllLogs.Should().HaveCount(100);
        }
    }
}

