from flask import Blueprint, request, redirect
from flask_login import login_required, login_user, logout_user

from core.session import attempt_login

session_bp = Blueprint("session", __name__, url_prefix="/session")


@session_bp.route("/")
def index():
    return "nothing here"


@session_bp.post("/login")
def login():
    key = request.form["key"]

    if not key:
        return "Invalid key"

    user_sesh = attempt_login(key)
    if user_sesh:
        login_user(user_sesh)
        return redirect("/panel/")
    else:
        return "Key does not exist."


@session_bp.route("/logout")
@login_required
def logout():
    logout_user()
    return redirect("../")
