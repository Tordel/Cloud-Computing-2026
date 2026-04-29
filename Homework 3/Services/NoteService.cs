using CloudNote.Models;
using Microsoft.AspNetCore.Http;

namespace CloudNote.Services;

public class NoteService(
    FirestoreService firestore,
    CloudStorageService storage,
    PubSubService pubSub,
    NaturalLanguageService nlp,
    ILogger<NoteService> logger)
{
    // Create

    public async Task<string> CreateNoteAsync(
        string title, string content, string rawTags,
        IFormFile? attachment, string ownerId, string ownerEmail)
    {
        NlpResult nlp1 = await nlp.AnalyzeAsync(content);

        List<string> tags = ParseTags(rawTags);
        foreach (string t in nlp1.Tags)
            if (!tags.Contains(t)) tags.Add(t);

        string? attachUrl = null;
        string? attachName = null;
        if (attachment is { Length: > 0 })
        {
            await using Stream stream = attachment.OpenReadStream();
            attachUrl  = await storage.UploadFileAsync(stream, attachment.FileName, attachment.ContentType);
            attachName = attachment.FileName;
        }

        var note = new Note
        {
            Title               = title,
            Content             = content,
            OwnerId             = ownerId,
            OwnerEmail          = ownerEmail,
            Tags                = tags,
            AttachmentUrl       = attachUrl,
            AttachmentFileName  = attachName,
            SentimentScore      = nlp1.SentimentScore,
            SentimentMagnitude  = nlp1.SentimentLabel
        };
        string noteId = await firestore.CreateNoteAsync(note);

        await firestore.IncrementNoteCountAsync(ownerId, 1);

        await SafePublish("note.created", noteId, ownerEmail);

        return noteId;
    }

    // Update

    public async Task UpdateNoteAsync(
        string noteId, string title, string content, string rawTags,
        IFormFile? newAttachment, string actorEmail)
    {
        Note note = await firestore.GetNoteAsync(noteId)
            ?? throw new KeyNotFoundException($"Note {noteId} not found.");

        NlpResult nlp1 = await nlp.AnalyzeAsync(content);

        List<string> tags = ParseTags(rawTags);
        foreach (string t in nlp1.Tags)
            if (!tags.Contains(t)) tags.Add(t);

        if (newAttachment is { Length: > 0 })
        {
            if (!string.IsNullOrEmpty(note.AttachmentUrl))
                await storage.DeleteFileAsync(note.AttachmentUrl);

            await using Stream stream = newAttachment.OpenReadStream();
            note.AttachmentUrl       = await storage.UploadFileAsync(stream, newAttachment.FileName, newAttachment.ContentType);
            note.AttachmentFileName  = newAttachment.FileName;
        }

        note.Title              = title;
        note.Content            = content;
        note.Tags               = tags;
        note.SentimentScore     = nlp1.SentimentScore;
        note.SentimentMagnitude = nlp1.SentimentLabel;

        await firestore.UpdateNoteAsync(note);
        await SafePublish("note.updated", noteId, actorEmail);
    }

    // Delete

    public async Task DeleteNoteAsync(string noteId, string actorEmail)
    {
        Note? note = await firestore.GetNoteAsync(noteId);
        if (note is null) return;

        if (!string.IsNullOrEmpty(note.AttachmentUrl))
            await storage.DeleteFileAsync(note.AttachmentUrl);

        await firestore.DeleteNoteAsync(noteId);
        await firestore.IncrementNoteCountAsync(note.OwnerId, -1);
        await SafePublish("note.deleted", noteId, actorEmail);
    }

    // Share

    public async Task ShareNoteAsync(string noteId, string targetEmail, string actorEmail)
    {
        await firestore.ShareNoteAsync(noteId, targetEmail);

        var targetUser = await firestore.GetUserByEmailAsync(targetEmail);
        if (targetUser is not null)
        {
            await firestore.CreateNotificationAsync(new Models.Notification
            {
                UserId  = targetUser.Id!,
                Message = $"{actorEmail} shared a note with you.",
                NoteId  = noteId,
                Type    = "shared",
                IsRead  = false
            });
        }

        await SafePublish("note.shared", noteId, actorEmail, targetEmail);
    }

    // Helpers

    private static List<string> ParseTags(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
              .Select(t => t.Trim().ToLowerInvariant())
              .Where(t => t.Length > 0)
              .Distinct()
              .ToList();

    private async Task SafePublish(string type, string noteId, string actor, string? target = null)
    {
        try { await pubSub.PublishNoteEventAsync(type, noteId, actor, target); }
        catch (Exception ex) { logger.LogWarning(ex, "Pub/Sub publish failed for {type}", type); }
    }
}
