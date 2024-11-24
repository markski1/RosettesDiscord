import json

from flask import Blueprint, render_template, request
from flask_login import login_required

from utils.db_helpers import set_server_settings, insert_new_autorole, insert_role_for_autorole
from utils.miscfuncs import ownership_required

action_bp = Blueprint("action", __name__, url_prefix="/action")


@action_bp.route("/")
@login_required
def index():
    return "Nothing here but us sneps!"


@action_bp.post("/<int:server_id>/update-settings")
@login_required
@ownership_required
def post_settings(server_id):
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
    return render_template("prompts/success.jinja2", message="Settings updated successfully.")


@action_bp.post("/<int:server_id>/create-autoroles")
@login_required
@ownership_required
def post_new_autoroles(server_id):
    role_name = request.form.get("name")
    role_list = json.loads(request.form.get('RoleListJson'))

    for role in role_list:
        if not role['roleId'].isnumeric():
            return "Invalid parameters. [Role ID must be an integer.]"

    autorole_id = insert_new_autorole(server_id, role_name)

    # Separate loop, to avoid partial autorole group inserts.
    for role in role_list:
        insert_role_for_autorole(server_id, autorole_id, role)

    return render_template(
        template_name_or_list="prompts/success.jinja2",
        message=f"Autorole group created successfully. Use `/setautorole {autorole_id}` in the desired channel."
    )
