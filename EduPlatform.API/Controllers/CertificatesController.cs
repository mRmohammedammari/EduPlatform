using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers;

[ApiController]
[Route("api/certificates")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly EduDbContext _db;
    private readonly TestResultRepository _testResults;

    public CertificatesController(EduDbContext db, TestResultRepository testResults)
    {
        _db = db;
        _testResults = testResults;
    }

    [HttpGet("{courseId:guid}")]
    public async Task<IActionResult> GetCertificate(Guid courseId)
    {
        var userId = Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var enrolled = await _db.Enrollments.AnyAsync(enrollment =>
            enrollment.UserId == userId && enrollment.CourseId == courseId);
        if (!enrolled)
            return Forbid();

        var course = await _db.Courses.FirstOrDefaultAsync(item =>
            item.Id == courseId && item.IsPublished && !item.IsArchived);
        if (course == null)
            return NotFound();

        var result = await _testResults.GetBestPassedResultAsync(userId, courseId);
        if (result == null)
            return Conflict(new { message = "Réussissez le test pour obtenir le certificat." });

        return Ok(new
        {
            certificateId = $"EDU-{userId:N}-{courseId:N}",
            studentName = User.FindFirst("firstName")?.Value ?? "Étudiant",
            courseTitle = course.Title,
            issuedAt = result.TakenAt,
            score = result.Score,
            maxScore = result.MaxScore
        });
    }
}
