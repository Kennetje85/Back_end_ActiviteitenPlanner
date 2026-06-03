using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Identity;
using Backend_ActiviteitenPlanner.Models;

namespace Backend_ActiviteitenPlanner.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, ILogger<AuthController> logger)
        {
            _db = db;
            _hasher = hasher;
            _config = config;
            _logger = logger;
        }

        // GET: api/auth
        [HttpGet]
        public IActionResult Get() => Ok("Auth controller working.");

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Email and password are required." });

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == req.Email.Trim());
            if (user == null)
                return Unauthorized(new { message = "Invalid credentials." });

            var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (verify == PasswordVerificationResult.Failed)
                return Unauthorized(new { message = "Invalid credentials." });

            var (token, expiresAt) = GenerateJwtToken(user);

            return Ok(new
            {
                accessToken = token,
                expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds
            });
        }

        private (string Token, DateTime ExpiresAt) GenerateJwtToken(User user)
        {
            var keyString = _config["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT__Key") ?? throw new InvalidOperationException("JWT key missing");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(15);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role ?? "user"),
                new Claim("role", user.Role ?? "user"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "Backend_ActiviteitenPlanner",
                audience: _config["Jwt:Issuer"] ?? "Backend_ActiviteitenPlanner",
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expires);
        }

        // DTOs
        public class LoginRequest
        {
            public string Email { get; set; } = null!;
            public string Password { get; set; } = null!;
        }
    }
}