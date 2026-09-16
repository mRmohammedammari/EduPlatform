using EduPlatform.API.Services;
using EduPlatform.Data.Cassandra.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatbotController : ControllerBase
    {
        private readonly ChatbotService _chatbot;
        private readonly ChatRepository _chatRepo;

        public ChatbotController(ChatbotService chatbot, ChatRepository chatRepo)
        {
            _chatbot = chatbot;
            _chatRepo = chatRepo;
        }

        [HttpPost("message")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequestDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            // Récupérer l'historique
            var history = await _chatRepo.GetMessagesAsync(userId, dto.SessionId);

            // Convertir vers ChatMessage de l'API
            var apiHistory = history.Select(m => new EduPlatform.API.Services.ChatMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList();

            // Obtenir la réponse du chatbot
            var response = await _chatbot.GetResponseAsync(
                userId, dto.CourseId, dto.Message, apiHistory);

            // Mettre à jour l'historique
            history.Add(new EduPlatform.Data.Cassandra.Repositories.ChatMessage
            { Role = "user", Content = dto.Message });
            history.Add(new EduPlatform.Data.Cassandra.Repositories.ChatMessage
            { Role = "assistant", Content = response });

            await _chatRepo.SaveMessagesAsync(userId, dto.SessionId, history);

            return Ok(new
            {
                message = response,
                sessionId = dto.SessionId,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("history/{sessionId:guid}")]
        public async Task<IActionResult> GetHistory(Guid sessionId)
        {
            var userId = Guid.Parse(User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

            var messages = await _chatRepo.GetMessagesAsync(userId, sessionId);
            return Ok(messages);
        }
    }

    public class ChatRequestDto
    {
        public Guid SessionId { get; set; }
        public Guid? CourseId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}