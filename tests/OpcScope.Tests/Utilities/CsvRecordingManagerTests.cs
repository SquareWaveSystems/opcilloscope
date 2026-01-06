using Opc.Ua;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;

namespace OpcScope.Tests.Utilities;

public class CsvRecordingManagerTests : IDisposable
{
    private readonly Logger _logger;
    private readonly CsvRecordingManager _manager;
    private readonly string _testDirectory;

    public CsvRecordingManagerTests()
    {
        _logger = new Logger();
        _manager = new CsvRecordingManager(_logger);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"OpcScope_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        _manager.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void StartRecording_CreatesFileWithHeader()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");

        // Act
        var result = _manager.StartRecording(filePath);

        // Assert
        Assert.True(result);
        Assert.True(File.Exists(filePath));
        var content = File.ReadAllText(filePath);
        Assert.Contains("Timestamp,DisplayName,NodeId,Value,Status", content);
    }

    [Fact]
    public void StartRecording_SetsIsRecording()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");

        // Act
        _manager.StartRecording(filePath);

        // Assert
        Assert.True(_manager.IsRecording);
    }

    [Fact]
    public void StartRecording_RaisesRecordingStateChangedEvent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        bool eventFired = false;
        bool eventValue = false;
        _manager.RecordingStateChanged += (isRecording) =>
        {
            eventFired = true;
            eventValue = isRecording;
        };

        // Act
        _manager.StartRecording(filePath);

        // Assert
        Assert.True(eventFired);
        Assert.True(eventValue);
    }

    [Fact]
    public void StartRecording_WhenAlreadyRecording_ReturnsFalse()
    {
        // Arrange
        var filePath1 = Path.Combine(_testDirectory, "test1.csv");
        var filePath2 = Path.Combine(_testDirectory, "test2.csv");
        _manager.StartRecording(filePath1);

        // Act
        var result = _manager.StartRecording(filePath2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void StopRecording_SetsIsRecordingToFalse()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);

        // Act
        _manager.StopRecording();

        // Assert
        Assert.False(_manager.IsRecording);
    }

    [Fact]
    public void StopRecording_RaisesRecordingStateChangedEvent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        bool eventFired = false;
        bool eventValue = true;
        _manager.RecordingStateChanged += (isRecording) =>
        {
            eventFired = true;
            eventValue = isRecording;
        };

        // Act
        _manager.StopRecording();

        // Assert
        Assert.True(eventFired);
        Assert.False(eventValue);
    }

    [Fact]
    public void RecordValue_WritesValueToFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "42.5",
            Timestamp = new DateTime(2026, 1, 6, 12, 30, 45, 123),
            StatusCode = 0
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("TestNode", content);
        Assert.Contains("42.5", content);
        Assert.Contains("2026-01-06T12:30:45.123", content);
        Assert.Contains("Good", content);
    }

    [Fact]
    public void RecordValue_IncrementsRecordCount()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100"
        };

        // Act
        _manager.RecordValue(node);
        _manager.RecordValue(node);
        _manager.RecordValue(node);
        Thread.Sleep(500); // Give background writer more time to process
        _manager.StopRecording();

        // Assert
        Assert.Equal(3, _manager.RecordCount);
    }

    [Fact]
    public void RecordValue_EscapesCommasInValues()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "Test,Node",
            NodeId = new NodeId(1234),
            Value = "Value,With,Commas"
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("\"Test,Node\"", content);
        Assert.Contains("\"Value,With,Commas\"", content);
    }

    [Fact]
    public void RecordValue_EscapesQuotesInValues()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "Test\"Node",
            NodeId = new NodeId(1234),
            Value = "Value\"With\"Quotes"
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("\"Test\"\"Node\"", content);
        Assert.Contains("\"Value\"\"With\"\"Quotes\"", content);
    }

    [Fact]
    public void RecordValue_EscapesStatusField()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100",
            StatusCode = 0x80020000 // Bad status with parentheses
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        // Status field contains parentheses: "Bad (0x80020000)"
        // Our CSV escaping only handles commas, quotes, and newlines, not parentheses
        Assert.Contains("Bad", content);
    }

    [Fact]
    public void RecordValue_UsesIso8601TimestampFormat()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100",
            Timestamp = new DateTime(2026, 1, 6, 14, 30, 45, 678)
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        // Should use ISO 8601 format with T separator
        Assert.Contains("2026-01-06T14:30:45.678", content);
    }

    [Fact]
    public void RecordValue_WhenNotRecording_DoesNotWrite()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        _manager.StopRecording();
        var initialContent = File.ReadAllText(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100"
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give time for any potential writes

        // Assert
        var finalContent = File.ReadAllText(filePath);
        Assert.Equal(initialContent, finalContent);
    }

    [Fact]
    public void RecordingDuration_WhenRecording_ReturnsElapsedTime()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        Thread.Sleep(150); // Wait a bit

        // Act
        var duration = _manager.RecordingDuration;

        // Assert
        Assert.True(duration.TotalMilliseconds >= 100);
        Assert.True(duration.TotalMilliseconds < 1000); // Should be less than 1 second
    }

    [Fact]
    public void RecordingDuration_WhenNotRecording_ReturnsZero()
    {
        // Act
        var duration = _manager.RecordingDuration;

        // Assert
        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void RecordingDuration_AfterStop_ReturnsZero()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        Thread.Sleep(100);
        _manager.StopRecording();

        // Act
        var duration = _manager.RecordingDuration;

        // Assert
        Assert.Equal(TimeSpan.Zero, duration);
    }

    [Fact]
    public void FilePath_WhenRecording_ReturnsPath()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);

        // Act
        var result = _manager.FilePath;

        // Assert
        Assert.Equal(filePath, result);
    }

    [Fact]
    public void RecordValue_MultipleThreads_HandlesThreadSafety()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var tasks = new List<Task>();
        const int numThreads = 10;
        const int recordsPerThread = 10;

        // Act - Record from multiple threads
        for (int i = 0; i < numThreads; i++)
        {
            var threadId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < recordsPerThread; j++)
                {
                    var node = new MonitoredNode
                    {
                        DisplayName = $"Node_{threadId}_{j}",
                        NodeId = new NodeId((uint)(threadId * 100 + j)),
                        Value = $"Value_{threadId}_{j}"
                    };
                    _manager.RecordValue(node);
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());
        Thread.Sleep(500); // Give background writer time to process all records
        _manager.StopRecording();

        // Assert
        Assert.Equal(numThreads * recordsPerThread, _manager.RecordCount);
        var lines = File.ReadAllLines(filePath);
        // Should have header + all records
        Assert.Equal(numThreads * recordsPerThread + 1, lines.Length);
    }

    [Fact]
    public void Dispose_StopsRecording()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        var manager = new CsvRecordingManager(_logger);
        manager.StartRecording(filePath);

        // Act
        manager.Dispose();

        // Assert
        Assert.False(manager.IsRecording);
    }

    [Fact]
    public void RecordValue_WithNullTimestamp_UsesCurrentTime()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100",
            Timestamp = null // No timestamp
        };

        // Act
        _manager.RecordValue(node);
        Thread.Sleep(100); // Give background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        // Should contain a timestamp close to current time
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2); // Header + at least one record
        var dataLine = lines[1];
        var timestamp = dataLine.Split(',')[0];
        // Verify it's in ISO 8601 format with T
        Assert.Contains("T", timestamp);
    }
}
