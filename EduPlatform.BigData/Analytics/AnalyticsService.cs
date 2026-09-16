using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.BigData.Analytics
{
    public class AnalyticsService
    {
        private readonly ActivityRepository _activityRepo;
        private readonly TestResultRepository _testRepo;
        private readonly EduDbContext _db;

        public AnalyticsService(
            ActivityRepository activityRepo,
            TestResultRepository testRepo,
            EduDbContext db)
        {
            _activityRepo = activityRepo;
            _testRepo = testRepo;
            _db = db;
        }

        public async Task<CourseAnalytics> GetCourseAnalyticsAsync(Guid courseId)
        {
            var testResults = await _testRepo.GetResultsByCourseAsync(courseId);
            var enrollmentCount = await _db.Enrollments
                .CountAsync(e => e.CourseId == courseId);

            return new CourseAnalytics
            {
                CourseId = courseId,
                TotalEnrollments = enrollmentCount,
                TotalTestAttempts = testResults.Count,
                AverageScore = testResults.Any()
                    ? testResults.Average(r => (double)(r.Score / r.MaxScore * 100))
                    : 0,
                PassRate = testResults.Any()
                    ? (double)testResults.Count(r => r.Passed) / testResults.Count * 100
                    : 0,
                GeneratedAt = DateTime.UtcNow
            };
        }

        public async Task<UserProgressReport> GetUserProgressReportAsync(Guid userId)
        {
            var enrollments = await _db.Enrollments
                .Include(e => e.Course)
                .Where(e => e.UserId == userId)
                .ToListAsync();

            var report = new UserProgressReport { UserId = userId };

            foreach (var enrollment in enrollments)
            {
                var totalTime = await _activityRepo
                    .GetTotalTimeOnCourseAsync(userId, enrollment.CourseId);
                var bestScore = await _testRepo
                    .GetBestScoreAsync(userId, enrollment.CourseId);

                report.CourseStats.Add(new CourseProgressStat
                {
                    CourseId = enrollment.CourseId,
                    CourseTitle = enrollment.Course.Title,
                    TotalTimeMinutes = totalTime / 60,
                    BestScore = bestScore
                });
            }

            return report;
        }
    }

    public class CourseAnalytics
    {
        public Guid CourseId { get; set; }
        public int TotalEnrollments { get; set; }
        public int TotalTestAttempts { get; set; }
        public double AverageScore { get; set; }
        public double PassRate { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public class UserProgressReport
    {
        public Guid UserId { get; set; }
        public List<CourseProgressStat> CourseStats { get; set; } = new();
    }

    public class CourseProgressStat
    {
        public Guid CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public int TotalTimeMinutes { get; set; }
        public decimal BestScore { get; set; }
    }
}