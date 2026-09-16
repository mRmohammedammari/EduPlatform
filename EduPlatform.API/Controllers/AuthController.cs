using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly EduDbContext _db;
        private readonly AuthService _auth;

        public AuthController(EduDbContext db, AuthService auth)
        {
            _db = db;
            _auth = auth;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (_db.Users.Any(u => u.Email == dto.Email))
                return BadRequest(new { message = "Email déjà utilisé" });

            var user = new User
            {
                Email = dto.Email,
                PasswordHash = _auth.HashPassword(dto.Password),
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Role = UserRole.Student
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var token = _auth.GenerateToken(user);
            return Ok(new { token, userId = user.Id, role = user.Role });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto dto)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == dto.Email);

            if (user == null || !_auth.VerifyPassword(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Email ou mot de passe incorrect" });

            var token = _auth.GenerateToken(user);
            return Ok(new
            {
                token,
                userId = user.Id,
                firstName = user.FirstName,
                role = user.Role
            });
        }
    }

    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class LoginDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}