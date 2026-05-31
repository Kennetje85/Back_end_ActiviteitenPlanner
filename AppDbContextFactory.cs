using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Backend_ActiviteitenPlanner
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            // Priority: environment variable -> appsettings.json -> environment-aware fallback
            var envConn = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var configConn = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString("Default");

            var isProd = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Production", StringComparison.OrdinalIgnoreCase)
                         || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

            var azureFallback = "Server=tcp:dbhhs.database.windows.net,1433;Initial Catalog=Activiteitenplanner;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=\"Active Directory Default\";";
            var localFallback = "Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=False;";

            var connectionString = envConn
                ?? configConn
                ?? (isProd ? azureFallback : localFallback);

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}