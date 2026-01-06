using Opc.Ua;
using OpcScope.Utilities;

namespace OpcScope.Tests.Utilities;

public class OpcValueConverterTests
{
    #region Boolean Tests

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("on", true)]
    [InlineData("On", true)]
    public void TryConvert_Boolean_TrueValues_ReturnsTrue(string input, bool expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Boolean);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    public void TryConvert_Boolean_FalseValues_ReturnsFalse(string input, bool expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Boolean);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("2")]
    [InlineData("maybe")]
    public void TryConvert_Boolean_InvalidValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Boolean);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
    }

    #endregion

    #region Integer Tests

    [Theory]
    [InlineData("0", (sbyte)0)]
    [InlineData("-128", (sbyte)-128)]
    [InlineData("127", (sbyte)127)]
    [InlineData("  42  ", (sbyte)42)]
    public void TryConvert_SByte_ValidValues_ReturnsValue(string input, sbyte expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.SByte);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("128")]
    [InlineData("-129")]
    [InlineData("abc")]
    public void TryConvert_SByte_InvalidValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.SByte);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    [InlineData("128", (byte)128)]
    public void TryConvert_Byte_ValidValues_ReturnsValue(string input, byte expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Byte);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("-32768", (short)-32768)]
    [InlineData("32767", (short)32767)]
    [InlineData("0", (short)0)]
    public void TryConvert_Int16_ValidValues_ReturnsValue(string input, short expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Int16);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0", (ushort)0)]
    [InlineData("65535", (ushort)65535)]
    [InlineData("32768", (ushort)32768)]
    public void TryConvert_UInt16_ValidValues_ReturnsValue(string input, ushort expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.UInt16);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("-2147483648", -2147483648)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    public void TryConvert_Int32_ValidValues_ReturnsValue(string input, int expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Int32);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("4294967295", 4294967295u)]
    [InlineData("42", 42u)]
    public void TryConvert_UInt32_ValidValues_ReturnsValue(string input, uint expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.UInt32);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("-9223372036854775808", -9223372036854775808L)]
    [InlineData("9223372036854775807", 9223372036854775807L)]
    [InlineData("0", 0L)]
    public void TryConvert_Int64_ValidValues_ReturnsValue(string input, long expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Int64);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("18446744073709551615", 18446744073709551615UL)]
    [InlineData("42", 42UL)]
    public void TryConvert_UInt64_ValidValues_ReturnsValue(string input, ulong expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.UInt64);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    #endregion

    #region Float and Double Tests

    [Theory]
    [InlineData("0.0", 0.0f)]
    [InlineData("3.14", 3.14f)]
    [InlineData("-1.5", -1.5f)]
    [InlineData("  2.5  ", 2.5f)]
    public void TryConvert_Float_ValidValues_ReturnsValue(string input, float expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Float);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, (float)value!);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TryConvert_Float_SpecialValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Float);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.Contains("out of range", error);
    }

    [Theory]
    [InlineData("0.0", 0.0)]
    [InlineData("3.14159265358979", 3.14159265358979)]
    [InlineData("-1.5", -1.5)]
    public void TryConvert_Double_ValidValues_ReturnsValue(string input, double expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Double);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, (double)value!);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TryConvert_Double_SpecialValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Double);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.Contains("out of range", error);
    }

    #endregion

    #region String Tests

    [Theory]
    [InlineData("Hello World", "Hello World")]
    [InlineData("", "")]
    [InlineData("  spaces  ", "  spaces  ")]
    [InlineData("123", "123")]
    public void TryConvert_String_AllValues_ReturnsValue(string input, string expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.String);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    [Fact]
    public void TryConvert_String_EmptyString_ReturnsEmpty()
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert("", BuiltInType.String);

        // Assert
        Assert.True(success);
        Assert.Equal("", value);
        Assert.Null(error);
    }

    #endregion

    #region DateTime Tests

    [Theory]
    [InlineData("2026-01-06")]
    [InlineData("2026-01-06 12:00:00")]
    [InlineData("1/6/2026")]
    public void TryConvert_DateTime_ValidValues_ReturnsValue(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.DateTime);

        // Assert
        Assert.True(success);
        Assert.IsType<DateTime>(value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("invalid-date")]
    [InlineData("2026-13-01")]
    [InlineData("abc")]
    public void TryConvert_DateTime_InvalidValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.DateTime);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
    }

    #endregion

    #region Guid Tests

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789012")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TryConvert_Guid_ValidValues_ReturnsValue(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Guid);

        // Assert
        Assert.True(success);
        Assert.IsType<Guid>(value);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("invalid-guid")]
    [InlineData("12345678")]
    [InlineData("abc")]
    public void TryConvert_Guid_InvalidValues_ReturnsFalse(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Guid);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
    }

    #endregion

    #region Variant Tests

    [Theory]
    [InlineData("test")]
    [InlineData("123")]
    [InlineData("")]
    public void TryConvert_Variant_AllValues_ReturnsWrappedVariant(string input)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Variant);

        // Assert
        Assert.True(success);
        Assert.IsType<Variant>(value);
        Assert.Null(error);
    }

    [Fact]
    public void TryConvert_Variant_EmptyString_ReturnsEmptyVariant()
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert("", BuiltInType.Variant);

        // Assert
        Assert.True(success);
        Assert.IsType<Variant>(value);
        Assert.Null(error);
    }

    #endregion

    #region Empty String Tests

    [Theory]
    [InlineData(BuiltInType.Boolean)]
    [InlineData(BuiltInType.Int32)]
    [InlineData(BuiltInType.Double)]
    [InlineData(BuiltInType.DateTime)]
    public void TryConvert_EmptyString_NonStringTypes_ReturnsFalse(BuiltInType dataType)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert("", dataType);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.Contains("cannot be empty", error);
    }

    #endregion

    #region Unsupported Types

    [Fact]
    public void TryConvert_UnsupportedType_ReturnsFalse()
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert("test", BuiltInType.XmlElement);

        // Assert
        Assert.False(success);
        Assert.Null(value);
        Assert.NotNull(error);
        Assert.Contains("Unsupported", error);
    }

    #endregion

    #region GetInputHint Tests

    [Theory]
    [InlineData(BuiltInType.Boolean, "true/false, 1/0, yes/no")]
    [InlineData(BuiltInType.Int32, "Integer")]
    [InlineData(BuiltInType.Float, "Decimal number")]
    [InlineData(BuiltInType.String, "Text")]
    [InlineData(BuiltInType.DateTime, "Date/time")]
    [InlineData(BuiltInType.Guid, "GUID")]
    public void GetInputHint_ReturnsExpectedHint(BuiltInType dataType, string expectedHint)
    {
        // Act
        var hint = OpcValueConverter.GetInputHint(dataType);

        // Assert
        Assert.Contains(expectedHint, hint, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region IsWriteSupported Tests

    [Theory]
    [InlineData(BuiltInType.Boolean, true)]
    [InlineData(BuiltInType.Int32, true)]
    [InlineData(BuiltInType.String, true)]
    [InlineData(BuiltInType.XmlElement, false)]
    [InlineData(BuiltInType.StatusCode, false)]
    public void IsWriteSupported_ReturnsExpectedResult(BuiltInType dataType, bool expected)
    {
        // Act
        var result = OpcValueConverter.IsWriteSupported(dataType);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Whitespace Handling

    [Theory]
    [InlineData("  42  ", 42)]
    [InlineData("\t100\t", 100)]
    [InlineData("\n50\n", 50)]
    public void TryConvert_Int32_WhitespaceHandling_TrimsCorrectly(string input, int expected)
    {
        // Act
        var (success, value, error) = OpcValueConverter.TryConvert(input, BuiltInType.Int32);

        // Assert
        Assert.True(success);
        Assert.Equal(expected, value);
        Assert.Null(error);
    }

    #endregion
}
