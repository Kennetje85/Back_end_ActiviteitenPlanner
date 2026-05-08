using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        // POST api/users
        [HttpPost]
        public async Task<ActionResult> CreateUser([FromBody] CreateUserDto dto)
        {
            if (dto == null) return BadRequest("Missing body.");
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Name, Email and Password are required.");

            var existingUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (existingUser != null)
                return BadRequest("Email already exists");

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                PasswordHash = HashPassword(dto.Password),
                Role = string.IsNullOrWhiteSpace(dto.Role) ? "user" : dto.Role
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUserByEmail), new { email = user.Email }, new { user.Id, user.Name, user.Email, user.Role });
        }

        // POST api/users/login
        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null) return BadRequest("Missing body.");
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Email and Password are required.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return Unauthorized("Invalid credentials.");

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !VerifyPassword(dto.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials.");

            // Successful login — do NOT return the password hash
            return Ok(new { user.Id, user.Name, user.Email, user.Role });
        }

        // Accept both POST and PUT for change-password to match frontend
        [HttpPost("{id:int}/change-password")]
        [HttpPut("{id:int}/change-password")]
        public async Task<IActionResult> ChangePassword(int id, [FromBody] ChangePasswordDto dto)
        {
            if (dto == null) return BadRequest("Missing body.");
            if (string.IsNullOrWhiteSpace(dto.OldPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
                return BadRequest("OldPassword and NewPassword are required.");

            if (dto.NewPassword.Length < 6) // minimal check
                return BadRequest("New password must be at least 6 characters.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound("User not found.");

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || !VerifyPassword(dto.OldPassword, user.PasswordHash))
                return Unauthorized("Old password is incorrect.");

            user.PasswordHash = HashPassword(dto.NewPassword);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers([FromQuery] string? email = null)
        {
            if (!string.IsNullOrEmpty(email))
                return Ok(await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Email == email)
                    .ToListAsync());

            return Ok(await _context.Users
                .AsNoTracking()
                .ToListAsync());
        }

        // GET api/users/{email}
        [HttpGet("{email}")]
        public async Task<ActionResult> GetUserByEmail(string email)
        {
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null) return NotFound();
            return Ok(new { user.Id, user.Name, user.Email, user.Role });
        }

        // Simple PBKDF2 hashing; stored format = iterations.saltBase64.hashBase64
        private static string HashPassword(string password)
        {
            const int iterations = 100_000;
            const int saltSize = 16;
            const int hashSize = 32;

            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[saltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            var hash = pbkdf2.GetBytes(hashSize);

            return $"{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        // Verify password against stored hash in format iterations.salt.hash
        private static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var parts = storedHash.Split('.');
                if (parts.Length != 3) return false;

                var iterations = int.Parse(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var hash = Convert.FromBase64String(parts[2]);

                using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
                var computed = pbkdf2.GetBytes(hash.Length);

                return CryptographicOperations.FixedTimeEquals(computed, hash);
            }
            catch
            {
                return false;
            }
        }
    }

    public class CreateUserDto
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Role { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
    }
}