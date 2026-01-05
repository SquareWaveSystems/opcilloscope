using OpcScope.Utilities;

namespace OpcScope.Tests.Utilities;

public class LoggerTests
{
    [Fact]
    public void Logger_AddMessage_StoresMessages()
    {
        // Arrange
        var logger = new Logger();

        // Act
        logger.Info("Test message");
        logger.Warning("Warning message");
        logger.Error("Error message");

        // Assert
        var entries = logger.GetEntries();
        Assert.Equal(3, entries.Count);
        Assert.Equal("Test message", entries[0].Message);
        Assert.Equal("Warning message", entries[1].Message);
        Assert.Equal("Error message", entries[2].Message);
    }

    [Fact]
    public void Logger_Info_SetsInfoLevel()
    {
        // Arrange
        var logger = new Logger();

        // Act
        logger.Info("Test");

        // Assert
        var entry = logger.GetEntries().First();
        Assert.Equal(LogLevel.Info, entry.Level);
    }

    [Fact]
    public void Logger_Warning_SetsWarningLevel()
    {
        // Arrange
        var logger = new Logger();

        // Act
        logger.Warning("Test");

        // Assert
        var entry = logger.GetEntries().First();
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Logger_Error_SetsErrorLevel()
    {
        // Arrange
        var logger = new Logger();

        // Act
        logger.Error("Test");

        // Assert
        var entry = logger.GetEntries().First();
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    [Fact]
    public void Logger_Clear_RemovesAllMessages()
    {
        // Arrange
        var logger = new Logger();
        logger.Info("Message 1");
        logger.Info("Message 2");

        // Act
        logger.Clear();

        // Assert
        Assert.Empty(logger.GetEntries());
    }

    [Fact]
    public void LogEntry_ToString_IncludesInfoPrefix()
    {
        // Arrange
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Info,
            Message = "Test"
        };

        // Assert
        var str = entry.ToString();
        Assert.Contains("[INFO]", str);
        Assert.Contains("Test", str);
    }

    [Fact]
    public void LogEntry_ToString_IncludesWarningPrefix()
    {
        // Arrange
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Warning,
            Message = "Test"
        };

        // Assert
        var str = entry.ToString();
        Assert.Contains("[WARN]", str);
    }

    [Fact]
    public void LogEntry_ToString_IncludesErrorPrefix()
    {
        // Arrange
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Error,
            Message = "Test"
        };

        // Assert
        var str = entry.ToString();
        Assert.Contains("[ERROR]", str);
    }

    [Fact]
    public void Logger_LogAdded_EventFires()
    {
        // Arrange
        var logger = new Logger();
        LogEntry? receivedEntry = null;
        logger.LogAdded += entry => receivedEntry = entry;

        // Act
        logger.Info("Test message");

        // Assert
        Assert.NotNull(receivedEntry);
        Assert.Equal("Test message", receivedEntry.Message);
    }
}
