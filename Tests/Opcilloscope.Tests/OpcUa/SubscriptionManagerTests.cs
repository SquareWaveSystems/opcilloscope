using System.Globalization;
using Opc.Ua;
using Opcilloscope.OpcUa;

namespace Opcilloscope.Tests.OpcUa;

public class SubscriptionManagerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(250, 250)]
    [InlineData(5000, 5000)]
    [InlineData(-1, 0)]
    [InlineData(100000, 60000)]
    public void SamplingInterval_ClampsToValidRange(int requested, int expected)
    {
        var manager = new SubscriptionManager(
            new global::Opcilloscope.OpcUa.OpcUaClientWrapper(),
            new global::Opcilloscope.Utilities.Logger());

        manager.SamplingInterval = requested;

        Assert.Equal(expected, manager.SamplingInterval);
    }

    [Fact]
    public void FormatValue_ReturnsNull_WhenValueIsNull()
    {
        // Act
        var result = SubscriptionManager.FormatValue(null);

        // Assert
        Assert.Equal("null", result);
    }

    [Fact]
    public void FormatValue_FormatsString_AsIs()
    {
        // Arrange
        var value = "Hello World";

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void FormatValue_FormatsInteger_AsIs()
    {
        // Arrange
        var value = 42;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("42", result);
    }

    [Fact]
    public void FormatValue_FormatsFloat_WithTwoDecimals()
    {
        // Arrange
        var value = 3.14159f;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("3.14", result);
    }

    [Fact]
    public void FormatValue_FormatsDouble_WithTwoDecimals()
    {
        // Arrange
        var value = 2.71828;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("2.72", result);
    }

    [Fact]
    public void FormatValue_FormatsDouble_ZeroDecimalPlaces()
    {
        // Arrange
        var value = 100.0;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("100.00", result);
    }

    [Fact]
    public void FormatValue_FormatsBoolean_True()
    {
        // Arrange
        var value = true;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("True", result);
    }

    [Fact]
    public void FormatValue_FormatsBoolean_False()
    {
        // Arrange
        var value = false;

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("False", result);
    }

    [Fact]
    public void FormatValue_FormatsDateTime()
    {
        // Arrange
        var value = new DateTime(2024, 6, 15, 10, 30, 45);

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Contains("2024", result);
    }

    [Fact]
    public void FormatValue_FormatsEmptyByteArray()
    {
        // Arrange
        var value = new byte[0];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[0 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsNonEmptyByteArray()
    {
        // Arrange
        var value = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[5 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsLargeByteArray()
    {
        // Arrange
        var value = new byte[1024];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[1024 bytes]", result);
    }

    [Fact]
    public void FormatValue_FormatsEmptyIntArray()
    {
        // Arrange
        var value = new int[0];

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[0 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsIntArray()
    {
        // Arrange
        var value = new int[] { 1, 2, 3, 4, 5 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[5 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsStringArray()
    {
        // Arrange
        var value = new string[] { "a", "b", "c" };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[3 items]", result);
    }

    [Fact]
    public void FormatValue_FormatsDoubleArray()
    {
        // Arrange
        var value = new double[] { 1.1, 2.2, 3.3 };

        // Act
        var result = SubscriptionManager.FormatValue(value);

        // Assert
        Assert.Equal("[3 items]", result);
    }

    [Theory]
    [InlineData(0.0f, "0.00")]
    [InlineData(-1.5f, "-1.50")]
    [InlineData(999.999f, "1000.00")]
    [InlineData(0.001f, "0.00")]
    [InlineData(0.006f, "0.01")] // 0.005f is not exactly representable in IEEE754 (becomes 0.00499999989)
    public void FormatValue_FloatFormatting_VariousValues(float input, string expected)
    {
        // Act
        var result = SubscriptionManager.FormatValue(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(0.0, "0.00")]
    [InlineData(-1.5, "-1.50")]
    [InlineData(999.999, "1000.00")]
    [InlineData(0.001, "0.00")]
    [InlineData(0.005, "0.01")]
    public void FormatValue_DoubleFormatting_VariousValues(double input, string expected)
    {
        // Act
        var result = SubscriptionManager.FormatValue(input);

        // Assert
        Assert.Equal(expected, result);
    }
}

public class FormatRawValueTests
{
    /// <summary>
    /// Runs an action with the given culture set as both the current and the
    /// default thread culture, restoring the originals afterwards.
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
    public void FormatRawValue_Null_ReturnsNullString()
    {
        Assert.Equal("null", SubscriptionManager.FormatRawValue(null));
    }

    [Fact]
    public void FormatRawValue_String_ReturnsAsIs()
    {
        Assert.Equal("Hello, World", SubscriptionManager.FormatRawValue("Hello, World"));
    }

    [Fact]
    public void FormatRawValue_Double_PreservesFullPrecision()
    {
        // Display format truncates to "2.72"; raw must keep full precision.
        Assert.Equal("2.71828", SubscriptionManager.FormatRawValue(2.71828));
    }

    [Fact]
    public void FormatRawValue_Float_PreservesFullPrecision()
    {
        Assert.Equal("3.14159", SubscriptionManager.FormatRawValue(3.14159f));
    }

    [Fact]
    public void FormatRawValue_Double_RoundTrips()
    {
        var original = 1.0 / 3.0;

        var text = SubscriptionManager.FormatRawValue(original);
        var parsed = double.Parse(text, CultureInfo.InvariantCulture);

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void FormatRawValue_Float_RoundTrips()
    {
        var original = 0.1f * 7f;

        var text = SubscriptionManager.FormatRawValue(original);
        var parsed = float.Parse(text, CultureInfo.InvariantCulture);

        Assert.Equal(original, parsed);
    }

    [Theory]
    [InlineData("fi-FI")]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public void FormatRawValue_Double_UsesDotDecimalSeparator_UnderHostileCulture(string cultureName)
    {
        WithCulture(cultureName, () =>
        {
            var result = SubscriptionManager.FormatRawValue(42.12);

            // Exact ordinal comparison: '.' decimal separator, never ','.
            // (Avoid Assert.DoesNotContain(string) here - its default
            // comparison is culture-sensitive and th-TH collation treats
            // punctuation as ignorable.)
            Assert.Equal("42.12", result);
        });
    }

    [Fact]
    public void FormatRawValue_Decimal_UsesInvariantCulture()
    {
        WithCulture("de-DE", () =>
        {
            Assert.Equal("1234.5678", SubscriptionManager.FormatRawValue(1234.5678m));
        });
    }

    [Fact]
    public void FormatRawValue_DateTime_UsesIso8601RoundTripFormat()
    {
        WithCulture("th-TH", () =>
        {
            var value = new DateTime(2026, 1, 6, 14, 30, 45, 678, DateTimeKind.Utc);

            var result = SubscriptionManager.FormatRawValue(value);

            // ISO 8601, Gregorian year (not Buddhist 2569), ':' separators.
            Assert.Equal("2026-01-06T14:30:45.6780000Z", result);
        });
    }

    [Fact]
    public void FormatRawValue_IntArray_SerializesElementsSemicolonJoined()
    {
        Assert.Equal("1;2;3;4;5", SubscriptionManager.FormatRawValue(new[] { 1, 2, 3, 4, 5 }));
    }

    [Fact]
    public void FormatRawValue_DoubleArray_SerializesElementsInvariantly()
    {
        WithCulture("fi-FI", () =>
        {
            Assert.Equal("1.1;2.2;3.3", SubscriptionManager.FormatRawValue(new[] { 1.1, 2.2, 3.3 }));
        });
    }

    [Fact]
    public void FormatRawValue_StringArray_SerializesElements()
    {
        Assert.Equal("a;b;c", SubscriptionManager.FormatRawValue(new[] { "a", "b", "c" }));
    }

    [Fact]
    public void FormatRawValue_StringArrayWithSemicolons_EscapesElementSeparators()
    {
        // ["a;b", "c"] must not collide with ["a", "b", "c"].
        Assert.Equal(@"a\;b;c", SubscriptionManager.FormatRawValue(new[] { "a;b", "c" }));
        Assert.NotEqual(
            SubscriptionManager.FormatRawValue(new[] { "a", "b", "c" }),
            SubscriptionManager.FormatRawValue(new[] { "a;b", "c" }));
    }

    [Fact]
    public void FormatRawValue_StringArrayWithBackslashes_EscapesBackslashes()
    {
        // A literal backslash is doubled so it can't be misread as an escape.
        Assert.Equal(@"a\\;b\\\;c", SubscriptionManager.FormatRawValue(new[] { @"a\", @"b\;c" }));
    }

    [Fact]
    public void FormatRawValue_EmptyArray_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, SubscriptionManager.FormatRawValue(Array.Empty<int>()));
    }

    [Fact]
    public void FormatRawValue_ByteArray_SerializesElements()
    {
        Assert.Equal("1;2;255", SubscriptionManager.FormatRawValue(new byte[] { 1, 2, 255 }));
    }

    [Fact]
    public void FormatRawValue_Boolean_FormatsAsTrueFalse()
    {
        Assert.Equal("True", SubscriptionManager.FormatRawValue(true));
        Assert.Equal("False", SubscriptionManager.FormatRawValue(false));
    }

    [Fact]
    public void FormatRawValue_Integer_FormatsInvariantly()
    {
        WithCulture("de-DE", () =>
        {
            Assert.Equal("1234567", SubscriptionManager.FormatRawValue(1234567));
        });
    }
}

public class NodeIdExtensionsTests
{
    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenFirstIsNull()
    {
        // Arrange
        NodeId? nodeId = null;
        var other = new NodeId(100);

        // Act
        var result = nodeId!.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenSecondIsNull()
    {
        // Arrange
        var nodeId = new NodeId(100);
        NodeId? other = null;

        // Act
        var result = nodeId.EqualsNodeId(other!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenBothAreNull()
    {
        // Arrange
        NodeId? nodeId = null;
        NodeId? other = null;

        // Act
        var result = nodeId!.EqualsNodeId(other!);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameNumericId()
    {
        // Arrange
        var nodeId = new NodeId(100);
        var other = new NodeId(100);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentNumericId()
    {
        // Arrange
        var nodeId = new NodeId(100);
        var other = new NodeId(200);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameStringId()
    {
        // Arrange
        var nodeId = new NodeId("TestNode", 2);
        var other = new NodeId("TestNode", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentStringId()
    {
        // Arrange
        var nodeId = new NodeId("TestNode1", 2);
        var other = new NodeId("TestNode2", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentNamespace()
    {
        // Arrange
        var nodeId = new NodeId("TestNode", 1);
        var other = new NodeId("TestNode", 2);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameGuidId()
    {
        // Arrange
        var guid = Guid.NewGuid();
        var nodeId = new NodeId(guid, 3);
        var other = new NodeId(guid, 3);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsFalse_WhenDifferentGuidId()
    {
        // Arrange
        var nodeId = new NodeId(Guid.NewGuid(), 3);
        var other = new NodeId(Guid.NewGuid(), 3);

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_WhenSameReference()
    {
        // Arrange
        var nodeId = new NodeId(100);

        // Act
        var result = nodeId.EqualsNodeId(nodeId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EqualsNodeId_ReturnsTrue_ForWellKnownObjectIds()
    {
        // Arrange
        var nodeId = ObjectIds.RootFolder;
        var other = new NodeId(84, 0); // RootFolder = ns=0;i=84

        // Act
        var result = nodeId.EqualsNodeId(other);

        // Assert
        Assert.True(result);
    }
}
