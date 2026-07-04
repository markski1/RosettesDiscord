"""
Tests for core.bot_api.

These cover the panel-side helpers added for the new /guild/{id}/channels and
/guild/{id}/roles/live endpoints, and the three new fields in the
/guild/{id}/settings payload.

No DB, no network, no Flask app boot. urllib.request.urlopen is mocked.
"""
import io
import json
import os
import unittest
from contextlib import contextmanager
from urllib import error, request

# Set env vars before importing the module, so config picks them up.
os.environ.setdefault("SECRET_KEY", "test-secret")
os.environ.setdefault("PANEL_API_SECRET", "test-secret")
os.environ.setdefault("BOT_API_BASE_URL", "http://bot.test")

import core.bot_api as bot_api
import core.config as config


def _make_response(body: bytes, status: int = 200) -> io.BytesIO:
    resp = io.BytesIO(body)
    resp.code = status
    resp.headers = {"Content-Type": "application/json"}
    return resp


@contextmanager
def _patched_urlopen(responses):
    """
    Patch urlopen to yield canned responses in order.
    Each response is (body_bytes, status) or 'http_error' (status) or 'url_error'.
    """
    iter_responses = iter(responses)
    calls = []

    def fake_urlopen(req, timeout=None):
        calls.append(req)
        kind = next(iter_responses)
        if kind == "url_error":
            raise error.URLError("connection refused")
        if isinstance(kind, tuple) and kind[0] == "http_error":
            status = kind[1]
            err = error.HTTPError(
                url=req.full_url,
                code=status,
                msg="",
                hdrs={"Content-Type": "application/json"},
                fp=io.BytesIO(b""),
            )
            raise err
        body, status = kind
        return _make_response(body, status)

    orig = bot_api.request.urlopen
    bot_api.request.urlopen = fake_urlopen
    try:
        yield calls
    finally:
        bot_api.request.urlopen = orig


class TestGetGuildChannels(unittest.TestCase):
    def test_returns_list_on_success(self):
        body = json.dumps({
            "success": True,
            "message": "guild_channels",
            "data": {
                "channels": [
                    {"id": 5, "name": "general", "type": "text"},
                    {"id": 6, "name": "lobby", "type": "voice"},
                ]
            }
        }).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            result = bot_api.get_guild_channels(123)
        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0].method, "GET")
        self.assertIn("/rosapi/internal/guild/123/channels", calls[0].full_url)
        # urllib canonicalizes header keys to title-case-with-lowercase-tail.
        secret_header = next(
            (v for k, v in calls[0].header_items() if k.lower() == "x-rosettes-panel-secret"),
            None,
        )
        self.assertEqual(secret_header, "test-secret")
        self.assertEqual(len(result), 2)
        self.assertEqual(result[0]["name"], "general")
        self.assertEqual(result[1]["type"], "voice")

    def test_returns_empty_on_urllib_error(self):
        with _patched_urlopen(["url_error"]):
            self.assertEqual(bot_api.get_guild_channels(1), [])

    def test_returns_empty_on_http_error(self):
        with _patched_urlopen([("http_error", 503)]):
            self.assertEqual(bot_api.get_guild_channels(1), [])

    def test_returns_empty_on_unexpected_payload(self):
        body = json.dumps({"success": True, "message": "ok", "data": None}).encode("utf-8")
        with _patched_urlopen([(body, 200)]):
            self.assertEqual(bot_api.get_guild_channels(1), [])

    def test_filters_non_dict_entries(self):
        body = json.dumps({
            "success": True,
            "data": {"channels": [{"id": 1, "name": "x"}, "not a dict", None]}
        }).encode("utf-8")
        with _patched_urlopen([(body, 200)]):
            result = bot_api.get_guild_channels(1)
        self.assertEqual(len(result), 1)
        self.assertEqual(result[0]["name"], "x")


class TestGetGuildRolesLive(unittest.TestCase):
    def test_returns_list_on_success(self):
        body = json.dumps({
            "success": True,
            "message": "guild_roles_live",
            "data": {
                "roles": [
                    {"id": 1, "name": "@everyone", "isEveryone": True, "position": 0},
                    {"id": 7, "name": "Member", "isEveryone": False, "position": 5},
                ]
            }
        }).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            result = bot_api.get_guild_roles_live(42)
        self.assertEqual(calls[0].method, "GET")
        self.assertIn("/rosapi/internal/guild/42/roles/live", calls[0].full_url)
        self.assertEqual(len(result), 2)
        self.assertEqual(result[1]["name"], "Member")

    def test_returns_empty_on_urllib_error(self):
        with _patched_urlopen(["url_error"]):
            self.assertEqual(bot_api.get_guild_roles_live(1), [])

    def test_returns_empty_on_http_error(self):
        with _patched_urlopen([("http_error", 404)]):
            self.assertEqual(bot_api.get_guild_roles_live(1), [])


