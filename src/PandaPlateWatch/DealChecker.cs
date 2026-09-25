using System.Globalization;
using PandaPlateWatch.Mlb;
using PandaPlateWatch.Ntfy;

namespace PandaPlateWatch;

public sealed class DealChecker(
    IMlbScheduleClient mlb,
    INtfyPublisher ntfy,
    IStateStore state,
    AppConfig config,
    TextWriter output,
    TextWriter error)
{
    public const int ExitOk = 0;
    public const int ExitError = 1;

    public async Task<int> RunAsync(CommandLineOptions options, DateOnly gameDay,
        CancellationToken ct = default)
    {
        var dealDay = gameDay.AddDays(1);

        ScheduleResponse payload;
        try
        {
            payload = await mlb.FetchScheduleAsync(gameDay, ct).ConfigureAwait(false);
        }
        catch (MlbException ex)
        {
            await error.WriteLineAsync($"error: {ex.Message}").ConfigureAwait(false);
            return ExitError;
        }

        var wins = Dodgers.HomeWins(payload);
        if (wins.Count == 0)
        {
            await LogAsync(options,
                $"no Dodgers home win on {Iso(gameDay)} — no deal today").ConfigureAwait(false);
            return ExitOk;
        }

        var game = wins[0];
        var key = game.OfficialDate is DateOnly played ? Iso(played) : Iso(gameDay);
        if (state.AlreadyNotified(key))
        {
            await LogAsync(options, $"already notified for {key}").ConfigureAwait(false);
            return ExitOk;
        }

        var (title, message) = BuildMessage(game, config, dealDay);

        if (options.DryRun)
        {
            await output.WriteLineAsync(
                $"[dry run] would notify topic '{config.Topic}'").ConfigureAwait(false);
            await output.WriteLineAsync($"  {title}").ConfigureAwait(false);
            foreach (var line in message.Split('\n'))
            {
                await output.WriteLineAsync($"  {line}").ConfigureAwait(false);
            }

            return ExitOk;
        }

        try
        {
            await ntfy.PublishAsync(new Notification(
                Topic: config.Topic,
                Title: title,
                Message: message,
                Tags: config.Tags,
                Priority: config.Priority,
                Click: config.OrderUrl), ct).ConfigureAwait(false);
        }
        catch (NtfyException ex)
        {
            await error.WriteLineAsync($"error: {ex.Message}").ConfigureAwait(false);
            return ExitError;
        }

        state.Record(key);
        await LogAsync(options,
            $"notified: {title} ({game.Opponent} {game.ScoreLine})").ConfigureAwait(false);
        return ExitOk;
    }

    public static (string Title, string Message) BuildMessage(
        Game game, AppConfig config, DateOnly dealDay)
    {
        var played = game.OfficialDate ?? dealDay.AddDays(-1);
        var title = $"{config.DealPrice} Panda Plate today";
        var message =
            $"Dodgers beat the {game.Opponent} {game.ScoreLine} at home on {Pretty(played)}.\n\n"
            + $"Two-entree plate for {config.DealPrice} today ({Pretty(dealDay)}) "
            + $"with code {config.PromoCode} — online and app orders only, "
            + "while the offer lasts.";
        return (title, message);
    }

    /// <summary>The prior calendar day in Los Angeles — the promotion's own clock.</summary>
    public static DateOnly YesterdayPacific(DateTimeOffset now)
    {
        var pacific = TimeZoneInfo.FindSystemTimeZoneById(AppConfig.PacificTimeZoneId);
        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTime(now, pacific).Date).AddDays(-1);
    }

    private Task LogAsync(CommandLineOptions options, string text) =>
        options.Quiet ? Task.CompletedTask : output.WriteLineAsync(text);

    private static string Iso(DateOnly d) =>
        d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Pretty(DateOnly d) =>
        d.ToString("ddd MMM d", CultureInfo.InvariantCulture);
}
