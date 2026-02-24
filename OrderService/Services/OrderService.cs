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
    public OrderService(IOrderRepository repository, IRabbitMqPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderDto dto)
    {
        try
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
                // important: do NOT fail order
                Console.WriteLine($"RabbitMQ publish failed: {ex.Message}");
            }

            return order;
        }
        catch (Exception)
        {
            throw; // let middleware handle
        }
    }

    public async Task<Order> GetByIdAsync(Guid id)
    {
        var order = await _repository.GetByIdAsync(id);

        if (order == null)
            throw new KeyNotFoundException($"Order {id} not found");

        return order;
    }

    public async Task<List<Order>> GetAllAsync()
        => await _repository.GetAllAsync();
}