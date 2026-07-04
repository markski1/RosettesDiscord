import json

from flask import Blueprint, request, jsonify, redirect, url_for
from flask_login import login_required, current_user

from core.bot_api import BotApiError, update_guild_settings, create_autorole_group, delete_autorole_group, \
    create_application, rotate_application_token, delete_application_remote, revoke_application_user
from utils.db_helpers import get_app_by_name, get_server_data, get_app_by_id
from utils.miscfuncs import ownership_required
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
        random_cmds = abs(int(request.form["random"]))
        dumb_cmds = abs(int(request.form["dumb"]))
    except:
        return render_error("Invalid parameters.")

    if msgparse > 1 or minigame > 1 or announce > 1 or random_cmds > 1 or dumb_cmds > 1:
        return render_error("Invalid parameters.")

    server = get_server_data(server_id)
    if not server:
        return render_error("Server not found.")

    raw = server["settings"]
    if len(raw) < 10:
        raw = (raw + "1111111111")[:10]

    try:
        default_role = int(request.form.get("defaultrole", 0))
        log_channel = int(request.form.get("logchannel", 0))
        farm_channel = int(request.form.get("farmchannel", 0))
    except (TypeError, ValueError):
        return render_error("Invalid channel or role ID.")

    try:
        ok, message = update_guild_settings(
            server_id,
            message_parsing=bool(msgparse),
            random_commands=bool(random_cmds),
            dumb_commands=bool(dumb_cmds),
            farm=bool(minigame),
            voice_announce=bool(announce),
            default_role=default_role,
            log_channel=log_channel,
            farm_channel=farm_channel,
        )
    except BotApiError as exc:
        return render_error(f"Settings could not be saved: {exc}")

    if not ok:
        return render_error(f"Settings could not be saved: {message}")

    return render_success("Settings updated successfully.")


@action_bp.post("/<int:server_id>/create-autoroles")
@login_required
@ownership_required
def post_new_autoroles(server_id):
    role_name = (request.form.get("name") or "").strip()
    if not role_name:
        return jsonify(ok=False, message="Please provide a profile name."), 400

    if len(role_name) > 32:
        return jsonify(ok=False, message="Profile name is too long (max 32 characters)."), 400

    try:
        role_list = json.loads(request.form.get('RoleListJson') or "[]")
    except (TypeError, ValueError):
        return jsonify(ok=False, message="Invalid role data."), 400

    if not role_list:
        return jsonify(ok=False, message="Add at least one role before creating."), 400

    entries = []
    for role in role_list:
        role_id_raw = role.get('roleId', '')
        if not str(role_id_raw).isnumeric():
            return jsonify(ok=False, message="Invalid parameters. [Role ID must be an integer.]"), 400
        emote = role.get('roleEmoji', '')
        if not isinstance(emote, str) or not emote:
            return jsonify(ok=False, message="Invalid parameters. [Emoji is required.]"), 400
        entries.append({"emote": emote, "roleId": int(role_id_raw)})

    try:
        group_id, message = create_autorole_group(server_id, role_name, entries)
    except BotApiError as exc:
        return jsonify(ok=False, message=f"Autorole group could not be created: {exc}"), 502

    if group_id is None:
        return jsonify(ok=False, message=f"Autorole group could not be created: {message}"), 502

    return jsonify(ok=True,
                   message=f"Autorole group created successfully. "
                           f"Use `/setautorole {group_id}` in the desired channel.")


@action_bp.post("/<int:server_id>/delete-autoroles/<int:group_id>")
@login_required
@ownership_required
def post_delete_autoroles(server_id, group_id):
    try:
        ok, message = delete_autorole_group(server_id, group_id)
    except BotApiError as exc:
        return render_error(f"Autorole group could not be deleted: {exc}")

    if not ok:
        return render_error(f"Autorole group could not be deleted: {message}")

    return redirect(url_for("panel.roles", server_id=server_id))


@action_bp.post("/delete-app/<int:app_id>")
@login_required
def post_delete_app(app_id):
    app = get_app_by_id(app_id)
    if not app:
        return render_error("App not found.")

    if int(app["owner_id"]) != int(current_user.id):
        return render_error("You don't own this application.")

    try:
        ok, message = delete_application_remote(app_id, int(current_user.id))
    except BotApiError as exc:
        return render_error(f"Application could not be deleted: {exc}")

    if not ok:
        return render_error(f"Application could not be deleted: {message}")

    return render_success("Application deleted successfully.")


@action_bp.post("/rotate-app-token/<int:app_id>")
@login_required
def post_rotate_app_token(app_id):
    app = get_app_by_id(app_id)
    if not app:
        return render_error("App not found.")

    if int(app["owner_id"]) != int(current_user.id):
        return render_error("You don't own this application.")

    try:
        token, message = rotate_application_token(app_id, int(current_user.id))
    except BotApiError as exc:
        return render_error(f"Token could not be rotated: {exc}")

    if token is None:
        return render_error(f"Token could not be rotated: {message}")

    return render_success(f"Token rotated successfully. Copy it now; it will not be shown again: {token}")


@action_bp.post("/revoke-app-user/<int:app_id>/<int:user_id>")
@login_required
def post_revoke_app_user(app_id, user_id):
    app = get_app_by_id(app_id)
    if not app:
        return render_error("App not found.")

    if int(app["owner_id"]) != int(current_user.id):
        return render_error("You don't own this application.")

    try:
        ok, message = revoke_application_user(app_id, int(current_user.id), user_id)
    except BotApiError as exc:
        return render_error(f"User authorization could not be revoked: {exc}")

    if not ok:
        return render_error(f"User authorization could not be revoked: {message}")

    return redirect(url_for("panel.app_manage", app_id=app_id))


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

    try:
        app_id, token, message = create_application(name, int(current_user.id))
    except BotApiError as exc:
        return render_error(f"An error occurred while creating the app: {exc}")

    if app_id is None or token is None:
        return render_error(f"An error occurred while creating the app: {message}")

    return render_success(f"App created successfully. Copy this token now; it will not be shown again: {token}")
