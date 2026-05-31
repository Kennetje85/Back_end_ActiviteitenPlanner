using System;
using System.IO;
using System.Text;
using System.Threading;
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

// DbContext - prefer configuration values (appsettings: ConnectionStrings:DefaultConnection)
// This uses SQL authentication connection strings (User ID / Password) as requested.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("CONNECTION_STRING")
    ?? throw new InvalidOperationException("Connection string not configured. Set ConnectionStrings:DefaultConnection in appsettings or CONNECTION_STRING env var.");

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

// Apply EF Core migrations automatically at startup with basic retry
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<AppDbContext>();

    const int maxAttempts = 5;
    for (int attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            logger.LogInformation("Attempting database migration (attempt {Attempt}/{Max}).", attempt, maxAttempts);
            db.Database.Migrate();
            logger.LogInformation("Database migration completed.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration attempt {Attempt} failed.", attempt);
            if (attempt == maxAttempts)
            {
                logger.LogError(ex, "Exceeded max migration attempts; rethrowing.");
                throw;
            }

            // exponential backoff
            var delaySeconds = Math.Pow(2, attempt);
            logger.LogInformation("Waiting {Delay}s before retrying migration.", delaySeconds);
            Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
        }
    }
}

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
