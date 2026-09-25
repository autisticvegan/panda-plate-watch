using System.Text.Json;
using PandaPlateWatch.Mlb;

namespace PandaPlateWatch.Tests;

/// <summary>Recorded MLB Stats API responses, so the suite never touches the network.</summary>
internal static class Fixtures
{
    public static string RawJson(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", $"{name}.json"));

    /// <summary>
    /// Re-parses on every call, so a test that mutates the DTOs can't leak into the
    /// next one.
    /// </summary>
    public static ScheduleResponse Load(string name) =>
        JsonSerializer.Deserialize<ScheduleResponse>(RawJson(name))
        ?? throw new InvalidOperationException($"fixture '{name}' did not deserialize");

    public static ScheduleGame FirstGame(this ScheduleResponse response) =>
        response.Dates?[0].Games?[0]
        ?? throw new InvalidOperationException("fixture has no games");
}
