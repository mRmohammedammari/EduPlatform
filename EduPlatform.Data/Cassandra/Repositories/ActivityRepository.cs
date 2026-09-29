using Cassandra;
using EduPlatform.Core.Models;

namespace EduPlatform.Data.Cassandra.Repositories
{
    public class ActivityRepository
    {
        private readonly ISession _session;
        private readonly PreparedStatement _insertActivity;
        private readonly PreparedStatement _selectByUserAndCourse;

        public ActivityRepository(CassandraContext context)
        {
            _session = context.Session;

            _insertActivity = _session.Prepare(
                "INSERT INTO user_activities (user_id, course_id, timestamp, session_id, action_type, duration_sec, page_url, device_type) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?, ?)");

            _selectByUserAndCourse = _session.Prepare(
                "SELECT * FROM user_activities WHERE user_id = ? AND course_id = ?");
        }

        public async Task LogActivityAsync(UserActivityEvent evt)
        {
            var bound = _insertActivity.Bind(
                evt.UserId, evt.CourseId, evt.EventTime, evt.SessionId,
                evt.ActionType, evt.DurationSec, evt.PageUrl, evt.DeviceType);
            await _session.ExecuteAsync(bound);
        }

        public async Task<List<UserActivityEvent>> GetUserActivitiesAsync(
            Guid userId,
            IEnumerable<Guid> courseIds,
            int limit = 50)
        {
            limit = Math.Clamp(limit, 1, 100);
            var courses = courseIds.Where(courseId => courseId != Guid.Empty).Distinct().ToArray();
            if (courses.Length == 0)
                return new List<UserActivityEvent>();

            var activities = new List<UserActivityEvent>();
            foreach (var courseBatch in courses.Chunk(16))
            {
                var batchResults = await Task.WhenAll(courseBatch.Select(async courseId =>
                {
                    // LIMIT is interpolated only after clamping to a small integer range.
                    var query = new SimpleStatement(
                        $"SELECT * FROM user_activities WHERE user_id = ? AND course_id = ? LIMIT {limit}",
                        userId,
                        courseId);
                    var rows = await _session.ExecuteAsync(query);
                    return rows.Select(MapActivity).ToList();
                }));

                activities.AddRange(batchResults.SelectMany(result => result));
            }

            return activities
                .OrderByDescending(activity => activity.EventTime)
                .Take(limit)
                .ToList();
        }

        private static UserActivityEvent MapActivity(Row row) => new()
        {
            UserId = row.GetValue<Guid?>("user_id") ?? Guid.Empty,
            SessionId = row.GetValue<Guid?>("session_id") ?? Guid.Empty,
            EventTime = row.GetValue<DateTimeOffset>("timestamp").DateTime,
            CourseId = row.GetValue<Guid?>("course_id") ?? Guid.Empty,
            ActionType = row.GetValue<string>("action_type") ?? string.Empty,
            DurationSec = row.GetValue<int?>("duration_sec") ?? 0,
            PageUrl = row.GetValue<string>("page_url") ?? string.Empty,
            DeviceType = row.GetValue<string>("device_type") ?? "web"
        };

        public async Task<int> GetTotalTimeOnCourseAsync(Guid userId, Guid courseId)
        {
            var cql = "SELECT duration_sec FROM user_activities WHERE user_id = ? AND course_id = ?";
            var rows = await _session.ExecuteAsync(new SimpleStatement(cql, userId, courseId));
            return rows.Sum(r => r.GetValue<int?>("duration_sec") ?? 0);
        }
    }
}