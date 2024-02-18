using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;

namespace SSRabbitMQBroker;
public class RabbitMQProviderOptions<T>
    where T : IRabbitMQProvider
{
    public ILogger<T> Logger { get; set; }
    public IConnectionFactory ConnectionFactory { get; set; }
    public bool ShouldRetry => this.RetryCount > 0;
    public int RetryCount { get; set; } = 5;
    public string ClientName { get; set; } = null;
}
