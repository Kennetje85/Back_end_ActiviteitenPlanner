using System;
using System.Collections.Generic;
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Poll>>> GetPolls()
        {
            var polls = await _db.Polls.AsNoTracking().ToListAsync();
            return Ok(polls);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Poll>> GetPoll(int id)
        {
            var poll = await _db.Polls.FindAsync(id);
            if (poll == null) return NotFound();
            return Ok(poll);
        }

        [HttpPost]
        public async Task<ActionResult<Poll>> CreatePoll([FromBody] CreatePollDto dto)
        {
            if (dto == null) return BadRequest("Body is required.");
            if (dto.Rating < 1 || dto.Rating > 5) return BadRequest("Rating must be between 1 and 5.");

            int userId;
            if (dto.UserId.HasValue)
            {
                userId = dto.UserId.Value;
                var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
                if (!userExists) return BadRequest("UserId does not exist.");
            }
            else if (!string.IsNullOrWhiteSpace(dto.UserEmail))
            {
                var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == dto.UserEmail);
                if (user == null) return BadRequest("User with specified email not found.");
                userId = user.Id;
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
                // Unique index on (ActivityId, UserId) may cause conflict
                return Conflict(new { message = "A poll by this user for this activity already exists.", detail = ex.Message });
            }

            return CreatedAtAction(nameof(GetPoll), new { id = poll.Id }, poll);
        }

        [HttpPatch("{id:int}")]
        public async Task<ActionResult<Poll>> UpdatePoll(int id, [FromBody] UpdatePollDto dto)
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
            return Ok(poll);
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