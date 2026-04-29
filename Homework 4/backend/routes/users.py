"""
/api/users  –  User profiles & preferences.
"""

import uuid
from flask import Blueprint, request, jsonify, abort

from azure_clients import users_container

users_bp = Blueprint("users", __name__)


@users_bp.route("/", methods=["POST"])
def create_user():
    data = request.get_json(force=True)
    if not data.get("email"):
        abort(400, "email is required")

    user = {
        "id":              str(uuid.uuid4()),
        "email":           data["email"],
        "displayName":     data.get("displayName", data["email"].split("@")[0]),
        "favoriteGenres":  data.get("favoriteGenres", []),
        "readBookIds":     [],
        "wishlistBookIds": [],
    }
    users_container.create_item(user)
    return jsonify(user), 201


@users_bp.route("/<user_id>", methods=["GET"])
def get_user(user_id):
    items = list(users_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": user_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    return jsonify(items[0])


@users_bp.route("/<user_id>", methods=["PATCH"])
def update_user(user_id):
    items = list(users_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": user_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    user = items[0]
    for k, v in (request.get_json(force=True) or {}).items():
        if k != "id":
            user[k] = v
    users_container.replace_item(item=user["id"], body=user)
    return jsonify(user)


@users_bp.route("/<user_id>/wishlist/<book_id>", methods=["PUT"])
def add_to_wishlist(user_id, book_id):
    items = list(users_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": user_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    user = items[0]
    if book_id not in user["wishlistBookIds"]:
        user["wishlistBookIds"].append(book_id)
        users_container.replace_item(item=user["id"], body=user)
    return jsonify({"wishlist": user["wishlistBookIds"]})
