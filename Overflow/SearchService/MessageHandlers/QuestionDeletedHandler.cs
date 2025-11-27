using Contracts;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SearchService.Models;
using System.Text;
using Typesense;

namespace SearchService.MessageHandlers
{
    public class QuestionDeletedHandler(ITypesenseClient client, 
        ILogger<QuestionDeletedHandler> logger, 
        IConnection _messageConnection) : BackgroundService
    {
        private IModel? _messageChannel;
        private EventingBasicConsumer consumer;
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            string queueName = Contracts.Contanst.QuestionDeletedQueue;

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

        private void ProcessMessageAsync(object? sender, BasicDeliverEventArgs args)
        {
            // var message = args.Body;
            string message = Encoding.UTF8.GetString(args.Body.ToArray());
            var messageData = System.Text.Json.JsonSerializer.Deserialize<QuestionDeleted>(message);

            client.DeleteDocument<SearchQuestion>("questions", messageData.QuestionId).GetAwaiter().GetResult();

            Console.WriteLine($"Created question with id {messageData.QuestionId}");
            logger.LogInformation("Message retrieved from queue at {now}. Message Text: {text}", DateTime.Now, message);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            consumer.Received -= ProcessMessageAsync;
            _messageChannel?.Dispose();
        }
    }
}
