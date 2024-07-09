from flask_login import (
    LoginManager,
    UserMixin
)

from core.database import Database
import os
from dotenv import load_dotenv

load_dotenv()


def init_app(app):
    app.config['SECRET_KEY'] = os.getenv('SECRET_KEY')

    login_manager = LoginManager()
    login_manager.init_app(app)
    login_manager.session_protection = "strong"

    @login_manager.user_loader
    def user_loader(load_id):
        db = Database()
        db.execute("SELECT * FROM users WHERE id = %s", load_id)
        result = db.fetch_one()

        if result:
            user_model = Session()
            user_model.id = load_id
            return user_model

    return None


def attempt_login(auth_key):
    db = Database()
    db.execute("SELECT id FROM login_keys WHERE login_key = %s", auth_key)
    result = db.fetch_one()

    if result:
        user_model = Session()
        user_model.id = result['id']
        return user_model

    return None


class Session(UserMixin):
    ...
