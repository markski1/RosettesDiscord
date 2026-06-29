from flask_login import (
    LoginManager,
    UserMixin
)

from core.bot_api import validate_panel_login_key, BotApiError
from core.database import get_db_conn
import os
from dotenv import load_dotenv

load_dotenv()


def init_app(app):
    app.config['SECRET_KEY'] = os.getenv('SECRET_KEY')
    if not app.config['SECRET_KEY']:
        raise RuntimeError("SECRET_KEY is not configured. The panel cannot start without it.")

    app.config.setdefault('SESSION_COOKIE_HTTPONLY', True)
    app.config.setdefault('SESSION_COOKIE_SAMESITE', 'Lax')
    if os.getenv('FLASK_ENV') == 'production' or os.getenv('SESSION_COOKIE_SECURE') == '1':
        app.config['SESSION_COOKIE_SECURE'] = True

    login_manager = LoginManager()
    login_manager.init_app(app)
    login_manager.session_protection = "strong"

    @login_manager.user_loader
    def user_loader(load_id):
        db = get_db_conn()
        db.execute("SELECT * FROM users WHERE id = %s", load_id)
        result = db.fetch_one()
        db.pool()

        if result:
            user_model = Session()
            user_model.id = load_id
            return user_model

        return None


def attempt_login(auth_key):
    try:
        user_id, error_message = validate_panel_login_key(auth_key)
    except BotApiError as exc:
        return None, str(exc)

    if user_id:
        user_model = Session()
        user_model.id = user_id
        return user_model, None

    return None, error_message or "Key does not exist."


class Session(UserMixin):
    ...
