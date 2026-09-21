using System.Security.Claims;
using EduPlatform.API.Controllers;
using EduPlatform.Core.Models;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduPlatform.Tests;

public class ReviewsControllerTests
{
    [Fact]
    public async Task Report_CreatesReportForEnrolledUser()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db, enrolled: true);
        var controller = CreateController(db, fixture.Reporter.Id);

        var result = await controller.Report(fixture.Review.Id, new ReviewReportDto
        {
            Reason = "Ce commentaire contient un lien inapproprié."
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Single(db.CourseReviewReports);
    }

    [Fact]
    public async Task Report_RejectsDuplicateReportFromSameUser()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db, enrolled: true);
        db.CourseReviewReports.Add(new CourseReviewReport
        {
            ReviewId = fixture.Review.Id,
            ReporterId = fixture.Reporter.Id,
            Reason = "Déjà signalé"
        });
        await db.SaveChangesAsync();
        var controller = CreateController(db, fixture.Reporter.Id);

        var result = await controller.Report(fixture.Review.Id, new ReviewReportDto
        {
            Reason = "Nouveau motif"
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Report_RejectsUserWhoIsNotEnrolled()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db, enrolled: false);
        var controller = CreateController(db, fixture.Reporter.Id);

        var result = await controller.Report(fixture.Review.Id, new ReviewReportDto
        {
            Reason = "Motif"
        });

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Report_RejectsOwnReview()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db, enrolled: true);
        var controller = CreateController(db, fixture.Review.UserId);

        var result = await controller.Report(fixture.Review.Id, new ReviewReportDto
        {
            Reason = "Motif"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Report_RejectsMissingReason()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db, enrolled: true);
        var controller = CreateController(db, fixture.Reporter.Id);

        var result = await controller.Report(fixture.Review.Id, new ReviewReportDto());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Report_ReturnsNotFoundForUnknownReview()
    {
        await using var db = CreateContext();
        var reporter = new User { Email = "reporter@example.com" };
        db.Users.Add(reporter);
        await db.SaveChangesAsync();
        var controller = CreateController(db, reporter.Id);

        var result = await controller.Report(Guid.NewGuid(), new ReviewReportDto
        {
            Reason = "Motif"
        });

        Assert.IsType<NotFoundResult>(result);
    }

    private static ReviewsController CreateController(EduDbContext db, Guid userId)
    {
        var controller = new ReviewsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                        "TestAuth"))
                }
            }
        };
        return controller;
    }

    private static (CourseReview Review, User Reporter) SeedReview(EduDbContext db, bool enrolled)
    {
        var course = new Course { Title = "Cours de test" };
        var author = new User { Email = "author@example.com", FirstName = "Auteur" };
        var reporter = new User { Email = "reporter@example.com", FirstName = "Rapporteur" };
        var review = new CourseReview
        {
            Course = course,
            User = author,
            Rating = 2,
            Comment = "Avis à vérifier"
        };
        db.Courses.Add(course);
        db.Users.AddRange(author, reporter);
        db.CourseReviews.Add(review);
        if (enrolled)
        {
            db.Enrollments.Add(new Enrollment
            {
                Course = course,
                User = reporter,
                CourseId = course.Id,
                UserId = reporter.Id
            });
        }
        db.SaveChanges();
        return (review, reporter);
    }

    private static EduDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EduDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EduDbContext(options);
    }
}
