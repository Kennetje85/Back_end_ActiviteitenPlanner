using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner

{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<Registration> Registrations => Set<Registration>();
        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<LogEntry> Logs => Set<LogEntry>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=True;");
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(x => x.Email)
                .IsUnique();

            modelBuilder.Entity<Registration>()
                .HasIndex(x => new { x.ActivityId, x.UserId })
                .IsUnique(); // 1 registratie per user per activiteit

            modelBuilder.Entity<Poll>()
                .HasIndex(x => new { x.ActivityId, x.UserId })
                .IsUnique(); // 1 poll per user per activiteit

            modelBuilder.Entity<Poll>()
                .Property(x => x.Rating);

            modelBuilder.Entity<Poll>()
                .ToTable(t => t.HasCheckConstraint("CK_Poll_Rating", "[Rating] >= 1 AND [Rating] <= 5"));

            modelBuilder.Entity<Activity>()
                .HasOne(x => x.CreatedByUser)
                .WithMany(u => u.CreatedActivities)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }


}
