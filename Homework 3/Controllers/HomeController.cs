using CloudNote.Models;
using CloudNote.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;

namespace CloudNote.Controllers;

public class HomeController(
    FirestoreService firestore,
    IConfiguration config,
    ILogger<HomeController> logger)
    : Controller
{
    [HttpGet("/")]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("UserId") is not null)
            return RedirectToAction("Dashboard", "Notes");
        return View();
    }

    
    [HttpPost("/auth/google")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        try
        {
            GoogleJsonWebSignature.ValidationSettings settings = new()
            {
                Audience = new[] { config["GoogleAuth:ClientId"] }
            };

            GoogleJsonWebSignature.Payload payload =
                await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            var user = new UserProfile
            {
                Id          = payload.Subject,
                Email       = payload.Email,
                DisplayName = payload.Name,
                PhotoUrl    = payload.Picture
            };
            await firestore.UpsertUserAsync(user);

            HttpContext.Session.SetString("UserId",    payload.Subject);
            HttpContext.Session.SetString("UserEmail", payload.Email);
            HttpContext.Session.SetString("UserName",  payload.Name);
            HttpContext.Session.SetString("UserPhoto", payload.Picture ?? "");

            return Ok(new { success = true });
        }
        catch (InvalidJwtException ex)
        {
            logger.LogWarning(ex, "Invalid Google ID token");
            return BadRequest(new { error = "Invalid token." });
        }
    }

    [HttpPost("/auth/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Index));
    }
}

public record GoogleLoginRequest(string IdToken);
