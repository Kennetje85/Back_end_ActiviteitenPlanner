using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<UsersController> _logger;
        private readonly IWebHostEnvironment _env;

        public UsersController(AppDbContext context, IConfiguration config, ILogger<UsersController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _logger = logger;
            _env = env;
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
            try
            {
                if (dto == null) return BadRequest("Missing body.");
                if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest("Email and Password are required.");

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
                if (user == null) return Unauthorized("Invalid credentials.");

                if (string.IsNullOrWhiteSpace(user.PasswordHash) || !VerifyPassword(dto.Password, user.PasswordHash))
                    return Unauthorized("Invalid credentials.");

                // Create JWT
                var jwtKey = _config["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT__Key");
                if (string.IsNullOrWhiteSpace(jwtKey))
                {
                    _logger.LogError("JWT key not configured. Set Jwt:Key or JWT__Key.");
                    return Problem("Server configuration error: JWT key not configured.", statusCode: 500);
                }

                var jwtIssuer = _config["Jwt:Issuer"] ?? "Backend_ActiviteitenPlanner";
                var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
                var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

                var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                    new Claim(ClaimTypes.Name, user.Name ?? string.Empty),
                    new Claim(ClaimTypes.Role, user.Role ?? "user"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                var token = new JwtSecurityToken(
                    issuer: jwtIssuer,
                    audience: jwtIssuer,
                    claims: claims,
                    notBefore: DateTime.UtcNow,
                    expires: DateTime.UtcNow.AddHours(8),
                    signingCredentials: creds
                );

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                // Return token + basic user info
                return Ok(new
                {
                    token = tokenString,
                    expires = token.ValidTo,
                    user = new { user.Id, user.Name, user.Email, user.Role }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in Login for {Email}", dto?.Email);
                if (_env.IsDevelopment())
                {
                    // Return detailed error in Development only (temporary)
                    return Problem(detail: ex.ToString(), title: ex.Message, statusCode: 500);
                }

                return Problem("An unexpected error occurred while processing login.", statusCode: 500);
            }
        }

        // GET api/users
        [HttpGet]
        [Authorize]
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

        // Password helpers (PBKDF2)
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

    // DTOs
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
}