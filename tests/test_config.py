import pytest

from pandaplate.config import Config, ConfigError


def test_defaults():
    config = Config.from_env({"NTFY_TOPIC": "abc"})
    assert config.topic == "abc"
    assert config.server == "https://ntfy.sh"
    assert config.token is None
    assert config.priority == 4
    assert config.deal_price == "$7"
    assert config.promo_code == "DODGERSWIN"


def test_overrides():
    config = Config.from_env(
        {
            "NTFY_TOPIC": "abc",
            "NTFY_SERVER": "https://ntfy.example/",
            "NTFY_TOKEN": "tk_1",
            "NTFY_PRIORITY": "5",
            "NTFY_TAGS": "panda_face, hamburger ,",
            "PANDA_DEAL_PRICE": "$6",
            "PANDA_PROMO_CODE": "LADWIN",
        }
    )
    assert config.server == "https://ntfy.example"
    assert config.token == "tk_1"
    assert config.priority == 5
    assert config.tags == ["panda_face", "hamburger"]
    assert config.deal_price == "$6"
    assert config.promo_code == "LADWIN"


@pytest.mark.parametrize("env", [{}, {"NTFY_TOPIC": "   "}])
def test_missing_topic_is_an_error(env):
    with pytest.raises(ConfigError, match="NTFY_TOPIC"):
        Config.from_env(env)


@pytest.mark.parametrize("value", ["0", "6", "high"])
def test_bad_priority_is_an_error(value):
    with pytest.raises(ConfigError, match="NTFY_PRIORITY"):
        Config.from_env({"NTFY_TOPIC": "abc", "NTFY_PRIORITY": value})
