from flask import Blueprint, render_template, redirect
from flask_login import current_user

home_bp = Blueprint("home", __name__, url_prefix="/")


@home_bp.route("/")
def index():
    if current_user.is_authenticated:
        return redirect("/panel/")

    return render_template("index.jinja2")
