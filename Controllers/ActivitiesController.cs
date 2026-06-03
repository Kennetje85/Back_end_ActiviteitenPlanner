using Backend_ActiviteitenPlanner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivitiesController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ActivitiesController(AppDbContext db) => _db = db;

        // GET api/activities
        [HttpGet]
       
        public async Task<ActionResult<IEnumerable<ActivityDto>>> GetAll()
        {
            var items = await _db.Activities
                .AsNoTracking()
                .Include(a => a.Polls)
                .ToListAsync();

            var dtos = items.Select(a => new ActivityDto
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                Date = a.Date,
                Time = a.Time,
                Location = a.Location,
                Image = a.Image,
                CreatedByUserId = a.CreatedByUserId,
                PollCount = a.Polls?.Count ?? 0,
                AverageRating = a.Polls != null && a.Polls.Any() ? a.Polls.Average(p => p.Rating) : 0.0
            }).ToList();

            return Ok(dtos);
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
        
        // Allow unauthenticated users to create activities (optional, adjust as needed)
        public async Task<ActionResult<Activity>> Create([FromBody] CreateActivityDto dto)
        {
            if (dto == null) return BadRequest("Body is required.");

            // automatic ModelState validation (ApiController)
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            // Trim and defensive validation for whitespace-only values
            dto.Location = dto.Location?.Trim();
            if (string.IsNullOrWhiteSpace(dto.Location))
            {
                ModelState.AddModelError(nameof(dto.Location), "Location is required and must contain non-whitespace characters.");
                return ValidationProblem(ModelState);
            }

            // Title defensive trim
            dto.Title = dto.Title?.Trim();

            var activity = new Activity
            {
                Title = dto.Title!,
                Description = dto.Description ?? "",
                Date = dto.Date ?? "",
                Time = dto.Time ?? "",
                Location = dto.Location,
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
        [Authorize(Roles = "admin,user")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateActivityDto dto)
        {
            if (dto == null) return BadRequest("Body is required.");
            if (!ModelState.IsValid) return ValidationProblem(ModelState);

            var existing = await _db.Activities.FindAsync(id);
            if (existing == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(dto.Title)) { 
                existing.Title = dto.Title;
            }
            if (dto.Description != null) existing.Description = dto.Description;
            if (dto.Date != null) existing.Date = dto.Date;
            if (dto.Time != null) existing.Time = dto.Time;

            // If client provided Location, ensure it's not empty or whitespace and trim it
            if (dto.Location != null)
            {
                var trimmed = dto.Location.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    ModelState.AddModelError(nameof(dto.Location), "Location must not be empty when provided.");
                    return ValidationProblem(ModelState);
                }

                existing.Location = trimmed;
            }

            if (dto.Image != null) existing.Image = dto.Image;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        [Authorize(Roles = "admin,user")]
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
        [Required(ErrorMessage = "Title is required")]
        public string Title { get; set; } = null!;

        public string? Description { get; set; }
        public string? Date { get; set; }
        public string? Time { get; set; }

        [Required(ErrorMessage = "Location is required")]
        [MinLength(1, ErrorMessage = "Location must not be empty")]
        [RegularExpression(@"\S+.*", ErrorMessage = "Location must contain non-whitespace characters")]
        public string Location { get; set; } = null!;

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

        // Optional on update: when provided it must not be empty or whitespace
        [MinLength(1, ErrorMessage = "Location must not be empty")]
        [RegularExpression(@"\S+.*", ErrorMessage = "Location must contain non-whitespace characters")]
        public string? Location { get; set; }
        public string? Image { get; set; }
    }

    public class ActivityDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Description { get; set; } = "";
        public string Date { get; set; } = "";
        public string Time { get; set; } = "";
        public string Location { get; set; } = "";
        public string Image { get; set; } = "";
        public int? CreatedByUserId { get; set; }
        public int PollCount { get; set; }
        public double AverageRating { get; set; }
    }
}