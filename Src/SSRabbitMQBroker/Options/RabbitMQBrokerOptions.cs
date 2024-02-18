using Microsoft.Extensions.Logging;

namespace SSRabbitMQBroker;

public class RabbitMQBrokerOptions<T>
    where T : IRabbitMQBroker
{
    public IRabbitMQProvider Provider { get; set; }
    public ILogger<T> Logger{ get; set; }
    public string QueueName { get; set; } = $"{typeof(T).Name}_Queue";
    public bool QueueDurable { get; set; } = false;
    public bool QueueExclusive { get; set; } = false;
    public bool QueueAutoDelete { get; set; } = false;
    public BrokerSubscriber BrokerSubscriber { get; set; } = new BrokerSubscriber();
    public string ExchangeName { get; set; } = $"{typeof(T).Name}_Exchange";
    public string RoutingKey { get; set; } = $"{typeof(T).Name}_RoutingKey";
    public bool ExchangeDurable { get; set; } = false;
    public bool ExchangeAutoDelete { get; set; } = false;
    public bool ShouldRetry => this.RetryCount > 0;
    public int RetryCount { get; set; } = 5;
    public TimeSpan? ExpirationTime { get; set; }
}
