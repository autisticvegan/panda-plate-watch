using PandaPlateWatch;
using PandaPlateWatch.Mlb;
using PandaPlateWatch.Ntfy;

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (ConfigException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    Console.Error.WriteLine();
    Console.Error.WriteLine(CommandLineOptions.Usage);
    return DealChecker.ExitError;
}

if (options.ShowHelp)
{
    Console.WriteLine(CommandLineOptions.Usage);
    return DealChecker.ExitOk;
}

AppConfig config;
try
{
    config = AppConfig.FromEnvironment();
}
catch (ConfigException ex)
{
    // A dry run is for eyeballing the message, so don't make it need a real topic.
    if (!options.DryRun)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return DealChecker.ExitError;
    }

    config = new AppConfig { Topic = "dry-run" };
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
http.DefaultRequestHeaders.UserAgent.ParseAdd(MlbScheduleClient.UserAgent);

var checker = new DealChecker(
    new MlbScheduleClient(http),
    new NtfyPublisher(http, config.Server, config.Token),
    options.StateFile is { Length: > 0 } path
        ? new FileStateStore(path)
        : NullStateStore.Instance,
    config,
    Console.Out,
    Console.Error);

var gameDay = options.Date ?? DealChecker.YesterdayPacific(DateTimeOffset.Now);
return await checker.RunAsync(options, gameDay);
