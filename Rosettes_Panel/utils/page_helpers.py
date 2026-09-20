from flask import render_template


def render_error(message):
    return render_template("prompts/error.jinja2", message=message)


def render_success(message):
    return render_template("prompts/success.jinja2", message=message)
