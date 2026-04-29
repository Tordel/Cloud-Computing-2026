"""
/api/reviews  –  Book reviews with:
  • Azure AI Text Analytics  → sentiment analysis (positive / neutral / negative)
  • Azure Service Bus        → async event emitted on every new review
                               (picked up by the recommendation worker)
"""

import uuid, json
from flask import Blueprint, request, jsonify, abort
from azure.servicebus import ServiceBusMessage

from azure_clients import (
    reviews_container,
    books_container,
    text_analytics,
    get_sb_sender,
)

reviews_bp = Blueprint("reviews", __name__)


def _analyse_sentiment(text: str) -> dict:
    """Call Azure AI Text Analytics and return a compact sentiment result."""
    result = text_analytics.analyze_sentiment([text])[0]
    if result.is_error:
        return {"label": "unknown", "scores": {}}
    return {
        "label":  result.sentiment,         # positive | neutral | negative | mixed
        "scores": {
            "positive": round(result.confidence_scores.positive, 3),
            "neutral":  round(result.confidence_scores.neutral,  3),
            "negative": round(result.confidence_scores.negative, 3),
        },
    }


def _update_book_rating(book_id: str):
    """Recompute average rating for a book after a new review is added."""
    reviews = list(reviews_container.query_items(
        query="SELECT c.rating FROM c WHERE c.bookId = @bid",
        parameters=[{"name": "@bid", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if not reviews:
        return
    avg = sum(r["rating"] for r in reviews) / len(reviews)

    books = list(books_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    if books:
        book = books[0]
        book["avgRating"]   = round(avg, 2)
        book["reviewCount"] = len(reviews)
        books_container.replace_item(item=book["id"], body=book)


# ── List reviews for a book ───────────────────────────────────────────────────
@reviews_bp.route("/book/<book_id>", methods=["GET"])
def list_reviews(book_id):
    limit  = int(request.args.get("limit", 20))
    offset = int(request.args.get("offset", 0))
    items = list(reviews_container.query_items(
        query="SELECT * FROM c WHERE c.bookId = @bid OFFSET @offset LIMIT @limit",
        parameters=[
            {"name": "@bid",    "value": book_id},
            {"name": "@offset", "value": offset},
            {"name": "@limit",  "value": limit},
        ],
        enable_cross_partition_query=True,
    ))
    return jsonify(items)


# ── Create review ─────────────────────────────────────────────────────────────
@reviews_bp.route("/", methods=["POST"])
def create_review():
    data = request.get_json(force=True)
    required = {"bookId", "userId", "rating", "text"}
    if not required.issubset(data):
        abort(400, f"Missing fields: {required - data.keys()}")
    if not (1 <= int(data["rating"]) <= 5):
        abort(400, "rating must be 1-5")

    # --- Sentiment analysis via Azure AI Text Analytics ---
    sentiment = _analyse_sentiment(data["text"])

    review = {
        "id":        str(uuid.uuid4()),
        "bookId":    data["bookId"],
        "userId":    data["userId"],
        "rating":    int(data["rating"]),
        "text":      data["text"],
        "sentiment": sentiment,
    }
    reviews_container.create_item(review)

    # --- Update cached book rating ---
    _update_book_rating(data["bookId"])

    # --- Publish event to Service Bus for the recommendation worker ---
    event_payload = json.dumps({
        "reviewId": review["id"],
        "bookId":   review["bookId"],
        "userId":   review["userId"],
        "rating":   review["rating"],
        "sentiment":sentiment["label"],
    })
    try:
        with get_sb_sender() as sender:
            sender.send_messages(ServiceBusMessage(event_payload))
    except Exception as exc:
        # Non-critical: log and continue (review is already persisted)
        print(f"[WARN] Service Bus send failed: {exc}")

    return jsonify(review), 201


# ── Get single review ─────────────────────────────────────────────────────────
@reviews_bp.route("/<review_id>", methods=["GET"])
def get_review(review_id):
    items = list(reviews_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": review_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    return jsonify(items[0])


# ── Delete review ─────────────────────────────────────────────────────────────
@reviews_bp.route("/<review_id>", methods=["DELETE"])
def delete_review(review_id):
    items = list(reviews_container.query_items(
        query="SELECT * FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": review_id}],
        enable_cross_partition_query=True,
    ))
    if not items:
        abort(404)
    r = items[0]
    reviews_container.delete_item(item=r["id"], partition_key=r["bookId"])
    _update_book_rating(r["bookId"])
    return "", 204
