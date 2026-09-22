"""Environment-driven configuration."""

from __future__ import annotations

import os
from dataclasses import dataclass, field

from .notify import DEFAULT_SERVER

PACIFIC = "America/Los_Angeles"


class ConfigError(RuntimeError):
    """Required configuration is missing or invalid."""


@dataclass(frozen=True)
class Config:
    topic: str
    server: str = DEFAULT_SERVER
    token: str | None = None
    priority: int = 4
    tags: list[str] = field(default_factory=lambda: ["panda_face", "baseball"])
    deal_price: str = "$7"
    promo_code: str = "DODGERSWIN"
    order_url: str = "https://www.pandaexpress.com/"

    @classmethod
    def from_env(cls, env: dict[str, str] | None = None) -> "Config":
        env = os.environ if env is None else env

        topic = (env.get("NTFY_TOPIC") or "").strip()
        if not topic:
            raise ConfigError(
                "NTFY_TOPIC is not set. Pick any hard-to-guess string, subscribe "
                "to it in the ntfy app, and export it as NTFY_TOPIC."
            )

        raw_priority = (env.get("NTFY_PRIORITY") or "4").strip()
        try:
            priority = int(raw_priority)
        except ValueError:
            raise ConfigError(f"NTFY_PRIORITY must be 1-5, got {raw_priority!r}") from None
        if not 1 <= priority <= 5:
            raise ConfigError(f"NTFY_PRIORITY must be 1-5, got {priority}")

        raw_tags = (env.get("NTFY_TAGS") or "").strip()
        tags = [t.strip() for t in raw_tags.split(",") if t.strip()] or None

        return cls(
            topic=topic,
            server=(env.get("NTFY_SERVER") or DEFAULT_SERVER).strip().rstrip("/"),
            token=(env.get("NTFY_TOKEN") or "").strip() or None,
            priority=priority,
            **({"tags": tags} if tags else {}),
            deal_price=(env.get("PANDA_DEAL_PRICE") or "$7").strip(),
            promo_code=(env.get("PANDA_PROMO_CODE") or "DODGERSWIN").strip(),
        )
