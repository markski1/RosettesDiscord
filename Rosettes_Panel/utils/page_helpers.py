from flask import render_template


def render_error(message):
    return render_template(
            template_name_or_list="prompts/error.jinja2",
            message=message
        )


def render_success(message):
    return render_template(
            template_name_or_list="prompts/success.jinja2",
            message=message
        )