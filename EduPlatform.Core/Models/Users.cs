using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Student;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSuspended { get; set; } = false;
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    }

    public enum UserRole { Student, Instructor, Admin }
}