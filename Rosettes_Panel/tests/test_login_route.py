import os
import unittest
from unittest.mock import patch

os.environ.setdefault("SECRET_KEY", "test-secret")
os.environ.setdefault("PANEL_API_SECRET", "test-secret")
os.environ.setdefault("BOT_API_BASE_URL", "http://bot.test")
os.environ.setdefault("DB_USER", "x")
os.environ.setdefault("DB_PASS", "x")
os.environ.setdefault("DB_HOST", "x")
os.environ.setdefault("DB_NAME", "x")

from app import app  # noqa: E402
import routes.session as session_route  # noqa: E402


class LoginRouteTests(unittest.TestCase):
    def setUp(self):
        app.config["LOGIN_DISABLED"] = True
        app.config["WTF_CSRF_ENABLED"] = False
        self.client = app.test_client()
        session_route._login_attempts.clear()

    def tearDown(self):
        session_route._login_attempts.clear()

    def _post_login(self, key: str):
        with self.client.session_transaction() as sess:
            sess["_csrf_token"] = "csrf-test-token"
        return self.client.post("/session/login", data={"key": key, "csrf_token": "csrf-test-token"})

    def test_invalid_key_uses_generic_message(self):
        with patch("routes.session.attempt_login", return_value=(None, "Key does not exist.")):
            resp = self._post_login("bad-key")

        body = resp.get_data(as_text=True)
        self.assertEqual(resp.status_code, 200)
        self.assertIn("Invalid Rosettes key.", body)
        self.assertNotIn("Key does not exist.", body)

    def test_rate_limit_returns_429(self):
        with patch("routes.session.attempt_login", return_value=(None, "Invalid Rosettes key.")):
            for _ in range(session_route.LOGIN_MAX_ATTEMPTS):
                resp = self._post_login("bad-key")
                self.assertEqual(resp.status_code, 200)

            resp = self._post_login("bad-key")

        self.assertEqual(resp.status_code, 429)
        self.assertIn("Too many login attempts", resp.get_data(as_text=True))


if __name__ == "__main__":
    unittest.main()
