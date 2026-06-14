using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        [HttpPost]
        public async Task<ActionResult<Activity>> Create(Activity activity)
        {
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
}   