class TestApplicationApi(unittest.TestCase):
    def test_create_application_returns_id_and_token(self):
        body = json.dumps({
            "success": True,
            "message": "application_created",
            "data": {"app_id": 12, "token": "new-token"},
        }).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            app_id, token, message = bot_api.create_application("My App", 99)

        self.assertEqual(app_id, 12)
        self.assertEqual(token, "new-token")
        self.assertEqual(message, "")
        self.assertEqual(calls[0].method, "POST")
        self.assertIn("/rosapi/internal/apps", calls[0].full_url)
        sent = json.loads(calls[0].data.decode("utf-8"))
        self.assertEqual(sent, {"name": "My App", "ownerId": 99})

    def test_rotate_application_token_returns_token(self):
        body = json.dumps({
            "success": True,
            "message": "application_token_rotated",
            "data": {"token": "rotated-token"},
        }).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            token, message = bot_api.rotate_application_token(12, 99)

        self.assertEqual(token, "rotated-token")
        self.assertEqual(message, "")
        self.assertEqual(calls[0].method, "POST")
        self.assertIn("/rosapi/internal/apps/12/rotate-token", calls[0].full_url)
        sent = json.loads(calls[0].data.decode("utf-8"))
        self.assertEqual(sent, {"ownerId": 99})

    def test_delete_application_remote_sends_owner(self):
        body = json.dumps({"success": True, "message": "application_deleted"}).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            ok, message = bot_api.delete_application_remote(12, 99)

        self.assertTrue(ok)
        self.assertEqual(message, "application_deleted")
        self.assertEqual(calls[0].method, "DELETE")
        self.assertIn("/rosapi/internal/apps/12", calls[0].full_url)
        sent = json.loads(calls[0].data.decode("utf-8"))
        self.assertEqual(sent, {"ownerId": 99})

    def test_revoke_application_user_sends_owner(self):
        body = json.dumps({"success": True, "message": "application_user_revoked"}).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            ok, message = bot_api.revoke_application_user(12, 99, 1234)

        self.assertTrue(ok)
        self.assertEqual(message, "application_user_revoked")
        self.assertEqual(calls[0].method, "DELETE")
        self.assertIn("/rosapi/internal/apps/12/users/1234", calls[0].full_url)
        sent = json.loads(calls[0].data.decode("utf-8"))
        self.assertEqual(sent, {"ownerId": 99})


class TestUpdateGuildSettings(unittest.TestCase):
    def test_payload_includes_new_fields(self):
        body = json.dumps({"success": True, "message": "guild_settings_updated"}).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            ok, message = bot_api.update_guild_settings(
                7,
                message_parsing=True,
                random_commands=False,
                dumb_commands=True,
                farm=False,
                voice_announce=True,
                default_role=11,
                log_channel=22,
                farm_channel=33,
            )
        self.assertTrue(ok)
        self.assertEqual(message, "guild_settings_updated")
        self.assertEqual(len(calls), 1)
        req = calls[0]
        self.assertEqual(req.method, "POST")
        self.assertIn("/rosapi/internal/guild/7/settings", req.full_url)
        ct_header = next(
            (v for k, v in req.header_items() if k.lower() == "content-type"),
            None,
        )
        self.assertEqual(ct_header, "application/json")
        sent = json.loads(req.data.decode("utf-8"))
        self.assertEqual(sent, {
            "messageParsing": True,
            "randomCommands": False,
            "dumbCommands": True,
            "farm": False,
            "voiceAnnounce": True,
            "defaultRole": 11,
            "logChannel": 22,
            "farmChannel": 33,
        })

    def test_defaults_for_new_fields_are_zero(self):
        body = json.dumps({"success": True, "message": "guild_settings_updated"}).encode("utf-8")
        with _patched_urlopen([(body, 200)]) as calls:
            bot_api.update_guild_settings(
                7, message_parsing=True, random_commands=True,
                dumb_commands=True, farm=True, voice_announce=True,
            )
        sent = json.loads(calls[0].data.decode("utf-8"))
        self.assertEqual(sent["defaultRole"], 0)
        self.assertEqual(sent["logChannel"], 0)
        self.assertEqual(sent["farmChannel"], 0)

    def test_returns_error_message_on_failure(self):
        body = json.dumps({"success": False, "message": "guild_runtime_fields_failed"}).encode("utf-8")
        with _patched_urlopen([(body, 200)]):
            ok, message = bot_api.update_guild_settings(
                7, True, True, True, True, True, 1, 2, 3,
            )
        self.assertFalse(ok)
        self.assertEqual(message, "guild_runtime_fields_failed")

    def test_raises_on_urllib_error(self):
        with _patched_urlopen(["url_error"]):
            with self.assertRaises(bot_api.BotApiError):
                bot_api.update_guild_settings(7, True, True, True, True, True, 0, 0, 0)


if __name__ == "__main__":
    unittest.main()
