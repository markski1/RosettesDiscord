import json

from flask import Blueprint, render_template, request, jsonify, redirect, url_for
from flask_login import login_required, current_user

from core.bot_api import reload_autoroles, reload_guild, BotApiError
from utils.db_helpers import set_server_settings, insert_new_autorole, insert_role_for_autorole, get_app_by_name, \
    insert_application, get_server_data, delete_autorole_group, get_app_by_id, delete_application
from utils.miscfuncs import ownership_required, generate_random_string
from utils.page_helpers import render_success, render_error

action_bp = Blueprint("action", __name__, url_prefix="/action")


@action_bp.route("/")
@login_required
def index():
    return "No one here but us sneps!"


@action_bp.post("/<int:server_id>/update-settings")
@login_required
@ownership_required
def post_settings(server_id):
    try:
        msgparse = abs(int(request.form["msgparse"]))
        minigame = abs(int(request.form["minigame"]))
        announce = abs(int(request.form["announce"]))
    except:
        return render_error("Invalid parameters.")

    if msgparse > 1 or minigame > 1 or announce > 1:
        return render_error("Invalid parameters.")

    # Gambling has no toggle in the panel, so preserve whatever the server already has.
    server = get_server_data(server_id)
    if not server:
        return render_error("Server not found.")
    gambling = server["settings"][2]

    new_settings = f"{msgparse}1{gambling}1{minigame}{announce}1111"
    set_server_settings(server_id, new_settings)

    try:
        reload_ok, reload_message = reload_guild(server_id)
    except BotApiError as exc:
        return render_success(f"Settings were saved, but the bot reload failed: {exc}")

    if not reload_ok:
        return render_success(f"Settings were saved, but the bot reload failed: {reload_message}")

    return render_success("Settings updated successfully.")


@action_bp.post("/<int:server_id>/create-autoroles")
@login_required
@ownership_required
def post_new_autoroles(server_id):
    role_name = request.form.get("name")
    if not role_name or not role_name.strip():
        return jsonify(ok=False, message="Please provide a profile name."), 400

    try:
        role_list = json.loads(request.form.get('RoleListJson') or "[]")
    except (TypeError, ValueError):
        return jsonify(ok=False, message="Invalid role data."), 400

    if not role_list:
        return jsonify(ok=False, message="Add at least one role before creating."), 400

    for role in role_list:
        if not str(role.get('roleId', '')).isnumeric():
            return jsonify(ok=False, message="Invalid parameters. [Role ID must be an integer.]"), 400

    autorole_id = insert_new_autorole(server_id, role_name)

    # Separate loop, to avoid partial autorole group inserts.
    for role in role_list:
        insert_role_for_autorole(server_id, autorole_id, role)

    try:
        reload_ok, reload_message = reload_autoroles(server_id)
    except BotApiError as exc:
        return jsonify(ok=True,
                       message=f"Autorole group was created, but the bot reload failed: {exc}"), 200

    if not reload_ok:
        return jsonify(ok=True,
                       message=f"Autorole group was created, but the bot reload failed: {reload_message}"), 200

    return jsonify(ok=True,
                   message=f"Autorole group created successfully. "
                           f"Use `/setautorole {autorole_id}` in the desired channel.")


@action_bp.post("/<int:server_id>/delete-autoroles/<int:group_id>")
@login_required
@ownership_required
def post_delete_autoroles(server_id, group_id):
    delete_autorole_group(server_id, group_id)

    try:
        reload_ok, reload_message = reload_autoroles(server_id)
    except BotApiError as exc:
        return render_success(f"Autorole group was deleted, but the bot reload failed: {exc}")

    if not reload_ok:
        return render_success(f"Autorole group was deleted, but the bot reload failed: {reload_message}")

    return redirect(url_for("panel.roles", server_id=server_id))


@action_bp.post("/delete-app/<int:app_id>")
@login_required
def post_delete_app(app_id):
    app = get_app_by_id(app_id)
    if not app:
        return render_error("App not found.")

    if int(app["owner_id"]) != int(current_user.id):
        return render_error("You don't own this application.")

    delete_application(app_id, int(current_user.id))
    return render_success("Application deleted successfully.")


@action_bp.post("/create-app")
@login_required
def post_new_app():
    name = request.form.get("name")

    if not name:
        return render_error("Invalid parameters.")

    existing = get_app_by_name(name)

    if existing:
        return render_error("An app with this name already exists.")

    if len(name) < 3 or len(name) > 50:
        return render_error("Name must be between 3 and 50 characters long.")

    app_token = generate_random_string(32)
    success = insert_application(name, app_token)

    if success:
        return render_success("App created successfully.")
    else:
        return render_error("An error occurred while creating the app.")
