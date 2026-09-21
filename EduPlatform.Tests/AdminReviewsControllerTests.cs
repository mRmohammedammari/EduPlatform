using EduPlatform.API.Controllers;
using EduPlatform.Core.Models;
using EduPlatform.Data.SqlServer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduPlatform.Tests;

public class AdminReviewsControllerTests
{
    [Fact]
    public async Task GetReviews_ReturnsReviewAndReportHistory()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db);
        db.CourseReviewReports.Add(new CourseReviewReport
        {
            ReviewId = fixture.Review.Id,
            ReporterId = fixture.Reporter.Id,
            Reason = "Motif de modération"
        });
        await db.SaveChangesAsync();
        var controller = new AdminController(db);

        var result = await controller.GetReviews();

        var response = Assert.IsType<OkObjectResult>(result);
        var reviews = Assert.IsAssignableFrom<IEnumerable<object>>(response.Value);
        Assert.Single(reviews);
    }

    [Fact]
    public async Task DeleteReview_RemovesReviewAndCascadesReports()
    {
        await using var db = CreateContext();
        var fixture = SeedReview(db);
        db.CourseReviewReports.Add(new CourseReviewReport
        {
            ReviewId = fixture.Review.Id,
            ReporterId = fixture.Reporter.Id,
            Reason = "Motif de modération"
        });
        await db.SaveChangesAsync();
        var controller = new AdminController(db);

        var result = await controller.DeleteReview(fixture.Review.Id);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.CourseReviews);
        Assert.Empty(db.CourseReviewReports);
    }

    private static (CourseReview Review, User Reporter) SeedReview(EduDbContext db)
    {
        var course = new Course { Title = "Cours de modération" };
        var author = new User { Email = "author@example.com", FirstName = "Auteur" };
        var reporter = new User { Email = "reporter@example.com", FirstName = "Rapporteur" };
        var review = new CourseReview
        {
            Course = course,
            User = author,
            Rating = 4,
            Comment = "Avis à conserver"
        };
        db.Courses.Add(course);
        db.Users.AddRange(author, reporter);
        db.CourseReviews.Add(review);
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
