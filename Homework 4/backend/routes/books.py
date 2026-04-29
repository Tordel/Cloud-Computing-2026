"""
/api/books  –  CRUD for books.
Cover images are uploaded to Azure Blob Storage.
"""

import uuid, json
from flask import Blueprint, request, jsonify, abort
from azure.cosmos.exceptions import CosmosResourceNotFoundError

from azure_clients import books_container, get_cover_container

books_bp = Blueprint("books", __name__)


# ── List / create ─────────────────────────────────────────────────────────────
@books_bp.route("/", methods=["GET"])
def list_books():
    genre  = request.args.get("genre")
    limit  = int(request.args.get("limit", 20))
    offset = int(request.args.get("offset", 0))

    if genre:
        query  = "SELECT * FROM c WHERE c.genre = @genre OFFSET @offset LIMIT @limit"
        params = [{"name": "@genre", "value": genre},
                  {"name": "@offset", "value": offset},
                  {"name": "@limit",  "value": limit}]
    else:
        query  = "SELECT * FROM c OFFSET @offset LIMIT @limit"
        params = [{"name": "@offset", "value": offset},
                  {"name": "@limit",  "value": limit}]

    items = list(books_container.query_items(query=query, parameters=params,
                                             enable_cross_partition_query=True))
    return jsonify(items)


@books_bp.route("/", methods=["POST"])
def create_book():
    data = request.get_json(force=True)
    required = {"title", "author", "genre", "year"}
    if not required.issubset(data):
        abort(400, f"Missing fields: {required - data.keys()}")

    book = {
        "id":          str(uuid.uuid4()),
        "title":       data["title"],
        "author":      data["author"],
        "genre":       data["genre"],
        "year":        data["year"],
        "description": data.get("description", ""),
        "coverUrl":    None,
        "avgRating":   0.0,
        "reviewCount": 0,
    }
    books_container.create_item(book)
    return jsonify(book), 201


# ── Single book ───────────────────────────────────────────────────────────────
@books_bp.route("/<book_id>", methods=["GET"])
def get_book(book_id):
    items = list(books_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404, "Book not found")
    return jsonify(items[0])


@books_bp.route("/<book_id>", methods=["PATCH"])
def update_book(book_id):
    items = list(books_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    book = items[0]
    patch = request.get_json(force=True)
    for k, v in patch.items():
        if k not in ("id",):          # protect immutable fields
            book[k] = v
    books_container.replace_item(item=book["id"], body=book)
    return jsonify(book)


@books_bp.route("/<book_id>", methods=["DELETE"])
def delete_book(book_id):
    items = list(books_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    book = items[0]
    books_container.delete_item(item=book["id"], partition_key=book["genre"])
    return "", 204


# ── Cover upload → Blob Storage ───────────────────────────────────────────────
@books_bp.route("/<book_id>/cover", methods=["PUT"])
def upload_cover(book_id):
    """
    Accepts multipart/form-data with a 'cover' file field.
    Uploads to Azure Blob Storage and stores the public URL on the book.
    """
    if "cover" not in request.files:
        abort(400, "No 'cover' file in request")

    file = request.files["cover"]
    if file.mimetype not in ("image/jpeg", "image/png", "image/webp"):
        abort(415, "Unsupported image type")

    # Fetch book (cross-partition)
    items = list(books_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    book = items[0]

    ext        = file.mimetype.split("/")[-1]
    blob_name  = f"{book_id}.{ext}"
    cc         = get_cover_container()
    blob_client = cc.get_blob_client(blob_name)
    blob_client.upload_blob(file.stream, overwrite=True,
                            content_settings={"content_type": file.mimetype})

    book["coverUrl"] = blob_client.url

    books_container.replace_item(
        item=book["id"],
        body=book,
        partition_key=book["genre"]
    )

    return jsonify({"coverUrl": book["coverUrl"]}), 200
