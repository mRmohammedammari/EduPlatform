using Confluent.Kafka;
using EduPlatform.Core.Models;
using EduPlatform.Data.Cassandra.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EduPlatform.BigData.Kafka
{
    public class ActivityConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<ActivityConsumer> _logger;
        private readonly IServiceProvider _serviceProvider;

        public ActivityConsumer(
            IConfiguration config,
            ILogger<ActivityConsumer> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = config["Kafka:BootstrapServers"],
                GroupId = "edu-platform-consumers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Yield();
            _consumer.Subscribe("user-activity-events");
            _logger.LogInformation("[Kafka] Consumer démarré...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
                    if (consumeResult == null) continue;

                    var evt = JsonSerializer.Deserialize<UserActivityEvent>(
                        consumeResult.Message.Value);

                    if (evt != null)
                    {
                        _logger.LogInformation(
                            "[Kafka] Événement reçu : {Action} par {User}",
                            evt.ActionType, evt.UserId);
                    }

                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "[Kafka] Consumer indisponible; nouvelle tentative");
                    try
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Kafka] Erreur consumer");
                }
            }

            _consumer.Close();
        }
    }
}