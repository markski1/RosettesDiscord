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
    return Database()


def db_execute(query: str, *params) -> Optional[int]:
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.cursor.lastrowid
    finally:
        db.pool()


def db_fetch_one(query: str, *params) -> Optional[dict]:
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.fetch_one()
    finally:
        db.pool()


def db_fetch_all(query: str, *params) -> List[dict]:
    db = get_db_conn()
    try:
        db.get_cursor().execute(query, params)
        return db.fetch_all()
    finally:
        db.pool()
