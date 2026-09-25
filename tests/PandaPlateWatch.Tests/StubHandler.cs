using System.Net;

namespace PandaPlateWatch.Tests;

/// <summary>An HttpMessageHandler that records requests and replays canned responses.</summary>
internal sealed class StubHandler(
    Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    public HttpRequestMessage LastRequest => Requests[^1];
    public string LastBody => Bodies[^1];

    public static StubHandler Ok(string body = "{}") => new(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });

    public static StubHandler Status(HttpStatusCode code) => new(_ =>
        new HttpResponseMessage(code));

    public static StubHandler Throws(Exception ex) => new(_ => throw ex);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken));
        return respond(request);
    }

    public HttpClient Client() => new(this) { Timeout = TimeSpan.FromSeconds(5) };
}
