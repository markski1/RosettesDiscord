import os
from typing import List, Optional

from dotenv import load_dotenv
import mysql.connector
from mysql.connector import pooling

load_dotenv()

DB_POOL_SIZE = 10

_db_pool: pooling.MySQLConnectionPool | None = None


def _db_config() -> dict[str, object]:
    return {
        "user": os.getenv("DB_USER"),
        "password": os.getenv("DB_PASS"),
        "host": os.getenv("DB_HOST"),
        "database": os.getenv("DB_NAME"),
        "autocommit": True,
    }


def _get_pool() -> pooling.MySQLConnectionPool:
    global _db_pool
    if _db_pool is None:
        _db_pool = pooling.MySQLConnectionPool(
            pool_name="rosettes_panel_pool",
            pool_size=DB_POOL_SIZE,
            pool_reset_session=True,
            **_db_config(),
        )
    return _db_pool


class Database:
    """
    Small compatibility wrapper around mysql-connector's built-in pool.
    Prefer the db_* helpers for new code.
    """

    def __init__(self):
        self.conn = _get_pool().get_connection()
        self.cursor = self.conn.cursor(dictionary=True, buffered=True)

    def get_cursor(self):
        if not self.conn.is_connected():
            self.conn.reconnect()
        return self.cursor

    def execute(self, query, *params) -> None:
        self.get_cursor().execute(query, params)

    def fetch_all(self) -> List[dict]:
        return list(self.cursor.fetchall())

    def fetch_one(self) -> Optional[dict]:
        return self.cursor.fetchone()

    def last_insert_id(self) -> Optional[int]:
        return self.cursor.lastrowid

    def pool(self) -> None:
        self.cursor.close()
        self.conn.close()

    def is_healthy(self) -> bool:
        return self.conn.is_connected()


# Helpers


def get_db_conn() -> Database:
    """
    Fetches a pooled database connection.
    :return: A Database object.
    """
    return Database()


def db_execute(query: str, *params) -> Optional[int]:
    """
    Executes a query.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: If an INSERT, returns the inserted row ID, otherwise None.
    """
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.cursor.lastrowid
    finally:
        db.pool()


def db_fetch_one(query: str, *params) -> Optional[dict]:
    """
    Executes a query and returns the first or only row.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: A dictionary keyed after each field name, or None if no results.
    """
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.fetch_one()
    finally:
        db.pool()


def db_fetch_all(query: str, *params) -> List[dict]:
    """
    Executes a query and returns every row.
    :param query: Query to be executed.
    :param params: Parameters to be prepared on execution.
    :return: A list of dictionaries keyed after each field name.
    """
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.fetch_all()
    finally:
        db.pool()
