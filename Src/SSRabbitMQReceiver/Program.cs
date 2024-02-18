using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SSRabbitMQSender;

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
using var provider = new RabbitMQProvider(new RabbitMQProviderOptions<RabbitMQProvider>
{
    ClientName = "client",
    ConnectionFactory = new ConnectionFactory
    {
        Uri = new Uri("amqp://guest:guest@localhost:5672")
    },
    Logger = loggerFactory.CreateLogger<RabbitMQProvider>(),
    RetryCount = 5
});

var broker = new RabbitMQBroker(new()
{
    Provider = provider,
    Logger = loggerFactory.CreateLogger<RabbitMQBroker>(),
    ExpirationTime = TimeSpan.FromHours(2),
});

broker.Subscribe<EchoMessage, EchoMessageHandler>();

Console.WriteLine("Hello, World!");