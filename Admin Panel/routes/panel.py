from flask import Blueprint, render_template
from flask_login import login_required, current_user

from utils.db_getters import get_owned_servers, get_user_data, get_server_data, get_server_roles
from utils.miscfuncs import htmx_check, ownership_required

panel_bp = Blueprint("panel", __name__, url_prefix="/panel")


@panel_bp.route("/")
@login_required
@htmx_check
def index(is_htmx):
    servers = get_owned_servers(current_user.id)
    user = get_user_data(current_user.id)

    return render_template("panel.jinja2", is_htmx=is_htmx, servers=servers, user=user)


@panel_bp.get("/<int:server_id>/settings")
@login_required
@ownership_required
@htmx_check
def settings(is_htmx, server_id):
    server = get_server_data(server_id)
    if not server:
        return "Server not found."

    msgparse = server["settings"][0] == '1'
    minigame = server["settings"][4] == '1'
    gambling = server["settings"][2] == '1'
    announce = server["settings"][5] == '1'

    return render_template("settings.jinja2", is_htmx=is_htmx, server=server,
                           msgparse=msgparse, minigame=minigame, gambling=gambling, announce=announce)


@panel_bp.get("/<int:server_id>/roles")
@login_required
@ownership_required
@htmx_check
def roles(is_htmx, server_id):
    server = get_server_data(server_id)
    if not server:
        return "Server not found."

    roles = get_server_roles(server_id)

    return render_template("roles.jinja2", is_htmx=is_htmx, server=server, roles=roles)
