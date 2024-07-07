from flask_login import (
    LoginManager,
    UserMixin
)

from core.database import Database
from dotenv import load_dotenv

load_dotenv()


def init_app(app):
    login_manager = LoginManager()
    login_manager.init_app(app)
    login_manager.session_protection = "strong"

    @login_manager.user_loader
    def user_loader(load_id):
        # TODO: Token de sesión
        user_model = Session()
        user_model.id = load_id
        return user_model

    return None


def attempt_login(auth_key):
    db = Database()
    db.execute("SELECT id FROM login_keys WHERE login_key = %s", auth_key)
    result = db.fetch_one()

    if result:
        db.execute("SELECT * FROM users WHERE id = %s", result[0])
        user_data = db.fetch_one()

        user_model = Session()
        user_model.id = result[0]
        user_model.name = user_data['username']
        return user_model

    return None


class Session(UserMixin):
    ...
