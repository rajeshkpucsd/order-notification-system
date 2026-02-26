namespace NotificationService.Messaging;

public interface IRabbitMqPublisher
{
    void Publish<T>(T message, string exchangeName, string routingKey);
}
