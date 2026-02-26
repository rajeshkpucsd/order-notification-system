using System.Text;
using System.Text.Json;
using OrderService.Events;
using OrderService.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace OrderService.Messaging;

public class OrderEmailSentConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderEmailSentConsumer> _logger;
    private IConnection _connection;
    private IModel _channel;

    public OrderEmailSentConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OrderEmailSentConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("RabbitMq");
        var hostName = section.GetValue<string>("HostName") ?? "localhost";
        var port = section.GetValue<int?>("Port") ?? 5672;
        var userName = section.GetValue<string>("UserName") ?? "guest";
        var password = section.GetValue<string>("Password") ?? "guest";
        var startupRetryCount = section.GetValue<int?>("StartupRetryCount") ?? 5;
        var startupRetryDelaySeconds = section.GetValue<int?>("StartupRetryDelaySeconds") ?? 5;

        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port,
            DispatchConsumersAsync = true
        };

        Exception? lastException = null;
        for (var attempt = 1; attempt <= startupRetryCount && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                _logger.LogInformation("Connecting to RabbitMQ. Attempt {Attempt}/{MaxAttempts}",
                    attempt, startupRetryCount);

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _logger.LogInformation("RabbitMQ connected at {Host}:{Port}", hostName, port);
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex,
                    "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s",
                    attempt, startupRetryCount, startupRetryDelaySeconds);

                if (attempt < startupRetryCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(startupRetryDelaySeconds), stoppingToken);
                }
            }
        }

        if (_connection == null || _channel == null)
        {
            throw new InvalidOperationException(
                $"Unable to connect to RabbitMQ after {startupRetryCount} attempts.",
                lastException);
        }

        _channel.ExchangeDeclare(
            exchange: "orders.exchange",
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null);

        _channel.QueueDeclare(
            queue: "order-service",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.QueueBind(
            queue: "order-service",
            exchange: "orders.exchange",
            routingKey: "email-sent");

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (sender, ea) =>
        {
            if (stoppingToken.IsCancellationRequested)
            {
                _channel.BasicAck(ea.DeliveryTag, false);
                return;
            }

            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var evt = JsonSerializer.Deserialize<OrderEmailSentEvent>(json);
                if (evt == null)
                {
                    _logger.LogWarning("Invalid message payload: null event.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

                var updated = await repo.UpdateStatusAsync(evt.OrderId, "EMAIL_SENT");
                if (!updated)
                {
                    _logger.LogWarning("Order {OrderId} not found when applying EMAIL_SENT.", evt.OrderId);
                }
                else
                {
                    _logger.LogInformation("Order {OrderId} status updated to EMAIL_SENT.", evt.OrderId);
                }

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing RabbitMQ message. DeliveryTag {DeliveryTag}",
                    ea.DeliveryTag);
                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: "order-service",
            autoAck: false,
            consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _channel?.Close();
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing RabbitMQ connection");
        }

        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
