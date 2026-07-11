using Opc.Ua;
using Opcilloscope.App.Dialogs;

namespace Opcilloscope.Tests.App;

public class WriteValueDialogTests
{
    [Fact]
    public void NormalizeInput_String_PreservesSignificantWhitespace()
    {
        Assert.Equal("  value  ", WriteValueDialog.NormalizeInput("  value  ", BuiltInType.String));
    }

    [Fact]
    public void NormalizeInput_String_AllowsEmptyValue()
    {
        Assert.Equal(string.Empty, WriteValueDialog.NormalizeInput(string.Empty, BuiltInType.String));
    }

    [Fact]
    public void NormalizeInput_Numeric_TrimsWhitespace()
    {
        Assert.Equal("42", WriteValueDialog.NormalizeInput(" 42 ", BuiltInType.Int32));
    }
}
