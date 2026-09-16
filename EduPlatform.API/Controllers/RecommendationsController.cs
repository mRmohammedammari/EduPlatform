using EduPlatform.BigData.Analytics;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RecommendationsController : ControllerBase
    {
        private readonly RecommendationService _recommender;
        private readonly EduDbContext _db;

        public RecommendationsController(
            RecommendationService recommender,
            EduDbContext db)
        {
            _recommender = recommender;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendations()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var enrolledCourseIds = await _db.Enrollments
                .Where(e => e.UserId == userId)
                .Select(e => e.CourseId)
                .ToListAsync();

            var availableCourseIds = await _db.Courses
                .Where(c => c.IsPublished && !enrolledCourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            var recommendedIds = _recommender
                .GetRecommendedCourses(userId, availableCourseIds);

            var courses = await _db.Courses
                .Where(c => recommendedIds.Contains(c.Id))
                .ToListAsync();

            return Ok(courses);
        }

        [HttpPost("train")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TrainModel()
        {
            await _recommender.TrainModelAsync();
            return Ok(new { message = "Modèle entraîné avec succès" });
        }
    }
}