using MuseumAdmin.Components;
// using MuseumAdmin.Auth;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MuseumAdmin.Services;
using Microsoft.EntityFrameworkCore;
using MuseumAdmin.Data;
using MuseumAdmin.Models;


Console.WriteLine("🚀 MuseumAdmin BUILD: 14-Jan-2026 11:53PM");


var builder = WebApplication.CreateBuilder(args);

// Load Environment Variables (.env) early so all services, cache configs, and auth options have access
var envFile = Path.Combine(builder.Environment.ContentRootPath ?? Directory.GetCurrentDirectory(), ".env");
if (File.Exists(envFile))
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            continue;
        var idx = trimmed.IndexOf('=');
        if (idx <= 0)
            continue;
        var key = trimmed.Substring(0, idx).Trim();
        var val = trimmed.Substring(idx + 1).Trim().Trim('"');
        Environment.SetEnvironmentVariable(key, val);
    }
}

// 1. Add Razor Components
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = true)
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 64 * 1024 * 1024; // 64MB to support large video previews
    });

builder.Services.AddHttpClient();

builder.Services.AddAuthorizationCore();
// builder.Services.AddScoped<ProtectedLocalStorage>();
    builder.Services.AddHttpClient<MuseumAuthService>(client =>
    {
        client.BaseAddress = new Uri("https://membyapi.azurewebsites.net/memby/");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });
    
