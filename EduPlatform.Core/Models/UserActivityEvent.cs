using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EduPlatform.Core.Models
{
    public class UserActivityEvent
    {
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
        public Guid CourseId { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public int DurationSec { get; set; }
        public string PageUrl { get; set; } = string.Empty;
        public string DeviceType { get; set; } = "web";
    }
}