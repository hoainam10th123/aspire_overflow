using Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MessageHandlers
{
    public class QuestionUpdatedHandler : BackgroundService
    {
        private readonly ILogger<QuestionUpdatedHandler> _logger;
        private readonly IConnection _messageConnection;
        private IModel? _messageChannel;
        private EventingBasicConsumer consumer;
        private readonly ITypesenseClient _client;

        public QuestionUpdatedHandler(ILogger<QuestionUpdatedHandler> logger,
            IConnection messageConnection, ITypesenseClient client)
        {
            _logger = logger;
            _messageConnection = messageConnection!;
            _client = client;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string queueName = Contracts.Contanst.QuestionUpdatedQueue;

            _messageChannel = _messageConnection.CreateModel();
            _messageChannel.QueueDeclare(queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            consumer = new EventingBasicConsumer(_messageChannel);
            consumer.Received += ProcessMessageAsync;

            _messageChannel.BasicConsume(queue: queueName,
                autoAck: true,
                consumer: consumer);

            return Task.CompletedTask;
        }
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            consumer.Received -= ProcessMessageAsync;
            _messageChannel?.Dispose();
        }
        private void ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
        {
            // var message = args.Body;
            string message = Encoding.UTF8.GetString(args.Body.ToArray());
            var messageData = System.Text.Json.JsonSerializer.Deserialize<QuestionUpdated>(message);
            
            _client.UpdateDocument("questions", messageData.QuestionId, new
            {
                messageData.Title,
                Content = StripHtml(messageData.Content),
                Tags = messageData.Tags.ToArray(),
            }).GetAwaiter().GetResult();

            Console.WriteLine($"Created question with id {messageData.QuestionId}");
            _logger.LogInformation("Message retrieved from queue at {now}. Message Text: {text}", DateTime.Now, message);            
        }

        private static string StripHtml(string content)
        {
            return Regex.Replace(content, "<.*?>", string.Empty);
        }
    }

}
