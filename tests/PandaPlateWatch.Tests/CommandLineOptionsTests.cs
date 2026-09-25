namespace PandaPlateWatch.Tests;

public class CommandLineOptionsTests
{
    [Fact]
    public void DefaultsAreAllOff()
    {
        var options = CommandLineOptions.Parse([]);

        Assert.Null(options.Date);
        Assert.Null(options.StateFile);
        Assert.False(options.DryRun);
        Assert.False(options.Quiet);
        Assert.False(options.ShowHelp);
    }

    [Fact]
    public void ParsesEveryFlag()
    {
        var options = CommandLineOptions.Parse(
            ["--date", "2026-09-20", "--dry-run", "--state-file", "s.json", "--quiet"]);

        Assert.Equal(new DateOnly(2026, 9, 20), options.Date);
        Assert.True(options.DryRun);
        Assert.Equal("s.json", options.StateFile);
        Assert.True(options.Quiet);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void HelpFlags(string flag) =>
        Assert.True(CommandLineOptions.Parse([flag]).ShowHelp);

    [Theory]
    [InlineData("2026-9-20")]
    [InlineData("09/20/2026")]
    [InlineData("yesterday")]
    [InlineData("2026-13-01")]
    public void BadDatesAreRejected(string value) =>
        Assert.Throws<ConfigException>(() => CommandLineOptions.Parse(["--date", value]));

    [Theory]
    [InlineData("--date")]
    [InlineData("--state-file")]
    public void FlagsThatNeedAValueSaySo(string flag)
    {
        var ex = Assert.Throws<ConfigException>(() => CommandLineOptions.Parse([flag]));
        Assert.Contains(flag, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownArgumentIsRejected()
    {
        var ex = Assert.Throws<ConfigException>(() => CommandLineOptions.Parse(["--nope"]));
        Assert.Contains("--nope", ex.Message, StringComparison.Ordinal);
    }
}
