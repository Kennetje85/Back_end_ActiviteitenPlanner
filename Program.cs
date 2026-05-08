using Backend_ActiviteitenPlanner;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Forceer Kestrel listening URLs (optioneel)
builder.WebHost.UseUrls("http://localhost:5000", "https://localhost:5001");

// CORS voor je frontend(s) — exact origin strings
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
              .AllowAnyMethod();
              // .AllowCredentials(); // uncomment if you must send cookies/credentials (then do NOT use AllowAnyOrigin)
    });
});

builder.Services.AddControllers();

// DbContext
var connectionString = "Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=False;";
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// IMPORTANT: apply CORS before any middleware that might short-circuit (do NOT rely on HttpsRedirection to run before)
app.UseCors("AllowFrontend");

// For development: remove HTTPS redirect to avoid preflight redirects that break CORS
// app.UseHttpsRedirection(); // <-- intentionally commented out in dev

app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy" }));
app.MapControllers();
app.Run();
