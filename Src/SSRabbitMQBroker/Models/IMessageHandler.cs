namespace SSRabbitMQBroker
{
    public interface IMessageHandler<T>
        where T : IMessage
    {
        Task ProcessAsync(T message, CancellationToken cancellationToken);
    }
}
