import copy

import pytest

from pandaplate import mlb


def test_home_win_is_detected(fixture):
    wins = mlb.home_wins(fixture("home_win"))
    assert len(wins) == 1
    assert wins[0].opponent == "San Francisco Giants"
    assert wins[0].score_line == "3-1"
    assert wins[0].official_date == "2026-09-20"


def test_home_loss_is_not_a_win(fixture):
    assert mlb.home_wins(fixture("home_loss")) == []


def test_road_win_does_not_count(fixture):
    assert mlb.home_wins(fixture("road_win")) == []


def test_no_game_day(fixture):
    assert mlb.home_wins(fixture("no_game")) == []


def _mutate(payload, **changes):
    payload = copy.deepcopy(payload)
    game = payload["dates"][0]["games"][0]
    for key, value in changes.items():
        game[key] = value
    return payload


def test_game_in_progress_does_not_count(fixture):
    payload = _mutate(
        fixture("home_win"),
        status={"abstractGameState": "Live", "detailedState": "In Progress"},
    )
    assert mlb.home_wins(payload) == []


def test_spring_training_does_not_count(fixture):
    assert mlb.home_wins(_mutate(fixture("home_win"), gameType="S")) == []


def test_postseason_home_win_counts(fixture):
    assert len(mlb.home_wins(_mutate(fixture("home_win"), gameType="W"))) == 1


def test_neutral_site_home_game_does_not_count(fixture):
    payload = _mutate(fixture("home_win"), venue={"id": 5000, "name": "Tokyo Dome"})
    assert mlb.home_wins(payload) == []
    # ...unless the caller opts out of the venue check.
    assert len(mlb.home_wins(payload, venue_id=None)) == 1


def test_falls_back_to_score_when_is_winner_missing(fixture):
    payload = copy.deepcopy(fixture("home_win"))
    for side in payload["dates"][0]["games"][0]["teams"].values():
        side.pop("isWinner", None)
    assert len(mlb.home_wins(payload)) == 1


def test_doubleheader_sweep_returns_both(fixture):
    payload = copy.deepcopy(fixture("home_win"))
    games = payload["dates"][0]["games"]
    second = copy.deepcopy(games[0])
    second["gamePk"] += 1
    games.append(second)
    assert len(mlb.home_wins(payload)) == 2


def test_empty_payload_is_safe():
    assert mlb.home_wins({}) == []
    assert mlb.home_wins({"dates": None}) == []
    assert mlb.home_wins({"dates": [{"games": [{}]}]}) == []


def test_fetch_wraps_transport_errors():
    def boom(*_args, **_kwargs):
        raise TimeoutError("slow")

    with pytest.raises(mlb.MLBError):
        mlb.fetch_schedule(__import__("datetime").date(2026, 9, 20), opener=boom)
