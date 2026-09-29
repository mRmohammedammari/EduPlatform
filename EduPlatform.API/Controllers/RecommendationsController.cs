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

            var enrolledProfile = await _db.Courses
                .Where(course => enrolledCourseIds.Contains(course.Id))
                .Select(course => new { course.Category, course.Level })
                .ToListAsync();

            // !IsArchived : un cours archive restait eligible aux recommandations.
            var availableCourseIds = await _db.Courses
                .Where(c => c.IsPublished && !c.IsArchived && !enrolledCourseIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync();

            var recommendedIds = _recommender
                .GetRecommendedCourses(userId, availableCourseIds);

            var courses = await _db.Courses
                .Where(c => recommendedIds.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.Category,
                    c.Level,
                    c.DurationMinutes,
                    c.ThumbnailUrl,
                    ModuleCount = c.Modules.Count,
                    AverageRating = _db.CourseReviews
                        .Where(review => review.CourseId == c.Id)
                        .Select(review => (double?)review.Rating)
                        .Average() ?? 0,
                    ReviewCount = _db.CourseReviews.Count(review => review.CourseId == c.Id)
                })
                .ToListAsync();

            return Ok(courses.Select(course => new
            {
                course.Id,
                course.Title,
                course.Description,
                course.Category,
                course.Level,
                course.DurationMinutes,
                course.ThumbnailUrl,
                course.ModuleCount,
                AverageRating = Math.Round(course.AverageRating, 1),
                course.ReviewCount,
                RecommendationReason = GetRecommendationReason(
                    course.Category,
                    course.Level,
                    enrolledProfile.Select(item => (item.Category, item.Level)))
            }));
        }

        private static string GetRecommendationReason(
            string category,
            string level,
            IEnumerable<(string Category, string Level)> enrolledProfile)
        {
            var profile = enrolledProfile.ToList();
            if (profile.Any(item => item.Category == category))
                return $"Parce que vous apprenez déjà dans la catégorie {category}.";
            if (profile.Any(item => item.Level == level))
                return $"Parce que ce cours correspond à votre niveau {level}.";
            return "Sélectionné selon votre parcours et les cours disponibles.";
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