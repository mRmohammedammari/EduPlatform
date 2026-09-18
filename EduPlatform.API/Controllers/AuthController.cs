using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var user = await _db.Users.FindAsync(userId);
            if (user is null)
                return NotFound();

            return Ok(new { user.FirstName, user.LastName, user.Email, user.Role, user.CreatedAt });
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
            var refreshToken = await IssueRefreshTokenAsync(user.Id);
            return Ok(new { token, refreshToken, userId = user.Id, role = user.Role });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == dto.Email);

            if (user == null || !_auth.VerifyPassword(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Email ou mot de passe incorrect" });

            var token = _auth.GenerateToken(user);
            var refreshToken = await IssueRefreshTokenAsync(user.Id);
            return Ok(new
            {
                token,
                refreshToken,
                userId = user.Id,
                firstName = user.FirstName,
                role = user.Role
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshDto dto)
        {
            var existing = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (existing is null || !existing.IsActive)
                return Unauthorized(new { message = "Session expirée, veuillez vous reconnecter." });

            var user = await _db.Users.FindAsync(existing.UserId);
            if (user is null)
                return Unauthorized(new { message = "Session expirée, veuillez vous reconnecter." });

            existing.RevokedAt = DateTime.UtcNow;

            var token = _auth.GenerateToken(user);
            var refreshToken = await IssueRefreshTokenAsync(user.Id);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                token,
                refreshToken,
                userId = user.Id,
                firstName = user.FirstName,
                role = user.Role
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshDto dto)
        {
            var existing = await _db.RefreshTokens
                .FirstOrDefaultAsync(r => r.Token == dto.RefreshToken);

            if (existing is not null && existing.RevokedAt is null)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
                return BadRequest(new { message = "Le prénom et le nom sont obligatoires." });

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var user = await _db.Users.FindAsync(userId);
            if (user is null)
                return NotFound();

            user.FirstName = dto.FirstName.Trim();
            user.LastName = dto.LastName.Trim();
            await _db.SaveChangesAsync();

            return Ok(new { user.FirstName, user.LastName });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                return BadRequest(new { message = "Le nouveau mot de passe doit contenir au moins 6 caractères." });

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var user = await _db.Users.FindAsync(userId);
            if (user is null)
                return NotFound();
            if (!_auth.VerifyPassword(dto.CurrentPassword, user.PasswordHash))
                return BadRequest(new { message = "Mot de passe actuel incorrect." });

            user.PasswordHash = _auth.HashPassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok();
        }

        private async Task<string> IssueRefreshTokenAsync(Guid userId)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = _auth.GenerateRefreshToken(),
                ExpiresAt = DateTime.UtcNow.AddDays(_auth.GetRefreshTokenExpirationDays())
            };

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();
            return refreshToken.Token;
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

    public class RefreshDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UpdateProfileDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}