namespace SSRabbitMQSender;
public class EchoMessage(string message) : IMessage
{
    public string Message { get; set; } = message;
    public string Id { get; set; } = Guid.NewGuid().ToString();
}
