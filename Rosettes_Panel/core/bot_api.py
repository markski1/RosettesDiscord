import json
from typing import cast
from urllib import error, request

from core.config import bot_api_base_url, panel_api_secret


DEFAULT_TIMEOUT_SECONDS = 10
JsonDict = dict[str, object]


class BotApiError(Exception):
    pass


def _build_url(path: str) -> str:
    base = (bot_api_base_url or "").rstrip("/")
    if not base:
        raise BotApiError("BOT_API_BASE_URL is not configured.")
    return f"{base}/{path.lstrip('/')}"


def _request_json(method: str, path: str, payload: JsonDict | None = None) -> JsonDict:
    if not panel_api_secret:
        raise BotApiError("PANEL_API_SECRET is not configured.")

    body = None
    if payload is not None:
        body = json.dumps(payload).encode("utf-8")

    req = request.Request(_build_url(path), data=body, method=method.upper())
    req.add_header("Accept", "application/json")
    req.add_header("X-Rosettes-Panel-Secret", panel_api_secret)

    if body is not None:
        req.add_header("Content-Type", "application/json")

    try:
        with request.urlopen(req, timeout=DEFAULT_TIMEOUT_SECONDS) as response:
            raw = cast(bytes, response.read()).decode("utf-8")
            if not raw.strip():
                return {"success": True, "message": "ok", "data": None}
            return cast(JsonDict, json.loads(raw))
    except error.HTTPError as exc:
        raw = cast(bytes, exc.read()).decode("utf-8")
        if raw.strip():
            try:
                return cast(JsonDict, json.loads(raw))
            except json.JSONDecodeError:
                pass
        return {"success": False, "message": f"http_{exc.code}", "data": None}
    except error.URLError as exc:
        raise BotApiError(f"Unable to reach bot API: {exc.reason}") from exc


def validate_panel_login_key(key: str) -> tuple[int | None, str | None]:
    response = _request_json("POST", "/rosapi/internal/panel/login", {"key": key})
    if not response.get("success"):
        message = str(response.get("message") or "login_failed")
        if message == "key_not_found":
            return None, "Key does not exist."
        if message == "login_db_unavailable":
            return None, "Rosettes could not verify your key because the bot database is unavailable right now. Please try again later."
        if message == "unauthorized":
            return None, "The panel could not authenticate against the bot API. Please try again later."
        if message == "http_404":
            return None, "The panel could not authenticate against the bot API. Please try again later. [E1]"
        if message.startswith("http_"):
            return None, f"The bot API returned {message.replace('http_', 'HTTP ')} during login validation."
        return None, f"Login validation failed: {message}"

    data = response.get("data")
    if not isinstance(data, dict):
        return None, "Login validation returned an invalid response."

    user_id = data.get("user_id")
    if user_id is None:
        return None, "Login validation returned no user ID."

    return int(cast(int | str, user_id)), None


def reload_guild(server_id: int) -> tuple[bool, str]:
    response = _request_json("POST", f"/rosapi/internal/guild/{int(server_id)}/reload")
    return bool(response.get("success")), str(response.get("message") or "guild_reload_failed")


def reload_autoroles(server_id: int) -> tuple[bool, str]:
    response = _request_json("POST", f"/rosapi/internal/autoroles/{int(server_id)}/reload")
    return bool(response.get("success")), str(response.get("message") or "autoroles_reload_failed")


def update_guild_settings(
    server_id: int,
    message_parsing: bool,
    random_commands: bool,
    dumb_commands: bool,
    farm: bool,
    voice_announce: bool,
    default_role: int = 0,
    log_channel: int = 0,
    farm_channel: int = 0,
) -> tuple[bool, str]:
    response = _request_json(
        "POST",
        f"/rosapi/internal/guild/{int(server_id)}/settings",
        {
            "messageParsing": bool(message_parsing),
            "randomCommands": bool(random_commands),
            "dumbCommands": bool(dumb_commands),
            "farm": bool(farm),
            "voiceAnnounce": bool(voice_announce),
            "defaultRole": int(default_role),
            "logChannel": int(log_channel),
            "farmChannel": int(farm_channel),
        },
    )
    return bool(response.get("success")), str(response.get("message") or "settings_failed")


def get_guild_channels(server_id: int) -> list[dict[str, object]]:
    """
    Returns live channels from the bot for the given guild, or [] if the bot
    is unreachable / not in the guild. Never raises on connectivity errors so
    the panel can fall back to a raw-ID input.
    """
    try:
        response = _request_json("GET", f"/rosapi/internal/guild/{int(server_id)}/channels")
    except BotApiError:
        return []

    data = response.get("data")
    if not isinstance(data, dict):
        return []
    channels = data.get("channels")
    if not isinstance(channels, list):
        return []
    return [c for c in channels if isinstance(c, dict)]


def get_guild_roles_live(server_id: int) -> list[dict[str, object]]:
    """
    Returns live roles from the bot for the given guild, or [] if the bot is
    unreachable / not in the guild. Never raises on connectivity errors.
    """
    try:
        response = _request_json("GET", f"/rosapi/internal/guild/{int(server_id)}/roles/live")
    except BotApiError:
        return []

    data = response.get("data")
    if not isinstance(data, dict):
        return []
    roles = data.get("roles")
    if not isinstance(roles, list):
        return []
    return [r for r in roles if isinstance(r, dict)]


def create_autorole_group(
    server_id: int,
    name: str,
    entries: list[dict[str, object]],
) -> tuple[int | None, str]:
    response = _request_json(
        "POST",
        f"/rosapi/internal/guild/{int(server_id)}/autoroles",
        {"name": name, "entries": entries},
    )
    if not response.get("success"):
        return None, str(response.get("message") or "autorole_create_failed")

    data = response.get("data")
    if not isinstance(data, dict):
        return None, "autorole_create_failed"

    group_id = data.get("group_id")
    if group_id is None:
        return None, "autorole_create_failed"

    return int(cast(int | str, group_id)), ""


def delete_autorole_group(server_id: int, group_id: int) -> tuple[bool, str]:
    response = _request_json(
        "DELETE",
        f"/rosapi/internal/guild/{int(server_id)}/autoroles/{int(group_id)}",
    )
    return bool(response.get("success")), str(response.get("message") or "autorole_delete_failed")
