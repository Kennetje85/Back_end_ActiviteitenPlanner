using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend_ActiviteitenPlanner
{
    public class Poll
    {
        public int Id { get; set; }
        public int ActivityId { get; set; }
        public Activity Activity { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int Rating { get; set; } // 1..5
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
