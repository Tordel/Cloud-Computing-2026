"""
/api/search  –  Full-text search across books (title + author + description).
Uses Cosmos DB CONTAINS queries (simple; swap for Azure Cognitive Search for production).
"""

from flask import Blueprint, request, jsonify
from azure_clients import books_container

search_bp = Blueprint("search", __name__)


@search_bp.route("/", methods=["GET"])
def search():
    q      = request.args.get("q", "").strip()
    limit  = int(request.args.get("limit", 10))

    if not q:
        return jsonify([])

    # Case-insensitive CONTAINS on key fields
    query = """
        SELECT * FROM c
        WHERE CONTAINS(LOWER(c.title),  @q)
           OR CONTAINS(LOWER(c.author), @q)
           OR CONTAINS(LOWER(c.description), @q)
        OFFSET 0 LIMIT @limit
    """
    items = list(books_container.query_items(
        query=query,
        parameters=[
            {"name": "@q",     "value": q.lower()},
            {"name": "@limit", "value": limit},
        ],
        enable_cross_partition_query=True,
    ))
    return jsonify(items)