builder.Services.AddHttpClient<ExhibitService>(client =>
{
    client.BaseAddress = new Uri("https://membyapi.azurewebsites.net/memby/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient<UserService>(client =>
{
    client.BaseAddress = new Uri("https://membyapi.azurewebsites.net/memby/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient<ChatService>(client =>
{
    client.BaseAddress = new Uri("https://membyapi.azurewebsites.net/memby/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});


// Configure caching: prefer Redis in production (set REDIS_CONNECTION_STRING), otherwise use in-memory cache for dev
var redisConn = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConn))
{
    // Use StackExchange.Redis-backed IDistributedCache
    builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConn; });
    builder.Services.AddSingleton<MuseumAdmin.Services.IUserCacheService, MuseumAdmin.Services.RedisUserCacheService>();
}
else
{
    // Development fallback: in-memory cache
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<MuseumAdmin.Services.IUserCacheService, MuseumAdmin.Services.InMemoryUserCacheService>();
}

var sessionTimeoutStr = Environment.GetEnvironmentVariable("SESSION_TIMEOUT_MINUTES");

double sessionTimeoutMinutes = 15; // default fallback (15 minutes in production for HIPAA compliance)

if (!string.IsNullOrWhiteSpace(sessionTimeoutStr) &&
    double.TryParse(sessionTimeoutStr, out var parsedValue))
{
    sessionTimeoutMinutes = parsedValue;
}

// This registers the standard ASP.NET Core Identity/Cookie system
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => 
    {
        options.LoginPath = "/login";
        // HIPAA Security Requirements
        options.Cookie.HttpOnly = true;            // Prevent JS access (XSS protection)
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Secure on HTTPS, compatible on HTTP dev
        options.Cookie.SameSite = SameSiteMode.Lax; // Reliable same-origin navigation & fetch
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionTimeoutMinutes); // Strict HIPAA inactivity session lifetime (15 mins)
        options.SlidingExpiration = true;          // Refresh timer on activity
    });

// This enables the "CascadingAuthState" for Blazor views
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<AppState>();
builder.Services.AddSingleton<NotificationService>();

// 4. Configure Database and Services
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "DataSource=Data/app.db";
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IClinicalNoteService, ClinicalNoteService>();
builder.Services.AddScoped<ITimeTrackingService, TimeTrackingService>();
builder.Services.AddScoped<BulkExerciseImportService>();

// 5. Configure Circuit Retention
var retentionMinutes = 60;
var retentionEnv = Environment.GetEnvironmentVariable("CIRCUIT_RETENTION_MINUTES");
if (!string.IsNullOrEmpty(retentionEnv) && int.TryParse(retentionEnv, out var parsed))
{
    retentionMinutes = parsed;
}

builder.Services.Configure<CircuitOptions>(options =>
{
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(retentionMinutes);
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("🚀 MuseumAdmin BUILD: 14-Jan-2026 11:53PM");

// 5. Configure Middleware Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// IMPORTANT: Authentication must be between UseRouting and UseAntiforgery
app.UseAuthentication();
app.UseAuthorization(); 

app.UseAntiforgery();

// 6. Login & Logout Endpoints (The "Login Desk")
app.MapPost("/account/login", async (
    HttpContext httpContext, 
    [FromForm] string username, 
    [FromForm] string password,
    MuseumAuthService authService) => // Inject the service here
{
        // DEBUG: Print what the server received from the HTML Form
    var loginRequest = new MuseumAdmin.Models.LoginRequest 
    { 
        UserText = username, 
        Password = password 
    };

    // Call the real API
    var result = await authService.LoginAsync(loginRequest);

//  print result to console for debugging
    Console.WriteLine($"Login result: {result?.Message ?? "No result"}");
    if (result != null && result.Status && result.StatusCode == 200)
    {
        // Create the user session
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim("Token", result.Token), // Securely store token in the cookie
            // Add safe null checks for User/Museum properties
            new Claim("MuseumId", result.User?.MuseumId.ToString() ?? "0"),
            new Claim("MuseumName", result.Museum?.MuseumName ?? string.Empty),
            new Claim("UserId", result.User?.Id.ToString() ?? "0"),
            new Claim(ClaimTypes.Role, result.User?.Role ?? "Admin")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProps = new AuthenticationProperties
        {
            IsPersistent = false, // HIPAA compliance: Do NOT store persistent session across browser restarts
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(sessionTimeoutMinutes)
        };

        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);
        
        // Redirect based on role
        var role = result.User?.Role ?? "Admin";
        if (role == "Therapist")
        {
            return Results.Redirect("/programs");
        }

        return Results.Redirect("/home");
    }

    // Login failed
    return Results.Redirect("/login?error=InvalidCredentials");
});

app.MapGet("/account/keep-alive", async (HttpContext httpContext) =>
{
    var isAuth = httpContext.User.Identity?.IsAuthenticated == true;
    var userName = httpContext.User.Identity?.Name ?? "Anonymous";
    Console.WriteLine($"[KEEP-ALIVE] Request received. IsAuthenticated: {isAuth}, User: {userName}");

    if (!isAuth)
    {
        Console.WriteLine("[KEEP-ALIVE] ❌ Rejected: User is not authenticated.");
        return Results.Unauthorized();
    }
    
    // Explicitly re-issue the cookie ticket to reset sliding expiration on the server
    var authProps = new AuthenticationProperties
    {
        IsPersistent = false, // HIPAA compliance: Do NOT store persistent session across browser restarts
        IssuedUtc = DateTimeOffset.UtcNow,
        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(sessionTimeoutMinutes)
    };

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme, 
        httpContext.User,
        authProps);

    Console.WriteLine($"[KEEP-ALIVE] ✅ Successfully refreshed authentication cookie for user: {userName}");
    return Results.Ok(new { status = "active", timestamp = DateTime.UtcNow });
});

app.MapGet("/account/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    var message = httpContext.Request.Query["message"].ToString();
    var userName = httpContext.Request.Query["userName"].ToString();

    var queryParams = new List<string>();
    if (!string.IsNullOrEmpty(message))
    {
        queryParams.Add($"message={Uri.EscapeDataString(message)}");
    }
    if (!string.IsNullOrEmpty(userName))
    {
        queryParams.Add($"userName={Uri.EscapeDataString(userName)}");
    }

    if (queryParams.Count > 0)
    {
        return Results.Redirect($"/login?{string.Join("&", queryParams)}");
    }
    return Results.Redirect("/login");
});

// 7. Map Blazor Components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
