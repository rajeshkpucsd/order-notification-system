using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace OrderService.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IConfiguration _configuration;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Publish<T>(T message, string queueName)
    {
        var factory = new ConnectionFactory()
        {
            HostName = "localhost"
        };

        using var _connection = factory.CreateConnection();
        using var _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.DeliveryMode = 2; // persistent

        _channel.BasicPublish(
            exchange: "", // Default exchange
            routingKey: queueName,
            basicProperties: properties,
            body: body);
    }
}
