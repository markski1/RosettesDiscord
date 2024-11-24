from flask import Blueprint, render_template, request
from flask_login import login_required

from utils.db_getters import set_server_settings
from utils.miscfuncs import htmx_check, ownership_required

panel_bp = Blueprint("action", __name__, url_prefix="/action")


@panel_bp.route("/")
@login_required
@htmx_check
def index():
    return "Nothing here but us sneps!"


@panel_bp.post("/<int:server_id>/update-settings")
@login_required
@ownership_required
@htmx_check
def post_settings(is_htmx, server_id):
    try:
        msgparse = abs(int(request.form["msgparse"]))
        minigame = abs(int(request.form["minigame"]))
        gambling = abs(int(request.form["gambling"]))
        announce = abs(int(request.form["announce"]))
    except:
        return "Invalid parameters."

    if int(msgparse) > 1 or int(minigame) > 1 or int(gambling) > 1 or int(announce) > 2:
        return "Invalid parameters."

    new_settings = f"{msgparse}1{gambling}1{minigame}{announce}1111"
    set_server_settings(server_id, new_settings)
    return render_template("prompts/success.jinja2", is_htmx=is_htmx, message="Settings updated successfully.")
