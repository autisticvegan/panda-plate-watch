using PandaPlateWatch.Mlb;
using PandaPlateWatch.Ntfy;

namespace PandaPlateWatch.Tests;

public class DealCheckerTests
{
    private sealed class FakeMlb(Func<DateOnly, ScheduleResponse> respond) : IMlbScheduleClient
    {
        public static FakeMlb Fixture(string name) => new(_ => Fixtures.Load(name));

        public static FakeMlb Failing() => new(_ => throw new MlbException("down"));

        public Task<ScheduleResponse> FetchScheduleAsync(
            DateOnly day, CancellationToken ct = default) => Task.FromResult(respond(day));
    }

    private sealed class FakeNtfy(bool fail = false) : INtfyPublisher
    {
        public List<Notification> Sent { get; } = [];

        public Task PublishAsync(Notification notification, CancellationToken ct = default)
        {
            if (fail)
            {
                throw new NtfyException("429");
            }

            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    private static AppConfig Config => new() { Topic = "test-topic" };

    private static (DealChecker Checker, FakeNtfy Ntfy, StringWriter Out) Build(
        IMlbScheduleClient mlb, IStateStore? state = null, FakeNtfy? ntfy = null)
    {
        ntfy ??= new FakeNtfy();
        var output = new StringWriter();
        var checker = new DealChecker(
            mlb, ntfy, state ?? NullStateStore.Instance, Config, output, output);
        return (checker, ntfy, output);
    }

    [Fact]
    public async Task HomeWinSendsNotification()
    {
        var (checker, ntfy, _) = Build(FakeMlb.Fixture("home_win"));

        var exit = await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));

        Assert.Equal(DealChecker.ExitOk, exit);
        var sent = Assert.Single(ntfy.Sent);
        Assert.Equal("test-topic", sent.Topic);
        Assert.Contains("$7", sent.Title, StringComparison.Ordinal);
        Assert.Contains("San Francisco Giants", sent.Message, StringComparison.Ordinal);
        Assert.Contains("3-1", sent.Message, StringComparison.Ordinal);
        Assert.Contains("DODGERSWIN", sent.Message, StringComparison.Ordinal);
        Assert.Contains("Sun Sep 20", sent.Message, StringComparison.Ordinal);
        // The deal lands the day after the game.
        Assert.Contains("Mon Sep 21", sent.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("home_loss")]
    [InlineData("road_win")]
    [InlineData("no_game")]
    public async Task NonQualifyingDaysSendNothing(string fixture)
    {
        var (checker, ntfy, output) = Build(FakeMlb.Fixture(fixture));

        var exit = await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 1));

        Assert.Equal(DealChecker.ExitOk, exit);
        Assert.Empty(ntfy.Sent);
        Assert.Contains("no deal today", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DryRunPrintsAndSendsNothing()
    {
        var (checker, ntfy, output) = Build(FakeMlb.Fixture("home_win"));

        var exit = await checker.RunAsync(
            new CommandLineOptions { DryRun = true }, new DateOnly(2026, 9, 20));

        Assert.Equal(DealChecker.ExitOk, exit);
        Assert.Empty(ntfy.Sent);
        Assert.Contains("dry run", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("San Francisco Giants", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuietSuppressesTheNoDealLine()
    {
        var (checker, _, output) = Build(FakeMlb.Fixture("home_loss"));

        await checker.RunAsync(new CommandLineOptions { Quiet = true }, new DateOnly(2026, 9, 1));

        Assert.Equal("", output.ToString());
    }

    [Fact]
    public async Task StateFileSuppressesASecondRun()
    {
        using var temp = new TempFile();
        var ntfy = new FakeNtfy();
        var state = new FileStateStore(temp.Path);

        var (checker, _, _) = Build(FakeMlb.Fixture("home_win"), state, ntfy);
        await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));
        await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));

        Assert.Single(ntfy.Sent);
    }

    [Fact]
    public async Task StateFileStillAlertsForANewGame()
    {
        using var temp = new TempFile();
        var ntfy = new FakeNtfy();
        var state = new FileStateStore(temp.Path);

        var (first, _, _) = Build(FakeMlb.Fixture("home_win"), state, ntfy);
        await first.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));

        var later = new FakeMlb(_ =>
        {
            var payload = Fixtures.Load("home_win");
            payload.FirstGame().OfficialDate = "2026-09-25";
            return payload;
        });
        var (second, _, _) = Build(later, state, ntfy);
        await second.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 25));

        Assert.Equal(2, ntfy.Sent.Count);
    }

    [Fact]
    public async Task ApiFailureIsAnError()
    {
        var (checker, ntfy, output) = Build(FakeMlb.Failing());

        var exit = await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));

        Assert.Equal(DealChecker.ExitError, exit);
        Assert.Empty(ntfy.Sent);
        Assert.Contains("error: down", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedSendIsNotRecorded()
    {
        using var temp = new TempFile();
        var state = new FileStateStore(temp.Path);
        var (checker, _, _) = Build(
            FakeMlb.Fixture("home_win"), state, new FakeNtfy(fail: true));

        var exit = await checker.RunAsync(new CommandLineOptions(), new DateOnly(2026, 9, 20));

        Assert.Equal(DealChecker.ExitError, exit);
        // Recording a failed send would make the retry skip the alert entirely.
        Assert.False(File.Exists(temp.Path));
    }

    [Fact]
    public void UtcEarlyMorningStillLooksAtThePacificPriorDay()
    {
        // 00:30 UTC on the 21st is 17:30 Pacific on the 20th, so "yesterday" is the 19th.
        var now = new DateTimeOffset(2026, 9, 21, 0, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 9, 19), DealChecker.YesterdayPacific(now));
    }

    [Fact]
    public void PacificMorning()
    {
        var now = new DateTimeOffset(2026, 9, 21, 9, 0, 0, TimeSpan.FromHours(-7));

        Assert.Equal(new DateOnly(2026, 9, 20), DealChecker.YesterdayPacific(now));
    }

    /// <summary>A path in the temp dir that nothing has created yet.</summary>
    private sealed class TempFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"panda-state-{Guid.NewGuid():N}.json");

        public void Dispose() => File.Delete(Path);
    }
}
