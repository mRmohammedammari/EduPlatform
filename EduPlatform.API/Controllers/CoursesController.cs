using EduPlatform.Core.Models;
using EduPlatform.Data.Cache;
using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly EduDbContext _db;
        private readonly CacheService _cache;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly ActivityRepository _activityRepo;
        private readonly TestResultRepository _testRepo;

        public CoursesController(
            EduDbContext db,
            CacheService cache,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ActivityRepository activityRepo,
            TestResultRepository testRepo)
        {
            _db = db;
            _cache = cache;
            _environment = environment;
            _configuration = configuration;
            _activityRepo = activityRepo;
            _testRepo = testRepo;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? category,
            [FromQuery] string? level)
        {
            if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(level))
            {
                var cachedCourses = await _cache.GetAsync<List<Course>>(CacheService.CourseListKey());
                if (cachedCourses != null)
                    return Ok(cachedCourses);
            }

            var query = _db.Courses
                .Include(c => c.Modules)
                .Where(c => c.IsPublished && !c.IsArchived);

            if (!string.IsNullOrEmpty(category))
                query = query.Where(c => c.Category == category);
            if (!string.IsNullOrEmpty(level))
                query = query.Where(c => c.Level == level);

            var courses = await query.ToListAsync();

            if (string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(level))
            {
                await _cache.SetAsync(CacheService.CourseListKey(), courses,
                    TimeSpan.FromMinutes(10));
            }

            return Ok(courses);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var cached = await _cache.GetAsync<CourseStatsDto>(CacheService.CourseStatsKey());
            if (cached != null)
                return Ok(cached);

            var publishedQuery = _db.Courses.Where(c => c.IsPublished && !c.IsArchived);

            var stats = new CourseStatsDto
            {
                PublishedCourses = await publishedQuery.CountAsync(),
                Categories = await publishedQuery.Select(c => c.Category).Distinct().CountAsync(),
                Modules = await _db.Modules
                    .CountAsync(m => publishedQuery.Select(c => c.Id).Contains(m.CourseId)),
                ActiveLearners = await _db.Enrollments.Select(e => e.UserId).Distinct().CountAsync()
            };

            await _cache.SetAsync(CacheService.CourseStatsKey(), stats, TimeSpan.FromMinutes(10));
            return Ok(stats);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var isPrivilegedViewer = false;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = Guid.Parse(User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
                isPrivilegedViewer = User.IsInRole("Admin") ||
                    await _db.Courses.AnyAsync(c => c.Id == id && c.InstructorId == userId);
            }

            if (!isPrivilegedViewer)
            {
                var cached = await _cache.GetAsync<Course>(CacheService.CourseKey(id));
                if (cached != null) return Ok(cached);
            }

            var course = await _db.Courses
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id &&
                    ((!c.IsArchived && c.IsPublished) || isPrivilegedViewer));

            if (course == null) return NotFound();

            if (course.IsPublished)
            {
                await _cache.SetAsync(CacheService.CourseKey(id), course,
                    TimeSpan.FromHours(1));
            }
            return Ok(course);
        }

        [HttpGet("mine")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetMine()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var query = _db.Courses.Include(c => c.Modules).AsQueryable();
            if (!User.IsInRole("Admin"))
                query = query.Where(c => c.InstructorId == userId);

            return Ok(await query.OrderByDescending(c => c.CreatedAt).ToListAsync());
        }

        [HttpGet("instructor-analytics")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetInstructorAnalytics()
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var courses = await _db.Courses
                .Where(c => User.IsInRole("Admin") || c.InstructorId == userId)
                .Select(c => new { c.Id, c.Title, c.IsPublished, c.Status })
                .ToListAsync();

            var courseIds = courses.Select(c => c.Id).ToList();
            var enrollments = await _db.Enrollments
                .Where(e => courseIds.Contains(e.CourseId))
                .GroupBy(e => e.CourseId)
                .Select(group => new { CourseId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(item => item.CourseId, item => item.Count);

            var quizResults = new List<TestResult>();
            foreach (var course in courses)
                quizResults.AddRange(await _testRepo.GetResultsByCourseAsync(course.Id));

            return Ok(new
            {
                totalCourses = courses.Count,
                publishedCourses = courses.Count(c => c.IsPublished),
                pendingCourses = courses.Count(c => c.Status == CourseStatus.PendingReview),
                totalEnrollments = enrollments.Values.Sum(),
                totalQuizAttempts = quizResults.Count,
                averageQuizScore = quizResults.Count == 0
                    ? 0
                    : Math.Round(quizResults.Average(result => result.MaxScore == 0
                        ? 0
                        : (double)(result.Score / result.MaxScore * 100)), 1),
                passRate = quizResults.Count == 0
                    ? 0
                    : Math.Round(quizResults.Count(result => result.Passed) * 100d / quizResults.Count, 1),
                courses = courses.Select(course => new
                {
                    course.Title,
                    course.IsPublished,
                    status = course.Status.ToString(),
                    enrollments = enrollments.GetValueOrDefault(course.Id)
                })
            });
        }

        [HttpPost]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Create([FromBody] CreateCourseDto dto)
        {
            if (!IsValidCourse(dto))
                return BadRequest(new { message = "Le titre, la description, la catégorie et la durée sont obligatoires." });

            var instructorId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var course = new Course
            {
                Title = dto.Title,
                Description = dto.Description,
                Category = dto.Category,
                Level = dto.Level,
                DurationMinutes = dto.DurationMinutes,
                ThumbnailUrl = dto.ThumbnailUrl,
                Price = dto.Price,
                IsPublished = false,
                Status = CourseStatus.Draft,
                InstructorId = instructorId
            };
            _db.Courses.Add(course);
            await _db.SaveChangesAsync();

            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());
            return CreatedAtAction(nameof(GetById), new { id = course.Id }, course);
        }

        [HttpPost("{id:guid}/modules")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateModule(Guid id, [FromBody] CreateModuleDto dto)
        {
            if (!IsValidModule(dto))
                return BadRequest(new { message = "Le titre, le contenu et la durée du module sont obligatoires." });

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            var module = new Module
            {
                CourseId = id,
                Title = dto.Title,
                Description = dto.Description,
                VideoUrl = dto.VideoUrl,
                DurationMinutes = dto.DurationMinutes,
                Order = dto.Order
            };
            _db.Modules.Add(module);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(module);
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CreateCourseDto dto)
        {
            if (!IsValidCourse(dto))
                return BadRequest(new { message = "Le titre, la description, la catégorie et la durée sont obligatoires." });

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            course.Title = dto.Title;
            course.Description = dto.Description;
            course.Category = dto.Category;
            course.Level = dto.Level;
            course.DurationMinutes = dto.DurationMinutes;
            course.ThumbnailUrl = dto.ThumbnailUrl;
            course.Price = dto.Price;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPendingReview()
        {
            var pending = await _db.Courses
                .Include(c => c.Modules)
                .Where(c => c.Status == CourseStatus.PendingReview)
                .OrderBy(c => c.CreatedAt)
                .Join(_db.Users, c => c.InstructorId, u => u.Id, (c, u) => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.Category,
                    c.Level,
                    c.DurationMinutes,
                    ModuleCount = c.Modules.Count,
                    InstructorName = u.FirstName + " " + u.LastName,
                    InstructorEmail = u.Email
                })
                .ToListAsync();

            return Ok(pending);
        }

        [HttpPost("{id:guid}/submit-for-review")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> SubmitForReview(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();
            if (!course.Modules.Any())
                return BadRequest(new { message = "Ajoutez au moins un module avant de soumettre le cours." });
            if (course.Status is not (CourseStatus.Draft or CourseStatus.Rejected))
                return BadRequest(new { message = "Ce cours a déjà été soumis ou approuvé." });

            course.Status = CourseStatus.PendingReview;
            course.RejectionReason = null;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));

            return Ok(course);
        }

        [HttpPost("{id:guid}/approve")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(Guid id)
        {
            var course = await _db.Courses.FindAsync(id);
            if (course == null)
                return NotFound();
            if (course.Status != CourseStatus.PendingReview)
                return BadRequest(new { message = "Ce cours n'est pas en attente de validation." });

            course.Status = CourseStatus.Approved;
            course.IsPublished = true;
            course.RejectionReason = null;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCourseDto? dto)
        {
            var course = await _db.Courses.FindAsync(id);
            if (course == null)
                return NotFound();
            if (course.Status != CourseStatus.PendingReview)
                return BadRequest(new { message = "Ce cours n'est pas en attente de validation." });
            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "Le motif du rejet est obligatoire." });

            course.Status = CourseStatus.Rejected;
            course.IsPublished = false;
            course.RejectionReason = dto.Reason.Trim();
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));

            return Ok(course);
        }

        [HttpPost("{id:guid}/publish")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Publish(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();
            if (!course.Modules.Any())
                return BadRequest(new { message = "Ajoutez au moins un module avant de publier." });
            if (course.Status != CourseStatus.Approved && !User.IsInRole("Admin"))
                return BadRequest(new { message = "Ce cours doit d'abord être validé par un administrateur." });
            if (course.Status != CourseStatus.Approved)
                course.Status = CourseStatus.Approved;

            course.IsPublished = true;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpPost("{id:guid}/unpublish")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Unpublish(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            course.IsPublished = false;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpPost("{id:guid}/archive")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Archive(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            course.IsPublished = false;
            course.IsArchived = true;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpPost("{id:guid}/restore")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> Restore(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);

            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            course.IsArchived = false;
            course.IsPublished = false;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteArchived(Guid id, [FromQuery] string confirm)
        {
            if (!string.Equals(confirm, "DELETE", StringComparison.Ordinal))
                return BadRequest(new { message = "Confirmation DELETE requise." });

            var course = await _db.Courses.FirstOrDefaultAsync(item =>
                item.Id == id && item.IsArchived);
            if (course == null)
                return NotFound(new { message = "Seuls les cours archivés peuvent être supprimés." });

            _db.Courses.Remove(course);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return NoContent();
        }

        [HttpDelete("{courseId:guid}/modules/{moduleId:guid}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> DeleteModule(Guid courseId, Guid moduleId)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(courseId);
            var module = await _db.Modules.FirstOrDefaultAsync(m =>
                m.Id == moduleId && m.CourseId == courseId);

            if (course == null || module == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            _db.Modules.Remove(module);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(courseId));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return NoContent();
        }

        [HttpPut("{courseId:guid}/modules/{moduleId:guid}")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> UpdateModule(
            Guid courseId,
            Guid moduleId,
            [FromBody] CreateModuleDto dto)
        {
            if (!IsValidModule(dto))
                return BadRequest(new { message = "Le titre, le contenu et la durée du module sont obligatoires." });

            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(courseId);
            var module = await _db.Modules.FirstOrDefaultAsync(m =>
                m.Id == moduleId && m.CourseId == courseId);

            if (course == null || module == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            module.Title = dto.Title;
            module.Description = dto.Description;
            module.VideoUrl = dto.VideoUrl;
            module.DurationMinutes = dto.DurationMinutes;
            module.Order = dto.Order;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(courseId));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(module);
        }

        [HttpPost("{courseId:guid}/modules/{moduleId:guid}/video")]
        [Authorize(Roles = "Instructor,Admin")]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadModuleVideo(
            Guid courseId,
            Guid moduleId,
            IFormFile video)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(courseId);
            var module = await _db.Modules.FirstOrDefaultAsync(m =>
                m.Id == moduleId && m.CourseId == courseId);

            if (course == null || module == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();
            const long maxVideoSize = 100_000_000;
            if (video == null || video.Length == 0)
                return BadRequest(new { message = "Sélectionnez une vidéo." });
            if (video.Length > maxVideoSize)
                return BadRequest(new { message = "La vidéo ne doit pas dépasser 100 Mo." });

            var extension = Path.GetExtension(video.FileName).ToLowerInvariant();
            var allowedContentTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                [".mp4"] = ["video/mp4"],
                [".webm"] = ["video/webm"],
                [".ogg"] = ["video/ogg"]
            };
            if (!allowedContentTypes.TryGetValue(extension, out var contentTypes)
                || !contentTypes.Contains(video.ContentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "Formats acceptés : mp4, webm, ogg." });

            var webRoot = _environment.WebRootPath ??
                Path.Combine(_environment.ContentRootPath, "wwwroot");
            var relativeDirectory = Path.Combine("uploads", "courses", courseId.ToString());
            var directory = Path.Combine(webRoot, relativeDirectory);
            Directory.CreateDirectory(directory);

            var fileName = $"{moduleId}{extension}";
            var filePath = Path.Combine(directory, fileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await video.CopyToAsync(stream);
            }

            var publicBaseUrl = _configuration["PublicBaseUrl"]
                ?? $"{Request.Scheme}://{Request.Host}";
            module.VideoUrl = $"{publicBaseUrl.TrimEnd('/')}/uploads/courses/{courseId}/{fileName}";
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(courseId));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(module);
        }

        [HttpPost("{id:guid}/thumbnail")]
        [Authorize(Roles = "Instructor,Admin")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> UploadThumbnail(Guid id, IFormFile thumbnail)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);
            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();
            if (thumbnail == null || thumbnail.Length == 0)
                return BadRequest(new { message = "Sélectionnez une image." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(thumbnail.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Formats acceptés : jpg, png, webp." });

            var webRoot = _environment.WebRootPath ??
                Path.Combine(_environment.ContentRootPath, "wwwroot");
            var relativeDirectory = Path.Combine("uploads", "courses", id.ToString());
            var directory = Path.Combine(webRoot, relativeDirectory);
            Directory.CreateDirectory(directory);

            var fileName = $"thumbnail{extension}";
            var filePath = Path.Combine(directory, fileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await thumbnail.CopyToAsync(stream);
            }

            var publicBaseUrl = _configuration["PublicBaseUrl"]
                ?? $"{Request.Scheme}://{Request.Host}";
            course.ThumbnailUrl = $"{publicBaseUrl.TrimEnd('/')}/uploads/courses/{id}/{fileName}?v={DateTime.UtcNow.Ticks}";
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseStatsKey());

            return Ok(course);
        }

        private static bool IsValidCourse(CreateCourseDto dto)
        {
            return !string.IsNullOrWhiteSpace(dto.Title)
                && !string.IsNullOrWhiteSpace(dto.Description)
                && !string.IsNullOrWhiteSpace(dto.Category)
                && dto.DurationMinutes > 0
                && dto.Price >= 0;
        }

        private static bool IsValidModule(CreateModuleDto dto)
        {
            return !string.IsNullOrWhiteSpace(dto.Title)
                && !string.IsNullOrWhiteSpace(dto.Description)
                && dto.DurationMinutes > 0
                && dto.Order > 0;
        }

        [HttpPost("{id:guid}/enroll")]
        [Authorize]
        public async Task<IActionResult> Enroll(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var course = await _db.Courses.FirstOrDefaultAsync(c =>
                c.Id == id && c.IsPublished);
            if (course == null)
                return NotFound(new { message = "Cours introuvable ou non publié." });
            if (course.Price > 0)
                return BadRequest(new { message = "Utilisez le parcours d'inscription avec sélection de plan." });

            if (_db.Enrollments.Any(e => e.UserId == userId && e.CourseId == id))
                return BadRequest(new { message = "Déjà inscrit à ce cours" });

            var enrollment = new Enrollment
            {
                UserId = userId,
                CourseId = id,
                EnrolledAt = DateTime.UtcNow
            };
            _db.Enrollments.Add(enrollment);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Inscription réussie" });
        }

        [HttpGet("{id:guid}/enrollment-status")]
        [Authorize]
        public IActionResult GetEnrollmentStatus(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var isEnrolled = _db.Enrollments
                .Any(e => e.UserId == userId && e.CourseId == id);
            return Ok(isEnrolled);
        }

        [HttpGet("{id:guid}/students")]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> GetStudents(Guid id)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var course = await _db.Courses.FindAsync(id);
            if (course == null)
                return NotFound();
            if (!User.IsInRole("Admin") && course.InstructorId != userId)
                return Forbid();

            var enrollments = await _db.Enrollments
                .Where(e => e.CourseId == id)
                .Include(e => e.User)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            var expectedTimeSeconds = Math.Max(course.DurationMinutes * 60, 1);
            var students = new List<object>();
            foreach (var enrollment in enrollments)
            {
                var totalTimeSeconds = await _activityRepo
                    .GetTotalTimeOnCourseAsync(enrollment.UserId, id);
                var progressPercent = Math.Min(
                    100,
                    (int)Math.Round(totalTimeSeconds * 100d / expectedTimeSeconds));

                students.Add(new
                {
                    UserId = enrollment.UserId,
                    FirstName = enrollment.User.FirstName,
                    LastName = enrollment.User.LastName,
                    Email = enrollment.User.Email,
                    enrollment.EnrolledAt,
                    ProgressPercent = progressPercent
                });
            }

            return Ok(students);
        }
    }

    public class CreateCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Level { get; set; } = "Debutant";
        public int DurationMinutes { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class RejectCourseDto
    {
        public string? Reason { get; set; }
    }

    public class CreateModuleDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int Order { get; set; }
    }

    public class CourseStatsDto
    {
        public int PublishedCourses { get; set; }
        public int Categories { get; set; }
        public int Modules { get; set; }
        public int ActiveLearners { get; set; }
    }
}