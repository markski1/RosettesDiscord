import hashlib
import time

from flask import Blueprint, request, redirect, render_template
from flask_login import login_required, login_user, logout_user

from core.session import attempt_login

session_bp = Blueprint("session", __name__, url_prefix="/session")

LOGIN_WINDOW_SECONDS = 300
LOGIN_MAX_ATTEMPTS = 8
_login_attempts: dict[str, list[float]] = {}


def _client_ip() -> str:
    forwarded = request.headers.get("X-Forwarded-For", "")
    if forwarded:
        return forwarded.split(",", 1)[0].strip()
    return request.remote_addr or "unknown"


def _key_bucket(key: str) -> str:
    return hashlib.sha256(key.strip().encode("utf-8")).hexdigest()[:12]


def _bucket_limited(bucket: str, now: float) -> bool:
    cutoff = now - LOGIN_WINDOW_SECONDS
    attempts = [ts for ts in _login_attempts.get(bucket, []) if ts >= cutoff]
    _login_attempts[bucket] = attempts
    return len(attempts) >= LOGIN_MAX_ATTEMPTS


def _record_attempt(bucket: str, now: float) -> None:
    _login_attempts.setdefault(bucket, []).append(now)


def _clear_attempts(*buckets: str) -> None:
    for bucket in buckets:
        _login_attempts.pop(bucket, None)


@session_bp.route("/")
def index():
    return "No one here but us sneps!"


@session_bp.post("/login")
def login():
    key = request.form["key"]

    if not key:
        return render_template("index.jinja2", error_message="Please enter your Rosettes key.")

    now = time.monotonic()
    ip_bucket = f"ip:{_client_ip()}"
    key_bucket = f"key:{_key_bucket(key)}"

    if _bucket_limited(ip_bucket, now) or _bucket_limited(key_bucket, now):
        return render_template(
            "index.jinja2",
            error_message="Too many login attempts. Please wait a few minutes and try again.",
        ), 429

    _record_attempt(ip_bucket, now)
    _record_attempt(key_bucket, now)

    user_sesh, error_message = attempt_login(key)
    if user_sesh:
        _clear_attempts(ip_bucket, key_bucket)
        login_user(user_sesh)
        return redirect("/panel/")

    generic_failure = "Invalid Rosettes key."
    if error_message and "could not verify" in error_message:
        generic_failure = error_message
    elif error_message and "bot API" in error_message:
        generic_failure = error_message
    return render_template("index.jinja2", error_message=generic_failure)


@session_bp.route("/logout")
@login_required
def logout():
    logout_user()
    return redirect("../")
