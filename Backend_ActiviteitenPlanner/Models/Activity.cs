using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Backend_ActiviteitenPlanner.Models
{
    public class Activity
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = "";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        [Required]
        public string Location { get; set; } = "";
        public string Image { get; set; } = "";
        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }
        public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
        public ICollection<Poll> Polls { get; set; } = new List<Poll>();
    }
}
