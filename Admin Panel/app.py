from flask import Flask

from config import app_host, app_port, app_debug
from routes.home import home_bp
from routes.panel import panel_bp


app = Flask(
        __name__,
        static_folder="static",
        template_folder="templates"
    )

app.register_blueprint(home_bp)
app.register_blueprint(panel_bp)

if __name__ == "__main__":
    app.run(host=app_host, port=app_port, debug=app_debug)
