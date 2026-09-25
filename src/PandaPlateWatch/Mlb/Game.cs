using System.Globalization;

namespace PandaPlateWatch.Mlb;

/// <summary>A completed game, flattened from the schedule payload.</summary>
public sealed record Game(
    long GamePk,
    DateOnly? OfficialDate,
    string GameType,
    string Venue,
    string Opponent,
    int? HomeScore,
    int? AwayScore,
    string State)
{
    public string ScoreLine =>
        HomeScore is int home && AwayScore is int away
            ? string.Create(CultureInfo.InvariantCulture, $"{home}-{away}")
            : "final";
}
