import os
from typing import List, Optional

from dotenv import load_dotenv
import mysql.connector

load_dotenv()

db_user = os.getenv('DB_USER')
db_password = os.getenv('DB_PASS')
db_host = os.getenv('DB_HOST')
db_database = os.getenv('DB_NAME')

db_pool = []


class Database:
    """
        A database connection.
        Can be interacted with directly if fetched through get_database(), but I'd recommend
        just using the db_* helpers when possible.
    """
    def __init__(self):
        db_config = {
            'user': db_user,
            'password': db_password,
            'host': db_host,
            'database': db_database
        }

        self.conn = mysql.connector.connect(**db_config)
        self.conn.autocommit = True
        self.cursor = self.conn.cursor(buffered=True)

    def get_cursor(self):
        if not self.conn.is_connected():
            self.conn.reconnect()
            self.conn.autocommit = True
            self.cursor = self.conn.cursor(buffered=True)

        return self.cursor

    def fix_cursor(self):
        self.conn.disconnect()
        self.conn.reconnect()
        self.conn.autocommit = True
        self.cursor = self.conn.cursor(buffered=True)
        return self.cursor

    def execute(self, query, *params) -> None:
        if not self.conn.is_connected():
            self.conn.reconnect()
            self.conn.autocommit = True
            self.cursor = self.conn.cursor(buffered=True)

        self.cursor.execute(query, params)

    def fetch_all(self) -> List[dict]:
        """
        Returns all resulting rows of the previous query.
        :return: A list of dicts, keyed after the name of the resulting columns.
        """
        columns = [col[0] for col in self.cursor.description]
        results = [dict(zip(columns, row)) for row in self.cursor.fetchall()]
        return results

    def fetch_one(self) -> Optional[dict]:
        """
        Returns a single result of the previously executed query.
        :return: A dictionary keyed after the name of each resulting column. Or none if no results.
        """
        columns = [col[0] for col in self.cursor.description]
        row = self.cursor.fetchone()
        if row is None:
            return None
        else:
            result = dict(zip(columns, row))
            return result

    def last_insert_id(self) -> Optional[int]:
        """
        Retrieves the last row ID inserted into the database.
        :return: The ID of the last inserted row or None if not available.
        """
        return self.cursor.lastrowid

    def pool(self) -> None:
        """
        Returns the database connection to the pool.
        """
        global db_pool
        if len(db_pool) < 10:
            db_pool.append(self)
            # do 'fetchall()' to clear the buffer. This has caused stale data issues.
            try:
                self.cursor.fetchall()
            except:
                return

    def is_healthy(self) -> bool:
        """
        Mostly for internal use. Returns if the database connection is active.
        :return: True or False.
        """
        if self.conn.is_connected():
            return True

        return False


# Helpers


def get_db_conn() -> Database:
    """
    Fetches a pooled database connection if available or starts a new one.
    :return: A Database object.
    """
    global db_pool

    while len(db_pool) > 0:
        db = db_pool.pop()

        if db.is_healthy():
            return db

    return Database()


def db_execute(query: str, *params) -> Optional[int]:
    """
    Executes a query.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: If an INSERT, returns the inserted row ID, otherwise None.
    """
    db = get_db_conn()
    cursor = db.get_cursor()
    try:
        cursor.execute(query, params)
    except:
        cursor = db.fix_cursor()
        cursor.execute(query, params)

    value = db.cursor.lastrowid
    db.pool()
    return value


def db_fetch_one(query: str, *params) -> Optional[dict]:
    """
    Executes a query and returns the first or only row.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: A dictionary, where each key is the name for each field. Or None, if no results.
    """
    db = get_db_conn()
    cursor = db.get_cursor()
    try:
        cursor.execute(query, params)
    except:
        cursor = db.fix_cursor()
        cursor.execute(query, params)
    result = db.fetch_one()
    db.pool()
    return result


def db_fetch_all(query: str, *params) -> List[dict]:
    """
    Executes a query and returns every row.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: A list of dictionaries, one per row, where each key is the name for each field.
    """
    db = get_db_conn()
    cursor = db.get_cursor()
    try:
        cursor.execute(query, params)
    except:
        cursor = db.fix_cursor()
        cursor.execute(query, params)
    result = db.fetch_all()
    db.pool()
    return result
