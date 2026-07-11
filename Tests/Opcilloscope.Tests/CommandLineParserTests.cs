namespace Opcilloscope.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void Parse_ConfigOptionWithoutValue_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(["--config"]));

        Assert.Contains("requires a value", error.Message);
    }

    [Fact]
    public void Parse_OptionLookingConfigValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(["--config", "--insecure"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOptionValue_Throws(string value)
    {
        Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(["--config", value]));
    }

    [Fact]
    public void Parse_UnknownOption_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(["--wat"]));
    }

    [Theory]
    [InlineData("settings.cfg")]
    [InlineData("settings.opcilloscope")]
    [InlineData("settings.json")]
    public void Parse_DirectConfigPath_AcceptsEverySupportedExtension(string path)
    {
        var options = CommandLineParser.Parse([path]);

        Assert.Equal(path, options.ConfigPath);
    }

    [Fact]
    public void Parse_UnexpectedPositionalArgument_Throws()
    {
        Assert.Throws<ArgumentException>(() => CommandLineParser.Parse(["notes.txt"]));
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_Help_ShortCircuitsTrailingInvalidArguments(string help)
    {
        var options = CommandLineParser.Parse([help, "--wat", "--config"]);

        Assert.True(options.ShowHelp);
        Assert.Null(options.ConfigPath);
        Assert.Null(options.AutoConnectUrl);
    }

    [Fact]
    public void Parse_AllPositiveOptions_ArePreserved()
    {
        var options = CommandLineParser.Parse(
            ["--config", "settings.cfg", "--connect", "opc.tcp://server:4840", "--insecure"]);

        Assert.Equal("settings.cfg", options.ConfigPath);
        Assert.Equal("opc.tcp://server:4840", options.AutoConnectUrl);
        Assert.True(options.AllowInsecureCertificates);
        Assert.False(options.ShowHelp);
    }
}
