using OrderService.DTOs;
using OrderService.Models;

namespace OrderService.Services;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderDto dto);
    Task<Order?> GetByIdAsync(Guid id);
    Task<List<Order>> GetAllAsync();
}