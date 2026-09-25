namespace PandaPlateWatch.Tests;

public class AppConfigTests
{
    private static AppConfig From(params (string Key, string? Value)[] pairs) =>
        AppConfig.FromEnvironment(pairs.ToDictionary(p => p.Key, p => p.Value));

    [Fact]
    public void Defaults()
    {
        var config = From(("NTFY_TOPIC", "abc"));

        Assert.Equal("abc", config.Topic);
        Assert.Equal("https://ntfy.sh", config.Server);
        Assert.Null(config.Token);
        Assert.Equal(4, config.Priority);
        Assert.Equal(["panda_face", "baseball"], config.Tags);
        Assert.Equal("$7", config.DealPrice);
        Assert.Equal("DODGERSWIN", config.PromoCode);
    }

    [Fact]
    public void Overrides()
    {
        var config = From(
            ("NTFY_TOPIC", "abc"),
            ("NTFY_SERVER", "https://ntfy.example/"),
            ("NTFY_TOKEN", "tk_1"),
            ("NTFY_PRIORITY", "5"),
            ("NTFY_TAGS", "panda_face, hamburger ,"),
            ("PANDA_DEAL_PRICE", "$6"),
            ("PANDA_PROMO_CODE", "LADWIN"));

        Assert.Equal("https://ntfy.example", config.Server);
        Assert.Equal("tk_1", config.Token);
        Assert.Equal(5, config.Priority);
        Assert.Equal(["panda_face", "hamburger"], config.Tags);
        Assert.Equal("$6", config.DealPrice);
        Assert.Equal("LADWIN", config.PromoCode);
    }

    [Fact]
    public void EmptyVariablesFallBackToDefaults()
    {
        // GitHub Actions passes undefined repository variables through as empty strings.
        var config = From(
            ("NTFY_TOPIC", "abc"),
            ("NTFY_SERVER", ""),
            ("NTFY_TOKEN", ""),
            ("NTFY_PRIORITY", ""),
            ("NTFY_TAGS", ""),
            ("PANDA_DEAL_PRICE", ""),
            ("PANDA_PROMO_CODE", ""));

        Assert.Equal("https://ntfy.sh", config.Server);
        Assert.Null(config.Token);
        Assert.Equal(4, config.Priority);
        Assert.Equal(["panda_face", "baseball"], config.Tags);
        Assert.Equal("$7", config.DealPrice);
        Assert.Equal("DODGERSWIN", config.PromoCode);
    }

    [Fact]
    public void MissingTopicIsAnError()
    {
        var ex = Assert.Throws<ConfigException>(() => From());
        Assert.Contains("NTFY_TOPIC", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankTopicIsAnError(string topic)
    {
        var ex = Assert.Throws<ConfigException>(() => From(("NTFY_TOPIC", topic)));
        Assert.Contains("NTFY_TOPIC", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("high")]
    public void BadPriorityIsAnError(string priority)
    {
        var ex = Assert.Throws<ConfigException>(
            () => From(("NTFY_TOPIC", "abc"), ("NTFY_PRIORITY", priority)));
        Assert.Contains("NTFY_PRIORITY", ex.Message, StringComparison.Ordinal);
    }
}
