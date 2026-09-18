using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CourseStatus { Draft, PendingReview, Approved, Rejected }

    public class Course
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Level { get; set; } = "Debutant";
        public int DurationMinutes { get; set; }
        public string ThumbnailUrl { get; set; } = string.Empty;
        public Guid InstructorId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsPublished { get; set; } = false;
        public bool IsArchived { get; set; } = false;
        public decimal Price { get; set; } = 0;
        public CourseStatus Status { get; set; } = CourseStatus.Draft;
        public string? RejectionReason { get; set; }
        public ICollection<Module> Modules { get; set; } = new List<Module>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }
}