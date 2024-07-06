from flask import Blueprint, render_template, request

from utils.miscfuncs import htmx_check

panel_bp = Blueprint("panel", __name__, url_prefix="/panel")


@panel_bp.route("/")
@htmx_check
def index(is_htmx):
    return "Panel login - Not yet implemented"


@panel_bp.post("/login")
def login():
    key = request.form["key"]

    if not key:
        return "Invalid key"