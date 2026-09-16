using Cassandra;
using System.Text.Json;

namespace EduPlatform.Data.Cassandra.Repositories
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class ChatRepository
    {
        private readonly ISession _session;
        private readonly PreparedStatement _saveMessages;
        private readonly PreparedStatement _getMessages;

        public ChatRepository(CassandraContext context)
        {
            _session = context.Session;

            _saveMessages = _session.Prepare(
                "UPDATE chatbot_sessions SET messages = ? " +
                "WHERE user_id = ? AND session_id = ?");

            _getMessages = _session.Prepare(
                "SELECT messages FROM chatbot_sessions " +
                "WHERE user_id = ? AND session_id = ?");
        }

        public async Task SaveMessagesAsync(
            Guid userId, Guid sessionId, List<ChatMessage> messages)
        {
            var serialized = messages
                .Select(m => JsonSerializer.Serialize(m))
                .ToList();

            var bound = _saveMessages.Bind(serialized, userId, sessionId);
            await _session.ExecuteAsync(bound);
        }

        public async Task<List<ChatMessage>> GetMessagesAsync(
            Guid userId, Guid sessionId)
        {
            var bound = _getMessages.Bind(userId, sessionId);
            var rows = await _session.ExecuteAsync(bound);
            var row = rows.FirstOrDefault();

            if (row == null) return new List<ChatMessage>();

            var rawMessages = row.GetValue<IEnumerable<string>>("messages");
            if (rawMessages == null) return new List<ChatMessage>();

            return rawMessages
                .Select(m => JsonSerializer.Deserialize<ChatMessage>(m)!)
                .Where(m => m != null)
                .ToList();
        }
    }
}