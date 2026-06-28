import os
from dotenv import load_dotenv

load_dotenv()

app_host = os.getenv('APP_HOST')
app_port = int(os.getenv('APP_PORT', 5250))
app_debug = os.getenv('APP_DEBUG', 'False').strip().lower() in ('1', 'true', 'yes', 'on')
bot_api_base_url = os.getenv('BOT_API_BASE_URL', 'http://127.0.0.1:5000')
panel_api_secret = os.getenv('PANEL_API_SECRET', '')
