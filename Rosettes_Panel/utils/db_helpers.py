from typing import List, Optional

from core.database import db_fetch_one, db_fetch_all


def get_user_data(user_id: int) -> Optional[dict]:
    return db_fetch_one("SELECT * FROM users WHERE id = %s", user_id)


def get_server_data(server_id: int) -> Optional[dict]:
    return db_fetch_one("SELECT * FROM guilds WHERE id = %s", server_id)


def get_owned_servers(user_id: int) -> List[dict]:
    return db_fetch_all("SELECT * FROM guilds WHERE ownerid = %s", user_id)


def get_server_roles(server_id: int) -> List[dict]:
    return db_fetch_all("SELECT * FROM roles WHERE guildid = %s", server_id)


def get_server_autoroles(server_id: int) -> List[dict]:
    return db_fetch_all("SELECT * FROM autorole_groups WHERE guildid = %s", server_id)


def get_app_by_name(name: str) -> Optional[dict]:
    return db_fetch_one("SELECT * FROM app_auth WHERE name = %s", name)


def get_app_by_id(app_id: int) -> Optional[dict]:
    return db_fetch_one("SELECT * FROM app_auth WHERE id = %s", app_id)



def get_apps_for_user(user_id: int) -> List[dict]:
    return db_fetch_all("SELECT * FROM app_auth WHERE owner_id = %s", user_id)


def get_users_for_app(app_id: int) -> List[dict]:
    return db_fetch_all("SELECT u.* FROM users AS u "
                        "INNER JOIN app_auth_rel AS r ON r.user_id = u.id "
                        "WHERE r.app_id = %s", app_id)

