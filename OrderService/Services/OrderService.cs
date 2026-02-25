using OrderService.DTOs;
using OrderService.Events;
using OrderService.Exceptions;
using OrderService.Messaging;
using OrderService.Models;
using OrderService.Repositories;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IRabbitMqPublisher _publisher;
    private readonly ILogger<OrderService> _logger;
    public OrderService(IOrderRepository repository, IRabbitMqPublisher publisher, ILogger<OrderService> logger)
    {
        _repository = repository;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderDto dto)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerEmail = dto.CustomerEmail,
            ProductCode = dto.ProductCode,
            Quantity = dto.Quantity,
            Status = "CREATED",
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(order);

        bool published = true;

        try
        {
            var evt = new OrderCreatedEvent
            {
                EventId = Guid.NewGuid(),
                OrderId = order.Id,
                Email = order.CustomerEmail,
                ProductCode = order.ProductCode,
                Quantity = order.Quantity
            };

            _publisher.Publish(evt,"order-created");
        }
        catch (Exception ex)
        {
            published = false;
            _logger.LogError(ex, "Failed to publish OrderCreated event for OrderId {OrderId}", order.Id);
        }

        return new OrderResult
        {
            Order = order,
            EventPublished = published
        };
    }

    public async Task<Order> GetByIdAsync(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);

        if (order == null)
            throw new NotFoundException($"Order {id} not found");

        return order;
    }

    public async Task<List<Order>> GetAllAsync()
        => await _repository.GetAllAsync();
}
