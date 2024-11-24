from core.database import get_db_conn, pool_db_conn


def get_user_data(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM users WHERE id = %s", user_id)

    result = db.fetch_one()
    pool_db_conn(db)
    return result


def get_server_data(server_id):
    db = get_db_conn()
    db.execute("SELECT * FROM guilds WHERE id = %s", server_id)

    result = db.fetch_one()
    pool_db_conn(db)
    return result


def get_owned_servers(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM guilds WHERE ownerid = %s", user_id)

    result = db.fetch_all()
    pool_db_conn(db)
    return result


def get_server_roles(server_id):
    db = get_db_conn()
    db.execute("SELECT * FROM roles WHERE guildid = %s", server_id)

    result = db.fetch_all()
    pool_db_conn(db)
    return result


def set_server_settings(server_id, settings):
    db = get_db_conn()
    db.execute("UPDATE guilds SET settings = %s WHERE id = %s", settings, server_id)
    pool_db_conn(db)


def get_server_autoroles(server_id):
    db = get_db_conn()

    db.execute("SELECT * FROM autorole_groups WHERE guildid = %s", server_id)
    result = db.fetch_all()
    pool_db_conn(db)

    return result


def insert_new_autorole(server_id, role_name):
    db = get_db_conn()

    db.execute("INSERT INTO autorole_groups (guildid, messageid, name) VALUES(%s, 0, %s)", server_id, role_name)
    insert_id = db.last_insert_id()

    pool_db_conn(db)
    return insert_id


def insert_role_for_autorole(guild_id, autorole_id, role):
    db = get_db_conn()

    db.execute("INSERT INTO autorole_entries (guildid, emote, roleid, rolegroupid) VALUES(%s, %s, %s, %s)",
               guild_id, role['roleEmoji'], role['roleId'], autorole_id)

    pool_db_conn(db)
