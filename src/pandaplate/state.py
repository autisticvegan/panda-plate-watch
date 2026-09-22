"""Remember which game days we already alerted on, so re-runs stay quiet."""

from __future__ import annotations

import json
from pathlib import Path


def already_notified(path: Path | None, game_date: str) -> bool:
    if path is None:
        return False
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (FileNotFoundError, ValueError):
        return False
    return game_date in (data.get("notified") or [])


def record(path: Path | None, game_date: str, *, keep: int = 30) -> None:
    if path is None:
        return
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (FileNotFoundError, ValueError):
        data = {}
    notified = [d for d in (data.get("notified") or []) if d != game_date]
    notified.append(game_date)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps({"notified": notified[-keep:]}, indent=2) + "\n", encoding="utf-8"
    )
