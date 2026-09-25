using System.Globalization;
using System.Net;
using PandaPlateWatch.Mlb;

namespace PandaPlateWatch.Tests;

public class MlbScheduleClientTests
{
    [Fact]
    public async Task RequestsTheRightSingleDayWindow()
    {
        var stub = StubHandler.Ok(Fixtures.RawJson("home_win"));
        var client = new MlbScheduleClient(stub.Client());

        await client.FetchScheduleAsync(new DateOnly(2026, 9, 20));

        var url = stub.LastRequest.RequestUri!.ToString();
        Assert.Contains("teamId=119", url, StringComparison.Ordinal);
        Assert.Contains("startDate=2026-09-20", url, StringComparison.Ordinal);
        Assert.Contains("endDate=2026-09-20", url, StringComparison.Ordinal);
        Assert.Equal(HttpMethod.Get, stub.LastRequest.Method);
    }

    [Fact]
    public async Task ParsesTheRecordedPayload()
    {
        var client = new MlbScheduleClient(StubHandler.Ok(Fixtures.RawJson("home_win")).Client());

        var payload = await client.FetchScheduleAsync(new DateOnly(2026, 9, 20));

        Assert.Single(Dodgers.HomeWins(payload));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task NonSuccessStatusBecomesMlbException(HttpStatusCode code)
    {
        var client = new MlbScheduleClient(StubHandler.Status(code).Client());

        var ex = await Assert.ThrowsAsync<MlbException>(
            () => client.FetchScheduleAsync(new DateOnly(2026, 9, 20)));
        Assert.Contains(((int)code).ToString(CultureInfo.InvariantCulture), ex.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransportFailureBecomesMlbException()
    {
        var stub = StubHandler.Throws(new HttpRequestException("no route to host"));
        var client = new MlbScheduleClient(stub.Client());

        await Assert.ThrowsAsync<MlbException>(
            () => client.FetchScheduleAsync(new DateOnly(2026, 9, 20)));
    }

    [Fact]
    public async Task MalformedJsonBecomesMlbException()
    {
        var client = new MlbScheduleClient(StubHandler.Ok("{not json").Client());

        await Assert.ThrowsAsync<MlbException>(
            () => client.FetchScheduleAsync(new DateOnly(2026, 9, 20)));
    }
}
