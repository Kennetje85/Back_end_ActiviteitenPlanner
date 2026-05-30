using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PollsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PollsController(AppDbContext db) => _db = db;

        // GET /api/polls?activityId=2&userEmail=...
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PollDto>>> GetPolls([FromQuery] int? activityId = null, [FromQuery] string? userEmail = null)
        {
            var query = _db.Polls
                .AsNoTracking()
                .Include(p => p.User)
                .AsQueryable();

            if (activityId.HasValue)
                query = query.Where(p => p.ActivityId == activityId.Value);

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                var normalized = userEmail.Trim().ToLowerInvariant();
                query = query.Where(p => p.User.Email.ToLower() == normalized);
            }

            var polls = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PollDto
                {
                    Id = p.Id,
                    ActivityId = p.ActivityId,
                    UserEmail = p.User.Email,
                    UserName = p.User.Name,
                    Rating = p.Rating,
                    CreatedAt = p.CreatedAt.ToString("o"),
                    UpdatedAt = p.UpdatedAt.ToString("o")
                })
                .ToListAsync();

            return Ok(polls);
        }

        // GET /api/polls/{id}
        [HttpGet("{id:int}")]
        public async Task<ActionResult<PollDto>> GetPoll(int id)
        {
            var poll = await _db.Polls
                .AsNoTracking()
                .Include(p => p.User)
                .Where(p => p.Id == id)
                .Select(p => new PollDto
                {
                    Id = p.Id,
                    ActivityId = p.ActivityId,
                    UserEmail = p.User.Email,
                    UserName = p.User.Name,
                    Rating = p.Rating,
                    CreatedAt = p.CreatedAt.ToString("o"),
                    UpdatedAt = p.UpdatedAt.ToString("o")
                })
                .FirstOrDefaultAsync();

            if (poll == null) return NotFound();
            return Ok(poll);
        }

        // POST /api/polls
        [HttpPost]
        public async Task<ActionResult<PollDto>> CreatePoll([FromBody] CreatePollDto dto)
        {
            if (dto == null) return BadRequest("Body is required.");
            if (dto.Rating < 1 || dto.Rating > 5) return BadRequest("Rating must be between 1 and 5.");

            // Validate activity exists
            var activityExists = await _db.Activities.AsNoTracking().AnyAsync(a => a.Id == dto.ActivityId);
            if (!activityExists) return BadRequest("Activity does not exist.");

            int userId;
            User? resolvedUser = null;

            if (dto.UserId.HasValue)
            {
                userId = dto.UserId.Value;
                var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
                if (!userExists) return BadRequest("UserId does not exist.");
                resolvedUser = await _db.Users.FindAsync(userId);
            }
            else if (!string.IsNullOrWhiteSpace(dto.UserEmail))
            {
                var normalized = dto.UserEmail.Trim().ToLowerInvariant();
                resolvedUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);
                if (resolvedUser == null) return BadRequest("User with specified email not found.");
                userId = resolvedUser.Id;
            }
            else
            {
                return BadRequest("Either UserId or UserEmail must be provided.");
            }

            var poll = new Poll
            {
                ActivityId = dto.ActivityId,
                UserId = userId,
                Rating = dto.Rating,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Polls.Add(poll);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return Conflict(new { message = "A poll by this user for this activity already exists.", detail = ex.Message });
            }

            // load user if not already resolved
            if (resolvedUser == null)
                resolvedUser = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == poll.UserId);

            var result = new PollDto
            {
                Id = poll.Id,
                ActivityId = poll.ActivityId,
                UserEmail = resolvedUser?.Email ?? string.Empty,
                UserName = resolvedUser?.Name,
                Rating = poll.Rating,
                CreatedAt = poll.CreatedAt.ToString("o"),
                UpdatedAt = poll.UpdatedAt.ToString("o")
            };

            return CreatedAtAction(nameof(GetPoll), new { id = poll.Id }, result);
        }

        // PATCH /api/polls/{id}
        [HttpPatch("{id:int}")]
        public async Task<ActionResult<PollDto>> UpdatePoll(int id, [FromBody] UpdatePollDto dto)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();

            if (dto.Rating.HasValue)
            {
                if (dto.Rating < 1 || dto.Rating > 5) return BadRequest("Rating must be between 1 and 5.");
                poll.Rating = dto.Rating.Value;
            }

            poll.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == poll.UserId);

            var result = new PollDto
            {
                Id = poll.Id,
                ActivityId = poll.ActivityId,
                UserEmail = user?.Email ?? string.Empty,
                UserName = user?.Name,
                Rating = poll.Rating,
                CreatedAt = poll.CreatedAt.ToString("o"),
                UpdatedAt = poll.UpdatedAt.ToString("o")
            };

            return Ok(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePoll(int id)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();
            _db.Polls.Remove(poll);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }

    // DTOs used by the controller
    public class PollDto
    {
        public int Id { get; set; }
        public int ActivityId { get; set; }
        public string UserEmail { get; set; } = null!;
        public string? UserName { get; set; }
        public int Rating { get; set; }
        public string CreatedAt { get; set; } = null!;
        public string UpdatedAt { get; set; } = null!;
    }

    public class CreatePollDto
    {
        public int ActivityId { get; set; }
        public int? UserId { get; set; }          // preferred when frontend knows the user id
        public string? UserEmail { get; set; }    // alternative: resolve user by email
        public int Rating { get; set; }           // 1..5
    }

    public class UpdatePollDto
    {
        public int? Rating { get; set; }
    }
}