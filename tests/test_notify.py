import json
import urllib.error
from contextlib import contextmanager

import pytest

from pandaplate import notify


class _Response:
    status = 200

    def read(self):
        return b""


@contextmanager
def _ok(request, timeout=None):
    _ok.last = (request, timeout)
    yield _Response()


def test_publish_posts_json_body():
    notify.publish(
        topic="my-topic",
        title="Deal",
        message="Dodgers won — \U0001f43c",
        tags=["panda_face"],
        priority=5,
        click="https://example.test/",
        token="tk_secret",
        opener=_ok,
    )
    request, _ = _ok.last
    assert request.full_url == "https://ntfy.sh"
    assert request.get_method() == "POST"
    assert request.get_header("Authorization") == "Bearer tk_secret"

    body = json.loads(request.data.decode("utf-8"))
    assert body == {
        "topic": "my-topic",
        "title": "Deal",
        "message": "Dodgers won — \U0001f43c",
        "priority": 5,
        "tags": ["panda_face"],
        "click": "https://example.test/",
    }


def test_publish_omits_auth_header_without_token():
    notify.publish(topic="t", title="a", message="b", opener=_ok)
    request, _ = _ok.last
    assert request.get_header("Authorization") is None


def test_publish_honours_custom_server():
    notify.publish(
        topic="t", title="a", message="b", server="https://ntfy.example/", opener=_ok
    )
    request, _ = _ok.last
    assert request.full_url == "https://ntfy.example"


def test_publish_requires_a_topic():
    with pytest.raises(notify.NotifyError):
        notify.publish(topic="", title="a", message="b", opener=_ok)


def test_publish_wraps_http_errors():
    def boom(*_args, **_kwargs):
        raise urllib.error.HTTPError("https://ntfy.sh", 429, "Too Many", {}, None)

    with pytest.raises(notify.NotifyError, match="429"):
        notify.publish(topic="t", title="a", message="b", opener=boom)
