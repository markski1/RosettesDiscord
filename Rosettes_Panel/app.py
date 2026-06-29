from flask import Flask

from core import session
from core.config import app_host, app_port, app_debug
from core.csrf import csrf_input, validate_csrf
from routes.home import home_bp
from routes.panel import panel_bp
from routes.action import action_bp
from routes.session import session_bp

app = Flask(
        __name__,
        static_folder="static",
        template_folder="templates"
    )

app.register_blueprint(home_bp)
app.register_blueprint(session_bp)
app.register_blueprint(panel_bp)
app.register_blueprint(action_bp)

session.init_app(app)

app.jinja_env.globals["csrf_input"] = csrf_input
app.before_request(validate_csrf)

if __name__ == "__main__":
    app.run(host=app_host, port=app_port, debug=app_debug)
