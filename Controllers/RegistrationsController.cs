using Backend_ActiviteitenPlanner.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RegistrationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public RegistrationsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Registration>>> GetRegistrations()
        {
            return await _context.Registrations
                .Include(r => r.Activity)
                .Include(r => r.User)
                .ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Registration>> PostRegistration(
            [FromBody] CreateRegistrationDto dto)
        {
            var activity = await _context.Activities
                .FirstOrDefaultAsync(a => a.Id == dto.ActivityId);

            if (activity == null)
                return BadRequest("Activity bestaat niet.");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == dto.UserId);

            if (user == null)
                return BadRequest("User bestaat niet.");

            var registration = new Registration
            {
                ActivityId = dto.ActivityId,
                UserId = dto.UserId,
                Status = dto.Status,
                CreatedAt = DateTime.UtcNow
            };

            _context.Registrations.Add(registration);
            await _context.SaveChangesAsync();

            return Ok(registration);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRegistration(int id)
        {
            var registration = await _context.Registrations.FindAsync(id);

            if (registration == null)
                return NotFound();

            _context.Registrations.Remove(registration);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class CreateRegistrationDto
    {
        public int ActivityId { get; set; }
        public int UserId { get; set; }
        public ParticipationStatus Status { get; set; }
    }
}