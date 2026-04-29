"""
BookVault – Azure Multi-Tier Application
Backend: Python 3.11 / Flask
Services: Azure App Service, Cosmos DB, Blob Storage,
          Azure AI Text Analytics, Azure Service Bus
"""

from flask import Flask
from flask_cors import CORS
from dotenv import load_dotenv

from routes.books   import books_bp
from routes.reviews import reviews_bp
from routes.users   import users_bp
from routes.search  import search_bp

load_dotenv()

app = Flask(__name__)
CORS(app, resources={r"/api/": {"origins": ""}})

app.register_blueprint(books_bp,   url_prefix="/api/books")
app.register_blueprint(reviews_bp, url_prefix="/api/reviews")
app.register_blueprint(users_bp,   url_prefix="/api/users")
app.register_blueprint(search_bp,  url_prefix="/api/search")


@app.route("/api/health")
def health():
    return {"status": "ok", "service": "BookVault API"}, 200


if __name__ == "__main__":
    app.run(debug=False, host="0.0.0.0", port=8000)
