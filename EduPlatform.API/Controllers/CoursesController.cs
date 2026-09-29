using EduPlatform.Core.Models;
using EduPlatform.Core.Services;
using EduPlatform.BigData.Kafka;
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
        private readonly EventProducer _eventProducer;

        public CoursesController(
            EduDbContext db,
            CacheService cache,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ActivityRepository activityRepo,
            TestResultRepository testRepo,
            EventProducer eventProducer)
        {
            _db = db;
            _cache = cache;
            _environment = environment;
            _configuration = configuration;
            _activityRepo = activityRepo;
            _testRepo = testRepo;
            _eventProducer = eventProducer;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? category,
            [FromQuery] string? level)
        {
            var isUnfiltered = string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(level);
            List<CourseListItemDto>? courses = null;

            if (isUnfiltered)
                courses = await _cache.GetAsync<List<CourseListItemDto>>(CacheService.CourseListKey());

            if (courses is null)
            {
                var query = _db.Courses.Where(c => c.IsPublished && !c.IsArchived);

                if (!string.IsNullOrEmpty(category))
                    query = query.Where(c => c.Category == category);
                if (!string.IsNullOrEmpty(level))
                    query = query.Where(c => c.Level == level);

                courses = await query
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new CourseListItemDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        Description = c.Description,
                        Category = c.Category,
                        Level = c.Level,
                        DurationMinutes = c.DurationMinutes,
                        ThumbnailUrl = c.ThumbnailUrl,
                        Price = c.Price,
                        CreatedAt = c.CreatedAt,
                        ModuleCount = c.Modules.Count,
                        EnrollmentCount = c.Enrollments.Count
                    })
                    .ToListAsync();

                if (isUnfiltered)
                {
                    await _cache.SetAsync(CacheService.CourseListKey(), courses,
                        TimeSpan.FromMinutes(10));
                }
            }

            // Les notes sont volontairement calculees hors cache : un avis publie ou modere
            // doit se refleter immediatement, alors que la liste de cours tolere 10 minutes.
            await ApplyRatingsAsync(courses);
            return Ok(courses);
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] string? category,
            [FromQuery] string? level,
            [FromQuery] string? search,
            [FromQuery] string? sort = "recent",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var query = _db.Courses
                .Where(course => course.IsPublished && !course.IsArchived)
                .Select(course => new CourseListItemDto
                {
                    Id = course.Id,
                    Title = course.Title,
                    Description = course.Description,
                    Category = course.Category,
                    Level = course.Level,
                    DurationMinutes = course.DurationMinutes,
                    ThumbnailUrl = course.ThumbnailUrl,
                    Price = course.Price,
                    CreatedAt = course.CreatedAt,
                    ModuleCount = course.Modules.Count,
                    EnrollmentCount = course.Enrollments.Count,
                    AverageRating = _db.CourseReviews
                        .Where(review => review.CourseId == course.Id)
                        .Select(review => (double?)review.Rating)
                        .Average() ?? 0,
                    ReviewCount = _db.CourseReviews.Count(review => review.CourseId == course.Id)
                });

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(course => course.Category == category.Trim());
            if (!string.IsNullOrWhiteSpace(level))
                query = query.Where(course => course.Level == level.Trim());
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(course =>
                    course.Title.Contains(term)
                    || course.Description.Contains(term)
                    || course.Category.Contains(term));
            }

            var totalItems = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Min(page, totalPages);
            var ordered = (sort ?? "recent").Trim().ToLowerInvariant() switch
            {
                "rating" => query
                    .OrderByDescending(course => course.ReviewCount > 0 ? course.AverageRating : -1)
                    .ThenByDescending(course => course.ReviewCount)
                    .ThenByDescending(course => course.CreatedAt),
                "popular" => query
                    .OrderByDescending(course => course.EnrollmentCount)
                    .ThenByDescending(course => course.CreatedAt),
                "title" => query.OrderBy(course => course.Title),
                "duration-asc" => query.OrderBy(course => course.DurationMinutes)
                    .ThenByDescending(course => course.CreatedAt),
                "duration-desc" => query.OrderByDescending(course => course.DurationMinutes)
                    .ThenByDescending(course => course.CreatedAt),
                _ => query.OrderByDescending(course => course.CreatedAt)
            };

            var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
            var items = await ordered.Skip(skip).Take(pageSize).ToListAsync();

            return Ok(new PagedCourseResultDto
            {
                Items = items,
                TotalItems = totalItems,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetPublicCategories()
        {
            var cached = await _cache.GetAsync<List<CourseCategoryDto>>(
                CacheService.CourseCategoriesKey());
            if (cached != null)
                return Ok(cached);

            var disabled = await _db.CourseCategories
                .Where(item => !item.IsActive)
                .Select(item => item.Name)
                .ToListAsync();

            // Seules les categories qui contiennent au moins un cours publie sont exposees :
            // une tuile de la page d'accueil doit toujours mener a des resultats.
            var categories = await _db.Courses
                .Where(c => c.IsPublished && !c.IsArchived && c.Category != "")
                .GroupBy(c => c.Category)
                .Select(group => new CourseCategoryDto
                {
                    Name = group.Key,
                    CourseCount = group.Count(),
                    ThumbnailUrl = group
                        .Where(c => c.ThumbnailUrl != "")
                        .OrderByDescending(c => c.CreatedAt)
                        .Select(c => c.ThumbnailUrl)
                        .FirstOrDefault() ?? string.Empty
                })
                .ToListAsync();

            categories = categories
                .Where(item => !disabled.Contains(item.Name, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CourseCount)
                .ThenBy(item => item.Name)
                .ToList();

            await _cache.SetAsync(CacheService.CourseCategoriesKey(), categories,
                TimeSpan.FromMinutes(10));
            return Ok(categories);
        }

        private async Task ApplyRatingsAsync(List<CourseListItemDto> courses)
        {
            if (courses.Count == 0)
                return;

            var ids = courses.Select(course => course.Id).ToList();
            var ratings = await _db.CourseReviews
                .Where(review => ids.Contains(review.CourseId))
                .GroupBy(review => review.CourseId)
                .Select(group => new
                {
                    CourseId = group.Key,
                    Average = group.Average(review => (double)review.Rating),
                    Count = group.Count()
                })
                .ToListAsync();

            var byCourse = ratings.ToDictionary(item => item.CourseId);
            foreach (var course in courses)
            {
                if (!byCourse.TryGetValue(course.Id, out var rating))
                {
                    course.AverageRating = 0;
                    course.ReviewCount = 0;
                    continue;
                }

                course.AverageRating = Math.Round(rating.Average, 1);
                course.ReviewCount = rating.Count;
            }
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
                // Le contenu du module est rendu en HTML brut cote Web : on ne stocke
                // que du balisage assaini, sans script ni attribut executable.
                Description = LessonHtml.Sanitize(dto.Description),
                VideoUrl = dto.VideoUrl,
                DurationMinutes = dto.DurationMinutes,
                Order = dto.Order
            };
            _db.Modules.Add(module);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(id));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            module.Description = LessonHtml.Sanitize(dto.Description);
            module.VideoUrl = dto.VideoUrl;
            module.DurationMinutes = dto.DurationMinutes;
            module.Order = dto.Order;
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync(CacheService.CourseKey(courseId));
            await _cache.RemoveAsync(CacheService.CourseListKey());
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            if (video is null)
                return BadRequest(new { message = "Sélectionnez une vidéo." });
            var validationError = CourseMediaValidator.ValidateVideo(
                video.FileName, video.ContentType, video.Length);
            if (validationError is not null)
                return BadRequest(new { message = validationError });

            var extension = Path.GetExtension(video.FileName).ToLowerInvariant();

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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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
            await _cache.RemoveAsync(CacheService.CourseCategoriesKey());
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

            await PublishActivityBestEffortAsync(new UserActivityEvent
            {
                UserId = userId,
                CourseId = id,
                ActionType = "course_enrolled",
                PageUrl = $"/courses/{id}",
                DeviceType = "web"
            });

            return Ok(new { message = "Inscription réussie" });
        }

        private async Task PublishActivityBestEffortAsync(UserActivityEvent activity)
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

    /// <summary>
    /// Projection du catalogue public. Volontairement distincte de l'entite <see cref="Course"/> :
    /// elle expose la vignette et les agregats d'affichage sans transporter les modules complets.
    /// </summary>
    public class CourseListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ModuleCount { get; set; }
        public int EnrollmentCount { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }

    public class PagedCourseResultDto
    {
        public List<CourseListItemDto> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class CourseCategoryDto
    {
        public string Name { get; set; } = string.Empty;
        public int CourseCount { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
    }

    public class CourseStatsDto
    {
        public int PublishedCourses { get; set; }
        public int Categories { get; set; }
        public int Modules { get; set; }
        public int ActiveLearners { get; set; }
    }
}