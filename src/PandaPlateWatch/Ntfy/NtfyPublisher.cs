using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PandaPlateWatch.Ntfy;

public sealed class NtfyException(string message, Exception? inner = null)
    : Exception(message, inner);

public sealed record Notification(
    string Topic,
    string Title,
    string Message,
    IReadOnlyList<string>? Tags = null,
    int Priority = 4,
    string? Click = null);

public interface INtfyPublisher
{
    Task PublishAsync(Notification notification, CancellationToken ct = default);
}

public sealed class NtfyPublisher(HttpClient http, string server, string? token = null)
    : INtfyPublisher
{
    public const string DefaultServer = "https://ntfy.sh";

    private readonly string _server = server.TrimEnd('/');

    /// <summary>
    /// Posts to ntfy's JSON endpoint rather than the header-based one: titles and
    /// tags here contain non-ASCII characters, which HTTP headers can't carry
    /// cleanly.
    /// </summary>
    public async Task PublishAsync(Notification notification, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notification.Topic))
        {
            throw new NtfyException("no ntfy topic configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _server)
        {
            Content = JsonContent.Create(
                NtfyRequest.From(notification), NtfyJsonContext.Default.NtfyRequest),
        };
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new NtfyException($"ntfy returned HTTP {(int)response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new NtfyException($"could not reach ntfy: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new NtfyException("ntfy timed out", ex);
        }
    }
}

internal sealed class NtfyRequest
{
    [JsonPropertyName("topic")] public required string Topic { get; init; }
    [JsonPropertyName("title")] public required string Title { get; init; }
    [JsonPropertyName("message")] public required string Message { get; init; }
    [JsonPropertyName("priority")] public int Priority { get; init; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tags { get; init; }

    [JsonPropertyName("click")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Click { get; init; }

    public static NtfyRequest From(Notification n) => new()
    {
        Topic = n.Topic,
        Title = n.Title,
        Message = n.Message,
        Priority = n.Priority,
        Tags = n.Tags is { Count: > 0 } ? n.Tags : null,
        Click = string.IsNullOrEmpty(n.Click) ? null : n.Click,
    };
}

[JsonSerializable(typeof(NtfyRequest))]
internal sealed partial class NtfyJsonContext : JsonSerializerContext;
