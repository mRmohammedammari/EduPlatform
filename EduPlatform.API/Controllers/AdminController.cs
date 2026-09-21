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
                user.CreatedAt,
                user.IsSuspended
            })
            .ToListAsync());
    }

    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var recentThreshold = DateTime.UtcNow.AddDays(-30);
        return Ok(new
        {
            users = await _db.Users.CountAsync(),
            activeUsers = await _db.Users.CountAsync(user => !user.IsSuspended),
            suspendedUsers = await _db.Users.CountAsync(user => user.IsSuspended),
            students = await _db.Users.CountAsync(user => user.Role == UserRole.Student),
            instructors = await _db.Users.CountAsync(user => user.Role == UserRole.Instructor),
            courses = await _db.Courses.CountAsync(),
            publishedCourses = await _db.Courses.CountAsync(course => course.IsPublished),
            pendingCourses = await _db.Courses.CountAsync(course => course.Status == CourseStatus.PendingReview),
            modules = await _db.Modules.CountAsync(),
            enrollments = await _db.Enrollments.CountAsync(),
            recentEnrollments = await _db.Enrollments.CountAsync(
                enrollment => enrollment.EnrolledAt >= recentThreshold),
            questions = await _db.Questions.CountAsync()
        });
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> GetReviews()
    {
        return Ok(await _db.CourseReviews
            .Include(review => review.Course)
            .Include(review => review.User)
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => new
            {
                review.Id,
                review.Rating,
                review.Comment,
                review.CreatedAt,
                CourseTitle = review.Course.Title,
                UserName = review.User.FirstName + " " + review.User.LastName,
                UserEmail = review.User.Email,
                Reports = _db.CourseReviewReports
                    .Where(report => report.ReviewId == review.Id)
                    .OrderByDescending(report => report.CreatedAt)
                    .Select(report => new
                    {
                        report.Reason,
                        report.CreatedAt,
                        ReporterName = report.Reporter.FirstName + " " + report.Reporter.LastName
                    })
                    .ToList()
            })
            .ToListAsync());
    }

    [HttpDelete("reviews/{id:guid}")]
    public async Task<IActionResult> DeleteReview(Guid id)
    {
        var review = await _db.CourseReviews.FindAsync(id);
        if (review is null)
            return NotFound();

        _db.CourseReviews.Remove(review);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        await EnsureCategoriesAsync();
        return Ok(await _db.CourseCategories
            .OrderBy(category => category.Name)
            .ToListAsync());
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Le nom de la catégorie est obligatoire." });

        await EnsureCategoriesAsync();
        if (await _db.CourseCategories.AnyAsync(category => category.Name == name))
            return Conflict(new { message = "Cette catégorie existe déjà." });

        var category = new CourseCategory { Name = name };
        _db.CourseCategories.Add(category);
        await _db.SaveChangesAsync();
        return Created($"api/admin/categories/{category.Id}", category);
    }

    [HttpPut("categories/{id:guid}/toggle")]
    public async Task<IActionResult> ToggleCategory(Guid id)
    {
        var category = await _db.CourseCategories.FindAsync(id);
        if (category is null)
            return NotFound();

        category.IsActive = !category.IsActive;
        await _db.SaveChangesAsync();
        return Ok(category);
    }

    private async Task EnsureCategoriesAsync()
    {
        var existing = await _db.CourseCategories
            .Select(category => category.Name)
            .ToListAsync();
        var courseCategories = await _db.Courses
            .Select(course => course.Category)
            .Where(category => category != "")
            .Distinct()
            .ToListAsync();

        foreach (var name in courseCategories.Except(existing, StringComparer.OrdinalIgnoreCase))
            _db.CourseCategories.Add(new CourseCategory { Name = name });

        if (courseCategories.Any(name => !existing.Contains(name)))
            await _db.SaveChangesAsync();
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

    [HttpPut("users/{id:guid}/suspend")]
    public async Task<IActionResult> SuspendUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.IsSuspended = true;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsSuspended });
    }

    [HttpPut("users/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound();

        user.IsSuspended = false;
        await _db.SaveChangesAsync();
        return Ok(new { user.Id, user.IsSuspended });
    }
}

public class UpdateRoleDto
{
    public string Role { get; set; } = string.Empty;
}

public class CategoryDto
{
    public string Name { get; set; } = string.Empty;
}