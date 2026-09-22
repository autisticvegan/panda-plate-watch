"""Publish a notification to an ntfy.sh topic."""

from __future__ import annotations

import json
import urllib.error
import urllib.request

DEFAULT_SERVER = "https://ntfy.sh"


class NotifyError(RuntimeError):
    """The notification could not be published."""


def publish(
    *,
    topic: str,
    title: str,
    message: str,
    server: str = DEFAULT_SERVER,
    tags: list[str] | None = None,
    priority: int = 4,
    click: str | None = None,
    token: str | None = None,
    timeout: float = 15.0,
    opener=urllib.request.urlopen,
) -> None:
    """POST to ntfy's JSON endpoint.

    The JSON endpoint rather than the header-based one: titles and tags here
    contain non-ASCII characters, which HTTP headers can't carry cleanly.
    """
    if not topic:
        raise NotifyError("no ntfy topic configured")

    body: dict[str, object] = {
        "topic": topic,
        "title": title,
        "message": message,
        "priority": priority,
    }
    if tags:
        body["tags"] = tags
    if click:
        body["click"] = click

    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"

    request = urllib.request.Request(
        server.rstrip("/"),
        data=json.dumps(body).encode("utf-8"),
        headers=headers,
        method="POST",
    )
    try:
        with opener(request, timeout=timeout) as response:
            if response.status >= 300:
                raise NotifyError(f"ntfy returned HTTP {response.status}")
    except urllib.error.HTTPError as exc:
        raise NotifyError(f"ntfy returned HTTP {exc.code}") from exc
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        raise NotifyError(f"could not reach ntfy: {exc}") from exc
