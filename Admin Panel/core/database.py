import os
from dotenv import load_dotenv
import mysql.connector

load_dotenv()

db_user = os.getenv('DB_USER')
db_password = os.getenv('DB_PASS')
db_host = os.getenv('DB_HOST')
db_database = os.getenv('DB_NAME')


class Database:
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

    def execute(self, query, *params):
        if not self.conn.is_connected():
            self.conn.reconnect()
            self.cursor = self.conn.cursor(buffered=True)

        try:
            self.cursor.execute(query, params)
        except mysql.connector.errors.DatabaseError:
            self.conn.disconnect()
            self.conn.reconnect()
            self.cursor = self.conn.cursor(buffered=True)
            self.cursor.execute(query, params)

    def fetch_all(self):
        columns = [col[0] for col in self.cursor.description]
        results = [dict(zip(columns, row)) for row in self.cursor.fetchall()]
        return results

    def fetch_one(self):
        columns = [col[0] for col in self.cursor.description]
        row = self.cursor.fetchone()
        if row is None:
            return None
        else:
            result = dict(zip(columns, row))
            return result
