using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Events;
using NotificationService.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Messaging;

public class RabbitMqConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqConsumer> _logger;
    private IConnection _connection;
    private IModel _channel;

    public RabbitMqConsumer(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RabbitMqConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ connection setup
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
        // Declare the queue
        _channel.QueueDeclare(
            queue: "order-created",
            durable: true, // Restart safe
            exclusive: false, //Allow multiple
            autoDelete: false, // No delete
            arguments: null);

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
                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);
                if (evt == null)
                {
                    _logger.LogWarning("Invalid message payload: null event.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                var exists = await db.Notifications.AnyAsync(n => n.EventId == evt.EventId);
                if (exists)
                {
                    _logger.LogInformation("Duplicate event {EventId} ignored.", evt.EventId);
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }                

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    EventId = evt.EventId,
                    OrderId = evt.OrderId,
                    Email = evt.Email,
                    Delivered = true,
                    CreatedAt = evt.CreatedAt
                };

                db.Notifications.Add(notification);
                await db.SaveChangesAsync();

                _logger.LogInformation("Email sent for EventId {EventId} to email {Email}",
                    evt.EventId, evt.Email);
                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                _logger.LogInformation(ex,
                    "Duplicate notification insert detected. Acknowledging message. DeliveryTag {DeliveryTag}",
                    ea.DeliveryTag);
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
            queue: "order-created",
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

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            // 2601: Cannot insert duplicate key row in object with unique index
            // 2627: Violation of PRIMARY KEY or UNIQUE KEY constraint
            return sqlEx.Number is 2601 or 2627;
        }

        return false;
    }
}
