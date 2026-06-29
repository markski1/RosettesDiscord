import hmac
import secrets

from flask import abort, request, session


def get_csrf_token() -> str:
    token = session.get("_csrf_token")
    if not token:
        token = secrets.token_urlsafe(32)
        session["_csrf_token"] = token
    return token


def csrf_input() -> str:
    token = get_csrf_token()
    return f'<input type="hidden" name="csrf_token" value="{token}">'


def validate_csrf():
    if request.method not in ("POST", "PUT", "DELETE", "PATCH"):
        return

    expected = session.get("_csrf_token")
    provided = request.form.get("csrf_token") or request.headers.get("X-CSRF-Token")

    if not expected or not provided or not hmac.compare_digest(expected, provided):
        abort(400, description="CSRF token missing or invalid.")
