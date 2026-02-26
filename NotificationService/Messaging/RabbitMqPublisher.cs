using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace NotificationService.Messaging;

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(IConfiguration configuration, ILogger<RabbitMqPublisher> logger)
    {
        _logger = logger;

        var section = configuration.GetSection("RabbitMq");
        var hostName = section.GetValue<string>("HostName") ?? "localhost";
        var port = section.GetValue<int?>("Port") ?? 5672;
        var userName = section.GetValue<string>("UserName") ?? "guest";
        var password = section.GetValue<string>("Password") ?? "guest";
        var startupRetryCount = section.GetValue<int?>("StartupRetryCount") ?? 5;
        var startupRetryDelaySeconds = section.GetValue<int?>("StartupRetryDelaySeconds") ?? 5;

        var factory = new ConnectionFactory
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port
        };

        Exception? lastException = null;
        for (var attempt = 1; attempt <= startupRetryCount; attempt++)
        {
            try
            {
                _logger.LogInformation("Connecting to RabbitMQ publisher. Attempt {Attempt}/{MaxAttempts}",
                    attempt, startupRetryCount);

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _logger.LogInformation("Publisher connected to RabbitMQ at {Host}:{Port}", hostName, port);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s",
                    attempt, startupRetryCount, startupRetryDelaySeconds);

                if (attempt < startupRetryCount)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(startupRetryDelaySeconds));
                }
            }
        }

        throw new InvalidOperationException(
            $"Unable to connect to RabbitMQ after {startupRetryCount} attempts.",
            lastException);
    }

    public void Publish<T>(T message, string exchangeName, string routingKey)
    {
        _channel.ExchangeDeclare(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        var json = JsonSerializer.Serialize(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = _channel.CreateBasicProperties();
        properties.DeliveryMode = 2;

        _channel.BasicPublish(
            exchange: exchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Message published to exchange {ExchangeName} with routing key {RoutingKey}",
            exchangeName, routingKey);
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
