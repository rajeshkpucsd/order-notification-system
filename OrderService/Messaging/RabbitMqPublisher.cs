using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderService.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public RabbitMqPublisher()
    {
        // Configure connection factory
        var factory = new ConnectionFactory()
        {
            HostName = "rabbitmq",
            UserName = "guest",
            Password = "guest",
            Port = 5672
        };

        // retry until broker ready
        while (true)
        {
            try
            {
                Console.WriteLine("Connecting to RabbitMQ Publisher...");
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                Console.WriteLine("Publisher connected to RabbitMQ");
                break;
            }
            catch
            {
                Console.WriteLine("RabbitMQ not ready for publisher, retrying...");
                Thread.Sleep(5000);
            }
        }
    }

    public void Publish<T>(T message, string queueName)
    {
        // Ensure the queue exists before publishing
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

        // Publish the message to the specified queue
        _channel.BasicPublish(
            exchange: "",
            routingKey: queueName,
            basicProperties: properties,
            body: body);

        Console.WriteLine($"Message published to {queueName}");
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}