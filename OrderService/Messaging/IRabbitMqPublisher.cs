namespace OrderService.Messaging;

public interface IRabbitMqPublisher
{
    void Publish<T>(T message, string queueName);
}