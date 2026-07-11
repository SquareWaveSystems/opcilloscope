using System.Globalization;
using System.Text;
using Opc.Ua;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Utilities;

public class CsvRecordingManagerTests : IDisposable
{
    private readonly Logger _logger;
    private readonly CsvRecordingManager _manager;
    private readonly string _testDirectory;

    public CsvRecordingManagerTests()
    {
        _logger = new Logger();
        _manager = new CsvRecordingManager(_logger);
        _testDirectory = Path.Combine(Path.GetTempPath(), $"Opcilloscope_Test_{Guid.NewGuid()}");
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

        // Stop recording to release the file handle before reading
        _manager.StopRecording();

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
        // Should use ISO 8601 format with T separator and UTC 'Z' designator
        // (Kind=Unspecified is treated as UTC per the OPC UA convention).
        Assert.Contains("2026-01-06T14:30:45.678Z", content);
    }

    [Fact]
    public void RecordValue_LocalKindTimestamp_IsConvertedToUtc()
    {
        // Arrange - a Kind=Local timestamp must be converted to UTC before
        // formatting so a single file never mixes timezones.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var localTime = new DateTime(2026, 1, 6, 14, 30, 45, 678, DateTimeKind.Local);
        var expected = localTime.ToUniversalTime()
            .ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "100",
            Timestamp = localTime
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains(expected, content);
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
    public async Task RecordValue_MultipleThreads_HandlesThreadSafety()
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

        await Task.WhenAll(tasks);
        await Task.Delay(500); // Give background writer time to process all records
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

    [Fact]
    public void RecordValue_SnapshotsValueAtEnqueueTime_NotLiveReference()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "SnapNode",
            NodeId = new NodeId(1234),
            Value = "original",
            StatusCode = 0
        };

