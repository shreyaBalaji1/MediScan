using Microsoft.AspNetCore.Mvc;
using MediScanApi.Data;
using MediScanApi.Models;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Claims;

namespace MediScanApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var existing = _db.Users.FirstOrDefault(u => u.Email == req.Email);
            if (existing != null)
                return Conflict("A user with that email already exists.");

            var user = new User
            {
                Email = req.Email,
                Name = req.Name
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, req.Password);
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return CreatedAtAction(null, new { id = user.Id }, new { user.Id, user.Email, user.Name });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == req.Email);
            if (user == null)
                return Unauthorized("Invalid credentials.");

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (result == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid credentials.");

            var jwtSection = _config.GetSection("Jwt");
            var key = jwtSection.GetValue<string>("Key");
            var issuer = jwtSection.GetValue<string>("Issuer");
            var audience = jwtSection.GetValue<string>("Audience");
            var expireMinutes = jwtSection.GetValue<int?>("ExpireMinutes") ?? 60;

            if (string.IsNullOrEmpty(key))
                return StatusCode(500, "JWT not configured on server.");

            var keyBytes = Encoding.UTF8.GetBytes(key);
            var tokenHandler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
            };

            if (!string.IsNullOrEmpty(user.Name)) claims.Add(new Claim(ClaimTypes.Name, user.Name));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);

            return Ok(new AuthResponse { Token = tokenString, ExpiresAt = tokenDescriptor.Expires!.Value.ToUniversalTime() });
        }
    }
}
