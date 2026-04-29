using Google.Cloud.Firestore;
using CloudNote.Models;

namespace CloudNote.Services;

public class FirestoreService
{
    private readonly FirestoreDb _db;
    private const string NotesCollection = "notes";
    private const string UsersCollection = "users";
    private const string NotificationsCollection = "notifications";

    public FirestoreService(IConfiguration config)
    {
        string projectId = config["GoogleCloud:ProjectId"]
            ?? throw new InvalidOperationException("GoogleCloud:ProjectId not configured.");
        _db = FirestoreDb.Create(projectId);
    }

    // Notes

    public async Task<string> CreateNoteAsync(Note note)
    {
        note.CreatedAt = Timestamp.GetCurrentTimestamp();
        note.UpdatedAt = Timestamp.GetCurrentTimestamp();
        DocumentReference docRef = await _db.Collection(NotesCollection).AddAsync(note);
        return docRef.Id;
    }

    public async Task UpdateNoteAsync(Note note)
    {
        note.UpdatedAt = Timestamp.GetCurrentTimestamp();
        await _db.Collection(NotesCollection).Document(note.Id).SetAsync(note, SetOptions.Overwrite);
    }

    public async Task DeleteNoteAsync(string noteId)
    {
        await _db.Collection(NotesCollection).Document(noteId).DeleteAsync();
    }

    public async Task<Note?> GetNoteAsync(string noteId)
    {
        DocumentSnapshot snap = await _db.Collection(NotesCollection).Document(noteId).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<Note>() : null;
    }

    public async Task<List<Note>> GetNotesByOwnerAsync(string ownerId)
    {
        QuerySnapshot snap = await _db.Collection(NotesCollection)
            .WhereEqualTo("OwnerId", ownerId)
            .WhereEqualTo("IsArchived", false)
            .OrderByDescending("UpdatedAt")
            .GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Note>()).ToList();
    }

    public async Task<List<Note>> GetNotesSharedWithAsync(string email)
    {
        QuerySnapshot snap = await _db.Collection(NotesCollection)
            .WhereArrayContains("SharedWith", email)
            .OrderByDescending("UpdatedAt")
            .GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Note>()).ToList();
    }

    public async Task ShareNoteAsync(string noteId, string email)
    {
        DocumentReference docRef = _db.Collection(NotesCollection).Document(noteId);
        await docRef.UpdateAsync("SharedWith", FieldValue.ArrayUnion(email));
    }

    // Users

    public async Task UpsertUserAsync(UserProfile user)
    {
        user.LastLogin = Timestamp.GetCurrentTimestamp();
        await _db.Collection(UsersCollection).Document(user.Id).SetAsync(user, SetOptions.MergeAll);
    }

    public async Task<UserProfile?> GetUserAsync(string userId)
    {
        DocumentSnapshot snap = await _db.Collection(UsersCollection).Document(userId).GetSnapshotAsync();
        return snap.Exists ? snap.ConvertTo<UserProfile>() : null;
    }

    public async Task<UserProfile?> GetUserByEmailAsync(string email)
    {
        QuerySnapshot snap = await _db.Collection(UsersCollection)
            .WhereEqualTo("Email", email)
            .Limit(1)
            .GetSnapshotAsync();
        return snap.Documents.FirstOrDefault()?.ConvertTo<UserProfile>();
    }

    public async Task IncrementNoteCountAsync(string userId, int delta = 1)
    {
        await _db.Collection(UsersCollection).Document(userId)
            .UpdateAsync("NoteCount", FieldValue.Increment(delta));
    }

    // Notifs

    public async Task CreateNotificationAsync(Notification notification)
    {
        notification.CreatedAt = Timestamp.GetCurrentTimestamp();
        await _db.Collection(NotificationsCollection).AddAsync(notification);
    }

    public async Task<List<Notification>> GetNotificationsAsync(string userId, int limit = 20)
    {
        QuerySnapshot snap = await _db.Collection(NotificationsCollection)
            .WhereEqualTo("UserId", userId)
            .OrderByDescending("CreatedAt")
            .Limit(limit)
            .GetSnapshotAsync();
        return snap.Documents.Select(d => d.ConvertTo<Notification>()).ToList();
    }

    public async Task MarkNotificationsReadAsync(string userId)
    {
        QuerySnapshot snap = await _db.Collection(NotificationsCollection)
            .WhereEqualTo("UserId", userId)
            .WhereEqualTo("IsRead", false)
            .GetSnapshotAsync();

        WriteBatch batch = _db.StartBatch();
        foreach (DocumentSnapshot doc in snap.Documents)
            batch.Update(doc.Reference, "IsRead", true);
        await batch.CommitAsync();
    }
}
