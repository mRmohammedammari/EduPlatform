using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace EduPlatform.Core.Models
{
    public class Module
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public int Order { get; set; }
        public Guid CourseId { get; set; }
        [JsonIgnore]
        public Course Course { get; set; } = null!;
    }
}