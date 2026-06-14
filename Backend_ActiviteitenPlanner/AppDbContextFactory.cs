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
            // Design-time: prefer ConnectionStrings:DefaultConnection or CONNECTION_STRING env var.
            var envConn = Environment.GetEnvironmentVariable("CONNECTION_STRING");
            var configConn = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString("DefaultConnection");

            var connectionString = envConn
                ?? configConn
                ?? throw new InvalidOperationException("Connection string not configured. Set ConnectionStrings:DefaultConnection in appsettings.json or CONNECTION_STRING env var.");

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}