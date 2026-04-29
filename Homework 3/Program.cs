using CloudNote.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────

builder.Services.AddControllersWithViews();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

// Google Cloud Services
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<CloudStorageService>();
builder.Services.AddSingleton<PubSubService>();
builder.Services.AddSingleton<NaturalLanguageService>();
builder.Services.AddScoped<NoteService>();

// ── App ───────────────────────────────────────────────────────────────────

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapControllers();

// Ensure Pub/Sub topic + subscription exist on startup
using (var scope = app.Services.CreateScope())
{
    var pubSub = scope.ServiceProvider.GetRequiredService<PubSubService>();
    try
    {
        await pubSub.EnsureTopicExistsAsync();
        await pubSub.EnsureSubscriptionExistsAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Pub/Sub initialization warning (non-fatal).");
    }
}

app.Run();
