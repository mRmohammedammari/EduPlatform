using EduPlatform.Core.Models;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/reviews")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly EduDbContext _db;

    public ReviewsController(EduDbContext db)
    {
        _db = db;
    }

    [HttpGet("course/{courseId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCourseReviews(Guid courseId)
    {
        var reviews = await _db.CourseReviews
            .Where(review => review.CourseId == courseId)
            .Include(review => review.User)
            .OrderByDescending(review => review.CreatedAt)
            .Select(review => new
            {
                review.Id,
                review.Rating,
                review.Comment,
                review.CreatedAt,
                UserName = review.User.FirstName
            })
            .ToListAsync();

        return Ok(new
        {
            averageRating = reviews.Count == 0 ? 0 : Math.Round(reviews.Average(review => review.Rating), 1),
            totalReviews = reviews.Count,
            reviews
        });
    }

    [HttpPost("course/{courseId:guid}")]
    public async Task<IActionResult> CreateOrUpdate(Guid courseId, [FromBody] ReviewDto dto)
    {
        var userId = GetUserId();
        var enrolled = await _db.Enrollments.AnyAsync(enrollment =>
            enrollment.CourseId == courseId && enrollment.UserId == userId);
        if (!enrolled)
            return Forbid();
        if (dto.Rating is < 1 or > 5)
            return BadRequest(new { message = "La note doit être comprise entre 1 et 5." });
        if (dto.Comment?.Length > 2000)
            return BadRequest(new { message = "Le commentaire est trop long." });

        var review = await _db.CourseReviews.FirstOrDefaultAsync(item =>
            item.CourseId == courseId && item.UserId == userId);
        if (review is null)
        {
            review = new CourseReview { CourseId = courseId, UserId = userId };
            _db.CourseReviews.Add(review);
        }

        review.Rating = dto.Rating;
        review.Comment = dto.Comment?.Trim() ?? string.Empty;
        review.CreatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(review);
    }

    [HttpPost("{reviewId:guid}/report")]
    public async Task<IActionResult> Report(Guid reviewId, [FromBody] ReviewReportDto dto)
    {
        var userId = GetUserId();
        var review = await _db.CourseReviews.FirstOrDefaultAsync(item => item.Id == reviewId);
        if (review is null)
            return NotFound();
        if (review.UserId == userId)
            return BadRequest(new { message = "Vous ne pouvez pas signaler votre propre avis." });
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 1000)
            return BadRequest(new { message = "Le motif du signalement est obligatoire et limité à 1000 caractères." });

        var enrolled = await _db.Enrollments.AnyAsync(enrollment =>
            enrollment.CourseId == review.CourseId && enrollment.UserId == userId);
        if (!enrolled)
            return Forbid();

        if (await _db.CourseReviewReports.AnyAsync(report =>
                report.ReviewId == reviewId && report.ReporterId == userId))
            return Conflict(new { message = "Cet avis a déjà été signalé." });

        _db.CourseReviewReports.Add(new CourseReviewReport
        {
            ReviewId = reviewId,
            ReporterId = userId,
            Reason = dto.Reason.Trim()
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirst(
        System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
}

public class ReviewDto
{
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

public class ReviewReportDto
{
    public string Reason { get; set; } = string.Empty;
}
