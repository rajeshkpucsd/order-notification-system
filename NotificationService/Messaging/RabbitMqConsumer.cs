using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private IConnection _connection;
    private IModel _channel;

    public RabbitMqConsumer(IServiceScopeFactory scopeFactory, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // RabbitMQ connection setup
        var section = _configuration.GetSection("RabbitMq");
        var hostName = section.GetValue<string>("HostName") ?? "localhost";
        var port = section.GetValue<int?>("Port") ?? 5672;
        var userName = section.GetValue<string>("UserName") ?? "guest";
        var password = section.GetValue<string>("Password") ?? "guest";

        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            UserName = userName,
            Password = password,
            Port = port,
            DispatchConsumersAsync = true
        };

        // Retry loop until RabbitMQ is ready
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine("Attempting RabbitMQ connection...");

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                Console.WriteLine("RabbitMQ Connected!");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RabbitMQ not ready: {ex.Message}");
                await Task.Delay(5000, stoppingToken);
            }
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
                    Console.WriteLine("Invalid message payload: null event.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                var exists = await db.Notifications.AnyAsync(n => n.EventId == evt.EventId);
                if (exists)
                {
                    Console.WriteLine("Duplicate event ignored.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }

                await Task.Delay(1000);

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

                Console.WriteLine($"Email sent to {evt.Email}");

                _channel.BasicAck(ea.DeliveryTag, false); // Acknowledge message
            }
            catch (Exception ex)
            {
                Console.WriteLine("Processing failed: " + ex.Message);
                _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue message
            }
        };
        // Start consuming messages
        _channel.BasicConsume(
            queue: "order-created",
            autoAck: false, // Manual acknowledgment
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
            Console.WriteLine("Error closing RabbitMQ connection: " + ex.Message);
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
