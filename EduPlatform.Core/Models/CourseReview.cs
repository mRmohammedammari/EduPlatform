namespace EduPlatform.Core.Models;

public class CourseReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Course Course { get; set; } = null!;
    public User User { get; set; } = null!;
}
