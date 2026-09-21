namespace EduPlatform.Core.Models;

public class CourseReviewReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReviewId { get; set; }
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public CourseReview Review { get; set; } = null!;
    public User Reporter { get; set; } = null!;
}
