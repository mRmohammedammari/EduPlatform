using EduPlatform.Core.Models;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly EduDbContext _db;

    public AdminController(EduDbContext db)
    {
        _db = db;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        return Ok(await _db.Users
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                Role = user.Role.ToString(),
                user.CreatedAt
            })
            .ToListAsync());
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        return Ok(new
        {
            users = await _db.Users.CountAsync(),
            students = await _db.Users.CountAsync(user => user.Role == UserRole.Student),
            instructors = await _db.Users.CountAsync(user => user.Role == UserRole.Instructor),
            courses = await _db.Courses.CountAsync(),
            publishedCourses = await _db.Courses.CountAsync(course => course.IsPublished),
            pendingCourses = await _db.Courses.CountAsync(course => course.Status == CourseStatus.PendingReview),
            modules = await _db.Modules.CountAsync(),
            enrollments = await _db.Enrollments.CountAsync(),
            questions = await _db.Questions.CountAsync()
        });
    }

    [HttpGet("reports/courses.csv")]
    public async Task<IActionResult> ExportCoursesReport()
    {
        var rows = await _db.Courses
            .Select(course => new
            {
                course.Title,
                course.Category,
                course.Level,
                course.IsPublished,
                course.IsArchived,
                Modules = course.Modules.Count,
                Enrollments = course.Enrollments.Count,
                Questions = _db.Questions.Count(question => question.CourseId == course.Id)
            })
            .OrderBy(row => row.Title)
            .ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Title,Category,Level,Published,Archived,Modules,Enrollments,Questions");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',',
                Csv(row.Title),
                Csv(row.Category),
                Csv(row.Level),
                row.IsPublished,
                row.IsArchived,
                row.Modules,
                row.Enrollments,
                row.Questions));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "eduplatform-courses.csv");
    }

    private static string Csv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleDto dto)
    {
        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest(new { message = "Rôle invalide." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.Role = role;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.Role });
    }
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}