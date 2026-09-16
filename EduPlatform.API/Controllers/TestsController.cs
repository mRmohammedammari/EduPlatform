using EduPlatform.Core.Models;
using EduPlatform.BigData.Kafka;
using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TestsController : ControllerBase
    {
        private readonly EduDbContext _db;
        private readonly TestResultRepository _testRepo;
        private readonly ActivityRepository _activityRepo;
        private readonly EventProducer _eventProducer;

        public TestsController(
            EduDbContext db,
            TestResultRepository testRepo,
            ActivityRepository activityRepo,
            EventProducer eventProducer)
        {
            _db = db;
            _testRepo = testRepo;
            _activityRepo = activityRepo;
            _eventProducer = eventProducer;
        }

        [HttpGet("{courseId:guid}")]
        public async Task<IActionResult> GetQuestions(Guid courseId)
        {
            if (!await IsEnrolledAsync(courseId))
                return Forbid();

            var questions = await _db.Questions
                .Where(q => q.CourseId == courseId)
                .Select(q => new {
                    q.Id,
                    q.Text,
                    q.Options,
                    q.Points
                })
                .ToListAsync();

            return Ok(questions);
        }

        [HttpGet("{courseId:guid}/manage")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetQuestionsForManagement(Guid courseId)
        {
            if (!await CanManageCourseAsync(courseId))
                return Forbid();

            return Ok(await _db.Questions
                .Where(q => q.CourseId == courseId)
                .OrderBy(q => q.Id)
                .ToListAsync());
        }

        [HttpPost("{courseId:guid}/questions")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateQuestion(
            Guid courseId,
            [FromBody] QuestionDto dto)
        {
            if (!await CanManageCourseAsync(courseId))
                return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Text) || dto.Options.Count < 2 ||
                !dto.Options.Contains(dto.CorrectAnswer))
                return BadRequest(new { message = "La question doit avoir au moins deux options et une bonne réponse valide." });

            var question = new Question
            {
                CourseId = courseId,
                Text = dto.Text,
                Options = dto.Options,
                CorrectAnswer = dto.CorrectAnswer,
                Points = Math.Max(1, dto.Points)
            };
            _db.Questions.Add(question);
            await _db.SaveChangesAsync();
            return Ok(question);
        }

        [HttpPut("{courseId:guid}/questions/{questionId:guid}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateQuestion(
            Guid courseId,
            Guid questionId,
            [FromBody] QuestionDto dto)
        {
            if (!await CanManageCourseAsync(courseId))
                return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Text) || dto.Options.Count < 2 ||
                !dto.Options.Contains(dto.CorrectAnswer))
                return BadRequest(new { message = "La question doit avoir au moins deux options et une bonne réponse valide." });

            var question = await _db.Questions.FirstOrDefaultAsync(q =>
                q.Id == questionId && q.CourseId == courseId);
            if (question == null)
                return NotFound();

            question.Text = dto.Text;
            question.Options = dto.Options;
            question.CorrectAnswer = dto.CorrectAnswer;
            question.Points = Math.Max(1, dto.Points);
            await _db.SaveChangesAsync();
            return Ok(question);
        }

        [HttpDelete("{courseId:guid}/questions/{questionId:guid}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteQuestion(Guid courseId, Guid questionId)
        {
            if (!await CanManageCourseAsync(courseId))
                return Forbid();

            var question = await _db.Questions.FirstOrDefaultAsync(q =>
                q.Id == questionId && q.CourseId == courseId);
            if (question == null)
                return NotFound();

            _db.Questions.Remove(question);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private async Task<bool> CanManageCourseAsync(Guid courseId)
        {
            if (User.IsInRole("Admin"))
                return await _db.Courses.AnyAsync(c => c.Id == courseId);

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            return await _db.Courses.AnyAsync(c =>
                c.Id == courseId && c.InstructorId == userId);
        }

        private async Task<bool> IsEnrolledAsync(Guid courseId)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            return await _db.Enrollments.AnyAsync(e =>
                e.UserId == userId && e.CourseId == courseId);
        }

        [HttpPost("{courseId:guid}/submit")]
        public async Task<IActionResult> Submit(
            Guid courseId,
            [FromBody] TestSubmissionDto submission)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            if (!await IsEnrolledAsync(courseId))
                return Forbid();

            if (!await _db.Courses.AnyAsync(course =>
                course.Id == courseId && course.IsPublished))
                return NotFound();

            var questions = await _db.Questions
                .Where(q => q.CourseId == courseId)
                .ToListAsync();

            decimal totalScore = 0;
            decimal maxScore = questions.Sum(q => q.Points);

            foreach (var question in questions)
            {
                if (submission.Answers.TryGetValue(
                    question.Id.ToString(), out var answer))
                    if (answer == question.CorrectAnswer)
                        totalScore += question.Points;
            }

            bool passed = maxScore > 0 && (totalScore / maxScore) >= 0.6m;

            var result = new TestResult
            {
                CourseId = courseId,
                UserId = userId,
                TakenAt = DateTime.UtcNow,
                Score = totalScore,
                MaxScore = maxScore,
                Passed = passed,
                Answers = submission.Answers
            };
            await _testRepo.SaveResultAsync(result);

            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = passed ? "Test réussi" : "Test terminé",
                Message = $"Votre score est de {totalScore}/{maxScore} ({(maxScore > 0 ? Math.Round(totalScore / maxScore * 100, 1) : 0)} %)."
            });
            await _db.SaveChangesAsync();

            var activity = new UserActivityEvent
            {
                UserId = userId,
                SessionId = submission.SessionId,
                CourseId = courseId,
                ActionType = "quiz_attempt",
                PageUrl = $"/courses/{courseId}/test",
                DeviceType = "web"
            };
            try { await _activityRepo.LogActivityAsync(activity); } catch { }
            try { await _eventProducer.PublishActivityAsync(activity); } catch { }
            try { await _eventProducer.PublishTestResultAsync(result); } catch { }

            return Ok(new
            {
                score = totalScore,
                maxScore,
                passed,
                percentage = maxScore > 0
                    ? Math.Round((totalScore / maxScore) * 100, 1)
                    : 0
            });
        }

        [HttpGet("{courseId:guid}/analytics")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetAnalytics(Guid courseId)
        {
            if (!await CanManageCourseAsync(courseId))
                return Forbid();

            var results = await _testRepo.GetResultsByCourseAsync(courseId);

            return Ok(new
            {
                totalAttempts = results.Count,
                averageScore = results.Any()
                    ? results.Where(r => r.MaxScore > 0)
                        .Select(r => (double)(r.Score / r.MaxScore * 100))
                        .DefaultIfEmpty()
                        .Average()
                    : 0,
                passRate = results.Any()
                    ? (double)results.Count(r => r.Passed) / results.Count * 100
                    : 0
            });
        }
    }

    public class TestSubmissionDto
    {
        public Guid SessionId { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    public class QuestionDto
    {
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string CorrectAnswer { get; set; } = string.Empty;
        public int Points { get; set; } = 1;
    }
}