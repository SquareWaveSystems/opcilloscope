using Opcilloscope.App.Dialogs;

namespace Opcilloscope.Tests.App;

public class SaveConfigDialogTests
{
    [Fact]
    public void GetNormalizedFilePath_TrimsBeforeAddingExtension()
    {
        var path = SaveConfigDialog.GetNormalizedFilePath(" /tmp/configs ", " production ");

        Assert.Equal(Path.Combine("/tmp/configs", "production.cfg"), path);
    }

    [Fact]
    public void GetNormalizedFilePath_UsesTheSameExistingTargetForWhitespaceVariant()
    {
        var canonical = SaveConfigDialog.GetNormalizedFilePath("/tmp/configs", "production.cfg");
        var whitespace = SaveConfigDialog.GetNormalizedFilePath(" /tmp/configs ", " production.cfg ");

        Assert.Equal(canonical, whitespace);
    }
}
