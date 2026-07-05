"""
Tests for the panel's guild settings route.

Verifies the route renders both branches (bot reachable vs unreachable)
correctly, including the new dropdowns, fallback inputs, and the
'bot_reachable' banner.
"""
import os
import re
import unittest
from unittest.mock import patch

# Env must be set before any panel import.
os.environ.setdefault("SECRET_KEY", "test-secret")
os.environ.setdefault("PANEL_API_SECRET", "test-secret")
os.environ.setdefault("BOT_API_BASE_URL", "http://bot.test")
os.environ.setdefault("DB_USER", "x")
os.environ.setdefault("DB_PASS", "x")
os.environ.setdefault("DB_HOST", "x")
os.environ.setdefault("DB_NAME", "x")

from app import app  # noqa: E402


def _server_row():
    return {
        "id": 1,
        "namecache": "Test Guild",
        "settings": "1101011111",  # 0=0, 2=0, 3=1, 4=0, 5=1
        "ownerid": 99,
        "defaultrole": 7,
        "logchan": 5,
        "rpgchan": 9,
    }


class SettingsRouteTests(unittest.TestCase):
    def setUp(self):
        app.config["LOGIN_DISABLED"] = True
        app.config["WTF_CSRF_ENABLED"] = False
        self.client = app.test_client()
        # ownership_required compares current_user.id to server['ownerid'].
        # LOGIN_DISABLED turns login_required into a no-op, leaving an
        # anonymous user whose .id lookup would fail; patch it to 99 so the
        # decorator passes.
        self._current_user_patcher = patch(
            "utils.miscfuncs.current_user",
            id=99,
        )
        self._current_user_patcher.start()

    def tearDown(self):
        self._current_user_patcher.stop()

    def _get(self):
        return self.client.get("/panel/1/settings")

    def test_unreachable_branch_renders_inputs_and_banner(self):
        with patch("utils.miscfuncs.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_guild_channels", return_value=[]), \
             patch("routes.panel.get_guild_roles_live", return_value=[]):
            resp = self._get()

        self.assertEqual(resp.status_code, 200)
        body = resp.get_data(as_text=True)
        # Banner appears.
        self.assertIn("Rosettes could not load channel and role names", body)
        # Falls back to numeric inputs.
        self.assertIn('name="defaultrole"', body)
        self.assertIn('type="number"', body)
        self.assertIn('value="7"', body)
        self.assertIn('value="5"', body)
        self.assertIn('value="9"', body)
        # All 5 toggles are present.
        for name in ("msgparse", "random", "dumb", "minigame", "announce"):
            self.assertIn(f'name="{name}"', body)

    def test_reachable_branch_renders_dropdowns(self):
        channels = [
            {"id": 5, "name": "general", "type": "text"},
            {"id": 6, "name": "lobby", "type": "voice"},
            {"id": 9, "name": "farm", "type": "text"},
        ]
        roles = [
            {"id": 1, "name": "@everyone", "isEveryone": True, "position": 0},
            {"id": 7, "name": "Member", "isEveryone": False, "position": 5},
            {"id": 8, "name": "Bot", "isEveryone": False, "position": 3},
        ]
        with patch("utils.miscfuncs.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_guild_channels", return_value=channels), \
             patch("routes.panel.get_guild_roles_live", return_value=roles):
            resp = self._get()

        self.assertEqual(resp.status_code, 200)
        body = resp.get_data(as_text=True)
        # No banner when the bot is reachable.
        self.assertNotIn("The bot API is unreachable", body)
        # Dropdowns rendered (not numeric inputs).
        self.assertIn('<select name="defaultrole"', body)
        self.assertIn('<select name="logchannel"', body)
        self.assertIn('<select name="farmchannel"', body)
        # Selected state for matching DB values.
        self.assertRegex(
            body,
            r'<option value="7"[^>]*selected[^>]*>Member</option>',
        )
        self.assertRegex(
            body,
            r'<option value="5"[^>]*selected[^>]*>#general</option>',
        )
        self.assertRegex(
            body,
            r'<option value="9"[^>]*selected[^>]*>#farm</option>',
        )
        # Voice channel excluded from log/farm selects.
        self.assertNotIn(">#lobby</option>", body)
        # @everyone excluded from role select.
        self.assertNotIn('<option value="1">@everyone</option>', body)

    def test_settings_index_parsing(self):
        # settings string "1101011111":
        #   [0] msgparse = '1' (True)
        #   [2] random  = '0' (False)
        #   [3] dumb    = '1' (True)
        #   [4] farm    = '0' (False)
        #   [5] announce= '1' (True)
        with patch("utils.miscfuncs.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_server_data", return_value=_server_row()), \
             patch("routes.panel.get_guild_channels", return_value=[]), \
             patch("routes.panel.get_guild_roles_live", return_value=[]):
            resp = self._get()
        body = resp.get_data(as_text=True)

        def select_for(name: str) -> str:
            m = re.search(
                rf'<select name="{name}">.*?</select>',
                body, re.S,
            )
            self.assertIsNotNone(m, f"no <select name={name!r}> in body")
            return m.group(0)

        # Template: Enabled has no `selected` marker; Disabled gets `selected`
        # when the toggle is False. So we check whether the value-0 option is
        # marked selected (i.e. toggle is False) or not (toggle is True).
        def has_disabled_selected(name: str) -> bool:
            sel = select_for(name)
            return bool(re.search(
                r'<option value="0"[^>]*selected[^>]*>Disabled</option>',
                sel,
            ))

        self.assertFalse(has_disabled_selected("msgparse"))
        self.assertTrue(has_disabled_selected("random"))
        self.assertFalse(has_disabled_selected("dumb"))
        self.assertTrue(has_disabled_selected("minigame"))
        self.assertFalse(has_disabled_selected("announce"))


if __name__ == "__main__":
    unittest.main()
