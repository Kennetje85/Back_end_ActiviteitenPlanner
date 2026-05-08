using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend_ActiviteitenPlanner
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=False;");
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}