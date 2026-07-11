using Opc.Ua;
using Opcilloscope.App.Views;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Tests.App.Views;

/// <summary>
/// Tests for ScopeView's sample extraction: the scope must plot the
/// full-precision RawValue rather than the "F2"-truncated display Value,
/// and must render booleans as 0/1.
/// </summary>
public class ScopeViewSampleTests
{
    private static MonitoredNode Node(string value, string rawValue = "") => new()
    {
        NodeId = new NodeId(1234),
        DisplayName = "TestNode",
        Value = value,
        RawValue = rawValue
    };

    [Fact]
    public void TryGetSample_PrefersRawValueOverDisplayValue()
    {
        // Display value is truncated to two decimals; RawValue is lossless.
        var node = Node(value: "0.00", rawValue: "0.0042");

        Assert.True(ScopeView.TryGetSample(node, out var sample));
        Assert.Equal(0.0042f, sample, precision: 6);
    }

    [Fact]
    public void TryGetSample_SubCentAmplitudeSignal_SurvivesIntoSample()
    {
        // A signal with amplitude below 0.01 flatlined when the "F2" display
        // string was parsed; RawValue preserves it.
        var node = Node(value: "0.00", rawValue: "0.005");

        Assert.True(ScopeView.TryGetSample(node, out var sample));
        Assert.NotEqual(0f, sample);
    }

    [Fact]
    public void TryGetSample_FallsBackToDisplayValue_WhenRawValueIsEmpty()
    {
        var node = Node(value: "42.50");

        Assert.True(ScopeView.TryGetSample(node, out var sample));
        Assert.Equal(42.5f, sample, precision: 4);
    }

    [Theory]
    [InlineData("True", 1f)]
    [InlineData("False", 0f)]
    [InlineData("true", 1f)]
    [InlineData("false", 0f)]
    public void TryGetSample_BooleanValues_PlotAsZeroOrOne(string raw, float expected)
    {
        var node = Node(value: raw, rawValue: raw);

        Assert.True(ScopeView.TryGetSample(node, out var sample));
        Assert.Equal(expected, sample);
    }

    [Theory]
    [InlineData("(pending)")]
    [InlineData("(reconnecting...)")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    public void TryGetSample_NonNumericValues_AreRejected(string raw)
    {
        var node = Node(value: raw, rawValue: raw);

        Assert.False(ScopeView.TryGetSample(node, out _));
    }

    [Fact]
    public void TryParseValue_UsesInvariantDecimalSeparator()
    {
        // RawValue is culture-invariant ('.' decimal separator); parsing must
        // match regardless of the ambient culture.
        Assert.True(ScopeView.TryParseValue("3.14", out var value));
        Assert.Equal(3.14f, value, precision: 4);
    }
}
