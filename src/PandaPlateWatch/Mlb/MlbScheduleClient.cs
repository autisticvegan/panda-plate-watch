using System.Globalization;
using System.Net.Http.Json;

namespace PandaPlateWatch.Mlb;

public sealed class MlbException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IMlbScheduleClient
{
    Task<ScheduleResponse> FetchScheduleAsync(DateOnly day, CancellationToken ct = default);
}

public sealed class MlbScheduleClient(HttpClient http) : IMlbScheduleClient
{
    public const string ScheduleUrl = "https://statsapi.mlb.com/api/v1/schedule";
    public const string UserAgent =
        "panda-plate-watch (+https://github.com/autisticvegan/panda-plate-watch)";

    public async Task<ScheduleResponse> FetchScheduleAsync(
        DateOnly day, CancellationToken ct = default)
    {
        var iso = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var url = $"{ScheduleUrl}?sportId=1&teamId={Dodgers.TeamId}&startDate={iso}&endDate={iso}";

        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new MlbException($"MLB API returned HTTP {(int)response.StatusCode}");
            }

            return await response.Content
                       .ReadFromJsonAsync(ScheduleJsonContext.Default.ScheduleResponse, ct)
                       .ConfigureAwait(false)
                   ?? new ScheduleResponse();
        }
        catch (HttpRequestException ex)
        {
            throw new MlbException($"could not reach the MLB API: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new MlbException("the MLB API timed out", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new MlbException($"MLB API returned malformed JSON: {ex.Message}", ex);
        }
    }
}
