using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivitiesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ActivitiesController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Activity>>> GetAll()
        {
            var items = await _db.Activities.AsNoTracking().ToListAsync();
            return Ok(items);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Activity>> Get(int id)
        {
            var item = await _db.Activities
                                .Include(a => a.Registrations)
                                .Include(a => a.Polls)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.Id == id);
            if (item == null) return NotFound();
            return Ok(item);
        }

        // Accept a CreateActivityDto from the frontend, validate and map to the Activity entity
        [HttpPost]
        public async Task<ActionResult<Activity>> Create([FromBody] CreateActivityDto dto)
        {
            if (dto == null) return BadRequest("Body is required.");
            if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest("Title is required.");

            var activity = new Activity
            {
                Title = dto.Title,
                Description = dto.Description ?? "",
                Date = dto.Date ?? "",
                Time = dto.Time ?? "",
                Location = dto.Location ?? "",
                Image = dto.Image ?? ""
            };

            // If frontend sent CreatedByUserId, use it. Otherwise, try resolve by email if provided.
            if (dto.CreatedByUserId.HasValue)
            {
                activity.CreatedByUserId = dto.CreatedByUserId;
            }
            else if (!string.IsNullOrWhiteSpace(dto.CreatedByEmail))
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == dto.CreatedByEmail);
                if (user != null) activity.CreatedByUserId = user.Id;
            }

            _db.Activities.Add(activity);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = activity.Id }, activity);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Activity input)
        {
            if (id != input.Id) return BadRequest();
            var existing = await _db.Activities.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Title = input.Title;
            existing.Description = input.Description;
            existing.Date = input.Date;
            existing.Time = input.Time;
            existing.Location = input.Location;
            existing.Image = input.Image;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.Activities.FindAsync(id);
            if (existing == null) return NotFound();
            _db.Activities.Remove(existing);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    // DTOs expected by the frontend
    public class CreateActivityDto
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public string? Location { get; set; }
        public string? Image { get; set; }

        // Accept either the numeric user id or the user's email (frontend choice)
        public int? CreatedByUserId { get; set; }
        public string? CreatedByEmail { get; set; }
    }

    public class UpdateActivityDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }
        public string? Location { get; set; }
        public string? Image { get; set; }
    }
}