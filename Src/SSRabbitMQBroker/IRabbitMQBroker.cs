namespace SSRabbitMQBroker;

public interface IRabbitMQBroker
{
    /// <summary>
    /// Publish a message
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="message">The message</param>
    /// <exception cref="ArgumentNullException"></exception>
    void Publish<TMessage>(TMessage message)
        where TMessage : IMessage;

    /// <summary>
    /// Subscribe a message type with a handler
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    void Subscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>;

    /// <summary>
    /// Unsubscribe a message type
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <typeparam name="THandler"></typeparam>
    void Unsubscribe<TMessage, THandler>()
        where TMessage : IMessage
        where THandler : IMessageHandler<TMessage>;
}
