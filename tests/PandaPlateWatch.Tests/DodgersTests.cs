using PandaPlateWatch.Mlb;

namespace PandaPlateWatch.Tests;

public class DodgersTests
{
    [Fact]
    public void HomeWinIsDetected()
    {
        var wins = Dodgers.HomeWins(Fixtures.Load("home_win"));

        var game = Assert.Single(wins);
        Assert.Equal("San Francisco Giants", game.Opponent);
        Assert.Equal("3-1", game.ScoreLine);
        Assert.Equal(new DateOnly(2026, 9, 20), game.OfficialDate);
    }

    [Theory]
    [InlineData("home_loss")]
    [InlineData("road_win")]
    [InlineData("no_game")]
    public void NonQualifyingDaysProduceNothing(string fixture) =>
        Assert.Empty(Dodgers.HomeWins(Fixtures.Load(fixture)));

    [Fact]
    public void GameInProgressDoesNotCount()
    {
        var payload = Fixtures.Load("home_win");
        payload.FirstGame().Status = new GameStatus
        {
            AbstractGameState = "Live",
            DetailedState = "In Progress",
        };

        Assert.Empty(Dodgers.HomeWins(payload));
    }

    [Theory]
    [InlineData("S")] // spring training
    [InlineData("E")] // exhibition
    [InlineData("A")] // all-star
    public void ExhibitionGameTypesDoNotCount(string gameType)
    {
        var payload = Fixtures.Load("home_win");
        payload.FirstGame().GameType = gameType;

        Assert.Empty(Dodgers.HomeWins(payload));
    }

    [Theory]
    [InlineData("R")]
    [InlineData("F")]
    [InlineData("D")]
    [InlineData("L")]
    [InlineData("W")]
    public void RegularAndPostseasonHomeWinsCount(string gameType)
    {
        var payload = Fixtures.Load("home_win");
        payload.FirstGame().GameType = gameType;

        Assert.Single(Dodgers.HomeWins(payload));
    }

    [Fact]
    public void NeutralSiteHomeGameDoesNotCount()
    {
        var payload = Fixtures.Load("home_win");
        payload.FirstGame().Venue = new NamedEntity { Id = 5000, Name = "Tokyo Dome" };

        Assert.Empty(Dodgers.HomeWins(payload));
        // ...unless the caller opts out of the venue check.
        Assert.Single(Dodgers.HomeWins(payload, venueId: null));
    }

    [Fact]
    public void FallsBackToScoreWhenIsWinnerMissing()
    {
        var payload = Fixtures.Load("home_win");
        var teams = payload.FirstGame().Teams!;
        teams.Home!.IsWinner = null;
        teams.Away!.IsWinner = null;

        Assert.Single(Dodgers.HomeWins(payload));
    }

    [Fact]
    public void WithoutIsWinnerOrScoreItIsNotAWin()
    {
        var payload = Fixtures.Load("home_win");
        var teams = payload.FirstGame().Teams!;
        teams.Home!.IsWinner = null;
        teams.Home.Score = null;
        teams.Away!.IsWinner = null;
        teams.Away.Score = null;

        Assert.Empty(Dodgers.HomeWins(payload));
    }

    [Fact]
    public void DoubleheaderSweepReturnsBoth()
    {
        var payload = Fixtures.Load("home_win");
        var second = Fixtures.Load("home_win").FirstGame();
        second.GamePk += 1;
        payload.Dates![0].Games!.Add(second);

        Assert.Equal(2, Dodgers.HomeWins(payload).Count);
    }

    [Fact]
    public void EmptyPayloadsAreSafe()
    {
        Assert.Empty(Dodgers.HomeWins(null));
        Assert.Empty(Dodgers.HomeWins(new ScheduleResponse()));
        Assert.Empty(Dodgers.HomeWins(new ScheduleResponse
        {
            Dates = [new ScheduleDate { Games = [new ScheduleGame()] }],
        }));
    }

    [Fact]
    public void ScoreLineDegradesGracefully() =>
        Assert.Equal("final",
            new Game(1, null, "R", "v", "o", null, null, "Final").ScoreLine);
}
