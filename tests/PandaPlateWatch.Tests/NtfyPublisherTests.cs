using System.Net;
using System.Text.Json;
using PandaPlateWatch.Ntfy;

namespace PandaPlateWatch.Tests;

public class NtfyPublisherTests
{
    private static Notification Sample(string topic = "my-topic") => new(
        Topic: topic,
        Title: "Deal",
        Message: "Dodgers won — \U0001f43c",
        Tags: ["panda_face"],
        Priority: 5,
        Click: "https://example.test/");

    [Fact]
    public async Task PostsJsonBody()
    {
        var stub = StubHandler.Ok();
        var publisher = new NtfyPublisher(stub.Client(), NtfyPublisher.DefaultServer, "tk_secret");

        await publisher.PublishAsync(Sample());

        Assert.Equal(HttpMethod.Post, stub.LastRequest.Method);
        Assert.Equal("https://ntfy.sh/", stub.LastRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", stub.LastRequest.Headers.Authorization!.Scheme);
        Assert.Equal("tk_secret", stub.LastRequest.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(stub.LastBody);
        var root = body.RootElement;
        Assert.Equal("my-topic", root.GetProperty("topic").GetString());
        Assert.Equal("Deal", root.GetProperty("title").GetString());
        // Non-ASCII survives the round trip — the reason we use the JSON endpoint.
        Assert.Equal("Dodgers won — \U0001f43c", root.GetProperty("message").GetString());
        Assert.Equal(5, root.GetProperty("priority").GetInt32());
        Assert.Equal("panda_face", root.GetProperty("tags")[0].GetString());
        Assert.Equal("https://example.test/", root.GetProperty("click").GetString());
    }

    [Fact]
    public async Task OmitsAuthHeaderWithoutToken()
    {
        var stub = StubHandler.Ok();
        var publisher = new NtfyPublisher(stub.Client(), NtfyPublisher.DefaultServer);

        await publisher.PublishAsync(Sample());

        Assert.Null(stub.LastRequest.Headers.Authorization);
    }

    [Fact]
    public async Task OmitsEmptyOptionalFields()
    {
        var stub = StubHandler.Ok();
        var publisher = new NtfyPublisher(stub.Client(), NtfyPublisher.DefaultServer);

        await publisher.PublishAsync(new Notification("t", "a", "b"));

        using var body = JsonDocument.Parse(stub.LastBody);
        Assert.False(body.RootElement.TryGetProperty("tags", out _));
        Assert.False(body.RootElement.TryGetProperty("click", out _));
    }

    [Fact]
    public async Task HonoursCustomServer()
    {
        var stub = StubHandler.Ok();
        var publisher = new NtfyPublisher(stub.Client(), "https://ntfy.example/");

        await publisher.PublishAsync(Sample());

        Assert.Equal("https://ntfy.example/", stub.LastRequest.RequestUri!.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RequiresATopic(string topic)
    {
        var publisher = new NtfyPublisher(StubHandler.Ok().Client(), NtfyPublisher.DefaultServer);

        await Assert.ThrowsAsync<NtfyException>(() => publisher.PublishAsync(Sample(topic)));
    }

    [Fact]
    public async Task NonSuccessStatusBecomesNtfyException()
    {
        var publisher = new NtfyPublisher(
            StubHandler.Status(HttpStatusCode.TooManyRequests).Client(),
            NtfyPublisher.DefaultServer);

        var ex = await Assert.ThrowsAsync<NtfyException>(() => publisher.PublishAsync(Sample()));
        Assert.Contains("429", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransportFailureBecomesNtfyException()
    {
        var publisher = new NtfyPublisher(
            StubHandler.Throws(new HttpRequestException("dns")).Client(),
            NtfyPublisher.DefaultServer);

        await Assert.ThrowsAsync<NtfyException>(() => publisher.PublishAsync(Sample()));
    }
}
