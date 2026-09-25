using System.Globalization;

namespace PandaPlateWatch;

public sealed record CommandLineOptions
{
    public DateOnly? Date { get; init; }
    public bool DryRun { get; init; }
    public string? StateFile { get; init; }
    public bool Quiet { get; init; }
    public bool ShowHelp { get; init; }

    public const string Usage = """
        panda-plate-watch — notify an ntfy.sh topic when the Dodgers won yesterday's
        home game, which is when Panda Express runs its discounted two-entree plate.

        Usage: panda-plate-watch [options]

          --date YYYY-MM-DD   game day to check (default: yesterday, Pacific time)
          --dry-run           print the notification instead of sending it
          --state-file PATH   JSON file used to suppress duplicate alerts
          --quiet             only print errors
          -h, --help          show this help
        """;

    /// <summary>Parses argv. Throws <see cref="ConfigException"/> on bad input.</summary>
    public static CommandLineOptions Parse(string[] args)
    {
        var options = new CommandLineOptions();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--date":
                    options = options with { Date = ParseDate(Next(args, ref i, "--date")) };
                    break;
                case "--state-file":
                    options = options with { StateFile = Next(args, ref i, "--state-file") };
                    break;
                case "--dry-run":
                    options = options with { DryRun = true };
                    break;
                case "--quiet":
                    options = options with { Quiet = true };
                    break;
                case "-h" or "--help":
                    options = options with { ShowHelp = true };
                    break;
                default:
                    throw new ConfigException($"unknown argument '{args[i]}'");
            }
        }

        return options;
    }

    private static string Next(string[] args, ref int i, string flag) =>
        ++i < args.Length ? args[i] : throw new ConfigException($"{flag} needs a value");

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new ConfigException($"--date must be YYYY-MM-DD, got '{value}'");
}
