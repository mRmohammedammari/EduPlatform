using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    public class TestResult
    {
        public Guid CourseId { get; set; }
        public Guid UserId { get; set; }
        public DateTime TakenAt { get; set; } = DateTime.UtcNow;
        public decimal Score { get; set; }
        public decimal MaxScore { get; set; }
        public bool Passed { get; set; }
        public Dictionary<string, string> Answers { get; set; } = new();
    }
}