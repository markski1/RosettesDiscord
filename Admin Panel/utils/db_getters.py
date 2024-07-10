from core.database import get_db_conn, pool_db_conn


def get_user_data(user_id):
    db = get_db_conn()
    db.execute("SELECT * FROM users WHERE id = %s", user_id)

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


def get_server_commands(server_id):
    db = get_db_conn()
    db.execute("SELECT * FROM custom_cmds WHERE guildid = %s", server_id)

    result = db.fetch_all()
    pool_db_conn(db)
    return result
