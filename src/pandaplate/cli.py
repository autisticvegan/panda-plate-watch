"""Check yesterday's Dodgers home game and notify if the Panda deal is live."""

from __future__ import annotations

import argparse
import sys
from datetime import date, datetime, timedelta
from pathlib import Path
from zoneinfo import ZoneInfo

from . import mlb, notify
from .config import PACIFIC, Config, ConfigError
from .state import already_notified, record

EXIT_OK = 0
EXIT_ERROR = 1


def yesterday_pacific(now: datetime | None = None) -> date:
    """The prior calendar day in Los Angeles — the promotion's own clock."""
    now = now or datetime.now(ZoneInfo(PACIFIC))
    return now.astimezone(ZoneInfo(PACIFIC)).date() - timedelta(days=1)


def build_message(game: mlb.Game, config: Config, deal_day: date) -> tuple[str, str]:
    played = date.fromisoformat(game.official_date) if game.official_date else deal_day
    title = f"{config.deal_price} Panda Plate today"
    message = (
        f"Dodgers beat the {game.opponent} {game.score_line} at home "
        f"on {played:%a %b %-d}.\n\n"
        f"Two-entree plate for {config.deal_price} today "
        f"({deal_day:%a %b %-d}) with code {config.promo_code} — "
        f"online and app orders only, while the offer lasts."
    )
    return title, message


def run(argv: list[str] | None = None) -> int:
    args = _parse_args(argv)

    try:
        config = Config.from_env()
    except ConfigError as exc:
        if args.dry_run:
            config = Config(topic="dry-run")
        else:
            print(f"error: {exc}", file=sys.stderr)
            return EXIT_ERROR

    game_day = args.date or yesterday_pacific()
    deal_day = game_day + timedelta(days=1)
    state_path = Path(args.state_file).expanduser() if args.state_file else None

    try:
        payload = mlb.fetch_schedule(game_day)
    except mlb.MLBError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return EXIT_ERROR

    wins = mlb.home_wins(payload)
    if not wins:
        _log(args, f"no Dodgers home win on {game_day.isoformat()} — no deal today")
        return EXIT_OK

    game = wins[0]
    if already_notified(state_path, game.official_date):
        _log(args, f"already notified for {game.official_date}")
        return EXIT_OK

    title, message = build_message(game, config, deal_day)

    if args.dry_run:
        print(f"[dry run] would notify topic {config.topic!r}")
        print(f"  {title}")
        for line in message.splitlines():
            print(f"  {line}")
        return EXIT_OK

    try:
        notify.publish(
            topic=config.topic,
            title=title,
            message=message,
            server=config.server,
            tags=config.tags,
            priority=config.priority,
            click=config.order_url,
            token=config.token,
        )
    except notify.NotifyError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return EXIT_ERROR

    record(state_path, game.official_date)
    _log(args, f"notified: {title} ({game.opponent} {game.score_line})")
    return EXIT_OK


def _log(args: argparse.Namespace, text: str) -> None:
    if not args.quiet:
        print(text)


def _parse_args(argv: list[str] | None) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="panda-plate-watch",
        description=(
            "Notify an ntfy.sh topic when the Dodgers won yesterday's home game, "
            "which is when Panda Express runs its discounted two-entree plate."
        ),
    )
    parser.add_argument(
        "--date",
        type=date.fromisoformat,
        metavar="YYYY-MM-DD",
        help="game day to check (default: yesterday, Pacific time)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="print the notification instead of sending it",
    )
    parser.add_argument(
        "--state-file",
        metavar="PATH",
        help="JSON file used to suppress duplicate alerts for the same game day",
    )
    parser.add_argument("--quiet", action="store_true", help="only print errors")
    return parser.parse_args(argv)


def main() -> None:
    sys.exit(run())
