using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend_ActiviteitenPlanner.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "user"; // bijv. "admin" of "user"

        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<Poll> Polls { get; set; } = new List<Poll>();
        public ICollection<Activity> CreatedActivities { get; set; } = new List<Activity>();
    }
}
