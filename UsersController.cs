csharp Backend_ActiviteitenPlanner\Controllers\UsersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UsersController(AppDbContext db) => _db = db;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetAll() =>
            Ok(await _db.Users.AsNoTracking().ToListAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<User>> Get(int id)
        {
            var u = await _db.Users.FindAsync(id);
            if (u == null) return NotFound();
            return Ok(u);
        }

        [HttpPost]
        public async Task<ActionResult<User>> Create(User user)
        {
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, User input)
        {
            if (id != input.Id) return BadRequest();
            var existing = await _db.Users.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = input.Name;
            existing.Email = input.Email;
            existing.Role = input.Role;
            existing.PasswordHash = input.PasswordHash;

            await _db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var existing = await _db.Users.FindAsync(id);
            if (existing == null) return NotFound();
            _db.Users.Remove(existing);
            await _db.SaveChangesAsync();
            return NoContent();
        }
    }
}