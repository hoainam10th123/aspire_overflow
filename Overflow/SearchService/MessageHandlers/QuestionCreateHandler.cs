using Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Models;
using System.Text;
using System.Text.RegularExpressions;
using Typesense;

namespace SearchService.MessageHandlers
{
    public class QuestionCreateHandler : BackgroundService
    {
        private readonly ILogger<QuestionCreateHandler> _logger;        
        private readonly IConnection _messageConnection;
        private IModel? _messageChannel;
        private EventingBasicConsumer consumer;
        private readonly ITypesenseClient _client;
        //private readonly IServiceProvider _serviceProvider;

        public QuestionCreateHandler(ILogger<QuestionCreateHandler> logger, 
            IConnection messageConnection, ITypesenseClient client)
        {
            _logger = logger;
            //_serviceProvider = serviceProvider;
            _messageConnection = messageConnection!;
            _client = client;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string queueName = Contracts.Contanst.QuestionCreatedQueue;
            //_messageConnection = _serviceProvider.GetRequiredService<IConnection>();

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
            string message = Encoding.UTF8.GetString(args.Body.ToArray());
            var messageData = System.Text.Json.JsonSerializer.Deserialize<QuestionCreated>(message);
            // Typesense không lưu DateTime dạng ISO string mà thường dùng Unix timestamp (số giây từ 1970).
            var created = new DateTimeOffset(messageData.Created).ToUnixTimeSeconds();

            var doc = new SearchQuestion
            {
                Id = messageData.QuestionId,
                Title = messageData.Title,
                Content = StripHtml(messageData.Content),
                CreatedAt = created,
                Tags = messageData.Tags.ToArray(),
            };
            _client.CreateDocument("questions", doc).GetAwaiter().GetResult();

            Console.WriteLine($"Created question with id {messageData.QuestionId}");
            _logger.LogInformation("Message retrieved from queue at {now}. Message Text: {text}", DateTime.Now, message);
            // var message = args.Body;
        }

        private static string StripHtml(string content)
        {
            return Regex.Replace(content, "<.*?>", string.Empty);
        }
    }
}
