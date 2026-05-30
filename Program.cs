using System;
using System.IO;
using System.Text;
using System.Threading.RateLimiting;
using Backend_ActiviteitenPlanner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Configuration sources: environment overrides appsettings
var config = builder.Configuration;

// Detect runtime and environment variables
var aspnetUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

// If running in a container, configure Kestrel for container runtime.
// Default container port remains unchanged here; use ASPNETCORE_URLS to override if needed.
if (runningInContainer && string.IsNullOrEmpty(aspnetUrls))
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Listen HTTP only inside container to avoid missing dev certs.
        options.ListenAnyIP(80, listenOptions => listenOptions.Protocols = HttpProtocols.Http1AndHttp2);
    });
}
else if (string.IsNullOrEmpty(aspnetUrls))
{
    // Local dev defaults - explicit ports to avoid ephemeral ports from tooling
    builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");
}

// DataProtection keys: persist to a mounted path to survive container restarts.
var keysPath = Environment.GetEnvironmentVariable("DATAPROTECTION_KEYS") ?? "/keys";
try
{
    Directory.CreateDirectory(keysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
        .SetApplicationName("Backend_ActiviteitenPlanner");
}
catch
{
    // Fallback to in-container keys if directory unavailable
}

// CORS for your frontend(s) — explicit origins and allow credentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:5173",
                "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Rate limiting - protect endpoints from bursts
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("global", config =>
    {
        config.PermitLimit = 100; // adjust to needs
        config.Window = TimeSpan.FromSeconds(60);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 50;
    });
    // Use the "global" limiter by default
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromSeconds(60),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 50
        }));
});

// Add controllers and ensure ModelState failures return JSON ValidationProblemDetails
builder.Services.AddControllers();

// Configure automatic JSON response for invalid model state
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var pd = new ValidationProblemDetails(context.ModelState)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        return new BadRequestObjectResult(pd)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

// DbContext - prefer configuration values (environment / appsettings)
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? "Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=False;";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// JWT config (use env/config; do not hardcode in production)
var jwtKey = builder.Configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT__Key") ?? "ThisIsADevSecretKeyChangeMe!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Backend_ActiviteitenPlanner";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

// Configure authentication + JWT bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // production: true
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtIssuer,
        ValidateLifetime = true
    };
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("user", "admin"));
});

// Password hasher used by AuthController
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddAuthorization();

// Build app
var app = builder.Build();

// Security: HSTS + HTTPS redirect in production
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Rate limiter middleware
app.UseRateLimiter();

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    // Simple CSP — tune for your frontend
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' http://localhost:5173; img-src 'self' data:;";
    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowFrontend");

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Root and health endpoints
app.MapGet("/", () => Results.Redirect("/api/health", permanent: false));
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));

// Debug endpoint to check Jwt config presence
app.MapGet("/debug/jwt", (Microsoft.Extensions.Configuration.IConfiguration cfg) =>
{
    var configuredInConfig = !string.IsNullOrEmpty(cfg["Jwt:Key"]);
    var presentInEnv = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JWT__Key"));
    return Results.Ok(new { configuredInConfig, presentInEnv });
}).AllowAnonymous();

app.MapControllers();

app.Run();
