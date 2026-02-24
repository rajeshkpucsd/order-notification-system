using System.Text;
using System.Text.Json;
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
    private IConnection _connection;
    private IModel _channel;

    public RabbitMqConsumer(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;

        var factory = new ConnectionFactory()
        {
            HostName = "localhost"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
    queue: "order-created",
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: null);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new EventingBasicConsumer(_channel);

        consumer.Received += async (sender, ea) =>
        {
            var body = ea.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);

            try
            {
                var evt = JsonSerializer.Deserialize<OrderCreatedEvent>(json);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

                // Idempotency check
                var exists = await db.Notifications.AnyAsync(n => n.EventId == evt.EventId);
                if (exists)
                {
                    Console.WriteLine("Duplicate event ignored.");
                    _channel.BasicAck(ea.DeliveryTag, false);
                    return;
                }
                //Can send email here
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
                
                _channel.BasicAck(ea.DeliveryTag, false); 
            }
            catch (DbUpdateException)
            {
                Console.WriteLine("Duplicate event detected by DB constraint.");

                _channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Processing failed: " + ex.Message);

                _channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        _channel.BasicConsume(
            queue: "order-created",
            autoAck: false,
            consumer: consumer);

        return Task.CompletedTask;
    }
}