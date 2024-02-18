using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace SSRabbitMQBroker;

public class RabbitMQBroker : IRabbitMQBroker, IDisposable
{
    private readonly RabbitMQBrokerOptions<RabbitMQBroker> options;
    private IModel channel;
    public RabbitMQBroker(RabbitMQBrokerOptions<RabbitMQBroker> options)
    {
        this.options = options;
        this.channel = this.CreateChannel();
    }

    /// <summary>
    /// Publish a message
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="message">The message</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void Publish<TMessage>(TMessage message)
        where TMessage : IMessage
    {
        if (message is null)
        {
            throw new ArgumentNullException("The published message is null");
        }

        using (this.options.Logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = message.CorrelationId
        }))
        {
            if (string.IsNullOrEmpty(message.Id))
            {
                message.Id = Guid.NewGuid().ToString();
            }

            var policy = RetryPolicyFactory.Create(this.options.Logger, this.options.RetryCount);

            var serialized = JsonSerializer.Serialize(message);
            this.options.Logger.LogInformation("Publish message: {messageName} (id={id}, CorrelationId: {correlationId}, Discriminator: {discriminator})", nameof(message), message.Id, message.CorrelationId, message.Discriminator);

            if (this.options.ShouldRetry)
            {
                policy.Execute(() =>
                {
                    this.channel.BasicPublish(
                        exchange: this.options.ExchangeName,
                        routingKey: this.options.RoutingKey,
                        basicProperties: this.CreateBasicProperties(message),
                        body: Encoding.UTF8.GetBytes(serialized));
                });
            }
            else
            {
                this.channel.BasicPublish(
                        exchange: this.options.ExchangeName,
                        routingKey: this.options.RoutingKey,
                        basicProperties: null,
                        body: Encoding.UTF8.GetBytes(serialized));
            }
        }
    }

    /// <summary>
    /// Subscribe a message type with a handler
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    public void Subscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        var messageName = typeof(TMessage).Name;

        if (!this.options.BrokerSubscriber.Exists<TMessage>())
        {
            var handlerName = typeof(THandler).Name;
            this.options.Logger.LogInformation("Start adding Subscriber: (Message: {message}, Handler: {handler}, Queue: {queue})", messageName, handlerName, this.options.QueueName);

            this.AddBasicConsumer(messageName);
            this.options.BrokerSubscriber.Add<TMessage, THandler>();
        }
    }

    /// <summary>
    /// Unsubscribe a message type
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    public void Unsubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>
    {
        var messageName = typeof(TMessage).Name;
        if (!this.options.BrokerSubscriber.Exists<TMessage>())
        {
            this.options.Logger.LogInformation("Unsubscribe: (Message: {message}, Queue: {queue})", messageName, this.options.QueueName);

            channel.QueueUnbind(
                   exchange: this.options.ExchangeName,
                   queue: this.options.QueueName,
                   routingKey: this.options.RoutingKey);
            this.options.BrokerSubscriber.Remove<TMessage, THandler>();
            this.options.Logger.LogInformation("Consumer unsubscribed successfully");
        }
    }

    public void Dispose()
    {
        this.options?.BrokerSubscriber?.Clear();
    }

    private IModel CreateChannel()
    {
        if (this.options.Provider.IsClosed)
        {
            this.options.Provider.TryConnect();
        }

        var queueName = this.options.QueueName;
        var exchangeName = this.options.ExchangeName;
        var routingKey = this.options.RoutingKey;
        this.options.Logger.LogInformation("Start creating RabbitMQ Channel (exchange={exchange}, queue={queue})", exchangeName, queueName);

        try
        {
            var channel = this.options.Provider.CreateModel();
            this.options.Logger.LogInformation("Start declaring Exchange (exchange={exchange}, durable={durable}, autoDelete={autoDelete})", exchangeName, this.options.ExchangeDurable, this.options.ExchangeAutoDelete);
            channel.ExchangeDeclare(exchange: exchangeName, type: "direct", durable: this.options.ExchangeDurable, autoDelete: this.options.ExchangeAutoDelete, arguments: null);

            this.options.Logger.LogInformation("Exchange declared successfully");

            this.options.Logger.LogInformation("Start declaring Queue (queue={queue}, durable={durable}, exclusive={exclusive}, autoDelete={autoDelete})", queueName, this.options.QueueDurable, this.options.QueueExclusive, this.options.QueueAutoDelete);
            channel.QueueDeclare(
                queue: queueName,
                durable: this.options.QueueDurable,
                exclusive: this.options.QueueExclusive,
                autoDelete: this.options.QueueAutoDelete,
                arguments: null);

            channel.QueueBind(queueName, exchangeName, routingKey, null);

            this.options.Logger.LogInformation("RabbitMQ Channel created successfully (exchange={exchange}, queue={queue})", exchangeName, queueName);
            return channel;
        }
        catch (Exception ex)
        {
            this.options.Logger.LogError("RabbitMQ channel cannot be created (exchange={exchange}, queue={queue}) (Exception: {exception})", exchangeName, queueName, ex.Message);
            throw;
        }
    }

    private IBasicProperties CreateBasicProperties<TMessage>(TMessage message)
        where TMessage : IMessage
    {
        var properties = this.channel.CreateBasicProperties();
        properties.DeliveryMode = 2; // persistent
        properties.Persistent = true;
        properties.Type = nameof(message);
        properties.MessageId = message.Id;
        properties.CorrelationId = message.CorrelationId;
        if (this.options.ExpirationTime.HasValue)
        {
            properties.Expiration = this.options.ExpirationTime.Value.Milliseconds.ToString();
        }

        return properties;
    }

    private void AddBasicConsumer(string messageName)
    {
        if (this.channel is null)
        {
            this.options.Logger.LogError("Couldn't find the channel");
            return;
        }

        this.options.Logger.LogInformation("Start RabbitMQ Consumer (exchange={exchange}, queue={queue})", this.options.ExchangeName, this.options.QueueName);
        var consumer = new AsyncEventingBasicConsumer(this.channel);
        consumer.Received += async (sender, args) =>
        {
            try
            {
                if (await this.ProcessMessage(messageName, args).ConfigureAwait(false))
                {
                    //this.channel.BasicAck(args.DeliveryTag, multiple: false);
                }
            }
            catch (Exception ex)
            {
                this.options.Logger.LogError("Cannot find RabbitMQ Message Handler (name={messageName}, id={id}) (Exception: {exception})", messageName, args.BasicProperties.MessageId, ex.Message);
            }
        };

        this.channel.BasicConsume(
            queue: this.options.QueueName,
            autoAck: true,
            consumer: consumer);

        this.options.Logger.LogInformation("Start RabbitMQ Consumer added successfully");
    }

    private async Task<bool> ProcessMessage(string messageName, BasicDeliverEventArgs args)
    {
        if (this.options.BrokerSubscriber.Exists(messageName))
        {
            return false;
        }
        this.options.Logger.LogInformation("Check if Message Subscriber is registered");
        var messageType = this.options.BrokerSubscriber.GetMessageType(messageName);
        if (messageType is null)
        {
            this.options.Logger.LogInformation("Message Subscriber (Message: {message}) is not registered ", messageType);
            return false;
        }

        this.options.Logger.LogInformation("Message Subscriber is registered. Getting all handlers");
        using (this.options.Logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = args.BasicProperties.CorrelationId,
        }))
        {
            foreach (var handlerType in this.options.BrokerSubscriber.GetAll(messageName))
            {
                if (handlerType is null)
                {
                    continue;
                }

                this.options.Logger.LogInformation("Message Handler (Handler: {handler}) was found", handlerType);

                if (!(JsonSerializer.Deserialize(args.Body.ToArray(), messageType) is IMessage message))
                {
                    return false;
                }

                // Create handler
                // Handle message
                var handler = Activator.CreateInstance(handlerType);
                if (handler is not null)
                {
                    continue;
                }

                var concreteType = typeof(IMessageHandler<>).MakeGenericType(messageType);
                var method = concreteType.GetMethod("ProcessAsync");
                if (method is not null)
                {
                    continue;
                }

                this.options.Logger.LogInformation("Process Method was found in the Handler. Invoking");
                await ((Task)method.Invoke(handler, new object[] { message })).ConfigureAwait(false);
                this.options.Logger.LogInformation("Process Method was successfully processed");
            }
        }

        this.options.Logger.LogInformation("All Handlers were successfully processed");

        return true;
    }
}