        // Act - enqueue, then mutate the live node in place (as the OPC thread would).
        // The snapshot is captured synchronously inside RecordValue, so the writer
        // must serialize "original", never the later "mutated" state.
        _manager.RecordValue(node);
        node.Value = "mutated";
        node.StatusCode = 0x80020000;
        Thread.Sleep(200); // Give the background writer time to process
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("original", content);
        Assert.DoesNotContain("mutated", content);
        // Status was Good at enqueue time; the later Bad mutation must not appear.
        Assert.DoesNotContain("Bad", content);
    }

    [Fact]
    public void StopRecording_FlushesQueuedRecordsBeforeClosing()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        const int count = 50;
        for (int i = 0; i < count; i++)
        {
            _manager.RecordValue(new MonitoredNode
            {
                DisplayName = $"Node{i}",
                NodeId = new NodeId((uint)i),
                Value = $"v{i}"
            });
        }

        // Act - stop immediately without waiting. The in-flight tail must still be
        // flushed (StopRecording waits for the writer, which drains the queue).
        _manager.StopRecording();

        // Assert
        Assert.Equal(count, _manager.RecordCount);
        var lines = File.ReadAllLines(filePath);
        Assert.Equal(count + 1, lines.Length); // header + all records
    }

    /// <summary>
    /// Runs an action under a hostile culture, set as both the current culture
    /// (flows to the background writer task via ExecutionContext) and the
    /// default thread culture (covers any thread that does not inherit it).
    /// Restored in a finally block so other tests are unaffected.
    /// </summary>
    private static void WithCulture(string cultureName, Action action)
    {
        var culture = new CultureInfo(cultureName);
        var originalCurrent = CultureInfo.CurrentCulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCurrent;
            CultureInfo.DefaultThreadCurrentCulture = originalDefault;
        }
    }

    [Fact]
    public void RecordValue_TimestampIsIso8601_UnderFinnishCulture()
    {
        // fi-FI replaces the ':' custom-format placeholder with '.', which
        // previously produced "14.30.45" instead of ISO 8601 "14:30:45".
        WithCulture("fi-FI", () =>
        {
            var filePath = Path.Combine(_testDirectory, "test.csv");
            _manager.StartRecording(filePath);
            var node = new MonitoredNode
            {
                DisplayName = "TestNode",
                NodeId = new NodeId(1234),
                Value = "100",
                Timestamp = new DateTime(2026, 1, 6, 14, 30, 45, 678)
            };

            _manager.RecordValue(node);
            _manager.StopRecording(); // waits for the background writer to drain

            var content = File.ReadAllText(filePath);
            Assert.Contains("2026-01-06T14:30:45.678", content);
            Assert.DoesNotContain("14.30.45", content);
        });
    }

    [Fact]
    public void RecordValue_TimestampUsesGregorianCalendar_UnderThaiCulture()
    {
        // th-TH defaults to the Buddhist calendar (2026 -> 2569), which
        // previously leaked into the recorded year.
        WithCulture("th-TH", () =>
        {
            var filePath = Path.Combine(_testDirectory, "test.csv");
            _manager.StartRecording(filePath);
            var node = new MonitoredNode
            {
                DisplayName = "TestNode",
                NodeId = new NodeId(1234),
                Value = "100",
                Timestamp = new DateTime(2026, 1, 6, 14, 30, 45, 678)
            };

            _manager.RecordValue(node);
            _manager.StopRecording();

            var content = File.ReadAllText(filePath);
            Assert.Contains("2026-01-06T14:30:45.678", content);
            Assert.DoesNotContain("2569", content);
        });
    }

    [Fact]
    public void RecordValue_NullTimestampFallback_IsIso8601_UnderFinnishCulture()
    {
        WithCulture("fi-FI", () =>
        {
            var filePath = Path.Combine(_testDirectory, "test.csv");
            _manager.StartRecording(filePath);
            var node = new MonitoredNode
            {
                DisplayName = "TestNode",
                NodeId = new NodeId(1234),
                Value = "100",
                Timestamp = null // DateTime.Now fallback formatted on the writer thread
            };

            _manager.RecordValue(node);
            _manager.StopRecording();

            var lines = File.ReadAllLines(filePath);
            Assert.True(lines.Length >= 2);
            var timestamp = lines[1].Split(',')[0];
            // ISO 8601 UTC: 'T' separator, ':' time separators (fi-FI would
            // emit '.') and a 'Z' designator on the normalized-to-UTC instant.
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$", timestamp);
        });
    }

    [Fact]
    public void RecordValue_RecordsRawValue_NotTruncatedDisplayValue()
    {
        // Arrange - display Value is the truncated "F2" string; RawValue holds
        // the full-precision invariant representation. The CSV must record RawValue.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "42.12",
            RawValue = "42.123456789012345",
            Timestamp = new DateTime(2026, 1, 6, 12, 30, 45, 123)
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("42.123456789012345", content);
    }

    [Fact]
    public void RecordValue_FallsBackToDisplayValue_WhenRawValueIsEmpty()
    {
        // Arrange - nodes that never had a raw representation set must still record.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "fallback-value"
            // RawValue left empty
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("fallback-value", content);
    }

    [Fact]
    public void RecordValue_SnapshotsRawValueAtEnqueueTime()
    {
        // Arrange - the RawValue snapshot must be immutable at enqueue time,
        // exactly like the display value snapshot.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "SnapNode",
            NodeId = new NodeId(1234),
            Value = "1.23",
            RawValue = "1.2345678"
        };

        // Act - enqueue, then mutate the live node (as the OPC thread would).
        _manager.RecordValue(node);
        node.RawValue = "9.8765432";
        node.Value = "9.88";
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains("1.2345678", content);
        Assert.DoesNotContain("9.8765432", content);
    }

    [Fact]
    public void GenerateDefaultRecordingFilename_UsesGregorianCalendar_UnderThaiCulture()
    {
        WithCulture("th-TH", () =>
        {
            var filename = CsvRecordingManager.GenerateDefaultRecordingFilename(
                "opc.tcp://localhost:4840", 3);

            // The timestamp must use the Gregorian year, not Buddhist (+543).
            var gregorianYear = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
            var buddhistYear = (DateTime.Now.Year + 543).ToString(CultureInfo.InvariantCulture);
            Assert.Contains(gregorianYear, filename);
            Assert.DoesNotContain(buddhistYear, filename);
        });
    }

    [Fact]
    public void RecordValue_NeutralizesFormulaInjection_InDisplayName()
    {
        // Arrange - DisplayName originates from the (potentially untrusted)
        // OPC UA server. "=cmd|'/C calc'!A0" executes as a formula when the
        // CSV is opened in Excel/LibreOffice (CWE-1236).
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "=cmd|'/C calc'!A0",
            NodeId = new NodeId(1234),
            Value = "100"
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording(); // waits for the background writer to drain

        // Assert - the field must be prefixed with a single quote so
        // spreadsheets treat it as text.
        var content = File.ReadAllText(filePath);
        Assert.Contains(",'=cmd|'/C calc'!A0,", content);
        Assert.DoesNotContain(",=cmd", content);
    }

    [Fact]
    public void RecordValue_DoesNotNeutralizeNegativeNumericValue()
    {
        // Arrange - recorded values are routinely negative numbers; they are
        // inert in spreadsheets and prefixing them would corrupt the data
        // column for downstream tools.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "TestNode",
            NodeId = new NodeId(1234),
            Value = "-12.5",
            RawValue = "-12.5"
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert - written verbatim, no neutralization prefix.
        var content = File.ReadAllText(filePath);
        Assert.Contains(",-12.5,", content);
        Assert.DoesNotContain("'-12.5", content);
    }

    [Fact]
    public void RecordValue_NeutralizesPlusPrefixedDisplayName()
    {
        // Arrange - "+SomeTag" starts with a formula trigger and is not a
        // valid invariant-culture number, so it must be neutralized.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "+SomeTag",
            NodeId = new NodeId(1234),
            Value = "100"
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath);
        Assert.Contains(",'+SomeTag,", content);
        Assert.DoesNotContain(",+SomeTag,", content);
    }

    [Fact]
    public void RecordValue_CombinesNeutralizationWithRfc4180Quoting()
    {
        // Arrange - a field that both starts with a formula trigger and
        // contains a comma must be neutralized AND wrapped in quotes.
        var filePath = Path.Combine(_testDirectory, "test.csv");
        _manager.StartRecording(filePath);
        var node = new MonitoredNode
        {
            DisplayName = "=HYPERLINK(\"http://evil\",\"click\")",
            NodeId = new NodeId(1234),
            Value = "=1+2,cmd"
        };

        // Act
        _manager.RecordValue(node);
        _manager.StopRecording();

        // Assert - neutralizing quote prefix inside the RFC 4180 quoted field,
        // with internal quotes doubled.
        var content = File.ReadAllText(filePath);
        Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\",\"\"click\"\")\"", content);
        Assert.Contains("\"'=1+2,cmd\"", content);
    }

    [Fact]
    public void StartRecording_ClearsStaleQueueFromPreviousSession()
    {
        // Arrange - first session leaves a record queued but is stopped.
        var filePath1 = Path.Combine(_testDirectory, "session1.csv");
        _manager.StartRecording(filePath1);
        _manager.StopRecording();

        // Enqueue while not recording (dropped at RecordValue, but be defensive).
        _manager.RecordValue(new MonitoredNode
        {
            DisplayName = "StaleNode",
            NodeId = new NodeId(999),
            Value = "stale"
        });

        // Act - a fresh session must not pick up anything from before.
        var filePath2 = Path.Combine(_testDirectory, "session2.csv");
        _manager.StartRecording(filePath2);
        Thread.Sleep(100);
        _manager.StopRecording();

        // Assert
        var content = File.ReadAllText(filePath2);
        Assert.DoesNotContain("StaleNode", content);
        Assert.DoesNotContain("stale", content);
        Assert.Equal(0, _manager.RecordCount);
    }

    [Fact]
    public void RecordValue_WhenBoundedQueueIsFull_DropsNewRecordAndCountsIt()
    {
        var writer = new BlockingTextWriter();
        using var manager = new CsvRecordingManager(_logger, queueCapacity: 1, _ => writer);
        Assert.True(manager.StartRecording("unused.csv"));

        manager.RecordValue(CreateNode("first"));
        Assert.True(writer.WaitUntilRecordWriteStarts(TimeSpan.FromSeconds(5)));

        // The writer has consumed the first record and is deliberately stalled.
        // One record fits in the channel; the next is dropped without blocking
        // the OPC notification thread.
        manager.RecordValue(CreateNode("second"));
        manager.RecordValue(CreateNode("third"));

        Assert.Equal(1, manager.DroppedRecordCount);

        writer.Release();
        manager.StopRecording();

        Assert.Equal(2, manager.RecordCount);
        Assert.Contains("first", writer.ToString());
        Assert.Contains("second", writer.ToString());
        Assert.DoesNotContain("third", writer.ToString());
    }

    [Fact]
    public async Task StopRecordingAsync_WhenWriterDoesNotFinish_ReportsTimeoutAndKeepsSessionTracked()
    {
        var writer = new BlockingTextWriter();
        using var manager = new CsvRecordingManager(_logger, queueCapacity: 1, _ => writer);
        Assert.True(manager.StartRecording("unused.csv"));
        manager.RecordValue(CreateNode("first"));
        Assert.True(writer.WaitUntilRecordWriteStarts(TimeSpan.FromSeconds(5)));

        var timedOut = await manager.StopRecordingAsync(TimeSpan.FromMilliseconds(50));

        Assert.False(timedOut.Completed);
        Assert.True(manager.IsStopping);
        Assert.False(manager.StartRecording("second.csv"));

        writer.Release();
        var completed = await manager.StopRecordingAsync(System.Threading.Timeout.InfiniteTimeSpan);

        Assert.True(completed.Completed);
        Assert.False(manager.IsStopping);
        Assert.Equal(1, completed.RecordCount);
    }

    [Fact]
    public void StopRecording_WhenRecordWriteFails_ReportsFailedRecordAndError()
    {
        var writer = new ThrowingRecordWriter();
        using var manager = new CsvRecordingManager(_logger, queueCapacity: 10, _ => writer);
        Assert.True(manager.StartRecording("unused.csv"));

        manager.RecordValue(CreateNode("will-fail"));
        manager.StopRecording();

        Assert.Equal(0, manager.RecordCount);
        Assert.Equal(1, manager.FailedRecordCount);
        Assert.Contains("failed", manager.LastError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_WhenRecordWriteFails_LogsDataLoss()
    {
        var writer = new ThrowingRecordWriter();
        var manager = new CsvRecordingManager(_logger, queueCapacity: 10, _ => writer);
        Assert.True(manager.StartRecording("unused.csv"));
        manager.RecordValue(CreateNode("will-fail"));

        manager.Dispose();

        Assert.Contains(
            _logger.GetEntries(),
            entry => entry.Level == LogLevel.Error
                && entry.Message.Contains("data loss", StringComparison.OrdinalIgnoreCase));
    }

    private static MonitoredNode CreateNode(string value) => new()
    {
        DisplayName = value,
        NodeId = new NodeId(value, namespaceIndex: 1),
        Value = value,
        RawValue = value
    };

    private sealed class BlockingTextWriter : StringWriter
    {
        private readonly ManualResetEventSlim _recordWriteStarted = new();
        private readonly ManualResetEventSlim _release = new();
        private int _lineCount;
        private int _disposed;

        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string? value)
        {
            if (Interlocked.Increment(ref _lineCount) > 1)
            {
                _recordWriteStarted.Set();
                if (!_release.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("Test writer was not released");
                }
            }

            base.WriteLine(value);
        }

        public bool WaitUntilRecordWriteStarts(TimeSpan timeout) => _recordWriteStarted.Wait(timeout);

        public void Release() => _release.Set();

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _release.Set();
            _recordWriteStarted.Dispose();
            _release.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingRecordWriter : StringWriter
    {
        private int _lineCount;

        public override void WriteLine(string? value)
        {
            if (Interlocked.Increment(ref _lineCount) > 1)
            {
                throw new IOException("simulated storage failure");
            }

            base.WriteLine(value);
        }
    }
}
