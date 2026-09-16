using EduPlatform.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace EduPlatform.BigData.Analytics
{
    public class RecommendationService
    {
        private readonly MLContext _mlContext;
        private readonly EduDbContext _db;
        private ITransformer? _model;

        public RecommendationService(EduDbContext db)
        {
            _mlContext = new MLContext(seed: 42);
            _db = db;
        }

        public class CourseRating
        {
            public float UserId { get; set; }
            public float CourseId { get; set; }
            public float Label { get; set; }
        }

        public class CoursePrediction
        {
            public float Score { get; set; }
        }

        public async Task TrainModelAsync()
        {
            var enrollments = await _db.Enrollments
                .Select(e => new CourseRating
                {
                    UserId = Math.Abs(e.UserId.GetHashCode() % 100000),
                    CourseId = Math.Abs(e.CourseId.GetHashCode() % 100000),
                    Label = 3.0f
                })
                .ToListAsync();

            if (!enrollments.Any())
            {
                Console.WriteLine("[ML.NET] Pas assez de données pour entraîner.");
                return;
            }

            var trainingData = _mlContext.Data.LoadFromEnumerable(enrollments);

            var options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = nameof(CourseRating.UserId),
                MatrixRowIndexColumnName = nameof(CourseRating.CourseId),
                LabelColumnName = nameof(CourseRating.Label),
                NumberOfIterations = 20,
                ApproximationRank = 100
            };

            var pipeline = _mlContext.Recommendation()
                .Trainers.MatrixFactorization(options);

            _model = pipeline.Fit(trainingData);
            Console.WriteLine("[ML.NET] Modèle entraîné avec succès.");
        }

        public List<Guid> GetRecommendedCourses(
            Guid userId, List<Guid> availableCourseIds, int topN = 5)
        {
            if (_model == null || !availableCourseIds.Any())
                return availableCourseIds.Take(topN).ToList();

            var predEngine = _mlContext.Model
                .CreatePredictionEngine<CourseRating, CoursePrediction>(_model);

            var scores = availableCourseIds.Select(courseId => new
            {
                CourseId = courseId,
                Score = predEngine.Predict(new CourseRating
                {
                    UserId = Math.Abs(userId.GetHashCode() % 100000),
                    CourseId = Math.Abs(courseId.GetHashCode() % 100000)
                }).Score
            });

            return scores
                .OrderByDescending(s => s.Score)
                .Take(topN)
                .Select(s => s.CourseId)
                .ToList();
        }
    }
}