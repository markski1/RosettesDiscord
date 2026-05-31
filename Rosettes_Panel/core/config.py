import os
from dotenv import load_dotenv

load_dotenv()

app_host = os.getenv('APP_HOST')
app_port = int(os.getenv('APP_PORT', 5250))
app_debug = os.getenv('APP_DEBUG', 'False').strip().lower() in ('1', 'true', 'yes', 'on')
