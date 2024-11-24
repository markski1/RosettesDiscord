from flask import Blueprint, render_template, redirect
from flask_login import current_user

from utils.miscfuncs import htmx_check

home_bp = Blueprint("home", __name__, url_prefix="/")


@home_bp.route("/")
@htmx_check
def index(is_htmx):
    if current_user.is_authenticated:
        return redirect("/panel/")

    return render_template("index.jinja2", htmx=is_htmx)
