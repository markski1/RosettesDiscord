from flask_login import current_user

from core.database import get_db_conn


def get_user_data(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM users WHERE id = %s", user_id)

    result = db.fetch_one()
    db.pool()
    return result


def get_server_data(server_id):
    db = get_db_conn()
    db.execute("SELECT * FROM guilds WHERE id = %s", server_id)

    result = db.fetch_one()
    db.pool()
    return result


def get_owned_servers(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM guilds WHERE ownerid = %s", user_id)

    result = db.fetch_all()
    db.pool()
    return result


def get_server_roles(server_id):
    db = get_db_conn()
    db.execute("SELECT * FROM roles WHERE guildid = %s", server_id)

    result = db.fetch_all()
    db.pool()
    return result


def set_server_settings(server_id, settings):
    db = get_db_conn()
    db.execute("UPDATE guilds SET settings = %s WHERE id = %s", settings, server_id)
    db.pool()


def get_server_autoroles(server_id):
    db = get_db_conn()

    db.execute("SELECT * FROM autorole_groups WHERE guildid = %s", server_id)
    result = db.fetch_all()
    db.pool()

    return result


def insert_new_autorole(server_id, role_name):
    db = get_db_conn()

    db.execute("INSERT INTO autorole_groups (guildid, messageid, name) VALUES(%s, 0, %s)", server_id, role_name)
    insert_id = db.last_insert_id()

    db.pool()
    return insert_id


def insert_role_for_autorole(guild_id, autorole_id, role):
    db = get_db_conn()

    db.execute("INSERT INTO autorole_entries (guildid, emote, roleid, rolegroupid) VALUES(%s, %s, %s, %s)",
               guild_id, role['roleEmoji'], role['roleId'], autorole_id)

    db.pool()


def get_app_by_name(name):
    db = get_db_conn()
    db.execute("SELECT * FROM app_auth WHERE name = %s", name)
    result = db.fetch_one()
    db.pool()
    return result


def get_app_by_id(app_id):
    db = get_db_conn()
    db.execute("SELECT * FROM app_auth WHERE id = %s", app_id)
    result = db.fetch_one()
    db.pool()
    return result


def insert_application(name, app_token):
    db = get_db_conn()
    db.execute("INSERT INTO app_auth (name, owner_id, token_key) VALUES(%s, %s, %s)",
               name, current_user.id, app_token)
    result = db.last_insert_id()
    db.pool()
    return result


def get_apps_for_user(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM app_auth WHERE owner_id = %s", user_id)
    result = db.fetch_all()
    db.pool()
    return result
