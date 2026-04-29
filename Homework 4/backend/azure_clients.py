"""
Azure service client singletons.
All configuration is read from environment variables (set via App Service
Application Settings or a local .env for development).
"""

import os
from dotenv import load_dotenv
from azure.cosmos import CosmosClient, PartitionKey
from azure.storage.blob import BlobServiceClient
from azure.servicebus import ServiceBusClient
from azure.ai.textanalytics import TextAnalyticsClient
from azure.core.credentials import AzureKeyCredential

load_dotenv()

# ── Cosmos DB ────────────────────────────────────────────────────────────────
_cosmos = CosmosClient(
    url=os.environ["COSMOS_URI"],
    credential=os.environ["COSMOS_KEY"],
)
_db = _cosmos.get_database_client(os.environ.get("COSMOS_DB", "bookvault"))

books_container   = _db.get_container_client("books")
reviews_container = _db.get_container_client("reviews")
users_container   = _db.get_container_client("users")

def init_cosmos():
    """Create database + containers if they don't exist (idempotent)."""
    db = _cosmos.create_database_if_not_exists("bookvault")
    for name, pk in [("books", "/genre"), ("reviews", "/bookId"), ("users", "/id")]:
        db.create_container_if_not_exists(id=name, partition_key=PartitionKey(path=pk))

# ── Blob Storage ─────────────────────────────────────────────────────────────
blob_service = BlobServiceClient.from_connection_string(
    os.environ["AZURE_STORAGE_CONNECTION_STRING"]
)
COVER_CONTAINER = os.environ.get("BLOB_COVER_CONTAINER", "book-covers")

def get_cover_container():
    cc = blob_service.get_container_client(COVER_CONTAINER)
    try:
        cc.create_container(public_access="blob")
    except Exception:
        pass  # already exists
    return cc

# ── Service Bus ──────────────────────────────────────────────────────────────
_sb_client = ServiceBusClient.from_connection_string(
    os.environ["SERVICEBUS_CONNECTION_STRING"]
)
REVIEW_QUEUE = os.environ.get("SERVICEBUS_REVIEW_QUEUE", "new-reviews")

def get_sb_sender():
    return _sb_client.get_queue_sender(queue_name=REVIEW_QUEUE)

def get_sb_receiver():
    return _sb_client.get_queue_receiver(queue_name=REVIEW_QUEUE, max_wait_time=5)

# ── Azure AI Text Analytics ───────────────────────────────────────────────────
_ta_endpoint = os.environ["AZURE_TEXT_ANALYTICS_ENDPOINT"]
_ta_key      = os.environ["AZURE_TEXT_ANALYTICS_KEY"]
text_analytics = TextAnalyticsClient(
    endpoint=_ta_endpoint,
    credential=AzureKeyCredential(_ta_key),
)
