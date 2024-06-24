from flask import Blueprint, render_template

from utils.miscfuncs import htmx_check

basic_bp = Blueprint("home", __name__, url_prefix="/")


@basic_bp.route("/")
@htmx_check
def index(is_htmx):
    return render_template("index.html", htmx=is_htmx)
