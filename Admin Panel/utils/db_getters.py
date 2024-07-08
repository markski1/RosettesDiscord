from core.database import Database


def get_user_data(user_id):
    db = Database()
    db.execute("SELECT * FROM users WHERE id = %s", user_id)
    try:
        return db.fetch_one()
    except:
        return None


def get_owned_servers(user_id):
    db = Database()
    db.execute("SELECT * FROM guilds WHERE ownerid = %s", user_id)
    try:
        return db.fetch_all()
    except:
        return None


def get_server_roles(server_id):
    db = Database()
    db.execute("SELECT * FROM roles WHERE guildid = %s", server_id)
    try:
        return db.fetch_all()
    except:
        return None


def get_server_commands(server_id):
    db = Database()
    db.execute("SELECT * FROM custom_cmds WHERE guildid = %s", server_id)
    try:
        return db.fetch_all()
    except:
        return None
