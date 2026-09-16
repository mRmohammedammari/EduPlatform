using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    public class Enrollment
    {
        public Guid UserId { get; set; }
        public Guid CourseId { get; set; }
        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
        public string Plan { get; set; } = "Free";
        public decimal AmountPaid { get; set; } = 0;
        public string PaymentStatus { get; set; } = "Completed";
        public User User { get; set; } = null!;
        public Course Course { get; set; } = null!;
    }
}