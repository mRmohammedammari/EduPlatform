using EduPlatform.Core.Models;
using EduPlatform.Data.Cassandra.Repositories;
using EduPlatform.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EduPlatform.API.Services
{
    public class ChatbotService
    {
        private readonly HttpClient _http;
        private readonly TestResultRepository _testRepo;
        private readonly EduDbContext _db;
        private readonly string _apiKey;
        private readonly string _model;

        public ChatbotService(
            IHttpClientFactory httpFactory,
            TestResultRepository testRepo,
            EduDbContext db,
            IConfiguration config)
        {
            _http = httpFactory.CreateClient("openai");
            _testRepo = testRepo;
            _db = db;
            _apiKey = config["OpenAI:ApiKey"]!;
            _model = config["OpenAI:Model"] ?? "gpt-4o-mini";
        }

        public async Task<string> GetResponseAsync(
            Guid userId,
            Guid? currentCourseId,
            string userMessage,
            List<ChatMessage> conversationHistory)
        {
            // Construire le prompt système avec contexte
            var systemPrompt = await BuildSystemPromptAsync(userId, currentCourseId);

            // Construire les messages pour OpenAI
            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            foreach (var msg in conversationHistory.TakeLast(10))
                messages.Add(new { role = msg.Role, content = msg.Content });

            messages.Add(new { role = "user", content = userMessage });

            // Appeler OpenAI
            var requestBody = new
            {
                model = _model,
                messages,
                max_tokens = 500,
                temperature = 0.7
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return "Désolé, je ne peux pas répondre pour le moment.";

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OpenAIResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Choices?.FirstOrDefault()?.Message?.Content
                ?? "Désolé, je n'ai pas pu répondre.";
        }

        private async Task<string> BuildSystemPromptAsync(
            Guid userId, Guid? courseId)
        {
            var user = await _db.Users.FindAsync(userId);
            var sb = new StringBuilder();

            sb.AppendLine("Tu es un assistant pédagogique bienveillant.");
            sb.AppendLine("Tu aides les étudiants à comprendre les cours.");
            sb.AppendLine("Réponds toujours en français, clairement.");
            sb.AppendLine($"Tu parles à {user?.FirstName ?? "un étudiant"}.");

            if (courseId.HasValue)
            {
                var course = await _db.Courses
                    .Include(c => c.Modules)
                    .FirstOrDefaultAsync(c => c.Id == courseId);

                if (course != null)
                {
                    sb.AppendLine($"Cours actuel : '{course.Title}'");
                    sb.AppendLine($"Catégorie : {course.Category}");
                    sb.AppendLine($"Niveau : {course.Level}");
                }
            }

            sb.AppendLine("Ne donne JAMAIS les réponses aux questions de test.");
            return sb.ToString();
        }

        private class OpenAIResponse
        {
            public List<Choice>? Choices { get; set; }
            public class Choice
            {
                public Message? Message { get; set; }
            }
            public class Message
            {
                public string? Content { get; set; }
            }
        }
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
}