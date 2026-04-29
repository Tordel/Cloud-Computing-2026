using Microsoft.AspNetCore.Http;

namespace CloudNote.Models;

public class NoteCreateViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public IFormFile? Attachment { get; set; }
}

public class NoteEditViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public IFormFile? Attachment { get; set; }
    public string? ExistingAttachmentUrl { get; set; }
    public string? ExistingAttachmentFileName { get; set; }
}

public class ShareNoteViewModel
{
    public string NoteId { get; set; } = string.Empty;
    public string ShareWithEmail { get; set; } = string.Empty;
}

public class DashboardViewModel
{
    public List<Note> MyNotes { get; set; } = new();
    public List<Note> SharedWithMe { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public UserProfile? CurrentUser { get; set; }
    public int TotalNotes => MyNotes.Count;
    public int SharedCount => SharedWithMe.Count;
    public int UnreadNotifications => Notifications.Count(n => !n.IsRead);
}
