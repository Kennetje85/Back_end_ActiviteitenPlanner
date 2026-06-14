csharp Backend_ActiviteitenPlanner\Controllers\RegistrationsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public RegistrationsController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Registration>>> GetAll() =>
            Ok(await _db.Registrations.AsNoTracking().ToListAsync());

        [HttpPost]
        public async Task<ActionResult<Registration>> Create(Registration reg)
        {
            _db.Registrations.Add(reg);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = reg.Id }, reg);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Registration>> Get(int id)
        {
            var r = await _db.Registrations.FindAsync(id);
            if (r == null) return NotFound();
            return Ok(r);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, Registration input)
        {
            if (id != input.Id) return BadRequest();
            var existing = await _db.Registrations.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Status = input.Status;
            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.Registrations.FindAsync(id);
            if (existing == null) return NotFound();
            _db.Registrations.Remove(existing);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}