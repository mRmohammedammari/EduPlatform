using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    public class Question
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public string CorrectAnswer { get; set; } = string.Empty;
        public int Points { get; set; } = 1;
        public Guid CourseId { get; set; }
    }
}