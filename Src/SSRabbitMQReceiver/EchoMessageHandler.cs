namespace SSRabbitMQSender;
public class EchoMessageHandler : IMessageHandler<EchoMessage>
{
    public EchoMessageHandler()
    {
    }

    public async Task ProcessAsync(EchoMessage message, CancellationToken cancellationToken)
    {
        await Task.Delay(1, cancellationToken);
        Console.WriteLine(message.Message);
    }
}
