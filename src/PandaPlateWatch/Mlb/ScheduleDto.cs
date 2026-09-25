using System.Text.Json.Serialization;

namespace PandaPlateWatch.Mlb;

// Only the handful of fields we actually read. The MLB payload is much wider;
// unmapped properties are ignored.

public sealed class ScheduleResponse
{
    [JsonPropertyName("dates")]
    public List<ScheduleDate>? Dates { get; set; }
}

public sealed class ScheduleDate
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("games")]
    public List<ScheduleGame>? Games { get; set; }
}

public sealed class ScheduleGame
{
    [JsonPropertyName("gamePk")]
    public long GamePk { get; set; }

    [JsonPropertyName("officialDate")]
    public string? OfficialDate { get; set; }

    [JsonPropertyName("gameType")]
    public string? GameType { get; set; }

    [JsonPropertyName("status")]
    public GameStatus? Status { get; set; }

    [JsonPropertyName("teams")]
    public GameTeams? Teams { get; set; }

    [JsonPropertyName("venue")]
    public NamedEntity? Venue { get; set; }
}

public sealed class GameStatus
{
    [JsonPropertyName("abstractGameState")]
    public string? AbstractGameState { get; set; }

    [JsonPropertyName("detailedState")]
    public string? DetailedState { get; set; }
}

public sealed class GameTeams
{
    [JsonPropertyName("home")]
    public TeamSide? Home { get; set; }

    [JsonPropertyName("away")]
    public TeamSide? Away { get; set; }
}

public sealed class TeamSide
{
    [JsonPropertyName("team")]
    public NamedEntity? Team { get; set; }

    [JsonPropertyName("score")]
    public int? Score { get; set; }

    [JsonPropertyName("isWinner")]
    public bool? IsWinner { get; set; }
}

public sealed class NamedEntity
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

[JsonSerializable(typeof(ScheduleResponse))]
internal sealed partial class ScheduleJsonContext : JsonSerializerContext;
