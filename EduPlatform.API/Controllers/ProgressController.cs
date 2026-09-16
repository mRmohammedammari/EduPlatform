using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
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
    public class ProgressController : ControllerBase
    {
        private readonly ActivityRepository _activityRepo;
        private readonly EventProducer _eventProducer;
        private readonly EduDbContext _db;
        private readonly IPaymentGateway _paymentGateway;

        public ProgressController(
            ActivityRepository activityRepo,
            EventProducer eventProducer,
            EduDbContext db,
            IPaymentGateway paymentGateway)
        {
            _activityRepo = activityRepo;
            _eventProducer = eventProducer;
            _db = db;
            _paymentGateway = paymentGateway;
        }

        [HttpGet("enrollments")]
        public async Task<IActionResult> GetEnrollments()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var enrollments = await _db.Enrollments
                .Where(e => e.UserId == userId)
                .Include(e => e.Course)
                .ToListAsync();

            var progress = new List<object>();
            foreach (var enrollment in enrollments)
            {
                var totalTimeSeconds = await _activityRepo
                    .GetTotalTimeOnCourseAsync(userId, enrollment.CourseId);
                var expectedTimeSeconds = Math.Max(enrollment.Course.DurationMinutes * 60, 1);
                var progressPercent = Math.Min(
                    100,
                    (int)Math.Round(totalTimeSeconds * 100d / expectedTimeSeconds));

                progress.Add(new
                {
                    enrollment.CourseId,
                    CourseTitle = enrollment.Course.Title,
                    enrollment.EnrolledAt,
                    ProgressPercent = progressPercent
                });
            }

            return Ok(progress);
        }

        [HttpPost("enroll")]
        public async Task<IActionResult> Enroll([FromBody] EnrollDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var course = await _db.Courses.FirstOrDefaultAsync(c =>
                c.Id == dto.CourseId && c.IsPublished);
            if (course == null)
                return NotFound(new { message = "Cours introuvable ou non publié." });

            var plan = dto.Plan?.Trim();
            var expectedAmount = plan switch
            {
                "Free" => 0m,
                "Standard" => course.Price,
                "Premium" => course.Price * 1.5m,
                _ => -1m
            };
            if (expectedAmount < 0)
                return BadRequest(new { message = "Plan de cours invalide." });
            if (Math.Abs(dto.AmountPaid - expectedAmount) > 0.01m)
                return BadRequest(new { message = "Le montant du plan ne correspond pas au cours." });

            var exists = await _db.Enrollments
                .AnyAsync(e => e.UserId == userId && e.CourseId == dto.CourseId);

            if (exists)
                return BadRequest(new { message = "Déjà inscrit à ce cours" });

            var payment = _paymentGateway.CreateCheckoutSession(new PaymentRequest
            {
                CourseId = dto.CourseId,
                UserId = userId,
                CourseTitle = course.Title,
                Plan = plan!,
                Amount = expectedAmount,
                Currency = "DZD"
            });

            if (!payment.IsSuccess)
                return BadRequest(new { message = payment.ErrorMessage ?? "Paiement refusé." });

            _db.Enrollments.Add(new Enrollment
            {
                UserId = userId,
                CourseId = dto.CourseId,
                Plan = plan!,
                AmountPaid = expectedAmount,
                PaymentStatus = payment.Status == "Paid" ? "Completed" : "Completed"
            });
            _db.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Inscription confirmée",
                Message = $"Vous êtes maintenant inscrit au cours « {course.Title} »."
            });
            await _db.SaveChangesAsync();

            var activity = new UserActivityEvent
            {
                UserId = userId,
                SessionId = dto.SessionId,
                CourseId = dto.CourseId,
                ActionType = "course_enrolled",
                PageUrl = $"/courses/{dto.CourseId}",
                DeviceType = "web"
            };
            await PublishActivityAsync(activity);

            return Ok(new { message = "Inscription réussie" });
        }

        [HttpPost("log")]
        public async Task<IActionResult> LogActivity([FromBody] ActivityLogDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var activity = new UserActivityEvent
            {
                UserId = userId,
                SessionId = dto.SessionId,
                CourseId = dto.CourseId,
                ActionType = dto.ActionType,
                DurationSec = dto.DurationSec,
                PageUrl = dto.PageUrl,
                DeviceType = dto.DeviceType
            };
            if (dto.ActionType == "module_completed")
            {
                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Title = "Module terminé",
                    Message = "Votre progression a été mise à jour après la fin du module."
                });
                await _db.SaveChangesAsync();
            }
            await PublishActivityAsync(activity);

            return Ok();
        }

        [HttpGet("{courseId:guid}")]
        public async Task<IActionResult> GetProgress(Guid courseId)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var totalTime = await _activityRepo
                .GetTotalTimeOnCourseAsync(userId, courseId);

            return Ok(new { totalTimeSeconds = totalTime });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var activities = await _activityRepo
                .GetUserActivitiesAsync(userId, 20);

            return Ok(activities);
        }

        private async Task PublishActivityAsync(UserActivityEvent activity)
        {
            try
            {
                await _activityRepo.LogActivityAsync(activity);
            }
            catch
            {
            }

            try
            {
                await _eventProducer.PublishActivityAsync(activity);
            }
            catch
            {
            }
        }
    }

    public class ActivityLogDto
    {
        public Guid CourseId { get; set; }
        public Guid SessionId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int DurationSec { get; set; }
        public string PageUrl { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "web";
    }

    public class EnrollDto
    {
        public Guid CourseId { get; set; }
        public Guid SessionId { get; set; }
        public string? Plan { get; set; }
        public decimal AmountPaid { get; set; }
    }
}