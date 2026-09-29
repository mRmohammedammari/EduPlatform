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

        /// <summary>
        /// Avancement de l'apprenant sur chaque module d'un cours :
        /// alimente les pastilles « termine » et la reprise de lecture video.
        /// </summary>
        [HttpGet("course/{courseId:guid}/modules")]
        public async Task<IActionResult> GetModuleProgress(Guid courseId)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var progress = await _db.ModuleProgresses
                .Where(item => item.UserId == userId && item.CourseId == courseId)
                .Select(item => new
                {
                    item.ModuleId,
                    item.LastPositionSeconds,
                    item.WatchedRatio,
                    item.IsCompleted,
                    item.CompletedAt
                })
                .ToListAsync();

            return Ok(progress);
        }

        /// <summary>
        /// Enregistre la position de lecture et la fraction visionnee d'un module.
        /// </summary>
        /// <remarks>
        /// La completion est irreversible : <c>WatchedRatio</c> ne redescend jamais et
        /// <c>IsCompleted</c> ne repasse pas a faux. Un apprenant qui revient au debut
        /// d'une video ne doit pas perdre un module deja valide.
        /// </remarks>
        [HttpPost("module")]
        public async Task<IActionResult> SaveModuleProgress([FromBody] ModuleProgressDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var module = await _db.Modules
                .Where(item => item.Id == dto.ModuleId)
                .Select(item => new { item.Id, item.CourseId })
                .FirstOrDefaultAsync();

            if (module is null)
                return NotFound(new { message = "Module introuvable." });

            var enrolled = await _db.Enrollments.AnyAsync(enrollment =>
                enrollment.UserId == userId && enrollment.CourseId == module.CourseId);
            if (!enrolled)
                return Forbid();

            var ratio = Math.Clamp(dto.WatchedRatio, 0d, 1d);
            var position = Math.Max(0, dto.PositionSeconds);

            var progress = await _db.ModuleProgresses.FirstOrDefaultAsync(item =>
                item.UserId == userId && item.ModuleId == dto.ModuleId);

            if (progress is null)
            {
                progress = new ModuleProgress
                {
                    UserId = userId,
                    ModuleId = dto.ModuleId,
                    CourseId = module.CourseId
                };
                _db.ModuleProgresses.Add(progress);
            }

            progress.LastPositionSeconds = position;
            progress.WatchedRatio = Math.Max(progress.WatchedRatio, ratio);
            progress.UpdatedAt = DateTime.UtcNow;

            // 90 % : les generiques de fin ne doivent pas empecher la validation.
            var reachedCompletion = dto.MarkCompleted || progress.WatchedRatio >= 0.9d;
            if (reachedCompletion && !progress.IsCompleted)
            {
                progress.IsCompleted = true;
                progress.CompletedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            var totalModules = await _db.Modules.CountAsync(item => item.CourseId == module.CourseId);
            var completedModules = await _db.ModuleProgresses.CountAsync(item =>
                item.UserId == userId && item.CourseId == module.CourseId && item.IsCompleted);

            return Ok(new
            {
                progress.ModuleId,
                progress.LastPositionSeconds,
                progress.WatchedRatio,
                progress.IsCompleted,
                CompletedModules = completedModules,
                TotalModules = totalModules,
                CoursePercent = totalModules == 0 ? 0 : completedModules * 100 / totalModules
            });
        }

        /// <summary>
        /// Cours a reprendre, pour le bandeau de la page d'accueil.
        /// </summary>
        /// <remarks>
        /// Endpoint distinct de <c>enrollments</c> : celui-ci interroge Cassandra une fois
        /// par cours inscrit, ce qui est acceptable sur la page de progression mais pas
        /// sur la page d'accueil. Ici le nombre d'appels est borne a <c>limit</c>.
        /// </remarks>
        [HttpGet("continue")]
        public async Task<IActionResult> GetContinueLearning([FromQuery] int limit = 3)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            limit = Math.Clamp(limit, 1, 10);

            var enrollments = await _db.Enrollments
                .Where(enrollment => enrollment.UserId == userId
                    && !enrollment.Course.IsArchived)
                .OrderByDescending(enrollment => enrollment.EnrolledAt)
                .Take(limit)
                .Select(enrollment => new
                {
                    enrollment.CourseId,
                    enrollment.Course.Title,
                    enrollment.Course.Category,
                    enrollment.Course.ThumbnailUrl,
                    enrollment.Course.DurationMinutes,
                    enrollment.EnrolledAt
                })
                .ToListAsync();

            var result = new List<object>(enrollments.Count);
            foreach (var enrollment in enrollments)
            {
                var totalTimeSeconds = await _activityRepo
                    .GetTotalTimeOnCourseAsync(userId, enrollment.CourseId);
                var expectedTimeSeconds = Math.Max(enrollment.DurationMinutes * 60, 1);
                var progressPercent = Math.Clamp(
                    (int)Math.Round(totalTimeSeconds * 100d / expectedTimeSeconds), 0, 100);

                result.Add(new
                {
                    enrollment.CourseId,
                    CourseTitle = enrollment.Title,
                    enrollment.Category,
                    enrollment.ThumbnailUrl,
                    enrollment.EnrolledAt,
                    ProgressPercent = progressPercent
                });
            }

            return Ok(result);
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

    public class ModuleProgressDto
    {
        public Guid ModuleId { get; set; }
        public int PositionSeconds { get; set; }

        /// <summary>Fraction visionnee, de 0 a 1.</summary>
        public double WatchedRatio { get; set; }

        /// <summary>Validation explicite par l'apprenant, sans attendre les 90 %.</summary>
        public bool MarkCompleted { get; set; }
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