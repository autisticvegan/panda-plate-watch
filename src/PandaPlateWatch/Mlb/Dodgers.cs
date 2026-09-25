using System.Globalization;

namespace PandaPlateWatch.Mlb;

/// <summary>The rules that decide whether a game unlocked the promotion.</summary>
public static class Dodgers
{
    public const int TeamId = 119;
    public const int DodgerStadiumVenueId = 22;

    /// <summary>
    /// Regular season plus the four postseason rounds. Excludes spring training
    /// ("S"), exhibition ("E") and the All-Star game ("A") — not real home games.
    /// </summary>
    public static readonly IReadOnlySet<string> QualifyingGameTypes =
        new HashSet<string>(StringComparer.Ordinal) { "R", "F", "D", "L", "W" };

    /// <summary>
    /// Completed home games the team won, in schedule order. A doubleheader can
    /// produce two; splitting the promotion hair over which one counts isn't ours
    /// to make, so callers just check whether the list is empty.
    /// </summary>
    public static IReadOnlyList<Game> HomeWins(
        ScheduleResponse? payload,
        int teamId = TeamId,
        int? venueId = DodgerStadiumVenueId)
    {
        var found = new List<Game>();
        foreach (var date in payload?.Dates ?? [])
        {
            foreach (var raw in date.Games ?? [])
            {
                if (IsHomeWin(raw, teamId, venueId))
                {
                    found.Add(Flatten(raw));
                }
            }
        }

        return found;
    }

    private static bool IsHomeWin(ScheduleGame raw, int teamId, int? venueId)
    {
        if (raw.GameType is null || !QualifyingGameTypes.Contains(raw.GameType))
        {
            return false;
        }

        if (raw.Status?.AbstractGameState != "Final")
        {
            return false;
        }

        var home = raw.Teams?.Home;
        if (home?.Team?.Id != teamId)
        {
            return false;
        }

        // Guard against neutral-site "home" games (Mexico/Tokyo/Little League
        // Classic). The restaurant promotion is tied to Dodger Stadium.
        if (venueId is int expected && raw.Venue?.Id != expected)
        {
            return false;
        }

        if (home.IsWinner is bool won)
        {
            return won;
        }

        // Older/partial payloads omit isWinner; fall back to the score.
        return raw.Teams?.Away?.Score is int awayScore
               && home.Score is int homeScore
               && homeScore > awayScore;
    }

    private static Game Flatten(ScheduleGame raw)
    {
        var home = raw.Teams?.Home;
        var away = raw.Teams?.Away;
        return new Game(
            GamePk: raw.GamePk,
            OfficialDate: ParseDate(raw.OfficialDate),
            GameType: raw.GameType ?? "",
            Venue: raw.Venue?.Name ?? "unknown venue",
            Opponent: away?.Team?.Name ?? "unknown opponent",
            HomeScore: home?.Score,
            AwayScore: away?.Score,
            State: raw.Status?.DetailedState ?? "");
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
}
