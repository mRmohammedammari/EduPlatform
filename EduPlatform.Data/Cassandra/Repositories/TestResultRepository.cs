using Cassandra;
using EduPlatform.Core.Models;

namespace EduPlatform.Data.Cassandra.Repositories
{
    public class TestResultRepository
    {
        private readonly ISession _session;
        private readonly PreparedStatement _insertResult;
        private readonly PreparedStatement _selectByCourse;

        public TestResultRepository(CassandraContext context)
        {
            _session = context.Session;

            _insertResult = _session.Prepare(
                "INSERT INTO test_results (course_id, user_id, taken_at, score, max_score, passed, answers) " +
                "VALUES (?, ?, ?, ?, ?, ?, ?)");

            _selectByCourse = _session.Prepare(
                "SELECT * FROM test_results WHERE course_id = ?");
        }

        public async Task SaveResultAsync(TestResult result)
        {
            var answers = result.Answers.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString());

            var bound = _insertResult.Bind(
                result.CourseId, result.UserId, result.TakenAt,
                result.Score, result.MaxScore, result.Passed, answers);

            await _session.ExecuteAsync(bound);
        }

        public async Task<List<TestResult>> GetResultsByCourseAsync(Guid courseId)
        {
            var bound = _selectByCourse.Bind(courseId);
            var rows = await _session.ExecuteAsync(bound);

            return rows.Select(row => new TestResult
            {
                CourseId = row.GetValue<Guid>("course_id"),
                UserId = row.GetValue<Guid>("user_id"),
                TakenAt = row.GetValue<DateTimeOffset>("taken_at").DateTime,
                Score = row.GetValue<decimal>("score"),
                MaxScore = row.GetValue<decimal>("max_score"),
                Passed = row.GetValue<bool>("passed")
            }).ToList();
        }

        public async Task<decimal> GetBestScoreAsync(Guid userId, Guid courseId)
        {
            var cql = "SELECT score, max_score FROM test_results WHERE course_id = ? AND user_id = ?";
            var rows = await _session.ExecuteAsync(new SimpleStatement(cql, courseId, userId));
            var best = rows.Select(r => r.GetValue<decimal>("score")).DefaultIfEmpty(0).Max();
            return best;
        }

        public async Task<TestResult?> GetBestPassedResultAsync(Guid userId, Guid courseId)
        {
            var cql = "SELECT score, max_score, passed, taken_at FROM test_results " +
                "WHERE course_id = ? AND user_id = ?";
            var rows = await _session.ExecuteAsync(new SimpleStatement(cql, courseId, userId));

            return rows
                .Where(row => row.GetValue<bool>("passed"))
                .Select(row => new TestResult
                {
                    CourseId = courseId,
                    UserId = userId,
                    Score = row.GetValue<decimal>("score"),
                    MaxScore = row.GetValue<decimal>("max_score"),
                    Passed = true,
                    TakenAt = row.GetValue<DateTimeOffset>("taken_at").DateTime
                })
                .OrderByDescending(result => result.Score / Math.Max(result.MaxScore, 1))
                .FirstOrDefault();
        }
    }
}