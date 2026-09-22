from datetime import date, datetime
from zoneinfo import ZoneInfo

import pytest

from pandaplate import cli, mlb, notify


@pytest.fixture(autouse=True)
def topic(monkeypatch):
    monkeypatch.setenv("NTFY_TOPIC", "test-topic")
    monkeypatch.delenv("NTFY_TOKEN", raising=False)
    monkeypatch.delenv("NTFY_SERVER", raising=False)


@pytest.fixture
def sent(monkeypatch):
    calls = []
    monkeypatch.setattr(notify, "publish", lambda **kw: calls.append(kw))
    monkeypatch.setattr(cli.notify, "publish", lambda **kw: calls.append(kw))
    return calls


@pytest.fixture
def schedule(monkeypatch, fixture):
    def _use(name):
        monkeypatch.setattr(
            cli.mlb, "fetch_schedule", lambda *_a, **_kw: fixture(name)
        )

    return _use


def test_utc_early_morning_still_looks_at_the_pacific_prior_day():
    # 00:30 UTC on the 21st is 17:30 Pacific on the 20th, so "yesterday" is the 19th.
    now = datetime(2026, 9, 21, 0, 30, tzinfo=ZoneInfo("UTC"))
    assert cli.yesterday_pacific(now) == date(2026, 9, 19)


def test_pacific_afternoon():
    now = datetime(2026, 9, 21, 9, 0, tzinfo=ZoneInfo("America/Los_Angeles"))
    assert cli.yesterday_pacific(now) == date(2026, 9, 20)


def test_home_win_sends_notification(schedule, sent, capsys):
    schedule("home_win")
    assert cli.run(["--date", "2026-09-20"]) == cli.EXIT_OK
    assert len(sent) == 1
    call = sent[0]
    assert call["topic"] == "test-topic"
    assert "$7" in call["title"]
    assert "San Francisco Giants" in call["message"]
    assert "3-1" in call["message"]
    assert "DODGERSWIN" in call["message"]
    # The deal lands the day after the game.
    assert "Mon Sep 21" in call["message"]


def test_home_loss_sends_nothing(schedule, sent):
    schedule("home_loss")
    assert cli.run(["--date", "2026-09-01"]) == cli.EXIT_OK
    assert sent == []


def test_road_win_sends_nothing(schedule, sent):
    schedule("road_win")
    assert cli.run(["--date", "2026-09-14"]) == cli.EXIT_OK
    assert sent == []


def test_dry_run_prints_and_sends_nothing(schedule, sent, capsys):
    schedule("home_win")
    assert cli.run(["--date", "2026-09-20", "--dry-run"]) == cli.EXIT_OK
    assert sent == []
    assert "dry run" in capsys.readouterr().out


def test_dry_run_works_without_configuration(schedule, sent, monkeypatch):
    monkeypatch.delenv("NTFY_TOPIC")
    schedule("home_win")
    assert cli.run(["--date", "2026-09-20", "--dry-run"]) == cli.EXIT_OK
    assert sent == []


def test_missing_topic_is_an_error(schedule, monkeypatch):
    monkeypatch.delenv("NTFY_TOPIC")
    schedule("home_win")
    assert cli.run(["--date", "2026-09-20"]) == cli.EXIT_ERROR


def test_state_file_suppresses_a_second_run(schedule, sent, tmp_path):
    schedule("home_win")
    state = tmp_path / "state.json"
    args = ["--date", "2026-09-20", "--state-file", str(state)]
    assert cli.run(args) == cli.EXIT_OK
    assert cli.run(args) == cli.EXIT_OK
    assert len(sent) == 1


def test_state_file_still_alerts_for_a_new_game(schedule, sent, tmp_path, fixture):
    state = tmp_path / "state.json"
    schedule("home_win")
    assert cli.run(["--date", "2026-09-20", "--state-file", str(state)]) == cli.EXIT_OK

    later = fixture("home_win")
    later["dates"][0]["games"][0]["officialDate"] = "2026-09-25"
    schedule_next = lambda *_a, **_kw: later  # noqa: E731
    cli.mlb.fetch_schedule = schedule_next
    assert cli.run(["--date", "2026-09-25", "--state-file", str(state)]) == cli.EXIT_OK
    assert len(sent) == 2


def test_api_failure_is_an_error(monkeypatch, sent):
    def boom(*_a, **_kw):
        raise mlb.MLBError("down")

    monkeypatch.setattr(cli.mlb, "fetch_schedule", boom)
    assert cli.run(["--date", "2026-09-20"]) == cli.EXIT_ERROR
    assert sent == []


def test_notify_failure_is_an_error(schedule, monkeypatch, tmp_path):
    schedule("home_win")

    def boom(**_kw):
        raise notify.NotifyError("429")

    monkeypatch.setattr(cli.notify, "publish", boom)
    state = tmp_path / "state.json"
    assert (
        cli.run(["--date", "2026-09-20", "--state-file", str(state)]) == cli.EXIT_ERROR
    )
    # A failed send must not be recorded, or the retry would be skipped.
    assert not state.exists()
