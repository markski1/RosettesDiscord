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


def validate_panel_login_key(key: str) -> int | None:
    response = _request_json("POST", "/rosapi/internal/panel/login", {"key": key})
    if not response.get("success"):
        return None

    data = response.get("data")
    if not isinstance(data, dict):
        return None

    user_id = data.get("user_id")
    if user_id is None:
        return None

    return int(cast(int | str, user_id))


def reload_guild(server_id: int) -> tuple[bool, str]:
    response = _request_json("POST", f"/rosapi/internal/guild/{int(server_id)}/reload")
    return bool(response.get("success")), str(response.get("message") or "guild_reload_failed")


def reload_autoroles(server_id: int) -> tuple[bool, str]:
    response = _request_json("POST", f"/rosapi/internal/autoroles/{int(server_id)}/reload")
    return bool(response.get("success")), str(response.get("message") or "autoroles_reload_failed")
