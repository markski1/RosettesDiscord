from flask import Blueprint, render_template, request, redirect
from flask_login import login_required, current_user, login_user

from core.session import attempt_login
from utils.db_getters import get_owned_servers, get_user_data
from utils.miscfuncs import htmx_check

panel_bp = Blueprint("panel", __name__, url_prefix="/panel")


@panel_bp.route("/")
@login_required
@htmx_check
def index(is_htmx):
    servers = get_owned_servers(current_user.id)
    user = get_user_data(current_user.id)

    return render_template("panel.html", is_htmx=is_htmx, servers=servers, user=user)


@panel_bp.post("/login")
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
