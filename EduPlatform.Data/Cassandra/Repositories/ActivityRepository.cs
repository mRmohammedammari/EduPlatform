using Cassandra;
using EduPlatform.Core.Models;

namespace EduPlatform.Data.Cassandra.Repositories
{
    public class ActivityRepository
    {
        private readonly ISession _session;
        private readonly PreparedStatement _insertActivity;
        private readonly PreparedStatement _selectByUser;

        public ActivityRepository(CassandraContext context)
        {
            _session = context.Session;

            _insertActivity = _session.Prepare(
                "INSERT INTO user_activities (user_id, course_id, timestamp, session_id, action_type, duration_sec, page_url, device_type) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?)");

            _selectByUser = _session.Prepare(
                "SELECT * FROM user_activities WHERE user_id = ? ALLOW FILTERING");
        }

        public async Task LogActivityAsync(UserActivityEvent evt)
        {
            var bound = _insertActivity.Bind(
                evt.UserId, evt.CourseId, evt.EventTime, evt.SessionId,
                evt.ActionType, evt.DurationSec, evt.PageUrl, evt.DeviceType);
            await _session.ExecuteAsync(bound);
        }

        public async Task<List<UserActivityEvent>> GetUserActivitiesAsync(Guid userId, int limit = 50)
        {
            var bound = _selectByUser.Bind(userId);
            var rows = await _session.ExecuteAsync(bound);

            return rows.Select(row => new UserActivityEvent
            {
                UserId = row.GetValue<Guid?>("user_id") ?? Guid.Empty,
                SessionId = row.GetValue<Guid?>("session_id") ?? Guid.Empty,
                EventTime = row.GetValue<DateTimeOffset>("timestamp").DateTime,
                CourseId = row.GetValue<Guid?>("course_id") ?? Guid.Empty,
                ActionType = row.GetValue<string>("action_type") ?? string.Empty,
                DurationSec = row.GetValue<int?>("duration_sec") ?? 0,
                PageUrl = row.GetValue<string>("page_url") ?? string.Empty,
                DeviceType = row.GetValue<string>("device_type") ?? "web"
            }).Take(limit).ToList();
        }

        public async Task<int> GetTotalTimeOnCourseAsync(Guid userId, Guid courseId)
        {
            var cql = "SELECT duration_sec FROM user_activities WHERE user_id = ? AND course_id = ?";
            var rows = await _session.ExecuteAsync(new SimpleStatement(cql, userId, courseId));
            return rows.Sum(r => r.GetValue<int?>("duration_sec") ?? 0);
        }
    }
}