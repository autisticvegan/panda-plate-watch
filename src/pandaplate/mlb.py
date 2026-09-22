"""Read Dodgers results from the public MLB Stats API."""

from __future__ import annotations

import json
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import date

SCHEDULE_URL = "https://statsapi.mlb.com/api/v1/schedule"
DODGERS_TEAM_ID = 119
DODGER_STADIUM_VENUE_ID = 22

# Regular season plus the four postseason rounds. Excludes spring training ("S"),
# exhibition ("E") and the All-Star game ("A") — those aren't real home games.
QUALIFYING_GAME_TYPES = frozenset({"R", "F", "D", "L", "W"})

USER_AGENT = "panda-plate-watch (+https://github.com/autisticvegan/panda-plate-watch)"


class MLBError(RuntimeError):
    """The schedule could not be fetched or parsed."""


@dataclass(frozen=True)
class Game:
    game_pk: int
    official_date: str
    game_type: str
    venue: str
    opponent: str
    home_score: int | None
    away_score: int | None
    state: str

    @property
    def score_line(self) -> str:
        if self.home_score is None or self.away_score is None:
            return "final"
        return f"{self.home_score}-{self.away_score}"


def fetch_schedule(
    day: date,
    *,
    team_id: int = DODGERS_TEAM_ID,
    timeout: float = 15.0,
    opener=urllib.request.urlopen,
) -> dict:
    """Fetch one day of schedule for a team. Raises MLBError on any failure."""
    query = urllib.parse.urlencode(
        {
            "sportId": 1,
            "teamId": team_id,
            "startDate": day.isoformat(),
            "endDate": day.isoformat(),
        }
    )
    request = urllib.request.Request(
        f"{SCHEDULE_URL}?{query}", headers={"User-Agent": USER_AGENT}
    )
    try:
        with opener(request, timeout=timeout) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        raise MLBError(f"MLB API returned HTTP {exc.code}") from exc
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        raise MLBError(f"could not reach the MLB API: {exc}") from exc
    except (ValueError, json.JSONDecodeError) as exc:
        raise MLBError(f"MLB API returned malformed JSON: {exc}") from exc


def home_wins(
    payload: dict,
    *,
    team_id: int = DODGERS_TEAM_ID,
    venue_id: int | None = DODGER_STADIUM_VENUE_ID,
) -> list[Game]:
    """Completed home games the team won, in schedule order.

    A doubleheader can produce two; splitting the promotion hair over which one
    counts isn't ours to make, so callers just check whether the list is empty.
    """
    found: list[Game] = []
    for scheduled_date in payload.get("dates") or []:
        for raw in scheduled_date.get("games") or []:
            game = _parse(raw)
            if game is None:
                continue
            if not _is_home_win(raw, team_id=team_id, venue_id=venue_id):
                continue
            found.append(game)
    return found


def _is_home_win(raw: dict, *, team_id: int, venue_id: int | None) -> bool:
    if raw.get("gameType") not in QUALIFYING_GAME_TYPES:
        return False
    if (raw.get("status") or {}).get("abstractGameState") != "Final":
        return False

    teams = raw.get("teams") or {}
    home = teams.get("home") or {}
    away = teams.get("away") or {}
    if (home.get("team") or {}).get("id") != team_id:
        return False

    # Guard against neutral-site "home" games (Mexico/Tokyo/Little League
    # Classic). The restaurant promotion is tied to Dodger Stadium.
    if venue_id is not None and (raw.get("venue") or {}).get("id") != venue_id:
        return False

    if isinstance(home.get("isWinner"), bool):
        return home["isWinner"]

    # Older/partial payloads omit isWinner; fall back to the score.
    home_score, away_score = home.get("score"), away.get("score")
    if isinstance(home_score, int) and isinstance(away_score, int):
        return home_score > away_score
    return False


def _parse(raw: dict) -> Game | None:
    teams = raw.get("teams") or {}
    home = teams.get("home") or {}
    away = teams.get("away") or {}
    game_pk = raw.get("gamePk")
    if not isinstance(game_pk, int):
        return None
    return Game(
        game_pk=game_pk,
        official_date=raw.get("officialDate") or "",
        game_type=raw.get("gameType") or "",
        venue=(raw.get("venue") or {}).get("name") or "unknown venue",
        opponent=(away.get("team") or {}).get("name") or "unknown opponent",
        home_score=home.get("score") if isinstance(home.get("score"), int) else None,
        away_score=away.get("score") if isinstance(away.get("score"), int) else None,
        state=(raw.get("status") or {}).get("detailedState") or "",
    )
