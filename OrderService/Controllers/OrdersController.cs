using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;
using OrderService.Exceptions;
using OrderService.Models;
using OrderService.Responses;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDto dto)
    {
        var result = await _service.CreateOrderAsync(dto);

        if (!result.EventPublished)
        {
            return Ok(ApiResponse<Order>.SuccessResponse(
                result.Order,
                "Order created successfully, but notification service is currently unavailable."
            ));
        }

        return Ok(ApiResponse<Order>.SuccessResponse(
            result.Order,
            "Order created successfully."
        ));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var orders = await _service.GetAllAsync();
        return Ok(ApiResponse<List<Order>>.SuccessResponse(orders));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var order = await _service.GetByIdAsync(id);

        return Ok(ApiResponse<Order>.SuccessResponse(order));
    }
}
