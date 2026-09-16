using Confluent.Kafka;
using EduPlatform.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace EduPlatform.BigData.Kafka
{
    public class EventProducer : IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly string _topicActivity;

        public EventProducer(IConfiguration config)
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"],
                Acks = Acks.All,
                MessageSendMaxRetries = 3
            };

            _producer = new ProducerBuilder<string, string>(producerConfig).Build();
            _topicActivity = config["Kafka:TopicActivity"]!;
        }

        public async Task PublishActivityAsync(UserActivityEvent evt)
        {
            var message = new Message<string, string>
            {
                Key = evt.UserId.ToString(),
                Value = JsonSerializer.Serialize(evt)
            };

            await _producer.ProduceAsync(_topicActivity, message);
        }

        public async Task PublishTestResultAsync(TestResult result)
        {
            var message = new Message<string, string>
            {
                Key = result.CourseId.ToString(),
                Value = JsonSerializer.Serialize(result)
            };

            await _producer.ProduceAsync("test-results-events", message);
        }

        public void Dispose() => _producer?.Dispose();
    }
}