from functools import wraps

from flask import Blueprint, render_template, request, redirect
from flask_login import login_required, current_user, login_user

from core.session import attempt_login
from utils.db_getters import get_owned_servers, get_user_data, get_server_data
from utils.miscfuncs import htmx_check, ownership_required

panel_bp = Blueprint("panel", __name__, url_prefix="/panel")


@panel_bp.route("/")
@login_required
@htmx_check
def index(is_htmx):
    servers = get_owned_servers(current_user.id)
    user = get_user_data(current_user.id)

    return render_template("panel.html", is_htmx=is_htmx, servers=servers, user=user)


@panel_bp.post("/<int:server_id>/settings")
@login_required
@ownership_required
@htmx_check
def settings(is_htmx, server_id):
    server = get_server_data(server_id)
    if not server:
        return "Server not found."

    # TODO: Parse server['settings']

    return render_template("settings.html", is_htmx=is_htmx, server=server)


@panel_bp.post("/<int:server_id>/roles")
@login_required
@ownership_required
@htmx_check
def roles(is_htmx, server_id):
    server = get_server_data(server_id)
    if not server:
        return "Server not found."

    return render_template("roles.html", is_htmx=is_htmx, server=server)


@panel_bp.post("/<int:server_id>/cmds")
@login_required
@ownership_required
@htmx_check
def cmds(is_htmx, server_id):
    server = get_server_data(server_id)
    if not server:
        return "Server not found."

    return render_template("cmds.html", is_htmx=is_htmx, server=server)
