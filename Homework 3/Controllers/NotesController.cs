using CloudNote.Models;
using CloudNote.Services;
using Microsoft.AspNetCore.Mvc;

namespace CloudNote.Controllers;

public class NotesController(NoteService noteService, FirestoreService firestore) : Controller
{
    // Dashboard

    [HttpGet("/dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");

        if (userId is null) return RedirectToAction("Index", "Home");

        var myNotes      = await firestore.GetNotesByOwnerAsync(userId);
        var sharedNotes  = await firestore.GetNotesSharedWithAsync(email!);
        var notifications = await firestore.GetNotificationsAsync(userId);
        var profile      = await firestore.GetUserAsync(userId);

        var vm = new DashboardViewModel
        {
            MyNotes       = myNotes,
            SharedWithMe  = sharedNotes,
            Notifications = notifications,
            CurrentUser   = profile
        };

        return View(vm);
    }

    // Create

    [HttpGet("/notes/create")]
    public IActionResult Create()
    {
        if (HttpContext.Session.GetString("UserId") is null)
            return RedirectToAction("Index", "Home");
        return View(new NoteCreateViewModel());
    }

    [HttpPost("/notes/create")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Create(NoteCreateViewModel vm)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");
        if (userId is null) return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid) return View(vm);

        await noteService.CreateNoteAsync(vm.Title, vm.Content, vm.Tags,
                                            vm.Attachment, userId, email!);

        TempData["Success"] = "Note created and analysed by AI!";
        return RedirectToAction(nameof(Dashboard));
    }

    // Edit

    [HttpGet("/notes/{id}/edit")]
    public async Task<IActionResult> Edit(string id)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        if (userId is null) return RedirectToAction("Index", "Home");

        Note? note = await firestore.GetNoteAsync(id);
        if (note is null || note.OwnerId != userId) return NotFound();

        var vm = new NoteEditViewModel
        {
            Id                       = id,
            Title                    = note.Title,
            Content                  = note.Content,
            Tags                     = string.Join(", ", note.Tags),
            ExistingAttachmentUrl    = note.AttachmentUrl,
            ExistingAttachmentFileName = note.AttachmentFileName
        };
        return View(vm);
    }

    [HttpPost("/notes/{id}/edit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Edit(string id, NoteEditViewModel vm)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");
        if (userId is null) return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid) return View(vm);

        await noteService.UpdateNoteAsync(id, vm.Title, vm.Content, vm.Tags,
                                            vm.Attachment, email!);

        TempData["Success"] = "Note updated.";
        return RedirectToAction(nameof(Dashboard));
    }

    // Delete

    [HttpPost("/notes/{id}/delete")]
    public async Task<IActionResult> Delete(string id)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");
        if (userId is null) return RedirectToAction("Index", "Home");

        Note? note = await firestore.GetNoteAsync(id);
        if (note is null || note.OwnerId != userId) return Forbid();

        await noteService.DeleteNoteAsync(id, email!);
        TempData["Success"] = "Note deleted.";
        return RedirectToAction(nameof(Dashboard));
    }

    // View single note

    [HttpGet("/notes/{id}")]
    public async Task<IActionResult> View(string id)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");
        if (userId is null) return RedirectToAction("Index", "Home");

        Note? note = await firestore.GetNoteAsync(id);
        if (note is null) return NotFound();

        bool canView = note.OwnerId == userId || note.SharedWith.Contains(email!);
        if (!canView) return Forbid();

        return View("NoteDetail", note);
    }

    // Share

    [HttpPost("/notes/{id}/share")]
    public async Task<IActionResult> Share(string id, ShareNoteViewModel vm)
    {
        string? userId = HttpContext.Session.GetString("UserId");
        string? email  = HttpContext.Session.GetString("UserEmail");
        if (userId is null) return RedirectToAction("Index", "Home");

        Note? note = await firestore.GetNoteAsync(id);
        if (note is null || note.OwnerId != userId) return Forbid();

        await noteService.ShareNoteAsync(id, vm.ShareWithEmail, email!);
        TempData["Success"] = $"Note shared with {vm.ShareWithEmail}";
        return RedirectToAction(nameof(Dashboard));
    }

    // Mark notifications read

    [HttpPost("/notifications/read")]
    public async Task<IActionResult> MarkRead()
    {
        string? userId = HttpContext.Session.GetString("UserId");
        if (userId is null) return Unauthorized();

        await firestore.MarkNotificationsReadAsync(userId);
        return Ok();
    }
}
