import string
from functools import wraps
import random

from flask import request
from flask_login import current_user

from utils.db_getters import get_server_data


def htmx_check(func):
    @wraps(func)
    def wrapper(*args, **kwargs):
        if 'HX-Request' in request.headers:
            is_htmx = True
        else:
            is_htmx = False

        return func(is_htmx, *args, **kwargs)

    return wrapper


def generate_random_string(length):
    length = int(length)
    alphanumeric_characters = string.ascii_letters + string.digits
    return ''.join(random.choice(alphanumeric_characters) for _ in range(length))


def ownership_required(func):
    @wraps(func)
    def wrapper(*args, **kwargs):
        server = get_server_data(kwargs.get('server_id'))
        if not server:
            return "Rosettes has no knoweldge of this server."

        if not current_user.id == server['ownerid']:
            return "You don't own this server."

        return func(*args, **kwargs)

    return wrapper
