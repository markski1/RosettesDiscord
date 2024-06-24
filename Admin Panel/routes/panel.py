from flask import Blueprint, render_template

from utils.miscfuncs import htmx_check

basic_bp = Blueprint("panel", __name__, url_prefix="/panel")


@basic_bp.route("/")
@htmx_check
def index(is_htmx):
    return "Panel login - Not yet implemented"
