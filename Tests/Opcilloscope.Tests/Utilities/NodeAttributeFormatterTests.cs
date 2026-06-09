using Opc.Ua;
using Opcilloscope.Utilities;

namespace Opcilloscope.Tests.Utilities;

/// <summary>
/// Pure unit tests for <see cref="NodeAttributeFormatter"/>. These build attribute
/// dictionaries and assert on the rendered, human-readable output. No OPC server or
/// Terminal.Gui loop is required.
/// </summary>
public class NodeAttributeFormatterTests
{
    // ── Header & sections ──

    [Fact]
    public void Format_AlwaysIncludesHeaderAndIdentitySection()
    {
        var output = NodeAttributeFormatter.Format(new Dictionary<string, object?>());

        Assert.Contains("OPC UA Node Attributes", output);
        Assert.Contains("── Identity ──", output);
        Assert.Contains("Copied:", output);
    }

    [Fact]
    public void Format_OmitsSectionsWhenNoKeysPresent()
    {
        var output = NodeAttributeFormatter.Format(new Dictionary<string, object?>());

        Assert.DoesNotContain("── Access ──", output);
        Assert.DoesNotContain("── Value ──", output);
        Assert.DoesNotContain("── Type ──", output);
        Assert.DoesNotContain("── Method ──", output);
        Assert.DoesNotContain("── Permissions ──", output);
    }

    [Fact]
    public void Format_OmitsAttributesWithNullValues()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["NodeId"] = null,
            ["DisplayName"] = new LocalizedText("Visible Node"),
        };

        var output = NodeAttributeFormatter.Format(attributes);

        // NodeId is null so its label should not be rendered as a value line.
        Assert.DoesNotContain("NodeId   ", output);
        Assert.Contains("Visible Node", output);
    }

    // ── Identity / default formatting ──

    [Fact]
    public void Format_LocalizedText_RendersTextOnly()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["DisplayName"] = new LocalizedText("Temperature Sensor"),
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("Temperature Sensor", output);
    }

    [Fact]
    public void Format_QualifiedNameBrowseName_RendersToString()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["BrowseName"] = new QualifiedName("Counter", 2),
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("2:Counter", output);
    }

    [Fact]
    public void Format_NodeId_RendersToString()
    {
        var nodeId = new NodeId("Counter", 2);
        var attributes = new Dictionary<string, object?>
        {
            ["NodeId"] = nodeId,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains(nodeId.ToString()!, output);
    }

    // ── Access section ──

    [Fact]
    public void Format_AccessLevel_RendersFlagsAndHex()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["AccessLevel"] = (byte)0x03, // Read | Write
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("── Access ──", output);
        Assert.Contains("Read | Write (0x03)", output);
    }

    [Fact]
    public void Format_AccessLevel_Zero_RendersNone()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["AccessLevel"] = (byte)0x00,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("None (0x00)", output);
    }

    [Fact]
    public void Format_AccessLevelEx_RendersExtendedFlags()
    {
        var attributes = new Dictionary<string, object?>
        {
            // Read | NonatomicRead
            ["AccessLevelEx"] = (uint)0x101,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("Read | NonatomicRead (0x00000101)", output);
    }

    [Fact]
    public void Format_WriteMask_Zero_RendersNone()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["WriteMask"] = (uint)0,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("None (0)", output);
    }

    [Fact]
    public void Format_WriteMask_RendersFlags()
    {
        var attributes = new Dictionary<string, object?>
        {
            // AccessLevel (0x01) | DataType (0x10)
            ["WriteMask"] = (uint)0x11,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("AccessLevel | DataType (0x11)", output);
    }

    [Fact]
    public void Format_EventNotifier_RendersFlags()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["EventNotifier"] = (byte)0x01, // SubscribeToEvents
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("SubscribeToEvents (0x01)", output);
    }

    // ── Value section ──

    [Fact]
    public void Format_Value_StringIsQuoted()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = "hello",
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("── Value ──", output);
        Assert.Contains("\"hello\"", output);
    }

    [Fact]
    public void Format_Value_DataValueWithBadStatus_RendersBadCode()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = new DataValue { StatusCode = new StatusCode(StatusCodes.BadNodeIdUnknown) },
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains($"(bad: 0x{StatusCodes.BadNodeIdUnknown:X8})", output);
    }

    [Fact]
    public void Format_Value_DataValueWithGoodStatus_UnwrapsValue()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = new DataValue(new Variant(42)),
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("42", output);
    }

    [Fact]
    public void Format_Value_ByteArray_RendersByteCount()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = new byte[] { 1, 2, 3, 4 },
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("[4 bytes]", output);
    }

    [Fact]
    public void Format_Value_SmallArray_RendersElements()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = new int[] { 1, 2, 3 },
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("[1, 2, 3]", output);
    }

    [Fact]
    public void Format_Value_LargeArray_RendersElementCount()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["Value"] = new int[] { 1, 2, 3, 4, 5, 6, 7 },
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("[7 elements]", output);
    }

    [Fact]
    public void Format_ValueRank_RendersNamedRank()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["ValueRank"] = -1,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("Scalar (-1)", output);
    }

    [Fact]
    public void Format_ArrayDimensions_RendersDimensions()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["ArrayDimensions"] = new uint[] { 3, 4 },
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("[3, 4]", output);
    }

    [Fact]
    public void Format_MinimumSamplingInterval_AppendsMilliseconds()
    {
        var attributes = new Dictionary<string, object?>
        {
            ["MinimumSamplingInterval"] = 500.0,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("500 ms", output);
    }

    // ── Permissions section ──

    [Fact]
    public void Format_AccessRestrictions_RendersFlagsAndHex()
    {
        var attributes = new Dictionary<string, object?>
        {
            // SigningRequired (0x01) | EncryptionRequired (0x02)
            ["AccessRestrictions"] = (ushort)0x03,
        };

        var output = NodeAttributeFormatter.Format(attributes);

        Assert.Contains("── Permissions ──", output);
        Assert.Contains("SigningRequired | EncryptionRequired (0x0003)", output);
    }
}
