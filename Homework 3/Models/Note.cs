using Google.Cloud.Firestore;

namespace CloudNote.Models;

[FirestoreData]
public class Note
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Content { get; set; } = string.Empty;

    [FirestoreProperty]
    public string OwnerId { get; set; } = string.Empty;

    [FirestoreProperty]
    public string OwnerEmail { get; set; } = string.Empty;

    [FirestoreProperty]
    public List<string> Tags { get; set; } = new();

    [FirestoreProperty]
    public List<string> SharedWith { get; set; } = new();

    [FirestoreProperty]
    public string? AttachmentUrl { get; set; }

    [FirestoreProperty]
    public string? AttachmentFileName { get; set; }

    [FirestoreProperty]
    public float SentimentScore { get; set; }

    [FirestoreProperty]
    public string SentimentMagnitude { get; set; } = "Neutral";

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }

    [FirestoreProperty]
    public Timestamp UpdatedAt { get; set; }

    [FirestoreProperty]
    public bool IsArchived { get; set; }
}

[FirestoreData]
public class UserProfile
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty]
    public string DisplayName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string? PhotoUrl { get; set; }

    [FirestoreProperty]
    public Timestamp LastLogin { get; set; }

    [FirestoreProperty]
    public int NoteCount { get; set; }
}

[FirestoreData]
public class Notification
{
    [FirestoreDocumentId]
    public string? Id { get; set; }

    [FirestoreProperty]
    public string UserId { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Message { get; set; } = string.Empty;

    [FirestoreProperty]
    public string NoteId { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Type { get; set; } = string.Empty; // "shared", "commented", "updated"

    [FirestoreProperty]
    public bool IsRead { get; set; }

    [FirestoreProperty]
    public Timestamp CreatedAt { get; set; }
}
