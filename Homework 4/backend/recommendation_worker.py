"""
recommendation_worker.py
────────────────────────
Background worker that consumes review events from Azure Service Bus
and updates per-user recommendation scores stored in Cosmos DB.

Run separately (e.g. as a background App Service WebJob or a Container):
    python recommendation_worker.py

Algorithm (simple collaborative score):
  For each inbound review event:
    1. Load the reviewed book's genre.
    2. Fetch all books in the same genre.
    3. Score each candidate = avgRating * log(reviewCount + 1).
    4. Upsert a "recommendations" document per user in Cosmos DB.
"""

import os, json, math, time, logging
from dotenv import load_dotenv

load_dotenv()

from azure.servicebus import ServiceBusClient
from azure.cosmos import CosmosClient, PartitionKey

logging.basicConfig(level=logging.INFO, format="%(asctime)s [WORKER] %(message)s")
log = logging.getLogger(__name__)

# ── Clients ───────────────────────────────────────────────────────────────────
_sb  = ServiceBusClient.from_connection_string(os.environ["SERVICEBUS_CONNECTION_STRING"])
QUEUE = os.environ.get("SERVICEBUS_REVIEW_QUEUE", "new-reviews")

_cosmos = CosmosClient(os.environ["COSMOS_URI"], os.environ["COSMOS_KEY"])
_db     = _cosmos.get_database_client("bookvault")
books_c  = _db.get_container_client("books")
recs_c   = _db.create_container_if_not_exists(
    id="recommendations",
    partition_key=PartitionKey(path="/userId"),
)


def get_genre(book_id: str) -> str | None:
    items = list(books_c.query_items(
        query="SELECT c.genre FROM c WHERE c.id = @id",
        parameters=[{"name": "@id", "value": book_id}],
        enable_cross_partition_query=True,
    ))
    return items[0]["genre"] if items else None


def score_books(genre: str) -> list[dict]:
    candidates = list(books_c.query_items(
        query="SELECT c.id, c.title, c.author, c.coverUrl, c.avgRating, c.reviewCount "
              "FROM c WHERE c.genre = @genre",
        parameters=[{"name": "@genre", "value": genre}],
        enable_cross_partition_query=True,
    ))
    for b in candidates:
        b["_score"] = round(b.get("avgRating", 0) * math.log(b.get("reviewCount", 0) + 1, 2), 3)
    candidates.sort(key=lambda b: b["_score"], reverse=True)
    return candidates[:10]


def upsert_recommendations(user_id: str, genre: str, books: list[dict]):
    doc_id = f"{user_id}:{genre}"
    doc = {
        "id":            doc_id,
        "userId":        user_id,
        "genre":         genre,
        "recommendations": books,
    }
    recs_c.upsert_item(doc)
    log.info("Updated recommendations for user=%s genre=%s  (%d books)", user_id, genre, len(books))


def process_message(msg_str: str):
    try:
        event = json.loads(msg_str)
    except json.JSONDecodeError:
        log.warning("Malformed message: %s", msg_str)
        return

    book_id = event.get("bookId")
    user_id = event.get("userId")
    if not book_id or not user_id:
        return

    genre = get_genre(book_id)
    if not genre:
        log.warning("Book %s not found, skipping", book_id)
        return

    recommended = score_books(genre)
    upsert_recommendations(user_id, genre, recommended)


def run():
    log.info("Recommendation worker starting, queue=%s", QUEUE)
    with _sb.get_queue_receiver(queue_name=QUEUE, max_wait_time=10) as receiver:
        while True:
            messages = receiver.receive_messages(max_message_count=10, max_wait_time=5)
            if not messages:
                log.debug("No messages, sleeping…")
                time.sleep(2)
                continue

            for msg in messages:
                body = "".join(str(chunk) for chunk in msg.body)
                log.info("Processing message: %s", body[:120])
                try:
                    process_message(body)
                    receiver.complete_message(msg)
                except Exception as exc:
                    log.error("Failed to process message: %s", exc)
                    receiver.abandon_message(msg)


if __name__ == "__main__":
    run()
