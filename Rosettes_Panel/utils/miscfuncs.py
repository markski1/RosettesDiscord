import string
from functools import wraps
import secrets

from flask_login import current_user

from utils.db_helpers import get_server_data
from utils.page_helpers import render_error


def generate_random_string(length):
    length = int(length)
    alphanumeric_characters = string.ascii_letters + string.digits
    return ''.join(secrets.choice(alphanumeric_characters) for _ in range(length))


def ownership_required(func):
    @wraps(func)
    def wrapper(*args, **kwargs):
        server = get_server_data(kwargs.get('server_id'))
        if not server:
            return render_error("Rosettes has no knowledge of this server.")

        if int(current_user.id) != int(server['ownerid']):
            return render_error("You don't own this server.")

        return func(*args, **kwargs)

    return wrapper
