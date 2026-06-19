using Backend_ActiviteitenPlanner.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend_ActiviteitenPlanner

{

    //DbContext class die de database verbindt met de applicatie en de tabellen definieert
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        //dbsets tabellen. Startpunt van crud-operaties.
        public DbSet<User> Users => Set<User>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<Registration> Registrations => Set<Registration>();
        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<LogEntry> Logs => Set<LogEntry>();


        //Ontvangt instellingen voor de databaseverbinding en configureert deze indien nodig
      //  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      //  {
      //      if (!optionsBuilder.IsConfigured)
      //      {
      //          optionsBuilder.UseSqlServer("Server=DESKTOP-6C6PF5S\\SQLEXPRESS;Database=ActivityPlanner;Trusted_Connection=True;Encrypt=True;");
      //      }
      //  }




        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);



            //Maakt een unieke index op Email. 2 gebruikers mogen niet dezelfde email hebben.
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

            //Eén Activity heeft één maker(CreatedByUser).
            //Eén User kan meerdere activiteiten hebben gemaakt(CreatedActivities).
            //De foreign key is CreatedByUserId.

            modelBuilder.Entity<Activity>()
                .HasOne(x => x.CreatedByUser)
                .WithMany(u => u.CreatedActivities)
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }


}
