using Newtonsoft.Json;

namespace SSRabbitMQBroker
{
    public interface IMessage
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "correlationId")]
        public string CorrelationId => Guid.NewGuid().ToString();

        [JsonProperty(PropertyName = "discriminator")]
        public string Discriminator => GetType().FullName ?? string.Empty;
    }
}
