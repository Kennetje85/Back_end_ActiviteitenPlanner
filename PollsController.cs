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
        public async Task<ActionResult<IEnumerable<Poll>>> GetAll() =>
            Ok(await _db.Polls.AsNoTracking().ToListAsync());

        [HttpPost]
        public async Task<ActionResult<Poll>> Create(Poll poll)
        {
            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = poll.Id }, poll);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Poll>> Get(int id)
        {
            var p = await _db.Polls.FindAsync(id);
            if (p == null) return NotFound();
            return Ok(p);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Poll input)
        {
            if (id != input.Id) return BadRequest();
            var existing = await _db.Polls.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Rating = input.Rating;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.Polls.FindAsync(id);
            if (existing == null) return NotFound();
            _db.Polls.Remove(existing);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}