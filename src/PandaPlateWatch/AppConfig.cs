using System.Globalization;
using PandaPlateWatch.Ntfy;

namespace PandaPlateWatch;

public sealed class ConfigException(string message) : Exception(message);

public sealed record AppConfig
{
    public required string Topic { get; init; }
    public string Server { get; init; } = NtfyPublisher.DefaultServer;
    public string? Token { get; init; }
    public int Priority { get; init; } = 4;
    public IReadOnlyList<string> Tags { get; init; } = ["panda_face", "baseball"];
    public string DealPrice { get; init; } = "$7";
    public string PromoCode { get; init; } = "DODGERSWIN";
    public string OrderUrl { get; init; } = "https://www.pandaexpress.com/";

    public const string PacificTimeZoneId = "America/Los_Angeles";

    public static AppConfig FromEnvironment(IReadOnlyDictionary<string, string?> env)
    {
        var topic = Get(env, "NTFY_TOPIC");
        if (topic is null)
        {
            throw new ConfigException(
                "NTFY_TOPIC is not set. Pick any hard-to-guess string, subscribe to it "
                + "in the ntfy app, and export it as NTFY_TOPIC.");
        }

        var rawPriority = Get(env, "NTFY_PRIORITY") ?? "4";
        if (!int.TryParse(rawPriority, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var priority) || priority is < 1 or > 5)
        {
            throw new ConfigException($"NTFY_PRIORITY must be 1-5, got '{rawPriority}'");
        }

        var tags = (Get(env, "NTFY_TAGS") ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var defaults = new AppConfig { Topic = topic };

        return new AppConfig
        {
            Topic = topic,
            Server = Get(env, "NTFY_SERVER")?.TrimEnd('/') ?? NtfyPublisher.DefaultServer,
            Token = Get(env, "NTFY_TOKEN"),
            Priority = priority,
            Tags = tags.Length > 0 ? tags : defaults.Tags,
            DealPrice = Get(env, "PANDA_DEAL_PRICE") ?? defaults.DealPrice,
            PromoCode = Get(env, "PANDA_PROMO_CODE") ?? defaults.PromoCode,
        };
    }

    public static AppConfig FromEnvironment()
    {
        var env = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in
                 Environment.GetEnvironmentVariables())
        {
            env[(string)entry.Key] = entry.Value as string;
        }

        return FromEnvironment(env);
    }

    /// <summary>
    /// Set-but-empty is treated as unset: GitHub Actions passes undefined
    /// repository variables through to the step as empty strings.
    /// </summary>
    private static string? Get(IReadOnlyDictionary<string, string?> env, string key) =>
        env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
}
